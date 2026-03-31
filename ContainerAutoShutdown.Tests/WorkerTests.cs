using ContainerAutoShutdown.Configuration;
using ContainerAutoShutdown.Models;
using ContainerAutoShutdown.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace ContainerAutoShutdown.Tests;

public class WorkerTests
{
    private readonly Mock<IContainerMonitorService> _mockMonitor = new();
    private readonly Mock<IContainerShutdownService> _mockShutdown = new();
    private readonly ShutdownOptions _options = new() { PollingIntervalSeconds = 60 };
    private readonly Worker _sut;

    public WorkerTests()
    {
        _sut = new Worker(
            _mockMonitor.Object,
            _mockShutdown.Object,
            Options.Create(_options),
            NullLogger<Worker>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_StopsContainersDueForShutdown()
    {
        var container = new MonitoredContainer
        {
            Id = "abc123def456789",
            Name = "test-app",
            StartedAt = DateTimeOffset.UtcNow.AddHours(-3),
            AllowedDuration = TimeSpan.FromHours(2)
        };

        _mockMonitor
            .Setup(m => m.GetContainersDueForShutdownAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MonitoredContainer> { container });

        using var cts = new CancellationTokenSource();
        var workerTask = _sut.StartAsync(cts.Token);

        // Allow one polling cycle to execute
        await Task.Delay(500);
        cts.Cancel();
        await _sut.StopAsync(CancellationToken.None);

        _mockShutdown.Verify(
            s => s.StopContainerAsync("abc123def456789", It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_ContinuesWhenIndividualContainerStopFails()
    {
        var container1 = new MonitoredContainer
        {
            Id = "aaaa11112222333",
            Name = "failing-app",
            StartedAt = DateTimeOffset.UtcNow.AddHours(-5),
            AllowedDuration = TimeSpan.FromHours(1)
        };

        var container2 = new MonitoredContainer
        {
            Id = "bbbb44445555666",
            Name = "ok-app",
            StartedAt = DateTimeOffset.UtcNow.AddHours(-5),
            AllowedDuration = TimeSpan.FromHours(1)
        };

        _mockMonitor
            .Setup(m => m.GetContainersDueForShutdownAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MonitoredContainer> { container1, container2 });

        _mockShutdown
            .Setup(s => s.StopContainerAsync("aaaa11112222333", It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Docker API error"));

        using var cts = new CancellationTokenSource();
        var workerTask = _sut.StartAsync(cts.Token);

        await Task.Delay(500);
        cts.Cancel();
        await _sut.StopAsync(CancellationToken.None);

        // Second container should still be processed despite first one failing
        _mockShutdown.Verify(
            s => s.StopContainerAsync("bbbb44445555666", It.IsAny<CancellationToken>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task ExecuteAsync_DoesNotCallShutdownWhenNoContainersDue()
    {
        _mockMonitor
            .Setup(m => m.GetContainersDueForShutdownAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<MonitoredContainer>());

        using var cts = new CancellationTokenSource();
        var workerTask = _sut.StartAsync(cts.Token);

        await Task.Delay(500);
        cts.Cancel();
        await _sut.StopAsync(CancellationToken.None);

        _mockShutdown.Verify(
            s => s.StopContainerAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ContinuesAfterMonitoringFailure()
    {
        var callCount = 0;

        _mockMonitor
            .Setup(m => m.GetContainersDueForShutdownAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                callCount++;
                if (callCount == 1)
                    throw new InvalidOperationException("Docker daemon unreachable");

                return Task.FromResult<IReadOnlyList<MonitoredContainer>>(new List<MonitoredContainer>());
            });

        // Use a short polling interval to get two cycles quickly
        var fastOptions = new ShutdownOptions { PollingIntervalSeconds = 1 };
        var worker = new Worker(
            _mockMonitor.Object,
            _mockShutdown.Object,
            Options.Create(fastOptions),
            NullLogger<Worker>.Instance);

        using var cts = new CancellationTokenSource();
        var workerTask = worker.StartAsync(cts.Token);

        // Wait long enough for at least two cycles
        await Task.Delay(2500);
        cts.Cancel();
        await worker.StopAsync(CancellationToken.None);

        // Monitor was called more than once despite the first call throwing
        Assert.True(callCount >= 2, $"Expected at least 2 calls, got {callCount}");
    }
}
