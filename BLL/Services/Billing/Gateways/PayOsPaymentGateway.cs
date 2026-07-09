using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PRN222_FINAL.BLL.Options;
using PRN222_FINAL.Models;
using PRN222_FINAL.Models.DTOs.Billing;

namespace PRN222_FINAL.BLL.Services.Billing.Gateways;

public sealed class PayOsPaymentGateway : IPayOsPaymentGateway
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly PaymentOptions _options;

    public PayOsPaymentGateway(HttpClient httpClient, IOptions<PaymentOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<PaymentGatewayCreateResult> CreateCheckoutAsync(PaymentGatewayCreateRequest request, CancellationToken cancellationToken = default)
    {
        var payos = _options.PayOS;
        EnsureConfigured(payos.ClientId, payos.ApiKey, payos.ChecksumKey, payos.Endpoint, "PayOS");

        var amount = (long)request.AmountVnd;
        var description = TrimDescription(request.Description);
        var rawSignature = $"amount={amount}&cancelUrl={request.CancelUrl}&description={description}&orderCode={request.PayOsOrderCode}&returnUrl={request.ReturnUrl}";
        var signature = PaymentSignatureHelper.HmacSha256(rawSignature, payos.ChecksumKey);
        var payload = new Dictionary<string, object?>
        {
            ["orderCode"] = request.PayOsOrderCode,
            ["amount"] = amount,
            ["description"] = description,
            ["returnUrl"] = request.ReturnUrl,
            ["cancelUrl"] = request.CancelUrl,
            ["signature"] = signature
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, payos.Endpoint)
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        httpRequest.Headers.Add("x-client-id", payos.ClientId);
        httpRequest.Headers.Add("x-api-key", payos.ApiKey);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var rawResponse = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"PayOS checkout failed: {(int)response.StatusCode} {rawResponse}");
        }

        using var json = JsonDocument.Parse(rawResponse);
        var root = json.RootElement;
        var code = ReadString(root, "code");
        if (!string.Equals(code, "00", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(ReadString(root, "desc", "PayOS refused checkout request."));
        }

        var data = root.TryGetProperty("data", out var dataElement) ? dataElement : root;
        return new PaymentGatewayCreateResult
        {
            ProviderTransactionId = ReadString(data, "paymentLinkId"),
            CheckoutUrl = ReadString(data, "checkoutUrl"),
            QrCode = ReadString(data, "qrCode"),
            RawRequest = JsonSerializer.Serialize(payload, JsonOptions),
            RawResponse = rawResponse
        };
    }

    public PaymentGatewayWebhookResult VerifyWebhook(PaymentWebhookDto webhook)
    {
        var payos = _options.PayOS;
        EnsureConfigured(payos.ClientId, payos.ApiKey, payos.ChecksumKey, "configured", "PayOS");

        using var json = JsonDocument.Parse(string.IsNullOrWhiteSpace(webhook.RawBody) ? "{}" : webhook.RawBody);
        var root = json.RootElement;
        var data = root.TryGetProperty("data", out var dataElement) ? dataElement : root;
        var actualSignature = ReadString(root, "signature");
        if (string.IsNullOrWhiteSpace(actualSignature) && data.TryGetProperty("signature", out var nestedSignature))
        {
            actualSignature = nestedSignature.ToString();
        }

        var signatureData = BuildWebhookSignatureData(data);
        var expectedSignature = PaymentSignatureHelper.HmacSha256(signatureData, payos.ChecksumKey);
        var valid = PaymentSignatureHelper.FixedTimeEquals(expectedSignature, actualSignature);
        var code = ReadString(data, "code", ReadString(root, "code"));

        return new PaymentGatewayWebhookResult
        {
            IsSignatureValid = valid,
            OrderCode = ReadString(data, "orderCode"),
            ProviderTransactionId = ReadString(data, "paymentLinkId", ReadString(data, "reference")),
            Status = string.Equals(code, "00", StringComparison.OrdinalIgnoreCase) ? PaymentStatus.Paid : PaymentStatus.Failed,
            Message = ReadString(data, "desc", ReadString(root, "desc")),
            AmountVnd = decimal.TryParse(ReadString(data, "amount"), out var amount) ? amount : null
        };
    }

    private static string BuildWebhookSignatureData(JsonElement data)
    {
        var pairs = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in data.EnumerateObject())
        {
            if (string.Equals(property.Name, "signature", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            pairs[property.Name] = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString() ?? string.Empty,
                JsonValueKind.Null => string.Empty,
                _ => property.Value.ToString()
            };
        }

        return string.Join('&', pairs.Select(pair => $"{pair.Key}={pair.Value}"));
    }

    private static string TrimDescription(string description)
    {
        var normalized = string.Join(' ', (description ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = "PRN222 payment";
        }

        return normalized.Length <= 25 ? normalized : normalized[..25];
    }

    private static string ReadString(JsonElement element, string propertyName, string fallback = "")
        => element.TryGetProperty(propertyName, out var property) ? property.ToString() : fallback;

    private static void EnsureConfigured(string clientId, string apiKey, string checksumKey, string endpoint, string providerName)
    {
        if (string.IsNullOrWhiteSpace(clientId)
            || string.IsNullOrWhiteSpace(apiKey)
            || string.IsNullOrWhiteSpace(checksumKey)
            || string.IsNullOrWhiteSpace(endpoint)
            || clientId.Contains("YOUR_", StringComparison.OrdinalIgnoreCase)
            || apiKey.Contains("YOUR_", StringComparison.OrdinalIgnoreCase)
            || checksumKey.Contains("YOUR_", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{providerName} payment gateway is not configured.");
        }
    }
}
