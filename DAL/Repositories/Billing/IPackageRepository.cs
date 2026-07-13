using PRN222_FINAL.DAL.Entities.Billing;

namespace PRN222_FINAL.DAL.Repositories.Billing;

public interface IPackageRepository
{
    Task<IReadOnlyList<KnowledgeSqlPackage>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KnowledgeSqlPackage>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<KnowledgeSqlPackage?> GetByIdAsync(Guid packageId, CancellationToken cancellationToken = default);
    Task UpsertAsync(KnowledgeSqlPackage package, CancellationToken cancellationToken = default);
    Task<KnowledgeSqlPackagePriceChange?> UpdatePriceAsync(Guid packageId, decimal newPriceVnd, string changedBy, DateTimeOffset changedAt, CancellationToken cancellationToken = default);
    Task<KnowledgeSqlPackagePriceChange?> GetLatestPriceChangeAsync(CancellationToken cancellationToken = default);
}
