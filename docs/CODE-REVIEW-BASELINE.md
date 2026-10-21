# Code review — fresh baseline (2026-10-21)

Reviewed against `main` commit `6a159d9` (source version 0.5.1). This review
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

## Further review findings (not included in baseline cleanup)

### P2 — Persistence error can disappear before persistence recovers

`src/CodexMonitor.Core/Application/UsageCoordinator.cs`

A failed database write sets `PersistenceError` via `SetPersistenceError`.
A later successful settings write calls `ClearPersistenceError()`, which
reads only `_database.PersistenceError`. The database's own property does
not necessarily reflect the earlier write exception, so a successful settings
save can hide an unresolved database write failure. Track failures per
persistence component, clearing each only after its own successful write.

### P2 — Transient token failures are throttled for the full polling interval

`src/CodexMonitor.Core/Application/UsageCoordinator.cs`

`_tokenLastAttemptAt` is set before a token request. A failed request still
holds the normal TTL (30 minutes by default) until a manual forced refresh.
Use a bounded short retry/backoff for errors; leave the normal TTL for a
successful response.

### P2 — Reset detection can produce false resets

`src/CodexMonitor.Core/Application/UsageAlertService.cs`

A drop below the warning threshold may be treated as a new usage cycle even
without an actual reset. Slightly corrected or inconsistent usage data could
clear snooze or allow a repeat alert. Reset state should be based on reliable
window boundaries, not just alert severity crossing a threshold.

### P2 — Malformed settings are silently replaced with defaults

`src/CodexMonitor.Core/Persistence/MonitorSettings.cs`

`Load` catches JSON errors and returns a default settings object. The next
save may replace the unreadable file with defaults without preserving it.
Provide an explicit error and preserve the damaged file before replacement.
This is **failure recovery**, not migration.

### P2 — Plugin changes do not necessarily trigger a fresh read immediately

`UsageCoordinator.OnAgentChanged`, `MonitorContext.OnAgentChanged` and
`UnixDesktopHost.OnAgentChanged`

The selection change clears the currently displayed snapshots. If an earlier
read still owns a refresh semaphore, a new read may be skipped; the new
adapter can remain empty until the next scheduled refresh. Start a refresh
after the in-flight operation has released its gate, without poll loops.

### P2 — Release/build verification is not yet a reliable public gate

As of this review, the source tree is version 0.5.1 but GitHub Releases
lists v0.1.2 as the latest published download. GitHub Actions runs have
failed. Verify what is published separately from source versions; require
a successful compile and platform smoke check before distributing new binaries.

### P3 — Windows/Linux/macOS parity remains incomplete

The Unix host is experimental and the installers are not confirmed on
target systems. Tray behavior differs between X11, Wayland and macOS.
Native packaging, login startup and notification permission behavior require
manual platform verification.

## Review conclusion

The legacy migration paths are now removed from this branch. The architecture
has reasonable OS and plugin boundaries for a small application. Do not add
more interfaces for their own sake; prioritize data/error handling and
real platform validation before the next public release.
