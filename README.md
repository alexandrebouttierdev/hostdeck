# HostDeck

HostDeck is an open-source desktop application for supervising fleets of VPS, Linux servers and
Docker workloads. It connects over SSH — directly or through a jump host — collects system
metrics, keeps a local history, raises and tracks incidents, and renders it all in a dense,
technical, Netdata-flavoured interface.

It is a real desktop application: **C# / .NET 10 + Avalonia**, with no web layer, no embedded
browser and no mandatory SaaS backend. Everything runs and persists locally.

> **Status: V1 feature-complete on `dev`.** SSH monitoring, incidents/alerts, Docker
> supervision, local persistence, desktop notifications and the Avalonia shell are wired.
> Visual polish against the nine mockups and native installers remain iterative.

## Features (V1)

- Manage multiple Linux hosts, grouped and tagged
- Direct SSH and SSH jump host / bastion connections, with strict host key verification
- System metric collection: CPU, memory, swap, disks, load average, network, uptime
- Docker supervision: containers, stats, logs, start/stop/restart
- Local history with retention, pruning and downsampling
- Custom-rendered time series charts and in-table sparklines
- Incident engine with severities, acknowledgement, resolution and alert rules
- Desktop notifications
- Credentials stored in the OS keychain, never in the database

## Requirements

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) or newer
- Linux, macOS or Windows

On Linux, taking validation screenshots additionally needs `xvfb` and `ImageMagick`.

## Build and run

```bash
dotnet restore
dotnet build -c Release
dotnet run --project src/HostDeck.Desktop
```

## Tests

```bash
dotnet test -c Release
```

The full quality gate, which CI enforces on Linux, Windows and macOS:

```bash
dotnet restore
dotnet format --verify-no-changes
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
```

## Visual validation

The nine reference mockups in `mockups/` are the visual source of truth and are exactly
1672x941. `scripts/screenshot.sh` renders the application inside a private Xvfb display of the
same size, so a capture and its mockup can be compared directly:

```bash
scripts/screenshot.sh screenshots/02_infrastructure.png
```

Captures are written to `screenshots/`, which is git-ignored.

## Project layout

```text
src/
  HostDeck.Domain/          Entities, value objects and invariants. No dependencies.
  HostDeck.Application/     Use cases, DTOs, ports, validation.
  HostDeck.Infrastructure/  SSH, Docker, EF Core/SQLite, credentials, notifications.
  HostDeck.Presentation/    Avalonia views, view models, controls, charts, theme.
  HostDeck.Desktop/         Composition root and application entry point.
tests/                      One test project per layer, plus integration tests.
docs/                       Architecture and technical documentation.
mockups/                    Visual source of truth (nine screens).
scripts/                    Build, screenshot and packaging helpers.
```

## Documentation

Technical documentation lives in [`docs/`](docs/). Start with
[`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md). Also see
[`docs/DOCKER.md`](docs/DOCKER.md) and [`docs/PACKAGING.md`](docs/PACKAGING.md).
Contributors should read [`AGENTS.md`](AGENTS.md), which states the binding rules for this
codebase, and [`CONTRIBUTING.md`](CONTRIBUTING.md).

Security model and reporting: [`SECURITY.md`](SECURITY.md).
