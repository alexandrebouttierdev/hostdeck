# Dependencies

Every third-party package is vetted before it enters the project, as required by `AGENTS.md`
rule 9. Versions are pinned centrally in `Directory.Packages.props`.

All versions below were checked against nuget.org and their `net10.0` compatibility was
confirmed by an actual restore and build on .NET SDK 10.0.111, not assumed.

## UI

| Package | Version | Licence | Why |
|---|---|---|---|
| `Avalonia` | 12.1.2 | MIT | The UI framework. Ships a `net10.0` target. |
| `Avalonia.Desktop` | 12.1.2 | MIT | Desktop platform backends (X11, Win32, macOS). |
| `Avalonia.Themes.Fluent` | 12.1.2 | MIT | Base theme, heavily overridden by the HostDeck theme. |
| `Avalonia.Controls.DataGrid` | 12.1.2 | MIT | Dense virtualised tables. Free and maintained by the Avalonia team, so no paid component is required for an essential feature. |
| `Avalonia.Fonts.Inter` | 12.1.2 | MIT | Embedded UI typeface, so rendering does not depend on the host's installed fonts. |
| `CommunityToolkit.Mvvm` | 8.4.2 | MIT | Source-generated observable properties and commands. No runtime reflection, no heavy MVVM framework. |

Charts are **not** provided by a library. They are custom Avalonia controls, because no
general-purpose charting package reaches the density and the visual language the mockups
require while staying performant. See `docs/UI_DESIGN.md`.

## Validation

| Package | Version | Licence | Why |
|---|---|---|---|
| `FluentValidation` | 12.1.1 | Apache-2.0 | Declarative validation of input DTOs at the use-case boundary. |
| `FluentValidation.DependencyInjectionExtensions` | 12.1.1 | Apache-2.0 | Registers validators in the composition root. |

Validators check input shape. Domain invariants stay in the Domain, enforced by constructors, so
the model cannot be built in an invalid state even if a validator is bypassed.

## Infrastructure

| Package | Version | Licence | Why |
|---|---|---|---|
| `SSH.NET` | 2026.0.0 | MIT | SSH transport, command execution and port forwarding for jump hosts. Ships a native `net10.0` target. Host key verification is wired through `HostKeyReceived` and is never disabled. Jump hosts use a local forwarded port through the bastion session. |
| `Latchkey` | 0.1.0 | MIT | Cross-platform OS credential store (Windows Credential Manager, macOS Keychain, Linux Secret Service / libsecret). Pure managed P/Invoke, native `net10.0` target, no silent plaintext fallback — matches §14 / T1. |
| `Docker.DotNet` | 3.125.15 | MIT | Docker Engine API client, maintained under the `dotnet` organisation. `netstandard2.0`, so no native dependency. Kept behind the `IContainerRuntime` port so it can be swapped, and so Podman support stays possible later. |
| `Microsoft.EntityFrameworkCore.Sqlite` | 10.0.11 | MIT | Schema, versioned migrations and CRUD over the relational part of the model. |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.11 | MIT | Migration tooling. `PrivateAssets=all`, so it is not shipped. |
| `Microsoft.Data.Sqlite` | 10.0.11 | MIT | The underlying ADO.NET provider, used directly for raw parameterised SQL on the time-series hot path. |

### Note on EF Core

The original specification argued against a heavy ORM for this workload. The maintainer
explicitly chose EF Core, so the project uses it — but only where it pays off. Time-series
ingestion and downsampling go through raw parameterised SQL on the same connection, because EF
Core's change tracking and materialisation are a poor fit for hundreds of thousands of metric
samples. EF migrations satisfy the requirement for versioned, ordered schema migrations.

## Platform

| Package | Version | Licence | Why |
|---|---|---|---|
| `Microsoft.Extensions.Hosting` | 10.0.11 | MIT | Application lifecycle and background services. |
| `Microsoft.Extensions.Hosting.Abstractions` | 10.0.11 | MIT | `BackgroundService` / `IHostedService` for `FleetMonitoringCoordinator` without pulling the full hosting stack into Infrastructure. |
| `Microsoft.Extensions.DependencyInjection.Abstractions` | 10.0.11 | MIT | DI registration extension points in Infrastructure. |
| `Microsoft.Extensions.Logging.Abstractions` | 10.0.11 | MIT | Logging without binding a provider outside the composition root. |
| `Microsoft.Extensions.Options` | 10.0.11 | MIT | Typed configuration for adapters. |

## Tests

| Package | Version | Licence | Why |
|---|---|---|---|
| `xunit.v3` | 3.2.2 | Apache-2.0 | Test framework. |
| `xunit.runner.visualstudio` | 3.1.5 | Apache-2.0 | VSTest adapter used by `dotnet test`. |
| `Microsoft.NET.Test.Sdk` | 18.9.0 | MIT | Test host. |
| `Avalonia.Headless` | 12.1.2 | MIT | Runs Avalonia without a display for UI behaviour tests. |
| `Avalonia.Headless.XUnit` | 12.1.2 | MIT | `[AvaloniaFact]` integration with xUnit. |

`xunit.v3` is deliberately pinned to **3.2.2** rather than 4.0.0: `Avalonia.Headless.XUnit`
12.1.2 is built against `xunit.v3.extensibility.core` 3.2.2, and pairing it with the 4.x
assemblies risks a runtime binding mismatch. Revisit when Avalonia publishes an xUnit v4 build.

No mocking framework is used. Hand-written fakes are preferred while they stay simple; a vetted
substitution library will only be added if a real need appears.

## Deliberately not used

- **Any web technology** — React, Vue, Angular, Electron, Tauri, Blazor Hybrid, WebView. HostDeck
  is a native desktop application; this is a hard product requirement.
- **A charting library** — would not reach the required visual fidelity (see above).
- **Serilog or another logging provider** — `Microsoft.Extensions.Logging` covers the current
  need. A file sink with rotation will be reconsidered when file logging is implemented, and
  justified here at that point.
- **A mocking framework** — see above.
