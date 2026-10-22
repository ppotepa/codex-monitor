# Agent handoff — CodexMonitor Workspace / compact Overview

**Updated:** 2026-10-22  
**Repository:** https://github.com/ppotepa/codex-monitor  
**Delivery branch:** \`main\` (the owner explicitly requested direct writes, no new PR)  
**Status:** Source implementation pushed. Cross-platform .NET build and native GUI smoke test **not verified** in this environment.

## 1. Current user objective and design rules

CodexMonitor is a **local-first .NET 10 + Avalonia 11** utility for monitoring AI coding agents. The existing small tray popup is the **Quick View**; a separate Workspace window provides deeper tools. The user requested that the full Workspace Overview match the latest **Compact Overview HTML mockup** rather than the earlier spacious, light, card-heavy placeholder.

Design is deliberately:
- **Fluent Compact**, one Avalonia UI tree shared across Windows/Linux/macOS.
- Dark graphite/mint palette already used by the tray: window \`#15191F\`, surface \`#20262E\` or \`#1D232B\`, hairlines \`#29323D\`, mint \`#63D9C6\`.
- Small, consistent typography (most interface text 10–13 logical px), restrained whitespace, no gradients, neon effects, mountains/illustrative backgrounds, or dozens of metric cards.
- **At most two visual attention centres:** (1) projects; (2) items requiring user attention. Above them sits a factual allowance strip. Planner recommendation and recent activity are secondary; activity is collapsed by default.
- No dedicated authentication for local Git, skills, project registry, goals or instruction reading. AI is optional and uses the user's existing Codex CLI authentication when explicitly invoked.
- Missing telemetry must not be represented as \`Idle\`, \`Working\`, or \`0%\` without evidence. Never attribute repository Git changes to individual agents without trustworthy correlation.
- Local installation and file changes need explicit review/confirmation; passive inspection must be read-only.

## 2. Architecture and existing source

Solution: \`CodexMonitor.slnx\`; projects in \`src/Core\`, \`src/Infrastructure\`, \`src/Providers/Codex\`, \`src/Platform/Windows\`, \`src/Platform/Unix\`, \`src/Desktop/UI\`, \`src/Desktop/Host\`, and standalone checks in \`tests\`.

- \`src/Desktop/Host/DesktopHost.cs\`: main composition, callbacks, timers, shared tray, launches Workspace.
- \`src/Desktop/UI/Views/DashboardWindow.axaml\`: **Quick View**, keep small and stable.
- \`src/Desktop/UI/Views/WorkspaceWindow.axaml\`: full Workspace shell + other tool tabs, no WinForms duplicate.
- \`src/Desktop/UI/ViewModels/WorkspaceViewModel.cs\`: existing workspace context and command orchestration (historically too large; be surgical).
- \`src/Core/Workspace\`: provider feature contracts (\`IFeature\`) and tool abstractions.
- \`src/Infrastructure/Workspace\`: local JSON registry/goals, Git inspection, local skills, instruction scanning, activity journal, diagnostics.
- \`src/Providers/Codex\`: Codex usage, optional App Server stored-session listing, schema-bounded AI advisor and instructions optimizer.

The \`IFeature\` registry models **provider capabilities**, not every application widget. Codex has adapters; other detected CLIs are shown as unsupported / NotImplemented where appropriate. Do not create duplicate OS-specific presentation trees or migration scaffolding for this new app.

## 3. Overview work pushed directly to main on 2026-10-22

New files:
- \`src/Desktop/UI/Views/WorkspaceOverviewView.axaml\` — compact, evidence-aware dark UI. Four small facts in one ribbon: remaining quota, next reset, observed session IDs, tracked projects. Main left projects table with Git diff and a collapsible activity feed. Narrow right attention and secondary next-step panel.
- \`src/Desktop/UI/Views/WorkspaceOverviewView.axaml.cs\` — minimal Avalonia UserControl code-behind.
- \`src/Desktop/UI/ViewModels/WorkspaceViewModel.Overview.cs\` — new \`WorkspaceViewModel\` partial: async local read model, project rows, evidence-linked attention, actual provider quota, activity preview, commands.

Updated files:
- \`src/Desktop/UI/Views/WorkspaceWindow.axaml\` — old Overview replaced by the new view. Shell now uses dark palette, compact 202px sidebar with SVG-like PathIcons, 64px topbar/breadcrumb, selected-tab styling, and updated colors in the **existing** tool tabs. Default dimensions 1120×740, min width 960.
- \`src/Desktop/UI/Views/WorkspaceWindow.axaml.cs\` — initial local overview refresh on \`Opened\`.
- \`src/Desktop/UI/ViewModels/WorkspaceViewModel.cs\` — class made \`partial\`, selected-page breadcrumb and refresh wiring, quota snapshot updates, initial Overview command registration.
- \`docs/UI-DESIGN.md\` — compact UX and evidence contract.

Notable commits on \`main\` from this effort:
\`581bb851\` (overview model), \`c7cd8779\` (AXAML), \`623cb03b\` (view), \`11b769f8\` (VM wiring), \`d4b813ea\` (breadcrumb), \`26297bb2\` (compact shell), \`652ff03c\` (opened lifecycle), \`785d4aa7\` (safe active-project semantics), \`842ee89a\` / \`4c4f6fb8\` (attention source routing), \`01f8af12\` / \`87594536\` (compact sizing).

## 4. Data lineage and intentionally conservative semantics

| Display | Source | What is and is not guaranteed |
| --- | --- | --- |
| Remaining allowance | \`UsageCoordinator.Dashboard\` → \`DashboardViewModel\` | Real provider reading, or “No data”. Not session context tokens. |
| Next reset | \`UsageSnapshot.MainWindow.ResetsAt\` | Reported reset time; formatted locally; “Not reported” if missing. |
| Tracked projects | \`IWorkspaceProjectRepository.List()\` | Local saved paths; counts include entries outside current eight-row preview. |
| Git diff per project | \`IGitImpactService.InspectAsync\` | Working-tree diff at selected repository; per-project, **not per agent**. |
| Session IDs | Explicit stored App Server session read and/or unique IDs in local \`activity.jsonl\` | May be incomplete; **not** global live-agent process counts. |
| Project state | Explicit App Server \`active\` / \`waitingOnApproval\`, else recent observed events or “No signal” | “Reported active” is not a guarantee of a currently running external host. |
| Attention | Recent \`PermissionRequest\` hook/notify metadata (10-minute evidence window), or \`waitingOnApproval\` from previously loaded App Server sessions | “May be waiting”, never claim unresolved approval from an old event. |
| Next step | \`IWorkspacePlanner.Evaluate\` | Deterministic, evidence-qualified guidance. No automatic AI call during Overview refresh. |
| Activity | Local metadata-only \`IWorkspaceActivitySource.ReadRecent\` | Maximum eight preview rows, list collapsed by default; does not contain chat content. |

Overview reads up to **eight tracked repositories**, with a maximum of three concurrent Git reads. The total tracked-project count remains accurate even when the list is truncated. A manual refresh also requests normal limit polling through the existing usage ViewModel.

Commands:
- **Add project / View all / project row** → Projects screen (project selection also runs its read-only inspection).
- **Inspect event** → Sessions if an App Server session reports waiting approval, otherwise Activity.
- **Open planner** → Planner tab.
- **Expand/collapse** → local activity preview only.
- **Refresh** → local Git/event overview read plus the normal provider limit refresh.

## 5. Other previously delivered Workspace modules (preserve them)

The repository already contains:
- User/project registry, Git working-tree vs last-commit impact, goals/time budgets.
- Opt-in Codex notification/hooks metadata receiver and explicit read-only stored App Server session listing.
- Local Skills Manager, SKILL.md preview, explicit source/target review and staged hash validation before install.
- AGENTS.md hierarchical inspector, simple audit, optional structured Codex rewrite proposal, stale-content protection, user confirmation and backup before write.
- Local Agent Doctor, deterministic Work Planner and optional Codex CLI structured JSON AI advice.
- A provider-neutral capability registry; missing Claude/Gemini implementations must be represented honestly.

Still outstanding in the broad product plan: external-host live-session coverage, arbitrary Git ranges, full remote skills catalog/updates/rollback, richer Attention Center and rules engine. **Do not treat these as prerequisites for finishing only the new Overview UX.**

## 6. Validation and limitations

**Static review done:** Both new AXAML files and the updated workspace shell passed a basic tag-nesting check. The new Overview's bindings were checked against the partial ViewModel and its row types. No .NET SDK is installed in the execution environment used for connector writes.

**Not verified:** .NET compiler, Avalonia XAML compiler, first-run appearance and real GUI interactions. GitHub Actions has repeatedly failed before exposing build steps on Windows, Ubuntu and macOS; do not label those runs “passed” or claim the errors are definitely from source. The \`AGENTS.md\` repo identity script is located at \`C:\Users\ppotepa\.codex\scripts\repo-check.ps1\` on the developer's Windows machine and cannot be executed in this Linux connector environment; the authenticated GitHub connector account was explicitly verified as \`ppotepa\`, with write permission to \`ppotepa/codex-monitor\`.

Build and checks from a configured .NET 10 machine:

\`\`\`sh
dotnet restore CodexMonitor.slnx
dotnet build CodexMonitor.slnx -c Release --nologo
dotnet run --project tests/ParserChecks.csproj -c Release
dotnet run --project src/Desktop/Host/CodexMonitor.Desktop.csproj
\`\`\`

Manual acceptance checklist:
1. Launch from tray **Open workspace**; Quick View must remain unchanged.
2. At ~1110px width, sidebar remains compact; view remains readable at 960px min width. Check overflow and focus traversal.
3. Empty registry: shows honest empty-project state; no fake counters or login prompt.
4. Add a real local Git repository in Projects; return to Overview: project row, path/branch, and working-tree diff appear.
5. Select a project row: opens its Projects context without writing to the repo.
6. With notify/hooks not configured, show “Not configured” / partial; never falsely show “Idle”.
7. With an observed permission event, display one qualified attention item; clicking it opens the right evidence source.
8. Expand activity, confirm metadata only and provider scope; collapse again.
9. Refresh quota and verify it updates; no Codex App Server or AI request automatically started from Overview.
10. Verify remaining tabs Skills/AGENTS.md/Planner/Doctor/Usage and Settings remain legible in dark theme.
11. Run Windows, Linux and macOS build/smoke checks and resolve actual XAML compiler errors if any.

## 7. Next agent's recommended order

1. **First run the .NET 10 build and repair any XAML/compiler errors** — this was not available through this connector environment.
2. Launch Avalonia Workspace on Windows, capture 1120×740 screenshot, compare spacing/typography/sidebar and two focal areas against the dark compact Overview preview from the conversation. Do not assume pixel perfection without rendering it.
3. Check narrow window behavior, keyboard navigation and tab selection indicator in the actual Avalonia theme. Adjust style resource / TabStrip template as needed, rather than adding gradients or large cards.
4. Add a focused UI/VM smoke test if feasible; keep tests readable and provider data synthetic in tests only.
5. Consider extracting Overview data projection from the large workspace ViewModel if more screens need it; do not prematurely split into many projects.
6. Only after Overview reaches its acceptance gate, continue other Workspace features.

## 8. Collaboration rules for follow-up

The current user explicitly requested direct commits to **main**, **no PRs**, and expects a clear handoff. Any future agent should recheck repository identity and latest \`main\` HEAD before writing, preserve existing functionality, and communicate exactly what was verified. If the user later changes this preference, obey the latest instruction.

Core UX preference: **compact and calm, no bloat**. The sidebar, four-fact strip, project table, single attention panel and unboxed planner guidance are intentional. Avoid generic marketing dashboards with many separate cards or decorative imagery.
