using System.Runtime.InteropServices;

namespace ContainerAutoShutdown.Configuration;

public sealed class ShutdownOptions
{
    public const string SectionName = "AutoShutdown";

    public string LabelKey { get; set; } = "autoshutdown.duration";

    public int PollingIntervalSeconds { get; set; } = 30;

    public int StopTimeoutSeconds { get; set; } = 10;

    public string DockerEndpoint { get; set; } = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
        ? "npipe://./pipe/docker_engine"
        : "unix:///var/run/docker.sock";
}
