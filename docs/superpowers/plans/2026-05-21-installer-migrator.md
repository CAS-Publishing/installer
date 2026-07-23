# Installer Migrator Implementation Plan (Phase 4)

> **For agentic workers:** high-level spec. This plan has TWO sub-phases dispatched as separate subagent runs — 4a (Planner + schema, pure logic) then 4b (Backup + Apply + Rollback + UI wiring, with I/O). The controller reviews between them.

**Goal:** make the installer's `Apply selected` button perform actual migrations against the client project — adding/removing packages in `manifest.json`, deleting legacy `Assets/` directories with backup, registering scoped registries — and `Rollback last` reverse them. Phase 4a builds the deterministic planning core; Phase 4b builds the I/O and UI wiring on top.

**Architecture:** new `Editor/Migrator/` subsystem under `PSV.Installer.Migrator` namespace. The planner is pure (`Catalog + ScanReport + selection → ordered MigrationAction[]`) and the runner does the side effects. Backup snapshots affected files into `Library/InstallerBackups/<ts>/` together with `operations.json` describing what to undo. ManifestWriter generalises the JObject mutation logic that currently lives in `MetadataAutoInstall.EnsureScopedRegistry`. The UI's existing `EditorGUI.DisabledScope` wrappers come off the two action buttons.

**Tech stack:** unchanged — Unity 2022.3 Editor, C# 9, `Newtonsoft.Json.Linq`, `UnityEditor.PackageManager.Client`. No new deps.

---

## What we already know about real cases (drives the design)

| Case | Catalog shape | Migrator behaviour |
|---|---|---|
| `com.psv.crashlytics` → `com.psvgamestudio.crashlytics` | 1 `PackageRecord` with `legacyNpmIds=["com.psv.crashlytics"]` | Remove legacy id, add canonical id |
| `com.psv.firebase.base` → `analytics + remoteconfig` (split) | 2 `PackageRecord`s, **both** with `legacyNpmIds=["com.psv.firebase.base"]` | Migrator selects both; first action removes legacy id, subsequent ones see it already gone and just add their canonical |
| `com.psv.unity.edm` → nothing (transitive via Firebase) | **NEW** `UninstallRecord` with `legacyNpmIds=["com.psv.unity.edm"]` | Remove from manifest, install nothing |
| `Assets/Firebase/` legacy DLLs | `legacyAssetPaths` on the affected `PackageRecord` (analytics + remoteconfig both list it) | Backup, delete folder |

The only schema extension needed is `UninstallRecord` — split replace and asset cleanup are already covered.

---

## Phase 4a — Planner & schema (sub-agent #1)

### Schema extension

Extend `Catalog` POCO with one new top-level optional list:

```jsonc
{
  "schemaVersion": 1,
  "uninstall": [
    {
      "legacyNpmIds": ["com.psv.unity.edm"],
      "reason": "EDM 1.2.186 arrives transitively via com.google.firebase.app — no separate package needed."
    }
  ]
}
```

- **Field name:** `uninstall` (top-level array in `catalog.json`, sibling to `packages` / `external`).
- **POCO:** new class `UninstallRecord` with `LegacyNpmIds: List<string>` and `Reason: string`.
- **`schemaVersion` stays at 1.** Adding an optional list is backward-compatible (Newtonsoft ignores unknown fields, older installers reading new catalogs simply skip `uninstall`). Bump only if a breaking change comes later.

### Scanner extension

Scanner adds a second result list:

```
public sealed class UninstallScanResult { string LegacyNpmId; UninstallState State; string DetectedVersion; }
public enum UninstallState { NotInstalled, InstalledNeedsRemoval }
```

