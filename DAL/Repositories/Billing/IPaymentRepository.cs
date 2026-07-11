using PRN222_FINAL.Models;

namespace PRN222_FINAL.DAL.Repositories.Billing;

public interface IPaymentRepository
{
    Task AddAsync(Payment payment, CancellationToken cancellationToken = default);
    Task UpdateAsync(Payment payment, CancellationToken cancellationToken = default);
    Task<Payment?> GetByIdAsync(Guid paymentId, CancellationToken cancellationToken = default);
    Task<Payment?> GetByOrderCodeAsync(PaymentProvider provider, string orderCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Payment>> GetByUserAsync(Guid userId, int limit, CancellationToken cancellationToken = default);
    Task<bool> HasSuccessfulPaymentAsync(Guid userId, Guid packageId, CancellationToken cancellationToken = default);
}
