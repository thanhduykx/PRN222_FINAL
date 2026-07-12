using NSubstitute;
using PRN222_FINAL.BLL.Services.Billing;
using PRN222_FINAL.BLL.Services.Chat;
using Xunit;

namespace PRN222_FINAL.BLL.Tests;

public sealed class ChatUsageServiceTests
{
    [Fact]
    public async Task AcquireUserLockAsync_SerializesRequestsForSameStudent()
    {
        var service = CreateService();
        var userId = Guid.NewGuid();
        var firstLease = await service.AcquireUserLockAsync(userId);

        var secondLeaseTask = service.AcquireUserLockAsync(userId);
        await Task.Delay(50);
        Assert.False(secondLeaseTask.IsCompleted);

        await firstLease.DisposeAsync();
        await using var secondLease = await secondLeaseTask.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.NotNull(secondLease);
    }

    [Fact]
    public async Task AcquireUserLockAsync_DoesNotBlockDifferentStudents()
    {
        var service = CreateService();
        await using var firstLease = await service.AcquireUserLockAsync(Guid.NewGuid());

        await using var secondLease = await service
            .AcquireUserLockAsync(Guid.NewGuid())
            .WaitAsync(TimeSpan.FromSeconds(1));

        Assert.NotNull(secondLease);
    }

    private static ChatUsageService CreateService()
    {
        return new ChatUsageService(
            Substitute.For<ISubscriptionService>(),
            Substitute.For<IKnowledgeService>());
    }
}
