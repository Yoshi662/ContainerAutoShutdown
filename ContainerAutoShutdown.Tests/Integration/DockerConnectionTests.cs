using ContainerAutoShutdown.Configuration;
using Docker.DotNet;
using Docker.DotNet.Models;
using System.Runtime.InteropServices;

namespace ContainerAutoShutdown.Tests.Integration;

[Trait("Category", "Integration")]
public class DockerConnectionTests : IAsyncLifetime
{
    private readonly IDockerClient _client;
    private readonly string _endpoint;

    public DockerConnectionTests()
    {
        _endpoint = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "npipe://./pipe/docker_engine"
            : "unix:///var/run/docker.sock";

        _client = new DockerClientConfiguration(new Uri(_endpoint)).CreateClient();
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task DockerEndpoint_CanPingDaemon()
    {
        // This is the most basic connectivity check.
        // If this fails, Docker Desktop is not running or the endpoint is wrong.
        await _client.System.PingAsync();
    }

    [Fact]
    public async Task DockerEndpoint_CanGetSystemInfo()
    {
        var info = await _client.System.GetSystemInfoAsync();

        Assert.NotNull(info);
        Assert.False(string.IsNullOrEmpty(info.OperatingSystem));
    }

    [Fact]
    public async Task ListContainers_ReturnsWithoutError()
    {
        var containers = await _client.Containers.ListContainersAsync(
            new ContainersListParameters { All = true });

        Assert.NotNull(containers);
    }

    [Fact]
    public void ShutdownOptions_DefaultEndpoint_MatchesCurrentPlatform()
    {
        var options = new ShutdownOptions();

        Assert.Equal(_endpoint, options.DockerEndpoint);
    }
}
