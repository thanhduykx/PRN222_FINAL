using PRN222_FINAL.Models;
using PRN222_FINAL.Models.DTOs.Billing;

namespace PRN222_FINAL.BLL.Mapping;

public static class BillingDtoMapper
{
    public static PackageDto ToDto(Package package) => new()
    {
        Id = package.Id,
        Code = package.Code,
        Name = package.Name,
        Description = package.Description,
        PriceVnd = package.PriceVnd,
        DurationDays = package.DurationDays,
        MonthlyChatLimit = package.MonthlyChatLimit,
        MonthlyDocumentUploadLimit = package.MonthlyDocumentUploadLimit,
        StorageLimitMb = package.StorageLimitMb,
        IsLifetime = package.IsLifetime,
        IsActive = package.IsActive
    };

    public static SubscriptionDto ToDto(Subscription subscription)
    {
        var package = subscription.Package;
        return new SubscriptionDto
        {
            Id = subscription.Id,
            PackageId = subscription.PackageId,
            PackageName = package?.Name ?? string.Empty,
            UserId = subscription.UserId,
            Status = subscription.Status,
            StartsAt = subscription.StartsAt,
            EndsAt = subscription.EndsAt,
            MonthlyChatLimit = package?.MonthlyChatLimit ?? 0,
            MonthlyDocumentUploadLimit = package?.MonthlyDocumentUploadLimit ?? 0,
            StorageLimitMb = package?.StorageLimitMb ?? 0,
            IsLifetime = package?.IsLifetime == true
        };
    }
}
