using PRN222_FINAL.Models;

namespace PRN222_FINAL.DAL.Repositories.Billing;

public interface ISubscriptionRepository
{
    Task AddAsync(Subscription subscription, CancellationToken cancellationToken = default);
    Task<Subscription?> GetByPaymentIdAsync(Guid paymentId, CancellationToken cancellationToken = default);
    Task<Subscription?> GetCurrentActiveAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Subscription>> GetActiveByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task UpdateAsync(Subscription subscription, CancellationToken cancellationToken = default);
}
