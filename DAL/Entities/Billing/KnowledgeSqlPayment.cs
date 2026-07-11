using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using PRN222_FINAL.DAL.Enums;

namespace PRN222_FINAL.DAL.Entities.Billing;

[Table("payments")]
public sealed class KnowledgeSqlPayment
{
    [Key]
    public Guid Id { get; set; }

    public Guid PackageId { get; set; }
    public Guid UserId { get; set; }

    [Required, MaxLength(255)]
    public string UserName { get; set; } = string.Empty;

    [Required, MaxLength(255)]
    public string UserEmail { get; set; } = string.Empty;

    public PaymentProvider Provider { get; set; }
    public PaymentStatus Status { get; set; }

    [Column(TypeName = "numeric(18,2)")]
    public decimal AmountVnd { get; set; }

    [Required, MaxLength(8)]
    public string Currency { get; set; } = "VND";

    [Required, MaxLength(80)]
    public string OrderCode { get; set; } = string.Empty;

    [MaxLength(255)]
    public string ProviderTransactionId { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string CheckoutUrl { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string QrCode { get; set; } = string.Empty;

    public string RawRequest { get; set; } = string.Empty;
    public string RawResponse { get; set; } = string.Empty;
    public string RawWebhook { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset? FailedAt { get; set; }

    [MaxLength(1000)]
    public string FailureReason { get; set; } = string.Empty;

    public KnowledgeSqlPackage? Package { get; set; }
}
