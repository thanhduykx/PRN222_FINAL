using Microsoft.Extensions.Options;
using PRN222_FINAL.BLL.Options;
using PRN222_FINAL.BLL.Services.Billing.Gateways;
using PRN222_FINAL.DAL.Repositories.Billing;
using PRN222_FINAL.Models;
using PRN222_FINAL.Models.DTOs.Billing;

namespace PRN222_FINAL.BLL.Services.Billing;

public sealed class PaymentService : IPaymentService
{
    private readonly IPackageRepository _packages;
    private readonly IPaymentRepository _payments;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IMomoPaymentGateway _momoGateway;
    private readonly IPayOsPaymentGateway _payOsGateway;
    private readonly PaymentOptions _options;

    public PaymentService(
        IPackageRepository packages,
        IPaymentRepository payments,
        ISubscriptionRepository subscriptions,
        IMomoPaymentGateway momoGateway,
        IPayOsPaymentGateway payOsGateway,
        IOptions<PaymentOptions> options)
    {
        _packages = packages;
        _payments = payments;
        _subscriptions = subscriptions;
        _momoGateway = momoGateway;
        _payOsGateway = payOsGateway;
        _options = options.Value;
    }

    public async Task<PaymentCheckoutResultDto> CreateCheckoutAsync(CreatePaymentRequestDto request, CancellationToken cancellationToken = default)
    {
        ValidateCreateRequest(request);
        var package = await _packages.GetByIdAsync(request.PackageId, cancellationToken)
            ?? throw new InvalidOperationException("Package not found.");
        if (!package.IsActive)
        {
            throw new InvalidOperationException("Package is not active.");
        }

        var now = DateTimeOffset.UtcNow;
        var payment = new Payment
        {
            Id = Guid.NewGuid(),
            PackageId = package.Id,
            UserId = request.UserId,
            UserName = Normalize(request.UserName, 255),
            UserEmail = Normalize(request.UserEmail, 255),
            Provider = request.Provider,
            Status = PaymentStatus.Pending,
            AmountVnd = package.PriceVnd,
            Currency = "VND",
            OrderCode = GenerateOrderCode(request.Provider),
            CreatedAt = now
        };

        await _payments.AddAsync(payment, cancellationToken);

        if (package.PriceVnd <= 0)
        {
            payment.Status = PaymentStatus.Paid;
            payment.PaidAt = now;
            payment.CheckoutUrl = request.ReturnUrl;
            await _payments.UpdateAsync(payment, cancellationToken);
            await ActivateSubscriptionAsync(payment, package, false, cancellationToken);
            return ToCheckoutResult(payment, "Free package activated.");
        }

        var gatewayRequest = new PaymentGatewayCreateRequest
        {
            Provider = request.Provider,
            OrderCode = payment.OrderCode,
            PayOsOrderCode = BuildPayOsOrderCode(payment.OrderCode),
            AmountVnd = package.PriceVnd,
            Description = $"PRN222 {package.Code}",
            ReturnUrl = BuildReturnUrl(request.ReturnUrl, request.Provider, payment.OrderCode),
            CancelUrl = BuildReturnUrl(request.CancelUrl, request.Provider, payment.OrderCode),
            IpnUrl = BuildWebhookUrl(request.Provider),
            UserIpAddress = request.IpAddress
        };

        var gatewayResult = request.Provider switch
        {
            PaymentProvider.MoMo => await _momoGateway.CreateCheckoutAsync(gatewayRequest, cancellationToken),
            PaymentProvider.PayOS => await _payOsGateway.CreateCheckoutAsync(gatewayRequest, cancellationToken),
            _ => throw new InvalidOperationException("Unsupported payment provider.")
        };

        payment.ProviderTransactionId = gatewayResult.ProviderTransactionId;
        payment.CheckoutUrl = gatewayResult.CheckoutUrl;
        payment.QrCode = gatewayResult.QrCode;
        payment.RawRequest = gatewayResult.RawRequest;
        payment.RawResponse = gatewayResult.RawResponse;
        await _payments.UpdateAsync(payment, cancellationToken);

        return ToCheckoutResult(payment, "Checkout created.");
    }

