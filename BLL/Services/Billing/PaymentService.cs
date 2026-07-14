using Microsoft.Extensions.Options;
using PRN222_FINAL.BLL.Mapping;
using PRN222_FINAL.BLL.Options;
using PRN222_FINAL.BLL.Services.Billing.Gateways;
using PRN222_FINAL.DAL.Repositories.Billing;
using PRN222_FINAL.BLL.Models;
using PRN222_FINAL.BLL.Contracts.Billing;
using DalPaymentProvider = PRN222_FINAL.DAL.Enums.PaymentProvider;

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
        var packageEntity = await _packages.GetByIdAsync(request.PackageId, cancellationToken)
            ?? throw new InvalidOperationException("Package not found.");
        var package = BillingDtoMapper.ToModel(packageEntity);
        if (!package.IsActive)
        {
            throw new InvalidOperationException("Package is not active.");
        }

        var currentSubscriptionEntity = await _subscriptions.GetCurrentActiveAsync(request.UserId, cancellationToken);
        if (currentSubscriptionEntity?.Package is not null)
        {
            var currentPackage = BillingDtoMapper.ToModel(currentSubscriptionEntity.Package);
            if (package.SortOrder < currentPackage.SortOrder)
            {
                throw new InvalidOperationException(
                    $"Không thể hạ từ gói {currentPackage.Name} xuống gói {package.Name} khi gói hiện tại còn hiệu lực.");
            }
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

        if (package.PriceVnd <= 0)
        {
            payment.Status = PaymentStatus.Paid;
            payment.PaidAt = now;
            payment.CheckoutUrl = request.ReturnUrl;
            if (!await _payments.TryAddFreePaidAsync(BillingDtoMapper.ToEntity(payment), cancellationToken))
            {
                var priorFreePayment = await _payments.GetLatestSuccessfulByUserAsync(request.UserId, cancellationToken);
                if (priorFreePayment is not null && priorFreePayment.PackageId == package.Id)
                {
                    var priorPayment = BillingDtoMapper.ToModel(priorFreePayment);
                    await EnsurePaidSubscriptionAsync(priorPayment, package, cancellationToken);
                    return ToCheckoutResult(priorPayment, "Free package was already activated.");
                }
                throw new InvalidOperationException("Gói trải nghiệm chỉ được kích hoạt một lần cho mỗi tài khoản.");
            }
            await ActivateSubscriptionAsync(payment, package, cancellationToken);
            return ToCheckoutResult(payment, "Free package activated.");
        }

        await _payments.AddAsync(BillingDtoMapper.ToEntity(payment), cancellationToken);

        var gatewayRequest = new PaymentGatewayCreateRequest
        {
            Provider = request.Provider,
            OrderCode = payment.OrderCode,
            PayOsOrderCode = BuildPayOsOrderCode(payment.OrderCode),
            AmountVnd = package.PriceVnd,
            Description = $"PRN222 {package.Code}",
            ReturnUrl = BuildReturnUrl(request.ReturnUrl, request.Provider, payment.OrderCode),
            CancelUrl = BuildReturnUrl(request.CancelUrl, request.Provider, payment.OrderCode),
            IpnUrl = BuildWebhookUrl(request.Provider, request.ReturnUrl),
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
        await _payments.UpdateAsync(BillingDtoMapper.ToEntity(payment), cancellationToken);

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

        var paymentEntity = await _payments.GetByOrderCodeAsync((DalPaymentProvider)webhook.Provider, result.OrderCode, cancellationToken)
            ?? throw new InvalidOperationException("Payment not found.");
        var payment = BillingDtoMapper.ToModel(paymentEntity);
        var packageEntity = await _packages.GetByIdAsync(payment.PackageId, cancellationToken)
            ?? throw new InvalidOperationException("Package not found.");
        var package = BillingDtoMapper.ToModel(packageEntity);

        if (!result.AmountVnd.HasValue || result.AmountVnd.Value != payment.AmountVnd)
        {
            throw new InvalidOperationException("Webhook amount does not match payment amount.");
        }

        if (payment.Status == PaymentStatus.Paid)
        {
            await EnsurePaidSubscriptionAsync(payment, package, cancellationToken);
            return new PaymentWebhookResultDto
            {
                PaymentId = payment.Id,
                OrderCode = payment.OrderCode,
                Status = payment.Status,
                IsDuplicate = true,
                SubscriptionActivated = true,
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
            await _payments.UpdateAsync(BillingDtoMapper.ToEntity(payment), cancellationToken);
            await ActivateSubscriptionAsync(payment, package, cancellationToken);
        }
        else
        {
            payment.Status = result.Status == PaymentStatus.Canceled ? PaymentStatus.Canceled : PaymentStatus.Failed;
            payment.FailedAt = DateTimeOffset.UtcNow;
            payment.FailureReason = Normalize(result.Message, 1000);
            await _payments.UpdateAsync(BillingDtoMapper.ToEntity(payment), cancellationToken);
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

    public Task<PaymentWebhookResultDto> HandleSignedReturnAsync(
        PaymentWebhookDto returnData,
        CancellationToken cancellationToken = default)
    {
        if (returnData.Provider != PaymentProvider.MoMo)
        {
            throw new InvalidOperationException("Only signed MoMo return data can be processed directly.");
        }

        return HandleWebhookAsync(returnData, cancellationToken);
    }

    public async Task<PaymentReturnDto?> GetReturnStatusAsync(
        PaymentProvider provider,
        string orderCode,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderCode) || userId == Guid.Empty)
        {
            return null;
        }

        var paymentEntity = await _payments.GetByOrderCodeAsync((DalPaymentProvider)provider, orderCode, cancellationToken);
        var payment = paymentEntity is null ? null : BillingDtoMapper.ToModel(paymentEntity);
        if (payment is null || payment.UserId != userId)
        {
            return null;
        }

        if (payment.Provider == PaymentProvider.PayOS && payment.Status == PaymentStatus.Pending)
        {
            payment = await ReconcilePayOsPaymentAsync(payment, cancellationToken);
        }

        Package? package = null;
        Subscription? subscription = null;
        var packageEntity = await _packages.GetByIdAsync(payment.PackageId, cancellationToken);
        if (packageEntity is not null)
        {
            package = BillingDtoMapper.ToModel(packageEntity);
            if (payment.Status == PaymentStatus.Paid)
            {
                await EnsurePaidSubscriptionAsync(payment, package, cancellationToken);
                var subscriptionEntity = await _subscriptions.GetByPaymentIdAsync(payment.Id, cancellationToken);
                subscription = subscriptionEntity is null ? null : BillingDtoMapper.ToModel(subscriptionEntity);
            }
        }

        return new PaymentReturnDto
            {
                PaymentId = payment.Id,
                Provider = provider,
                OrderCode = payment.OrderCode,
                ProviderTransactionId = payment.ProviderTransactionId,
                Status = payment.Status,
                CustomerName = payment.UserName,
                CustomerEmail = payment.UserEmail,
                PackageName = package?.Name ?? string.Empty,
                PackageCode = package?.Code ?? string.Empty,
                AmountVnd = payment.AmountVnd,
                Currency = payment.Currency,
                CreatedAt = payment.CreatedAt,
                PaidAt = payment.PaidAt,
                SubscriptionStartsAt = subscription?.StartsAt,
                SubscriptionEndsAt = subscription?.EndsAt,
                IsLifetime = package?.IsLifetime == true,
                MonthlyChatLimit = package?.MonthlyChatLimit ?? 0,
                MonthlyDocumentUploadLimit = package?.MonthlyDocumentUploadLimit ?? 0,
                StorageLimitMb = package?.StorageLimitMb ?? 0,
                Message = payment.Status switch
                {
                    PaymentStatus.Paid => "Thanh toán thành công.",
                    PaymentStatus.Pending => "Thanh toán đang chờ xác nhận từ cổng thanh toán.",
                    PaymentStatus.Canceled => "Thanh toán đã bị hủy.",
                    _ => payment.FailureReason
                }
            };
    }

    private async Task<Payment> ReconcilePayOsPaymentAsync(Payment payment, CancellationToken cancellationToken)
    {
        PaymentGatewayStatusResult gatewayStatus;
        try
        {
            gatewayStatus = await _payOsGateway.GetStatusAsync(BuildPayOsOrderCode(payment.OrderCode), cancellationToken);
        }
        catch (HttpRequestException)
        {
            return payment;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return payment;
        }
        catch (InvalidOperationException)
        {
            return payment;
        }

        var expectedGatewayOrderCode = BuildPayOsOrderCode(payment.OrderCode).ToString();
        if (!string.Equals(gatewayStatus.OrderCode, expectedGatewayOrderCode, StringComparison.Ordinal)
            || (gatewayStatus.AmountVnd.HasValue && gatewayStatus.AmountVnd.Value != payment.AmountVnd))
        {
            return payment;
        }

        payment.ProviderTransactionId = string.IsNullOrWhiteSpace(gatewayStatus.ProviderTransactionId)
            ? payment.ProviderTransactionId
            : gatewayStatus.ProviderTransactionId;

        if (gatewayStatus.Status == PaymentStatus.Paid)
        {
            var packageEntity = await _packages.GetByIdAsync(payment.PackageId, cancellationToken);
            if (packageEntity is null)
            {
                return payment;
            }

            payment.Status = PaymentStatus.Paid;
            payment.PaidAt = DateTimeOffset.UtcNow;
            await _payments.UpdateAsync(BillingDtoMapper.ToEntity(payment), cancellationToken);
            await ActivateSubscriptionAsync(payment, BillingDtoMapper.ToModel(packageEntity), cancellationToken);
        }
        else if (gatewayStatus.Status == PaymentStatus.Canceled)
        {
            payment.Status = PaymentStatus.Canceled;
            payment.FailedAt = DateTimeOffset.UtcNow;
            payment.FailureReason = Normalize(gatewayStatus.Message, 1000);
            await _payments.UpdateAsync(BillingDtoMapper.ToEntity(payment), cancellationToken);
        }

        return payment;
    }

    public async Task<IReadOnlyList<PaymentHistoryItemDto>> GetPaymentsForUserAsync(Guid userId, int limit = 20, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new InvalidOperationException("User is required.");
        }

        var payments = (await _payments.GetByUserAsync(userId, Math.Clamp(limit, 1, 100), cancellationToken))
            .Select(BillingDtoMapper.ToModel).ToList();
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

    private async Task ActivateSubscriptionAsync(Payment payment, Package package, CancellationToken cancellationToken)
    {
        var latestPaidPayment = await _payments.GetLatestSuccessfulByUserAsync(payment.UserId, cancellationToken);
        if (latestPaidPayment is not null && latestPaidPayment.Id != payment.Id)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var existingEntity = await _subscriptions.GetByPaymentIdAsync(payment.Id, cancellationToken);
        var existing = existingEntity is null ? null : BillingDtoMapper.ToModel(existingEntity);
        var isAlreadyEffective = existing is not null
            && existing.Status == SubscriptionStatus.Active
            && existing.StartsAt <= now
            && existing.EndsAt > now;
        var startsAt = isAlreadyEffective ? existing!.StartsAt : now;
        var endsAt = isAlreadyEffective
            ? existing!.EndsAt
            : package.IsLifetime
            ? new DateTimeOffset(9999, 12, 31, 23, 59, 59, TimeSpan.Zero)
            : startsAt.AddDays(Math.Max(1, package.DurationDays));
        var subscription = new Subscription
        {
            Id = existing?.Id ?? Guid.NewGuid(),
            PackageId = package.Id,
            UserId = payment.UserId,
            UserName = payment.UserName,
            UserEmail = payment.UserEmail,
            Status = SubscriptionStatus.Active,
            StartsAt = startsAt,
            EndsAt = endsAt,
            PaymentId = payment.Id,
            CreatedAt = existing?.CreatedAt ?? now
        };

        await _subscriptions.ActivateExclusiveAsync(BillingDtoMapper.ToEntity(subscription), now, cancellationToken);
    }

    private async Task EnsurePaidSubscriptionAsync(Payment payment, Package package, CancellationToken cancellationToken)
    {
        await ActivateSubscriptionAsync(payment, package, cancellationToken);
    }

    private string BuildWebhookUrl(PaymentProvider provider, string checkoutReturnUrl)
    {
        var baseUrl = ResolvePublicBaseUrl(checkoutReturnUrl);

        return provider == PaymentProvider.MoMo
            ? $"{baseUrl}/Payments/MomoWebhook"
            : $"{baseUrl}/Payments/PayOsWebhook";
    }

    private string ResolvePublicBaseUrl(string checkoutReturnUrl)
    {
        var configured = (_options.BaseReturnUrl ?? string.Empty).Trim().TrimEnd('/');
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return ValidateAbsoluteHttpOrigin(configured, "Payment base return URL");
        }

        if (!Uri.TryCreate(checkoutReturnUrl, UriKind.Absolute, out var returnUri)
            || (!returnUri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !returnUri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException("Checkout return URL must be an absolute HTTP(S) URL.");
        }

        return returnUri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
    }

    private static string ValidateAbsoluteHttpOrigin(string value, string name)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (!uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            || uri.PathAndQuery != "/")
        {
            throw new InvalidOperationException($"{name} must be an absolute HTTP(S) origin without a path.");
        }

        return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
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
        if (provider == PaymentProvider.PayOS)
        {
            // PayOS accepts a numeric order code with at most 15 digits. Keeping the
            // persisted and provider order codes identical makes Return/Webhook lookup exact.
            return $"{DateTimeOffset.UtcNow:yyMMddHHmmss}{Random.Shared.Next(100, 999)}";
        }

        return $"8{DateTimeOffset.UtcNow:yyMMddHHmmssfff}{Random.Shared.Next(100, 999)}";
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
