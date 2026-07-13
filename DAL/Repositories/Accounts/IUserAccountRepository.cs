using PRN222_FINAL.DAL.Models.Accounts;

namespace PRN222_FINAL.DAL.Repositories.Accounts;

public interface IUserAccountRepository
{
    Task<List<UserAccountData>> LoadAllAsync(CancellationToken cancellationToken = default);
    Task UpsertAsync(UserAccountData user, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid userId, CancellationToken cancellationToken = default);
    Task UpdateLastActiveAsync(Guid userId, DateTimeOffset activeAt, CancellationToken cancellationToken = default);
    Task RecordLoginFailureAsync(Guid userId, int maxFailures, DateTimeOffset lockoutEnd, CancellationToken cancellationToken = default);
    Task ResetLoginFailuresAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SetSuspendedAsync(Guid userId, bool isSuspended, DateTimeOffset changedAt, CancellationToken cancellationToken = default);
}
