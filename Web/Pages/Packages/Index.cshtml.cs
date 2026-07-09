using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222_FINAL.BLL.Services.Billing;
using PRN222_FINAL.Web.ViewModels.Billing;

namespace PRN222_FINAL.Web.Pages.Packages;

[Authorize]
public sealed class IndexModel : PageModel
{
    private readonly IPackageService _packages;

    public IndexModel(IPackageService packages)
    {
        _packages = packages;
    }

    public IReadOnlyList<PackageViewModel> Packages { get; private set; } = Array.Empty<PackageViewModel>();
    public string ErrorMessage { get; private set; } = string.Empty;

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        try
        {
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
                StorageLimitMb = package.StorageLimitMb
            }).ToList();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
