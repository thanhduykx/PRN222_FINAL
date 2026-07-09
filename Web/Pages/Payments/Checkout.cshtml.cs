using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222_FINAL.BLL.Services.Billing;
using PRN222_FINAL.Models;
using PRN222_FINAL.Models.DTOs.Billing;

namespace PRN222_FINAL.Web.Pages.Payments;

[Authorize]
public sealed class CheckoutModel : PageModel
{
    private readonly IPaymentService _payments;

    public CheckoutModel(IPaymentService payments)
    {
        _payments = payments;
    }

    [BindProperty(SupportsGet = true)]
    public Guid PackageId { get; set; }

    public string ErrorMessage { get; private set; } = string.Empty;

    public IActionResult OnGet()
    {
        return PackageId == Guid.Empty ? RedirectToPage("/Packages/Index") : Page();
    }

    public async Task<IActionResult> OnPostAsync(string provider, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<PaymentProvider>(provider, true, out var parsedProvider))
        {
            ErrorMessage = "Payment provider is invalid.";
            return Page();
        }

        try
        {
            var request = new CreatePaymentRequestDto
            {
                PackageId = PackageId,
                Provider = parsedProvider,
                UserId = GetUserId(),
                UserName = User.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
                UserEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
                ReturnUrl = Url.PageLink("/Payments/Return") ?? string.Empty,
                CancelUrl = Url.PageLink("/Payments/Return") ?? string.Empty,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty
            };

            var result = await _payments.CreateCheckoutAsync(request, cancellationToken);
            if (result.Status == PaymentStatus.Paid)
            {
                return RedirectToPage("/Subscriptions/Current");
            }

            return Redirect(result.CheckoutUrl);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return Page();
        }
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : Guid.Empty;
    }
}
