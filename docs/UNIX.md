# Linux and macOS preview

The repository contains an **experimental**, code-only Avalonia desktop host
in `src/CodexMonitor.Desktop.Unix`. It reuses the exact same `UsageCoordinator`,
`CodexPlugin`, settings schema and dashboard state as the Windows WinForms host.
Linux/macOS build and runtime verification have not yet been completed.

## Run from source

On a Linux or macOS desktop with the .NET 10 SDK:

```sh
dotnet run --project src/CodexMonitor.Desktop.Unix/CodexMonitor.Desktop.Unix.csproj
```

A dashboard window opens on startup, even when the desktop environment does not
expose a system tray. It shows limits and reset times, a basic list of other
usage windows, token activity, refresh/repair actions and settings. The tray
menu includes "Show usage", refresh, agent discovery, snooze and startup.

Codex CLI should already be authenticated for the current OS account.
Reading limits requires `codex-cli-usage` or `clanker-usage`; repairing
those tools requires `uv` on `PATH` (or `~/.local/bin`).
Reading tokens requires the `codex` CLI with app-server support.

## Publish

From the repository root:

```sh
bash scripts/publish-unix.sh linux-x64
bash scripts/publish-unix.sh linux-arm64
bash scripts/publish-unix.sh osx-x64
bash scripts/publish-unix.sh osx-arm64
```

Run the executable `dist/<rid>/CodexMonitor.Desktop.Unix` on its matching
OS/architecture. The runtime is not bundled; install .NET 10 Desktop/Runtime
as appropriate. The publish command is a **portable executable build**, not
a `.app`, `.deb`, `.rpm` or AppImage installer.

## Platform behavior

- Linux uses `XDG_DATA_HOME/codex-monitor` (default
  `~/.local/share/codex-monitor`). Start at login writes an XDG
  `autostart/codex-monitor.desktop` entry if the app is a published binary.
- macOS uses `~/Library/Application Support/CodexMonitor`. Start at login
  writes `~/Library/LaunchAgents/io.github.ppotepa.codex-monitor.plist`.
  It takes effect at login; a signed `.app` bundle is not yet built.
- Unix shell command launching and PATH search are shared by both systems,
  while login startup and storage paths are separate implementations.
- On Linux, Avalonia's tray requires a desktop supporting
  StatusNotifierItem/AppIndicator. GNOME may require an AppIndicator extension;
  Wayland window positioning is controlled by the compositor.
- On macOS, clicking the tray icon opens the menu. Use its "Show usage" item:
  Avalonia does not guarantee a separate primary-click callback.
- Native OS notification balloons are not implemented in this preview.
  Alerts are shown in-app. The Windows host keeps its existing native alerts.
- The Unix dashboard uses a compact text-based history/token summary, not the
  Windows WinForms charts. The underlying history data remains available
  through the shared core.

## What still needs validation

Build on real Linux and macOS machines, verify tray integration on target
desktop environments, validate cancellation and CLI installation, and package
for normal installation/uninstallation. Do not announce release support until
these checks pass.
