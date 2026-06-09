using BaggageDelivery.Core.Services;

namespace BaggageDelivery.Api.Services;

public sealed class BookingLinkDispatchWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<BookingLinkDispatchWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 25;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("BookingLinkDispatchWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IBookingLinkDispatchService>();
                await service.DrainOnceAsync(BatchSize, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "BookingLinkDispatchWorker iteration failed");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (TaskCanceledException) { break; }
        }

        logger.LogInformation("BookingLinkDispatchWorker stopping");
    }
}
