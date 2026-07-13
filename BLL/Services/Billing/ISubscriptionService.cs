using PRN222_FINAL.BLL.Contracts.Billing;

namespace PRN222_FINAL.BLL.Services.Billing;

public interface ISubscriptionService
{
    Task<SubscriptionDto?> GetCurrentSubscriptionAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubscriptionDto>> GetUnexpiredByUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
