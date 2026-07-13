using PRN222_FINAL.DAL.Repositories.Billing;
using Xunit;

namespace PRN222_FINAL.BLL.Tests;

public sealed class SubscriptionActivationPolicyTests
{
    [Fact]
    public void CalculateEnd_CarriesForwardUnusedTime()
    {
        var now = new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);

        var end = SubscriptionActivationPolicy.CalculateEnd(
            now,
            now,
            now.AddDays(30),
            new[] { now.AddDays(12) });

        Assert.Equal(now.AddDays(42), end);
    }

    [Fact]
    public void CalculateEnd_DoesNotDoubleCountOverlappingLegacySubscriptions()
    {
        var now = new DateTimeOffset(2026, 7, 14, 0, 0, 0, TimeSpan.Zero);

        var end = SubscriptionActivationPolicy.CalculateEnd(
            now,
            now,
            now.AddDays(30),
            new[] { now.AddDays(5), now.AddDays(12) });

        Assert.Equal(now.AddDays(42), end);
    }
}
