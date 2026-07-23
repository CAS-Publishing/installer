# Tenjin SDK Re-host Patch (1.15.14 → 1.15.14-psv.1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Patch our re-hosted `com.tenjin.sdk` so the iOS post-build step works when the package is installed from a registry/git (PackageCache), and eliminate the `Tenjin` namespace-vs-class collision, then re-publish as `1.15.14-psv.1` to Verdaccio + GitHub mirror and re-pin the catalog.

**Architecture:** We do **not** edit the embedded `dev/Packages/com.tenjin.sdk` as the source of truth — that copy may have drifted. Instead we patch the **exact published tarball** (`npm pack com.tenjin.sdk@1.15.14` from Verdaccio), apply two minimal source edits, bump to a fork version we own (`1.15.14-psv.1`), sign, `npm publish` to Verdaccio, mirror to GitHub via the existing `publish-to-github.ps1`, and bump the catalog pins. This mirrors the existing "source of truth = Verdaccio tarball" toolchain.

**Tech Stack:** Unity 2022.3.62f3 editor C# (`UnityEditor.iOS.Xcode`), npm pack/publish against Verdaccio (`https://npm.psvgamestudio.com/`), `unity-package-signing` skill (Unity 6.3 upm CLI), `ci/scripts/publish-to-github.ps1`, catalog JSON in `com.psvgamestudio.installer.metadata`.

## Execution addendum (2026-06-22) — tracked-patch model

Pre-flight surfaced that `dev/Packages/com.tenjin.sdk` is **gitignored/untracked** (cannot commit edits there), Unity Test Runner is **not headlessly runnable** by agents, and publish/mirror/iOS-build are **owner+macOS-gated**. Decision (owner): keep the fork source as a **tracked patch in the umbrella**.

- **Fork source of truth:** `ci/patches/tenjin/1.15.14-psv.1/` (umbrella `casai`, branch `feat/tenjin-rehost-patch`) holding unified diffs + `README.md` apply/publish steps. The patch is applied to the exact `npm pack`'d 1.15.14 tarball at publish time.
- **Agent-automatable scope:** producing that tracked patch artifact (Tasks 2–3 edits as diffs). Supersedes the "edit + commit the embedded copy" steps in Tasks 2/3 (those commits are invalid — gitignored).
- **Owner-run (unchanged):** running Unity Test Runner, `npm publish`, GitHub mirror push, catalog release, and the iOS build verification (Tasks 4–6 + the test-execution steps in 2–3).

## Global Constraints

- **Registry:** all publishes go to `https://npm.psvgamestudio.com/` — never npmjs.org. `publishConfig.registry` in the package's `package.json` must point there.
- **Verdaccio is immutable:** `1.15.14` cannot be republished. The fork version is **`1.15.14-psv.1`** — a controlled fork tag, pinned explicitly in the catalog (semver ordering is irrelevant because the catalog pins the exact version/tag).
- **Signing required:** every tarball published to the PSV registry for Unity 6.3+ consumers must be signed via the `unity-package-signing` skill before `npm publish`. Commit all `.meta` files before packing/publishing.
- **Minimal diff:** only the two source edits below. No "while I'm here" refactors of upstream Tenjin code. Preserve the public runtime API (global `class Tenjin`, `BaseTenjin`, etc.) — runtime-namespace wrapping is explicitly OUT OF SCOPE (see "Out of scope").
- **Supply-chain gate:** the agent does NOT run `npm publish`, does NOT push to the GitHub mirror, and does NOT run the iOS device build. Those are owner-run steps; the agent prepares everything and stops at each gate. (Memory: the harness is blocked from the mirror push.)
- **Fork provenance:** keep the original `Copyright (c) Tenjin` headers untouched; record our changes in a `CHANGELOG` note inside the working copy.

## Out of scope (explicitly NOT in this plan)

- Wrapping the entire `Runtime/` in a namespace (e.g. `namespace TenjinSDK`). That is a **breaking API change** for every consumer and would also break the installer's "Tenjin global-ns detect" classifier. If desired, it needs its own spec + coordinated migration + installer detection update. This plan only removes the *duplicate-identifier* collision, keeping the public global `Tenjin` API stable.
- The secondary "extract target already exists" concern in `ZipFile.ExtractToDirectory` (not the reported bug; YAGNI).
- `Runtime/CspManager.cs` `"Assets/csc.rsp"` hardcode — gated behind `#if !UNITY_2021_1_OR_NEWER`, dead on 2022.3.

---

### Task 1: Materialize the patched fork working copy from the exact published tarball

