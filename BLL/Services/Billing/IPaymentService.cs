using PRN222_FINAL.Models;
using PRN222_FINAL.Models.DTOs.Billing;

namespace PRN222_FINAL.BLL.Services.Billing;

public interface IPaymentService
{
    Task<PaymentCheckoutResultDto> CreateCheckoutAsync(CreatePaymentRequestDto request, CancellationToken cancellationToken = default);
    Task<PaymentWebhookResultDto> HandleWebhookAsync(PaymentWebhookDto webhook, CancellationToken cancellationToken = default);
    Task<PaymentReturnDto?> GetReturnStatusAsync(PaymentProvider provider, string orderCode, CancellationToken cancellationToken = default);
}
