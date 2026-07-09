using PRN222_FINAL.Models;

namespace PRN222_FINAL.Models.DTOs.Billing;

public sealed class PaymentReturnDto
{
    public PaymentProvider Provider { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string ProviderTransactionId { get; set; } = string.Empty;
    public PaymentStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
}
