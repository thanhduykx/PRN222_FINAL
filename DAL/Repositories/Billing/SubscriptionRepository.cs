using Microsoft.EntityFrameworkCore;
using PRN222_FINAL.DAL.Mapping;
using PRN222_FINAL.Models;

namespace PRN222_FINAL.DAL.Repositories.Billing;

public sealed class SubscriptionRepository : SqlBillingRepositoryBase, ISubscriptionRepository
{
    public SubscriptionRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task AddAsync(Subscription subscription, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        context.Subscriptions.Add(BillingSqlMapper.ToEntity(subscription));
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Subscription?> GetByPaymentIdAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var entity = await context.Subscriptions
            .AsNoTracking()
            .Include(subscription => subscription.Package)
            .FirstOrDefaultAsync(subscription => subscription.PaymentId == paymentId, cancellationToken);

        return entity is null ? null : BillingSqlMapper.ToModel(entity);
    }

    public async Task<Subscription?> GetCurrentActiveAsync(Guid userId, CancellationToken cancellationToken = default)
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

        return entity is null ? null : BillingSqlMapper.ToModel(entity);
    }

    public async Task<IReadOnlyList<Subscription>> GetActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default)
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
            .Select(subscription => BillingSqlMapper.ToModel(subscription))
            .ToListAsync(cancellationToken);
    }

    public async Task UpdateAsync(Subscription subscription, CancellationToken cancellationToken = default)
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
