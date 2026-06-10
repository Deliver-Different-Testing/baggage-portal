using BaggageDelivery.Core.Interfaces;
using Serilog;

namespace BaggageDelivery.Api.Services;

public sealed class OrphanReconciliationWorker(IServiceScopeFactory scopeFactory) : BackgroundService
{
    // Hygiene cadence — slower than the two drain workers. Reconciliation
    // checks against Despatch via HTTP, so we keep the batch small and the
    // interval long to limit pressure on the API and circuit-breaker.
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(5);
    private const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Log.Information("OrphanReconciliationWorker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IOrphanReconciliationService>();
                await service.ReconcileOnceAsync(BatchSize, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "OrphanReconciliationWorker iteration failed");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (TaskCanceledException) { break; }
        }

        Log.Information("OrphanReconciliationWorker stopping");
    }
}
