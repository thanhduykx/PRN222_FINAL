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
        var notifications = await context.SystemNotifications
            .AsNoTracking()
            .Where(notification => notification.OccurredAt >= sinceUtc)
            .OrderBy(notification => notification.OccurredAt)
            .ThenBy(notification => notification.Id)
            .Take(Math.Clamp(limit, 1, 100))
            .ToListAsync(cancellationToken);

        var packageNotifications = notifications
            .Where(notification =>
                notification.Type == SystemNotificationTypes.PackagePriceChanged
                && notification.EntityId.HasValue
                && !notification.Message.Contains(
                    PackagePriceChangeNotificationFormatter.ReasonLabel,
                    StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (packageNotifications.Count == 0)
        {
            return notifications;
        }

        var packageIds = packageNotifications
            .Select(notification => notification.EntityId!.Value)
            .Distinct()
            .ToArray();
        var priceChanges = await context.PackagePriceChanges
            .AsNoTracking()
            .Where(change => packageIds.Contains(change.PackageId) && change.ChangedAt >= sinceUtc)
            .ToListAsync(cancellationToken);
        var changesByNotification = priceChanges
            .GroupBy(change => (change.PackageId, change.ChangedAt))
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var notification in packageNotifications)
        {
            if (changesByNotification.TryGetValue(
                (notification.EntityId!.Value, notification.OccurredAt),
                out var change))
            {
                notification.Message = PackagePriceChangeNotificationFormatter.AppendReason(
                    notification.Message,
                    change.Reason);
            }
        }

        return notifications;
    }
}
