using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222_FINAL.BLL.Contracts.Billing;
using PRN222_FINAL.BLL.Services.Billing;
using PRN222_FINAL.Web.Security;

namespace PRN222_FINAL.Web.Pages.Admin;

[Authorize(Policy = AuthorizationPolicies.AdminOnly)]
public sealed class PackagesModel : PageModel
{
    private readonly IPackageService _packages;
    private readonly ILogger<PackagesModel> _logger;

    public PackagesModel(IPackageService packages, ILogger<PackagesModel> logger)
    {
        _packages = packages;
        _logger = logger;
    }

    public IReadOnlyList<PackageDto> Packages { get; private set; } = Array.Empty<PackageDto>();
    public IReadOnlyList<PackagePriceChangeDto> PriceHistory { get; private set; } = Array.Empty<PackagePriceChangeDto>();
    public IReadOnlyDictionary<Guid, int> PendingPaymentCounts { get; private set; } = new Dictionary<Guid, int>();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        Packages = await _packages.GetActivePackagesAsync(cancellationToken);
        PriceHistory = await _packages.GetRecentPriceChangesAsync(50, cancellationToken);
        PendingPaymentCounts = await _packages.GetPendingPaymentCountsAsync(cancellationToken);
    }

    public async Task<IActionResult> OnPostUpdatePriceAsync(
        [FromForm] UpdatePackagePriceInput input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Vui lòng nhập giá hợp lệ và lý do thay đổi từ 5 đến 500 ký tự.";
            return RedirectToPage();
        }

        try
        {
            var changedBy = User.FindFirstValue(ClaimTypes.Email)
                ?? User.FindFirstValue(ClaimTypes.Name)
                ?? "Admin";
            var change = await _packages.UpdatePriceAsync(
                input.PackageId,
                input.ExpectedCurrentPriceVnd,
                input.PriceVnd,
                changedBy,
                input.Reason,
                input.ConfirmFreePrice,
                cancellationToken);
            TempData[change is null ? "Error" : "Success"] = change is null
                ? "Giá mới không thay đổi so với giá hiện tại."
                : $"Đã cập nhật giá gói {change.PackageName}. Thông báo sẽ hiển thị cho mọi người dùng đăng nhập trong 3 ngày.";
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            _logger.LogWarning(exception, "Could not update package {PackageId} price.", input.PackageId);
            TempData["Error"] = exception.Message switch
            {
                "Package not found." => "Không tìm thấy gói dịch vụ.",
                "Package price changed since this page was loaded." =>
                    "Giá gói đã được một quản trị viên khác thay đổi. Vui lòng kiểm tra lại giá mới nhất rồi thử lại.",
                _ => exception.Message
            };
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogError(exception, "Unexpected error while updating package {PackageId} price.", input.PackageId);
            TempData["Error"] = "Không thể cập nhật giá gói lúc này. Vui lòng thử lại sau.";
        }

        return RedirectToPage();
    }

    public sealed class UpdatePackagePriceInput
    {
        [Required]
        public Guid PackageId { get; set; }

        [Range(typeof(decimal), "0", "1000000000")]
        public decimal PriceVnd { get; set; }

        [Range(typeof(decimal), "0", "1000000000")]
        public decimal ExpectedCurrentPriceVnd { get; set; }

        [Required, StringLength(500, MinimumLength = 5)]
        public string Reason { get; set; } = string.Empty;

        public bool ConfirmFreePrice { get; set; }
    }
}
