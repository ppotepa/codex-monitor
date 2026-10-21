# Codex Monitor

A small Windows tray app that shows your Codex usage next to the clock. It reads data through `codex-cli-usage.exe` and does not read or store your login credentials.

## Install

1. Download `CodexMonitor-0.1.2-Setup.exe` from [Releases](https://github.com/ppotepa/codex-monitor/releases) and run it.
2. Install the [.NET 10 Desktop Runtime for Windows x64](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) if needed. The small installer does not bundle the runtime.
3. Start Codex Monitor from the Start menu. Codex CLI should be signed in under the same Windows account.

If `codex-cli-usage.exe` is missing or its Python environment is broken, the app attempts to install or repair it automatically with `uv` and Python 3.12. If the package install fails, it installs the core CLI from the [`clanker-usage` repository](https://github.com/wakamex/clanker-usage) at tag `v0.4.1`. You can retry with the button in the popup. This requires [uv](https://docs.astral.sh/uv/getting-started/installation/) in `PATH` or `%USERPROFILE%\.local\bin`.

Left-click the tray icon to see usage and reset times. Right-click for refresh, startup, and exit options. Windows may initially hide the icon in the overflow area; you can drag it next to the clock.

## Build

Requires the .NET 10 SDK and Inno Setup 6. From the project directory, run:

```powershell
./build-installer.ps1
```

The installer is written to `installer\`. To publish only the app executable:

```powershell
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o dist
```

## Data

Every five minutes, the app runs `codex-cli-usage.exe json --max-age 300` without opening a console window. If only the core CLI is available, it runs `clanker-usage.exe codex json --max-age 300`. It displays the limits in `windows[]` without assuming that every account has a five-hour limit. If a refresh fails, it retains the last result and marks it as stale.
