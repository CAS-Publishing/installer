# Migrator asset-roots safety — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make SDK migration delete ONLY the folders the catalog explicitly declares as SDK-owned (`assetRoots`), removing the file-walk heuristic that could target a user's own folder (e.g. `Assets/Scripts`).

**Architecture:** `ExternalRecord` gains `assetRoots`; `MigrateExternal` deletes `AssetProbe.FindExisting(rec.AssetRoots)` instead of walking `Assets/` via `FindRootsForMigration`; the walk and its helpers are deleted. Presence detection (reflection markers) is unchanged.

**Tech Stack:** C# (Unity Editor, .NET), Newtonsoft.Json, NUnit (Unity Test Framework). Spec: `docs/superpowers/specs/2026-06-17-migrator-asset-roots-safety-design.md`.

**Test note:** This project has no CLI test runner — EditMode tests run in **Unity → Window → General → Test Runner** (or batchmode: `Unity.exe -batchmode -projectPath <dev> -runTests -testPlatform EditMode -testResults out.xml -logFile run.log -quit`). "Run" steps below mean that.

---

### Task 1: Catalog model — add `assetRoots`, drop `extraCleanupPaths`

**Files:**
- Modify: `Editor/Catalog/Catalog.cs` (the `ExternalRecord` class)
- Test: `Editor/Tests/CatalogLoaderTests.cs` (add one test)

- [ ] **Step 1: Write the failing test** — add to `CatalogLoaderTests` (namespace `PSV.Installer.Tests`, `using Newtonsoft.Json; using PSV.Installer.Catalog;`):

```csharp
[Test]
public void ExternalRecord_parses_assetRoots()
{
    const string json = "{\"id\":\"com.x\",\"assetRoots\":[\"Firebase\",\"ExternalDependencyManager\"]}";
    var rec = JsonConvert.DeserializeObject<ExternalRecord>(json);
    Assert.AreEqual(2, rec.AssetRoots.Count);
    Assert.AreEqual("Firebase", rec.AssetRoots[0]);
    Assert.AreEqual("ExternalDependencyManager", rec.AssetRoots[1]);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run the EditMode tests. Expected: FAIL — `ExternalRecord` has no `AssetRoots` member (compile error).

- [ ] **Step 3: Implement** — in `Editor/Catalog/Catalog.cs`, in `ExternalRecord`, ADD the `assetRoots` field (keep `extraCleanupPaths` for now — it's removed in Task 2 once its last user is gone, so each commit compiles):

```csharp
        /// <summary>
        /// Assets-relative folders WHOLLY OWNED by this SDK that migration deletes when moving it to UPM
        /// (its install folder + satellites like EDM / PlayServicesResolver / its own
        /// <c>Editor Default Resources/&lt;sdk&gt;</c> subfolder). Only the ones that actually exist are
        /// deleted, each through the git/path-safety guard. NEVER list a shared folder
        /// (<c>Assets/Plugins</c>, <c>Resources</c>, …) — those are surfaced as a manual-cleanup warning.
        /// This is the ONLY source of migration delete targets — there is no file-walk. Absent → the
        /// migrator reports "couldn't locate, remove manually" (safe no-op).
        /// </summary>
        [JsonProperty("assetRoots")]         public List<string> AssetRoots;
```

(Leave the existing `extraCleanupPaths` field in place for this task — Task 2 removes it after switching its last consumer, so every commit compiles.)

- [ ] **Step 4: Run test to verify it passes**

Run the EditMode tests. Expected: `ExternalRecord_parses_assetRoots` PASS.

- [ ] **Step 5: Commit**

```bash
git add Editor/Catalog/Catalog.cs Editor/Tests/CatalogLoaderTests.cs
git commit -m "feat(installer): ExternalRecord.assetRoots (replaces extraCleanupPaths)"
```

---

### Task 2: Migration deletes only declared roots

**Files:**
- Modify: `Editor/Wizard/WizardActions.cs` (`MigrateExternal`)
- Modify: `Editor/Catalog/Catalog.cs` (remove now-unused `ExtraCleanupPaths` field)

- [ ] **Step 1: Implement** — in `MigrateExternal`, REPLACE this block:

```csharp
            var roots = AssetInstallProbe.FindRootsForMigration(rec.AssetMarkers);
            if (roots.Count == 0)
            {
                EditorUtility.DisplayDialog("PSV Installer",
                    $"{displayName} appears to be installed manually, but its files couldn't be " +
                    "located under Assets/ (no matching asmdef / DLL / script folder). Remove the " +
                    "manual copy yourself, then use Install to add the UPM version.", "OK");
                return false;
            }

            // Extra SDK-owned satellite folders the .unitypackage drops OUTSIDE the primary root
            // (EDM, the SDK's own Editor Default Resources subfolder…). Only existing ones are kept;
            // shared roots (Assets/Plugins) are never in this list — they're warned about below.
            var extraRoots = AssetProbe.FindExisting(rec.ExtraCleanupPaths);
            var deletePaths = new List<string>(roots);
            foreach (var p in extraRoots)
                if (!deletePaths.Contains(p)) deletePaths.Add(p);
```

with:

```csharp
            // Delete ONLY the SDK-owned folders the catalog declares, and only those that exist. No
            // file-walk: a folder can never be inferred from a stray user script, so user folders
            // (Assets/Scripts, …) can never be targeted.
            var deletePaths = AssetProbe.FindExisting(rec.AssetRoots);
            if (deletePaths.Count == 0)
            {
                EditorUtility.DisplayDialog("PSV Installer",
                    $"{displayName} appears to be installed manually, but its known folders weren't " +
                    "found under Assets/ (non-standard layout). Remove the manual copy yourself, then " +
                    "use Install to add the UPM version.", "OK");
                return false;
            }
