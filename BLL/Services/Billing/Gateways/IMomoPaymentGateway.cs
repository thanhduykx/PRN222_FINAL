using PRN222_FINAL.BLL.Contracts.Billing;

namespace PRN222_FINAL.BLL.Services.Billing.Gateways;

public interface IMomoPaymentGateway
{
    Task<PaymentGatewayCreateResult> CreateCheckoutAsync(PaymentGatewayCreateRequest request, CancellationToken cancellationToken = default);
    Task<PaymentGatewayStatusResult> GetStatusAsync(string orderCode, CancellationToken cancellationToken = default);
    PaymentGatewayWebhookResult VerifyWebhook(PaymentWebhookDto webhook);
}
