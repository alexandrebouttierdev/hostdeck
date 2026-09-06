# Security model and threat model

`SECURITY.md` at the repository root covers how to report a vulnerability. This document
describes what HostDeck defends against and how.

## What HostDeck is worth to an attacker

HostDeck stores the means to reach every server it supervises: SSH credentials, jump host
routes, Docker endpoints and the fleet's topology. Compromising a HostDeck installation is
close to compromising the fleet. The application is therefore treated as a high-value target on
the operator's workstation, not as a read-only dashboard.

## Assets

| Asset | Where it lives | Protection |
|---|---|---|
| SSH private keys, passphrases, passwords | OS credential store | Never in the database, never in logs |
| Approved SSH host key fingerprints | Local database | Change detection is a security event |
| Server inventory and topology | Local database | User-profile file permissions |
| Metric history | Local database | Not sensitive on its own, but reveals infrastructure shape |
| Docker endpoint configuration | Local database + credential store | TLS material in the credential store |

## Trust boundaries

```text
Operator workstation
  HostDeck process
    |  OS credential store API      <- secrets never leave it in plaintext at rest
    |  local database file          <- references only, no secrets
    |  SSH (direct or via bastion)  <- host key verified on every connection
    |  Docker Engine API / SSH tunnel
  Monitored hosts
```

The monitored hosts are **not** trusted. Everything they return — command output, Docker
metadata, container logs — is untrusted input and is parsed defensively.

## Threats and mitigations

### T1 — Credential theft from local storage

An attacker with read access to the user's files reads stored SSH keys.

*Mitigation:* secrets are held by the OS credential store (macOS Keychain, Windows Credential
Manager, Linux Secret Service). The database stores only an opaque reference. There is no
"fallback to plaintext" path: if the keychain is unavailable, the operation fails and says so.

### T2 — Credential leakage through logs or crash reports

*Mitigation:* secrets are never passed to a logger, never included in exception messages, and
never included in an error surfaced to the user. Log fields are an explicit allow-list
(server id, container id, operation, duration, result, retry count, exception type).

### T3 — Man-in-the-middle on SSH

*Mitigation:* host key verification is always on and cannot be disabled. Three outcomes are
distinguished: first connection (fingerprint presented for an explicit decision), known and
matching key (proceed), and **changed** key (refuse, and report it as a security event
requiring an explicit decision). Approved fingerprints are stored locally.

### T4 — Command injection over SSH

A malicious server name, tag or path is interpolated into a shell command.

*Mitigation:* the commands HostDeck runs are fixed, controlled strings. Nothing supplied by the
user is concatenated into a command line, and no shell interpolation of user data occurs.
Collection reads `/proc` and a small set of well-known utilities.

### T5 — SQL injection

*Mitigation:* all SQL is parameterised, including the hand-written time-series queries. Input is
validated at the application boundary before reaching the database.

### T6 — Hostile or malformed data from a monitored host

A compromised host returns enormous or malformed `/proc` output, or a container emits an endless
log stream.

*Mitigation:* parsers are total functions over untrusted text — unknown lines are skipped, bad
numbers are reported as format errors rather than throwing at an arbitrary depth, and no parser
trusts a field count. Command output, log buffers and in-memory histories are all bounded, and
log streaming is cancellable and back-pressured.

### T7 — Insecure Docker access

*Mitigation:* remote Docker goes over the Engine API with TLS, or through a controlled SSH
tunnel. The socket is never exposed publicly, and TLS verification is never bypassed — there is
no "temporary" certificate-validation switch anywhere in the codebase.

### T8 — Denial of service against the operator's own fleet

An aggressive polling configuration hammers production servers.

*Mitigation:* collection concurrency is bounded, intervals have sane minimums, failures back off
exponentially with jitter, and every operation has a timeout.

## Out of scope for V1

- Multi-user access control inside HostDeck. The mockups show user and role management; V1 is a
  single-operator desktop application and does not implement it.
- Encrypting the local metric database at rest. The database contains no secrets; the operating
  system's own disk encryption is the appropriate control.
- Defending against an attacker who already has code execution as the operator's user. At that
  point the OS credential store is the remaining boundary, and it is the OS's to hold.
