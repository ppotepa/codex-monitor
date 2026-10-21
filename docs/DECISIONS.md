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

1. Four source projects: Core, Codex plugin, Windows desktop host, Unix OS services.
2. WinForms references removed from Core; native shell remains Windows-only.
3. Explicit singleton lifetime of coordinator, settings, database and plugin.
4. Platform executable location, command launching, app data paths and startup
   contracts extracted. Windows and Unix have separate lookup/startup classes.
5. Shared agent discovery created from plugin metadata and diagnostics.
6. Legacy test and screenshot projects point at the new project layout; no new
   behavioral tests have been added.

## Deliberately deferred

1. Shared Linux/macOS graphical host, popup and tray integration. Select a
   supported UI toolkit only after validating tray behavior on the target desktops.
2. Linux/macOS packaging, icons and actual platform smoke runs.
3. Split the larger Windows presentation classes only when there is a clear
   ownership boundary: `UsagePopup` (~446 lines), `MonitorContext`
   (~247 lines) and chart rendering. Avoid an interface per WinForms control.
4. Migrate persisted agent-specific alert/history keys before adding a second
   active provider.
5. Correct the previously identified alert re-triggering and stale token
   refresh scenarios as focused behavior fixes, separate from this migration.
6. Introduce normal build validation on Windows and Unix. Automated tests
   are intentionally deferred at this stage.

## Merge policy

The stable `main` should be fast-forwarded to the migration commit only
after the Windows project and Unix-neutral projects compile and existing
Windows UI behavior has been checked. Preserve a single parent and never
force-push the default branch.
