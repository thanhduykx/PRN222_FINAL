using PRN222_FINAL.Models;

namespace PRN222_FINAL.DAL.Repositories.Billing;

public interface IPackageRepository
{
    Task<IReadOnlyList<Package>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Package>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<Package?> GetByIdAsync(Guid packageId, CancellationToken cancellationToken = default);
    Task UpsertAsync(Package package, CancellationToken cancellationToken = default);
}
