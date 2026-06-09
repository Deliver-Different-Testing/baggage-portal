using BaggageDelivery.Core.Services;

namespace BaggageDelivery.Api.Services;

public sealed class DespatchJobReleaseWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<DespatchJobReleaseWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);
    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("DespatchJobReleaseWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IDespatchJobReleaseService>();
                await service.DrainOnceAsync(BatchSize, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "DespatchJobReleaseWorker iteration failed");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (TaskCanceledException) { break; }
        }

        logger.LogInformation("DespatchJobReleaseWorker stopping");
    }
}
