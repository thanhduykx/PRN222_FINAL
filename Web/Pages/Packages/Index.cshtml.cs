using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222_FINAL.BLL.Services.Billing;
using PRN222_FINAL.Models;
using PRN222_FINAL.Models.DTOs.Billing;
using PRN222_FINAL.Web.ViewModels.Billing;

namespace PRN222_FINAL.Web.Pages.Packages;

[Authorize]
public sealed class IndexModel : PageModel
{
    private readonly IPackageService _packages;
    private readonly IPaymentService _payments;
    private readonly ISubscriptionService _subscriptions;

    public IndexModel(
        IPackageService packages,
        IPaymentService payments,
        ISubscriptionService subscriptions)
    {
        _packages = packages;
        _payments = payments;
        _subscriptions = subscriptions;
    }

    public IReadOnlyList<PackageViewModel> Packages { get; private set; } = Array.Empty<PackageViewModel>();
    public SubscriptionViewModel? CurrentSubscription { get; private set; }
    public string ErrorMessage { get; private set; } = string.Empty;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadPageAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostCheckoutAsync(Guid packageId, string provider, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<PaymentProvider>(provider, true, out var parsedProvider))
        {
            ErrorMessage = "Cổng thanh toán không hợp lệ.";
            await LoadPageAsync(cancellationToken);
            return Page();
        }

        try
        {
            var request = new CreatePaymentRequestDto
            {
                PackageId = packageId,
                Provider = parsedProvider,
                UserId = GetUserId(),
                UserName = User.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
                UserEmail = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
                ReturnUrl = Url.PageLink("/Payments/Return") ?? string.Empty,
                CancelUrl = Url.PageLink("/Payments/Return") ?? string.Empty,
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty
            };

            var result = await _payments.CreateCheckoutAsync(request, cancellationToken);
            return result.Status == PaymentStatus.Paid
                ? RedirectToPage("/Subscriptions/Current")
                : Redirect(result.CheckoutUrl);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            await LoadPageAsync(cancellationToken);
            return Page();
        }
    }

    private async Task LoadPageAsync(CancellationToken cancellationToken)
    {
        try
        {
            var currentSubscription = await _subscriptions.GetCurrentSubscriptionAsync(GetUserId(), cancellationToken);
            CurrentSubscription = currentSubscription is null
                ? null
                : new SubscriptionViewModel
                {
                    PackageId = currentSubscription.PackageId,
                    PackageName = currentSubscription.PackageName,
                    Status = currentSubscription.Status.ToString(),
                    StartsAt = currentSubscription.StartsAt,
                    EndsAt = currentSubscription.EndsAt,
                    MonthlyChatLimit = currentSubscription.MonthlyChatLimit,
                    MonthlyDocumentUploadLimit = currentSubscription.MonthlyDocumentUploadLimit,
                    StorageLimitMb = currentSubscription.StorageLimitMb
                };

            var packages = await _packages.GetActivePackagesAsync(cancellationToken);
            Packages = packages.Select(package => new PackageViewModel
            {
                Id = package.Id,
                Code = package.Code,
                Name = package.Name,
                Description = package.Description,
                PriceVnd = package.PriceVnd,
                DurationDays = package.DurationDays,
                MonthlyChatLimit = package.MonthlyChatLimit,
                MonthlyDocumentUploadLimit = package.MonthlyDocumentUploadLimit,
                StorageLimitMb = package.StorageLimitMb,
                IsCurrentPackage = CurrentSubscription?.PackageId == package.Id
            }).ToList();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : Guid.Empty;
    }
}
