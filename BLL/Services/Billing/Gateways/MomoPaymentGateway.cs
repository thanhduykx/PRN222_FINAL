using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PRN222_FINAL.BLL.Options;
using PRN222_FINAL.BLL.Models;
using PRN222_FINAL.BLL.Contracts.Billing;
using PRN222_FINAL.DAL.Models.Http;
using PRN222_FINAL.DAL.Repositories.Http;

namespace PRN222_FINAL.BLL.Services.Billing.Gateways;

public sealed class MomoPaymentGateway : IMomoPaymentGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IHttpRepository _http;
    private readonly PaymentOptions _options;

    public MomoPaymentGateway(IHttpRepository http, IOptions<PaymentOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public async Task<PaymentGatewayCreateResult> CreateCheckoutAsync(PaymentGatewayCreateRequest request, CancellationToken cancellationToken = default)
    {
        var momo = _options.MoMo;
        EnsureConfigured(momo.PartnerCode, momo.AccessKey, momo.SecretKey, momo.Endpoint, "MoMo");

        var amount = ((long)request.AmountVnd).ToString();
        var requestId = request.OrderCode;
        var extraData = string.Empty;
        var rawSignature =
            $"accessKey={momo.AccessKey}&amount={amount}&extraData={extraData}&ipnUrl={request.IpnUrl}&orderId={request.OrderCode}&orderInfo={request.Description}&partnerCode={momo.PartnerCode}&redirectUrl={request.ReturnUrl}&requestId={requestId}&requestType={momo.RequestType}";
        var signature = PaymentSignatureHelper.HmacSha256(rawSignature, momo.SecretKey);
        var payload = new Dictionary<string, object?>
        {
            ["partnerCode"] = momo.PartnerCode,
            ["partnerName"] = "PRN222_FINAL",
            ["storeId"] = "PRN222_FINAL",
            ["requestId"] = requestId,
            ["amount"] = amount,
            ["orderId"] = request.OrderCode,
            ["orderInfo"] = request.Description,
            ["redirectUrl"] = request.ReturnUrl,
            ["ipnUrl"] = request.IpnUrl,
            ["lang"] = "vi",
            ["requestType"] = momo.RequestType,
            ["autoCapture"] = true,
            ["extraData"] = extraData,
            ["signature"] = signature
        };

        var rawRequest = JsonSerializer.Serialize(payload, JsonOptions);
        var response = await _http.SendAsync(new HttpRequestData("POST", momo.Endpoint, rawRequest), cancellationToken);
        var rawResponse = response.Body;
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"MoMo checkout failed: {response.StatusCode} {rawResponse}");
        }

        using var json = JsonDocument.Parse(rawResponse);
        var root = json.RootElement;
        var resultCode = ReadInt(root, "resultCode");
        if (resultCode != 0)
        {
            throw new InvalidOperationException(ReadString(root, "message", "MoMo refused checkout request."));
        }

        var checkoutUrl = ReadString(root, "payUrl");
        var qrPayload = ReadString(root, "qrCodeUrl");

        return new PaymentGatewayCreateResult
        {
            ProviderTransactionId = ReadString(root, "requestId"),
            CheckoutUrl = checkoutUrl,
            // MoMo documents qrCodeUrl as QR payload data, not as an image URL.
            // Production accounts may not receive that optional field, so payUrl
            // remains a scannable fallback for the same signed checkout.
            QrCode = string.IsNullOrWhiteSpace(qrPayload) ? checkoutUrl : qrPayload,
            RawRequest = JsonSerializer.Serialize(payload, JsonOptions),
            RawResponse = rawResponse
        };
    }

    public PaymentGatewayWebhookResult VerifyWebhook(PaymentWebhookDto webhook)
    {
        var values = webhook.Values;
        var momo = _options.MoMo;
        EnsureConfigured(momo.PartnerCode, momo.AccessKey, momo.SecretKey, "configured", "MoMo");

        var rawSignature =
            $"accessKey={momo.AccessKey}&amount={Get(values, "amount")}&extraData={Get(values, "extraData")}&message={Get(values, "message")}&orderId={Get(values, "orderId")}&orderInfo={Get(values, "orderInfo")}&orderType={Get(values, "orderType")}&partnerCode={Get(values, "partnerCode")}&payType={Get(values, "payType")}&requestId={Get(values, "requestId")}&responseTime={Get(values, "responseTime")}&resultCode={Get(values, "resultCode")}&transId={Get(values, "transId")}";
        var expected = PaymentSignatureHelper.HmacSha256(rawSignature, momo.SecretKey);
        var actual = Get(values, "signature");
        var valid = PaymentSignatureHelper.FixedTimeEquals(expected, actual);
        var resultCode = int.TryParse(Get(values, "resultCode"), out var parsed) ? parsed : -1;

        return new PaymentGatewayWebhookResult
        {
            IsSignatureValid = valid,
            OrderCode = Get(values, "orderId"),
            ProviderTransactionId = Get(values, "transId"),
            Status = resultCode == 0 ? PaymentStatus.Paid : PaymentStatus.Failed,
            Message = Get(values, "message"),
            AmountVnd = decimal.TryParse(Get(values, "amount"), out var amount) ? amount : null
        };
    }

    private static string Get(IReadOnlyDictionary<string, string> values, string key)
        => values.TryGetValue(key, out var value) ? value : string.Empty;

    private static string ReadString(JsonElement element, string propertyName, string fallback = "")
        => element.TryGetProperty(propertyName, out var property) ? property.ToString() : fallback;

    private static int ReadInt(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property) && property.TryGetInt32(out var value) ? value : -1;

    private static void EnsureConfigured(string partnerCode, string accessKey, string secretKey, string endpoint, string providerName)
    {
        if (string.IsNullOrWhiteSpace(partnerCode)
            || string.IsNullOrWhiteSpace(accessKey)
            || string.IsNullOrWhiteSpace(secretKey)
            || string.IsNullOrWhiteSpace(endpoint)
            || partnerCode.Contains("YOUR_", StringComparison.OrdinalIgnoreCase)
            || accessKey.Contains("YOUR_", StringComparison.OrdinalIgnoreCase)
            || secretKey.Contains("YOUR_", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{providerName} payment gateway is not configured.");
        }
    }
}
