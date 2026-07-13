using PRN222_FINAL.BLL.Contracts.Notifications;
using PRN222_FINAL.DAL.Repositories.Notifications;

namespace PRN222_FINAL.BLL.Services.Notifications;

public interface ISystemNotificationService
{
    Task<IReadOnlyList<SystemNotificationDto>> GetActiveAsync(CancellationToken cancellationToken = default);
}

public sealed class SystemNotificationService : ISystemNotificationService
{
    internal static readonly TimeSpan VisibilityWindow = TimeSpan.FromDays(3);
    private const int MaxNotifications = 50;
    private readonly ISystemNotificationRepository _notifications;
    private readonly TimeProvider _timeProvider;

    public SystemNotificationService(
        ISystemNotificationRepository notifications,
        TimeProvider? timeProvider = null)
    {
        _notifications = notifications;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<IReadOnlyList<SystemNotificationDto>> GetActiveAsync(
        CancellationToken cancellationToken = default)
    {
        var sinceUtc = _timeProvider.GetUtcNow() - VisibilityWindow;
        var notifications = await _notifications.GetSinceAsync(sinceUtc, MaxNotifications, cancellationToken);
        return notifications.Select(notification => new SystemNotificationDto
        {
            Id = notification.Id,
            Type = notification.Type,
            EntityId = notification.EntityId,
            Title = notification.Title,
            Message = notification.Message,
            OccurredAt = notification.OccurredAt
        }).ToList();
    }
}
