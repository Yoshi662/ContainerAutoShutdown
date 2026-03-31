using ContainerAutoShutdown.Configuration;
using ContainerAutoShutdown.Services;
using Microsoft.Extensions.Options;

namespace ContainerAutoShutdown;

public sealed class Worker(
    IContainerMonitorService monitorService,
    IContainerShutdownService shutdownService,
    IOptions<ShutdownOptions> options,
    ILogger<Worker> logger) : BackgroundService
{
    private readonly ShutdownOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "Container Auto-Shutdown Manager started. Polling every {IntervalSeconds}s, Label: '{LabelKey}'",
            _options.PollingIntervalSeconds, _options.LabelKey);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var containers = await monitorService.GetContainersDueForShutdownAsync(stoppingToken);

                if (containers.Count > 0)
                {
                    logger.LogInformation("Found {Count} container(s) due for shutdown", containers.Count);
                }

                foreach (var container in containers)
                {
                    try
                    {
                        await shutdownService.StopContainerAsync(container.Id, stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogError(ex,
                            "Failed to stop container {ContainerName} ({ContainerId})",
                            container.Name, container.Id[..12]);
                    }
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                if (ex.InnerException is HttpRequestException or IOException)
                {
                    logger.LogError(ex, "Cannot connect to Docker. Verify Docker Desktop is running and the endpoint is correct.");
                }
                else
                {
                    logger.LogError(ex, "Error during container monitoring cycle");
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(_options.PollingIntervalSeconds), stoppingToken);
        }

        logger.LogInformation("Container Auto-Shutdown Manager is shutting down");
    }
}
