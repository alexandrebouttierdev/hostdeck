# Architecture

HostDeck is a layered .NET 10 desktop application. The layering exists to keep the supervision
core — which is concurrent, cancellable and security-sensitive — testable in isolation from
Avalonia, SSH, Docker and the database.

## Layers

```text
src/HostDeck.Domain/          Entities, value objects, invariants. Zero dependencies.
src/HostDeck.Application/     Use cases, DTOs, ports, validation. Depends on Domain.
src/HostDeck.Infrastructure/  Adapters. Depends on Application.
src/HostDeck.Presentation/    Avalonia UI. Depends on Application.
src/HostDeck.Desktop/         Composition root. Depends on Presentation + Infrastructure.
```

The dependency direction is enforced by project references — Presentation and Infrastructure
cannot see each other, and neither can reach into the other's implementation types.

## Runtime flow

```text
Avalonia View
  |  binding / command
ViewModel
  |
Application use case
  |
Port (interface)
  |
Infrastructure adapter   ->  SSH.NET | Docker Engine | SQLite | OS keychain
```

A view model never opens a database connection, an SSH session or a Docker client. It calls a
use case, which calls a port, which is implemented by an adapter registered in the composition
root.

## Domain

Pure C#: entities, `readonly record struct` value objects, and closed enums for states. Business
invariants are held by constructors and factory methods, and are covered by fast unit tests.
Business states are never represented as free-form strings.

The Domain has no reference to Avalonia, EF Core, SSH.NET, Docker.DotNet, the credential store
or the notification stack.

## Application

Holds one type per use case, each with a single responsibility and a `CancellationToken` on
every I/O-bound method. DTOs are `record` types, separate from Domain entities, with explicit
and tested conversions. Ports (`IServerRepository`, `ISshConnectionFactory`,
`IContainerRuntime`, `ICredentialStore`, …) are declared here because this is the layer that
needs them.

Input DTOs are validated with **FluentValidation** at the use-case boundary. This is
input-shape validation — required fields, ranges, formats. It does not replace Domain
invariants, which remain the Domain's own responsibility so that the model cannot be
constructed in an invalid state even if validation is bypassed.

## Infrastructure

Contains the adapters, and confines every third-party model to itself. `SshClient`,
Docker SDK types and EF Core entities never appear above this layer. Technical exceptions are
translated here into meaningful application-level errors.

### Persistence

Persistence uses **EF Core 10 with the SQLite provider**, in a deliberately hybrid way:

- **EF Core** owns the schema, the versioned migrations, and CRUD over the low-volume,
  relational part of the model: servers, tags, groups, alert rules, incidents, incident events,
  settings and SSH host keys.
- **Raw parameterised SQL**, executed on the same `DbConnection`, owns the time-series hot path:
  batched metric ingestion and bucketed min/avg/max downsampling queries. EF Core's change
  tracker and materialisation are the wrong tool for hundreds of thousands of samples, and the
  aggregation shapes are expressed far more directly in SQL.

Persistence records are distinct types from Domain entities, mapped explicitly. The database
holds UTC timestamps only; conversion to local time happens in Presentation.

`Microsoft.Data.Sqlite` does not provide true asynchronous file I/O. Repository code therefore
does not assume that `ExecuteReaderAsync` moves work off the calling thread, and long database
work is kept off the UI thread by the layer that calls it.

### Linux parsers

The metric parsers under `HostDeck.Infrastructure/Linux` convert the raw text read from a host
(`/proc/stat`, `/proc/meminfo`, `/proc/loadavg`, `/proc/net/dev`, `/proc/uptime`,
`df -P -k` and `/etc/os-release`) into Domain value objects. They are pure functions: no SSH,
no Avalonia and no I/O. Host output is untrusted input, so unknown lines are skipped and only
the absence of essential data is reported as an explicit format error
(`MetricParseException`); the CPU usage is derived from the difference between two counter
snapshots, never from a single read.

### Background collection

Fleet collection runs as a hosted service (`FleetMonitoringCoordinator`) with explicit
scheduling, a bounded concurrency limit, timeouts, bounded retry with backoff, SSH connection
reuse and full cancellation. It publishes changes through an internal typed event channel rather
than referencing view models.

## Presentation

MVVM, with views kept as passive as possible. Each screen owns a view model that receives its
dependencies through the constructor, exposes commands, and models loading, error and empty
states explicitly. Code-behind is allowed only for purely visual interaction that cannot be
expressed cleanly through bindings.

Charts and sparklines are custom controls that inherit from `Control` and override `Render`,
because no general-purpose chart library reaches the density and visual language the mockups
require.

## Desktop

The composition root. It configures dependency injection, logging and options, builds the
generic host, starts and stops background services, creates the main window, and translates
process signals (SIGTERM, SIGINT, SIGQUIT) into an ordered application shutdown.

## Threading

Avalonia is single-threaded for UI. SSH, Docker and database work happens off the UI thread and
results reach the UI through bindings and the dispatcher. There is no `.Result`, no `.Wait()`,
and no `async void` outside genuine UI event handlers.

## Further reading

- [`DEPENDENCIES.md`](DEPENDENCIES.md) — every dependency and why it is here
- [`SECURITY.md`](SECURITY.md) — threat model
- [`UI_DESIGN.md`](UI_DESIGN.md) — design system and visual validation
