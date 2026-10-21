# Codex Monitor 0.5.2

0.5.2 packages the unified Avalonia desktop UI for Windows, Linux and macOS.

## User-visible changes

- One shared Limits/Tokens dashboard is used on all desktop platforms.
- Tray actions, settings, agent diagnostics and notifications use the shared
  desktop host while keeping platform-specific services isolated.
- The tray icon renders the selected limit percentage and refreshes with live
  usage state.
- The Windows installer includes the published Avalonia runtime dependencies.

## Reliability changes

- Refresh, persistence and plugin selection fixes from 0.5.1 remain included.
- The installer and package metadata use version `0.5.2` consistently.

## Validation

```powershell
dotnet build CodexMonitor.slnx -c Release --nologo
dotnet run --project tests/ParserChecks.csproj -c Release
./scripts/build-installer.ps1
```

The Windows installer is `installer\CodexMonitor-0.5.2-Setup.exe`. It targets
x64 and expects the .NET 10 Desktop Runtime; it does not bundle the runtime.
Linux and macOS packaging remain experimental and require validation on their
respective target systems.
