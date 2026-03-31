using ContainerAutoShutdown.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ContainerAutoShutdown.Extensions;

public static class HealthCheckExtensions
{
    public static IServiceCollection AddDockerHealthChecks(this IServiceCollection services)
    {
        services.AddHealthChecks()
            .AddCheck<DockerHealthCheck>("docker", tags: ["docker"]);

        return services;
    }

    public static async Task<int> RunHealthCheckAsync(this IHost host)
    {
        var healthService = host.Services.GetRequiredService<HealthCheckService>();
        var result = await healthService.CheckHealthAsync();
        return result.Status == HealthStatus.Healthy ? 0 : 1;
    }
}
