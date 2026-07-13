namespace PRN222_FINAL.BLL.Contracts.Notifications;

public sealed class SystemNotificationDto
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
}
