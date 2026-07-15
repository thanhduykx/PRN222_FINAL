using PRN222_FINAL.DAL.Entities.Billing;
using PRN222_FINAL.DAL.Enums;

namespace PRN222_FINAL.DAL.Repositories.Billing;

public interface IPaymentRepository
{
    Task AddAsync(KnowledgeSqlPayment payment, CancellationToken cancellationToken = default);
    Task<bool> TryAddFreePaidAsync(KnowledgeSqlPayment payment, CancellationToken cancellationToken = default);
    Task UpdateAsync(KnowledgeSqlPayment payment, CancellationToken cancellationToken = default);
    Task<KnowledgeSqlPayment?> GetByIdAsync(Guid paymentId, CancellationToken cancellationToken = default);
    Task<KnowledgeSqlPayment?> GetByOrderCodeAsync(PaymentProvider provider, string orderCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KnowledgeSqlPayment>> GetByUserAsync(Guid userId, int limit, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KnowledgeSqlPayment>> GetPendingByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> DeletePendingAsync(Guid paymentId, Guid userId, CancellationToken cancellationToken = default);
    Task<int> DeleteExpiredPendingAsync(DateTimeOffset createdBeforeOrAt, CancellationToken cancellationToken = default);
    Task<bool> HasSuccessfulPaymentAsync(Guid userId, Guid packageId, CancellationToken cancellationToken = default);
    Task<KnowledgeSqlPayment?> GetLatestSuccessfulByUserAsync(Guid userId, CancellationToken cancellationToken = default);
}
