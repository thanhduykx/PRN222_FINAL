namespace PRN222_FINAL.BLL.Options;

public sealed class PaymentOptions
{
    public string BaseReturnUrl { get; set; } = string.Empty;
    public MomoOptions MoMo { get; set; } = new();
    public PayOsOptions PayOS { get; set; } = new();
}

public sealed class MomoOptions
{
    public string PartnerCode { get; set; } = string.Empty;
    public string AccessKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = "https://test-payment.momo.vn/v2/gateway/api/create";
    public string RequestType { get; set; } = "captureWallet";
}

public sealed class PayOsOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ChecksumKey { get; set; } = string.Empty;
    public string Endpoint { get; set; } = "https://api-merchant.payos.vn/v2/payment-requests";
}
