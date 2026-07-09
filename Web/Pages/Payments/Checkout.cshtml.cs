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
    private readonly IPackageService _packages;

    public CheckoutModel(IPaymentService payments, IPackageService packages)
    {
        _payments = payments;
        _packages = packages;
    }

    [BindProperty(SupportsGet = true)]
    public Guid PackageId { get; set; }

    public PackageDto? Package { get; private set; }
    public string ErrorMessage { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (PackageId == Guid.Empty)
        {
            return RedirectToPage("/Packages/Index");
        }

        Package = await _packages.GetPackageAsync(PackageId, cancellationToken);
        return Package is null ? RedirectToPage("/Packages/Index") : Page();
    }

    public async Task<IActionResult> OnPostAsync(string provider, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<PaymentProvider>(provider, true, out var parsedProvider))
        {
            ErrorMessage = "Payment provider is invalid.";
            Package = await _packages.GetPackageAsync(PackageId, cancellationToken);
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
            Package = await _packages.GetPackageAsync(PackageId, cancellationToken);
            return Page();
        }
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : Guid.Empty;
    }
}
