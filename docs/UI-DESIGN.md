# UI design

Codex Monitor is a Windows tray app. The shell shows the active built-in plugin; currently only Codex is registered. There is no provider chooser until a second provider actually works.

## Layout

The popup is 420 × 654 logical pixels at 100% scaling. Both tabs share its size so switching views does not expand the window beneath the taskbar. WinForms handles DPI scaling. If the working area is too small, the popup becomes scrollable, keeping content reachable.

The header contains the application name, a Codex plugin badge and a view-specific update timestamp. Product strings are English; dates and numeric separators follow the user's locale.

**Limits:** the ring says how much is used; the adjacent large value says how much is left. Reset time appears separately. Additional reported windows use compact bars. History uses the remaining space below those windows. Refresh and dependency repair are at the bottom.

**Tokens:** the actual date of the latest daily total appears above the largest value. Lifetime and peak-day totals share a separate card. The chart shows seven calendar days ending on the latest reported date, not necessarily today. Compact values appear above bars; hover or arrow-key selection exposes exact totals. Missing dates remain gaps, and zero remains a real observation.

![Actual views with illustrative data](docs/images/overview.png)

## States and interactions

| State | Behavior |
| --- | --- |
| First launch | Limits selected; no fabricated zero values |
| Refreshing | Preserve previous result; disable that section's refresh button |
| Missing daily data | Explain that no daily totals were returned |
| Refresh failure | Keep last successful values and timestamp; show an error with a full-text tooltip |
| Missing/broken limit CLI | Codex plugin attempts repair once; allow manual retry |
| Unsupported token capability | Hide token navigation and refuse to select that view |
| Short display | Scroll content; tab changes preserve constrained window size |

Left-click opens the popup; right-click opens settings and commands. The selected tab is retained while the process runs. Opening Tokens loads its data independently of Limits. Alerts use percentages, not token counts. Reset boundaries and gaps over 15 minutes break the history line.

The tray menu's **AI agents...** screen is a diagnostics view, not a provider marketplace. It lists known command-line agents, their detected executable path, adapter status and data source. “Detected” never means “supported”: only a registered adapter can become the active source.

The **Settings...** dialog groups operational controls into two ideas: alert thresholds and a smart refresh policy. Limits, token activity and agent detection have independent cadences. The defaults (5 minutes, 30 minutes and 30 minutes) keep the tray responsive without waking provider processes unnecessarily. Token refresh can be disabled for background polling while a manual refresh remains available from the Tokens view.

## Screenshot workflow

Run `dotnet run --project tools/Screenshots/Screenshots.csproj` on Windows. The tool creates native control handles without displaying a window, renders them with `DrawToBitmap`, and composes the README overview in memory. It does not access the desktop, CLI, settings, history or a signed-in account.

Fixtures cover populated, empty, loading and error states. Layout checks cover a 1080p working area, a short working area, tab switching while constrained, restoring full size, and unavailable token capability. Physical monitor DPI transitions still require manual verification.

## Deliberate limits

Windows determines tray icon dimensions; the popup supplies detailed numbers. Token totals do not reveal remaining quota or subscription renewal date. Other providers, other operating systems, billing and external plugin downloads are outside the current implementation.
