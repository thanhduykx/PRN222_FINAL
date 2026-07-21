using Microsoft.Extensions.Options;
using NSubstitute;
using PRN222_FINAL.BLL.Options;
using PRN222_FINAL.BLL.Services.Billing.Gateways;
using PRN222_FINAL.DAL.Models.Http;
using PRN222_FINAL.DAL.Repositories.Http;
using Xunit;

namespace PRN222_FINAL.BLL.Tests;

public sealed class PayOsPaymentGatewayTests
{
    [Fact]
    public async Task GetStatusAsync_UsesOrderEndpointAndReturnsPaidTransaction()
    {
        const long orderCode = 260721123456789;
        var http = Substitute.For<IHttpRepository>();
        HttpRequestData? captured = null;
        http.SendAsync(Arg.Any<HttpRequestData>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                captured = call.ArgAt<HttpRequestData>(0);
                return new HttpResponseData(
                    200,
                    "OK",
                    """{"code":"00","desc":"success","data":{"orderCode":260721123456789,"amount":49000,"status":"PAID","paymentLinkId":"link-123"}}""");
            });
        var gateway = new PayOsPaymentGateway(http, Microsoft.Extensions.Options.Options.Create(new PaymentOptions
        {
            PayOS = new PayOsOptions
            {
                ClientId = "client-id",
                ApiKey = "api-key",
                ChecksumKey = "checksum-key",
                Endpoint = "https://api-merchant.payos.vn/v2/payment-requests"
            }
        }));

        var result = await gateway.GetStatusAsync(orderCode);

        Assert.NotNull(captured);
        Assert.Equal("GET", captured!.Method);
        Assert.Equal($"https://api-merchant.payos.vn/v2/payment-requests/{orderCode}", captured.Url);
        Assert.Equal("client-id", captured.Headers!["x-client-id"]);
        Assert.Equal("api-key", captured.Headers["x-api-key"]);
        Assert.Equal(PRN222_FINAL.BLL.Models.PaymentStatus.Paid, result.Status);
        Assert.Equal(orderCode.ToString(), result.OrderCode);
        Assert.Equal(49_000, result.AmountVnd);
        Assert.Equal("link-123", result.ProviderTransactionId);
    }
}
