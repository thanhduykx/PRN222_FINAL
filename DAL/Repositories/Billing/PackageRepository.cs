using Microsoft.EntityFrameworkCore;
using PRN222_FINAL.DAL.Mapping;
using PRN222_FINAL.Models;

namespace PRN222_FINAL.DAL.Repositories.Billing;

public sealed class PackageRepository : SqlBillingRepositoryBase, IPackageRepository
{
    public PackageRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task<IReadOnlyList<Package>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Packages
            .AsNoTracking()
            .OrderBy(package => package.SortOrder)
            .ThenBy(package => package.PriceVnd)
            .Select(package => BillingSqlMapper.ToModel(package))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Package>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Packages
            .AsNoTracking()
            .Where(package => package.IsActive)
            .OrderBy(package => package.SortOrder)
            .ThenBy(package => package.PriceVnd)
            .Select(package => BillingSqlMapper.ToModel(package))
            .ToListAsync(cancellationToken);
    }

    public async Task<Package?> GetByIdAsync(Guid packageId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var entity = await context.Packages.AsNoTracking().FirstOrDefaultAsync(package => package.Id == packageId, cancellationToken);
        return entity is null ? null : BillingSqlMapper.ToModel(entity);
    }

    public async Task UpsertAsync(Package package, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var existing = await context.Packages.FirstOrDefaultAsync(item => item.Code == package.Code, cancellationToken);
        if (existing is null)
        {
            context.Packages.Add(BillingSqlMapper.ToEntity(package));
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
            existing.IsActive = package.IsActive;
            existing.SortOrder = package.SortOrder;
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
