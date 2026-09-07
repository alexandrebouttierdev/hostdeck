# UI design

## The mockups are the source of truth

The nine images in `mockups/` define the interface. They outrank Avalonia defaults, personal
preference and any automatic decision. `MOCKUPS_CONTACT_SHEET.png` shows all nine at once.

| File | Screen |
|---|---|
| `01_overview.png` | Overview / global infrastructure state |
| `02_infrastructure_hosts.png` | Infrastructure / dense host list |
| `03_host_details.png` | Full host detail and metrics |
| `04_active_incidents.png` | Active incidents + diagnostic panel |
| `05_alert_rules.png` | Alert rules + rule editor |
| `06_reports.png` | Reports, SLA, availability, capacity |
| `07_topology.png` | Infrastructure topology map |
| `08_live_data.png` | Live data / real-time metrics |
| `09_settings.png` | Global settings |

`references/netdata_charts_reference.webp` is a reference for the **chart visual language only**
— density, time series, stacked areas, thin grids, compact legends, technical colours. It does
not define anything else in the interface, and is not to be copied pixel for pixel.

## Non-negotiables

- A **compact vertical rail**, never a wide web-style sidebar.
- **Dense tables**, never a grid of large cards.
- A **detail panel** beside the table, never a separate page.
- **Sparklines** inside table rows, never generic bars.
- **Technical charts**, never simplified ones.
- **Desktop density**: small padding, thin borders, discreet selection, small labels.

The result must read as a professional systems supervision tool — not a SaaS dashboard, an admin
panel, a landing page, or a scaled-up mobile interface.

## Layout

```text
+------+--------------------------------------------------+
| Rail | Topbar                                           |
|      +--------------------------------------------------+
|      | Secondary nav | Main content                     |
|      |               |                                  |
+------+---------------+----------------------------------+
| Status bar                                              |
+---------------------------------------------------------+
```

The detail panel opens on row selection, is resizable from the side the mockup shows, honours a
minimum and maximum width, and is not rendered at all when nothing is selected.

## Design system

The theme is centralised under `src/HostDeck.Presentation/Themes/`. Colours, typography,
spacing, radii, border thickness, row heights, icon sizes, rail width and panel widths are
tokens. Views never hard-code a colour, and no magic size is repeated across files.

Token families: surfaces (`SurfaceBackground`, `SurfaceRail`, `SurfacePanel`, `SurfaceRaised`),
borders (`BorderSubtle`), text (`TextPrimary`, `TextSecondary`, `TextMuted`), interaction
(`Accent`, `Selection`), status (`StatusOnline`, `StatusOffline`, `StatusWarning`) and severity
(`SeverityInformation`, `SeverityWarning`, `SeverityAverage`, `SeverityHigh`,
`SeverityCritical`).

## Charts

Charts are custom controls that inherit from `Control` and override `Render(DrawingContext)`.
`AffectsRender` drives invalidation, so redraws happen on real changes rather than on a timer.

Performance rules: no allocation inside `Render`, reuse brushes, pens and geometries, cache
computed scales and geometry, bound every dataset, downsample before drawing, reduce label
count with available width, and never run a 60 fps loop for data that arrives once a second.

Table sparklines use an even lighter renderer: no axes, no labels, short datasets, no per-point
view model, and no per-row timer.

## Visual validation

Validation is mandatory and is not satisfied by the code compiling.

The mockups are exactly **1672x941**. `scripts/screenshot.sh` renders the application inside a
private Xvfb display of exactly that size, so a capture and its mockup line up one to one:

```bash
scripts/screenshot.sh screenshots/02_infrastructure.png
```

The script never touches the developer's own display: `DISPLAY` is unset and rendering uses
Mesa's software rasteriser, which is also what CI has.

For each screen: capture, compare against the mockup, then correct proportions, rail width, row
heights, density, spacing, typography, borders, colours, selection, tables, sparklines, charts
and panels — and repeat until the match is strong.

Also verify dark theme, resizing, HiDPI, loading/empty/error states, keyboard navigation, focus,
and hover/selected/disabled states.

## V1 scope inside the mockups

The mockups show data V1 does not collect: per-host service inventories, database-query and
HTTP-error charts, a HostDeck agent, Windows hosts, user and role management, and external
notification channels.

The ruling is to **keep the layout faithful and render those panels as an explicit empty state**
— never to fabricate data, and never to delete the panel and redistribute the space. The
architecture leaves room for these features without implementing them now.
