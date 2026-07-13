using PRN222_FINAL.BLL.Contracts.Billing;

namespace PRN222_FINAL.BLL.Services.Billing;

public interface IPackageService
{
    Task<IReadOnlyList<PackageDto>> GetActivePackagesAsync(CancellationToken cancellationToken = default);
    Task<PackageDto?> GetPackageAsync(Guid packageId, CancellationToken cancellationToken = default);
    Task<PackagePriceChangeDto?> UpdatePriceAsync(Guid packageId, decimal newPriceVnd, string changedBy, CancellationToken cancellationToken = default);
    Task<PackagePriceChangeDto?> GetLatestPriceChangeAsync(CancellationToken cancellationToken = default);
}
