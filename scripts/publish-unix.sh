#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
rid="${1:-}"
case "$rid" in
  linux-x64|linux-arm64|osx-x64|osx-arm64) ;;
  *)
    echo "Usage: ./scripts/publish-unix.sh linux-x64|linux-arm64|osx-x64|osx-arm64" >&2
    exit 2
    ;;
esac

dotnet publish "$root/src/CodexMonitor.Desktop.Unix/CodexMonitor.Desktop.Unix.csproj" \
  --configuration Release \
  --runtime "$rid" \
  --self-contained false \
  -p:PublishSingleFile=true \
  -p:UseAppHost=true \
  --output "$root/dist/$rid"

echo "Published: $root/dist/$rid/CodexMonitor.Desktop.Unix"
