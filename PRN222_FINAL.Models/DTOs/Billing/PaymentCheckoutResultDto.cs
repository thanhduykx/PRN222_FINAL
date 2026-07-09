using PRN222_FINAL.Models;

namespace PRN222_FINAL.Models.DTOs.Billing;

public sealed class PaymentCheckoutResultDto
{
    public Guid PaymentId { get; set; }
    public Guid PackageId { get; set; }
    public PaymentProvider Provider { get; set; }
    public PaymentStatus Status { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string CheckoutUrl { get; set; } = string.Empty;
    public string QrCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
