# Architecture — shared Avalonia desktop

## Dependency direction

```mermaid
flowchart TB
    Desktop[CodexMonitor.Desktop<br/>composition root, Avalonia host and tray] --> UI[CodexMonitor.UI<br/>AXAML, ViewModels, charts]
    Desktop --> Windows[Platform.Windows]
    Desktop --> Unix[Platform.Unix]
    Desktop --> Infrastructure[CodexMonitor.Infrastructure<br/>processes and JSON stores]
    Desktop --> Provider[CodexMonitor.Providers.Codex]
    UI --> Core[CodexMonitor.Core]
    Infrastructure --> Core
    Provider --> Core
    Windows --> Core
    Unix --> Core
```

The **only** graphical implementation is Avalonia. The Windows and Unix
WinForms/code-only dashboard projects have been removed. A single
`DashboardViewModel` translates `DashboardState` into values for AXAML.
The same controls render via Avalonia's cross-platform drawing backend.
No renderer-per-OS code or user-defined layout language is maintained.

## Responsibilities

| Project | Owns |
| --- | --- |
| `Core` | Domain models, application services, settings/history ports, contracts and agent selection |
| `Infrastructure` | Process execution, JSON settings repository and `monitor.db.json` adapter |
| `Providers.Codex` | Codex usage CLI, app-server protocol, parsing and `AddCodexProvider()` |
| `UI` | Shared `DashboardWindow`, `LimitsView`, `TokensView`, `SettingsWindow`, `AgentStatusWindow`, AXAML styles, `UsageRing`, history chart and ViewModels |
| `Desktop` | One `AppBuilder`, `DesktopApp`, `DesktopHost`, `DesktopTrayShell` and composition root |
| `Platform.Windows` | Registry startup, Windows PATH discovery, Windows process launching and notification adapter |
| `Platform.Unix` | Executable lookup, process launching; separate XDG/LaunchAgent, notification and window-close policies |

Platform selection is confined to `src/Desktop/Host/Program.cs`. Shared UI and Core
do not contain large OS-specific conditionals. Platform-specific menu bar
behavior and tray limitations remain host concerns.

The solution contains seven production projects. `Core` has no dependency on
the UI, host, Infrastructure or a concrete provider. Infrastructure, provider,
platform and UI adapters point inward to Core; Desktop composes them.

## Source folders and namespace roots

Each project file sits directly in its architectural folder. The folder path
does not repeat the project name; C# namespaces use the product and project
name as their root, then follow source subfolders:

| Source folder | Project file | Namespace root |
| --- | --- | --- |
| `src/Core/` | `CodexMonitor.Core.csproj` | `CodexMonitor.Core` |
| `src/Infrastructure/` | `CodexMonitor.Infrastructure.csproj` | `CodexMonitor.Infrastructure` |
| `src/Providers/Codex/` | `CodexMonitor.Providers.Codex.csproj` | `CodexMonitor.Providers.Codex` |
| `src/Platform/Windows/` | `CodexMonitor.Platform.Windows.csproj` | `CodexMonitor.Platform.Windows` |
| `src/Platform/Unix/` | `CodexMonitor.Platform.Unix.csproj` | `CodexMonitor.Platform.Unix` |
| `src/Desktop/UI/` | `CodexMonitor.UI.csproj` | `CodexMonitor.UI` |
| `src/Desktop/Host/` | `CodexMonitor.Desktop.csproj` | `CodexMonitor.Desktop` |

## Interaction flow

```text
Operating system host / tray menu
    -> UsageCoordinator, IAgentManager
    -> current provider capability interfaces (IUsageProvider, ITokenUsageProvider)
    -> DashboardState (domain)
    -> DashboardViewModel (presentation)
    -> DashboardWindow / LimitsView / TokensView (single AXAML tree)
```

The UI emits commands (refresh, repair, settings, token selection) through
an observable ViewModel. The host orchestrates operations, schedules timers,
shows dialogs and sends notifications via `IDesktopNotificationService`.
The GUI never installs or repairs a CLI automatically.

## Persistence

Only `monitor.db.json` schema `v1` and `settings.json` are supported.
Every history item includes an explicit provider ID; alert keys are namespaced.
The C# model uses `ProviderId`, while serialization retains the existing
`pluginId` JSON property to keep `monitor.db.json` compatible.
No old-file migration or fallback writer exists. Atomic updates, current
format recovery copies and persistence diagnostics remain.

## Provider registration

`[UsageProvider]` annotates stable identity and executable names. Optional
`ITokenUsageProvider` and `IRepairableUsageProvider` describe capabilities;
the Codex adapter is registered explicitly through `AddCodexProvider()`.
`AgentCatalog` reports other installed CLIs without pretending they are
supported providers. Windows, Linux and macOS adapters expose their own
registration methods, keeping platform implementations internal to their projects.

## Build/release limitations

This migration changes the Windows presentation from WinForms to Avalonia.
No matching public release should be announced until the Windows tray,
installer, Linux X11/Wayland behavior and macOS menu bar/bundle are built
and checked on their corresponding targets. GitHub-hosted build jobs have
previously failed before runner allocation; those failures do not validate
or invalidate the source. See `docs/RELEASE-GATE.md`.
