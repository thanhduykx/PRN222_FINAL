using PRN222_FINAL.Models;

namespace PRN222_FINAL.Models.DTOs.Billing;

public sealed class PaymentHistoryItemDto
{
    public Guid PaymentId { get; set; }
    public Guid PackageId { get; set; }
    public string PackageName { get; set; } = string.Empty;
    public string PackageCode { get; set; } = string.Empty;
    public PaymentProvider Provider { get; set; }
    public PaymentStatus Status { get; set; }
    public decimal AmountVnd { get; set; }
    public string Currency { get; set; } = "VND";
    public string OrderCode { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public string FailureReason { get; set; } = string.Empty;
}
