using Xunit;

namespace PRN222_FINAL.BLL.Tests;

public sealed class SubscriptionStatusCompatibilityTests
{
    [Fact]
    public void PausedDatabaseStatus_IsSupportedByDalAndBusinessEnums()
    {
        Assert.True(Enum.TryParse<PRN222_FINAL.DAL.Enums.SubscriptionStatus>("Paused", out _));
        Assert.True(Enum.TryParse<PRN222_FINAL.BLL.Models.SubscriptionStatus>("Paused", out _));
    }
}
