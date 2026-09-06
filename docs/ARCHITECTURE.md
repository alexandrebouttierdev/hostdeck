# Architecture

HostDeck follows a modular hexagonal style:

- `internal/domain` — entities and value objects (servers, metrics, incidents, docker, settings)
- `internal/application` — use cases, DTOs and port interfaces
- `internal/infrastructure` — SQLite, SSH factory/collector/parsers, credentials, demo seed, notifications
- `internal/presentation/fyne` — theme, custom charts, shell, screens
- `internal/bootstrap` — composition root

## Runtime flow

1. `cmd/hostdeck` opens bootstrap services (DB migrate + optional demo seed).
2. Fyne shell renders screens that call application services.
3. Fleet monitoring coordinator (skeleton) can start/stop collection loops.
4. SSH collectors execute remote commands, parsers convert `/proc` output into metric samples, repositories persist them.

## UI principles

- Compact left rail (icons only)
- Dense tables and panels
- Dark monitoring palette
- Custom canvas sparklines / time-series charts
- French visible copy
