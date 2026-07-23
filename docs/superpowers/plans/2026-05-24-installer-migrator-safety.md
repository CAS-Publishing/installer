# Installer Infra Hardening — Migrator Safety (Plan 2 of 3)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Make the migrator safe against client-project corruption: legacy-asset deletion can no longer escape `Assets/`, the manifest is written atomically and BEFORE any irreversible delete, deletions are refused unless git can recover them, partial split migrations can never strip a legacy package without its full replacement, and the manifest write/read path stops throwing on hand-edited (comment-bearing) manifests.

**Architecture:** Two phases. **Phase A** adds pure, fully unit-testable helpers — `PathSafety` (containment), `GitGuard` (git tracked+clean precondition) — and tightens the pure `MigrationPlanner` (empty-version guard, partial-split backstop). **Phase B** wires them into the I/O layer: `ManifestWriter` switches its read+write to the Plan-1 `ManifestIO` (tolerant read, atomic `.bak` write) and gains case-insensitive idempotency + registry-URL normalisation; `MigrationRunner` reorders to **manifest-first, delete-last**, routes every delete through `PathSafety` + `GitGuard`, and aborts the whole apply before touching disk if the manifest can't be written.

**Tech Stack:** Unity 2022.3, C#, Newtonsoft.Json, Unity Test Framework. Depends on Plan 1's `PSV.Installer.Common.ManifestIO` and `SemVer` (already on `main`).

**Decisions locked:** keep direct manifest write (no `Client.AddAndRemove`); git-precondition = **block** delete when path isn't git-tracked-and-clean (non-git clients are refused, not silently risked); manifest format = JObject round-trip; split safety = planner **drops** the `RemovePackage` for a partially-selected group (UI checkbox-linking is Plan 3 — this is the defensive backstop).

**Out of scope (Plan 3):** scanner read path (`ManifestProbe`) switching to `ManifestIO` + a ScanReport error state; bootstrap guards; UI (linked split checkboxes, async Apply, banners). This plan does not change `ManifestProbe`.

---

## File structure

| File | Change |
|---|---|
| `Editor/Common/PathSafety.cs` | **New.** `TryResolveContained(root, rel, out resolved, out error)` — canonicalise + containment. |
| `Editor/Common/GitGuard.cs` | **New.** `IsTrackedAndClean(absPath, out reason)` — shells `git`, refuses on any doubt. |
| `Editor/Migrator/MigrationPlanner.cs` | Modify: `ResolveVersion` treats empty as null; split loop drops `RemovePackage` for partial groups. |
| `Editor/Migrator/ManifestWriter.cs` | Modify: extract pure `TryApply(JObject, actions)`; `ApplyActions` uses `ManifestIO`; case-insensitive add idempotency; URL normalisation in registry lookup. |
| `Editor/Migrator/Migrator.cs` | Modify: manifest-first/delete-last ordering; `PathSafety` + `GitGuard` before each delete. |
| `Editor/Tests/PathSafetyTests.cs` | **New.** |
| `Editor/Tests/GitGuardTests.cs` | **New.** |
| `Editor/Tests/MigrationPlannerSafetyTests.cs` | **New.** |
| `Editor/Tests/ManifestWriterTests.cs` | **New.** |

> **.meta / test-run:** same as Plan 1 — implementers commit source only (no `.meta`); after all tasks, the human focuses Unity (generates `.meta`, compiles) and runs `Test Runner → EditMode → Run All`; then `.meta` is committed. New EditMode test files are picked up by the existing `PSV.Installer.Editor.Tests` asmdef automatically (same folder). `GitGuardTests` requires `git` on PATH.

---

# PHASE A — Pure, unit-testable safety

## Task 1: `PathSafety.TryResolveContained`

**Files:** Create `Editor/Common/PathSafety.cs`; Test `Editor/Tests/PathSafetyTests.cs`.

- [ ] **Step 1: Write the failing tests**

`Editor/Tests/PathSafetyTests.cs`:

```csharp
using System.IO;
using NUnit.Framework;
using PSV.Installer.Common;

namespace PSV.Installer.Tests
{
    public sealed class PathSafetyTests
    {
        private static string Root => Path.Combine(Path.GetTempPath(), "psvroot", "Assets");

        [Test]
        public void Contained_subpath_resolves()
        {
            var ok = PathSafety.TryResolveContained(Root, "Plugins/Foo", out var resolved, out var err);
            Assert.IsTrue(ok, err);
            StringAssert.EndsWith("Assets/Plugins/Foo", resolved.Replace('\\', '/'));
        }

        [Test]
        public void Dot_segments_inside_are_allowed()
        {
            var ok = PathSafety.TryResolveContained(Root, "Plugins/./Bar", out var resolved, out _);
            Assert.IsTrue(ok);
            StringAssert.EndsWith("Assets/Plugins/Bar", resolved.Replace('\\', '/'));
        }

        [TestCase("../Outside")]
        [TestCase("../../etc")]
        [TestCase("Plugins/../../../etc")]
        public void Escaping_paths_are_rejected(string rel)
        {
            var ok = PathSafety.TryResolveContained(Root, rel, out var resolved, out var err);
            Assert.IsFalse(ok);
            Assert.IsNull(resolved);
            Assert.IsNotEmpty(err);
        }

        [Test]
        public void Rooted_absolute_path_is_rejected()
        {
            var ok = PathSafety.TryResolveContained(Root, Path.GetTempPath(), out _, out var err);
            Assert.IsFalse(ok);
            Assert.IsNotEmpty(err);
        }

        [Test]
        public void Empty_relative_is_rejected()
        {
            Assert.IsFalse(PathSafety.TryResolveContained(Root, "", out _, out _));
            Assert.IsFalse(PathSafety.TryResolveContained(Root, "   ", out _, out _));
        }

        [Test]
        public void Root_itself_is_rejected()
        {
            Assert.IsFalse(PathSafety.TryResolveContained(Root, ".", out _, out var err));
            Assert.IsNotEmpty(err);
        }
    }
}
```

