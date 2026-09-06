# Performance

- Prefer virtualized Fyne tables for host lists
- Keep sparkline datasets short
- Bound metric history queries with limit + time range
- Avoid full-window refresh; update targeted widgets
- Fleet coordinator must use bounded concurrency and cancellation
