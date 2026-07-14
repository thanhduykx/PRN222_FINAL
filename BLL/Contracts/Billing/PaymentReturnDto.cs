using PRN222_FINAL.BLL.Models;

namespace PRN222_FINAL.BLL.Contracts.Billing;

public sealed class PaymentReturnDto
{
    public Guid PaymentId { get; set; }
    public PaymentProvider Provider { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string ProviderTransactionId { get; set; } = string.Empty;
    public PaymentStatus Status { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string PackageCode { get; set; } = string.Empty;
    public decimal AmountVnd { get; set; }
    public string Currency { get; set; } = "VND";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset? SubscriptionStartsAt { get; set; }
    public DateTimeOffset? SubscriptionEndsAt { get; set; }
    public bool IsLifetime { get; set; }
    public int MonthlyChatLimit { get; set; }
    public int MonthlyDocumentUploadLimit { get; set; }
    public int StorageLimitMb { get; set; }
    public string Message { get; set; } = string.Empty;
}
