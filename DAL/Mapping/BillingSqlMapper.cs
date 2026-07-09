using PRN222_FINAL.DAL.Entities.Billing;
using PRN222_FINAL.Models;

namespace PRN222_FINAL.DAL.Mapping;

public static class BillingSqlMapper
{
    public static Package ToModel(KnowledgeSqlPackage entity) => new()
    {
        Id = entity.Id,
        Code = entity.Code,
        Name = entity.Name,
        Description = entity.Description,
        PriceVnd = entity.PriceVnd,
        DurationDays = entity.DurationDays,
        MonthlyChatLimit = entity.MonthlyChatLimit,
        MonthlyDocumentUploadLimit = entity.MonthlyDocumentUploadLimit,
        StorageLimitMb = entity.StorageLimitMb,
        IsLifetime = entity.IsLifetime,
        IsActive = entity.IsActive,
        SortOrder = entity.SortOrder,
        CreatedAt = entity.CreatedAt
    };

    public static KnowledgeSqlPackage ToEntity(Package model) => new()
    {
        Id = model.Id,
        Code = model.Code,
        Name = model.Name,
        Description = model.Description,
        PriceVnd = model.PriceVnd,
        DurationDays = model.DurationDays,
        MonthlyChatLimit = model.MonthlyChatLimit,
        MonthlyDocumentUploadLimit = model.MonthlyDocumentUploadLimit,
        StorageLimitMb = model.StorageLimitMb,
        IsLifetime = model.IsLifetime,
        IsActive = model.IsActive,
        SortOrder = model.SortOrder,
        CreatedAt = model.CreatedAt
    };

    public static Payment ToModel(KnowledgeSqlPayment entity) => new()
    {
        Id = entity.Id,
        PackageId = entity.PackageId,
        UserId = entity.UserId,
        UserName = entity.UserName,
        UserEmail = entity.UserEmail,
        Provider = entity.Provider,
        Status = entity.Status,
        AmountVnd = entity.AmountVnd,
        Currency = entity.Currency,
        OrderCode = entity.OrderCode,
        ProviderTransactionId = entity.ProviderTransactionId,
        CheckoutUrl = entity.CheckoutUrl,
        QrCode = entity.QrCode,
        RawRequest = entity.RawRequest,
        RawResponse = entity.RawResponse,
        RawWebhook = entity.RawWebhook,
        CreatedAt = entity.CreatedAt,
        PaidAt = entity.PaidAt,
        FailedAt = entity.FailedAt,
        FailureReason = entity.FailureReason,
        Package = entity.Package is null ? null : ToModel(entity.Package)
    };

    public static KnowledgeSqlPayment ToEntity(Payment model) => new()
    {
        Id = model.Id,
        PackageId = model.PackageId,
        UserId = model.UserId,
        UserName = model.UserName,
        UserEmail = model.UserEmail,
        Provider = model.Provider,
        Status = model.Status,
        AmountVnd = model.AmountVnd,
        Currency = model.Currency,
        OrderCode = model.OrderCode,
        ProviderTransactionId = model.ProviderTransactionId,
        CheckoutUrl = model.CheckoutUrl,
        QrCode = model.QrCode,
        RawRequest = model.RawRequest,
        RawResponse = model.RawResponse,
        RawWebhook = model.RawWebhook,
        CreatedAt = model.CreatedAt,
        PaidAt = model.PaidAt,
        FailedAt = model.FailedAt,
        FailureReason = model.FailureReason
    };

    public static Subscription ToModel(KnowledgeSqlSubscription entity) => new()
    {
        Id = entity.Id,
        PackageId = entity.PackageId,
        UserId = entity.UserId,
        UserName = entity.UserName,
        UserEmail = entity.UserEmail,
        Status = entity.Status,
        StartsAt = entity.StartsAt,
        EndsAt = entity.EndsAt,
        PaymentId = entity.PaymentId,
        CreatedAt = entity.CreatedAt,
        Package = entity.Package is null ? null : ToModel(entity.Package)
    };

    public static KnowledgeSqlSubscription ToEntity(Subscription model) => new()
    {
        Id = model.Id,
        PackageId = model.PackageId,
        UserId = model.UserId,
        UserName = model.UserName,
        UserEmail = model.UserEmail,
        Status = model.Status,
        StartsAt = model.StartsAt,
        EndsAt = model.EndsAt,
        PaymentId = model.PaymentId,
        CreatedAt = model.CreatedAt
    };
}
