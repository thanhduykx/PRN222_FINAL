using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222_FINAL.BLL.Services.Billing;
using PRN222_FINAL.BLL.Models;
using PRN222_FINAL.BLL.Contracts.Billing;

namespace PRN222_FINAL.Web.Pages.Payments;

#pragma warning disable S4502 // External PayOS callbacks cannot carry a browser CSRF token; HMAC validation is mandatory in PaymentService.
[IgnoreAntiforgeryToken]
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Security",
    "S4502:CSRF protection is intentionally disabled for this signed external callback",
    Justification = "PayOS cannot provide a browser antiforgery token; PaymentService rejects callbacks unless their HMAC signature is valid.")]
public sealed class PayOsWebhookModel : PageModel
{
    private readonly IPaymentService _payments;

    public PayOsWebhookModel(IPaymentService payments)
    {
        _payments = payments;
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var reader = new StreamReader(Request.Body);
            var rawBody = await reader.ReadToEndAsync(cancellationToken);
            var result = await _payments.HandleWebhookAsync(new PaymentWebhookDto
            {
                Provider = PaymentProvider.PayOS,
                RawBody = rawBody,
                Values = new Dictionary<string, string>()
            }, cancellationToken);

            return new JsonResult(new { code = "00", desc = result.Message, success = result.Status == PaymentStatus.Paid });
        }
        catch (Exception ex)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return new JsonResult(new { code = "01", desc = ex.Message, success = false });
        }
    }
}