    public async Task<PaymentWebhookResultDto> HandleWebhookAsync(PaymentWebhookDto webhook, CancellationToken cancellationToken = default)
    {
        var result = webhook.Provider switch
        {
            PaymentProvider.MoMo => _momoGateway.VerifyWebhook(webhook),
            PaymentProvider.PayOS => _payOsGateway.VerifyWebhook(webhook),
            _ => throw new InvalidOperationException("Unsupported payment provider.")
        };

        if (!result.IsSignatureValid)
        {
            throw new InvalidOperationException("Invalid payment webhook signature.");
        }

        if (string.IsNullOrWhiteSpace(result.OrderCode))
        {
            throw new InvalidOperationException("Payment order code is required.");
        }

        var payment = await _payments.GetByOrderCodeAsync(webhook.Provider, result.OrderCode, cancellationToken)
            ?? throw new InvalidOperationException("Payment not found.");
        var package = await _packages.GetByIdAsync(payment.PackageId, cancellationToken)
            ?? throw new InvalidOperationException("Package not found.");

        if (result.AmountVnd.HasValue && result.AmountVnd.Value != payment.AmountVnd)
        {
            throw new InvalidOperationException("Webhook amount does not match payment amount.");
        }

        if (payment.Status == PaymentStatus.Paid)
        {
            return new PaymentWebhookResultDto
            {
                PaymentId = payment.Id,
                OrderCode = payment.OrderCode,
                Status = payment.Status,
                IsDuplicate = true,
                SubscriptionActivated = await _subscriptions.GetByPaymentIdAsync(payment.Id, cancellationToken) is not null,
                Message = "Webhook already processed."
            };
        }

        payment.RawWebhook = webhook.RawBody;
        payment.ProviderTransactionId = string.IsNullOrWhiteSpace(result.ProviderTransactionId)
            ? payment.ProviderTransactionId
            : result.ProviderTransactionId;

        if (result.Status == PaymentStatus.Paid)
        {
            payment.Status = PaymentStatus.Paid;
            payment.PaidAt = DateTimeOffset.UtcNow;
            await _payments.UpdateAsync(payment, cancellationToken);
            await ActivateSubscriptionAsync(payment, package, true, cancellationToken);
        }
        else
        {
            payment.Status = result.Status == PaymentStatus.Canceled ? PaymentStatus.Canceled : PaymentStatus.Failed;
            payment.FailedAt = DateTimeOffset.UtcNow;
            payment.FailureReason = Normalize(result.Message, 1000);
            await _payments.UpdateAsync(payment, cancellationToken);
        }

        return new PaymentWebhookResultDto
        {
            PaymentId = payment.Id,
            OrderCode = payment.OrderCode,
            Status = payment.Status,
            SubscriptionActivated = payment.Status == PaymentStatus.Paid,
            Message = result.Message
        };
    }