- [ ] **Step 2: Run to verify failure** — Test Runner → EditMode → `PathSafetyTests`: compile error, `PathSafety` undefined.

- [ ] **Step 3: Implement**

`Editor/Common/PathSafety.cs`:

```csharp
using System;
using System.IO;

namespace PSV.Installer.Common
{
    /// <summary>
    /// Guards filesystem operations against escaping a trusted root directory.
    /// A migration deletes paths taken from the (auto-updated) catalog, so a stray
    /// <c>..</c> or rooted path must never be allowed to touch files outside <c>Assets/</c>.
    /// </summary>
    public static class PathSafety
    {
        /// <summary>
        /// Resolves <paramref name="relativePath"/> against <paramref name="rootAbsolute"/>,
        /// canonicalises it (collapsing <c>.</c>/<c>..</c>), and verifies the result stays
        /// strictly inside the root. Rejects empty input, rooted/absolute relatives, the root
        /// itself, and any path that escapes. On success <paramref name="resolved"/> is the
        /// canonical absolute path (forward-slash separators). On failure returns false with a
        /// human-readable <paramref name="error"/> and a null <paramref name="resolved"/>.
        /// </summary>
        public static bool TryResolveContained(string rootAbsolute, string relativePath, out string resolved, out string error)
        {
            resolved = null;
            error = null;

            if (string.IsNullOrWhiteSpace(rootAbsolute)) { error = "root is empty"; return false; }
            if (string.IsNullOrWhiteSpace(relativePath)) { error = "relative path is empty"; return false; }
            if (Path.IsPathRooted(relativePath)) { error = $"rooted path not allowed: '{relativePath}'"; return false; }

            string rootFull, candidate;
            try
            {
                rootFull = Path.GetFullPath(rootAbsolute).Replace('\\', '/').TrimEnd('/');
                candidate = Path.GetFullPath(Path.Combine(Path.GetFullPath(rootAbsolute), relativePath)).Replace('\\', '/').TrimEnd('/');
            }
            catch (Exception e) { error = $"path resolve failed: {e.Message}"; return false; }

            if (string.Equals(candidate, rootFull, StringComparison.OrdinalIgnoreCase))
            {
                error = "refusing to operate on the root itself";
                return false;
            }

            if (!candidate.StartsWith(rootFull + "/", StringComparison.OrdinalIgnoreCase))
            {
                error = $"path escapes root: '{relativePath}'";
                return false;
            }

            resolved = candidate;
            return true;
        }
    }
}
```

- [ ] **Step 4: Run to verify pass** — `PathSafetyTests` all PASS.

- [ ] **Step 5: Commit**

```bash
git add Editor/Common/PathSafety.cs Editor/Tests/PathSafetyTests.cs
git commit -m "feat(installer): PathSafety.TryResolveContained — reject Assets/ escapes"
```

---

## Task 2: `GitGuard.IsTrackedAndClean`

**Files:** Create `Editor/Common/GitGuard.cs`; Test `Editor/Tests/GitGuardTests.cs`.

- [ ] **Step 1: Write the failing tests**

`Editor/Tests/GitGuardTests.cs` (requires `git` on PATH):

