using System.Text.Json;
using PRN222_FINAL.DAL.Models.Accounts;
using PRN222_FINAL.DAL.Repositories.Accounts;

namespace PRN222_FINAL.BLL;

public sealed record WelcomeEmailJob(
    Guid Id,
    Guid UserId,
    string Email,
    string FullName,
    string Role,
    string ApplicationBaseUrl,
    IReadOnlyList<string> SubjectLabels,
    int Attempts = 0);

public interface IAccountEmailJobQueue
{
    Task EnqueueAsync(WelcomeEmailJob job, CancellationToken cancellationToken = default);
    Task<WelcomeEmailJob?> ClaimNextAsync(CancellationToken cancellationToken = default);
    Task CompleteAsync(Guid jobId, CancellationToken cancellationToken = default);
    Task RetryAsync(WelcomeEmailJob job, string error, bool terminal, CancellationToken cancellationToken = default);
}

public sealed class AccountEmailJobQueue : IAccountEmailJobQueue
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IAccountEmailJobRepository _repository;

    public AccountEmailJobQueue(IAccountEmailJobRepository repository) => _repository = repository;

    public Task EnqueueAsync(WelcomeEmailJob job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);
        var now = DateTimeOffset.UtcNow;
        return _repository.EnqueueAsync(new AccountEmailJobData
        {
            Id = job.Id,
            UserId = job.UserId,
            Email = job.Email,
            FullName = job.FullName,
            Role = job.Role,
            ApplicationBaseUrl = job.ApplicationBaseUrl,
            SubjectLabelsJson = JsonSerializer.Serialize(job.SubjectLabels, JsonOptions),
            AvailableAt = now,
            CreatedAt = now
        }, cancellationToken);
    }

    public async Task<WelcomeEmailJob?> ClaimNextAsync(CancellationToken cancellationToken = default)
    {
        var data = await _repository.ClaimNextAsync(DateTimeOffset.UtcNow, TimeSpan.FromMinutes(5), cancellationToken);
        if (data is null)
        {
            return null;
        }

        IReadOnlyList<string> labels;
        try
        {
            labels = JsonSerializer.Deserialize<string[]>(data.SubjectLabelsJson, JsonOptions) ?? Array.Empty<string>();
        }
        catch (JsonException)
        {
            labels = Array.Empty<string>();
        }

        return new WelcomeEmailJob(data.Id, data.UserId, data.Email, data.FullName, data.Role,
            data.ApplicationBaseUrl, labels, data.Attempts);
    }

    public Task CompleteAsync(Guid jobId, CancellationToken cancellationToken = default) =>
        _repository.CompleteAsync(jobId, DateTimeOffset.UtcNow, cancellationToken);

    public Task RetryAsync(WelcomeEmailJob job, string error, bool terminal, CancellationToken cancellationToken = default)
    {
        var delayMinutes = Math.Min(60, Math.Pow(2, Math.Max(0, job.Attempts - 1)));
        return _repository.RescheduleAsync(
            job.Id,
            DateTimeOffset.UtcNow.AddMinutes(delayMinutes),
            error,
            terminal,
            cancellationToken);
    }
}
