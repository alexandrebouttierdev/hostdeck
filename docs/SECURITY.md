# Security

- Private keys / passwords / passphrases are stored in the OS keyring when enabled, otherwise an in-memory store for demos/tests.
- SQLite stores only credential references.
- SSH host keys are verified (TOFU) and persisted.
- Logs must never include secrets.
- No shell interpolation of remote commands beyond controlled collector scripts.
