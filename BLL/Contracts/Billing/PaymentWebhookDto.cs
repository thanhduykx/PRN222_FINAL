using PRN222_FINAL.BLL.Models;

namespace PRN222_FINAL.BLL.Contracts.Billing;

public sealed class PaymentWebhookDto
{
    public PaymentProvider Provider { get; set; }
    public string RawBody { get; set; } = string.Empty;
    public IReadOnlyDictionary<string, string> Values { get; set; } = new Dictionary<string, string>();
}
