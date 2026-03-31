using ContainerAutoShutdown.Models;

namespace ContainerAutoShutdown.Services;

public interface IContainerMonitorService
{
    Task<IReadOnlyList<MonitoredContainer>> GetContainersDueForShutdownAsync(CancellationToken cancellationToken);
}
