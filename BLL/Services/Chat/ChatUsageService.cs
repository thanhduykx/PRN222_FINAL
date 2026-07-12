using PRN222_FINAL.BLL;
using PRN222_FINAL.BLL.Services.Billing;

namespace PRN222_FINAL.BLL.Services.Chat;

public sealed record ChatUsage(int? MonthlyLimit, int Used, int? Remaining, string PlanName)
{
    public bool IsExhausted => Remaining == 0;
}

public interface IChatUsageService
{
    Task<ChatUsage> GetAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IAsyncDisposable> AcquireUserLockAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed class ChatUsageService : IChatUsageService
{
    private static readonly object UserLocksSync = new();
    private static readonly Dictionary<Guid, UserLockEntry> UserLocks = new();
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
        {
            return new ChatUsage(0, 0, 0, "Chưa có gói");
        }

        var now = DateTimeOffset.UtcNow;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var sessions = await _knowledge.GetSessionsForOwnerAsync(userId, cancellationToken);
        var used = sessions.Sum(session => session.Messages.Count(message =>
            message.Role.Equals("user", StringComparison.OrdinalIgnoreCase) && message.CreatedAt >= monthStart));
        var limit = Math.Max(0, subscription.MonthlyChatLimit);
        return new ChatUsage(limit, used, Math.Max(0, limit - used), subscription.PackageName);
    }

    public async Task<IAsyncDisposable> AcquireUserLockAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("A valid user id is required for quota synchronization.", nameof(userId));
        }

        UserLockEntry entry;
        lock (UserLocksSync)
        {
            if (!UserLocks.TryGetValue(userId, out entry!))
            {
                entry = new UserLockEntry();
                UserLocks[userId] = entry;
            }
            else
            {
                entry.ReferenceCount++;
            }
        }

        try
        {
            await entry.Semaphore.WaitAsync(cancellationToken);
            return new UserLockLease(userId, entry);
        }
        catch
        {
            ReleaseReference(userId, entry, releaseSemaphore: false);
            throw;
        }
    }

    private static void ReleaseReference(Guid userId, UserLockEntry entry, bool releaseSemaphore)
    {
        if (releaseSemaphore)
        {
            entry.Semaphore.Release();
        }

        lock (UserLocksSync)
        {
            entry.ReferenceCount--;
            if (entry.ReferenceCount == 0
                && UserLocks.TryGetValue(userId, out var current)
                && ReferenceEquals(current, entry))
            {
                UserLocks.Remove(userId);
                entry.Semaphore.Dispose();
            }
        }
    }

    private sealed class UserLockEntry
    {
        public readonly SemaphoreSlim Semaphore = new(1, 1);
        public int ReferenceCount = 1;
    }

    private sealed class UserLockLease : IAsyncDisposable
    {
        private readonly Guid _userId;
        private UserLockEntry? _entry;

        public UserLockLease(Guid userId, UserLockEntry entry)
        {
            _userId = userId;
            _entry = entry;
        }

        public ValueTask DisposeAsync()
        {
            var entry = Interlocked.Exchange(ref _entry, null);
            if (entry is not null)
            {
                ReleaseReference(_userId, entry, releaseSemaphore: true);
            }

            return ValueTask.CompletedTask;
        }
    }
}
