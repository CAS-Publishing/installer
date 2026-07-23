# Installer Window Redesign — Per-Package Version Target + Split-Migration Grouping

> **For agentic workers:** high-level spec. No code dictation below the public surface. Implementer chooses class layout, IMGUI helper extraction, persistence keys.

**Goal:** redesign the installer window so the user can see **installed / min / recommended versions** for every catalog entry on one row, choose **per-package** which version to install (Min or Recommended), and surface **split-replacement migrations** (one legacy id → N new packages) as a dedicated top section so partial selection is visually obvious.

**Why:** today the planner hardcodes `RecommendedVersion ?? MinVersion`. Clients with stability constraints (other SDK pinning, schedule risk) cannot opt into the safer minimum. And split migrations like `com.psv.firebase.base → analytics + remoteconfig` look like two independent rows — partial selection silently breaks the project (rainbow-high-beauty-salon, 2026-05-21).

**Architecture:** changes spread across three subsystems:
- `Editor/Ui` — rewrite `InstallerWindowReportView` row layout, replace category foldouts with **action-oriented groups** (`To install`, `To update`, `To migrate`, `To uninstall`, `Up to date`), add Pending Split section, persist per-package target choice.
- `Editor/Migrator` — extend `ISelectionSet` to carry version target, planner reads target instead of hardcoding, planner emits new warnings for partial split migration.
- `Editor/Scanner` — derive `MigrationGroup` metadata from existing `legacyNpmIds` (no catalog schema change).

**Why action-groups, not categories:** at the current scale (<30 packages across all categories combined) the category foldouts (`Core`, `Analytics`, `Ads`, `IAP`, `Crash`, `Debug`) average 1–2 rows each — they cost more vertical space than they save. The client's mental model is "what do I need to **do**?", not "what kind of package is this?". Action-grouping answers the first question directly. Categories can be reintroduced as a filter later if the catalog grows past ~30 entries.

**Tech stack:** unchanged. Unity 2022.3 IMGUI (`EditorGUILayout`), `EditorPrefs` for muting, Newtonsoft.Json (already pulled by metadata).

---

## Acceptance Criteria

### Window structure (top to bottom)

```
┌─ Toolbar ────────────────────────────────────────────────────┐
│ [Refresh catalog]  [Run scan]  [Apply selected]              │
├─ Body (scroll view) ─────────────────────────────────────────┤
│  ▼ Pending split migrations  (only when groups exist)        │
│    Replaces com.psv.firebase.base (2 packages):              │
│      [ ] PSV.Analytics      [Legacy UPM]  →  ( Min │ Rec )   │
│      [ ] PSV.RemoteConfig   [Legacy UPM]  →  ( Min │ Rec )   │
│                                                              │
│  ▼ To install   (NotInstalled — PSV + External)              │
│      [ ] Firebase Analytics    [Add]      → ( Min │ Rec )    │
│      [ ] CAS Mediation         [Add]      → ( Min │ Rec )    │
│                                                              │
│  ▼ To update    (UpmOutdated / UpmBelowMin)                  │
│      [ ] PSV.Foo   [Outdated]    1.0.0   → ( Min │ Rec )     │
│      [ ] PSV.Bar   [Below min⚠]  0.0.5   → ( Min │ Rec )     │
│                                                              │
│  ▼ To migrate   (LegacyUpm / LegacyAssets / Conflict /       │
│                  External ScopeMissing)                      │
│      [ ] PSV.Edm   [Legacy UPM]  com.psv.unity.edm           │
│                                                              │
│  ▼ To uninstall (UninstallRecord — legacy w/ no replacement) │
│      [ ] com.psv.legacy.edm     [Needs removal]              │
│                                                              │
│  ▶ Up to date   (UpmCurrent + External UpmCurrent, 3)        │
│                  collapsed by default                        │
├─ Status bar ─────────────────────────────────────────────────┤
│ Catalog v0.0.2-preview.1 · Last scan: ... · N need attention │
└──────────────────────────────────────────────────────────────┘
```

**Group ordering (fixed):** Pending split → To install → To update → To migrate → To uninstall → Up to date. Sections are skipped (not rendered) when their list of rows is empty, **except** Up to date which always renders so the user has a single place to find what is currently in their project.

**Action-group → state mapping:**

| Section          | Includes                                                                  |
| ---------------- | ------------------------------------------------------------------------- |
| Pending split    | Any catalog package whose state is `LegacyUpm`/`LegacyAssets`/`Conflict`/`NotInstalled` AND whose record shares a `legacyNpmId` with another catalog package |
| To install       | `PackageState.NotInstalled` + `ExternalState.NotInstalled` (only those NOT already in Pending split) |
| To update        | `PackageState.UpmOutdated` + `PackageState.UpmBelowMin`                   |
| To migrate       | `PackageState.LegacyUpm` + `PackageState.LegacyAssets` + `PackageState.Conflict` + `ExternalState.ScopeMissing` (only those NOT already in Pending split) |
| To uninstall     | `UninstallScanResult` with `InstalledNeedsRemoval` (unchanged from existing) |
| Up to date       | `PackageState.UpmCurrent` + `ExternalState.UpmCurrent` (read-only rows, collapsed foldout with `(N packages)` count in the header) |

