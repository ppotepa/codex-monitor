# Architecture decisions

## Shared UI

- Avalonia 11 is the one rendering framework for Windows, Linux and macOS.
- Views/layout/styles are written once in AXAML under `CodexMonitor.UI`.
- ViewModels expose display values and commands, leaving provider discovery,
  refresh scheduling and persistence in Core.
- Custom usage ring and history chart controls use Avalonia drawing, not
  OS-dependent GDI or platform-specific layout.
- There are **no** duplicate WinForms/Unix views, UI-only bridge interfaces
  with one implementation, or custom renderer DSL.
- Desktop tray commands, settings dialogs and timers are shared; actual OS
  integration services remain distinct.
- Windows/macOS close to tray where supported; Linux keeps a visible window
  because Wayland/SNI implementations may hide the tray entirely.
- Preserve the `CodexMonitor.exe` name and existing user data directories on
  Windows, and Linux/macOS locations as defined previously.

## Baseline / reliability

- `Core` has no Avalonia dependency and no app-version migration code.
- Current JSON schema v1 is the only supported persistence format.
- Every history and alert key includes the provider ID.
- Atomic writes, corrupt file preservation and matching-format backups are
  failure recovery, not migration.
- CLI installation/repair is explicit; provider fallbacks are for runtime
  failures, not application-version compatibility.
- Provider-switch refresh, short error backoff and per-store persistence errors
  remain provider-neutral.

## Scope and release

The production solution contains seven projects: Core, Infrastructure,
Providers.Codex, Platform.Windows, Platform.Unix, Desktop.UI and Desktop.Host.
Core is divided into Agents, Usage, Notifications/Alerts, Settings and
Application modules. Infrastructure owns process execution and local JSON
persistence. Desktop.Host is the composition root; platform projects register
their own internal implementations through explicit service extensions.

There is one Avalonia UI for all target systems. The old WinForms host and
code-only Unix host are historical only; screenshots and release notes from
those versions stay as historical documentation.

Automated tests are deferred. Only a real three-OS compilation followed by
platform smoke checks and packaging validation can clear the next release.
