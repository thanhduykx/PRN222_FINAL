using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PRN222_FINAL.DAL.Enums;

namespace PRN222_FINAL.DAL.Entities.Billing;

[Table("subscriptions")]
public sealed class KnowledgeSqlSubscription
{
    [Key]
    public Guid Id { get; set; }

    public Guid PackageId { get; set; }
    public Guid UserId { get; set; }

    [Required, MaxLength(255)]
    public string UserName { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string UserEmail { get; set; } = string.Empty;

    public SubscriptionStatus Status { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
    public Guid? PaymentId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public KnowledgeSqlPackage? Package { get; set; }
    public KnowledgeSqlPayment? Payment { get; set; }
}
