using Microsoft.EntityFrameworkCore;
using PRN222_FINAL.DAL.Entities.Billing;

namespace PRN222_FINAL.DAL.Repositories.Billing;

public sealed class PackageRepository : SqlBillingRepositoryBase, IPackageRepository
{
    public PackageRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task<IReadOnlyList<KnowledgeSqlPackage>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Packages
            .AsNoTracking()
            .OrderBy(package => package.SortOrder)
            .ThenBy(package => package.PriceVnd)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<KnowledgeSqlPackage>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Packages
            .AsNoTracking()
            .Where(package => package.IsActive)
            .OrderBy(package => package.SortOrder)
            .ThenBy(package => package.PriceVnd)
            .ToListAsync(cancellationToken);
    }

    public async Task<KnowledgeSqlPackage?> GetByIdAsync(Guid packageId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Packages.AsNoTracking().FirstOrDefaultAsync(package => package.Id == packageId, cancellationToken);
    }

    public async Task UpsertAsync(KnowledgeSqlPackage package, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var existing = await context.Packages.FirstOrDefaultAsync(item => item.Code == package.Code, cancellationToken);
        if (existing is null)
        {
            context.Packages.Add(package);
        }
        else
        {
            existing.Name = package.Name;
            existing.Description = package.Description;
            existing.PriceVnd = package.PriceVnd;
            existing.DurationDays = package.DurationDays;
            existing.MonthlyChatLimit = package.MonthlyChatLimit;
            existing.MonthlyDocumentUploadLimit = package.MonthlyDocumentUploadLimit;
            existing.StorageLimitMb = package.StorageLimitMb;
            existing.IsLifetime = package.IsLifetime;
            existing.IsActive = package.IsActive;
            existing.SortOrder = package.SortOrder;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
