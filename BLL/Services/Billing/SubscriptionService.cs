using PRN222_FINAL.BLL.Mapping;
using PRN222_FINAL.DAL.Repositories.Billing;
using PRN222_FINAL.Models.DTOs.Billing;

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
        return subscription is null ? null : BillingDtoMapper.ToDto(subscription);
    }
}
