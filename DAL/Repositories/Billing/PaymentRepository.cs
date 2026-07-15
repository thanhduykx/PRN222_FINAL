using Microsoft.EntityFrameworkCore;
using PRN222_FINAL.DAL.Entities.Billing;
using PRN222_FINAL.DAL.Enums;

namespace PRN222_FINAL.DAL.Repositories.Billing;

public sealed class PaymentRepository : SqlBillingRepositoryBase, IPaymentRepository
{
    public PaymentRepository(string connectionString) : base(connectionString)
    {
    }

    public async Task AddAsync(KnowledgeSqlPayment payment, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        context.Payments.Add(payment);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> TryAddFreePaidAsync(KnowledgeSqlPayment payment, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var lockKey = $"free-package:{payment.UserId:N}:{payment.PackageId:N}";
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
            cancellationToken);

        var alreadyClaimed = await context.Payments.AnyAsync(item =>
            item.UserId == payment.UserId
            && item.PackageId == payment.PackageId
            && item.Status == PaymentStatus.Paid,
            cancellationToken)
            || await context.Subscriptions.AnyAsync(item =>
                item.UserId == payment.UserId && item.PackageId == payment.PackageId,
                cancellationToken);
        if (alreadyClaimed)
        {
            await transaction.RollbackAsync(cancellationToken);
            return false;
        }

        context.Payments.Add(payment);
        await context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public async Task UpdateAsync(KnowledgeSqlPayment payment, CancellationToken cancellationToken = default)
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

    public async Task<KnowledgeSqlPayment?> GetByIdAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var entity = await context.Payments
            .AsNoTracking()
            .Include(item => item.Package)
            .FirstOrDefaultAsync(item => item.Id == paymentId, cancellationToken);
        return entity;
    }

    public async Task<KnowledgeSqlPayment?> GetByOrderCodeAsync(PaymentProvider provider, string orderCode, CancellationToken cancellationToken = default)
    {
        var normalizedOrderCode = (orderCode ?? string.Empty).Trim();
        await using var context = CreateContext();
        var entity = await context.Payments
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Provider == provider && item.OrderCode == normalizedOrderCode, cancellationToken);

        if (entity is not null
            || provider != PaymentProvider.PayOS
            || normalizedOrderCode.Length is < 1 or > 15
            || !normalizedOrderCode.All(char.IsDigit))
        {
            return entity;
        }

        // Compatibility for payments created before PayOS and internal order codes
        // were aligned. The old integration sent the last 15 digits to PayOS.
        var legacyMatches = await context.Payments
            .AsNoTracking()
            .Where(item => item.Provider == provider && item.OrderCode.EndsWith(normalizedOrderCode))
            .OrderByDescending(item => item.CreatedAt)
            .Take(2)
            .ToListAsync(cancellationToken);

        return legacyMatches.Count == 1 ? legacyMatches[0] : null;
    }

    public async Task<IReadOnlyList<KnowledgeSqlPayment>> GetByUserAsync(Guid userId, int limit, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Payments
            .AsNoTracking()
            .Include(payment => payment.Package)
            .Where(payment => payment.UserId == userId)
            .OrderByDescending(payment => payment.CreatedAt)
            .Take(Math.Clamp(limit, 1, 100))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<KnowledgeSqlPayment>> GetPendingByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Payments
            .AsNoTracking()
            .Include(payment => payment.Package)
            .Where(payment => payment.UserId == userId && payment.Status == PaymentStatus.Pending)
            .OrderBy(payment => payment.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> DeletePendingAsync(
        Guid paymentId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        var deleted = await context.Payments
            .Where(payment => payment.Id == paymentId
                && payment.UserId == userId
                && payment.Status == PaymentStatus.Pending)
            .ExecuteDeleteAsync(cancellationToken);
        return deleted == 1;
    }

    public async Task<int> DeleteExpiredPendingAsync(
        DateTimeOffset createdBeforeOrAt,
        CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Payments
            .Where(payment => payment.Status == PaymentStatus.Pending
                && payment.CreatedAt <= createdBeforeOrAt)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<bool> HasSuccessfulPaymentAsync(Guid userId, Guid packageId, CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Payments.AsNoTracking().AnyAsync(payment =>
            payment.UserId == userId
            && payment.PackageId == packageId
            && payment.Status == PaymentStatus.Paid,
            cancellationToken);
    }

    public async Task<KnowledgeSqlPayment?> GetLatestSuccessfulByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        await using var context = CreateContext();
        return await context.Payments
            .AsNoTracking()
            .Where(payment => payment.UserId == userId && payment.Status == PaymentStatus.Paid)
            .OrderByDescending(payment => payment.PaidAt ?? payment.CreatedAt)
            .ThenByDescending(payment => payment.CreatedAt)
            .ThenByDescending(payment => payment.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
