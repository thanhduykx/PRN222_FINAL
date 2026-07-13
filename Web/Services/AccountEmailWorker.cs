using PRN222_FINAL.BLL;
using PRN222_FINAL.BLL.Services.Accounts;
using PRN222_FINAL.BLL.Services.Email;

namespace PRN222_FINAL.Web.Services;

public sealed class AccountEmailWorker : BackgroundService
{
    private const int MaxAttempts = 5;
    private readonly IAccountEmailJobQueue _queue;
    private readonly IUserAccountService _users;
    private readonly IAccountEmailService _email;
    private readonly ILogger<AccountEmailWorker> _logger;

    public AccountEmailWorker(
        IAccountEmailJobQueue queue,
        IUserAccountService users,
        IAccountEmailService email,
        ILogger<AccountEmailWorker> logger)
    {
        _queue = queue;
        _users = users;
        _email = email;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            WelcomeEmailJob? job;
            try
            {
                job = await _queue.ClaimNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not claim the next account email job.");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
                continue;
            }
            if (job is null)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                continue;
            }

            await ProcessAsync(job, stoppingToken);
        }
    }

    private async Task ProcessAsync(WelcomeEmailJob job, CancellationToken cancellationToken)
    {
        try
        {
            var reset = await _users.CreatePasswordResetTokenAsync(job.Email, TimeSpan.FromHours(24), cancellationToken)
                ?? throw new InvalidOperationException("The account no longer exists.");
            var activationUrl = $"{job.ApplicationBaseUrl.TrimEnd('/')}/Account/ResetPassword" +
                                $"?email={Uri.EscapeDataString(reset.Account.Email)}" +
                                $"&token={Uri.EscapeDataString(reset.Token)}";
            await _email.SendWelcomeActivationEmailAsync(
                reset.Account,
                activationUrl,
                reset.ExpiresAt,
                cancellationToken);
            await _queue.CompleteAsync(job.Id, cancellationToken);
            _logger.LogInformation("Welcome activation email sent to {Email}.", job.Email);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var terminal = job.Attempts >= MaxAttempts;
            await _queue.RetryAsync(job, exception.Message, terminal, cancellationToken);
            if (terminal)
            {
                _logger.LogError(exception, "Welcome activation email to {Email} failed permanently.", job.Email);
            }
            else
            {
                _logger.LogWarning(exception, "Welcome activation email attempt {Attempt}/{MaxAttempts} failed for {Email}.",
                    job.Attempts, MaxAttempts, job.Email);
            }
        }
    }
}
