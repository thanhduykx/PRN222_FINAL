using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222_FINAL.BLL.Contracts.Billing;
using PRN222_FINAL.BLL.Security;
using PRN222_FINAL.BLL.Services.Billing;

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

    public async Task<IActionResult> OnPostDeleteAsync(Guid paymentId, CancellationToken cancellationToken)
    {
        await _payments.DeletePendingPaymentAsync(paymentId, GetUserId(), cancellationToken);
        return RedirectToPage();
    }

    public static bool IsSafeCheckoutUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);

    public static bool IsSafeQrImageUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps;

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : Guid.Empty;
    }
}