`ScanReport.Uninstalls: IReadOnlyList<UninstallScanResult>`. Each `UninstallRecord` from catalog produces zero or more results (one per `legacyNpmId` that's actually in manifest). If none of an `UninstallRecord`'s ids are present → no result emitted. If present → state `InstalledNeedsRemoval`, with the detected manifest version copied.

`ScanReport.Hash` MUST include `Uninstalls` (id + state pairs). Otherwise the auto-popup wouldn't notice a freshly-added legacy package needing removal.

`ScanReport.NeedsAttentionCount` (status bar) MUST count `InstalledNeedsRemoval` entries.

### MigrationPlanner

New static class `PSV.Installer.Migrator.MigrationPlanner`:

```
public static IReadOnlyList<MigrationAction> Plan(
    Catalog catalog,
    ScanReport report,
    ISelectionSet selection);
```

`ISelectionSet` is a small interface — `bool IsSelected(string id)` — implementer's call whether to define here or in UI namespace. The planner is pure: given a frozen catalog + report + selection, returns a deterministic action list. Same inputs → same output.

**Action types** (sealed classes or one struct-with-discriminator, implementer's choice):

| Action | Fields | What it does |
|---|---|---|
| `AddPackage` | id, version | Add `id: version` to `dependencies` in manifest |
| `RemovePackage` | id | Remove `id` from `dependencies` |
| `UpdatePackageVersion` | id, version | Replace `dependencies[id]` value |
| `AddScopedRegistry` | name, url, scope | Add a new `scopedRegistries` block |
| `AddScopeToRegistry` | url, scope | Append a scope to an existing `scopedRegistries` block matching url |
| `BackupAndDeletePath` | absolutePath | Snapshot then delete file/dir under `Assets/` |

**Planner rules** (per selected item):

For each selected `PackageScanResult`:
- `NotInstalled` → `AddPackage(record.Id, record.RecommendedVersion ?? record.MinVersion)`
- `UpmCurrent` → no actions
- `UpmOutdated` / `UpmBelowMin` → `UpdatePackageVersion(record.Id, record.RecommendedVersion ?? record.MinVersion)`
- `LegacyUpm` → `RemovePackage(detectedLegacyNpmId)` + `AddPackage(record.Id, recommended/min)`
- `LegacyAssets` → for each `detectedLegacyPaths` entry: `BackupAndDeletePath(path)` + `AddPackage(record.Id, recommended/min)`
- `Conflict` → both: backup-and-delete each detected path + `RemovePackage(detectedLegacyNpmId)` (if any) + `AddPackage(record.Id, recommended/min)`

For each selected `ExternalScanResult`:
- `NotInstalled` → `AddScopedRegistry` (or `AddScopeToRegistry` if URL already exists with different scope) + `AddPackage(record.Id, "latest"? leave version blank?)` — see "Version selection for externals" below
- `ScopeMissing` → `AddScopeToRegistry`
- `UpmCurrent` → no actions

For each selected `UninstallScanResult` (state always `InstalledNeedsRemoval` if visible):
- `RemovePackage(legacyNpmId)` — only

**Ordering rule** (matters for split-replace and conflict cleanup):
1. All `BackupAndDeletePath` first (snapshot before any manifest change).
2. All `RemovePackage` next (legacy ids out before canonical ids in, to avoid a moment of duplicate manifest entries).
3. All `AddScopedRegistry` / `AddScopeToRegistry`.
4. All `AddPackage` / `UpdatePackageVersion`.

Within each group, source-order preserved.

**Deduplication:** if the same `RemovePackage(x)` is generated by two different selected items (e.g. firebase.base split-replace), the planner outputs it ONCE. Idempotent action set.

### Version selection for externals

Catalog's `ExternalRecord` doesn't carry a version field today — it only describes scope. For `AddPackage` actions on externals, the planner needs a version source. Options for the implementer:
- Extend `ExternalRecord` with `RecommendedVersion` / `MinVersion` mirroring `PackageRecord`. Cleanest.
- Add `"version"` field. Minimal.

Pick whichever fits — describe choice in the implementer's report. Without a version, `AddPackage` is meaningless for externals; treat that as plan-level validation: emit a `MigrationAction` only if version is available, otherwise produce a `PlannerWarning` (separate output list).

### Phase 4a deliverables

- `Editor/Catalog/Catalog.cs` — add `UninstallRecord` class + `Catalog.Uninstall` property.
- `Editor/Scanner/ScanReport.cs` — add `UninstallScanResult`, `UninstallState`, `ScanReport.Uninstalls`.
- `Editor/Scanner/Scanner.cs` — populate uninstalls; include them in hash.
- `Editor/Migrator/MigrationAction.cs` — action type hierarchy.
- `Editor/Migrator/MigrationPlanner.cs` — pure planner.
- `Editor/Migrator/PlannerWarning.cs` — warning data type (optional, only if implementer needs it).
- **No** touching `InstallerWindow.cs`, `Bootstrap.cs`, `MetadataAutoInstall.cs` in 4a.
- **No** I/O code, no `File.Delete`, no `Client.Add` calls. Pure logic only.

### Phase 4a acceptance

- `MigrationPlanner.Plan` is deterministic and side-effect-free.
- Scanner exposes uninstall scan results and includes them in hash.
- Catalog parses an `uninstall` array if present; tolerates absence (backward-compat).
- No UI changes; existing `InstallerWindow` continues to work unchanged (it doesn't read uninstalls yet — that's 4b).

### Phase 4a commit policy

- `feat(catalog): add UninstallRecord for uninstall-only legacy migration`
- `feat(scanner): emit UninstallScanResult and include in hash`
- `feat(migrator): MigrationPlanner + action types (pure)`

---

## Phase 4b — Backup, Apply, Rollback, UI (sub-agent #2)

Phase 4b assumes 4a is merged. Adds the side-effect machinery and wires it into the UI.

### Backup format

Backups live under `<project>/Library/InstallerBackups/<unix-ts>/`:

```
Library/InstallerBackups/1716308400/
  manifest.json.snapshot         <- copy of manifest.json BEFORE the apply
  operations.json                <- machine-readable list of applied actions (for rollback)
  Assets/Firebase/...            <- copies of paths deleted by BackupAndDeletePath
  Assets/Game/Scripts/CustomAnalytics/AnalyticsFirebaseBridge.cs
```

`operations.json` lists each `MigrationAction` that was actually executed (in execution order), with action type, params, and a success flag. Rollback replays the inverse.

The `Library/` folder is git-ignored by Unity default — backups won't pollute commits.

Backups are kept indefinitely. Implementer doesn't need to add a cleanup policy (out of scope).

### ManifestWriter

Generalise the JObject mutation pattern from `MetadataAutoInstall.EnsureScopedRegistry`:

```
namespace PSV.Installer.Migrator
{
    internal static class ManifestWriter
    {
        public static void ApplyActions(string manifestPath, IEnumerable<ManifestMutation> mutations);
    }
}
```

`ManifestMutation` is the subset of `MigrationAction` that touches manifest.json (`AddPackage`, `RemovePackage`, `UpdatePackageVersion`, `AddScopedRegistry`, `AddScopeToRegistry`). The writer applies all of them to one in-memory `JObject` and writes it back exactly once.

Idempotent: applying the same mutation set twice yields the same manifest.

Preserves unknown fields and key order best-effort (Newtonsoft default JObject behaviour).

Calls `AssetDatabase.Refresh()` after write so UPM sees the change.

### Migrator orchestrator

```
namespace PSV.Installer.Migrator
{
    public sealed class Migrator
    {
        public ApplyResult Apply(IReadOnlyList<MigrationAction> plan);
        public RollbackResult RollbackLast();
        public bool HasBackup { get; }
        public DateTime? LastBackupTime { get; }
    }
}
```

`Apply(plan)`:
1. Create new `Library/InstallerBackups/<ts>/`.
2. Copy current `Packages/manifest.json` into `manifest.json.snapshot`.
3. For each `BackupAndDeletePath` action: copy file/dir into backup tree preserving relative path, then delete original (via `AssetDatabase.DeleteAsset` if under `Assets/`, plain `File.Delete`/`Directory.Delete` otherwise — but in practice all our targets are under `Assets/`).
4. Pass remaining `ManifestMutation` actions to `ManifestWriter.ApplyActions`.
5. Write `operations.json` with the full executed sequence.
6. Return `ApplyResult` (success bool, count of actions executed, list of failures if any).

If any step fails mid-apply, the orchestrator stops, leaves backup intact, and reports. Caller can use `RollbackLast` to recover.

`RollbackLast`:
1. Find newest backup directory under `Library/InstallerBackups/`.
2. Read `operations.json`, restore `manifest.json` from snapshot (atomic file copy).
3. For each `BackupAndDeletePath` in the operations log, copy backup tree contents back into `Assets/`.
4. Call `AssetDatabase.Refresh()`.
5. Move the backup directory to `Library/InstallerBackups/.consumed/<ts>/` so it's not the "newest" anymore but still available for forensics.

`HasBackup` is true when at least one non-`.consumed` backup directory exists.

### UI wiring

`InstallerWindow.cs`:
- `Apply selected` button: no longer in `EditorGUI.DisabledScope`. On click: build `MigrationAction` list via `MigrationPlanner.Plan(catalog, report, currentSelection)`, show modal confirmation (`EditorUtility.DisplayDialog`) summarising actions, on confirm call `Migrator.Apply(plan)`, then re-run scan and refresh window. Tooltip updates to describe what'll happen.
- `Rollback last` button: enabled only when `migrator.HasBackup`. On click: confirmation dialog, then `Migrator.RollbackLast()`, then re-run scan.
- The new `UninstallScanResult` entries surface in the body as a third section (after External): "To uninstall (legacy, no replacement)". Same row shape — display name (legacy id), state, detected version, checkbox.

Both buttons disabled while an operation is in flight (`_busy` flag).

### Phase 4b deliverables

- `Editor/Migrator/ManifestWriter.cs`
- `Editor/Migrator/Backup.cs` (folder snapshot + restore helpers)
- `Editor/Migrator/Migrator.cs` (orchestrator with Apply / RollbackLast)
- `Editor/Migrator/OperationsLog.cs` (typed JSON for operations.json + ApplyResult)
- `Editor/Ui/InstallerWindow.cs` — enable both buttons, wire to Migrator, add uninstall section, busy flag
- `Editor/Ui/InstallerWindowReportView.cs` — render uninstall section
- `Editor/MetadataAutoInstall.cs` — refactor to delegate manifest mutation to `ManifestWriter` (DRY)

### Phase 4b acceptance

- `Apply selected` runs a real migration on the dev project (caveat: dev project shouldn't be used as live test — verify on `sandboxtestproject`).
- `Rollback last` reverses the most recent Apply: manifest restored, deleted Assets/ paths copied back.
- Apply during in-flight operation is blocked (busy flag).
- Window auto-refreshes report after Apply or RollbackLast.
- Uninstall section visible in body when there are uninstall scan results.

### Phase 4b commit policy

- `feat(migrator): ManifestWriter + Backup primitives`
- `feat(migrator): Migrator orchestrator with Apply and RollbackLast`
- `refactor(metadata-autoinstall): use ManifestWriter for scoped-registry add`
- `feat(ui): wire Apply / Rollback to Migrator, render uninstall section`

---

## Out of Scope (across both 4a and 4b)

- Populating real catalog data (`packages: []`, `uninstall: []`) with PSV records — that's a separate session with Alexandr walking through each package.
- Unit tests — same calibration rule as Phase 2/3 (infrastructure first).
- Backup retention / cleanup policy.
- Multi-step undo (only `RollbackLast` — one level).
- Concurrent operations (single-threaded, busy-flag gate).
- Test matrix runs against `sandboxtestproject` (Phase 7).
- Distribution / publish (Phase 6).

## Verification (manual, by Alexandr)

After 4a lands:
1. Unity recompiles, no errors.
2. Open installer window — looks identical to Phase 3 (no UI changes yet).
3. `Run Scan (Debug)` still works.
4. Optionally add an `uninstall` block to `dev/Packages/com.psvgamestudio.installer.metadata/catalog.json` with a fake legacy id like `"com.fake.example"`; re-run scan; observe that nothing crashes (the id isn't in manifest, no scan result emitted).

After 4b lands:
1. Unity recompiles, no errors.
2. Installer window's `Apply selected` and `Rollback last` are no longer disabled.
3. Add a fake `PackageRecord` to catalog (e.g. dummy `com.example.test`); window shows it as `NotInstalled`; check the checkbox, click `Apply selected`; confirm dialog appears summarising the `AddPackage` action; on confirm, `manifest.json` gains the entry, window re-scans and shows the package as `UpmCurrent`.
4. Click `Rollback last`; confirm; manifest reverts; window shows the package as `NotInstalled` again.
5. `Library/InstallerBackups/<ts>/` and `Library/InstallerBackups/.consumed/<ts>/` both exist with `manifest.json.snapshot` and `operations.json`.

## Sub-phase Sequencing for Subagent Dispatch

Controller process:
1. Dispatch implementer #1 with Phase 4a section as scope. Brief states 4a is pure logic only — no I/O, no UI edits.
2. On DONE: controller reviews diff, verifies the planner is testable in isolation, confirms scanner+catalog changes don't regress Phase 2/3 behaviour.
3. Dispatch implementer #2 with Phase 4b section as scope. Brief states 4a is already done — pure planner and uninstall scanner are available — and 4b adds backup, apply, rollback, and UI wiring on top.
4. On DONE: controller reviews; user opens Unity and runs the verification steps above.

---

## Self-Review Notes for the Implementers

**4a:**
- `MigrationPlanner.Plan` must not throw on null/empty inputs — return empty action list.
- Action ordering (paths → removes → registry adds → package adds) is part of the deterministic contract.
- Catalog parsing of missing `uninstall` field must not produce a warning — it's optional.

**4b:**
- `Apply` is atomic at the manifest level (one read, one write) but not at the `BackupAndDeletePath` level (each file/dir is its own op). Acceptable — if one delete fails, manifest changes haven't happened yet.
- Backup directory naming: pure unix timestamp seconds (`{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}`) — sortable, collision-free at human timescale.
- `RollbackLast` is single-level; trying twice in a row rolls back the second-newest backup, not a no-op. (User intent: each Rollback undoes one Apply.)
