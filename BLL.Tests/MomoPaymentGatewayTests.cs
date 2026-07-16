using Microsoft.Extensions.Options;
using NSubstitute;
using PRN222_FINAL.BLL.Models;
using PRN222_FINAL.BLL.Options;
using PRN222_FINAL.BLL.Services.Billing.Gateways;
using PRN222_FINAL.DAL.Models.Http;
using PRN222_FINAL.DAL.Repositories.Http;
using Xunit;

namespace PRN222_FINAL.BLL.Tests;

public sealed class MomoPaymentGatewayTests
{
    private const string PayUrl = "https://payment.momo.vn/v2/gateway/pay?t=signed-checkout";
    private const string QrCodeUrl = "https://payment.momo.vn/v2/gateway/app?isScanQr=true&t=signed-checkout";

    [Theory]
    [InlineData(true, QrCodeUrl)]
    [InlineData(false, PayUrl)]
    public async Task CreateCheckoutAsync_ReturnsScannablePayload_WhenOptionalQrCodeUrlIsUnavailable(
        bool includeQrCodeUrl,
        string expectedQrPayload)
    {
        var http = Substitute.For<IHttpRepository>();
        var qrProperty = includeQrCodeUrl ? $",\"qrCodeUrl\":\"{QrCodeUrl}\"" : string.Empty;
        var responseBody = $$"""
            {"resultCode":0,"message":"Successful.","requestId":"request-123","payUrl":"{{PayUrl}}"{{qrProperty}}}
            """;
        http.SendAsync(Arg.Any<HttpRequestData>(), Arg.Any<CancellationToken>())
            .Returns(new HttpResponseData(200, "OK", responseBody));

        var gateway = new MomoPaymentGateway(http, Microsoft.Extensions.Options.Options.Create(new PaymentOptions
        {
            MoMo = new MomoOptions
            {
                PartnerCode = "partner",
                AccessKey = "access",
                SecretKey = "secret",
                Endpoint = "https://payment.momo.vn/v2/gateway/api/create",
                RequestType = "captureWallet"
            }
        }));

        var result = await gateway.CreateCheckoutAsync(new PaymentGatewayCreateRequest
        {
            Provider = PaymentProvider.MoMo,
            OrderCode = "ORDER-123",
            AmountVnd = 79_000,
            Description = "Thanh toan goi Student",
            ReturnUrl = "https://course.example/Payments/Return",
            IpnUrl = "https://course.example/Payments/MomoWebhook"
        });

        Assert.Equal(PayUrl, result.CheckoutUrl);
        Assert.Equal(expectedQrPayload, result.QrCode);
    }
}
