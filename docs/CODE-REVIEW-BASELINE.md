# Code review — fresh baseline (2026-10-21)

Reviewed against `main` commit `c302598` (source version 0.5.2). This review
treats Codex Monitor as a new application, not an upgrade path for older
on-disk formats. No unit tests were added or executed.

## Policy

- Exactly one local JSON database schema: `monitor.db.json` v1.
- Every history record includes `PluginId`; alert states use `pluginId:windowName`.
- No import from `history.json`, automatic alert-level conversions or
  fallback to old unqualified keys.
- Unsupported/missing database properties are **rejected**, not converted.
- Atomic writes, backup recovery and corruption preservation remain because
  they protect the current baseline, not because they migrate prior versions.
- Runtime paths are supplied by the platform host through DI.

## Cleanups completed on this branch

| Area | Change |
| --- | --- |
| `MonitorDatabase.Load` | Removed `history.json` importer and Windows-only default path |
| `MonitorDatabaseDocument` | Explicitly requires baseline schema v1 and required collections |
| `UsageHistory` | Removed alternate file writer, path-based constructor and default provider ID |
| `UsageAlertService` | Removed legacy alert-key lookup and implicit `codex` provider |
| `MonitorSettings` | Removed old percentage-to-level conversion and Windows default path |
| `CodexTokenClient` | Removed OS-dependent launcher selection from provider; bounded stderr cleanup |
| `UsageCoordinator` | Ignore read failures from an adapter that is no longer active |
| Documentation | Clarified baseline policy and distinguished source version from published GitHub release |
| Development tools | Updated existing console/screenshot fixture calls for the single history model (no new tests) |

## Resolution of review findings

| Issue | Change | Status |
| --- | --- | --- |
| Failed database write hidden by settings save | Independent database-load, database-write and settings diagnostics; only writes to the affected store clear its error | Implemented |
| Failed token request throttled for a full 30 minutes | 1/2/4/5-minute bounded retry after failure, configured normal cadence after success, force bypass | Implemented |
| Percentage drop treated as reset | Only an elapsed previous reset deadline followed by a later deadline begins a new cycle; corrected estimates preserve snooze | Implemented |
| Broken settings silently overwritten | Preserve settings as `settings.json.corrupt.*`, report diagnostics; block saving if preservation fails | Implemented |
| Agent switch skips new read while previous adapter is busy | Queue after semaphores, resolve capabilities after acquisition and reject previous generations | Implemented |
| Build/release not independently verified | Compile-only workflow and release gate; source version is not a published release | Configured; build results pending |
| Linux/macOS tray, packaging and installer parity | Existing experimental Unix platform integrations remain separate | Manual validation still required |

No new test suite or migration framework was introduced.

## Review conclusion

The concrete state, refresh, alert and persistence defects identified in the
previous code review are fixed in source. The changes still require a passing
.NET compilation and platform-specific runtime checks before release.
