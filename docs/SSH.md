# SSH

Connections are created through `SSHConnectionFactory`.

Supported modes:

- direct SSH
- jump / bastion tunneling

Security notes:

- host key verification with TOFU persistence in SQLite
- secrets loaded from credential store
- command execution is explicit and context-aware
- timeouts / keepalives configured in the factory

Linux metric collection reads `/proc/stat`, `/proc/meminfo`, `/proc/loadavg`, `/proc/net/dev`, `/proc/uptime`, `/etc/os-release` and `df -B1` through dedicated pure parsers.
