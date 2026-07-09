using PRN222_FINAL.Models;

namespace PRN222_FINAL.Models.DTOs.Billing;

public sealed class PaymentWebhookResultDto
{
    public Guid PaymentId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public PaymentStatus Status { get; set; }
    public bool IsDuplicate { get; set; }
    public bool SubscriptionActivated { get; set; }
    public string Message { get; set; } = string.Empty;
}
