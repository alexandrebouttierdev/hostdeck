# Testing

## Running the suite

```bash
dotnet test -c Release
```

The full gate, identical to CI:

```bash
dotnet restore
dotnet format --verify-no-changes
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
```

## Layout

| Project | Scope |
|---|---|
| `tests/HostDeck.Domain.Tests` | Invariants, state transitions, value objects, thresholds, severities. Fast, no I/O. |
| `tests/HostDeck.Application.Tests` | Use cases against hand-written fakes for the ports. |
| `tests/HostDeck.Infrastructure.Tests` | Parsers, repositories against a real temporary SQLite database, error mapping. |
| `tests/HostDeck.Presentation.Tests` | Avalonia headless tests of view model and view behaviour. |
| `tests/HostDeck.IntegrationTests` | End-to-end paths that need Docker or the network. Skippable. |

## Framework

xUnit v3, pinned to 3.2.2 to stay binary-compatible with `Avalonia.Headless.XUnit` — see
`docs/DEPENDENCIES.md`.

No mocking framework. Hand-written fakes are preferred while they remain simple; the ports are
small enough that a fake is usually a dozen lines and reads better than a mock setup.

## What each layer tests

### Domain

Validation rules, status and severity transitions, invalid transitions being rejected,
thresholds, cooldown arithmetic, retention windows, connection modes. These tests must never
touch infrastructure and must stay fast enough to run constantly.

### Application

CRUD paths and their failure modes: invalid server, missing jump host, jump host cycle, SSH
timeout, authentication failure, host key failure, gateway unavailable, repository failure,
incident creation, acknowledgement, resolution, Docker container states, alert cooldown,
cancellation, and bounded retry.

### Parsers

Fixture-driven, with real captured output from Ubuntu 22.04, Ubuntu 24.04, Debian 12, Debian 13,
Fedora, Rocky Linux and AlmaLinux. Each parser is tested against normal values, invalid values,
missing values, multiple interfaces, multiple partitions, unexpected whitespace, extra lines and
very large numbers. Technical formats are parsed with `CultureInfo.InvariantCulture`, and a test
asserts that parsing does not depend on the current culture.

### Persistence

Run against a real temporary SQLite database — the SQLite repository is never tested against a
mock of SQLite. Covers migrations, insert/update/delete, transactions and rollback, foreign
keys, constraints, critical indexes, time-range queries, retention, pruning and downsampling.

### Presentation

Avalonia headless tests cover behaviour, not pixel-perfect rendering: rail navigation, host
selection, opening and closing the detail panel, filters, incident actions, dialog confirmation,
loading/error/empty states, async commands, panel resize logic, and disabled/busy states.

A small number of stable components may get screenshot assertions. The suite as a whole must not
become fragile to cross-platform rendering differences, and these tests never replace human
comparison against the mockups.

### Integration

Tests needing Docker or the network live in `HostDeck.IntegrationTests`, are separated from unit
tests, and skip cleanly when the dependency is unavailable. Unit tests never depend on the
developer's own Docker daemon.

## Concurrency

.NET has no direct equivalent of `go test -race` for this project, so concurrency correctness is
pursued through design and targeted tests: immutable snapshots over shared mutable state,
explicit synchronisation, and stress plus cancellation tests around
`FleetMonitoringCoordinator`, the SSH connection pool, Docker stats streams, caches, event
publication, collection updates, application shutdown, and concurrent prune-plus-read.

## Long sessions

Memory and lifecycle are validated with long-running sessions simulating hundreds of collection
cycles, checking for timers that are never stopped, undisposed `CancellationTokenSource`,
unsubscribed handlers, unclosed SSH connections or Docker streams, unbounded caches, and view
models retained by global events.

## Visual validation

Screenshot comparison against the mockups is mandatory and manual. See `docs/UI_DESIGN.md`.
