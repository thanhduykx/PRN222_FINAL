using Microsoft.EntityFrameworkCore;
using PRN222_FINAL.DAL.Mapping;
using PRN222_FINAL.Models;

namespace PRN222_FINAL.DAL.Repositories.Billing;

public sealed class PaymentRepository : SqlBillingRepositoryBase, IPaymentRepository
{
    public PaymentRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task AddAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        context.Payments.Add(BillingSqlMapper.ToEntity(payment));
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var entity = await context.Payments.FirstOrDefaultAsync(item => item.Id == payment.Id, cancellationToken)
            ?? throw new InvalidOperationException("Payment not found.");

        entity.PackageId = payment.PackageId;
        entity.UserId = payment.UserId;
        entity.UserName = payment.UserName;
        entity.UserEmail = payment.UserEmail;
        entity.Provider = payment.Provider;
        entity.Status = payment.Status;
        entity.AmountVnd = payment.AmountVnd;
        entity.Currency = payment.Currency;
        entity.OrderCode = payment.OrderCode;
        entity.ProviderTransactionId = payment.ProviderTransactionId;
        entity.CheckoutUrl = payment.CheckoutUrl;
        entity.QrCode = payment.QrCode;
        entity.RawRequest = payment.RawRequest;
        entity.RawResponse = payment.RawResponse;
        entity.RawWebhook = payment.RawWebhook;
        entity.PaidAt = payment.PaidAt;
        entity.FailedAt = payment.FailedAt;
        entity.FailureReason = payment.FailureReason;

        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<Payment?> GetByIdAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var entity = await context.Payments
            .AsNoTracking()
            .Include(item => item.Package)
            .FirstOrDefaultAsync(item => item.Id == paymentId, cancellationToken);
        return entity is null ? null : BillingSqlMapper.ToModel(entity);
    }

    public async Task<Payment?> GetByOrderCodeAsync(PaymentProvider provider, string orderCode, CancellationToken cancellationToken = default)
    {
        var normalizedOrderCode = (orderCode ?? string.Empty).Trim();
        await using var context = CreateContext();
        var entity = await context.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Provider == provider && item.OrderCode == normalizedOrderCode, cancellationToken);

        return entity is null ? null : BillingSqlMapper.ToModel(entity);
    }

    public async Task<IReadOnlyList<Payment>> GetByUserAsync(Guid userId, int limit, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Payments
            .AsNoTracking()
            .Include(payment => payment.Package)
            .Where(payment => payment.UserId == userId)
            .OrderByDescending(payment => payment.CreatedAt)
            .Take(Math.Clamp(limit, 1, 100))
            .Select(payment => BillingSqlMapper.ToModel(payment))
            .ToListAsync(cancellationToken);
    }
}
