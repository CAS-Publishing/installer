# Installer UI Implementation Plan (Phase 3)

> **For agentic workers:** high-level spec. No code dictation below the public API contract. Implementer decides class layout, IMGUI structure, persistence keys, and any helper extraction.

**Goal:** give the user a real surface for the installer — an `EditorWindow` that renders the current `ScanReport` and auto-pops up when something actionable has changed since the user's last look. No mutation buttons are active yet (those belong to Phase 4 — migrator); they exist as disabled placeholders so the layout is final and the UX is testable.

**Architecture:** new `Editor/Ui/` subsystem under namespace `PSV.Installer.Ui`. IMGUI-based (`EditorGUILayout`) — faster to iterate than UI Toolkit at this stage, and the installer's UI is fundamentally tabular. `Bootstrap` gets one more call after a successful catalog load: run the scanner, compare the report hash with the last-shown one (stored in `EditorPrefs`), and surface the window only when the hash actually changed. Reuses `Scanner.Scan`, `ScanReport`, `CatalogLoader` unchanged.

**Tech stack:** Unity 2022.3 Editor, IMGUI (`EditorGUILayout`, `EditorWindow`), `EditorPrefs` for the mute key. No new dependencies.

---

## Acceptance Criteria

### Public surface

```
namespace PSV.Installer.Ui
{
    public sealed class InstallerWindow : EditorWindow
    {
        public static void Show();                        // opens or focuses the window
        public static void ShowIfReportChanged(ScanReport report);  // opens only if hash != last-shown
    }
}
```

Menu items:
- `Tools/PSV Installer/Open Installer Window` → `InstallerWindow.Show()`
- `Tools/PSV Installer/Run Scan (Debug)` — **keep existing**; it's an internal escape hatch.

### Window content

**Toolbar (top):**
- `Refresh catalog` — re-runs `CatalogUpdater.CheckRemoteLatestVersion` and, if newer, `CatalogUpdater.InstallVersion`. After UPM resolves, the window picks up the new catalog on its next paint cycle.
- `Run scan` — recomputes a fresh `ScanReport` and re-renders. Updates the "last shown" hash.
- `Apply selected` — **DISABLED**. Tooltip: "Migrator coming in Phase 4."
- `Rollback last` — **DISABLED**. Tooltip: "Backup / rollback coming in Phase 4."

**Body — grouped by category:**
- One foldout per category from `Catalog.Categories` (display name). Empty categories are hidden.
- Each row inside a category foldout:
  - `[✓]` checkbox — selectable but Apply is disabled, so it's purely visual for now
  - `DisplayName` — bold
  - `State` — coloured / styled string: red for `Conflict`/`UpmBelowMin`, yellow for `UpmOutdated`/`LegacyUpm`/`LegacyAssets`/`ScopeMissing`, green for `UpmCurrent`, grey for `NotInstalled`
  - `Detected version` — small grey text if applicable
  - `Legacy hits` — count of legacy paths/ids found, expanded on hover or via a fold-in row
