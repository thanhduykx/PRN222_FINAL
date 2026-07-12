using PRN222_FINAL.BLL.Contracts.Billing;

namespace PRN222_FINAL.BLL.Services.Billing.Gateways;

public interface IPayOsPaymentGateway
{
    Task<PaymentGatewayCreateResult> CreateCheckoutAsync(PaymentGatewayCreateRequest request, CancellationToken cancellationToken = default);
    Task<PaymentGatewayStatusResult> GetStatusAsync(long orderCode, CancellationToken cancellationToken = default);
    PaymentGatewayWebhookResult VerifyWebhook(PaymentWebhookDto webhook);
}
