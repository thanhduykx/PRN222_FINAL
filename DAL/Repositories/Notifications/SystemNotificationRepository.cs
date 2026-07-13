using Microsoft.EntityFrameworkCore;
using PRN222_FINAL.DAL.Entities;
using PRN222_FINAL.DAL.Repositories.Billing;

namespace PRN222_FINAL.DAL.Repositories.Notifications;

public sealed class SystemNotificationRepository : SqlBillingRepositoryBase, ISystemNotificationRepository
{
    public SystemNotificationRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task<IReadOnlyList<KnowledgeSqlSystemNotification>> GetSinceAsync(
        DateTimeOffset sinceUtc,
        int limit,
        CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.SystemNotifications
            .AsNoTracking()
            .Where(notification => notification.OccurredAt >= sinceUtc)
            .OrderBy(notification => notification.OccurredAt)
            .ThenBy(notification => notification.Id)
            .Take(Math.Clamp(limit, 1, 100))
            .ToListAsync(cancellationToken);
    }
}