**Files:**
- Create (work dir): `%TEMP%/tenjin-patch/package/...` (npm pack extraction — not committed)
- Create: `dev/Packages/com.psvgamestudio.installer/docs/superpowers/plans/2026-06-22-tenjin-sdk-rehost-patch.md` (this plan — already created)

**Interfaces:**
- Produces: a clean extracted copy of `com.tenjin.sdk@1.15.14` at `$WORK/package/` whose `Editor/BuildPostProcessor.cs` and `Editor/*.cs` are byte-identical to what ships. Tasks 2–3 edit files under `$WORK/package/`.

- [ ] **Step 1: Pull the exact published tarball into a clean work dir**

```powershell
$work = Join-Path $env:TEMP "tenjin-patch"
if (Test-Path $work) { Remove-Item $work -Recurse -Force }
New-Item -ItemType Directory $work | Out-Null
Push-Location $work
$tgz = npm pack "com.tenjin.sdk@1.15.14" --registry https://npm.psvgamestudio.com/ 2>$null | Select-Object -Last 1
tar -xzf (Join-Path $work $tgz)
Pop-Location
```

- [ ] **Step 2: Verify the two target files are present and match the embedded copy**

Run:
```powershell
Get-ChildItem "$env:TEMP\tenjin-patch\package\Editor\BuildPostProcessor.cs","$env:TEMP\tenjin-patch\package\Editor\TenjinPackager.cs"
```
Expected: both files listed. Confirm `BuildPostProcessor.cs` line 137 contains the literal `"Packages/com.tenjin.sdk/Plugins/iOS/TenjinSDK.xcframework.zip"`.

- [ ] **Step 3: Record current version**

Run: `node -p "require('$env:TEMP/tenjin-patch/package/package.json'.replace(/\\/g,'/')).version"` → Expected: `1.15.14`.

*(No commit — this is throwaway working material. Source of truth is the published tarball + the edits in Tasks 2–3.)*

---

### Task 2: Fix the iOS post-build path resolution (the reported "incorrect paths" bug)

**Root cause:** `Editor/BuildPostProcessor.cs:137` hardcodes a **Unity virtual path** `"Packages/com.tenjin.sdk/Plugins/iOS/TenjinSDK.xcframework.zip"` and passes it straight to `System.IO.Compression.ZipFile.ExtractToDirectory` (`:148`). The .NET `ZipFile`/`File` APIs resolve relative to the build process CWD, not Unity's virtual filesystem. Embedded packages (physically under `Packages/`) work; registry/git installs land in `Library/PackageCache/com.tenjin.sdk@<hash>/` and the path does not exist → exception → iOS post-build fails. Both our delivery methods (Verdaccio UPM + GitHub git URL) install into PackageCache, so both are broken.

**Fix:** resolve the virtual path to a real filesystem path with `Path.GetFullPath`, which Unity maps correctly for both embedded and PackageCache layouts. (The menu "Export Unity Package" paths at `:27–61` go through `AssetDatabase.ExportPackage`, which already understands virtual paths — leave them.)

**Files:**
- Modify: `$WORK/package/Editor/BuildPostProcessor.cs` (the `zipPathInUnity` assignment, currently line 137)
- Test (dev-only, NOT shipped): `dev/Packages/com.tenjin.sdk/Editor/Tests/TenjinPathResolutionTests.cs` + asmdef

**Interfaces:**
- Consumes: extracted working copy from Task 1.
- Produces: `BuildPostProcessor.EmbedSignFramework` resolving the xcframework zip via an absolute path.

- [ ] **Step 1: Write the failing test (in the embedded dev copy)**

Create `dev/Packages/com.tenjin.sdk/Editor/Tests/TenjinPathResolutionTests.asmdef`:
```json
{
    "name": "Tenjin.Editor.Tests",
    "rootNamespace": "",
    "references": ["UnityEngine.TestRunner", "UnityEditor.TestRunner"],
    "includePlatforms": ["Editor"],
    "excludePlatforms": [],
    "overrideReferences": true,
    "precompiledReferences": ["nunit.framework.dll"],
    "defineConstraints": ["UNITY_INCLUDE_TESTS"],
    "autoReferenced": false
}
```
Create `dev/Packages/com.tenjin.sdk/Editor/Tests/TenjinPathResolutionTests.cs`:
```csharp
using System.IO;
using NUnit.Framework;

public class TenjinPathResolutionTests
{
    // The xcframework zip must resolve to an existing absolute path,
    // regardless of embedded vs PackageCache layout.
    [Test]
    public void XcframeworkZip_ResolvesToExistingAbsolutePath()
    {
        const string virtualPath = "Packages/com.tenjin.sdk/Plugins/iOS/TenjinSDK.xcframework.zip";
        string resolved = Path.GetFullPath(virtualPath);

        Assert.IsTrue(Path.IsPathRooted(resolved), "resolved path must be absolute");
        Assert.IsTrue(File.Exists(resolved), $"zip must exist at resolved path: {resolved}");
    }
}
```

