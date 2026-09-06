# Monitoring

Monitoring is split into:

1. parsers (pure, fixture-tested)
2. SSH collector
3. metrics repository / history queries
4. fleet coordinator skeleton (`StartFleet` / `StopFleet`)

History windows are requested via DTO ranges (`15m`, `1h`, `6h`, …). Downsampling and retention are handled at persistence level.
