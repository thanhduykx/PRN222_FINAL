using PRN222_FINAL.BLL.Models;
using PRN222_FINAL.BLL.Contracts.Billing;

namespace PRN222_FINAL.BLL.Services.Billing;

public interface IPaymentService
{
    Task<PaymentCheckoutResultDto> CreateCheckoutAsync(CreatePaymentRequestDto request, CancellationToken cancellationToken = default);
    Task<PaymentWebhookResultDto> HandleWebhookAsync(PaymentWebhookDto webhook, CancellationToken cancellationToken = default);
    Task<PaymentWebhookResultDto> HandleSignedReturnAsync(PaymentWebhookDto returnData, CancellationToken cancellationToken = default);
    Task<PaymentReturnDto?> GetReturnStatusAsync(PaymentProvider provider, string orderCode, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PendingPaymentDto>> GetPendingPaymentsAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeletePendingPaymentAsync(Guid paymentId, Guid userId, CancellationToken cancellationToken = default);
    Task<int> CleanupExpiredPaymentsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentHistoryItemDto>> GetPaymentsForUserAsync(Guid userId, int limit = 20, CancellationToken cancellationToken = default);
}