```

- [ ] **Step 2: Remove the now-unused field** — in `Editor/Catalog/Catalog.cs`, delete the `extraCleanupPaths` field and its doc comment (its only consumer was the block replaced in Step 1):

```csharp
        // DELETE these lines:
        // /// <summary> … extra Assets-relative paths … </summary>
        // [JsonProperty("extraCleanupPaths")]  public List<string> ExtraCleanupPaths;
```

Run: `grep -rn "ExtraCleanupPaths" Editor/` → expected: no matches.

- [ ] **Step 3: Verify it compiles & the migrate flow works**

Run the EditMode tests (must still compile). Then, in the editor: open the hub on a project with an out-of-UPM SDK, click Migrate, and confirm the dialog's "Delete" list shows only the declared `assetRoots` (no user folders). Expected: only SDK folders listed.

- [ ] **Step 4: Commit**

```bash
git add Editor/Wizard/WizardActions.cs Editor/Catalog/Catalog.cs
git commit -m "fix(installer): migration deletes only catalog-declared assetRoots (no file-walk)"
```

---

### Task 3: Remove the dangerous file-walk

**Files:**
- Modify: `Editor/Scanner/AssetInstallProbe.cs`

- [ ] **Step 1: Confirm no other callers**

Run: `grep -rn "FindRootsForMigration" Editor/`
Expected: no matches (Task 2 removed the only caller).

- [ ] **Step 2: Delete dead code** — in `Editor/Scanner/AssetInstallProbe.cs`, remove these members entirely: `FindRootsForMigration`, `FirstNamespace`, `ReadAsmdef`, `TopRoot`, and the `NamespaceRx` regex field. KEEP `MatchesAny`, `CollectLoadedIdentifiers`, `IsPresentInIdentifiers`, `TypeIdentifier`, `FindLooseFiles`, `ReadStaticVersion`. Also drop now-unused `using` directives (`System.Text.RegularExpressions`, `Newtonsoft.Json.Linq`) if nothing else in the file uses them.

- [ ] **Step 3: Run tests / compile**

Run the EditMode tests. Expected: compiles clean, all green (`OutsideUpmDetectionTests`, `SetupCheckerTests`, `MigrateExternalGroupTests`, `CatalogLoaderTests`).

- [ ] **Step 4: Commit**

```bash
git add Editor/Scanner/AssetInstallProbe.cs
git commit -m "refactor(installer): drop FindRootsForMigration file-walk + dead helpers"
```

---

### Task 4: Catalog data — declare `assetRoots`

**Files:**
- Modify: `../com.psvgamestudio.installer.metadata/catalog.json` (separate `metadata` repo, branch `main`)

- [ ] **Step 1: Edit `catalog.json`**

Firebase external — replace `extraCleanupPaths` with:
```json
      "assetRoots": [
        "Firebase",
        "ExternalDependencyManager",
        "PlayServicesResolver",
        "Editor Default Resources/Firebase"
      ],
```
Tenjin external — add: `"assetRoots": ["Tenjin"],`
CAS external (`com.cleversolutions.ads.unity`) — add: `"assetRoots": ["CleverAdsSolutions"],`
Bump top-level: `"catalogVersion": "0.0.2-preview.15",`

- [ ] **Step 2: Validate JSON**

Run: `python -c "import json; d=json.load(open('catalog.json')); print(d['catalogVersion'], [e.get('assetRoots') for e in d['external']])"`
Expected: prints `0.0.2-preview.15` and the assetRoots lists; no JSON error; no `extraCleanupPaths` remaining (`grep -c extraCleanupPaths catalog.json` → 0).

- [ ] **Step 3: Commit (metadata repo)**

```bash
git -C ../com.psvgamestudio.installer.metadata add catalog.json
git -C ../com.psvgamestudio.installer.metadata commit -m "feat(metadata): assetRoots per external (catalog 0.0.2-preview.15)"
```

---

### Task 5: Release (after Unity verification)

Do this only once Tasks 1–4 are green in Unity Test Runner and the Migrate dialog was eyeballed.

- [ ] **Step 1:** Bump `package.json`: installer → `0.0.1-preview.19`; metadata → `0.0.2-preview.13`.
- [ ] **Step 2:** Sign + publish each (skill `unity-package-signing`; **installer = no-timeout isolated pack**, metadata = normal). Verify `latest` on `npm.psvgamestudio.com`.
- [ ] **Step 3:** Commit the version bumps (release commits) and push installer (`feat/installer-wizard-ui`) + metadata (`main`) to GitLab.
- [ ] **Step 4:** Mirror to GitHub: `publish-to-github.ps1` for `installer@0.0.1-preview.19` and `installer-metadata@0.0.2-preview.13` (auto-mode off).

---

## Self-Review

- **Spec coverage:** model (T1), migration logic (T2), remove walk (T3), catalog data (T4), release (T5) — all spec sections covered. ✓
- **Placeholders:** none — every code step shows the actual code. ✓
- **Type consistency:** `AssetRoots` (List<string>) used consistently in T1 (def), T2 (`rec.AssetRoots`), T4 (`assetRoots` JSON). `AssetProbe.FindExisting(IReadOnlyList<string>)` returns `List<string>` → matches `deletePaths` usage. ✓
- **Note:** `MigrateExternal` logic (T2) has no clean unit test (Unity AssetDatabase/IO + dialogs); verified by compile + the T1 parse test (data) + manual editor eyeball of the Migrate dialog. Stated honestly in T2 Step 2.
