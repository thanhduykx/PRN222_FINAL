using Microsoft.EntityFrameworkCore;
using PRN222_FINAL.DAL.Entities.Billing;
using PRN222_FINAL.DAL.Enums;

namespace PRN222_FINAL.DAL.Repositories.Billing;

public sealed class SubscriptionRepository : SqlBillingRepositoryBase, ISubscriptionRepository
{
    public SubscriptionRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task AddAsync(KnowledgeSqlSubscription subscription, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        context.Subscriptions.Add(subscription);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<KnowledgeSqlSubscription?> GetByPaymentIdAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var entity = await context.Subscriptions
            .AsNoTracking()
            .Include(subscription => subscription.Package)
            .FirstOrDefaultAsync(subscription => subscription.PaymentId == paymentId, cancellationToken);

        return entity;
    }

    public async Task<KnowledgeSqlSubscription?> GetCurrentActiveAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        await using var context = CreateContext();
        var entity = await context.Subscriptions
            .AsNoTracking()
            .Include(subscription => subscription.Package)
            .Where(subscription => subscription.UserId == userId
                && subscription.Status == SubscriptionStatus.Active
                && subscription.StartsAt <= now
                && subscription.EndsAt > now)
            .OrderByDescending(subscription => subscription.EndsAt)
            .FirstOrDefaultAsync(cancellationToken);

        return entity;
    }

    public async Task<KnowledgeSqlSubscription?> RenewExpiredFreeAsync(
        Guid userId,
        DateTimeOffset renewedAt,
        CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var lockKey = $"subscription:{userId:N}";
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
            cancellationToken);

        var current = await context.Subscriptions
            .AsNoTracking()
            .Include(subscription => subscription.Package)
            .Where(subscription => subscription.UserId == userId
                && subscription.Status == SubscriptionStatus.Active
                && subscription.StartsAt <= renewedAt
                && subscription.EndsAt > renewedAt)
            .OrderByDescending(subscription => subscription.EndsAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (current is not null)
        {
            await transaction.CommitAsync(cancellationToken);
            return current;
        }

        var expiredFree = await context.Subscriptions
            .AsNoTracking()
            .Include(subscription => subscription.Package)
            .Where(subscription => subscription.UserId == userId
                && subscription.Status == SubscriptionStatus.Active
                && subscription.EndsAt <= renewedAt)
            .OrderByDescending(subscription => subscription.EndsAt)
            .ThenByDescending(subscription => subscription.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (expiredFree?.Package is null
            || !expiredFree.Package.Code.Equals("FREE", StringComparison.OrdinalIgnoreCase))
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var freePackage = expiredFree.Package;
        var renewal = new KnowledgeSqlSubscription
        {
            Id = Guid.NewGuid(),
            PackageId = freePackage.Id,
            UserId = expiredFree.UserId,
            UserName = expiredFree.UserName,
            UserEmail = expiredFree.UserEmail,
            Status = SubscriptionStatus.Active,
            StartsAt = renewedAt,
            EndsAt = renewedAt.AddDays(Math.Max(1, freePackage.DurationDays)),
            PaymentId = null,
            CreatedAt = renewedAt
        };
        context.Subscriptions.Add(renewal);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        renewal.Package = freePackage;
        return renewal;
    }

    public async Task<IReadOnlyList<KnowledgeSqlSubscription>> GetActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        await using var context = CreateContext();
        return await context.Subscriptions
            .AsNoTracking()
            .Include(subscription => subscription.Package)
            .Where(subscription => subscription.UserId == userId
                && subscription.Status == SubscriptionStatus.Active
                && subscription.EndsAt > now)
            .OrderBy(subscription => subscription.StartsAt)
            .ToListAsync(cancellationToken);
    }

    public async Task ActivateExclusiveAsync(
        KnowledgeSqlSubscription subscription,
        DateTimeOffset activatedAt,
        CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var lockKey = $"subscription:{subscription.UserId:N}";
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
            cancellationToken);

        var target = await context.Subscriptions
            .FirstOrDefaultAsync(item => item.Id == subscription.Id, cancellationToken);
        if (target is not null
            && target.Status == SubscriptionStatus.Active
            && target.StartsAt <= activatedAt
            && target.EndsAt > activatedAt)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var superseded = await context.Subscriptions
            .Where(item => item.UserId == subscription.UserId
                && item.Status == SubscriptionStatus.Active
                && item.Id != subscription.Id)
            .ToListAsync(cancellationToken);

        var calculatedEnd = SubscriptionActivationPolicy.CalculateEnd(
            activatedAt,
            subscription.StartsAt,
            subscription.EndsAt,
            superseded.Where(item => item.StartsAt <= activatedAt).Select(item => item.EndsAt));
        subscription.StartsAt = activatedAt;
        subscription.EndsAt = calculatedEnd;

        foreach (var item in superseded)
        {
            item.Status = SubscriptionStatus.Canceled;
            if (item.StartsAt > activatedAt)
            {
                item.StartsAt = activatedAt;
            }
            item.EndsAt = item.EndsAt > activatedAt ? activatedAt : item.EndsAt;
        }

        if (target is null)
        {
            context.Subscriptions.Add(subscription);
        }
        else
        {
            target.PackageId = subscription.PackageId;
            target.UserId = subscription.UserId;
            target.UserName = subscription.UserName;
            target.UserEmail = subscription.UserEmail;
            target.Status = SubscriptionStatus.Active;
            target.StartsAt = subscription.StartsAt;
            target.EndsAt = subscription.EndsAt;
            target.PaymentId = subscription.PaymentId;
        }

        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpdateAsync(KnowledgeSqlSubscription subscription, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var entity = await context.Subscriptions.FirstOrDefaultAsync(item => item.Id == subscription.Id, cancellationToken)
            ?? throw new InvalidOperationException("Subscription not found.");

        entity.PackageId = subscription.PackageId;
        entity.UserId = subscription.UserId;
        entity.UserName = subscription.UserName;
        entity.UserEmail = subscription.UserEmail;
        entity.Status = subscription.Status;
        entity.StartsAt = subscription.StartsAt;
        entity.EndsAt = subscription.EndsAt;
        entity.PaymentId = subscription.PaymentId;
        await context.SaveChangesAsync(cancellationToken);
    }
}
