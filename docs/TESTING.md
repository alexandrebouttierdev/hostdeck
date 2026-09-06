# Testing

Primary suites:

- parser fixtures under `tests/fixtures`
- SQLite repository integration tests with temp DB
- application service unit tests (servers, incidents)
- bootstrap wiring + demo seed test
- SSH factory/error unit tests

Commands:

```bash
go test ./...
go test -race ./internal/infrastructure/ssh/... ./internal/application/servers/... ./internal/application/incidents/...
```
