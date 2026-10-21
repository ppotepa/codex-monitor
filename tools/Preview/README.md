# Desktop UI previews

Run from the repository root:

```powershell
dotnet run --project tools/Preview/Preview.csproj -c Release
```

Renders the actual Avalonia controls using Headless + Skia with synthetic
data. No desktop capture, account access or CLI process is involved.
Output defaults to `build/preview`; pass another directory as the first
argument. Includes Limits, Tokens, empty tokens and a smaller window with an
error. Also renders Settings and validation errors in a smaller settings window.
Limits, Tokens and Settings include 125% and 150% raster renders. These check
visual scaling, but do not replace native per-monitor DPI and keyboard checks.
The preview uses the same shared theme as the desktop host. Settings validation
uses deliberately invalid input, so it does not save settings or change startup.
This is a visual development tool, not a unit-test suite.
