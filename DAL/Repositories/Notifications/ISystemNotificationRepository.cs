using PRN222_FINAL.DAL.Entities;

namespace PRN222_FINAL.DAL.Repositories.Notifications;

public interface ISystemNotificationRepository
{
    Task<IReadOnlyList<KnowledgeSqlSystemNotification>> GetSinceAsync(
        DateTimeOffset sinceUtc,
        int limit,
        CancellationToken cancellationToken = default);
}
