# HostDeck — specification package

This repository holds the complete HostDeck V1 specification for **C# / .NET 10 + Avalonia**,
together with the nine validated mockups.

## Contents

- `PROMPT_HOSTDECK.md` — full product and technical specification
- `AGENTS.md` — binding rules for contributors and agents
- `mockups/` — nine HostDeck screens (visual source of truth, 1672×941)
- `MOCKUPS_CONTACT_SHEET.png` — overview of all mockups
- `references/netdata_charts_reference.webp` — chart visual language only
- `docs/` — architecture and technical notes

## Recommended workflow

1. Read `PROMPT_HOSTDECK.md` and `AGENTS.md`
2. Open every mockup before changing UI
3. Prefer Context7 (or official docs) for non-trivial APIs
4. Compare each screen to its mockup via `scripts/screenshot.sh`

The mockups use a **compact vertical rail**, not a wide web-style sidebar.
