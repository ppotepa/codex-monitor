# UI design — Avalonia 11

All current desktop presentation is declared once in `src/Desktop/UI/`.
The same layout runs on Windows, Linux and macOS. Avalonia scales logical
pixels according to the target platform; views use Grid, StackPanel,
ItemsControl, ScrollViewer and binding rather than fixed WinForms coordinates.

## Compact Workspace Overview (October 2026)

The full Workspace is a separate window from the original 420 px tray Quick View.
It uses the same **Avalonia 11 Fluent Compact** theme and dark palette as
the rest of CodexMonitor: `#15191F` window, `#20262E` surface,
`#29323D` subtle border and `#63D9C6` accent. No ornamental
illustrations, neon gradients or login flow.

The 1120 × 740 window has a narrow icon-labelled navigation rail and a
small breadcrumb/provider/refresh bar. The Overview itself deliberately has
only **two attention centres**: (1) tracked local repositories with Git and
session evidence, and (2) observations that may require action. Above these,
a slim four-column factual strip presents the allowance, reset, observed
session IDs and number of saved projects. Below are a secondary planner
recommendation and an initially collapsed local activity feed.

Source files:
- `Views/WorkspaceWindow.axaml`: shell, navigation and existing tool tabs.
- `Views/WorkspaceOverviewView.axaml`: isolated, shared compact Overview UI.
- `ViewModels/WorkspaceViewModel.Overview.cs`: data projection and commands.
- `ViewModels/WorkspaceViewModel.cs`: existing tool commands and selected
  provider/project context; remains the shared ViewModel.

Never present mocked metrics in the running app. A blank or unavailable value
is shown as such. Git impact is per working tree, not attributed to an agent;
hook/notify session IDs represent **partial observation**, not proof of active
processes; stored App Server status is marked as reported rather than claimed
as universal live state. The Overview does not launch App Server or AI on
refresh. Local filesystem and Git reads require no new login.

Actions: opening a repository navigates to Projects; Add project selects
Projects; Inspect attention opens Activity or Sessions according to evidence;
Open planner selects Planner; Refresh updates local state and requests
the selected provider's limit refresh. Only local activity feed expansion is
interactive within Overview.

Manual viewport, font scaling and keyboard checks on Windows/Linux/macOS
remain part of the release gate. See `docs/AGENT-HANDOFF-WORKSPACE.md`.

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
