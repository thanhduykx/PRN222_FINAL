using PRN222_FINAL.Models.DTOs.Billing;

namespace PRN222_FINAL.BLL.Services.Billing.Gateways;

public interface IPayOsPaymentGateway
{
    Task<PaymentGatewayCreateResult> CreateCheckoutAsync(PaymentGatewayCreateRequest request, CancellationToken cancellationToken = default);
    PaymentGatewayWebhookResult VerifyWebhook(PaymentWebhookDto webhook);
}
