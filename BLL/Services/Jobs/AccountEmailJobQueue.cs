using System.Threading.Channels;
using PRN222_FINAL.BLL.Models;

namespace PRN222_FINAL.BLL;

public sealed record WelcomeEmailJob(
    Guid UserId,
    string Email,
    string FullName,
    string Role,
    string TemporaryPassword,
    string LoginUrl,
    IReadOnlyList<string> SubjectLabels)
{
    public UserAccount ToAccount() => new()
    {
        Id = UserId,
        Email = Email,
        FullName = FullName,
        Role = Role
    };
}

public interface IAccountEmailJobQueue
{
    void Enqueue(WelcomeEmailJob job);
    IAsyncEnumerable<WelcomeEmailJob> DequeueAllAsync(CancellationToken cancellationToken = default);
}

public sealed class AccountEmailJobQueue : IAccountEmailJobQueue
{
    private readonly Channel<WelcomeEmailJob> _channel = Channel.CreateUnbounded<WelcomeEmailJob>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public void Enqueue(WelcomeEmailJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (!_channel.Writer.TryWrite(job))
        {
            throw new InvalidOperationException("Welcome email could not be queued.");
        }
    }

    public IAsyncEnumerable<WelcomeEmailJob> DequeueAllAsync(CancellationToken cancellationToken = default) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
