using NSubstitute;
using PRN222_FINAL.BLL.Services.Billing;
using PRN222_FINAL.DAL.Entities.Billing;
using PRN222_FINAL.DAL.Repositories.Billing;
using Xunit;

namespace PRN222_FINAL.BLL.Tests;

public sealed class PackageServiceTests
{
    [Fact]
    public async Task UpdatePriceAsync_PersistsChangeAndMapsNotification()
    {
        var repository = Substitute.For<IPackageRepository>();
        var packageId = Guid.NewGuid();
        repository.UpdatePriceAsync(packageId, 49_000m, 79_000m, "admin@example.com", "Điều chỉnh học kỳ mới", Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(call => new KnowledgeSqlPackagePriceChange
            {
                Id = Guid.NewGuid(),
                PackageId = packageId,
                PackageName = "Student",
                OldPriceVnd = 49_000m,
                NewPriceVnd = 79_000m,
                ChangedBy = "admin@example.com",
                Reason = "Điều chỉnh học kỳ mới",
                ChangedAt = call.ArgAt<DateTimeOffset>(5)
            });
        var service = new PackageService(repository);

        var result = await service.UpdatePriceAsync(packageId, 49_000m, 79_000m, "admin@example.com", "  Điều chỉnh học kỳ mới  ", false);

        Assert.NotNull(result);
        Assert.Equal("Student", result.PackageName);
        Assert.Equal(49_000m, result.OldPriceVnd);
        Assert.Equal(79_000m, result.NewPriceVnd);
        Assert.Equal("Điều chỉnh học kỳ mới", result.Reason);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(1000000001)]
    [InlineData(1.5)]
    public async Task UpdatePriceAsync_RejectsInvalidPrice(decimal price)
    {
        var repository = Substitute.For<IPackageRepository>();
        var service = new PackageService(repository);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            service.UpdatePriceAsync(Guid.NewGuid(), 49_000m, price, "admin@example.com", "Điều chỉnh giá", false));
        await repository.DidNotReceiveWithAnyArgs()
            .UpdatePriceAsync(default, default, default, default!, default!, default, default);
    }

    [Fact]
    public async Task UpdatePriceAsync_RequiresExplicitConfirmationWhenPaidPackageBecomesFree()
    {
        var repository = Substitute.For<IPackageRepository>();
        var service = new PackageService(repository);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.UpdatePriceAsync(Guid.NewGuid(), 49_000m, 0, "admin@example.com", "Chuyển sang miễn phí", false));

        await repository.DidNotReceiveWithAnyArgs()
            .UpdatePriceAsync(default, default, default, default!, default!, default, default);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    public async Task UpdatePriceAsync_RejectsMissingOrShortReason(string reason)
    {
        var repository = Substitute.For<IPackageRepository>();
        var service = new PackageService(repository);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.UpdatePriceAsync(Guid.NewGuid(), 49_000m, 59_000m, "admin@example.com", reason, false));
    }

    [Fact]
    public async Task GetActivePackagesAsync_DoesNotOverwriteExistingAdminPrice()
    {
        var repository = Substitute.For<IPackageRepository>();
        repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new[]
        {
            new KnowledgeSqlPackage { Id = Guid.NewGuid(), Code = "FREE", PriceVnd = 0 },
            new KnowledgeSqlPackage { Id = Guid.NewGuid(), Code = "STUDENT", PriceVnd = 79_000 },
            new KnowledgeSqlPackage { Id = Guid.NewGuid(), Code = "PRO", PriceVnd = 159_000 },
            new KnowledgeSqlPackage { Id = Guid.NewGuid(), Code = "ANNUAL", PriceVnd = 599_000 }
        });
        repository.GetActiveAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<KnowledgeSqlPackage>());
        var service = new PackageService(repository);

        await service.GetActivePackagesAsync();

        await repository.DidNotReceiveWithAnyArgs().UpsertAsync(default!, default);
        await repository.Received(1).RemoveRetiredAsync("LIFETIME", Arg.Any<CancellationToken>());
    }
}