    public async Task<PaymentReturnDto?> GetReturnStatusAsync(PaymentProvider provider, string orderCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderCode))
        {
            return null;
        }

        var payment = await _payments.GetByOrderCodeAsync(provider, orderCode, cancellationToken);
        return payment is null
            ? null
            : new PaymentReturnDto
            {
                Provider = provider,
                OrderCode = payment.OrderCode,
                ProviderTransactionId = payment.ProviderTransactionId,
                Status = payment.Status,
                Message = payment.Status switch
                {
                    PaymentStatus.Paid => "Payment completed.",
                    PaymentStatus.Pending => "Payment is waiting for confirmation.",
                    PaymentStatus.Canceled => "Payment was canceled.",
                    _ => payment.FailureReason
                }
            };
    }

    public async Task<IReadOnlyList<PaymentHistoryItemDto>> GetPaymentsForUserAsync(Guid userId, int limit = 20, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new InvalidOperationException("User is required.");
        }

        var payments = await _payments.GetByUserAsync(userId, Math.Clamp(limit, 1, 100), cancellationToken);
        return payments.Select(payment => new PaymentHistoryItemDto
        {
            PaymentId = payment.Id,
            PackageId = payment.PackageId,
            PackageName = payment.Package?.Name ?? string.Empty,
            PackageCode = payment.Package?.Code ?? string.Empty,
            Provider = payment.Provider,
            Status = payment.Status,
            AmountVnd = payment.AmountVnd,
            Currency = payment.Currency,
            OrderCode = payment.OrderCode,
            CreatedAt = payment.CreatedAt,
            PaidAt = payment.PaidAt,
            FailureReason = payment.FailureReason
        }).ToList();
    }

    private async Task ActivateSubscriptionAsync(Payment payment, Package package, bool idempotent, CancellationToken cancellationToken)
    {
        if (idempotent && await _subscriptions.GetByPaymentIdAsync(payment.Id, cancellationToken) is not null)
        {
            return;
        }

        var current = await _subscriptions.GetCurrentActiveAsync(payment.UserId, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var startsAt = current is not null && current.EndsAt > now ? current.EndsAt : now;
        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            PackageId = package.Id,
            UserId = payment.UserId,
            UserName = payment.UserName,
            UserEmail = payment.UserEmail,
            Status = SubscriptionStatus.Active,
            StartsAt = startsAt,
            EndsAt = startsAt.AddDays(Math.Max(1, package.DurationDays)),
            PaymentId = payment.Id,
            CreatedAt = now
        };

        await _subscriptions.AddAsync(subscription, cancellationToken);
    }

    private string BuildWebhookUrl(PaymentProvider provider)
    {
        var baseUrl = _options.BaseReturnUrl.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Payment base return URL is not configured.");
        }

        return provider == PaymentProvider.MoMo
            ? $"{baseUrl}/Payments/MomoWebhook"
            : $"{baseUrl}/Payments/PayOsWebhook";
    }

    private static string BuildReturnUrl(string returnUrl, PaymentProvider provider, string orderCode)
    {
        var separator = returnUrl.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return $"{returnUrl}{separator}provider={provider}&orderCode={Uri.EscapeDataString(orderCode)}";
    }

    private static long BuildPayOsOrderCode(string orderCode)
    {
        var digits = new string(orderCode.Where(char.IsDigit).ToArray());
        if (digits.Length > 15)
        {
            digits = digits[^15..];
        }

        return long.TryParse(digits, out var result) ? result : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }

    private static string GenerateOrderCode(PaymentProvider provider)
    {
        var prefix = provider == PaymentProvider.PayOS ? "9" : "8";
        return $"{prefix}{DateTimeOffset.UtcNow:yyMMddHHmmssfff}{Random.Shared.Next(100, 999)}";
    }

    private static PaymentCheckoutResultDto ToCheckoutResult(Payment payment, string message) => new()
    {
        PaymentId = payment.Id,
        PackageId = payment.PackageId,
        Provider = payment.Provider,
        Status = payment.Status,
        OrderCode = payment.OrderCode,
        CheckoutUrl = payment.CheckoutUrl,
        QrCode = payment.QrCode,
        Message = message
    };

    private static void ValidateCreateRequest(CreatePaymentRequestDto request)
    {
        if (request.UserId == Guid.Empty)
        {
            throw new InvalidOperationException("User is required.");
        }

        if (request.PackageId == Guid.Empty)
        {
            throw new InvalidOperationException("Package is required.");
        }

        if (!Enum.IsDefined(request.Provider))
        {
            throw new InvalidOperationException("Payment provider is invalid.");
        }

        if (string.IsNullOrWhiteSpace(request.ReturnUrl) || string.IsNullOrWhiteSpace(request.CancelUrl))
        {
            throw new InvalidOperationException("Return and cancel URLs are required.");
        }
    }

    private static string Normalize(string value, int maxLength)
    {
        var normalized = (value ?? string.Empty).Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}
