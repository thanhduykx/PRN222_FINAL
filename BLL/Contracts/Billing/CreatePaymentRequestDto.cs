using PRN222_FINAL.BLL.Models;

namespace PRN222_FINAL.BLL.Contracts.Billing;

public sealed class CreatePaymentRequestDto
{
    public Guid PackageId { get; set; }
    public PaymentProvider Provider { get; set; }
    public Guid UserId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
}
