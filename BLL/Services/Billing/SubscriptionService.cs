using PRN222_FINAL.BLL.Mapping;
using PRN222_FINAL.DAL.Repositories.Billing;
using PRN222_FINAL.BLL.Contracts.Billing;

namespace PRN222_FINAL.BLL.Services.Billing;

public sealed class SubscriptionService : ISubscriptionService
{
    private readonly ISubscriptionRepository _subscriptions;
    private readonly TimeProvider _timeProvider;

    public SubscriptionService(ISubscriptionRepository subscriptions, TimeProvider? timeProvider = null)
    {
        _subscriptions = subscriptions;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<SubscriptionDto?> GetCurrentSubscriptionAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new InvalidOperationException("User is required.");
        }

        var subscription = await _subscriptions.GetCurrentActiveAsync(userId, cancellationToken);
        if (subscription is null)
        {
            subscription = await _subscriptions.RenewExpiredFreeAsync(
                userId,
                _timeProvider.GetUtcNow(),
                cancellationToken);
        }

        return subscription is null ? null : BillingDtoMapper.ToDto(BillingDtoMapper.ToModel(subscription));
    }
}
