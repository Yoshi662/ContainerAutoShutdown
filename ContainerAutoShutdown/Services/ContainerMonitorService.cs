using ContainerAutoShutdown.Configuration;
using ContainerAutoShutdown.Helpers;
using ContainerAutoShutdown.Models;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace ContainerAutoShutdown.Services;

public sealed class ContainerMonitorService(
    IDockerClient dockerClient,
    IOptions<ShutdownOptions> options,
    TimeProvider timeProvider,
    ILogger<ContainerMonitorService> logger) : IContainerMonitorService
{
    private readonly ShutdownOptions _options = options.Value;
    private readonly string? _selfContainerId = Environment.GetEnvironmentVariable("HOSTNAME");

    public async Task<IReadOnlyList<MonitoredContainer>> GetContainersDueForShutdownAsync(CancellationToken cancellationToken)
    {
        var parameters = new ContainersListParameters
        {
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["label"] = new Dictionary<string, bool>
                {
                    [_options.LabelKey] = true
                }
            }
        };
        var containers = await dockerClient.Containers.ListContainersAsync(parameters, cancellationToken);

        logger.LogDebug("Found {Count} container(s) with label '{LabelKey}'", containers.Count, _options.LabelKey);

        var result = new List<MonitoredContainer>();
        var now = timeProvider.GetUtcNow();

        foreach (var container in containers)
        {
            var shortId = container.ID[..12];

            if (_selfContainerId is not null && container.ID.StartsWith(_selfContainerId, StringComparison.OrdinalIgnoreCase))
            {
                logger.LogDebug("Skipping self container {ContainerId}", shortId);
                continue;
            }

            if (!container.Labels.TryGetValue(_options.LabelKey, out var durationStr))
                continue;

            if (!DurationParser.TryParse(durationStr, out var duration))
            {
                logger.LogWarning(
                    "Container {ContainerName} ({ContainerId}) has invalid duration label value: '{Duration}'",
                    container.Names.FirstOrDefault(), shortId, durationStr);
                continue;
            }

            ContainerInspectResponse inspection;
            try
            {
                inspection = await dockerClient.Containers.InspectContainerAsync(container.ID, cancellationToken);
            }
            catch (DockerContainerNotFoundException)
            {
                logger.LogDebug("Container {ContainerId} disappeared before inspection", shortId);
                continue;
            }

            var startedAt = DateTimeOffset.Parse(inspection.State.StartedAt, CultureInfo.InvariantCulture);
            var name = container.Names.FirstOrDefault()?.TrimStart('/') ?? shortId;

            var monitored = new MonitoredContainer
            {
                Id = container.ID,
                Name = name,
                StartedAt = startedAt,
                AllowedDuration = duration
            };

            if (now >= monitored.ShutdownAt)
            {
                logger.LogInformation(
                    "Container {ContainerName} ({ContainerId}) is due for shutdown. Started: {StartedAt}, Allowed: {Duration}, Deadline: {ShutdownAt}",
                    name, shortId, startedAt, duration, monitored.ShutdownAt);
                result.Add(monitored);
            }
            else
            {
                var remaining = monitored.ShutdownAt - now;
                logger.LogDebug(
                    "Container {ContainerName} ({ContainerId}) has {Remaining} remaining",
                    name, shortId, remaining);
            }
        }

        return result;
    }
}