```csharp
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using PSV.Installer.Common;

namespace PSV.Installer.Tests
{
    public sealed class GitGuardTests
    {
        private string _repo;

        private static void Git(string dir, string args)
        {
            var psi = new ProcessStartInfo("git", args)
            {
                WorkingDirectory = dir, RedirectStandardOutput = true,
                RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true,
            };
            using (var p = Process.Start(psi)) { p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd(); p.WaitForExit(10000); }
        }

        [SetUp] public void SetUp()
        {
            _repo = Path.Combine(Path.GetTempPath(), "psvgit_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_repo);
            Git(_repo, "init");
            Git(_repo, "config user.email t@t.t");
            Git(_repo, "config user.name t");
        }

        [TearDown] public void TearDown()
        {
            // .git holds read-only files on Windows; best-effort cleanup.
            try { if (Directory.Exists(_repo)) { foreach (var f in Directory.GetFiles(_repo, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal); Directory.Delete(_repo, true); } } catch { }
        }

        [Test]
        public void Tracked_and_clean_is_true()
        {
            var dir = Path.Combine(_repo, "Plugins"); Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "a.txt"), "x");
            Git(_repo, "add -A"); Git(_repo, "commit -m init");

            Assert.IsTrue(GitGuard.IsTrackedAndClean(dir, out var reason), reason);
        }

        [Test]
        public void Untracked_is_false()
        {
            var dir = Path.Combine(_repo, "New"); Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "b.txt"), "x");
            Assert.IsFalse(GitGuard.IsTrackedAndClean(dir, out var reason));
            Assert.IsNotEmpty(reason);
        }

        [Test]
        public void Modified_is_false()
        {
            var dir = Path.Combine(_repo, "Mod"); Directory.CreateDirectory(dir);
            var f = Path.Combine(dir, "c.txt");
            File.WriteAllText(f, "x");
            Git(_repo, "add -A"); Git(_repo, "commit -m init");
            File.WriteAllText(f, "changed");
            Assert.IsFalse(GitGuard.IsTrackedAndClean(dir, out var reason));
            Assert.IsNotEmpty(reason);
        }

        [Test]
        public void Non_git_directory_is_false()
        {
            var plain = Path.Combine(Path.GetTempPath(), "psvplain_" + Path.GetRandomFileName());
            Directory.CreateDirectory(plain);
            try { Assert.IsFalse(GitGuard.IsTrackedAndClean(plain, out var reason)); Assert.IsNotEmpty(reason); }
            finally { Directory.Delete(plain, true); }
        }
    }
}
```

- [ ] **Step 2: Run to verify failure** — compile error, `GitGuard` undefined.

- [ ] **Step 3: Implement**

`Editor/Common/GitGuard.cs`:

```csharp
using System;
using System.Diagnostics;
using System.IO;

namespace PSV.Installer.Common
{
    /// <summary>
    /// The migrator has no backup of its own; it relies on the client's git for undo.
    /// This guard verifies that reliance actually holds for a given path before any
    /// irreversible deletion: the path must be inside a git work tree, tracked, and clean.
    /// On ANY uncertainty (not a repo, git missing, untracked, ignored, or dirty) it returns
    /// false with a reason — callers MUST refuse to delete.
    /// </summary>
    public static class GitGuard
    {
        public static bool IsTrackedAndClean(string absolutePath, out string reason)
        {
            reason = null;
            if (string.IsNullOrWhiteSpace(absolutePath)) { reason = "empty path"; return false; }

            var dir = Directory.Exists(absolutePath) ? absolutePath : Path.GetDirectoryName(absolutePath);
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) { reason = "path does not exist"; return false; }

            // Tracked? (also fails for git-ignored paths — exactly what we want to refuse.)
            if (!TryRunGit(dir, $"ls-files --error-unmatch \"{absolutePath}\"", out _, out var lsErr))
            {
                reason = $"path is not tracked by git (untracked or ignored): {Trim(lsErr)}";
                return false;
            }

            // Clean? (staged/unstaged/untracked changes under the path → non-empty output)
            if (!TryRunGit(dir, $"status --porcelain -- \"{absolutePath}\"", out var statusOut, out var statusErr))
            {
                reason = $"git status failed: {Trim(statusErr)}";
                return false;
            }
            if (!string.IsNullOrWhiteSpace(statusOut))
            {
                reason = "path has uncommitted changes — commit or stash before migrating";
                return false;
            }

            return true;
        }

        private static string Trim(string s) => string.IsNullOrEmpty(s) ? "" : s.Trim();

        private static bool TryRunGit(string workingDir, string args, out string stdout, out string stderr)
        {
            stdout = null; stderr = null;
            try
            {
                var psi = new ProcessStartInfo("git", args)
                {
                    WorkingDirectory = workingDir,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                using (var p = Process.Start(psi))
                {
                    stdout = p.StandardOutput.ReadToEnd();
                    stderr = p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(10000)) { try { p.Kill(); } catch { } stderr = "git timed out"; return false; }
                    return p.ExitCode == 0;
                }
            }
            catch (Exception e) { stderr = e.Message; return false; }
        }
    }
}
```

- [ ] **Step 4: Run to verify pass** — `GitGuardTests` all PASS (requires git on PATH).

- [ ] **Step 5: Commit**

```bash
git add Editor/Common/GitGuard.cs Editor/Tests/GitGuardTests.cs
git commit -m "feat(installer): GitGuard.IsTrackedAndClean — refuse deletes git can't recover"
```

---

## Task 3: `MigrationPlanner` — empty-version guard + partial-split backstop

**Files:** Modify `Editor/Migrator/MigrationPlanner.cs`; Test `Editor/Tests/MigrationPlannerSafetyTests.cs`.

- [ ] **Step 1: Write the failing tests**

`Editor/Tests/MigrationPlannerSafetyTests.cs`:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PSV.Installer.Catalog;
using PSV.Installer.Migrator;
using PSV.Installer.Scanner;

