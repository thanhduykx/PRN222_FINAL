using PRN222_FINAL.BLL.Mapping;
using PRN222_FINAL.BLL.Models;
using PRN222_FINAL.BLL.Security;
using PRN222_FINAL.BLL.Services.Billing;
using PRN222_FINAL.DAL.Repositories.Billing;

namespace PRN222_FINAL.BLL.Services.Accounts;

public interface IUserProvisioningService
{
    Task<UserAccount> CreateLocalForAdminAsync(
        string fullName,
        string email,
        string password,
        string role,
        CancellationToken cancellationToken = default);
}

public sealed class UserProvisioningService : IUserProvisioningService
{
    private readonly IUserAccountService _users;
    private readonly IPackageService _packages;
    private readonly ISubscriptionRepository _subscriptions;

    public UserProvisioningService(
        IUserAccountService users,
        IPackageService packages,
        ISubscriptionRepository subscriptions)
    {
        _users = users;
        _packages = packages;
        _subscriptions = subscriptions;
    }

    public async Task<UserAccount> CreateLocalForAdminAsync(
        string fullName,
        string email,
        string password,
        string role,
        CancellationToken cancellationToken = default)
    {
        var account = await _users.CreateLocalForAdminAsync(fullName, email, password, role, cancellationToken);
        await EnsureFreeSubscriptionAsync(account, cancellationToken);
        return account;
    }

    private async Task EnsureFreeSubscriptionAsync(UserAccount account, CancellationToken cancellationToken)
    {
        if (AppRoles.Normalize(account.Role) != AppRoles.Student
            || await _subscriptions.GetCurrentActiveAsync(account.Id, cancellationToken) is not null)
        {
            return;
        }

        var freePackage = (await _packages.GetActivePackagesAsync(cancellationToken))
            .FirstOrDefault(item => item.Code.Equals("FREE", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The FREE package is not configured.");
        var now = DateTimeOffset.UtcNow;
        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            PackageId = freePackage.Id,
            UserId = account.Id,
            UserName = account.FullName,
            UserEmail = account.Email,
            Status = SubscriptionStatus.Active,
            StartsAt = now,
            EndsAt = now.AddDays(Math.Max(1, freePackage.DurationDays)),
            PaymentId = null,
            CreatedAt = now
        };

        await _subscriptions.ActivateExclusiveAsync(BillingDtoMapper.ToEntity(subscription), now, cancellationToken);
    }
}
