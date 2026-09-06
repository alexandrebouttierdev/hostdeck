# HostDeck

HostDeck is a native desktop application for supervising Linux VPS, bare-metal hosts and Docker workloads. It is built with **Go** and **Fyne**, stores data locally in **SQLite**, collects metrics over **SSH** (direct or jump host), and keeps secrets outside the database.

Official repository: https://github.com/alexandrebouttierdev/hostdeck

## Features (V1)

- Compact vertical navigation rail and dense monitoring UI (French labels)
- Fleet overview with KPIs, charts and recent incidents
- Infrastructure host table with sparklines
- Host details and live metric history
- Incident list with acknowledge / resolve
- Alert rules browser
- Reports, topology grouping and settings
- Demo seed data for local exploration without real servers
- SSH collectors and Linux `/proc` parsers
- Pure-Go SQLite persistence (`modernc.org/sqlite`)

## Requirements

- Go 1.22+ (toolchain may auto-download a newer version)
- Linux/macOS/Windows desktop with Fyne system dependencies
- On Debian/Ubuntu: `libgl1-mesa-dev xorg-dev libxcursor-dev libxrandr-dev libxinerama-dev libxi-dev libxxf86vm-dev`

## Quick start

```bash
go mod tidy
go run ./cmd/hostdeck -seed=true
```

Optional flags:

```bash
go run ./cmd/hostdeck -db /path/to/hostdeck.db -seed=true
```

Default database path: `~/.local/share/hostdeck/hostdeck.db`

## Architecture

```text
cmd/hostdeck
internal/
  domain/           # pure business types
  application/      # use cases, DTOs, ports
  infrastructure/   # SQLite, SSH, credentials, demo seed
  presentation/fyne # theme, charts, screens, shell
  bootstrap/        # wiring
```

Dependency flow: Fyne UI → application services → ports → infrastructure.

## Tests

```bash
go test ./...
go test -race ./internal/infrastructure/ssh/... ./internal/application/...
```

## Visual references

Validated mockups live in `mockups/`. They are the visual source of truth for the Fyne UI (compact rail, dense tables, dark monitoring aesthetic).

## License

See repository license file when published.