namespace PSV.Installer.Tests
{
    public sealed class MigrationPlannerSafetyTests
    {
        // Minimal selection stub.
        private sealed class Sel : ISelectionSet
        {
            private readonly HashSet<string> _ids;
            public Sel(params string[] ids) { _ids = new HashSet<string>(ids); }
            public bool IsSelected(string id) => _ids.Contains(id);
            public VersionTarget GetTarget(string id) => VersionTarget.Recommended;
        }

        private static PackageRecord Rec(string id, string rec, string min) =>
            new PackageRecord { Id = id, RecommendedVersion = rec, MinVersion = min };

        // ── Empty-version guard ───────────────────────────────────────────
        [Test]
        public void Empty_version_record_produces_no_AddPackage()
        {
            var catalog = new PackageCatalog { Packages = new List<PackageRecord> { Rec("com.x", "", null) } };
            var report = ScanReportFactory.With(
                new[] { ScanReportFactory.Pkg("com.x", PackageState.NotInstalled) });

            var plan = MigrationPlanner.Plan(catalog, report, new Sel("com.x"), out _);
            Assert.IsFalse(plan.OfType<AddPackage>().Any(), "empty version must not yield an AddPackage");
        }

        [Test]
        public void Valid_version_record_produces_AddPackage()
        {
            var catalog = new PackageCatalog { Packages = new List<PackageRecord> { Rec("com.x", "1.2.3", null) } };
            var report = ScanReportFactory.With(
                new[] { ScanReportFactory.Pkg("com.x", PackageState.NotInstalled) });

            var plan = MigrationPlanner.Plan(catalog, report, new Sel("com.x"), out _);
            var add = plan.OfType<AddPackage>().Single();
            Assert.AreEqual("1.2.3", add.Version);
        }

        // ── Partial-split backstop ────────────────────────────────────────
        [Test]
        public void Full_split_selection_removes_legacy()
        {
            var catalog = new PackageCatalog { Packages = new List<PackageRecord> {
                Rec("com.a", "1.0.0", null), Rec("com.b", "1.0.0", null) } };
            var report = ScanReportFactory.WithSplit(
                new[] {
                    ScanReportFactory.PkgLegacy("com.a", "legacy.base"),
                    ScanReportFactory.PkgLegacy("com.b", "legacy.base"),
                },
                new MigrationGroup("legacy.base", new[] { "com.a", "com.b" }));

            var plan = MigrationPlanner.Plan(catalog, report, new Sel("com.a", "com.b"), out var warnings);
            Assert.IsTrue(plan.OfType<RemovePackage>().Any(r => r.Id == "legacy.base"));
            Assert.IsFalse(warnings.OfType<PartialSplitWarning>().Any());
        }

        [Test]
        public void Partial_split_selection_drops_the_legacy_remove_and_warns()
        {
            var catalog = new PackageCatalog { Packages = new List<PackageRecord> {
                Rec("com.a", "1.0.0", null), Rec("com.b", "1.0.0", null) } };
            var report = ScanReportFactory.WithSplit(
                new[] {
                    ScanReportFactory.PkgLegacy("com.a", "legacy.base"),
                    ScanReportFactory.PkgLegacy("com.b", "legacy.base"),
                },
                new MigrationGroup("legacy.base", new[] { "com.a", "com.b" }));

            // Only com.a selected.
            var plan = MigrationPlanner.Plan(catalog, report, new Sel("com.a"), out var warnings);

            Assert.IsFalse(plan.OfType<RemovePackage>().Any(r => r.Id == "legacy.base"),
                "must NOT remove legacy when its full replacement set isn't selected");
            Assert.IsTrue(plan.OfType<AddPackage>().Any(a => a.Id == "com.a"),
                "the selected replacement is still added");
            Assert.IsTrue(warnings.OfType<PartialSplitWarning>().Any(w => w.LegacyId == "legacy.base"));
        }
    }
}
```

`Editor/Tests/ScanReportFactory.cs` (test helper — builds reports via the internal ctors, accessible through InternalsVisibleTo):

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using PSV.Installer.Scanner;

namespace PSV.Installer.Tests
{
    internal static class ScanReportFactory
    {
        public static PackageScanResult Pkg(string id, PackageState state) =>
            (PackageScanResult)Activator.CreateInstance(
                typeof(PackageScanResult),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new object[] { id, id, state, null, null, null }, null);

        public static PackageScanResult PkgLegacy(string id, string legacyNpmId) =>
            (PackageScanResult)Activator.CreateInstance(
                typeof(PackageScanResult),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new object[] { id, id, PackageState.LegacyUpm, "1.0.0", legacyNpmId, null }, null);

        public static ScanReport With(IEnumerable<PackageScanResult> packages) =>
            Build(packages, Array.Empty<MigrationGroup>());

        public static ScanReport WithSplit(IEnumerable<PackageScanResult> packages, params MigrationGroup[] groups) =>
            Build(packages, groups);

        private static ScanReport Build(IEnumerable<PackageScanResult> packages, IReadOnlyList<MigrationGroup> groups) =>
            (ScanReport)Activator.CreateInstance(
                typeof(ScanReport),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null,
                new object[] { "v", DateTime.UtcNow, packages.ToList(), new List<ExternalScanResult>(), new List<UninstallScanResult>(), groups, "hash" },
                null);
    }
}
```