- A separate `External` foldout at the bottom for `ExternalScanResult` entries — same row shape minus the categories grouping (or grouped by category, implementer's call).

**Status bar (bottom):**
- `Catalog v{X}` · `Last scan: {timestamp}` · `{N} item(s) need attention` (count of non-`UpmCurrent`/non-`NotInstalled` states).

### Auto-open behaviour

In `Bootstrap.RunOnce`, after the existing `CatalogLoader.Load` success branch (and after `CatalogUpdater.CheckRemoteLatestVersion` is dispatched), additionally:
1. Run `Scanner.Scan(catalog)`.
2. Read `EditorPrefs.GetString("PSV.Installer.LastShownScanHash", "")`.
3. If the new `ScanReport.Hash` differs from the stored value, call `InstallerWindow.ShowIfReportChanged(report)`.
4. `ShowIfReportChanged` opens the window AND writes the new hash to `EditorPrefs`. Subsequent reloads with the same state stay silent.

The user can always force-open via the menu. Force-open does NOT touch the stored hash (so the silence policy survives a manual peek).

### Refresh / re-scan semantics

`Run scan` button in the toolbar:
- Recomputes the scan
- Updates the displayed report
- **Writes the new hash to `EditorPrefs`** — manual scan counts as "user saw this state", so the auto-popup won't fire again until something else changes.

`Refresh catalog` button:
- Calls into existing `CatalogUpdater.CheckRemoteLatestVersion` → on success, if newer, kicks `Client.Add`. UPM resolution is async; on next OnGUI tick the window detects the new catalog and re-scans automatically.

---

## File Structure (recommended)

- `Editor/Ui/InstallerWindow.cs` — `EditorWindow` lifecycle, menu items, top-level OnGUI dispatcher. Holds the in-memory `ScanReport`, current `PackageCatalog`, selection state.
- `Editor/Ui/InstallerWindowToolbar.cs` (optional) — draws the top toolbar.
- `Editor/Ui/InstallerWindowReportView.cs` (optional) — draws the categorised report.
- `Editor/Ui/ScanReportStore.cs` (optional) — `EditorPrefs` read/write for last-shown hash.
- `Editor/Bootstrap.cs` — extend `RunOnce` to add the scan + ShowIfReportChanged call after the catalog-load success branch.

If splitting feels like premature decomposition, the implementer may keep everything in `InstallerWindow.cs` — but the file should not exceed ~400 lines. If it does, split.

---

## Constraints

- **No tests this pass.** Phase 1+2+3 will be exercised manually first; tests come after the surface has stabilised.
- **Editor-only.** Lives in `PSV.Installer.Editor` asmdef. No assembly changes needed.
- **No mutation of the project.** The window must not write to `manifest.json`, must not delete from `Assets/`, must not call `Client.Add` for any package other than the metadata one via the existing `Refresh catalog` flow.
- **Reuse Scanner.** Do not write a second scanning code path.
- **Auto-popup respects user fatigue.** Hash-stored-in-EditorPrefs gate; never spam the user with the same window twice on the same state.
- **IMGUI, not UI Toolkit.** Faster for tabular prototypes and consistent with existing `_DebugMenu.cs` style.
- **No external assets** (icons, USS, etc). Use built-in `EditorStyles` / `EditorGUIUtility.IconContent` if you want colour cues.

---

## Out of Scope

- Apply / migrator logic (Phase 4).
- Rollback / backup logic (Phase 4).
- Catalog content population (still empty `packages: []` — that's a separate pass, per-package).
- Removing `_DebugMenu.cs`.
- Window state persistence (size, position, fold states) — Unity handles window position automatically via `EditorWindow`; foldouts default open per session is fine.
- Localisation.
- Icons, USS theming.

---

## Verification (manual, by Alexandr)

1. Open `E:\workspace\casai\dev` in Unity. Wait for compile.
2. On the first reload after this patch lands, Console should show the usual `Catalog v0.0.1-preview.1 loaded …` log AND the installer window should auto-open (because the hash is unset → differs from stored "").
3. Window content with the current empty catalog:
   - No category foldouts (categories are populated but `packages` is empty)
   - One row in `External` section: `CAS Mediation`, state `NotInstalled` (or `ScopeMissing` if scope was registered), styled accordingly
   - Status bar: `Catalog v0.0.1-preview.1 · Last scan: <ts> · 1 item(s) need attention`
4. Close the window. Trigger a domain reload (save any .cs file). Window should NOT re-open — hash matches stored.
5. Manually open it again via `Tools/PSV Installer/Open Installer Window`. Window appears, no auto-popup fires.
6. Click `Run scan` in the toolbar — re-runs, same result. Hash overwrites itself (no change).
7. Click `Refresh catalog` — should kick `Client.Add` if Verdaccio has a newer version; otherwise log "catalog up to date" and no-op.
8. Hover over `Apply selected` and `Rollback last` — see disabled buttons + tooltip text.

---

## Commit Policy

Per logical unit:
- `feat(ui): installer window skeleton + menu items`
- `feat(ui): scan report rendering grouped by category`
- `feat(ui): refresh catalog + run scan toolbar actions`
- `feat(ui): auto-popup gated by scan-report hash in EditorPrefs`
- `feat(bootstrap): trigger scan + show window on first changed report`

Conventional Commits.

---

## Self-Review for the Implementer

- `EditorWindow` opens cleanly, repaints without exceptions when packages list is empty.
- Auto-popup hash key is a stable string constant in one place — no scattered literals.
- Disabled buttons use `EditorGUI.DisabledScope` (not just `GUI.enabled` flips) for cleanliness.
- Foldout state survives a single window-open session (use `[SerializeField] bool` fields on the window).
- Status bar text is generated from the report, not hardcoded.
- The `Refresh catalog` button doesn't block the UI thread — fire-and-forget like Bootstrap.
- Bootstrap edit is minimal and doesn't alter the existing happy path's logs.
