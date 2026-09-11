#!/usr/bin/env bash
# Publie HostDeck en self-contained pour un RID donné.
# Usage: scripts/publish.sh [rid] [output-dir]
# Exemples:
#   scripts/publish.sh linux-x64
#   scripts/publish.sh win-x64 artifacts/win
#   scripts/publish.sh osx-arm64

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RID="${1:-linux-x64}"
OUT="${2:-$ROOT/artifacts/publish/$RID}"

mkdir -p "$OUT"

dotnet publish "$ROOT/src/HostDeck.Desktop/HostDeck.Desktop.csproj" \
  -c Release \
  -r "$RID" \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o "$OUT"

echo "Published HostDeck for $RID → $OUT"
ls -lah "$OUT"
