using Docker.DotNet;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ContainerAutoShutdown.Services;

public sealed class DockerHealthCheck(IDockerClient dockerClient) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await dockerClient.System.PingAsync(cancellationToken);
            return HealthCheckResult.Healthy("Docker daemon is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Cannot reach Docker daemon.", ex);
        }
    }
}
