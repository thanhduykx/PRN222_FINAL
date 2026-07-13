using PRN222_FINAL.DAL.Models.Accounts;

namespace PRN222_FINAL.DAL.Repositories.Accounts;

public interface IAccountEmailJobRepository
{
    Task EnqueueAsync(AccountEmailJobData job, CancellationToken cancellationToken = default);
    Task<AccountEmailJobData?> ClaimNextAsync(DateTimeOffset now, TimeSpan lease, CancellationToken cancellationToken = default);
    Task CompleteAsync(Guid jobId, DateTimeOffset completedAt, CancellationToken cancellationToken = default);
    Task RescheduleAsync(Guid jobId, DateTimeOffset availableAt, string error, bool terminal, CancellationToken cancellationToken = default);
}