> Note: `PackageScanResult` / `ScanReport` ctors are `internal`; the test assembly reaches them via the `InternalsVisibleTo` added in Plan 1. Reflection is used because the ctors take many positional args — it keeps the helper compact and tolerant of param tweaks. `PackageRecord` fields are public.

- [ ] **Step 2: Run to verify failure** — `Empty_version_record_produces_no_AddPackage` and `Partial_split_selection_drops_the_legacy_remove_and_warns` FAIL against current planner (empty version yields `AddPackage("com.x","")`; partial split keeps the `RemovePackage`).

- [ ] **Step 3a: Fix `ResolveVersion` (empty → null)**

In `Editor/Migrator/MigrationPlanner.cs`, replace the `ResolveVersion` method body:

```csharp
        private static string ResolveVersion(string recommended, string min, VersionTarget target)
        {
            recommended = string.IsNullOrEmpty(recommended) ? null : recommended;
            min         = string.IsNullOrEmpty(min)         ? null : min;
            return target == VersionTarget.Min
                ? (min ?? recommended)
                : (recommended ?? min);
        }
```

(The existing `targetVersion != null` guards then correctly skip when both versions are absent/empty.)

- [ ] **Step 3b: Add the partial-split backstop**

In the same file, in the `// ── Detect partial split migrations ──` block, replace the inner `if (unselectedSiblings.Count > 0)` statement with one that also drops the remove:

```csharp
                    if (unselectedSiblings.Count > 0)
                    {
                        warningList.Add(new PartialSplitWarning(group.LegacyId, selectedSiblings, unselectedSiblings));

                        // Safety backstop: never strip a legacy package whose full set of
                        // replacements isn't selected. Dropping the RemovePackage leaves the
                        // project in a re-scannable Conflict state rather than losing the
                        // unselected replacement's functionality. (UI checkbox-linking in
                        // Plan 3 prevents partial selection at the source; this guards the
                        // planner's public API against any caller that bypasses the UI.)
                        removes.RemoveAll(r => r.Id == group.LegacyId);
                    }
```

- [ ] **Step 4: Run to verify pass** — all `MigrationPlannerSafetyTests` PASS.

- [ ] **Step 5: Commit**

```bash
git add Editor/Migrator/MigrationPlanner.cs Editor/Tests/MigrationPlannerSafetyTests.cs Editor/Tests/ScanReportFactory.cs
git commit -m "fix(installer): planner drops empty-version adds and partial-split removes (data-loss backstop)"
```

---

# PHASE B — Integration (verified in Unity)

## Task 4: `ManifestWriter` — pure `TryApply` + case-insensitive idempotency + URL normalisation

**Files:** Modify `Editor/Migrator/ManifestWriter.cs`; Test `Editor/Tests/ManifestWriterTests.cs`.

This task extracts the pure mutation logic (`TryApply(JObject, actions) → bool modified`) so it is unit-testable without I/O, fixes case-insensitive add idempotency, and normalises registry URLs before matching. `ApplyActions` (the I/O entry point) is rewired in Task 5.

- [ ] **Step 1: Write the failing tests**

`Editor/Tests/ManifestWriterTests.cs`:

```csharp
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using PSV.Installer.Migrator;

namespace PSV.Installer.Tests
{
    public sealed class ManifestWriterTests
    {
        [Test]
        public void Add_is_idempotent_case_insensitively()
        {
            var m = JObject.Parse("{ \"dependencies\": { \"Com.X\": \"1.0.0\" } }");
            var modified = ManifestWriter.TryApply(m, new MigrationAction[] { new AddPackage("com.x", "2.0.0") });
            Assert.IsFalse(modified, "an existing dependency differing only in case must not be re-added");
            Assert.AreEqual(1, ((JObject)m["dependencies"]).Count, "no duplicate entry");
        }

        [Test]
        public void Add_new_package_inserts_entry()
        {
            var m = JObject.Parse("{ \"dependencies\": {} }");
            var modified = ManifestWriter.TryApply(m, new MigrationAction[] { new AddPackage("com.x", "1.0.0") });
            Assert.IsTrue(modified);
            Assert.AreEqual("1.0.0", (string)m["dependencies"]["com.x"]);
        }

        [Test]
        public void Remove_package_drops_entry()
        {
            var m = JObject.Parse("{ \"dependencies\": { \"com.x\": \"1.0.0\" } }");
            var modified = ManifestWriter.TryApply(m, new MigrationAction[] { new RemovePackage("com.x") });
            Assert.IsTrue(modified);
            Assert.IsNull(m["dependencies"]["com.x"]);
        }

        [Test]
        public void Registry_match_ignores_trailing_slash()
        {
            // Existing registry has NO trailing slash; action URL HAS one → must merge, not duplicate.
            var m = JObject.Parse(
                "{ \"scopedRegistries\": [ { \"name\": \"PSV\", \"url\": \"https://npm.psvgamestudio.com\", \"scopes\": [\"com.a\"] } ] }");
            var modified = ManifestWriter.TryApply(m, new MigrationAction[] {
                new AddScopedRegistry("PSV", "https://npm.psvgamestudio.com/", "com.b") });

            Assert.IsTrue(modified);
            var regs = (JArray)m["scopedRegistries"];
            Assert.AreEqual(1, regs.Count, "must not create a second registry for the same URL modulo trailing slash");
            var scopes = (JArray)regs[0]["scopes"];
            CollectionAssert.AreEquivalent(new[] { "com.a", "com.b" }, scopes.ToObject<string[]>());
        }

        [Test]
        public void New_registry_added_when_url_absent()
        {
            var m = JObject.Parse("{ }");
            var modified = ManifestWriter.TryApply(m, new MigrationAction[] {
                new AddScopedRegistry("PSV", "https://npm.psvgamestudio.com/", "com.a") });
            Assert.IsTrue(modified);
            Assert.AreEqual(1, ((JArray)m["scopedRegistries"]).Count);
        }
    }
}
```

