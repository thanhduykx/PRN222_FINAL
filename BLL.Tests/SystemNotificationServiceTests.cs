using NSubstitute;
using PRN222_FINAL.BLL.Services.Notifications;
using PRN222_FINAL.DAL.Entities;
using PRN222_FINAL.DAL.Repositories.Notifications;
using Xunit;

namespace PRN222_FINAL.BLL.Tests;

public sealed class SystemNotificationServiceTests
{
    [Fact]
    public void PublicNotificationTypeName_MatchesPersistenceContract()
    {
        Assert.Equal(
            PRN222_FINAL.DAL.Entities.SystemNotificationTypes.PackagePriceChanged,
            PRN222_FINAL.BLL.Contracts.Notifications.SystemNotificationTypeNames.PackagePriceChanged);
    }

    [Fact]
    public async Task GetActiveAsync_ReadsNotificationsFromLastThreeDaysAndMapsThem()
    {
        var now = new DateTimeOffset(2026, 7, 14, 9, 30, 0, TimeSpan.Zero);
        var cancellationToken = new CancellationTokenSource().Token;
        var repository = Substitute.For<ISystemNotificationRepository>();
        var notification = new KnowledgeSqlSystemNotification
        {
            Id = Guid.NewGuid(),
            Type = SystemNotificationTypes.SubjectRenamed,
            EntityId = Guid.NewGuid(),
            Title = "Tên môn học đã thay đổi",
            Message = "Môn học cũ đã được đổi tên thành môn học mới.",
            OccurredAt = now.AddHours(-2)
        };
        repository
            .GetSinceAsync(now.AddDays(-3), 50, cancellationToken)
            .Returns(new[] { notification });
        var service = new SystemNotificationService(repository, new FixedTimeProvider(now));

        var result = await service.GetActiveAsync(cancellationToken);

        var mapped = Assert.Single(result);
        Assert.Equal(notification.Id, mapped.Id);
        Assert.Equal(notification.Type, mapped.Type);
        Assert.Equal(notification.EntityId, mapped.EntityId);
        Assert.Equal(notification.Title, mapped.Title);
        Assert.Equal(notification.Message, mapped.Message);
        Assert.Equal(notification.OccurredAt, mapped.OccurredAt);
        await repository.Received(1).GetSinceAsync(now.AddDays(-3), 50, cancellationToken);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
