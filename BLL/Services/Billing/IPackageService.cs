using PRN222_FINAL.BLL.Contracts.Billing;

namespace PRN222_FINAL.BLL.Services.Billing;

public interface IPackageService
{
    Task<IReadOnlyList<PackageDto>> GetActivePackagesAsync(CancellationToken cancellationToken = default);
    Task<PackageDto?> GetPackageAsync(Guid packageId, CancellationToken cancellationToken = default);
}
