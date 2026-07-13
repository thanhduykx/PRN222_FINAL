namespace PRN222_FINAL.DAL.Repositories.Billing;

public static class SubscriptionActivationPolicy
{
    public static DateTimeOffset CalculateEnd(
        DateTimeOffset activatedAt,
        DateTimeOffset purchasedStartsAt,
        DateTimeOffset purchasedEndsAt,
        IEnumerable<DateTimeOffset> activeSubscriptionEnds)
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

        var remainingDuration = activeSubscriptionEnds
            .Where(end => end > activatedAt)
            .Select(end => end - activatedAt)
            .DefaultIfEmpty(TimeSpan.Zero)
            .Max();
        return activatedAt.Add(purchasedDuration).Add(remainingDuration);
    }
}
