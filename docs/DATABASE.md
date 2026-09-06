# Database

HostDeck uses SQLite via `modernc.org/sqlite` (pure Go, no CGO).

Default file: `~/.local/share/hostdeck/hostdeck.db`

## Schema highlights

- `servers`, `server_tags`
- `metric_samples` (+ time indexes)
- `incidents`, `alert_rules`
- `settings`
- `ssh_host_keys` (TOFU fingerprints)

Migrations live under `internal/infrastructure/persistence` and are applied at startup.