**Precedence (same id never appears twice):** Pending split > To install > To update > To migrate > To uninstall > Up to date. First match wins.

### Single-row layout (PSV packages + External — uniform)

```
[chk]  Name [State]               Installed   →    Target switch                 Extra
 ☑     CAS Mediation [Outdated]   4.5.4       →    [ Min 4.5.4 │ Rec 4.7.0 ]
 ☐     Firebase Analytics [Add]      —        →    [ Min 1.0.0 │ Rec 1.2.0 ]
 ─     PSV.Debug [Current ✓]       0.2.0           at recommended — no action
 ☑     Old.Package [Below min ⚠]   0.0.5       →   [ Min 1.0.0 │ Rec 1.5.0 ]   ⚠
```

**Columns** (in IMGUI horizontal scope, indent reset for clickability — see `b06703b` and the follow-up):
- Selection checkbox + clickable name (`EditorGUILayout.ToggleLeft` with bold label, MinWidth 200)
- State badge (colored rich label, MinWidth 100)
- Installed version (60px) — `—` when not installed
- `→` separator + Target switch (160px) — `GUILayout.Toolbar` as segmented control, two options `Min X.Y.Z` and `Rec A.B.C`
- Extra column for warnings / legacy-paths hit count (flex)

### State-driven row behaviour

| State                | Checkbox | Target switch                                         | Notes                                                              |
| -------------------- | -------- | ----------------------------------------------------- | ------------------------------------------------------------------ |
| `NotInstalled`       | enabled  | Min / Rec — default Rec                               | Installed column shows `—`                                         |
| `UpmCurrent`         | disabled | replaced with text "at recommended — no action"       | No action available                                                |
| `UpmOutdated`        | enabled  | Min / Rec — default Rec                               | Choosing Min when installed ≥ Min → no-op in plan (silent)         |
| `UpmBelowMin`        | enabled  | Min / Rec — default Rec, ⚠ marker                     | Min selection = force upgrade to minimum                           |
| `LegacyUpm`          | enabled  | Min / Rec — default Rec                               | Plan: remove legacy id + add canonical at target                   |
| `LegacyAssets`       | enabled  | Min / Rec — default Rec                               | Plan: delete legacy paths + add canonical                          |
| `Conflict`           | enabled  | Min / Rec — default Rec, ⚠ marker                     | Plan: delete paths + remove legacy id + add canonical              |
| External `NotInstalled` | enabled | Min / Rec — default Rec                            | Plus scope registration                                            |
| External `ScopeMissing` | enabled | hidden (no version action, only scope registration)| Switch hidden                                                      |
| External `UpmCurrent` | disabled | hidden                                              | No action                                                          |
| Uninstall record     | enabled  | replaced with "remove (no replacement)"               | Existing section, single column                                    |

### Pending split migrations section

**Definition of a split group:** any legacy npm id that appears in `legacyNpmIds` of **two or more** catalog `PackageRecord`s. Computed by the scanner from the existing catalog — no new schema field.

**Visibility rule:** section only shown when at least one split group has at least one member row in the report with an actionable state (`LegacyUpm`, `LegacyAssets`, `Conflict`, or `NotInstalled`). Pure `UpmCurrent`/`UpmOutdated` split groups are NOT surfaced — they live in their normal sections.

**Rendering:** sub-header `Replaces <legacyId> (<N> packages):`, then **all member rows of that group** (including the ones already at `UpmCurrent`) — full membership gives the user context for "what's still missing vs. what's already there". Members already at `UpmCurrent` render with checkbox disabled and a `✓ installed` badge, no target switch.

Each member row is rendered **only here** when a split group is active — it is NOT duplicated inside `To install` / `To migrate` / `Up to date`, to avoid visual confusion and double-tick traps. The exclusion rule is baked into the action-group → state mapping table above.

### Apply summary modal — additions

Existing modal stays, two additions:

1. Per-action version target is named:
   ```
   • Add 1 package(s): com.psvgamestudio.analytics@4.7.0 (recommended)
   • Update 1 package(s): com.psv.foo: 0.0.5 → 1.0.0 (minimum)
   ```

2. **Partial-split warning** — when a `RemovePackage(legacyId)` action is in the plan but **not all** sibling replacements of that legacyId are selected:
   ```
   ⚠ Warning: split migration partial
     com.psv.firebase.base is replaced by 2 packages:
       Selected: PSV.Analytics
       NOT selected: PSV.RemoteConfig
     Continuing will leave the project without RemoteConfig.
     Continue anyway?
   ```
   Two buttons: `Continue anyway`, `Cancel`. Cancel returns to the window with selection intact.

### Public surface — `Editor/Migrator/MigrationPlanner.cs`

```csharp
namespace PSV.Installer.Migrator
{
    public enum VersionTarget { Recommended, Min }

    public interface ISelectionSet
    {
        bool IsSelected(string id);

        // Returns the chosen target for the id. For ids not in the selection set,
        // returns Recommended (the safe default). Planner must call this only for
        // selected ids — calling for unselected is allowed and returns Recommended.
        VersionTarget GetTarget(string id);
    }
}
```

