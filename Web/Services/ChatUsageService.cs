using PRN222_FINAL.BLL;
using PRN222_FINAL.BLL.Services.Billing;

namespace PRN222_FINAL.Web.Services;

public sealed record ChatUsage(int? MonthlyLimit, int Used, int? Remaining, string PlanName)
{
    public bool IsExhausted => Remaining == 0;
}

public interface IChatUsageService
{
    Task<ChatUsage> GetAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed class ChatUsageService : IChatUsageService
{
    private readonly ISubscriptionService _subscriptions;
    private readonly IKnowledgeService _knowledge;

    public ChatUsageService(ISubscriptionService subscriptions, IKnowledgeService knowledge)
    {
        _subscriptions = subscriptions;
        _knowledge = knowledge;
    }

    public async Task<ChatUsage> GetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var subscription = await _subscriptions.GetCurrentSubscriptionAsync(userId, cancellationToken);
        if (subscription is null)
            return new ChatUsage(null, 0, null, "Chưa có gói");

        var now = DateTimeOffset.UtcNow;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var sessions = await _knowledge.GetSessionsForOwnerAsync(userId, cancellationToken);
        var used = sessions.Sum(session => session.Messages.Count(message =>
            message.Role.Equals("user", StringComparison.OrdinalIgnoreCase) && message.CreatedAt >= monthStart));
        var limit = Math.Max(0, subscription.MonthlyChatLimit);
        return new ChatUsage(limit, used, Math.Max(0, limit - used), subscription.PackageName);
    }
}
