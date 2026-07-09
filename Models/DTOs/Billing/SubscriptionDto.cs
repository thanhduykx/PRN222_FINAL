using PRN222_FINAL.Models;

namespace PRN222_FINAL.Models.DTOs.Billing;

public sealed class SubscriptionDto
{
    public Guid Id { get; set; }
    public Guid PackageId { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public SubscriptionStatus Status { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public int MonthlyChatLimit { get; set; }
    public int MonthlyDocumentUploadLimit { get; set; }
    public int StorageLimitMb { get; set; }
    public bool IsLifetime { get; set; }
}
