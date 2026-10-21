#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
rid=osx-arm64
if (( $# > 0 )); then rid="$1"; fi
case "$rid" in
  osx-arm64|osx-x64) ;;
  *)
    echo "Usage: bash scripts/package-macos.sh osx-arm64|osx-x64" >&2
    exit 2
    ;;
esac

command -v dotnet >/dev/null || { echo ".NET 10 SDK is required" >&2; exit 1; }
if [[ "$(uname -s)" != Darwin ]]; then
  echo "Build the macOS application bundle on macOS" >&2
  exit 1
fi

output="$root/build/$rid"
bundle="$root/dist/$rid/Codex Monitor.app"
mkdir -p "$output"
dotnet publish "$root/src/Desktop/Host/CodexMonitor.Desktop.csproj" \
  --configuration Release --runtime "$rid" --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  --output "$output"

rm -rf "$bundle"
mkdir -p "$bundle/Contents/MacOS" "$bundle/Contents/Resources"
cp -a "$output/." "$bundle/Contents/MacOS/"
chmod 755 "$bundle/Contents/MacOS/CodexMonitor"
cp "$root/packaging/macos/Info.plist" "$bundle/Contents/Info.plist"
cp "$root/assets/tray.png" "$bundle/Contents/Resources/tray.png"

# Ad-hoc signing helps local execution; it is NOT Developer ID signing or notarization.
if command -v codesign >/dev/null; then
  codesign --force --deep --sign - "$bundle"
fi
echo "Created: $bundle"
echo "This preview bundle is not notarized for public distribution."
