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

1. Four source projects: Core, Codex plugin, Windows desktop host and Unix OS services.
2. OS-independent `DashboardState` and `IDashboardView` contract; WinForms stays behind the Windows host.
3. `UsagePopup` reduced by extracting `LimitsDashboardView` and `TokensDashboardView`.
   `MonitorTrayMenu` owns menu commands; `MonitorContext` owns only the host lifetime and event wiring.
4. `UsageAlertService` owns alert states separately from refresh and error transitions.
5. Plugin identity and executable metadata from `[UsagePlugin]`; optional capabilities from interfaces.
6. Explicit singleton registrations for settings, database, history and coordinator via Microsoft DI.
7. OS seams for paths, process launching, executable lookup and autostart; Linux/macOS share Unix process rules but have separate startup and app data paths.
8. Shared usage formatting in `Core/Presentation`; settings now save atomically.
9. Build-only GitHub workflow, switched to manual trigger because hosted runners fail before job allocation.

## Remaining work

1. Implement the actual Linux/macOS dashboard and tray host, likely with a shared Avalonia presentation and OS-specific tray lifecycle. This is the largest incomplete item.
2. Verify that all Windows projects compile, that existing tray/settings behavior is unchanged,
   and that published binaries and Inno Setup install correctly.
3. Compile and package Linux and macOS hosts on actual target environments.
4. Improve the Codex app-server/protocol handling and address reset timestamp drift if needed.
5. Before adding a second active plugin, scope alert states and historical limits to the plugin ID.
6. No automated behavioral tests are added in this phase.

## Merge policy

The stable `main` should be fast-forwarded to the migration commit only
after the Windows project and Unix-neutral projects compile and existing
Windows UI behavior has been checked. Preserve a single parent and never
force-push the default branch.
