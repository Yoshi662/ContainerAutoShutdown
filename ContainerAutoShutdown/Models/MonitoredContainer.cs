namespace ContainerAutoShutdown.Models;

public sealed record MonitoredContainer
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required DateTimeOffset StartedAt { get; init; }

    public required TimeSpan AllowedDuration { get; init; }

    public DateTimeOffset ShutdownAt => StartedAt + AllowedDuration;
}
