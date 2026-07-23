# WS-8 — Migrate: Delete-anyway + precise Plugins auto-delete — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax.

**Goal:** (A) When Migrate's git-guard blocks deleting an untracked manual copy, offer an explicit "Delete anyway" fallback (#4). (B) Auto-delete precisely-named CAS/Firebase native libs in the shared `Assets/Plugins` folder so the user never prunes Plugins by hand (new #8).

**Architecture:** `BackupAndDeletePath` gains an `IgnoreGitGuard` flag the runner honors — but PathSafety (no escaping `Assets/`) is NEVER bypassed. `MigrateExternal` retries the delete with that flag set only after a SECOND explicit confirm, and only when the first failure was a git-guard refusal. For (B), a catalog `pluginFiles` list (exact names / simple `*` globs) drives a new precise `FindPluginFiles` probe whose hits are folded into the delete set (shown in the confirm window, deleted under the same git-guard + Delete-anyway path).

**Tech Stack:** Unity 2022.3 Editor, C# UPM editor package, NUnit, manifest/asset migration, JSON catalog.

**Decision source:** `docs/superpowers/specs/2026-06-29-installer-feedback-round2-decisions.md` (#4 Delete-anyway, new #8).

## Global Constraints

- **PathSafety is never bypassed.** `IgnoreGitGuard` only skips the GitGuard recoverability check, never the `Assets/`-containment check.
- Delete-anyway is **opt-in**: a second confirm dialog, shown only when the first delete failed with a git-guard refusal ("refusing to delete"). It warns the deletion is permanent / not git-recoverable.
- Every file to be deleted (owned roots, signature files, AND precise Plugins libs) is listed in `MigrateConfirmWindow` before any deletion.
- `pluginFiles` patterns must be SPECIFIC (e.g. `libFirebaseCppApp.a`, `libFirebaseCppAnalytics.a`), not broad globs — a wrong pattern + Delete-anyway is unrecoverable.
- No CLI/headless runner: dialogs, git, and asset deletion are OWNER-RUN. Pure helpers (`IgnoreGitGuard` honoring is reviewed; the glob matcher is unit-tested).
- Conventional Commits, `feat(installer):` / `chore(metadata):`. Installer branch `feat/installer-wizard-ui`; metadata branch `chore/cas-pin-4.7.4`.

---

### Task 1: `BackupAndDeletePath.IgnoreGitGuard` + runner honors it

**Files:**
- Modify: `Editor/Migrator/MigrationAction.cs` (`BackupAndDeletePath`)
- Modify: `Editor/Migrator/Migrator.cs` (delete loop)

**Interfaces:**
- Produces: `BackupAndDeletePath(string relativePath, bool ignoreGitGuard = false)` + `bool IgnoreGitGuard { get; }`. The runner skips the GitGuard check when `IgnoreGitGuard` is true; PathSafety still runs.

- [ ] **Step 1: Add the flag to `BackupAndDeletePath`**

In `Editor/Migrator/MigrationAction.cs`, replace the `BackupAndDeletePath` class body:

```csharp
    public sealed class BackupAndDeletePath : MigrationAction
    {
        /// <summary>
        /// Path relative to <c>Assets/</c> (e.g. "Plugins/Firebase").
        /// The executor resolves this against <c>Application.dataPath</c>.
        /// </summary>
        public string RelativePath { get; }

        /// <summary>
        /// When true, the runner deletes even if git can't recover the path (untracked/dirty).
        /// PathSafety (no escaping Assets/) is still enforced. Set only after an explicit
        /// "Delete anyway" confirmation — the deletion is permanent.
        /// </summary>
        public bool IgnoreGitGuard { get; }

        public BackupAndDeletePath(string relativePath, bool ignoreGitGuard = false)
        {
            RelativePath = relativePath;
            IgnoreGitGuard = ignoreGitGuard;
        }
    }
```

- [ ] **Step 2: Honor the flag in the runner**

In `Editor/Migrator/Migrator.cs`, in the delete loop, replace the GitGuard block:

```csharp
                if (!GitGuard.IsTrackedAndClean(absolutePath, out var gitReason))
                {
                    failures.Add($"DeletePath({action.RelativePath}): refusing to delete — {gitReason}");
                    return new ApplyResult(false, executedCount, failures);
                }
```

with:

```csharp
                // PathSafety (above) is always enforced. The git recoverability guard can be
                // explicitly overridden via IgnoreGitGuard (the user's "Delete anyway" choice).
                if (!action.IgnoreGitGuard && !GitGuard.IsTrackedAndClean(absolutePath, out var gitReason))
                {
                    failures.Add($"DeletePath({action.RelativePath}): refusing to delete — {gitReason}");
                    return new ApplyResult(false, executedCount, failures);
                }
```

- [ ] **Step 3: Owner-run compile check** — confirm `Migrator.cs` / `MigrationAction.cs` compile.

- [ ] **Step 4: Commit**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer"
git add Editor/Migrator/MigrationAction.cs Editor/Migrator/Migrator.cs
git commit -m "feat(installer): BackupAndDeletePath.IgnoreGitGuard (PathSafety still enforced) (#4)"
```

---

### Task 2: Delete-anyway fallback in `MigrateExternal`

**Files:**
- Modify: `Editor/Wizard/WizardActions.cs` (`MigrateExternal` STEP 1 failure handling)

**Interfaces:**
- Consumes: `BackupAndDeletePath(path, ignoreGitGuard: true)`, `MigrationRunner`, `EditorUtility.DisplayDialog`.

- [ ] **Step 1: Add the fallback after a git-guard-blocked STEP 1**

In `Editor/Wizard/WizardActions.cs`, in `MigrateExternal`, replace the STEP-1 failure block:

```csharp
            var del = new MigrationRunner().Apply(deletePlan);
            if (!del.Success)
            {
                EditorUtility.DisplayDialog("PSV Installer",
                    $"Couldn't remove the manual copy of {displayName}:\n• " +
                    string.Join("\n• ", del.Failures) +
                    "\n\nCommit those files to git first (so they're recoverable), or delete them " +
                    "manually, then migrate again. manifest.json was NOT changed.", "OK");
                return true; // re-scan: state is unchanged but the user acted
            }
```

with:

```csharp
            var del = new MigrationRunner().Apply(deletePlan);
            if (!del.Success)
            {
                // Offer a permanent "Delete anyway" ONLY when the block was git's recoverability
                // guard (untracked/dirty). Other failures (PathSafety, IO) are not overridable here.
                var gitBlocked = del.Failures.Count > 0 &&
                                 del.Failures.TrueForAll(f => f.Contains("refusing to delete"));

                if (!gitBlocked || !EditorUtility.DisplayDialog("PSV Installer",
                        $"Some files of {displayName} aren't tracked by git, so they can't be recovered " +
                        "if removed:\n• " + string.Join("\n• ", del.Failures) +
                        "\n\nDelete them PERMANENTLY anyway? This cannot be undone.",
                        "Delete anyway", "Cancel"))
                {
                    EditorUtility.DisplayDialog("PSV Installer",
                        $"Couldn't remove the manual copy of {displayName}:\n• " +
                        string.Join("\n• ", del.Failures) +
                        "\n\nCommit those files to git first (so they're recoverable), or delete them " +
                        "manually, then migrate again. manifest.json was NOT changed.", "OK");
                    return true; // re-scan: state unchanged but the user acted
                }

                // User opted in: retry the same deletes with the git guard overridden (PathSafety still on).
                var forcePlan = new List<MigrationAction>();
                foreach (var r in deletePaths) forcePlan.Add(new BackupAndDeletePath(r, ignoreGitGuard: true));
                var forced = new MigrationRunner().Apply(forcePlan);
                if (!forced.Success)
                {
                    EditorUtility.DisplayDialog("PSV Installer",
                        $"Delete-anyway still failed for {displayName}:\n• " +
                        string.Join("\n• ", forced.Failures) +
                        "\n\nmanifest.json was NOT changed.", "OK");
                    return true;
                }
            }
```

(`deletePaths` and `List<MigrationAction>` are already in scope in `MigrateExternal`.)

- [ ] **Step 2: Owner-run verification**

Import a CAS/Firebase .unitypackage (files untracked by git). Migrate → first attempt blocks ("not tracked by git"); a "Delete anyway" dialog appears; choosing it removes the files and proceeds to the UPM install. Choosing Cancel leaves everything unchanged.

- [ ] **Step 3: Commit**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer"
git add Editor/Wizard/WizardActions.cs
git commit -m "feat(installer): Migrate 'Delete anyway' fallback for git-untracked files (#4)"
```

---

### Task 3: Precise Plugins-lib matching folded into the delete set

**Files:**
- Modify: `Editor/Catalog/Catalog.cs` (`ExternalRecord` gains `PluginFiles`)
- Modify: `Editor/Scanner/AssetInstallProbe.cs` (`FindPluginFiles` + pure `MatchesFilePattern`)
- Modify: `Editor/Wizard/WizardActions.cs` (fold Plugins-lib hits into `deletePaths`)
- Test: `Editor/Tests/PluginFileMatchTests.cs` (+ `.meta` guid `c2d3e4f5a6b70819203a4b5c6d7e8f01`)

**Interfaces:**
- Produces: `ExternalRecord.PluginFiles` (`List<string>`, json `pluginFiles`); `bool AssetInstallProbe.MatchesFilePattern(string fileName, string pattern)` (exact, or one `*` wildcard); `List<string> AssetInstallProbe.FindPluginFiles(IReadOnlyList<string> patterns, string relativeDir = "Plugins", int max = 50)`.

- [ ] **Step 1: Write the failing test (pure matcher)**

Create `Editor/Tests/PluginFileMatchTests.cs`:

```csharp
using NUnit.Framework;
using PSV.Installer.Scanner;

namespace PSV.Installer.Tests
{
    public class PluginFileMatchTests
    {
        [Test] public void Exact_match() => Assert.IsTrue(AssetInstallProbe.MatchesFilePattern("libFirebaseCppApp.a", "libFirebaseCppApp.a"));
        [Test] public void Exact_mismatch() => Assert.IsFalse(AssetInstallProbe.MatchesFilePattern("libOther.a", "libFirebaseCppApp.a"));
        [Test] public void Prefix_wildcard() => Assert.IsTrue(AssetInstallProbe.MatchesFilePattern("libFirebaseCppAnalytics.a", "libFirebaseCpp*"));
        [Test] public void Suffix_wildcard() => Assert.IsTrue(AssetInstallProbe.MatchesFilePattern("libFirebaseCppApp.a", "*.a"));
        [Test] public void Mid_wildcard() => Assert.IsTrue(AssetInstallProbe.MatchesFilePattern("libFirebaseCppApp.a", "libFirebase*App.a"));
        [Test] public void Wildcard_no_false_positive() => Assert.IsFalse(AssetInstallProbe.MatchesFilePattern("libUnrelated.a", "libFirebaseCpp*"));
        [Test] public void Case_insensitive() => Assert.IsTrue(AssetInstallProbe.MatchesFilePattern("LIBFIREBASECPPAPP.A", "libFirebaseCppApp.a"));
        [Test] public void Null_safe() => Assert.IsFalse(AssetInstallProbe.MatchesFilePattern(null, "x"));
    }
}
```

Create `Editor/Tests/PluginFileMatchTests.cs.meta`:
```
fileFormatVersion: 2
guid: c2d3e4f5a6b70819203a4b5c6d7e8f01
MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData:
  assetBundleName:
  assetBundleVariant:
```

- [ ] **Step 2: Run to verify it fails** — Expected: FAIL (no `MatchesFilePattern`).

- [ ] **Step 3: Add `MatchesFilePattern` + `FindPluginFiles`**

In `Editor/Scanner/AssetInstallProbe.cs`, add:

```csharp
        /// <summary>
        /// Case-insensitive file-name match supporting at most one '*' wildcard (prefix*suffix).
        /// Used for precise Plugins-lib targeting (e.g. "libFirebaseCpp*"). Pure/testable.
        /// </summary>
        public static bool MatchesFilePattern(string fileName, string pattern)
        {
            if (string.IsNullOrEmpty(fileName) || string.IsNullOrEmpty(pattern)) return false;
            var f = fileName.ToLowerInvariant();
            var p = pattern.ToLowerInvariant();
            var star = p.IndexOf('*');
            if (star < 0) return f == p;
            var prefix = p.Substring(0, star);
            var suffix = p.Substring(star + 1);
            return f.Length >= prefix.Length + suffix.Length
                && f.StartsWith(prefix, System.StringComparison.Ordinal)
                && f.EndsWith(suffix, System.StringComparison.Ordinal);
        }

        /// <summary>
        /// Finds files under <paramref name="relativeDir"/> (default "Plugins") whose file name matches
        /// any of <paramref name="patterns"/> (exact or single-'*' glob). Returns Assets-relative paths.
        /// Precise by design — patterns must be specific lib names, not broad globs.
        /// </summary>
        public static List<string> FindPluginFiles(IReadOnlyList<string> patterns, string relativeDir = "Plugins", int max = 50)
        {
            var hits = new List<string>();
            if (patterns == null || patterns.Count == 0 || string.IsNullOrEmpty(relativeDir)) return hits;

            var assetsRoot = Application.dataPath;
            var dir = Path.GetFullPath(Path.Combine(assetsRoot, relativeDir));
            if (!Directory.Exists(dir)) return hits;

            try
            {
                foreach (var file in Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories))
                {
                    if (string.Equals(Path.GetExtension(file), ".meta", System.StringComparison.OrdinalIgnoreCase)) continue;
                    var name = Path.GetFileName(file);
                    var matched = false;
                    foreach (var pat in patterns) if (MatchesFilePattern(name, pat)) { matched = true; break; }
                    if (!matched) continue;

                    var full = Path.GetFullPath(file);
                    if (full.Length <= assetsRoot.Length) continue;
                    var rel = full.Substring(assetsRoot.Length).Replace('\\', '/').TrimStart('/');
                    hits.Add(rel);
                    if (hits.Count >= max) break;
                }
            }
            catch { /* read-only probe: return what we have */ }
            return hits;
        }
```

(`AssetInstallProbe.cs` already imports `System.Collections.Generic`, `System.IO`, `UnityEngine`.)

- [ ] **Step 4: Add the catalog field**

In `Editor/Catalog/Catalog.cs`, in `ExternalRecord`, add (near `AssetMarkers`/`AssetRoots`):

```csharp
        /// <summary>Exact/glob file names (single '*') of this SDK's native libs in Assets/Plugins to
        /// auto-delete on migrate (e.g. "libFirebaseCppApp.a"). Precise — never broad globs.</summary>
        [JsonProperty("pluginFiles")] public List<string> PluginFiles;
```

- [ ] **Step 5: Fold Plugins-lib hits into the delete set**

In `Editor/Wizard/WizardActions.cs`, in `MigrateExternal`, after the existing `FindSignatureFiles` fold loop and BEFORE the `if (deletePaths.Count == 0)` check, add:

```csharp
            // Precise native libs in the shared Assets/Plugins folder (e.g. libFirebaseCpp*.a) — matched
            // by exact name/glob from the catalog, so they're deleted with the rest (not left for the
            // user to prune by hand). Imprecise marker-only leftovers remain in sharedLeftovers below.
            foreach (var f in AssetInstallProbe.FindPluginFiles(rec.PluginFiles))
                if (!deletePaths.Contains(f)) deletePaths.Add(f);
```

- [ ] **Step 6: Run to verify the matcher tests pass** — Expected: PASS (8/8).

- [ ] **Step 7: Commit**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer"
git add Editor/Catalog/Catalog.cs Editor/Scanner/AssetInstallProbe.cs Editor/Wizard/WizardActions.cs Editor/Tests/PluginFileMatchTests.cs Editor/Tests/PluginFileMatchTests.cs.meta
git commit -m "feat(installer): precise Plugins-lib auto-delete on migrate (pluginFiles) (new #8)"
```

---

### Task 4: Catalog `pluginFiles` for Firebase + version bumps

**Files:**
- Modify: metadata `catalog.json` (Firebase external `pluginFiles`), `package.json`, `CHANGELOG.md`
- Modify: installer `package.json`, `CHANGELOG.md`

- [ ] **Step 1: Add `pluginFiles` to the Firebase external record**

In `catalog.json`, on the Firebase external (`com.google.firebase.analytics`), add a `pluginFiles` array with the documented native libs:

```json
      "pluginFiles": ["libFirebaseCppApp.a", "libFirebaseCppAnalytics.a"],
```

(These are the libs shown in the round-2 report under `Assets/Plugins/iOS/Firebase` / `tvOS/Firebase`. Owner: extend this list with any other `libFirebaseCpp*.a/.aar` actually present before relying on Delete-anyway for binaries.)

- [ ] **Step 2: Verify JSON**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer.metadata"
python -c "import json;d=json.load(open('catalog.json'));fb=[e for e in d['external'] if e['id']=='com.google.firebase.analytics'][0];print(fb.get('pluginFiles'))"
```
Expected: `['libFirebaseCppApp.a', 'libFirebaseCppAnalytics.a']`.

- [ ] **Step 3: Bump metadata (`0.0.2-preview.22` → `0.0.2-preview.23`) + changelog**, commit `chore(metadata): Firebase pluginFiles for precise Plugins cleanup (new #8)` (branch `chore/cas-pin-4.7.4`).

- [ ] **Step 4: Bump installer (`0.0.1-preview.31` → `0.0.1-preview.32`) + changelog** ("Migrate now offers Delete-anyway for git-untracked files and auto-removes precisely-named Plugins libs (#4, new #8)."), commit `chore(installer): release notes for Migrate delete-anyway + Plugins cleanup (preview.32)`.

---

## Self-Review

- **Spec coverage:** Delete-anyway (#4) → Task 1 (flag/runner) + Task 2 (fallback + opt-in confirm); precise Plugins auto-delete (new #8) → Task 3 (`pluginFiles` + `FindPluginFiles` folded into deletePaths) + Task 4 (Firebase data). PathSafety-always → Task 1 (only GitGuard skipped). Confirm-lists-everything → existing MigrateConfirmWindow shows `deletePaths` (now incl. Plugins libs).
- **Placeholder scan:** none — pure matcher fully coded+tested; Unity-only delete/dialog paths are owner-run with documented behavior.
- **Type consistency:** `BackupAndDeletePath(string, bool)` / `.IgnoreGitGuard`, `AssetInstallProbe.MatchesFilePattern(string,string)` / `FindPluginFiles(IReadOnlyList<string>,string,int)`, `ExternalRecord.PluginFiles` — consistent across tasks. The git-blocked detection keys off the existing "refusing to delete" message from Task-1's runner.
