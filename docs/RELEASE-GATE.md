# Release gate — unified Avalonia source

This branch refactors the **source**, not the GitHub Release assets. The
source version is 0.5.1; a published tag or installer version must be
verified separately.

Before releasing:
1. Compile `dotnet build CodexMonitor.slnx -c Release` on Windows, Linux and
   macOS with the .NET 10 SDK; no errors, warnings reviewed.
2. Start the shared Avalonia executable and check the Limits/Tokens AXAML
   layout, UI binding, history ring/chart, settings and provider switching.
3. Windows: verify tray icon, notifications, autostart, in-place installer and
   all native Avalonia binaries in the Inno Setup package.
4. Linux: check X11 and Wayland tray visibility, close behavior, autostart,
   notifications and both Debian architecture packages.
5. macOS: check menu-bar icon/menu, LaunchAgent, notifications, app bundle
   signing, launch and eventual notarization.
6. Check the exact installer artifacts and published version before adding
   a new GitHub Release.

Automated tests are intentionally deferred. GitHub-hosted runners have
sometimes failed before any steps ran; a failed/empty CI job cannot be
reported as successful compilation. Do not merge to main until the new
cross-platform UI at least builds.
