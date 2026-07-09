using PRN222_FINAL.Models;

namespace PRN222_FINAL.BLL.Services.Billing.Gateways;

public sealed class PaymentGatewayCreateRequest
{
    public PaymentProvider Provider { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public long PayOsOrderCode { get; set; }
    public decimal AmountVnd { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = string.Empty;
    public string CancelUrl { get; set; } = string.Empty;
    public string IpnUrl { get; set; } = string.Empty;
    public string UserIpAddress { get; set; } = string.Empty;
}

public sealed class PaymentGatewayCreateResult
{
    public string ProviderTransactionId { get; set; } = string.Empty;
    public string CheckoutUrl { get; set; } = string.Empty;
    public string QrCode { get; set; } = string.Empty;
    public string RawRequest { get; set; } = string.Empty;
    public string RawResponse { get; set; } = string.Empty;
}

public sealed class PaymentGatewayWebhookResult
{
    public bool IsSignatureValid { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public string ProviderTransactionId { get; set; } = string.Empty;
    public PaymentStatus Status { get; set; }
    public string Message { get; set; } = string.Empty;
    public decimal? AmountVnd { get; set; }
}