- [ ] **Step 2: Add `testables` entry so the Test Runner sees it, then run to confirm baseline**

In `dev/Packages/manifest.json` add `"com.tenjin.sdk"` to the `testables` array. In Unity: `Window → General → Test Runner → EditMode → Run`. The test passes in dev (embedded) — this is the **baseline guard**; its real value is catching a regression if the fix is reverted. Note in the plan log that PackageCache verification is covered by the manual build gate (Task 6).

- [ ] **Step 3: Apply the fix in the working copy**

In `$WORK/package/Editor/BuildPostProcessor.cs`, change the assignment (currently `:137`):
```csharp
// before
string zipPathInUnity = "Packages/com.tenjin.sdk/Plugins/iOS/TenjinSDK.xcframework.zip";
// after
string zipPathInUnity = Path.GetFullPath("Packages/com.tenjin.sdk/Plugins/iOS/TenjinSDK.xcframework.zip");
```
Mirror the identical edit into the embedded dev copy `dev/Packages/com.tenjin.sdk/Editor/BuildPostProcessor.cs` so the dev project and the test stay coherent.

- [ ] **Step 4: Re-run the test, confirm still green; eyeball the diff**

Run: Test Runner EditMode → `XcframeworkZip_ResolvesToExistingAbsolutePath` → Expected: PASS. Run `git -C dev/Packages/com.tenjin.sdk diff Editor/BuildPostProcessor.cs` and confirm only the one line changed.

- [ ] **Step 5: Commit (umbrella-level, dev test + embedded mirror only)**

```bash
git add dev/Packages/com.tenjin.sdk/Editor/BuildPostProcessor.cs \
        dev/Packages/com.tenjin.sdk/Editor/Tests/ \
        dev/Packages/manifest.json
git commit -m "fix(tenjin): resolve iOS xcframework zip via Path.GetFullPath (PackageCache-safe)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 3: Resolve the `Tenjin` namespace-vs-class collision

**Root cause:** `Runtime/Tenjin.cs:9` declares a global `public static class Tenjin`, while `Editor/TenjinAssetSelector.cs:12`, `Editor/TenjinEditorPrefs.cs:12`, `Editor/TenjinPackager.cs:12` declare `namespace Tenjin`. A type and a namespace sharing the identifier `Tenjin` is a C# footgun: any consumer that does `using Tenjin;` (to reach the editor tooling types) then references the `Tenjin` class hits CS0118 ("`Tenjin` is a namespace but is used like a type"). `Editor/BuildPostProcessor.cs:17` is additionally inconsistent — it sits in the **global** namespace while its sibling editor files are namespaced.

**Fix (collision removal only):** move all four Editor types into a distinct, non-colliding namespace `TenjinEditor`. This removes the duplicate identifier and makes the Editor assembly consistent, without touching the public runtime `Tenjin` class. No live consumer imports the old `Tenjin` editor namespace (verified: zero `using Tenjin;` outside the SDK), so this is non-breaking.

**Files:**
- Modify: `$WORK/package/Editor/TenjinAssetSelector.cs` (`namespace Tenjin` → `namespace TenjinEditor`)
- Modify: `$WORK/package/Editor/TenjinEditorPrefs.cs` (same)
- Modify: `$WORK/package/Editor/TenjinPackager.cs` (same)
- Modify: `$WORK/package/Editor/BuildPostProcessor.cs` (wrap the class in `namespace TenjinEditor { ... }`)
- Mirror all four into `dev/Packages/com.tenjin.sdk/Editor/...`
- Test (dev-only): `dev/Packages/com.tenjin.sdk/Editor/Tests/TenjinNamespaceGuardTests.cs`

**Interfaces:**
- Consumes: working copy from Task 2.
- Produces: editor types live under `TenjinEditor`; the identifier `Tenjin` now unambiguously refers to the runtime class.

- [ ] **Step 1: Write the failing compile-guard test**

Create `dev/Packages/com.tenjin.sdk/Editor/Tests/TenjinNamespaceGuardTests.cs`:
```csharp
using NUnit.Framework;
using TenjinEditor; // must resolve to the renamed editor namespace

