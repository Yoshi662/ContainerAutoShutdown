using ContainerAutoShutdown;
using ContainerAutoShutdown.Configuration;
using ContainerAutoShutdown.Extensions;
using ContainerAutoShutdown.Services;
using Docker.DotNet;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<ShutdownOptions>(
    builder.Configuration.GetSection(ShutdownOptions.SectionName));

builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddSingleton<IDockerClient>(sp =>
{
    var shutdownOptions = sp.GetRequiredService<IOptions<ShutdownOptions>>().Value;
    var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DockerClient");
    logger.LogInformation("Connecting to Docker at {DockerEndpoint}", shutdownOptions.DockerEndpoint);
    return new DockerClientConfiguration(new Uri(shutdownOptions.DockerEndpoint)).CreateClient();
});

builder.Services.AddDockerHealthChecks();

builder.Services.AddSingleton<IContainerMonitorService, ContainerMonitorService>();
builder.Services.AddSingleton<IContainerShutdownService, ContainerShutdownService>();

if (args.Contains("healthcheck", StringComparer.OrdinalIgnoreCase))
{
    var healthHost = builder.Build();
    return await healthHost.RunHealthCheckAsync();
}

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

return 0;
