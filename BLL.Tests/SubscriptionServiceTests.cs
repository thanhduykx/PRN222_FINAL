using NSubstitute;
using PRN222_FINAL.BLL.Services.Billing;
using PRN222_FINAL.DAL.Entities.Billing;
using PRN222_FINAL.DAL.Enums;
using PRN222_FINAL.DAL.Repositories.Billing;
using Xunit;

namespace PRN222_FINAL.BLL.Tests;

public sealed class SubscriptionServiceTests
{
    [Fact]
    public async Task GetCurrentSubscriptionAsync_RenewsExpiredFreeSubscription()
    {
        var userId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        var repository = Substitute.For<ISubscriptionRepository>();
        var renewal = new KnowledgeSqlSubscription
        {
            Id = Guid.NewGuid(),
            PackageId = packageId,
            UserId = userId,
            UserName = "Student",
            UserEmail = "student@example.com",
            Status = SubscriptionStatus.Active,
            StartsAt = now,
            EndsAt = now.AddDays(30),
            CreatedAt = now,
            Package = new KnowledgeSqlPackage
            {
                Id = packageId,
                Code = "FREE",
                Name = "Free",
                DurationDays = 30,
                MonthlyChatLimit = 10
            }
        };
        repository.GetCurrentActiveAsync(userId, Arg.Any<CancellationToken>()).Returns((KnowledgeSqlSubscription?)null);
        repository.RenewExpiredFreeAsync(userId, now, Arg.Any<CancellationToken>()).Returns(renewal);
        var service = new SubscriptionService(repository, new FixedTimeProvider(now));

        var result = await service.GetCurrentSubscriptionAsync(userId);

        Assert.NotNull(result);
        Assert.Equal("FREE", result.PackageCode);
        Assert.Equal(now, result.StartsAt);
        Assert.Equal(now.AddDays(30), result.EndsAt);
        await repository.Received(1).RenewExpiredFreeAsync(userId, now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetCurrentSubscriptionAsync_DoesNotRenewWhenAnActiveSubscriptionExists()
    {
        var userId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        var active = new KnowledgeSqlSubscription
        {
            Id = Guid.NewGuid(),
            PackageId = Guid.NewGuid(),
            UserId = userId,
            UserName = "Student",
            UserEmail = "student@example.com",
            Status = SubscriptionStatus.Active,
            StartsAt = now.AddDays(-5),
            EndsAt = now.AddDays(25),
            CreatedAt = now.AddDays(-5),
            Package = new KnowledgeSqlPackage { Code = "PRO", Name = "Pro" }
        };
        var repository = Substitute.For<ISubscriptionRepository>();
        repository.GetCurrentActiveAsync(userId, Arg.Any<CancellationToken>()).Returns(active);
        var service = new SubscriptionService(repository, new FixedTimeProvider(now));

        var result = await service.GetCurrentSubscriptionAsync(userId);

        Assert.NotNull(result);
        Assert.Equal("PRO", result.PackageCode);
        await repository.DidNotReceiveWithAnyArgs().RenewExpiredFreeAsync(default, default);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
