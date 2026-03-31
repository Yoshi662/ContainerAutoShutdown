using ContainerAutoShutdown.Configuration;
using ContainerAutoShutdown.Services;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Runtime.InteropServices;

namespace ContainerAutoShutdown.Tests.Integration;

[Trait("Category", "Integration")]
public class ContainerLifecycleTests : IAsyncLifetime
{
    private const string TestImage = "mcr.microsoft.com/dotnet/runtime-deps:10.0-alpine";
    private const string TestContainerName = "integration-test-autoshutdown";
    private const string TestLabel = "autoshutdown.duration";
    private const string TestDuration = "1h";

    private readonly IDockerClient _client;
    private string? _containerId;

    public ContainerLifecycleTests()
    {
        var endpoint = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "npipe://./pipe/docker_engine"
            : "unix:///var/run/docker.sock";

        _client = new DockerClientConfiguration(new Uri(endpoint)).CreateClient();
    }

    public async Task InitializeAsync()
    {
        // Ensure the image is available
        await _client.Images.CreateImageAsync(
            new ImagesCreateParameters { FromImage = TestImage },
            null,
            new Progress<JSONMessage>());

        // Remove leftover container from a previous failed run
        await RemoveTestContainerIfExists();

        // Create and start a labeled container
        var response = await _client.Containers.CreateContainerAsync(
            new CreateContainerParameters
            {
                Image = TestImage,
                Name = TestContainerName,
                Entrypoint = ["sleep", "3600"],
                Labels = new Dictionary<string, string>
                {
                    [TestLabel] = TestDuration
                }
            });

        _containerId = response.ID;
        await _client.Containers.StartContainerAsync(_containerId, new ContainerStartParameters());
    }

    public async Task DisposeAsync()
    {
        await RemoveTestContainerIfExists();
        _client.Dispose();
    }

    [Fact]
    public async Task MonitorService_DetectsLabeledContainer()
    {
        var options = new ShutdownOptions { LabelKey = TestLabel };
        var monitorService = new ContainerMonitorService(
            _client,
            Options.Create(options),
            TimeProvider.System,
            NullLogger<ContainerMonitorService>.Instance);

        // The container was just started with a 1h duration, so it should NOT be due yet
        var dueContainers = await monitorService.GetContainersDueForShutdownAsync(CancellationToken.None);

        // Not due for shutdown, but the service should not throw
        Assert.DoesNotContain(dueContainers, c => c.Id == _containerId);
    }

    [Fact]
    public async Task ListContainers_FilterByLabel_FindsTestContainer()
    {
        var parameters = new ContainersListParameters
        {
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["label"] = new Dictionary<string, bool>
                {
                    [TestLabel] = true
                }
            }
        };

        var containers = await _client.Containers.ListContainersAsync(parameters);

        Assert.Contains(containers, c => c.ID == _containerId);
    }

    [Fact]
    public async Task InspectContainer_ReturnsValidStartedAt()
    {
        Assert.NotNull(_containerId);

        var inspection = await _client.Containers.InspectContainerAsync(_containerId);

        Assert.NotNull(inspection.State.StartedAt);
        Assert.True(DateTimeOffset.TryParse(inspection.State.StartedAt, out var startedAt));
        Assert.True(startedAt > DateTimeOffset.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task ShutdownService_CanStopContainer()
    {
        Assert.NotNull(_containerId);

        var options = new ShutdownOptions { StopTimeoutSeconds = 5 };
        var shutdownService = new ContainerShutdownService(
            _client,
            Options.Create(options),
            NullLogger<ContainerShutdownService>.Instance);

        await shutdownService.StopContainerAsync(_containerId, CancellationToken.None);

        var inspection = await _client.Containers.InspectContainerAsync(_containerId);
        Assert.False(inspection.State.Running);
    }

    private async Task RemoveTestContainerIfExists()
    {
        try
        {
            await _client.Containers.StopContainerAsync(
                TestContainerName,
                new ContainerStopParameters { WaitBeforeKillSeconds = 1 });
        }
        catch (DockerContainerNotFoundException) { }
        catch (DockerApiException) { }

        try
        {
            await _client.Containers.RemoveContainerAsync(
                TestContainerName,
                new ContainerRemoveParameters { Force = true });
        }
        catch (DockerContainerNotFoundException) { }
        catch (DockerApiException) { }
    }
}
