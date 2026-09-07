# Security Policy

## Reporting a vulnerability

Please do **not** open a public issue for a security problem.

Report it privately through
[GitHub's private vulnerability reporting](https://github.com/alexandrebouttierdev/hostdeck/security/advisories/new)
on this repository. Include what you found, how to reproduce it, and the impact you believe it
has. You will get an acknowledgement, and we will keep you informed until the issue is resolved.

Please do not disclose the issue publicly until a fix is available.

## Supported versions

HostDeck is pre-1.0. Only the latest commit on the default branch is supported.

## Security model

HostDeck holds credentials for the servers it supervises, so it is a high-value target on a
developer or operator workstation. The design rules below are enforced in the codebase; see
[`docs/SECURITY.md`](docs/SECURITY.md) for the full threat model.

### Credentials

- Private keys, passwords, passphrases and API tokens are stored in the operating system's
  credential store (macOS Keychain, Windows Credential Manager, Linux Secret Service).
- The local database stores only a reference to a credential, never the secret itself.
- Secrets are never written to logs, exception messages, telemetry or crash reports.
- Sensitive form fields are never pre-filled with a real secret read back from storage.

### SSH

- Host key verification is always on and can never be turned off "for convenience".
- Approved fingerprints are stored locally. A first connection, a known host key and a
  **changed** host key are three distinct outcomes; a changed host key is treated as a security
  event and requires an explicit decision.
- Commands sent over SSH are fixed, controlled strings. They are never assembled from arbitrary
  user-supplied fragments, and no user input reaches a shell interpolation.

### Docker

- Remote Docker access goes through the Docker Engine API over TLS, or through a controlled SSH
  tunnel.
- The Docker socket is never exposed publicly and TLS verification is never bypassed.

### Data

- All SQL is parameterised.
- Input is validated at the application boundary before it reaches the domain or the database.
- The local database and log files are written under the user's own profile directory with
  default user-only permissions.

### Reminder

Access to a HostDeck installation is effectively access to the fleet it supervises. Protect the
workstation accordingly.
