using System.Security.Claims;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222_FINAL.BLL.Contracts.Billing;
using PRN222_FINAL.BLL.Models;
using PRN222_FINAL.BLL.Security;
using PRN222_FINAL.BLL.Services.Billing;
using QRCoder;
using QRCoder.Exceptions;

namespace PRN222_FINAL.Web.Pages.Payments;

[Authorize]
public sealed class CheckoutModel : PageModel
{
    private readonly IPaymentService _payments;

    public CheckoutModel(IPaymentService payments)
    {
        _payments = payments;
    }

    public IReadOnlyList<PendingPaymentDto> PendingPayments { get; private set; } = Array.Empty<PendingPaymentDto>();
    public Guid? SelectedPaymentId { get; private set; }

    [TempData]
    public string ErrorMessage { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(Guid? paymentId, CancellationToken cancellationToken)
    {
        if (!User.IsInRole(AppRoles.Student))
        {
            return RedirectToPage("/Home/Index");
        }

        SelectedPaymentId = paymentId;
        var pending = await _payments.GetPendingPaymentsAsync(GetUserId(), cancellationToken);
        PendingPayments = paymentId.HasValue
            ? pending.OrderBy(item => item.PaymentId == paymentId.Value ? 0 : 1).ThenBy(item => item.CreatedAt).ToList()
            : pending;
        return Page();
    }

    public async Task<IActionResult> OnPostSwitchProviderAsync(
        Guid paymentId,
        string provider,
        CancellationToken cancellationToken)
    {
        if (!User.IsInRole(AppRoles.Student))
        {
            return RedirectToPage("/Home/Index");
        }

        if (!Enum.TryParse<PaymentProvider>(provider, true, out var targetProvider)
            || targetProvider is not (PaymentProvider.MoMo or PaymentProvider.PayOS))
        {
            ErrorMessage = "Phương thức thanh toán không hợp lệ.";
            return RedirectToPage(new { paymentId });
        }

        var userId = GetUserId();
        var currentPayment = (await _payments.GetPendingPaymentsAsync(userId, cancellationToken))
            .SingleOrDefault(item => item.PaymentId == paymentId);
        if (currentPayment is null)
        {
            ErrorMessage = "Đơn thanh toán không còn tồn tại hoặc đã hết hạn.";
            return RedirectToPage();
        }

        if (currentPayment.Provider == targetProvider)
        {
            return RedirectToPage(new { paymentId });
        }

        try
        {
            var result = await _payments.CreateCheckoutAsync(new CreatePaymentRequestDto
            {
                UserId = userId,
                UserName = User.FindFirstValue(ClaimTypes.Name) ?? currentPayment.RecipientName,
                UserEmail = User.FindFirstValue(ClaimTypes.Email) ?? currentPayment.RecipientEmail,
                PackageId = currentPayment.PackageId,
                Provider = targetProvider,
                ReturnUrl = Url.PageLink("/Payments/Return") ?? string.Empty,
                CancelUrl = Url.PageLink("/Payments/Checkout") ?? string.Empty,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty
            }, cancellationToken);

            await _payments.DeletePendingPaymentAsync(paymentId, userId, cancellationToken);
            return RedirectToPage(new { paymentId = result.PaymentId });
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return RedirectToPage(new { paymentId });
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        await _payments.DeletePendingPaymentAsync(paymentId, GetUserId(), cancellationToken);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetStatusAsync(
        string? provider,
        string? orderCode,
        CancellationToken cancellationToken)
    {
        if (!User.IsInRole(AppRoles.Student))
        {
            return Forbid();
        }

        if (!Enum.TryParse<PaymentProvider>(provider, true, out var parsedProvider)
            || parsedProvider is not (PaymentProvider.MoMo or PaymentProvider.PayOS)
            || string.IsNullOrWhiteSpace(orderCode))
        {
            return BadRequest(new { message = "Thông tin giao dịch không hợp lệ." });
        }

        var status = await _payments.GetReturnStatusAsync(
            parsedProvider,
            orderCode.Trim(),
            GetUserId(),
            cancellationToken);
        if (status is null)
        {
            return NotFound(new { message = "Không tìm thấy giao dịch hoặc đơn đã hết hạn." });
        }

        return new JsonResult(new
        {
            status = status.Status.ToString(),
            redirectUrl = Url.Page(
                "/Payments/Return",
                pageHandler: null,
                values: new { provider = parsedProvider.ToString(), orderCode = status.OrderCode })
        });
    }

    public static bool IsSafeCheckoutUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    public static string BuildQrImageSource(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 4000)
        {
            return string.Empty;
        }

        try
        {
            using var qrData = QRCodeGenerator.GenerateQrCode(value, QRCodeGenerator.ECCLevel.M);
            using var qrCode = new PngByteQRCode(qrData);
            return $"data:image/png;base64,{Convert.ToBase64String(qrCode.GetGraphic(8))}";
        }
        catch (DataTooLongException)
        {
            return string.Empty;
        }
    }

    public static string BuildVietQrImageSource(
        string bankBin,
        string accountNumber,
        decimal amountVnd,
        string description,
        string accountName)
    {
        if (string.IsNullOrWhiteSpace(bankBin) || string.IsNullOrWhiteSpace(accountNumber) || amountVnd <= 0)
        {
            return string.Empty;
        }

        var bank = Uri.EscapeDataString(bankBin.Trim());
        var account = Uri.EscapeDataString(accountNumber.Trim());
        var amount = amountVnd.ToString("0", CultureInfo.InvariantCulture);
        var query = $"amount={amount}";

        if (!string.IsNullOrWhiteSpace(description))
        {
            query += $"&addInfo={Uri.EscapeDataString(description.Trim())}";
        }

        if (!string.IsNullOrWhiteSpace(accountName))
        {
            query += $"&accountName={Uri.EscapeDataString(accountName.Trim())}";
        }

        return $"https://img.vietqr.io/image/{bank}-{account}-compact2.png?{query}";
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : Guid.Empty;
    }
}
