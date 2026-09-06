# Agent guidelines for HostDeck

1. Communicate with humans in **French**.
2. Write code identifiers in **English**.
3. Keep all end-user UI strings in **French**.
4. Comments may be French when they add real context.
5. Technical documentation is written in **English**.
6. Prefer Context7 (or official docs) before inventing Fyne/SSH/Docker APIs.
7. UI must be native Fyne only — no Electron/WebView/HTML UI.
8. Domain must not import Fyne, SQLite, SSH or Docker clients.
9. Keep DTOs separate from domain entities and persistence records.
10. Presentation must not open SQL, SSH or Docker APIs directly.
11. Goroutines need owners, cancellation and clean shutdown.
12. Run `go test` / race detector on concurrency-sensitive packages.
13. Secrets stay in the OS credential store, never plaintext in SQLite or logs.
14. Mockups in `mockups/` are the visual source of truth; keep the compact vertical rail.
15. Charts should feel Netdata-like: dense, dark, technical.
16. Do not add heavy dependencies without checking license, CGO and cross-platform impact.
17. Prefer small focused packages over large god files.
18. Before claiming done: `go fmt`, `go vet`, `go test ./...`.