public class TenjinNamespaceGuardTests
{
    // If a namespace named `Tenjin` still existed, `using Tenjin;` here plus a
    // reference to the `Tenjin` class would be ambiguous and this file would not compile.
    [Test]
    public void TenjinIdentifier_ResolvesToRuntimeClass_NotNamespace()
    {
        // `Tenjin` resolves to the runtime static class (global namespace).
        System.Type t = typeof(Tenjin);
        Assert.AreEqual("Tenjin", t.Name);
        Assert.IsTrue(t.IsClass && t.IsAbstract && t.IsSealed, "Tenjin is a static class");

        // The editor tooling now lives under TenjinEditor (proves the rename took).
        Assert.IsNotNull(typeof(TenjinEditor.Exporter));
    }
}
```
*(`Exporter` is the `public static class Exporter` currently in `Editor/TenjinPackager.cs:21`; after the rename it is `TenjinEditor.Exporter`.)*

- [ ] **Step 2: Run to verify it FAILS to compile**

Run: Test Runner EditMode. Expected: **compile error** (`using TenjinEditor;` unresolved / `TenjinEditor.Exporter` not found) because the rename hasn't happened yet. This proves the guard is real.

- [ ] **Step 3: Apply the namespace rename in both working copy and embedded dev copy**

In each of `Editor/TenjinAssetSelector.cs`, `Editor/TenjinEditorPrefs.cs`, `Editor/TenjinPackager.cs`: change `namespace Tenjin` → `namespace TenjinEditor`. In `Editor/BuildPostProcessor.cs`: wrap the existing `public class BuildPostProcessor : MonoBehaviour { ... }` body in `namespace TenjinEditor { ... }` (keep all `[PostProcessBuild]` / `[MenuItem]` attributes intact). Apply identically to `$WORK/package/Editor/*` and `dev/Packages/com.tenjin.sdk/Editor/*`.

- [ ] **Step 4: Run the guard test, confirm PASS**

Run: Test Runner EditMode → `TenjinIdentifier_ResolvesToRuntimeClass_NotNamespace` → Expected: PASS, and the whole project recompiles clean (no CS0118).

- [ ] **Step 5: Commit**

```bash
git add dev/Packages/com.tenjin.sdk/Editor/
git commit -m "fix(tenjin): move editor types to TenjinEditor namespace (drop Tenjin ns/class collision)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

### Task 4: Bump fork version, regenerate `.meta`, sign, publish to Verdaccio (OWNER-GATED)

**Files:**
- Modify: `$WORK/package/package.json` (`version`, ensure `publishConfig.registry`)
- Create: `$WORK/package/CHANGELOG.psv.md` (fork provenance note)
- Modify: `$WORK/package/Editor/*.meta`, `Editor/Tests` excluded from ship (see Step 2)

**Interfaces:**
- Consumes: patched `$WORK/package/` from Tasks 2–3.
- Produces: a signed `com.tenjin.sdk@1.15.14-psv.1` published on Verdaccio.

- [ ] **Step 1: Bump version + confirm registry**

In `$WORK/package/package.json` set `"version": "1.15.14-psv.1"` and ensure `"publishConfig": { "registry": "https://npm.psvgamestudio.com/" }`.

- [ ] **Step 2: Strip dev-only tests from the ship copy**

Do NOT ship `Editor/Tests/`. Confirm `$WORK/package/Editor/Tests` does not exist (the tests live only in the embedded dev copy, never in `$WORK`). If present, delete it.

- [ ] **Step 3: Generate `.meta` for any changed/added files**

The only changed files are existing `.cs` (already have `.meta`). Confirm no new shippable files were added → no missing `.meta`. Run:
```powershell
Get-ChildItem "$env:TEMP\tenjin-patch\package" -Recurse -File -Include *.cs |
  Where-Object { -not (Test-Path ($_.FullName + ".meta")) }
```
Expected: no output (every `.cs` has a `.meta`).

- [ ] **Step 4: Sign the package**

REQUIRED SUB-SKILL: invoke the `unity-package-signing` skill and sign `$WORK/package` (org `7971476452176`, env vars per the skill). Confirm the `.attestation.p7m` / signature artifacts are produced.

- [ ] **Step 5: Dry-run publish (agent), then OWNER publishes**

Agent runs a pack dry-run and prints contents:
```powershell
Push-Location "$env:TEMP\tenjin-patch\package"; npm pack --dry-run; Pop-Location
```
**GATE — owner runs:**
```powershell
Push-Location "$env:TEMP\tenjin-patch\package"
npm publish --registry https://npm.psvgamestudio.com/
Pop-Location
```
Expected: `+ com.tenjin.sdk@1.15.14-psv.1`. Agent stops here until the owner confirms the publish succeeded.

---

### Task 5: Mirror to GitHub + re-pin the catalog (OWNER-GATED push)

**Files:**
- Run: `ci/scripts/publish-to-github.ps1`
- Modify: `dev/Packages/com.psvgamestudio.installer.metadata/catalog.json:66,67,70` (versions + git tag)
- Modify: `dev/Packages/com.psvgamestudio.installer.metadata/CHANGELOG.md`

**Interfaces:**
- Consumes: published `com.tenjin.sdk@1.15.14-psv.1` on Verdaccio.
- Produces: GitHub tag `1.15.14-psv.1` + catalog pinned to it.

- [ ] **Step 1: OWNER mirrors the new version to GitHub**

```powershell
& "E:\workspace\casai\ci\scripts\publish-to-github.ps1" -PackageId com.tenjin.sdk -RepoName tenjin-sdk -Version 1.15.14-psv.1
```
Expected: tag `1.15.14-psv.1` pushed to `CAS-Publishing/tenjin-sdk`. (Memory: harness is blocked from this push → owner runs it.)

- [ ] **Step 2: Re-pin the catalog (agent edits)**

In `dev/Packages/com.psvgamestudio.installer.metadata/catalog.json`, in the `com.tenjin.sdk` entry:
```json
      "minVersion": "1.15.14-psv.1",
      "recommendedVersion": "1.15.14-psv.1",
      "git": {
        "packages": [
          { "id": "com.tenjin.sdk", "url": "https://github.com/CAS-Publishing/tenjin-sdk.git", "tag": "1.15.14-psv.1" }
        ]
      }
```

- [ ] **Step 3: Add a CHANGELOG entry**

Prepend to `dev/Packages/com.psvgamestudio.installer.metadata/CHANGELOG.md`:
```markdown
- Tenjin SDK pinned to fork **1.15.14-psv.1**: PackageCache-safe iOS post-build path
  (`Path.GetFullPath`) + editor types moved to `TenjinEditor` namespace (drops the
  `Tenjin` namespace/class collision). Functionally identical runtime API.
```

- [ ] **Step 4: Commit the catalog bump (then release per repo convention)**

```bash
git -C dev/Packages/com.psvgamestudio.installer.metadata add catalog.json CHANGELOG.md
git -C dev/Packages/com.psvgamestudio.installer.metadata commit -m "feat(catalog): pin Tenjin SDK to fork 1.15.14-psv.1 (postbuild + namespace fixes)"
```
Then bump the catalog package version and release per the metadata repo's existing `release(metadata):` convention (owner-gated publish, same as prior `v0.0.2-preview.*` releases).

---

### Task 6: Manual verification gate (the real proof — OWNER, on macOS)

The unit tests above are regression guards; the definitive proof is a real install + iOS build, because the bug only manifests in a PackageCache layout on an actual Xcode build.

- [ ] **Step 1:** In a scratch consumer Unity project, add the git package:
`Package Manager → Add package from git URL → https://github.com/CAS-Publishing/tenjin-sdk.git#1.15.14-psv.1`. Confirm it resolves into `Library/PackageCache/` (NOT `Packages/`).
- [ ] **Step 2:** Build iOS. Expected: TenjinSDK post-build runs without "incorrect path" / `ZipFile`/`DirectoryNotFound` errors; `Frameworks/TenjinSDK.xcframework` is present and embedded in the Xcode project; `Info.plist` has `NSUserTrackingUsageDescription`.
- [ ] **Step 3:** Confirm the project compiles with no `CS0118`/namespace ambiguity and `Tenjin.GetTenjin(...)` is callable from consumer code.
- [ ] **Step 4:** Repeat Step 2 once with the Verdaccio UPM install path (scoped registry) to confirm parity.

---

## Self-Review

- **Spec coverage:** Problem 1 (post-build paths) → Task 2 + Task 6. Problem 2 (namespace/class collision) → Task 3 + Task 6 Step 3. Re-publish chain (Verdaccio → GitHub → catalog) → Tasks 4–5. ✓
- **Placeholder scan:** no TBD/TODO; every code step shows the exact edit. ✓
- **Type consistency:** `TenjinEditor` namespace + `TenjinEditor.Exporter` used consistently across Tasks 3 & 5; version `1.15.14-psv.1` consistent across Tasks 4–6. ✓
- **Open decision deferred to owner:** namespace direction — this plan implements the **safe collision-removal** option; full runtime-namespace isolation is documented as out of scope.
