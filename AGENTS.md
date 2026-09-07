# AGENTS.md — rules for anyone (human or agent) working on HostDeck

These rules are binding. They come from the project specification (`PROMPT_HOSTDECK.md`).
When a rule conflicts with a habit, a framework default, or a convenient shortcut, the rule wins.

## Languages

1. **Talk to the maintainer in French.** Plans, analyses, questions, decisions, reviews and
   reports are written in French.
2. **Write code in English.** Files, folders, namespaces, types, members, variables, DTOs,
   database tables and columns, migrations and internal error identifiers are all English.
3. **Write the user interface in French.** Every string a user can read on screen is French.
4. **Write code comments in French**, and only where they carry real context that the code
   itself cannot express.
5. **Write repository documentation in English.** README, `docs/`, CONTRIBUTING, SECURITY.

## Dependencies and APIs

6. **Context7 is mandatory** before any non-trivial integration. Never rely on the model's
   memory for an API surface, and never invent one. Documentation priority: Context7, then
   official docs, then the official repository, then the package source.
7. **.NET 10** is the target framework. **Avalonia 12** is the UI framework.
8. **Avalonia is the only UI technology.** No WebView, no embedded browser, no HTML/CSS/JS,
   no Electron, Tauri, Blazor Hybrid or any web front-end layer.
9. **No new NuGet dependency without vetting it**: latest stable version, active maintenance,
   compatible licence, .NET 10 support, Windows/macOS/Linux support, binary size impact and
   native dependencies. Record the outcome in `docs/DEPENDENCIES.md`.
10. **No paid Avalonia component** may be required for an essential feature of this
    open-source project.

## Architecture

11. **The Domain depends on nothing.** Not Avalonia, not EF Core, not SSH.NET, not Docker,
    not the credential store, not the notification stack.
12. **DTOs are separate from Domain entities.** Conversions are explicit and tested.
13. **Persistence records are separate from Domain entities.** The SQLite/EF model never
    leaks upward.
14. **No SQL, SSH or Docker in the Presentation layer.** Presentation talks to Application
    use cases and to nothing else.
15. **`HostDeck.Desktop` is the composition root** and the only project that knows both
    Infrastructure and Presentation.

## Concurrency and lifecycle

16. **Propagate `CancellationToken`** through every I/O and long-running operation.
17. **Every background task has an owner**, a cancellation token, a bounded retry policy,
    error handling, logging and a clean shutdown. No fire-and-forget `Task.Run`.
18. **Never block the UI thread.** No `.Result`, no `.Wait()`, no synchronous I/O on the
    dispatcher. `async void` is allowed only for genuine UI event handlers.
19. **Dispose what you own**: SSH clients, streams, Docker response streams, database
    connections and readers, timers, `CancellationTokenSource`, subscriptions.
20. **Mutate Avalonia controls only from the UI thread** (§36 of the specification).

## Security

21. **Secrets never go into SQLite.** Private keys, passwords, passphrases and tokens live in
    the OS credential store; the database holds only a reference.
22. **Secrets never go into logs**, exception messages or telemetry.
23. **SSH host key verification is never disabled.** A changed host key is a security event.
24. **SQL is always parameterised**, and SSH commands are fixed strings, never assembled from
    user-provided fragments.

## Visual fidelity

25. **The nine mockups in `mockups/` are the visual source of truth.** They are 1672x941 and
    outrank any framework default or personal preference.
26. **The compact vertical rail is mandatory.** Never replace it with a wide web-style sidebar,
    dense tables with cards, a detail panel with a separate page, or sparklines with bars.
27. **Charts follow the Netdata visual language**: dense, dark, thin grid, small labels, data
    first (see `references/netdata_charts_reference.webp`).
28. **Visual inspection is mandatory.** Run `scripts/screenshot.sh`, compare the capture with
    the corresponding mockup, and iterate. A screen that merely compiles is not done.

## Quality gate

29. **The build is warning-free.** `TreatWarningsAsErrors` is on; suppressions need a written
    justification next to them.
30. **Run the full gate before calling a task done**, and keep documentation in step with any
    architectural change:

```bash
dotnet restore
dotnet format --verify-no-changes
dotnet build -c Release --no-restore
dotnet test -c Release --no-build
```

## Scope ruling (V1)

The mockups intentionally show elements that V1 does not collect: per-host service inventories,
database-query and HTTP-error charts, a HostDeck agent, Windows hosts, user/role management and
external notification channels. The agreed ruling is:

> **Reproduce the mockup layout faithfully, and render out-of-scope panels as an explicit empty
> state.** Never fabricate data to fill a panel. The architecture must leave room for these
> features (§56) without implementing them now.

Collection in V1 is Linux-only, over direct SSH or through a jump host, with no agent installed
on the monitored machine.
