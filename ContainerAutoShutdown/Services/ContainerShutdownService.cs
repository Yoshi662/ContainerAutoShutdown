using ContainerAutoShutdown.Configuration;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Options;

namespace ContainerAutoShutdown.Services;

public sealed class ContainerShutdownService(
    IDockerClient dockerClient,
    IOptions<ShutdownOptions> options,
    ILogger<ContainerShutdownService> logger) : IContainerShutdownService
{
    private readonly ShutdownOptions _options = options.Value;

    public async Task StopContainerAsync(string containerId, CancellationToken cancellationToken)
    {
        var shortId = containerId[..12];

        logger.LogInformation(
            "Sending stop signal to container {ContainerId} with {Timeout}s grace period",
            shortId, _options.StopTimeoutSeconds);

        try
        {
            var stopped = await dockerClient.Containers.StopContainerAsync(
                containerId,
                new ContainerStopParameters
                {
                    WaitBeforeKillSeconds = (uint)_options.StopTimeoutSeconds
                },
                cancellationToken);

            if (stopped)
            {
                logger.LogInformation("Container {ContainerId} stopped successfully", shortId);
            }
            else
            {
                logger.LogWarning("Container {ContainerId} was already stopped", shortId);
            }
        }
        catch (DockerContainerNotFoundException)
        {
            logger.LogWarning("Container {ContainerId} was not found (may have been removed)", shortId);
        }
    }
}
