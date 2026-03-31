namespace ContainerAutoShutdown.Services;

public interface IContainerShutdownService
{
    Task StopContainerAsync(string containerId, CancellationToken cancellationToken);
}
