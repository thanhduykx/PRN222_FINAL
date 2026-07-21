namespace PRN222_FINAL.DAL.Repositories.Billing;

public static class SubscriptionActivationPolicy
{
    public static DateTimeOffset CalculateEnd(
        DateTimeOffset activatedAt,
        DateTimeOffset purchasedStartsAt,
        DateTimeOffset purchasedEndsAt)
    {
        if (purchasedEndsAt.Year >= 9999)
        {
            return new DateTimeOffset(9999, 12, 31, 23, 59, 59, TimeSpan.Zero);
        }

        var purchasedDuration = purchasedEndsAt - purchasedStartsAt;
        if (purchasedDuration <= TimeSpan.Zero)
        {
            throw new InvalidOperationException("Purchased subscription duration must be positive.");
        }

        return activatedAt.Add(purchasedDuration);
    }
}
