namespace PRN222_FINAL.BLL.Models;

public sealed class Subscription
{
    public Guid Id { get; set; }
    public Guid PackageId { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public SubscriptionStatus Status { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public Guid? PaymentId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Package? Package { get; set; }
}
