using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DocumentLifecycle.Infrastructure.Workspaces;

internal sealed class WorkspaceCleanupHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<DemoModeOptions> options,
    ILogger<WorkspaceCleanupHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(options.Value.CleanupInterval);

        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var runner = scope.ServiceProvider.GetRequiredService<WorkspaceCleanupRunner>();
                var removed = await runner.CleanupExpiredAsync(stoppingToken);

                if (removed > 0)
                {
                    logger.LogInformation("Removed {WorkspaceCount} expired demo workspaces.", removed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "The demo workspace cleanup pass failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
