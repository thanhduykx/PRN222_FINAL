using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PRN222_FINAL.DAL.Entities;

[Table("system_notifications")]
public sealed class KnowledgeSqlSystemNotification
{
    [Key]
    public Guid Id { get; set; }

    [Required, MaxLength(64)]
    public string Type { get; set; } = string.Empty;

    public Guid? EntityId { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string Message { get; set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; set; }
}

public static class SystemNotificationTypes
{
    public const string PackagePriceChanged = "PackagePriceChanged";
    public const string SubjectRenamed = "SubjectRenamed";
}
