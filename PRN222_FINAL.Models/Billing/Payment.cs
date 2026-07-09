namespace PRN222_FINAL.Models;

public sealed class Payment
{
    public Guid Id { get; set; }
    public Guid PackageId { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public PaymentProvider Provider { get; set; }
    public PaymentStatus Status { get; set; }
    public decimal AmountVnd { get; set; }
    public string Currency { get; set; } = "VND";
    public string OrderCode { get; set; } = string.Empty;
    public string ProviderTransactionId { get; set; } = string.Empty;
    public string CheckoutUrl { get; set; } = string.Empty;
    public string QrCode { get; set; } = string.Empty;
    public string RawRequest { get; set; } = string.Empty;
    public string RawResponse { get; set; } = string.Empty;
    public string RawWebhook { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? PaidAt { get; set; }
    public DateTimeOffset? FailedAt { get; set; }
    public string FailureReason { get; set; } = string.Empty;
}
