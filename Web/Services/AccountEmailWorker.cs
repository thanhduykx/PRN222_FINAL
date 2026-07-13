using PRN222_FINAL.BLL;
using PRN222_FINAL.BLL.Services.Email;

namespace PRN222_FINAL.Web.Services;

public sealed class AccountEmailWorker : BackgroundService
{
    private const int MaxAttempts = 3;
    private readonly IAccountEmailJobQueue _queue;
    private readonly IAccountEmailService _email;
    private readonly ILogger<AccountEmailWorker> _logger;

    public AccountEmailWorker(IAccountEmailJobQueue queue, IAccountEmailService email, ILogger<AccountEmailWorker> logger)
    {
        _queue = queue;
        _email = email;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var job in _queue.DequeueAllAsync(stoppingToken))
            {
                await SendWithRetryAsync(job, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown.
        }
    }

    private async Task SendWithRetryAsync(WelcomeEmailJob job, CancellationToken cancellationToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await _email.SendWelcomeEmailAsync(job.ToAccount(), job.TemporaryPassword, job.LoginUrl, cancellationToken, job.SubjectLabels);
                _logger.LogInformation("Welcome email sent to {Email}.", job.Email);
                return;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                if (attempt == MaxAttempts)
                {
                    _logger.LogError(exception, "Welcome email to {Email} failed after {Attempts} attempts.", job.Email, MaxAttempts);
                    return;
                }

                _logger.LogWarning(exception, "Welcome email attempt {Attempt}/{MaxAttempts} failed for {Email}.", attempt, MaxAttempts, job.Email);
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)), cancellationToken);
            }
        }
    }
}
