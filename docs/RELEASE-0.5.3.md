# Codex Monitor 0.5.3

0.5.3 refreshes the Windows installer with the updated shared Avalonia UI.

## User-visible changes

- Updated graphite and mint theme with larger, more consistent labels.
- Dashboard content and refresh actions have clearer spacing and alignment.
- Settings are grouped into refresh, alerts and startup sections, with a
  persistent Save/Cancel footer and clearer validation.
- The dashboard header's empty areas can be dragged to move the window.

## Validation

- Release build: `dotnet build CodexMonitor.slnx -c Release --nologo`.
- Synthetic UI renders generated at 100%, 125% and 150% scale.
- Installer artifact: `installer\CodexMonitor-0.5.3-Setup.exe`.

The installer targets x64 and expects the .NET 10 Desktop Runtime.
