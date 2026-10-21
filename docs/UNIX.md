# Linux / macOS desktop

Windows, Linux and macOS now run the **same Avalonia 11 presentation** from
`src/CodexMonitor.UI`, hosted by `src/CodexMonitor.Desktop`.

The Unix module supplies only platform services: executable lookup,
process startup, data directory, autostart, notifications and window behavior.
Linux and macOS differ in autostart and storage, not dashboard layout.

## Development

```sh
dotnet build CodexMonitor.slnx -c Release
dotnet run --project src/CodexMonitor.Desktop/CodexMonitor.Desktop.csproj

bash scripts/publish-unix.sh linux-x64
bash scripts/publish-unix.sh linux-arm64
bash scripts/publish-unix.sh osx-x64
bash scripts/publish-unix.sh osx-arm64
```

Output binary: `dist/<rid>/CodexMonitor`. Framework-dependent publication
requires the matching .NET 10 runtime.

Experimental self-contained packaging scripts:

```sh
bash scripts/package-deb.sh amd64       # or arm64, on Linux
bash scripts/package-macos.sh osx-arm64  # or osx-x64, on macOS
```

The generated macOS bundle is not Developer ID signed or notarized.
Do not distribute public packages before platform validation.

## Behavior

- Linux data: `XDG_DATA_HOME/codex-monitor` (fallback:
  `~/.local/share/codex-monitor`). Login startup writes a freedesktop
  `.desktop` entry.
- macOS data: `~/Library/Application Support/CodexMonitor`;
  startup uses a LaunchAgent.
- Linux keeps a visible dashboard at startup and closing its window exits
  if no secure tray-close behavior can be guaranteed.
- macOS uses a menu bar icon, whose native menu includes `Show usage`.
- On Wayland, the compositor controls window positioning; desktop tray
  availability varies by shell (AppIndicator/SNI support).
- Linux notifications attempt `notify-send`; macOS notifications use
  `osascript`. Both have an in-window error/alert fallback.
- Both systems show the same usage ring, history and token bars defined in AXAML.

Build and smoke verification have **not** yet succeeded in the current
environment. Validate real GUI startup, tray, permissions and packaging.
