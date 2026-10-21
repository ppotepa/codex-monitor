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
expose a system tray. It shows limits and reset times, recent usage history, daily token bars,
refresh/repair actions and settings (including login startup). The tray
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
as appropriate. The publish command is a **portable executable build**, not an installer.
To generate an experimental Debian package on Linux or app bundle on macOS:

```sh
# On Debian/Ubuntu with dpkg-deb and .NET 10 SDK:
bash scripts/package-deb.sh amd64       # or arm64

# On macOS with the .NET 10 SDK:
bash scripts/package-macos.sh osx-arm64  # or osx-x64
```

These packaging scripts publish **self-contained** applications. The macOS
bundle is only ad-hoc signed, not Developer ID signed or notarized. Installers
and graphical integration must be tested on real target machines.

## Platform behavior

- Linux uses `XDG_DATA_HOME/codex-monitor` (default
  `~/.local/share/codex-monitor`). Start at login writes an XDG
  `autostart/codex-monitor.desktop` entry if the app is a published binary.
- macOS uses `~/Library/Application Support/CodexMonitor`. Start at login
  writes `~/Library/LaunchAgents/io.github.ppotepa.codex-monitor.plist`.
  It takes effect at login; a signed `.app` bundle is not yet built.
- Unix shell command launching and PATH search are shared by both systems,
  while login startup and storage paths are separate implementations.
- Closing the window on Linux exits the app, avoiding invisible background processes when tray support is missing. On macOS, closing hides it only while the menu-bar tray is installed.
- On Linux, Avalonia's tray requires a desktop supporting
  StatusNotifierItem/AppIndicator. GNOME may require an AppIndicator extension;
  Wayland window positioning is controlled by the compositor.
- On macOS, clicking the tray icon opens the menu. Use its "Show usage" item:
  Avalonia does not guarantee a separate primary-click callback.
- Native notifications use Linux `notify-send` when installed, or macOS `osascript`.
  If permissions or desktop services are unavailable, alerts remain visible in-app.
- The Unix dashboard includes a 24-hour usage sparkline and daily token bars,
  implemented using Avalonia. The Windows UI has more polished native charts.
  Both use the same shared data models.

## What still needs validation

Build on real Linux and macOS machines; validate the generated `.deb`
and `.app`, tray integration, window lifecycle, cancellation and CLI installation.
Do not announce release support until these checks pass.
