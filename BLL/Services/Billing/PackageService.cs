using PRN222_FINAL.BLL.Mapping;
using PRN222_FINAL.DAL.Repositories.Billing;
using PRN222_FINAL.BLL.Models;
using PRN222_FINAL.BLL.Contracts.Billing;

namespace PRN222_FINAL.BLL.Services.Billing;

public sealed class PackageService : IPackageService
{
    private readonly IPackageRepository _packages;

    public PackageService(IPackageRepository packages)
    {
        _packages = packages;
    }

    public async Task<IReadOnlyList<PackageDto>> GetActivePackagesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureDefaultPackagesAsync(cancellationToken);
        var packages = await _packages.GetActiveAsync(cancellationToken);
        return packages.Select(BillingDtoMapper.ToModel).Select(BillingDtoMapper.ToDto).ToList();
    }

    public async Task<PackageDto?> GetPackageAsync(Guid packageId, CancellationToken cancellationToken = default)
    {
        if (packageId == Guid.Empty)
        {
            return null;
        }

        await EnsureDefaultPackagesAsync(cancellationToken);
        var package = await _packages.GetByIdAsync(packageId, cancellationToken);
        return package is null ? null : BillingDtoMapper.ToDto(BillingDtoMapper.ToModel(package));
    }

    public async Task<PackagePriceChangeDto?> UpdatePriceAsync(
        Guid packageId,
        decimal newPriceVnd,
        string changedBy,
        CancellationToken cancellationToken = default)
    {
        if (packageId == Guid.Empty)
        {
            throw new ArgumentException("Gói dịch vụ không hợp lệ.", nameof(packageId));
        }
        if (newPriceVnd < 0 || newPriceVnd > 1_000_000_000m || decimal.Truncate(newPriceVnd) != newPriceVnd)
        {
            throw new ArgumentOutOfRangeException(nameof(newPriceVnd), "Giá phải là số nguyên từ 0 đến 1.000.000.000 đồng.");
        }

        var actor = string.IsNullOrWhiteSpace(changedBy) ? "Admin" : changedBy.Trim();
        var change = await _packages.UpdatePriceAsync(packageId, newPriceVnd, actor, DateTimeOffset.UtcNow, cancellationToken);
        return change is null ? null : ToPriceChangeDto(change);
    }

    public async Task<PackagePriceChangeDto?> GetLatestPriceChangeAsync(CancellationToken cancellationToken = default)
    {
        var change = await _packages.GetLatestPriceChangeAsync(cancellationToken);
        return change is null ? null : ToPriceChangeDto(change);
    }

    private async Task EnsureDefaultPackagesAsync(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var existingPackages = (await _packages.GetAllAsync(cancellationToken))
            .Select(BillingDtoMapper.ToModel).ToList();
        var defaults = new[]
        {
            new Package
            {
                Id = Guid.NewGuid(),
                Code = "FREE",
                Name = "Free",
                Description = "Gói dùng thử cho sinh viên hỏi chatbot ở mức cơ bản.",
                PriceVnd = 0,
                DurationDays = 30,
                MonthlyChatLimit = 10,
                MonthlyDocumentUploadLimit = 0,
                StorageLimitMb = 0,
                IsLifetime = false,
                IsActive = true,
                SortOrder = 10,
                CreatedAt = now
            },
            new Package
            {
                Id = Guid.NewGuid(),
                Code = "STUDENT",
                Name = "Student",
                Description = "Gói học tập hằng tháng cho sinh viên hỏi chatbot theo môn.",
                PriceVnd = 49000,
                DurationDays = 30,
                MonthlyChatLimit = 60,
                MonthlyDocumentUploadLimit = 0,
                StorageLimitMb = 0,
                IsLifetime = false,
                IsActive = true,
                SortOrder = 20,
                CreatedAt = now
            },
            new Package
            {
                Id = Guid.NewGuid(),
                Code = "PRO",
                Name = "Pro",
                Description = "Gói nâng cao cho sinh viên cần hỏi chatbot thường xuyên.",
                PriceVnd = 129000,
                DurationDays = 30,
                MonthlyChatLimit = 180,
                MonthlyDocumentUploadLimit = 0,
                StorageLimitMb = 0,
                IsLifetime = false,
                IsActive = true,
                SortOrder = 30,
                CreatedAt = now
            },
            new Package
            {
                Id = Guid.NewGuid(),
                Code = "LIFETIME",
                Name = "Vĩnh viễn (đã ngừng bán)",
                Description = "Gói cũ được giữ quyền lợi cho người đã đăng ký trước đây.",
                PriceVnd = 0,
                DurationDays = 0,
                MonthlyChatLimit = 60,
                MonthlyDocumentUploadLimit = 0,
                StorageLimitMb = 0,
                IsLifetime = true,
                IsActive = false,
                SortOrder = 40,
                CreatedAt = now
            },
            new Package
            {
                Id = Guid.NewGuid(),
                Code = "ANNUAL",
                Name = "12 tháng",
                Description = "Trọn một năm học với chi phí tiết kiệm hơn so với gia hạn từng tháng.",
                PriceVnd = 499000,
                DurationDays = 365,
                MonthlyChatLimit = 60,
                MonthlyDocumentUploadLimit = 0,
                StorageLimitMb = 0,
                IsLifetime = false,
                IsActive = true,
                SortOrder = 40,
                CreatedAt = now
            }
        };

        foreach (var package in defaults)
        {
            var existing = existingPackages.FirstOrDefault(item => item.Code.Equals(package.Code, StringComparison.OrdinalIgnoreCase));
            if (existing is null)
            {
                await _packages.UpsertAsync(BillingDtoMapper.ToEntity(package), cancellationToken);
            }
        }
    }

    private static PackagePriceChangeDto ToPriceChangeDto(DAL.Entities.Billing.KnowledgeSqlPackagePriceChange change) => new()
    {
        Id = change.Id,
        PackageId = change.PackageId,
        PackageName = change.PackageName,
        OldPriceVnd = change.OldPriceVnd,
        NewPriceVnd = change.NewPriceVnd,
        ChangedAt = change.ChangedAt
    };
}