- [ ] **Step 2: Run to verify failure** — compile error (`TryApply` not public/extant); the case-insensitive and trailing-slash tests would fail against current logic.

- [ ] **Step 3: Implement** — replace the body of `Editor/Migrator/ManifestWriter.cs` with:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using PSV.Installer.Common;

namespace PSV.Installer.Migrator
{
    /// <summary>
    /// Applies manifest-level <see cref="MigrationAction"/>s to <c>Packages/manifest.json</c>.
    /// Reads + writes through <see cref="ManifestIO"/> (tolerant parse, atomic write with
    /// <c>.bak</c>). All mutations are idempotent and case-insensitive on dependency ids;
    /// registry matching ignores trailing-slash differences. <see cref="BackupAndDeletePath"/>
    /// and non-manifest action types are ignored here (the runner handles deletes).
    /// </summary>
    internal static class ManifestWriter
    {
        /// <summary>
        /// I/O entry point. Reads the manifest via <see cref="ManifestIO"/>, applies all
        /// manifest-mutating actions, and writes atomically only if something changed.
        /// Throws on a missing/unparseable manifest (callers must wrap) so the runner can
        /// abort BEFORE performing any irreversible delete.
        /// </summary>
        public static void ApplyActions(string manifestPath, IEnumerable<MigrationAction> actions)
        {
            if (string.IsNullOrEmpty(manifestPath)) throw new ArgumentNullException(nameof(manifestPath));
            if (actions == null) throw new ArgumentNullException(nameof(actions));

            var read = ManifestIO.Read(manifestPath);
            if (read.Status != ManifestReadStatus.Ok)
                throw new InvalidOperationException(
                    $"manifest.json could not be read ({read.Status}): {read.Error ?? manifestPath}");

            var manifest = read.Root;
            if (TryApply(manifest, actions))
            {
                ManifestIO.WriteAtomic(manifestPath, manifest);
                AssetDatabase.Refresh();
            }
        }

        /// <summary>
        /// Pure in-memory mutation. Applies all manifest-mutating actions to
        /// <paramref name="manifest"/>; returns true if anything changed. No I/O.
        /// </summary>
        public static bool TryApply(JObject manifest, IEnumerable<MigrationAction> actions)
        {
            if (manifest == null) throw new ArgumentNullException(nameof(manifest));
            if (actions == null) throw new ArgumentNullException(nameof(actions));

            var modified = false;
            foreach (var action in actions)
            {
                switch (action)
                {
                    case AddPackage add:               modified |= ApplyAddPackage(manifest, add); break;
                    case RemovePackage remove:         modified |= ApplyRemovePackage(manifest, remove); break;
                    case UpdatePackageVersion update:  modified |= ApplyUpdatePackageVersion(manifest, update); break;
                    case AddScopedRegistry addReg:     modified |= ApplyAddScopedRegistry(manifest, addReg); break;
                    case AddScopeToRegistry addScope:  modified |= ApplyAddScopeToRegistry(manifest, addScope); break;
                    // BackupAndDeletePath and any other types are intentionally ignored.
                }
            }
            return modified;
        }

        // ── Mutation helpers ──────────────────────────────────────────────────

        private static bool ApplyAddPackage(JObject manifest, AddPackage action)
        {
            if (string.IsNullOrEmpty(action.Version)) return false; // never write an empty version
            var deps = EnsureDependencies(manifest);
            if (FindPropertyIgnoreCase(deps, action.Id) != null)
                return false; // already present (any casing) — idempotent
            deps[action.Id] = action.Version;
            return true;
        }

        private static bool ApplyRemovePackage(JObject manifest, RemovePackage action)
        {
            var deps = manifest["dependencies"] as JObject;
            if (deps == null) return false;
            var prop = FindPropertyIgnoreCase(deps, action.Id);
            if (prop == null) return false; // already absent — idempotent
            prop.Remove();
            return true;
        }

        private static bool ApplyUpdatePackageVersion(JObject manifest, UpdatePackageVersion action)
        {
            if (string.IsNullOrEmpty(action.Version)) return false;
            var deps = EnsureDependencies(manifest);
            var prop = FindPropertyIgnoreCase(deps, action.Id);
            if (prop != null)
            {
                if (string.Equals(prop.Value?.Value<string>(), action.Version, StringComparison.Ordinal))
                    return false;
                prop.Value = action.Version;
                return true;
            }
            deps[action.Id] = action.Version;
            return true;
        }

