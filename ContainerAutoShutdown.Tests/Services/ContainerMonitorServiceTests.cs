using ContainerAutoShutdown.Configuration;
using ContainerAutoShutdown.Services;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ContainerAutoShutdown.Tests.Services;

public class ContainerMonitorServiceTests
{
    private readonly Mock<IDockerClient> _mockClient = new();
    private readonly Mock<IContainerOperations> _mockContainerOps = new();
    private readonly Mock<TimeProvider> _mockTimeProvider = new();
    private readonly ShutdownOptions _options = new();
    private readonly ContainerMonitorService _sut;

    private static readonly DateTimeOffset Now = new(2025, 7, 1, 12, 0, 0, TimeSpan.Zero);

    public ContainerMonitorServiceTests()
    {
        _mockClient.Setup(c => c.Containers).Returns(_mockContainerOps.Object);
        _mockTimeProvider.Setup(tp => tp.GetUtcNow()).Returns(Now);

        _sut = new ContainerMonitorService(
            _mockClient.Object,
            Options.Create(_options),
            _mockTimeProvider.Object,
            NullLogger<ContainerMonitorService>.Instance);
    }

    [Fact]
    public async Task GetContainersDueForShutdown_ReturnsExpiredContainers()
    {
        // Container started 3 hours ago with a 2h limit
        var startedAt = Now.AddHours(-3);
        SetupContainerList("aabbccddeeff0011", "test-app", "2h");
        SetupInspect("aabbccddeeff0011", startedAt);

        var result = await _sut.GetContainersDueForShutdownAsync(CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("aabbccddeeff0011", result[0].Id);
        Assert.Equal("test-app", result[0].Name);
    }

    [Fact]
    public async Task GetContainersDueForShutdown_ExcludesContainersNotYetExpired()
    {
        // Container started 1 hour ago with a 2h limit
        var startedAt = Now.AddHours(-1);
        SetupContainerList("aabbccddeeff0011", "test-app", "2h");
        SetupInspect("aabbccddeeff0011", startedAt);

        var result = await _sut.GetContainersDueForShutdownAsync(CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetContainersDueForShutdown_ExcludesContainersWithInvalidDuration()
    {
        SetupContainerList("aabbccddeeff0011", "test-app", "invalid");

        var result = await _sut.GetContainersDueForShutdownAsync(CancellationToken.None);

        Assert.Empty(result);
        _mockContainerOps.Verify(
            c => c.InspectContainerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetContainersDueForShutdown_ReturnsEmptyWhenNoContainers()
    {
        _mockContainerOps
            .Setup(c => c.ListContainersAsync(It.IsAny<ContainersListParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ContainerListResponse>());

        var result = await _sut.GetContainersDueForShutdownAsync(CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetContainersDueForShutdown_HandlesContainerDisappearingBeforeInspect()
    {
        SetupContainerList("aabbccddeeff0011", "test-app", "1h");

        _mockContainerOps
            .Setup(c => c.InspectContainerAsync("aabbccddeeff0011", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DockerContainerNotFoundException(System.Net.HttpStatusCode.NotFound, "not found"));

        var result = await _sut.GetContainersDueForShutdownAsync(CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetContainersDueForShutdown_ContainerExactlyAtDeadline_IsIncluded()
    {
        // Container started exactly 2 hours ago with a 2h limit
        var startedAt = Now.AddHours(-2);
        SetupContainerList("aabbccddeeff0011", "test-app", "2h");
        SetupInspect("aabbccddeeff0011", startedAt);

        var result = await _sut.GetContainersDueForShutdownAsync(CancellationToken.None);

        Assert.Single(result);
    }

    [Fact]
    public async Task GetContainersDueForShutdown_MultipleContainers_FiltersCorrectly()
    {
        var expiredStart = Now.AddHours(-3);
        var activeStart = Now.AddMinutes(-10);

        var containers = new List<ContainerListResponse>
        {
            CreateContainerListResponse("aabbccddeeff0011", "app-expired", "2h"),
            CreateContainerListResponse("112233445566aabb", "app-active", "2h")
        };

        _mockContainerOps
            .Setup(c => c.ListContainersAsync(It.IsAny<ContainersListParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(containers);

        SetupInspect("aabbccddeeff0011", expiredStart);
        SetupInspect("112233445566aabb", activeStart);

        var result = await _sut.GetContainersDueForShutdownAsync(CancellationToken.None);

        Assert.Single(result);
        Assert.Equal("aabbccddeeff0011", result[0].Id);
    }

    private ContainerListResponse CreateContainerListResponse(string id, string name, string durationLabel)
    {
        return new ContainerListResponse
        {
            ID = id,
            Names = new List<string> { $"/{name}" },
            Labels = new Dictionary<string, string>
            {
                [_options.LabelKey] = durationLabel
            }
        };
    }

    private void SetupContainerList(string id, string name, string durationLabel)
    {
        _mockContainerOps
            .Setup(c => c.ListContainersAsync(It.IsAny<ContainersListParameters>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ContainerListResponse>
            {
                CreateContainerListResponse(id, name, durationLabel)
            });
    }

    private void SetupInspect(string id, DateTimeOffset startedAt)
    {
        _mockContainerOps
            .Setup(c => c.InspectContainerAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContainerInspectResponse
            {
                State = new ContainerState
                {
                    StartedAt = startedAt.ToString("o")
                }
            });
    }
}
