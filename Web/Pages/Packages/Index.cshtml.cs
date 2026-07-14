using PRN222_FINAL.BLL.Security;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222_FINAL.BLL.Services.Billing;
using PRN222_FINAL.BLL.Models;
using PRN222_FINAL.BLL.Contracts.Billing;
using PRN222_FINAL.Web.Security;
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

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (!IsStudent())
        {
            return RedirectForRole();
        }

        await LoadPageAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostCheckoutAsync(Guid packageId, string provider, CancellationToken cancellationToken)
    {
        if (!IsStudent())
        {
            return RedirectForRole();
        }

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
                ? RedirectToPage("/Payments/Return", new { provider = result.Provider, orderCode = result.OrderCode })
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
                    IsLifetime = currentSubscription.IsLifetime
                };

            var packages = await _packages.GetActivePackagesAsync(cancellationToken);
            var currentPackage = CurrentSubscription is null
                ? null
                : packages.FirstOrDefault(package => package.Id == CurrentSubscription.PackageId);
            var studentPackage = packages.FirstOrDefault(package =>
                package.Code.Equals("STUDENT", StringComparison.OrdinalIgnoreCase));

            Packages = packages.Select(package => new PackageViewModel
            {
                Id = package.Id,
                Code = package.Code,
                Name = package.Name,
                Description = package.Description,
                PriceVnd = package.PriceVnd,
                DurationDays = package.DurationDays,
                MonthlyChatLimit = package.MonthlyChatLimit,
                IsLifetime = package.IsLifetime,
                IsCurrentPackage = CurrentSubscription?.PackageId == package.Id,
                IsDowngrade = currentPackage is not null && package.SortOrder < currentPackage.SortOrder,
                DiscountPercent = CalculateDiscountPercent(package, studentPackage)
            }).ToList();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private static int CalculateDiscountPercent(PackageDto package, PackageDto? studentPackage)
    {
        if (studentPackage is null
            || package.PriceVnd <= 0
            || package.DurationDays <= studentPackage.DurationDays
            || studentPackage.PriceVnd <= 0
            || studentPackage.DurationDays <= 0)
        {
            return 0;
        }

        var regularPrice = studentPackage.PriceVnd * package.DurationDays / studentPackage.DurationDays;
        if (regularPrice <= package.PriceVnd)
        {
            return 0;
        }

        return (int)Math.Round(
            (regularPrice - package.PriceVnd) * 100m / regularPrice,
            MidpointRounding.AwayFromZero);
    }

    private Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : Guid.Empty;
    }

    private bool IsStudent()
    {
        return AppRoles.Normalize(User.FindFirstValue(ClaimTypes.Role)) == AppRoles.Student;
    }

    private IActionResult RedirectForRole()
    {
        return AppRoles.Normalize(User.FindFirstValue(ClaimTypes.Role)) == AppRoles.Admin
            ? RedirectToPage("/Admin/Statistics")
            : RedirectToPage("/Home/Courses");
    }
}
