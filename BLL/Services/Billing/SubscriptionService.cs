using PRN222_FINAL.BLL.Mapping;
using PRN222_FINAL.DAL.Repositories.Billing;
using PRN222_FINAL.BLL.Contracts.Billing;

namespace PRN222_FINAL.BLL.Services.Billing;

public sealed class SubscriptionService : ISubscriptionService
{
    private readonly ISubscriptionRepository _subscriptions;

    public SubscriptionService(ISubscriptionRepository subscriptions)
    {
        _subscriptions = subscriptions;
    }

    public async Task<SubscriptionDto?> GetCurrentSubscriptionAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new InvalidOperationException("User is required.");
        }

        var subscription = await _subscriptions.GetCurrentActiveAsync(userId, cancellationToken);
        return subscription is null ? null : BillingDtoMapper.ToDto(BillingDtoMapper.ToModel(subscription));
    }

    public async Task<IReadOnlyList<SubscriptionDto>> GetUnexpiredByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new InvalidOperationException("User is required.");
        }

        var subs = await _subscriptions.GetUnexpiredByUserAsync(userId, cancellationToken);
        return subs.Select(s => BillingDtoMapper.ToDto(BillingDtoMapper.ToModel(s))).ToList();
    }
}
