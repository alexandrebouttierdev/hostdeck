# Packaging

HostDeck is a desktop app (`HostDeck.Desktop`). Publish self-contained single-file builds with:

```bash
scripts/publish.sh linux-x64
scripts/publish.sh win-x64
scripts/publish.sh osx-x64
scripts/publish.sh osx-arm64
```

Outputs land under `artifacts/publish/<rid>/` (git-ignored). The entry assembly is named
`hostdeck` / `hostdeck.exe`.

## Runtime notes

- Credentials use the OS store (Latchkey). A packaged build still needs a working keyring /
  Credential Manager / Keychain on the target machine.
- SQLite database defaults to the application data directory resolved at runtime.
- Linux screenshot validation additionally needs `xvfb` and ImageMagick (see `scripts/screenshot.sh`).

## CI

The quality gate in `.github/workflows/ci.yml` restores, formats, builds and tests on Linux,
Windows and macOS. Publishing installers (deb/msi/dmg) is left for a later packaging pass;
`scripts/publish.sh` is the reproducible binary baseline for V1.
