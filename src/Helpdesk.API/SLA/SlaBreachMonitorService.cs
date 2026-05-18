namespace Helpdesk.API.SLA;

public sealed class SlaBreachMonitorService(
    IServiceScopeFactory scopeFactory,
    ILogger<SlaBreachMonitorService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SLA breach monitor started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Interval, stoppingToken);
                await RunCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SLA breach monitor cycle failed.");
            }
        }

        logger.LogInformation("SLA breach monitor stopped.");
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var processor = scope.ServiceProvider.GetRequiredService<SlaBreachProcessor>();
        await processor.ProcessAsync(ct);
        logger.LogDebug("SLA breach processing cycle completed.");
    }
}
