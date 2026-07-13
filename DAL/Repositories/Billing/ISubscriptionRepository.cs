using PRN222_FINAL.DAL.Entities.Billing;

namespace PRN222_FINAL.DAL.Repositories.Billing;

public interface ISubscriptionRepository
{
    Task AddAsync(KnowledgeSqlSubscription subscription, CancellationToken cancellationToken = default);
    Task<KnowledgeSqlSubscription?> GetByPaymentIdAsync(Guid paymentId, CancellationToken cancellationToken = default);
    Task<KnowledgeSqlSubscription?> GetCurrentActiveAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KnowledgeSqlSubscription>> GetActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KnowledgeSqlSubscription>> GetUnexpiredByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task ActivateExclusiveAsync(KnowledgeSqlSubscription subscription, DateTimeOffset activatedAt, CancellationToken cancellationToken = default);
    Task UpdateAsync(KnowledgeSqlSubscription subscription, CancellationToken cancellationToken = default);
}
