# Contributing to HostDeck

Thanks for your interest. This document covers the practical workflow; the binding engineering
rules live in [`AGENTS.md`](AGENTS.md) and are not optional.

## Before you start

Read, in this order:

1. [`AGENTS.md`](AGENTS.md) — the rules this codebase is held to.
2. [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) — layers and dependency direction.
3. The mockup for the screen you are touching, in `mockups/`.

## Setup

```bash
git clone https://github.com/alexandrebouttierdev/hostdeck.git
cd hostdeck
dotnet restore
dotnet build -c Release
```

You need the .NET 10 SDK. On Linux, screenshot validation also needs `xvfb` and `ImageMagick`:

```bash
# Fedora
sudo dnf install xorg-x11-server-Xvfb ImageMagick
# Debian / Ubuntu
sudo apt install xvfb imagemagick
```

## Quality gate

Every change must pass the same four commands CI runs:

```bash
dotnet restore
dotnet format --verify-no-changes
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
```

`TreatWarningsAsErrors` is enabled. If a warning genuinely cannot be fixed, suppress it as
narrowly as possible and write down why, next to the suppression.

Run `dotnet format` (without `--verify-no-changes`) to fix formatting automatically.

## Language conventions

- Code, identifiers, database schema and documentation: **English**.
- User-facing strings and code comments: **French**.
- Commit messages and pull request descriptions: English.

## Working on a screen

UI work is not finished when it compiles. For each screen:

1. Open the corresponding mockup in `mockups/` and note its structure and proportions.
2. Implement the Avalonia layout.
3. Capture the result: `scripts/screenshot.sh screenshots/<screen>.png`.
4. Compare the capture with the mockup — rail width, row heights, density, spacing,
   typography, borders, colours, selection, sparklines, charts, panels.
5. Fix and repeat until the match is strong.

Also check dark theme, window resizing, HiDPI, loading/empty/error states, keyboard navigation,
focus, and hover/selected/disabled states.

## Adding a dependency

Do not add a NuGet package without checking, and recording in `docs/DEPENDENCIES.md`:

- latest stable version and active maintenance;
- licence compatibility;
- .NET 10 support;
- Windows / macOS / Linux support;
- binary size impact and native dependencies.

A paid component may never be required for an essential feature.

## Tests

- Domain and Application: fast, no infrastructure, manual fakes preferred over mocking
  frameworks when the fake is simple.
- Parsers: fixture-driven, covering several distributions and malformed input.
- Persistence: run against a real temporary SQLite database, never a mock.
- Presentation: Avalonia headless tests for behaviour, not pixel-perfect rendering.
- Integration tests that need Docker or the network live in `tests/HostDeck.IntegrationTests`
  and must be skippable when those are unavailable.

## Pull requests

Keep them focused. Explain what changed and why, note any architectural decision, and update
`docs/` when the architecture moves. Large unrequested refactors will be asked to be split out.

## Reporting security issues

Do not open a public issue. See [`SECURITY.md`](SECURITY.md).
