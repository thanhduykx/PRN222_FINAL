using PRN222_FINAL.DAL.Repositories.Billing;
using Xunit;

namespace PRN222_FINAL.BLL.Tests;

public sealed class SubscriptionActivationPolicyTests
{
    [Fact]
    public void CalculateEnd_ReplacesUnusedTimeWithPurchasedDuration()
    {
        var now = new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);

        var end = SubscriptionActivationPolicy.CalculateEnd(
            now,
            now,
            now.AddDays(30));

        Assert.Equal(now.AddDays(30), end);
    }

    [Fact]
    public void CalculateEnd_PreservesPurchasedDurationFromActivationTime()
    {
        var now = new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);

        var end = SubscriptionActivationPolicy.CalculateEnd(
            now,
            now.AddDays(-2),
            now.AddDays(28));

        Assert.Equal(now.AddDays(30), end);
    }
}
