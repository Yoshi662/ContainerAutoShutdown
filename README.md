# Container Auto-Shutdown

A lightweight .NET 10 Worker Service that automatically stops Docker containers after a configurable duration. Add a label to any container, and this service will shut it down when the time expires.

## How It Works

1. The service polls the Docker daemon at a configurable interval (default: 30s)
2. It finds all running containers with the `autoshutdown.duration` label (default: autoshutdown.duration)
3. For each container, it compares the uptime against the label value
4. If the container has exceeded its allowed duration, it sends a stop signal

## Quick Start

### Docker Compose

```yaml
services:
  container-auto-shutdown:
    image: ghcr.io/containerautoshutdown/containerautoshutdown:latest
    restart: always
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
```

### Label Your Containers

Add the `autoshutdown.duration` label to any container you want to auto-stop:

```yaml
services:
  my-temp-service:
    image: nginx:latest
    labels:
      autoshutdown.duration: "2h"
```

### Duration Format

| Suffix | Unit    | Examples              |
|--------|---------|-----------------------|
| `s`    | Seconds | `30s`, `90s`          |
| `m`    | Minutes | `5m`, `30m`           |
| `h`    | Hours   | `1h`, `1.5h`, `2h`    |
| `d`    | Days    | `1d`, `0.5d`          |

## Configuration

All settings can be configured via environment variables:

| Environment Variable                    | Default                        | Description                              |
|-----------------------------------------|--------------------------------|------------------------------------------|
| `AutoShutdown__LabelKey`                | `autoshutdown.duration`        | Docker label to look for                 |
| `AutoShutdown__PollingIntervalSeconds`  | `30`                           | How often to check containers (seconds)  |
| `AutoShutdown__StopTimeoutSeconds`      | `10`                           | Grace period before killing a container  |
| `AutoShutdown__DockerEndpoint`          | `unix:///var/run/docker.sock`  | Docker daemon endpoint                   |
| `Logging__LogLevel__ContainerAutoShutdown`                             | `Information`                  | Log level (`Debug`, `Information`, `Warning`, `Error`) |

### Example with custom settings

```yaml
services:
  container-auto-shutdown:
    image: ghcr.io/containerautoshutdown/containerautoshutdown:latest
    restart: always
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock
    environment:
      - AutoShutdown__PollingIntervalSeconds=60
      - AutoShutdown__StopTimeoutSeconds=30
      - Logging__LogLevel__ContainerAutoShutdown=Warning
```

## AI Disclousure
**AI Made. Human supervised.**