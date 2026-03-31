using ContainerAutoShutdown.Configuration;
using ContainerAutoShutdown.Services;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ContainerAutoShutdown.Tests.Services;

public class ContainerShutdownServiceTests
{
    private readonly Mock<IDockerClient> _mockClient = new();
    private readonly Mock<IContainerOperations> _mockContainerOps = new();
    private readonly ShutdownOptions _options = new() { StopTimeoutSeconds = 15 };
    private readonly ContainerShutdownService _sut;

    public ContainerShutdownServiceTests()
    {
        _mockClient.Setup(c => c.Containers).Returns(_mockContainerOps.Object);

        _sut = new ContainerShutdownService(
            _mockClient.Object,
            Options.Create(_options),
            NullLogger<ContainerShutdownService>.Instance);
    }

    [Fact]
    public async Task StopContainerAsync_CallsDockerStopWithCorrectParameters()
    {
        const string containerId = "abc123def456789";

        _mockContainerOps
            .Setup(c => c.StopContainerAsync(containerId, It.IsAny<ContainerStopParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _sut.StopContainerAsync(containerId, CancellationToken.None);

        _mockContainerOps.Verify(c => c.StopContainerAsync(
            containerId,
            It.Is<ContainerStopParameters>(p => p.WaitBeforeKillSeconds == 15),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StopContainerAsync_HandlesAlreadyStoppedContainer()
    {
        const string containerId = "abc123def456789";

        _mockContainerOps
            .Setup(c => c.StopContainerAsync(containerId, It.IsAny<ContainerStopParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Should not throw
        await _sut.StopContainerAsync(containerId, CancellationToken.None);

        _mockContainerOps.Verify(c => c.StopContainerAsync(
            containerId,
            It.IsAny<ContainerStopParameters>(),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task StopContainerAsync_HandlesContainerNotFound()
    {
        const string containerId = "abc123def456789";

        _mockContainerOps
            .Setup(c => c.StopContainerAsync(containerId, It.IsAny<ContainerStopParameters>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DockerContainerNotFoundException(System.Net.HttpStatusCode.NotFound, "not found"));

        // Should not throw - gracefully handled
        await _sut.StopContainerAsync(containerId, CancellationToken.None);
    }
}