The planner resolves version per-record as:
- `target == Recommended` → `record.RecommendedVersion ?? record.MinVersion`
- `target == Min` → `record.MinVersion ?? record.RecommendedVersion`

`PlannerWarning` gains a structured kind for partial split (so the UI can render it as a confirm modal, not a status-bar warning):

```csharp
public sealed class PartialSplitWarning : PlannerWarning
{
    public string LegacyId { get; }
    public IReadOnlyList<string> SelectedSiblings { get; }
    public IReadOnlyList<string> UnselectedSiblings { get; }
}
```

Existing `PlannerWarning` keeps its string-message shape — `PartialSplitWarning` extends, doesn't replace.

### Public surface — `Editor/Scanner/ScanReport.cs`

`ScanReport` gains:

```csharp
public sealed class MigrationGroup
{
    public string LegacyId { get; }
    public IReadOnlyList<string> PackageIds { get; }   // catalog Ids of the N replacements
}

public sealed class ScanReport
{
    // existing fields…
    public IReadOnlyList<MigrationGroup> SplitGroups { get; }
}
```

`SplitGroups` is derived in `ProjectScanner.Scan` by grouping `catalog.Packages` on each `legacyNpmId` and emitting only groups with `Count() >= 2`.

The existing scan hash is extended to include `SplitGroups` ids so adding a new split group triggers auto-popup.

### Window state persistence

`_selected: List<string>` already serialised — keep.

Add `_targets: List<TargetEntry>` parallel to `_selected`:

```csharp
[Serializable]
internal struct TargetEntry
{
    public string Id;
    public bool IsMin;   // false = Recommended (the default)
}
```

`List<TargetEntry>` is Unity-serializable through `[SerializeField]`. Membership lookup is O(N) — fine at ≤50 entries.

Default behaviour: an id present in `_selected` but absent from `_targets` resolves to `Recommended`. The UI only adds a `TargetEntry` when the user actually picks Min — keeps the serialised state minimal.

### Compilation order changes

- `MigrationPlanner.Plan(...)` signature gains no parameters (signature unchanged), but the `ISelectionSet` it consumes acquires a new method. All existing call sites (UI and tests) must implement `GetTarget`.
- `InstallerWindow.HashSetSelectionAdapter` (the adapter in `InstallerWindow.cs`) implements both methods using the window's `_selected` and `_targets` fields.

---

## What this design explicitly does NOT do

- **No catalog schema change.** `legacyNpmIds` already conveys split groups; no new `migrationGroup` field, no new `replacesGroup` field. Derive at scan time.
- **No auto-tick siblings.** User selects exactly what they tick; planner warns on partial split at Apply time. Auto-tick was rejected as "too magic" — visible warning is sufficient.
- **No per-target per-version dropdown beyond Min/Rec.** No "install version X.Y.Z" arbitrary picker. Two-choice segmented control only.
- **No category foldouts.** Categories (`Core`, `Analytics`, `Ads`, …) are removed from the installer window. Catalog still has `category` field — preserved for future use (e.g. registry-side grouping) but not rendered. The action-group structure replaces them at this scale.
- **No version downgrade flow for Current packages.** `UpmCurrent` rows are read-only in `Up to date`. If a client wants to downgrade, they edit manifest manually.
- **No "show installed only" / "show outdated only" filters.** Out of MVP. The action grouping serves the same purpose: the user opens the relevant foldout to see only what fits the action they want.

---

## Files affected (estimate)

| File                                                 | Change                                                                 |
| ---------------------------------------------------- | ---------------------------------------------------------------------- |
| `Editor/Ui/InstallerWindow.cs`                       | Add `_targets` SerializeField, extend `HashSetSelectionAdapter`        |
| `Editor/Ui/InstallerWindowReportView.cs`             | Rewrite row layout; replace `DrawPackageCategories`/`DrawExternalSection` with `DrawActionGroup` (one method, parameterised by section); add `DrawPendingSplitSection`; exclude split rows from `To install` / `To migrate` |
| `Editor/Migrator/MigrationPlanner.cs`                | Read target from ISelectionSet, emit `PartialSplitWarning`             |
| `Editor/Migrator/MigrationPlanner.cs` (interface)    | Add `VersionTarget` enum, extend `ISelectionSet`                       |
| `Editor/Migrator/PlannerWarning.cs`                  | Add `PartialSplitWarning` derived type                                 |
| `Editor/Scanner/Scanner.cs`                          | Compute `SplitGroups` and include in `ScanReport`                      |
| `Editor/Scanner/ScanReport.cs`                       | Add `MigrationGroup` type + `SplitGroups` property + hash inclusion    |

No new `.asmdef`. No new test asmdef.

---

## Out-of-scope follow-ups (track separately)

- "Show all installed" filter for Current packages (deferred until window grows past ~20 rows).
- Auto-init of CAS / Tenjin in preload (existing deferred item from MVP 1.0.1).
- Per-package custom version input (free-form version picker beyond Min/Rec).
- Catalog population with real PSV packages (separate session per Alexandr's "infrastructure first" rule).
