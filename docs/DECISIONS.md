# Architectural decisions and next migration steps

## Intent

Codex Monitor is a small application. Keep classes and modules focused, but do
not add interfaces solely to satisfy a pattern. Different operating systems
and different AI providers are the actual sources of variation.

- Use `src/` for product code and consistent PascalCase project/type folders.
  Keep top-level `docs/`, `scripts/`, `packaging/`, `tests/`, and `tools/`
  in lowercase.
- Keep Core independent of desktop UI, native OS APIs and provider-specific JSON.
- Build each desktop host through explicit dependency injection; never place
  Windows/Linux/macOS selection inside business logic.
- Use `IDesktopHost` for desktop lifetime. A platform implementation may use
  WinForms, Avalonia, native tray libraries or a combination as needed.
- Reuse Linux/macOS application views where practical, but do not assume that
  Wayland/X11 tray and macOS menu bar integration behave identically.
- Platform seams: `IExecutableLocator`, `ICommandLaunchStrategy`,
  `IAppPaths`, `IStartupService`. Add further contracts only for actual
  platform differences.
- Plugins register with DI. Identity and executable metadata come from
  `[UsagePlugin]`. Their real capabilities are interface-based, so a provider
  without token support does not need dummy methods.
- Keep a small read-only list of *unsupported* CLI names solely for diagnostics.
  It is not a plugin catalog or a promise of compatibility.
- Preserve published Windows behavior, local files and installer naming during
  migration. No feature rewrites as part of the structural change.

## Implemented in this migration

1. Six source projects: Core, Codex plugin, shared DI composition, WinForms
   desktop, Unix platform services, and experimental Avalonia Unix desktop.
2. Shared `DashboardState` / `IDashboardView` contract; all application
   state and plugin transports stay outside concrete desktop frameworks.
3. WinForms popup split into Limits/Tokens views, with a separate tray menu.
4. Avalonia Linux/macOS app with one shared dashboard, tray, settings and
   agent diagnostics; no OS-specific conditionals in application services.
5. Shared `UsageAlertService`, `UsageDisplayText`, `IProcessRunner`,
   explicit plugin capabilities and metadata, atomic settings writes.
6. Single DI registrations for coordinator, database/history, settings,
   discovery and Codex via `CodexMonitor.Composition`. Each host supplies
   only its platform services and views.
7. GitHub build workflow remains manually triggered: hosted jobs have failed
   before runner allocation; no compiler output is available.

## Remaining work

1. **Verify a real compile of the full solution and published binaries.** This
   is not completed because .NET SDK is unavailable in the working environment.
2. Check Windows WinForms behavior after the refactor, especially tray menus,
   settings persistence, installer and app-server process handling.
3. Check Avalonia Linux (X11/Wayland) and macOS menu bar/tray operation on
   real target desktops, including fallbacks without AppIndicator.
4. Make Unix native installers/packages and OS-specific notifications. The
   Unix dashboard currently uses a compact text-based usage representation
   rather than the richer Windows charts.
5. Add provider-namespaced alert state/history when a second plugin exists.
6. Automated behavioral tests remain deliberately deferred.

## Merge policy

The stable `main` should be fast-forwarded to the migration commit only
after the Windows project and Unix-neutral projects compile and existing
Windows UI behavior has been checked. Preserve a single parent and never
force-push the default branch.