        private static bool ApplyAddScopedRegistry(JObject manifest, AddScopedRegistry action)
        {
            var registries = EnsureScopedRegistries(manifest);
            var existing = FindRegistryByUrl(registries, action.Url);
            if (existing != null)
                return AddScopeToBlock(existing, action.Scope);

            registries.Add(new JObject(
                new JProperty("name", action.Name),
                new JProperty("url", action.Url),
                new JProperty("scopes", new JArray(action.Scope))));
            return true;
        }

        private static bool ApplyAddScopeToRegistry(JObject manifest, AddScopeToRegistry action)
        {
            var registries = manifest["scopedRegistries"] as JArray;
            if (registries == null) return false;
            var existing = FindRegistryByUrl(registries, action.Url);
            if (existing == null) return false;
            return AddScopeToBlock(existing, action.Scope);
        }

        // ── Low-level helpers ─────────────────────────────────────────────────

        private static JObject EnsureDependencies(JObject manifest)
        {
            if (manifest["dependencies"] is JObject deps) return deps;
            deps = new JObject();
            manifest["dependencies"] = deps;
            return deps;
        }

        private static JArray EnsureScopedRegistries(JObject manifest)
        {
            if (manifest["scopedRegistries"] is JArray arr) return arr;
            arr = new JArray();
            manifest["scopedRegistries"] = arr;
            return arr;
        }

        private static JProperty FindPropertyIgnoreCase(JObject obj, string name)
        {
            foreach (var prop in obj.Properties())
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                    return prop;
            return null;
        }

        private static JObject FindRegistryByUrl(JArray registries, string url)
        {
            var target = NormaliseUrl(url);
            foreach (var token in registries)
                if (token is JObject obj && NormaliseUrl(obj["url"]?.Value<string>()) == target)
                    return obj;
            return null;
        }

        private static string NormaliseUrl(string url) =>
            string.IsNullOrEmpty(url) ? string.Empty : url.TrimEnd('/').ToLowerInvariant();

        private static bool AddScopeToBlock(JObject block, string scope)
        {
            var scopes = block["scopes"] as JArray ?? new JArray();
            block["scopes"] = scopes;
            if (scopes.Any(s => string.Equals(s.Value<string>(), scope, StringComparison.OrdinalIgnoreCase)))
                return false;
            scopes.Add(scope);
            return true;
        }
    }
}
```

- [ ] **Step 4: Run to verify pass** — `ManifestWriterTests` all PASS.

- [ ] **Step 5: Commit**

```bash
git add Editor/Migrator/ManifestWriter.cs Editor/Tests/ManifestWriterTests.cs
git commit -m "feat(installer): ManifestWriter via ManifestIO; case-insensitive idempotency + URL-normalised registry merge"
```

---

## Task 5: `MigrationRunner` — manifest-first ordering + containment + git precondition

**Files:** Modify `Editor/Migrator/Migrator.cs`. (Integration — verified in Unity; the safety logic it calls is unit-tested in Tasks 1, 2, 4.)

- [ ] **Step 1: Replace `Apply` and the delete plumbing**

In `Editor/Migrator/Migrator.cs`, add `using PSV.Installer.Common;` to the using block, then replace the `Apply` method with:

```csharp
        /// <summary>
        /// Executes the migration plan. Order is deliberate and safety-critical:
        /// (1) apply manifest mutations FIRST via the atomic <see cref="ManifestWriter"/> —
        /// if that fails, nothing on disk has been deleted; (2) only then delete legacy asset
        /// paths, each guarded by <see cref="PathSafety"/> (no escaping Assets/) and
        /// <see cref="GitGuard"/> (refuse what git can't recover). A delete failure leaves the
        /// project in a re-scannable Conflict state, never a void.
        /// </summary>
        public ApplyResult Apply(IReadOnlyList<MigrationAction> plan)
        {
            if (plan == null || plan.Count == 0)
                return new ApplyResult(true, 0, Array.Empty<string>());

            var failures = new List<string>();
            var executedCount = 0;

            if (!File.Exists(_manifestPath))
                return Fail(failures, $"manifest.json not found at '{_manifestPath}'.");

            // ── 1. Manifest mutations FIRST (atomic; aborts before any delete on failure) ──
            var manifestMutations = plan.Where(a => !(a is BackupAndDeletePath)).ToList();
            if (manifestMutations.Count > 0)
            {
                try
                {
                    ManifestWriter.ApplyActions(_manifestPath, manifestMutations);
                    executedCount += manifestMutations.Count;
                }
                catch (Exception e)
                {
                    failures.Add($"ManifestWriter.ApplyActions failed: {e.Message}");
                    return new ApplyResult(false, executedCount, failures);
                }
            }

            // ── 2. Delete legacy asset paths LAST (irreversible) ──
            var assetsRoot = Application.dataPath;
            foreach (var action in plan.OfType<BackupAndDeletePath>())
            {
                if (!PathSafety.TryResolveContained(assetsRoot, action.RelativePath, out var absolutePath, out var pathError))
                {
                    failures.Add($"DeletePath({action.RelativePath}): {pathError}");
                    return new ApplyResult(false, executedCount, failures);
                }

                if (!GitGuard.IsTrackedAndClean(absolutePath, out var gitReason))
                {
                    failures.Add($"DeletePath({action.RelativePath}): refusing to delete — {gitReason}");
                    return new ApplyResult(false, executedCount, failures);
                }

                if (!DeletePath(absolutePath, out var deleteError))
                {
                    failures.Add($"DeletePath({action.RelativePath}): {deleteError}");
                    return new ApplyResult(false, executedCount, failures);
                }
                executedCount++;
            }

            Debug.Log($"{LogPrefix} Apply complete. {executedCount} action(s) executed.");
            return new ApplyResult(true, executedCount, Array.Empty<string>());
        }
