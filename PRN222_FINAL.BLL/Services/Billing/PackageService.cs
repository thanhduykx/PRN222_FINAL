using PRN222_FINAL.BLL.Mapping;
using PRN222_FINAL.DAL.Repositories.Billing;
using PRN222_FINAL.Models;
using PRN222_FINAL.Models.DTOs.Billing;

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
        return packages.Select(BillingDtoMapper.ToDto).ToList();
    }

    private async Task EnsureDefaultPackagesAsync(CancellationToken cancellationToken)
    {
        var existing = await _packages.GetAllAsync(cancellationToken);
        if (existing.Count > 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var defaults = new[]
        {
            new Package
            {
                Id = Guid.NewGuid(),
                Code = "FREE",
                Name = "Free",
                Description = "Goi dung thu cho chat va upload tai lieu gioi han.",
                PriceVnd = 0,
                DurationDays = 30,
                MonthlyChatLimit = 50,
                MonthlyDocumentUploadLimit = 3,
                StorageLimitMb = 100,
                IsActive = true,
                SortOrder = 10,
                CreatedAt = now
            },
            new Package
            {
                Id = Guid.NewGuid(),
                Code = "STUDENT",
                Name = "Student",
                Description = "Goi hoc tap cho sinh vien voi luot chat va upload cao hon.",
                PriceVnd = 49000,
                DurationDays = 30,
                MonthlyChatLimit = 1000,
                MonthlyDocumentUploadLimit = 50,
                StorageLimitMb = 1024,
                IsActive = true,
                SortOrder = 20,
                CreatedAt = now
            },
            new Package
            {
                Id = Guid.NewGuid(),
                Code = "PRO",
                Name = "Pro",
                Description = "Goi nang cao cho su dung RAG chatbot, upload va luu tru lon.",
                PriceVnd = 149000,
                DurationDays = 30,
                MonthlyChatLimit = 5000,
                MonthlyDocumentUploadLimit = 300,
                StorageLimitMb = 10240,
                IsActive = true,
                SortOrder = 30,
                CreatedAt = now
            }
        };

        foreach (var package in defaults)
        {
            await _packages.UpsertAsync(package, cancellationToken);
        }
    }
}
