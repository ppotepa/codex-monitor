# Codex Monitor 0.5.0

0.5.0 is the first release built from the refactored project layout.

## User-visible changes

- Token activity is cached in `%LOCALAPPDATA%\CodexMonitor\monitor.db.json` and
  appears immediately after restart. The configured token TTL controls when a
  provider read runs again; the manual refresh button bypasses the TTL.
- Limit history and token cache share one small atomic local store. Existing
  `history.json` data is migrated on first load.
- Settings expose independent refresh policies for limits, tokens and AI-agent
  discovery, including a manual-only token mode.
- **AI agents...** reports detected command-line agents and clearly separates
  detection from supported adapters. Codex remains the only working adapter.

## Structural changes

- Shared logic lives in `src/CodexMonitor.Core`.
- Codex integration lives in `src/CodexMonitor.Plugins.Codex`.
- Dependency injection and provider composition live in
  `src/CodexMonitor.Composition`.
- Windows uses the native WinForms host in `src/CodexMonitor.Desktop.Windows`.
- Linux/macOS have an experimental Avalonia host and platform services, but are
  not release-supported.

## Release validation

```powershell
dotnet build CodexMonitor.slnx -c Release --nologo
dotnet run --project tests/ParserChecks.csproj -c Release --nologo
dotnet run --project tools/Screenshots/Screenshots.csproj -c Release --nologo
./scripts/build-installer.ps1
```

The Windows installer is `installer\CodexMonitor-0.5.0-Setup.exe`. It targets
x64 and expects the .NET 10 Desktop Runtime; it does not bundle the runtime.