```

Leave `DeletePath`, `Fail`, `DeriveProjectRoot`, and `NormaliseSeparators` as they are — `DeletePath` now always receives a contained, canonical absolute path under `Assets/`, so its existing `StartsWith(Application.dataPath)` branch routes through `AssetDatabase.DeleteAsset` (correct `.meta` cleanup). Remove the now-unused `NormaliseSeparators` only if the compiler flags it as unused after this change; otherwise leave it.

- [ ] **Step 2: Update the class docstring** — change the class summary above `public sealed class MigrationRunner` to reflect the new guarantees:

```csharp
    /// <summary>
    /// Executes a migration plan produced by <see cref="MigrationPlanner"/>: applies manifest
    /// mutations atomically (via <see cref="ManifestWriter"/>/<see cref="ManifestIO"/>) BEFORE
    /// deleting any legacy assets, and refuses to delete a path that escapes Assets/ or that
    /// git cannot recover (<see cref="PathSafety"/>, <see cref="GitGuard"/>).
    ///
    /// No built-in backup: recovery for deleted assets is delegated to the client's git, which
    /// is why <see cref="GitGuard"/> blocks deletion of untracked/ignored/dirty paths.
    /// </summary>
```

- [ ] **Step 3: Verify in Unity** — focus Unity (compile), confirm no compile errors. There is no automated test for `MigrationRunner` (it binds to `Application.dataPath`/`AssetDatabase`); the safety primitives it calls are covered by Tasks 1, 2, 4. Manual smoke (optional, do not commit project changes): in a scratch project, point a plan at a `BackupAndDeletePath("../Outside")` and confirm `Apply` returns a failure with "path escapes root" and deletes nothing.

- [ ] **Step 4: Commit**

```bash
git add Editor/Migrator/Migrator.cs
git commit -m "fix(installer): MigrationRunner manifest-first ordering + PathSafety/GitGuard before deletes"
```

---

## Self-review (done while writing)

- **Spec coverage:** containment → Task 1+5; git-precondition → Task 2+5; manifest-first ordering → Task 5; atomic write + tolerant read in the apply path → Task 4; partial-split backstop → Task 3; empty-version guard → Task 3; case-insensitive idempotency + URL normalisation → Task 4. Scanner read path (`ManifestProbe`) + UI = **deferred to Plan 3** (stated in header).
- **Type consistency:** `PathSafety.TryResolveContained(root, rel, out resolved, out error)`, `GitGuard.IsTrackedAndClean(abs, out reason)`, `ManifestWriter.TryApply(JObject, IEnumerable<MigrationAction>) → bool` and `ManifestWriter.ApplyActions(string, IEnumerable<MigrationAction>)` are referenced identically across tasks. `MigrationPlanner.Plan` signature unchanged. Action types (`AddPackage`, `RemovePackage`, `AddScopedRegistry`) match `MigrationAction.cs`.
- **No placeholders:** every code/test/command step is concrete. `ManifestWriter.TryApply` is made `public` so the test assembly (separate assembly) can call it without relying on InternalsVisibleTo for `ManifestWriter` (which is `internal`); since the whole Editor assembly is InternalsVisibleTo the tests anyway, an `internal` `TryApply` would also work — `public` is used for clarity and matches how tests call it.

---

## Execution notes

- Phase A (Tasks 1-3) is fully unit-testable and independent — natural first batch.
- Phase B (Tasks 4-5): Task 4 is unit-testable; Task 5 is Unity-verified only.
- `GitGuardTests` needs `git` on PATH (present in this dev environment).
- After all tasks: human runs `Test Runner → EditMode → Run All`, then `.meta` files are committed, then `finishing-a-development-branch`.

## Next plan

- **Plan 3 — Bootstrap + UI safety:** `Application.isBatchMode`/play-mode guards; poll `Client.Add` + surface failures; per-session `SessionState` guard; catalog `schemaVersion` upper-bound + distinguish parse-fail from not-installed; `ManifestProbe` → `ManifestIO` with a ScanReport error state; UI linked split-group checkboxes, async Apply with a persistent result banner surviving the domain reload, stale-report indicator, friendly "all up to date" empty state.
