# Docker supervision

HostDeck supervises Docker on remote Linux hosts over the same SSH sessions used for metrics.
There is no requirement to expose the Docker Engine TCP port or the unix socket publicly.

## Approach

`IContainerRuntime` is the Application port. The Infrastructure adapter
`SshContainerRuntime` runs fixed `docker` CLI commands (`info`, `ps`, `inspect`, `stats`,
`start` / `stop` / `restart`, `logs`) and parses their JSON output.

Container identifiers that must appear in a command are validated as hexadecimal strings of
length 12–64 before interpolation, so user-controlled input cannot alter the command shape.

The `Docker.DotNet` package remains in the dependency set for a possible future Engine API
client over a controlled tunnel. V1 uses the CLI path because SSH.NET cannot forward a remote
unix socket to a local TCP listener without an extra remote helper.

## Enabling Docker on a host

Set **Docker enabled** when adding or updating a server. Only hosts with that flag are queried
by `ListDockerContainersUseCase`. If Docker is absent or the daemon is down, the screen shows
an empty state or a user-facing error — HostDeck never fabricates containers.

## Actions

Start, stop and restart are exposed from the Docker screen. Grace periods for stop/restart are
fixed in the use cases. Log follow (`docker logs -f`) over SSH is not wired in the UI for V1;
the port supports a bounded tail via `StreamLogsAsync`.

## Alerts

Domain metrics `DockerContainerStopped`, `DockerContainerUnhealthy` and `DockerRestartCount`
exist for later evaluation. V1 alert evaluation still focuses on host-level CPU/memory/disk/load
and availability; Docker-specific breach wiring can land without changing the port surface.
