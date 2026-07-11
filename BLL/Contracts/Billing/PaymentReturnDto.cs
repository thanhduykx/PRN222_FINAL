using PRN222_FINAL.BLL.Models;

namespace PRN222_FINAL.BLL.Contracts.Billing;

public sealed class PaymentReturnDto
{
    public PaymentProvider Provider { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string ProviderTransactionId { get; set; } = string.Empty;
    public PaymentStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
}
