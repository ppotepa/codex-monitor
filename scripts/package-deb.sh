#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
arch=amd64
if (( $# > 0 )); then arch="$1"; fi

case "$arch" in
  amd64) rid=linux-x64 ;;
  arm64) rid=linux-arm64 ;;
  *)
    echo "Usage: bash scripts/package-deb.sh amd64|arm64" >&2
    exit 2
    ;;
esac

command -v dotnet >/dev/null || { echo ".NET 10 SDK is required" >&2; exit 1; }
command -v dpkg-deb >/dev/null || { echo "Install dpkg-deb to build a Debian package" >&2; exit 1; }

version=0.5.3
staging="$(mktemp -d)"
trap 'rm -rf "$staging"' EXIT
appdir="$staging/usr/lib/codex-monitor"
mkdir -p "$appdir" "$staging/usr/bin" "$staging/usr/share/applications" \
  "$staging/usr/share/icons/hicolor/32x32/apps" "$staging/DEBIAN"

rm -rf "$root/build/linux-$arch"
dotnet publish "$root/src/Desktop/Host/CodexMonitor.Desktop.csproj" \
  --configuration Release --runtime "$rid" --self-contained true \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  --output "$root/build/linux-$arch"

cp -a "$root/build/linux-$arch/." "$appdir/"
chmod 755 "$appdir/CodexMonitor"
ln -s ../lib/codex-monitor/CodexMonitor "$staging/usr/bin/codex-monitor"
install -m 644 "$root/packaging/linux/codex-monitor.desktop" \
  "$staging/usr/share/applications/codex-monitor.desktop"
install -m 644 "$root/assets/tray.png" \
  "$staging/usr/share/icons/hicolor/32x32/apps/codex-monitor.png"

cat >"$staging/DEBIAN/control" <<EOF
Package: codex-monitor
Version: $version
Section: utils
Priority: optional
Architecture: $arch
Maintainer: Codex Monitor maintainers
Depends: libc6, libfontconfig1, libx11-6
Description: Codex usage monitor for Linux desktops
 Experimental Linux desktop application for Codex usage limits
 and token activity, with a graphical dashboard and system tray.
EOF
mkdir -p "$root/dist"
outfile="$(printf '%s/dist/codex-monitor_%s_%s.deb' "$root" "$version" "$arch")"
dpkg-deb --root-owner-group --build "$staging" "$outfile"
echo "Created: $outfile"
