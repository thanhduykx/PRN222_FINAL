using PRN222_FINAL.BLL.Contracts.Billing;

namespace PRN222_FINAL.BLL.Services.Billing;

public interface IPackageService
{
    Task<IReadOnlyList<PackageDto>> GetActivePackagesAsync(CancellationToken cancellationToken = default);
    Task<PackageDto?> GetPackageAsync(Guid packageId, CancellationToken cancellationToken = default);
    Task<PackagePriceChangeDto?> UpdatePriceAsync(Guid packageId, decimal expectedCurrentPriceVnd, decimal newPriceVnd, string changedBy, string reason, bool confirmFreePrice, CancellationToken cancellationToken = default);
    Task<PackagePriceChangeDto?> GetLatestPriceChangeAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PackagePriceChangeDto>> GetRecentPriceChangesAsync(int limit = 50, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, int>> GetPendingPaymentCountsAsync(CancellationToken cancellationToken = default);
}
