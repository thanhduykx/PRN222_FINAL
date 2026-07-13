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

    public async Task RemoveRetiredAsync(string packageCode, CancellationToken cancellationToken = default)
    {
        var normalizedCode = (packageCode ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return;
        }

        await using var context = CreateContext();
        var package = await context.Packages
            .FirstOrDefaultAsync(item => item.Code.ToUpper() == normalizedCode, cancellationToken);
        if (package is null)
        {
            return;
        }

        var hasHistory = await context.Payments.AnyAsync(item => item.PackageId == package.Id, cancellationToken)
                         || await context.Subscriptions.AnyAsync(item => item.PackageId == package.Id, cancellationToken);
        if (hasHistory)
        {
            package.IsActive = false;
            package.SortOrder = int.MaxValue;
        }
        else
        {
            await context.PackagePriceChanges
                .Where(change => change.PackageId == package.Id)
                .ExecuteDeleteAsync(cancellationToken);
            context.Packages.Remove(package);
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<KnowledgeSqlPackagePriceChange?> UpdatePriceAsync(
        Guid packageId,
        decimal expectedCurrentPriceVnd,
        decimal newPriceVnd,
        string changedBy,
        string reason,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable,
            cancellationToken);
        var package = await context.Packages.FirstOrDefaultAsync(item => item.Id == packageId, cancellationToken)
            ?? throw new InvalidOperationException("Package not found.");
        if (package.PriceVnd != expectedCurrentPriceVnd)
        {
            throw new InvalidOperationException("Package price changed since this page was loaded.");
        }
        if (package.PriceVnd == newPriceVnd)
        {
            return null;
        }

        var change = new KnowledgeSqlPackagePriceChange
        {
            Id = Guid.NewGuid(),
            PackageId = package.Id,
            PackageName = package.Name,
            OldPriceVnd = package.PriceVnd,
            NewPriceVnd = newPriceVnd,
            ChangedBy = changedBy,
            Reason = reason,
            ChangedAt = changedAt
        };
        package.PriceVnd = newPriceVnd;
        context.PackagePriceChanges.Add(change);
        var priceCulture = System.Globalization.CultureInfo.GetCultureInfo("vi-VN");
        context.SystemNotifications.Add(new PRN222_FINAL.DAL.Entities.KnowledgeSqlSystemNotification
        {
            Id = Guid.NewGuid(),
            Type = PRN222_FINAL.DAL.Entities.SystemNotificationTypes.PackagePriceChanged,
            EntityId = package.Id,
            Title = $"Cập nhật giá gói {package.Name}",
            Message = $"Giá gói {package.Name} đã thay đổi từ {change.OldPriceVnd.ToString("N0", priceCulture)}đ thành {change.NewPriceVnd.ToString("N0", priceCulture)}đ. Giá mới áp dụng cho các giao dịch tiếp theo.",
            OccurredAt = changedAt
        });
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return change;
    }

    public async Task<KnowledgeSqlPackagePriceChange?> GetLatestPriceChangeAsync(CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.PackagePriceChanges
            .AsNoTracking()
            .OrderByDescending(change => change.ChangedAt)
            .ThenByDescending(change => change.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<KnowledgeSqlPackagePriceChange>> GetPriceChangesAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.PackagePriceChanges
            .AsNoTracking()
            .OrderByDescending(change => change.ChangedAt)
            .ThenByDescending(change => change.Id)
            .Take(Math.Clamp(limit, 1, 100))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, int>> GetPendingPaymentCountsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Payments
            .AsNoTracking()
            .Where(payment => payment.Status == PRN222_FINAL.DAL.Enums.PaymentStatus.Pending)
            .GroupBy(payment => payment.PackageId)
            .Select(group => new { PackageId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.PackageId, item => item.Count, cancellationToken);
    }
}
