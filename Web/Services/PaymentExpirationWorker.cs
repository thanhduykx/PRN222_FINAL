using PRN222_FINAL.BLL.Services.Billing;

namespace PRN222_FINAL.Web.Services;

public sealed class PaymentExpirationWorker : BackgroundService
{
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromSeconds(30);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PaymentExpirationWorker> _logger;

    public PaymentExpirationWorker(IServiceScopeFactory scopeFactory, ILogger<PaymentExpirationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();
                var deleted = await paymentService.CleanupExpiredPaymentsAsync(stoppingToken);
                if (deleted > 0)
                {
                    _logger.LogInformation("Deleted {Count} expired pending payment orders.", deleted);
                }

                await Task.Delay(CleanupInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Could not clean up expired payment orders.");
                await Task.Delay(CleanupInterval, stoppingToken);
            }
        }
    }
}
