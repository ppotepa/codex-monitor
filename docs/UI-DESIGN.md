# UI design — Avalonia 11

All current desktop presentation is declared once in `src/Desktop/UI/`.
The same layout runs on Windows, Linux and macOS. Avalonia scales logical
pixels according to the target platform; views use Grid, StackPanel,
ItemsControl, ScrollViewer and binding rather than fixed WinForms coordinates.

## AXAML views

- `Views/DashboardWindow.axaml`: shared 420×654 dashboard window with
  header, status message, Limits and Tokens tabs.
- `Views/LimitsView.axaml`: responsive primary usage ring, remaining quota,
  reset text, additional windows, 24-hour history and refresh/repair actions.
- `Views/TokensView.axaml`: latest daily total, lifetime/peak values and
  seven-day relative activity bars.
- `Views/SettingsWindow.axaml`: alert thresholds, independent refresh
  intervals, auto-refresh and startup preferences.
- `Views/AgentStatusWindow.axaml`: detected CLIs, active adapter support
  and executable paths.
- `Styles/Palette.axaml`: shared dark styling.

`DashboardViewModel` projects `DashboardState` into bindable properties.
An OS switch never requires rewriting these AXAML files. `UsageRing` and
`UsageHistoryChart` are framework-native custom controls with shared
Avalonia rendering. Missing readings are gaps, never synthetic zeroes.

The AXAML layout has a scroll container for short displays and the Tokens
tab is hidden if the active provider does not support it.

## Interaction

- User actions are exposed as commands; `DesktopHost` dispatches them to
  `UsageCoordinator`.
- Active provider changes update the same Window and ViewModel; no second
  presentation tree exists.
- Persistence, CLI errors and transient refresh state can be shown together.
- Settings update only after Save; failed saves surface an error and restore
  the previous in-memory values.
- A native `TrayIcon` and `NativeMenu` are shared across desktop systems.
  Native startup/notification implementations differ through injected OS
  interfaces.

## Target-platform caveats

Windows and macOS attempt to start hidden in the tray. Linux opens the
dashboard at startup to avoid an unopenable window on Wayland environments
with no visible StatusNotifierItem. Window placement is managed by the
compositor where necessary.

The screenshots under `docs/images/` show this AXAML version with synthetic
data. The `tools/Preview` renderer can regenerate them. Native visual and
keyboard checks still require builds on the three target platforms.

No public release should claim UI parity before those checks pass.
