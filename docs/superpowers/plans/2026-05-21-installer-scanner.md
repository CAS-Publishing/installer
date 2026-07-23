# Installer Scanner Implementation Plan (Phase 2)

> **For agentic workers:** high-level spec. No code dictation. Implementer has full authority over class names, internal helpers, error-handling style, version-comparison details, and any structural choices below `Scanner.Scan` and `ScanReport`. The contract is acceptance criteria + public API + file boundaries — that's it.

**Goal:** produce `ScanReport` — a structured snapshot of every catalog package's state inside the current Unity project. The report is the input both for Phase 3 (UI) and Phase 4 (Migrator).

**Architecture:** new `Editor/Scanner/` subsystem inside the existing `PSV.Installer.Editor` assembly. Pure data flow: loaded `Catalog` → probes (`manifest.json`, `Assets/`) → classifier → `ScanReport`. No mutation of the project, no UI, no auto-trigger from `Bootstrap` — the API is offered, callers wire it in Phase 3.

**Tech stack:** same as Phase 1 (Unity 2022.3 Editor-only, C# 9, Newtonsoft.Json already available, `UnityEditor.PackageManager.PackageInfo` if helpful).

---

## Acceptance Criteria

### `Scanner.Scan(Catalog) → ScanReport`

For each `PackageRecord` in `catalog.Packages`, produce one `PackageScanResult` whose `State` is exactly one of:

| State | When |
|---|---|
| `NotInstalled` | Not in manifest under `id` or any `legacyNpmIds`, and no `legacyAssetPaths` exist under `Assets/`. |
| `UpmCurrent` | In manifest under `id`, version ≥ `recommendedVersion`. (If `recommendedVersion` is null/empty, any version ≥ `minVersion` counts.) |
| `UpmOutdated` | In manifest under `id`, version < `recommendedVersion` (but ≥ `minVersion`). |
| `UpmBelowMin` | In manifest under `id`, version < `minVersion`. |
| `LegacyUpm` | In manifest under one of `legacyNpmIds`, regardless of version. |
| `LegacyAssets` | One or more `legacyAssetPaths` exist under `Assets/`, manifest has neither current id nor any legacy id. |
| `Conflict` | Manifest has current id AND a legacy id, OR manifest has any id AND legacy paths also present. |

The result must include the detected version string (when applicable) and the list of legacy paths that actually exist on disk (when applicable). When `minVersion`/`recommendedVersion` are null, treat them as "no constraint" (never trigger `UpmOutdated`/`UpmBelowMin`).

For each `ExternalRecord` in `catalog.External`, produce one `ExternalScanResult`:

| State | When |
|---|---|
| `NotInstalled` | Not in manifest dependencies. |
| `UpmCurrent` | In manifest dependencies. |
| `ScopeMissing` | In manifest dependencies BUT none of `scopes` are registered in any `scopedRegistries` block. Anomaly worth reporting. |

### Report-level fields

- `CatalogVersion` — copy from input catalog (string)
- `ScannedAtUtc` — DateTime
- `Hash` — stable hash of the report's package + external states (order-independent, version-independent of timestamp). Used by Phase 3 to mute the auto-popup when nothing has changed between Unity loads.

### Public API surface (exact names)

```
namespace PSV.Installer.Scanner
{
    public static class Scanner
    {
        public static ScanReport Scan(Catalog catalog);
    }

    public sealed class ScanReport
    {
        public string CatalogVersion { get; }
        public DateTime ScannedAtUtc { get; }
        public IReadOnlyList<PackageScanResult> Packages { get; }
        public IReadOnlyList<ExternalScanResult> External { get; }
        public string Hash { get; }
    }

    public sealed class PackageScanResult { /* id, displayName, state, detectedVersion, detectedLegacyPaths, detectedLegacyNpmId, recommendation? */ }
    public sealed class ExternalScanResult { /* id, displayName, state, detectedVersion */ }
    public enum PackageState { NotInstalled, UpmCurrent, UpmOutdated, UpmBelowMin, LegacyUpm, LegacyAssets, Conflict }
    public enum ExternalState { NotInstalled, UpmCurrent, ScopeMissing }
}
```

Everything else — internal class layout, helper methods, error styles, how the hash is computed (SHA-256, xxhash, FNV — your call as long as it's stable across runs) — is implementer's choice.

---

## File Structure (recommended; you may reshape)

- `Editor/Scanner/Scanner.cs` — entry point
- `Editor/Scanner/ScanReport.cs` — report POCO + nested result types + state enums
- `Editor/Scanner/ManifestProbe.cs` — read & parse client `Packages/manifest.json`, expose `{ id → version }` dictionary + `scopedRegistries` info
- `Editor/Scanner/AssetProbe.cs` — for a set of candidate paths, return the ones that exist under `Assets/`
- `Editor/Scanner/StateClassifier.cs` — pure: given a `PackageRecord` + probe data, return `PackageScanResult`. Same for external.

If you find a cleaner decomposition (e.g. merge classifier into Scanner if it's trivial, split probes differently) — fine. The point is one responsibility per file.

---

## Constraints

- **No tests in this pass.** Infrastructure tests come after Phase 1+2 work end-to-end inside Unity (deliberate decision — recorded in `feedback_avoid_overceremony.md`).
- **No auto-trigger.** Do NOT modify `Bootstrap.cs` to call `Scanner.Scan(...)`. The wiring happens in Phase 3 when there's a UI to receive the report.
- **No mutation.** Scanner is read-only on the project. Reading `manifest.json` and probing `Assets/` is fine; writing anything is out of scope.
- **Editor-only.** Compile under the existing `PSV.Installer.Editor` asmdef. No platform-specific code.
- **No new external dependencies.** Newtonsoft.Json and `UnityEditor.PackageManager` are already available.
- **Path roots.** The client project root is `Path.GetFullPath(Path.Combine(Application.dataPath, ".."))`. `Assets/` is `Application.dataPath`. `Packages/manifest.json` is `<root>/Packages/manifest.json`.
- **Version comparison reuse.** `CatalogUpdater.IsNewer` already exists for semver-ish strings — reuse it (and expose it more publicly if you need to). Don't duplicate the logic.

---

## Out of Scope

- UI for displaying the report (Phase 3)
- Persisting the hash in `EditorPrefs` for the mute-popup logic (Phase 3)
- Performing migrations (Phase 4)
- Backup / rollback (Phase 4)
- Tests of any kind (later)
- Auto-running scanner on Unity load (Phase 3)

---

## Verification (manual, by Alexandr after implementation)

1. Open `E:\workspace\casai\dev` in Unity 2022.3.62f3.
2. Expect clean compile (no red in Console).
3. Run `Tools/PSV Installer/Run Scan (Debug)` (or whatever menu item the implementer chose to add for ad-hoc debugging — see "Debug entry point" below).
4. Expect a Console line dumping the report (JSON is easiest). With the current empty catalog (`packages: []`, one external CAS entry), the report should show 0 packages and 1 external entry in state `NotInstalled` (the dev project doesn't have CAS on OpenUPM installed).

### Debug entry point

The implementer **may** add a temporary `[MenuItem("Tools/PSV Installer/Run Scan (Debug)")]` that calls `Scanner.Scan` against the loaded catalog and dumps the report to Console. This is to make verification easy and will be removed in Phase 3 when the real UI takes over. Keep it small and in its own file (e.g. `Editor/Scanner/_DebugMenu.cs`) so it's obvious it's scaffolding.

---

## Commit Policy

Per logical unit, not one mega-commit:
- Probes (manifest + asset) — one commit
- Classifier + report types — one commit
- Top-level Scanner + Hash + debug menu — one commit

Conventional Commits: `feat(scanner): ...`.

---

## Self-Review for the implementer

Before reporting DONE:
- Every catalog `PackageRecord` field is actually consulted (id, legacyNpmIds, legacyAssetPaths, minVersion, recommendedVersion). If you ignored one, justify it in the report.
- Hash is stable: same project state → same hash, even after a Unity restart or list reordering.
- No try/catch swallowing all exceptions silently; surface parsing errors with `Debug.LogWarning` and a clear prefix.
- No Unity main-thread blocking I/O on large directory walks (probe shallow — exact paths from catalog, not `**` globs).
