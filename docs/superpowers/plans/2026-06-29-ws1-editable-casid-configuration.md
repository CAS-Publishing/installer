# WS-1 — Editable CAS-ID field in Configuration — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Let the user enter/correct the CAS managerId for a platform directly in the Configuration tab (feedback #2.1b), instead of only pinging the asset for editing in the Inspector.

**Architecture:** Mark the CAS `config` rows `editable` in the catalog. `SetupScreen` renders an inline `TextField` (seeded with the current managerId) for editable `settingsAssetField` cells; committing it force-writes the value to the CAS settings asset via a new `CasIdApplier.SetManagerId(platform, value)` and persists it to `InstallerKeyStore`. Other cells stay read-only.

**Tech Stack:** Unity 2022.3 Editor, C# UPM editor package, UI Toolkit, JSON catalog.

**Decision source:** `docs/superpowers/specs/2026-06-29-installer-feedback-round2-decisions.md` (#2.1b).

## Global Constraints

- Only `settingsAssetField` cells flagged `editable: true` in the catalog become editable; everything else stays read-only (click-to-ping, unchanged).
- Committing the field force-overwrites the managerId (even a real existing value), unlike `ApplyPending` which only fills empty/placeholder.
- New catalog field `editable` is optional; absent → not editable (fallback safe).
- No CLI/headless runner: window/asset-write behaviour is OWNER-RUN. Pure helpers are unit-tested.
- Conventional Commits, `feat(installer):` / `chore(metadata):`. Installer repo branch `feat/installer-wizard-ui`; metadata repo branch `chore/cas-pin-4.7.4` (stack here so it ships with the other metadata changes).

---

### Task 1: Catalog model `editable` flag + `CasIdApplier.SetManagerId`

**Files:**
- Modify: `Editor/Catalog/Catalog.cs` (`ConfigRequirement`, after `Hint`)
- Modify: `Editor/Wizard/CasIdApplier.cs` (add `SetManagerId` + a pure helper)
- Test: `Editor/Tests/CasIdApplierWriteTests.cs`

**Interfaces:**
- Produces: `ConfigRequirement.Editable` (bool, json `editable`); `public static void CasIdApplier.SetManagerId(string platform, string value)`; `internal static string CasIdApplier.NormalizeManagerId(string value)` (pure: trims; returns "" for null).

- [ ] **Step 1: Write the failing test**

Create `Editor/Tests/CasIdApplierWriteTests.cs`:

```csharp
using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class CasIdApplierWriteTests
    {
        [Test] public void Normalize_trims() => Assert.AreEqual("abc", CasIdApplier.NormalizeManagerId("  abc  "));
        [Test] public void Normalize_null_is_empty() => Assert.AreEqual("", CasIdApplier.NormalizeManagerId(null));
        [Test] public void Normalize_keeps_inner() => Assert.AreEqual("1234567890", CasIdApplier.NormalizeManagerId("1234567890"));
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Unity Test Runner → EditMode → `CasIdApplierWriteTests`. Expected: FAIL (no `NormalizeManagerId`).

- [ ] **Step 3: Add the catalog `editable` field**

In `Editor/Catalog/Catalog.cs`, inside `ConfigRequirement`, after the `Hint` field, add:

```csharp
        /// <summary>When true, the Configuration screen renders this settings field as an editable
        /// input that writes the value back to the settings asset (e.g. CAS managerId). Default false.</summary>
        [JsonProperty("editable")] public bool Editable;
```

- [ ] **Step 4: Add `SetManagerId` + `NormalizeManagerId` to `CasIdApplier`**

In `Editor/Wizard/CasIdApplier.cs`, add these members to the `CasIdApplier` class (after `ReadExisting`):

```csharp
        /// <summary>Trims the entered managerId; null → empty. Pure/testable.</summary>
        internal static string NormalizeManagerId(string value) => value?.Trim() ?? string.Empty;

        /// <summary>
        /// Force-writes <paramref name="value"/> to the CAS managerId for <paramref name="platform"/>,
        /// overwriting any current value (unlike <see cref="ApplyPending"/>, which only fills
        /// empty/placeholder). Also persists it to the key store so a later reinstall re-applies it.
        /// No-op when CAS isn't installed (its settings asset doesn't exist yet).
        /// </summary>
        public static void SetManagerId(string platform, string value)
        {
            var v = NormalizeManagerId(value);

            var load = CatalogLoader.Load();
            if (load.Status != CatalogLoadStatus.Ok || load.Catalog?.External == null) return;

            ExternalRecord cas = null;
            foreach (var e in load.Catalog.External)
                if (e != null && e.Id == CasId) { cas = e; break; }
            if (cas?.Config == null) return;

            foreach (var req in cas.Config)
            {
                if (req == null || req.Kind != "settingsAssetField" || string.IsNullOrEmpty(req.Field)) continue;
                if (!string.Equals(req.Platform, platform, System.StringComparison.OrdinalIgnoreCase)) continue;

                var asset = SetupChecker.LocateAsset(req.AssetPath, req.AssetType);
                if (asset == null) return;

                var so = new SerializedObject(asset);
                var prop = so.FindProperty(req.Field);
                if (prop == null) return;

                if (prop.isArray)
                {
                    if (prop.arraySize == 0) prop.arraySize = 1;
                    var first = prop.GetArrayElementAtIndex(0);
                    if (first.propertyType != SerializedPropertyType.String) return;
                    first.stringValue = v;
                }
                else if (prop.propertyType == SerializedPropertyType.String)
                {
                    prop.stringValue = v;
                }
                else return;

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssets();
                break;
            }

            InstallerKeyStore.Set(CasId, platform, v);
        }
```

- [ ] **Step 5: Run to verify it passes**

Unity Test Runner → EditMode → `CasIdApplierWriteTests`. Expected: PASS (3/3).

- [ ] **Step 6: Commit**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer"
git add Editor/Catalog/Catalog.cs Editor/Wizard/CasIdApplier.cs Editor/Tests/CasIdApplierWriteTests.cs
git commit -m "feat(installer): CasIdApplier.SetManagerId + catalog editable flag (#2.1b)"
```

---

### Task 2: Editable cell in `SetupScreen`

**Files:**
- Modify: `Editor/Wizard/Screens/SetupScreen.cs` (`BuildCell` — render an input for editable cells)

**Interfaces:**
- Consumes: `SetupModel.Cell.Req.Editable`, `Req.Platform`, `Req.Field`; `CasIdApplier.SetManagerId(platform, value)`; existing `cell.Result.Value` (the current configured value, may be null).

- [ ] **Step 1: Render an editable field for editable cells**

In `Editor/Wizard/Screens/SetupScreen.cs`, in `BuildCell`, after the existing status-text `Label` is added (the `line.Add(txt);` line) and BEFORE the read-only link wiring (`line.AddToClassList("cas-setup-cell--link"); ...`), branch on editability. Replace the block from `// Each cell opens THIS platform's settings/help ...` through `line.RegisterCallback<ClickEvent>(_ => DoCellAction(cell));` with:

```csharp
            if (cell.Req != null && cell.Req.Editable && cell.Req.Kind == "settingsAssetField")
            {
                // Editable: an inline input that force-writes the managerId for this platform.
                var input = new TextField { value = cell.Result?.Value ?? string.Empty };
                input.AddToClassList("cas-input");
                input.AddToClassList("cas-setup-cell__input");
                input.RegisterCallback<FocusOutEvent>(_ =>
                    CasIdApplier.SetManagerId(cell.Req.Platform, input.value));
                input.RegisterCallback<KeyDownEvent>(e =>
                {
                    if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                        CasIdApplier.SetManagerId(cell.Req.Platform, input.value);
                });
                line.Add(input);
            }
            else
            {
                // Read-only: each cell opens THIS platform's settings/help (CAS has separate assets).
                line.AddToClassList("cas-setup-cell--link");
                line.tooltip = cell.Req != null && cell.Req.Kind == "assetFile"
                    ? "Open the file or how to get it"
                    : "Open settings";
                line.RegisterCallback<ClickEvent>(_ => DoCellAction(cell));
            }
```

Add `using UnityEngine;` if not already present (for `KeyCode`) — it is already imported in this file.

- [ ] **Step 2: Add USS for the inline input**

In `Editor/Wizard/Uss/theme.uss`, after the `.cas-setup-cell` rules (search for `.cas-setup-cell`), add:

```css
.cas-setup-cell__input { margin-top: 3px; min-width: 150px; }
```

- [ ] **Step 3: Owner-run visual check**

Open Configuration with CAS installed: the CAS-ID cell shows an editable field seeded with the current managerId; typing a value and pressing Enter (or clicking away) writes it to the CAS settings asset (verify in the CAS Inspector); other rows (Tenjin/Firebase) stay read-only ping-on-click.

- [ ] **Step 4: Commit**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer"
git add Editor/Wizard/Screens/SetupScreen.cs Editor/Wizard/Uss/theme.uss
git commit -m "feat(installer): inline-editable CAS-ID cell in Configuration (#2.1b)"
```

---

### Task 3: Mark CAS config editable in the catalog + version bumps

**Files:**
- Modify: `E:/workspace/casai/dev/Packages/com.psvgamestudio.installer.metadata/catalog.json` (both CAS config rows)
- Modify: metadata `package.json` + `CHANGELOG.md`
- Modify: installer `package.json` + `CHANGELOG.md`

- [ ] **Step 1: Add `"editable": true` to both CAS config rows**

In `catalog.json`, the CAS record's two `config` rows already carry `regex`/`hint`. Add `"editable": true` to each (Android and iOS rows). Place it after `hint`.

- [ ] **Step 2: Verify JSON**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer.metadata"
python -c "import json;d=json.load(open('catalog.json'));cfg=[e for e in d['external'] if e['id']=='com.cleversolutions.ads.unity'][0]['config'];[print(c['platform'],c.get('editable')) for c in cfg]"
```

Expected:
```
Android True
iOS True
```

- [ ] **Step 3: Bump metadata version + changelog**

`package.json`: `0.0.2-preview.20` → `0.0.2-preview.21`. Add a `## [0.0.2-preview.21] - 2026-06-29` entry: "Mark CAS managerId config rows `editable` so the installer's Configuration tab can edit them inline (#2.1b)."

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer.metadata"
git add catalog.json package.json CHANGELOG.md
git commit -m "chore(metadata): mark CAS managerId config editable (#2.1b)"
```

- [ ] **Step 4: Bump installer version + changelog**

`package.json`: `0.0.1-preview.28` → `0.0.1-preview.29`. Add a `## [0.0.1-preview.29] - 2026-06-29` entry: "Configuration tab: the CAS-ID cell is now inline-editable (writes the managerId directly) (#2.1b)."

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer"
git add package.json CHANGELOG.md
git commit -m "chore(installer): release notes for editable CAS-ID config (preview.29)"
```

---

## Self-Review

- **Spec coverage (#2.1b):** force-write `SetManagerId` → Task 1; inline editable cell gated by catalog `editable` → Task 2; CAS rows flagged editable → Task 3; fallback (no `editable` → read-only) → Task 1 model default + Task 2 branch.
- **Placeholder scan:** none — complete code/commands throughout. SerializedObject/asset writes are owner-verified (Unity-only); the pure `NormalizeManagerId` is unit-tested.
- **Type consistency:** `ConfigRequirement.Editable`, `CasIdApplier.SetManagerId(string, string)`, `NormalizeManagerId(string)`, `SetupModel.Cell.Req.Editable` — consistent across tasks.
