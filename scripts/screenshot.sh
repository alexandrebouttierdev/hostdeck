#!/usr/bin/env bash
#
# Captures a real screenshot of HostDeck for visual comparison against the mockups.
#
# The window is rendered inside a private Xvfb display sized exactly like the mockups
# (1672x941), so the capture and the reference image are pixel-comparable and the
# developer's own desktop is never touched.
#
# Usage:
#   scripts/screenshot.sh <output.png> [seconds-to-wait] [-- <extra args passed to hostdeck>]
#
# Example:
#   scripts/screenshot.sh screenshots/02_infrastructure.png 12
#
set -euo pipefail

readonly MOCKUP_WIDTH=1672
readonly MOCKUP_HEIGHT=941

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

output="${1:?usage: scripts/screenshot.sh <output.png> [wait-seconds] [-- app args]}"
wait_seconds="${2:-12}"
shift $(( $# < 2 ? $# : 2 ))
if [[ "${1:-}" == "--" ]]; then shift; fi
app_args=("$@")

for tool in xvfb-run magick dotnet; do
  command -v "$tool" >/dev/null 2>&1 || { echo "error: '$tool' is required but not installed" >&2; exit 1; }
done

mkdir -p "$(dirname "$output")"

echo "==> Building HostDeck.Desktop (Release)"
dotnet build "$repo_root/src/HostDeck.Desktop/HostDeck.Desktop.csproj" -c Release --nologo -v q

binary="$repo_root/src/HostDeck.Desktop/bin/Release/net10.0/hostdeck"
[[ -x "$binary" ]] || { echo "error: binary not found at $binary" >&2; exit 1; }

echo "==> Rendering in a private ${MOCKUP_WIDTH}x${MOCKUP_HEIGHT} Xvfb display"
# LIBGL_ALWAYS_SOFTWARE keeps rendering on Mesa's software rasterizer, which is what CI has.
# DISPLAY is unset so the app can never open on the developer's real desktop.
env -u DISPLAY LIBGL_ALWAYS_SOFTWARE=1 \
  xvfb-run -a -s "-screen 0 ${MOCKUP_WIDTH}x${MOCKUP_HEIGHT}x24" \
  bash -c '
    set -euo pipefail
    "$1" "${@:4}" > "$2.log" 2>&1 &
    app_pid=$!
    sleep "$3"
    if ! kill -0 "$app_pid" 2>/dev/null; then
      echo "error: HostDeck exited before the screenshot could be taken; log follows:" >&2
      cat "$2.log" >&2
      exit 1
    fi
    magick "x:root" "$2"

    # HostDeck traite SIGTERM et se ferme proprement. SIGKILL sert uniquement de dernier
    # recours : un blocage à la fermeture ne doit jamais empêcher la capture.
    kill -TERM "$app_pid" 2>/dev/null || true
    for _ in $(seq 1 20); do
      kill -0 "$app_pid" 2>/dev/null || break
      sleep 0.25
    done
    if kill -0 "$app_pid" 2>/dev/null; then
      echo "warning: HostDeck ignored SIGTERM, forcing SIGKILL" >&2
      kill -KILL "$app_pid" 2>/dev/null || true
    fi
    wait "$app_pid" 2>/dev/null || true
  ' _ "$binary" "$output" "$wait_seconds" "${app_args[@]}"

echo "==> Wrote $output ($(magick identify -format '%wx%h' "$output"))"
