# Architecture decisions

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
- Keep the native Windows host and installation layout stable, but do not
  implement runtime migrations or compatibility layers for old data files.
  The application is a fresh baseline, not an upgrade framework.

## Current implementation

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
7. GitHub Actions compiles on Windows, Linux and macOS; automated tests are deferred.

## Remaining work

1. **Keep the release gate repeatable.** The full solution, parser checks,
   screenshot checks and Windows installer now build locally on Windows. The
   same commands remain the release checklist for future versions.
2. Continue checking Windows WinForms behavior after structural changes,
   especially tray menus, settings persistence, installer upgrades and
   app-server process handling.
3. Check Avalonia Linux (X11/Wayland) and macOS menu bar/tray operation on
   real target desktops, including fallback window closing without AppIndicator.
4. Build and validate the source-provided Debian and macOS packaging scripts.
   Native notifications have best-effort Linux/macOS adapters; Developer ID
   signing and notarization remain required for release distribution.
5. Provider-namespaced alert states and history are the only supported format.
   The old `history.json` import, default plugin IDs and pre-refactor
   alert-key lookup have been removed. Database reads accept only baseline
   schema `v1`, without any migration pipeline.
6. Automated behavioral tests remain deferred. CI validates compilation only.

## Merge policy

The stable `main` should be fast-forwarded to the migration commit only
after the Windows project and Unix-neutral projects compile and existing
Windows UI behavior has been checked. Preserve a single parent and never
force-push the default branch.

## Reliability corrections after review

- Every asynchronous provider refresh is tied to the active plugin generation.
  Agent changes queue a new refresh behind older operations instead of
  dropping it when a semaphore is occupied.
- Failed token requests use bounded 1/2/4/5-minute backoff. A successful
  read returns to the configured cadence.
- Only an elapsed reset boundary and newer reported boundary start a new alert
  cycle. Percentage decreases and timestamp corrections do not unsnooze.
- Database write errors remain visible until a database write succeeds,
  independently of settings reads/writes. Malformed settings are preserved
  before defaults may be saved.
- GitHub Actions is configured for three-OS compilation only; automated tests
  remain deferred. Real platform builds, app startup and packaging checks
  are release requirements, not assumed successes.
