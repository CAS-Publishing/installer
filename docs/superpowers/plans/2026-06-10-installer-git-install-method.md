# Git Install Method (sub-project A) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a global UPM/Git install-method selector to the installer; in Git mode the installer writes git-URL dependencies (clean, no scoped registry) for CAS and Tenjin, with Firebase falling back to UPM.

**Architecture:** Catalog gains a per-component `git` block listing the flat dependency chain (id/url/tag). A new `AddGitPackage` migration action writes `"id": "url#tag"` to `manifest.json`. `MigrationPlanner` takes the chosen `InstallMethod`; in Git mode it emits `AddGitPackage` for each chain entry (no `AddScopedRegistry`), or falls back to the existing UPM plan + warning when a component has no `git` block. The method is a per-project `EditorPrefs` flag set by a restored vertical selector on Welcome. Detection treats a git-URL dependency value as "installed (git)".

**Tech Stack:** Unity 2022.3 C# (Editor), Newtonsoft.Json, UI Toolkit (UXML/USS), NUnit (EditMode tests).

> **Running tests:** All tests are Unity **EditMode** (NUnit) under `Editor/Tests/`. There is **no CLI runner** — run via **Window → General → Test Runner → EditMode → Run All** in the Unity editor. A subagent writes the code + tests but **cannot execute Unity**; the owner runs the Test Runner and reports pass/fail. Treat "Run test" steps as "owner runs in Unity Test Runner".

> **Prerequisite (owner-run, outside this plan):** mirror the Tenjin SDK to a new repo so the Tenjin git URL resolves:
> ```powershell
> & "E:\workspace\casai\ci\scripts\publish-to-github.ps1" -PackageId com.tenjin.sdk -RepoName tenjin-sdk -Version 1.15.14
> ```
> This creates `CAS-Publishing/tenjin-sdk` (distinct from the existing `tenjin` wrapper repo). Task 9's catalog entry points at it.

---

## File structure

| File | Responsibility |
|---|---|
| `Editor/Migrator/MigrationAction.cs` (modify) | New `AddGitPackage` action type. |
| `Editor/Migrator/ManifestWriter.cs` (modify) | Write a git-URL dependency for `AddGitPackage`. |
| `Editor/Catalog/Catalog.cs` (modify) | `GitInstall` + `GitPackage` data classes; `git` field on records. |
| `Editor/Migrator/MigrationPlanner.cs` (modify) | Method-aware planning; git chain emission + UPM fallback. |
| `Editor/Common/InstallMethod.cs` (create) | `InstallMethod` enum + per-project persistence. |
| `Editor/Scanner/StateClassifier.cs` (modify) | Treat git-URL dependency values as installed-via-git. |
| `Editor/Wizard/ComponentStatusProvider.cs` (modify) | Surface "Installed (git)". |
| `Editor/Wizard/WizardActions.cs` (modify) | Pass the chosen method into the planner. |
| `Editor/Wizard/AutoInstaller.cs` (modify) | Pass the chosen method into the planner. |
| `Editor/Wizard/Uxml/Welcome.uxml` (modify) | Restore vertical method cards. |
| `Editor/Wizard/Screens/WelcomeScreen.cs` (modify) | Radio logic + persist method. |
| `com.psvgamestudio.installer.metadata/catalog.json` (modify) | `git` blocks for CAS + Tenjin. |
| `Editor/Tests/*` (create) | EditMode tests for the pure logic. |

---

## Task 1: AddGitPackage action

**Files:**
- Modify: `Editor/Migrator/MigrationAction.cs`

- [ ] **Step 1: Add the action type**

Append after the `RemovePackage` class in `Editor/Migrator/MigrationAction.cs`:

```csharp
/// <summary>
/// Add (or, idempotently, leave) a git-URL entry under <c>dependencies</c> in manifest.json,
/// e.g. <c>"com.tenjin.sdk": "https://github.com/CAS-Publishing/tenjin-sdk.git#1.15.14"</c>.
/// Used by the Git install method instead of a registry + version pair.
/// </summary>
public sealed class AddGitPackage : MigrationAction
{
    /// <summary>UPM package id to add.</summary>
    public string Id { get; }

    /// <summary>Full git dependency spec written as the value: <c>url#tag</c>.</summary>
    public string Spec { get; }

    public AddGitPackage(string id, string spec)
    {
        Id   = id;
        Spec = spec;
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Editor/Migrator/MigrationAction.cs
git commit -m "feat(installer): add AddGitPackage migration action"
```

---

## Task 2: ManifestWriter writes git dependencies

**Files:**
- Modify: `Editor/Migrator/ManifestWriter.cs`
- Test: `Editor/Tests/ManifestWriterGitTests.cs` (create)

- [ ] **Step 1: Write the failing test**

Create `Editor/Tests/ManifestWriterGitTests.cs`:

```csharp
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using PSV.Installer.Migrator;

namespace PSV.Installer.Tests
{
    public class ManifestWriterGitTests
    {
        [Test]
        public void AddGitPackage_writes_url_spec_as_dependency_value()
        {
            var manifest = JObject.Parse("{\"dependencies\":{}}");
            var changed = ManifestWriter.TryApply(manifest, new[] {
                new AddGitPackage("com.tenjin.sdk", "https://github.com/CAS-Publishing/tenjin-sdk.git#1.15.14")
            });

            Assert.IsTrue(changed);
            Assert.AreEqual("https://github.com/CAS-Publishing/tenjin-sdk.git#1.15.14",
                manifest["dependencies"]["com.tenjin.sdk"].Value<string>());
        }

        [Test]
        public void AddGitPackage_is_idempotent_when_id_already_present()
        {
            var manifest = JObject.Parse("{\"dependencies\":{\"com.tenjin.sdk\":\"1.15.14\"}}");
            var changed = ManifestWriter.TryApply(manifest, new[] {
                new AddGitPackage("com.tenjin.sdk", "https://github.com/CAS-Publishing/tenjin-sdk.git#1.15.14")
            });

            Assert.IsFalse(changed); // present in any form → left as-is (cross-method switch is out of scope)
        }

        [Test]
        public void AddGitPackage_empty_spec_writes_nothing()
        {
            var manifest = JObject.Parse("{\"dependencies\":{}}");
            var changed = ManifestWriter.TryApply(manifest, new[] { new AddGitPackage("com.x", "") });
            Assert.IsFalse(changed);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run in Unity Test Runner (EditMode). Expected: FAIL — `TryApply` does not handle `AddGitPackage` (compile error on the type until Task 1 is present; with Task 1, the test asserting the value fails because nothing is written).

- [ ] **Step 3: Implement the handler**

In `Editor/Migrator/ManifestWriter.cs`, add a case to the `switch` in `TryApply` (after the `AddPackage` case at line 57):

```csharp
                    case AddGitPackage addGit:         modified |= ApplyAddGitPackage(manifest, addGit); break;
```

Add the helper next to `ApplyAddPackage`:

```csharp
        private static bool ApplyAddGitPackage(JObject manifest, AddGitPackage action)
        {
            if (string.IsNullOrEmpty(action.Spec)) return false; // never write an empty spec
            var deps = EnsureDependencies(manifest);
            if (FindPropertyIgnoreCase(deps, action.Id) != null)
                return false; // already present (any form) — idempotent; switching method is out of scope
            deps[action.Id] = action.Spec;
            return true;
        }
```

- [ ] **Step 4: Run test to verify it passes**

Run in Unity Test Runner (EditMode). Expected: PASS (all three `ManifestWriterGitTests`).

- [ ] **Step 5: Commit**

```bash
git add Editor/Migrator/ManifestWriter.cs Editor/Tests/ManifestWriterGitTests.cs
git commit -m "feat(installer): ManifestWriter writes git-URL dependencies"
```

---

## Task 3: Catalog git schema

**Files:**
- Modify: `Editor/Catalog/Catalog.cs`

- [ ] **Step 1: Add the data classes**

In `Editor/Catalog/Catalog.cs`, add a `git` field to `ExternalRecord` (after its `assetMarkers` field) and to `PackageRecord` (after its `config` field):

```csharp
        /// <summary>
        /// Optional git-install chain for this component. When the Git method is chosen, the
        /// installer writes one git-URL dependency per entry here (top-level + transitive),
        /// with no scoped registry. Absent → git method falls back to UPM for this component.
        /// </summary>
        [JsonProperty("git")]                public GitInstall Git;
```

Add these classes at the end of the namespace (before the closing brace):

```csharp
    /// <summary>The flat set of packages to write as git-URL dependencies for one component.</summary>
    public sealed class GitInstall
    {
        [JsonProperty("packages")] public List<GitPackage> Packages;
    }

    /// <summary>One git-URL dependency: id plus repo URL plus tag.</summary>
    public sealed class GitPackage
    {
        [JsonProperty("id")]  public string Id;
        [JsonProperty("url")] public string Url;
        [JsonProperty("tag")] public string Tag;

        /// <summary>The manifest dependency value: <c>url#tag</c>.</summary>
        public string Spec => string.IsNullOrEmpty(Tag) ? Url : $"{Url}#{Tag}";
    }
```

- [ ] **Step 2: Commit**

```bash
git add Editor/Catalog/Catalog.cs
git commit -m "feat(installer): catalog git-install schema (GitInstall/GitPackage)"
```

---

## Task 4: InstallMethod state

**Files:**
- Create: `Editor/Common/InstallMethod.cs`

- [ ] **Step 1: Create the enum + persistence**

Create `Editor/Common/InstallMethod.cs`:

```csharp
using UnityEditor;
using UnityEngine;

namespace PSV.Installer.Common
{
    /// <summary>How the installer writes component dependencies into manifest.json.</summary>
    public enum InstallMethod
    {
        /// <summary>Scoped registry + version (the default).</summary>
        Upm,
        /// <summary>git-URL dependencies (clean, no scoped registry).</summary>
        Git,
    }

    /// <summary>
    /// Per-project persistence of the chosen install method. EditorPrefs is machine-global, so the
    /// key includes the project data path to scope it to THIS project (mirrors the IntroDone pattern).
    /// </summary>
    public static class InstallMethodState
    {
        private static string Key => "PSV.Installer.InstallMethod:" + Application.dataPath;

        public static InstallMethod Get() =>
            EditorPrefs.GetInt(Key, (int)InstallMethod.Upm) == (int)InstallMethod.Git
                ? InstallMethod.Git : InstallMethod.Upm;

        public static void Set(InstallMethod method) => EditorPrefs.SetInt(Key, (int)method);
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Editor/Common/InstallMethod.cs
git commit -m "feat(installer): per-project InstallMethod state"
```

---

## Task 5: MigrationPlanner — method-aware git planning

**Files:**
- Modify: `Editor/Migrator/MigrationPlanner.cs`
- Test: `Editor/Tests/MigrationPlannerGitTests.cs` (create)

The planner's public `Plan` currently has signature `Plan(catalog, report, selection, out warnings)`. Add an `InstallMethod method` parameter with a default so existing callers keep compiling, then branch on it.

- [ ] **Step 1a: Extend ScanReportFactory with external helpers**

`Editor/Tests/ScanReportFactory.cs` currently only builds package-reports (externals are empty). Add an `Ext` result builder and a `WithExternals` report builder so the planner test is deterministic (no dependency on the real project manifest). Append inside the class:

```csharp
        public static ExternalScanResult Ext(string id, ExternalState state) =>
            (ExternalScanResult)Activator.CreateInstance(
                typeof(ExternalScanResult),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new object[] { id, id, state, null }, null);

        public static ScanReport WithExternals(IEnumerable<ExternalScanResult> externals) =>
            (ScanReport)Activator.CreateInstance(
                typeof(ScanReport),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null,
                new object[] { "v", DateTime.UtcNow, new List<PackageScanResult>(), externals.ToList(),
                    new List<UninstallScanResult>(), Array.Empty<MigrationGroup>(), "hash", null },
                null);
```

(`ExternalScanResult`'s constructor is `(id, displayName, state, detectedVersion)` after the #4 changes — four args.)

- [ ] **Step 1b: Write the failing test**

Create `Editor/Tests/MigrationPlannerGitTests.cs`. Builds a catalog + a deterministic NotInstalled external report via the factory:

```csharp
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PSV.Installer.Catalog;
using PSV.Installer.Common;
using PSV.Installer.Migrator;
using PSV.Installer.Scanner;

namespace PSV.Installer.Tests
{
    public class MigrationPlannerGitTests
    {
        private sealed class OneId : ISelectionSet
        {
            private readonly string _id;
            public OneId(string id) { _id = id; }
            public bool IsSelected(string id) => id == _id;
            public VersionTarget GetTarget(string id) => VersionTarget.Recommended;
        }

        private static PackageCatalog CatalogWithGitExternal() => new PackageCatalog
        {
            Registries = new Dictionary<string, string> { { "psv", "https://npm.psvgamestudio.com/" } },
            External = new List<ExternalRecord>
            {
                new ExternalRecord
                {
                    Id = "com.tenjin.sdk", DisplayName = "Tenjin", Registry = "psv",
                    Scopes = new List<string> { "com.tenjin" },
                    RecommendedVersion = "1.15.14",
                    Git = new GitInstall
                    {
                        Packages = new List<GitPackage>
                        {
                            new GitPackage { Id = "com.tenjin.sdk", Url = "https://github.com/CAS-Publishing/tenjin-sdk.git", Tag = "1.15.14" }
                        }
                    }
                }
            }
        };

        [Test]
        public void GitMode_emits_AddGitPackage_for_each_chain_entry_and_no_registry()
        {
            var catalog = CatalogWithGitExternal();
            var report = ScanReportFactory.WithExternals(new[] {
                ScanReportFactory.Ext("com.tenjin.sdk", ExternalState.NotInstalled) });

            var plan = MigrationPlanner.Plan(catalog, report, new OneId("com.tenjin.sdk"),
                InstallMethod.Git, out _);

            Assert.AreEqual(1, plan.OfType<AddGitPackage>().Count());
            Assert.AreEqual("https://github.com/CAS-Publishing/tenjin-sdk.git#1.15.14",
                plan.OfType<AddGitPackage>().Single().Spec);
            Assert.IsFalse(plan.OfType<AddScopedRegistry>().Any(), "git mode must not register a scope");
            Assert.IsFalse(plan.OfType<AddPackage>().Any(), "git mode must not add a registry version");
        }

        [Test]
        public void UpmMode_unchanged_still_emits_registry_and_version()
        {
            var catalog = CatalogWithGitExternal();
            var report = ScanReportFactory.WithExternals(new[] {
                ScanReportFactory.Ext("com.tenjin.sdk", ExternalState.NotInstalled) });

            var plan = MigrationPlanner.Plan(catalog, report, new OneId("com.tenjin.sdk"),
                InstallMethod.Upm, out _);

            Assert.IsTrue(plan.OfType<AddPackage>().Any());
            Assert.IsFalse(plan.OfType<AddGitPackage>().Any());
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run in Unity Test Runner (EditMode). Expected: FAIL — `Plan` has no `InstallMethod` overload.

- [ ] **Step 3: Add the method parameter and git branch**

In `Editor/Migrator/MigrationPlanner.cs`, change the `Plan` signature to add the method (default keeps old callers compiling):

```csharp
        public static IReadOnlyList<MigrationAction> Plan(
            PackageCatalog catalog,
            ScanReport report,
            ISelectionSet selection,
            InstallMethod method,
            out IReadOnlyList<PlannerWarning> warnings)
```

Add `using PSV.Installer.Common;` at the top.

Add a git-mode accumulator and short-circuit. Right after the `pkgAdds` accumulator is declared (near line 94), add:

```csharp
            var gitAdds = new List<MigrationAction>(); // AddGitPackage entries (git method)
```

In the external loop (where `PlanForExternal` is called) and the package loop (where `PlanForPackage` is called), branch on the method BEFORE the existing UPM logic. Replace the body of the external loop:

```csharp
                    externalRecordById.TryGetValue(result.Id, out var record);
                    if (method == InstallMethod.Git && TryPlanGit(record?.Git, gitAdds, warningList, result.Id))
                        continue; // git chain emitted; skip UPM planning for this component
                    var target = selection.GetTarget(result.Id);
                    PlanForExternal(result, record, catalog, target, regAdds, pkgAdds, warningList);
```

Replace the body of the package loop similarly:

```csharp
                    packageRecordById.TryGetValue(result.Id, out var record);
                    if (method == InstallMethod.Git && TryPlanGit(record?.Git, gitAdds, warningList, result.Id))
                        continue;
                    var target = selection.GetTarget(result.Id);
                    PlanForPackage(result, record, target, backups, removes, pkgAdds, removedIds);
```

Add the helper (anywhere among the private methods):

```csharp
        /// <summary>
        /// Emits an <see cref="AddGitPackage"/> for each entry in the component's git chain. Returns
        /// true when git planning handled the component (a git block was present). Returns false (with
        /// a warning) when there is no git block, so the caller falls back to UPM planning.
        /// </summary>
        private static bool TryPlanGit(GitInstall git, List<MigrationAction> gitAdds,
            List<PlannerWarning> warnings, string componentId)
        {
            if (git?.Packages == null || git.Packages.Count == 0)
            {
                warnings.Add(new PlannerWarning(componentId,
                    $"No git source for '{componentId}' — falling back to UPM for this component."));
                return false;
            }
            foreach (var p in git.Packages)
                if (p != null && !string.IsNullOrEmpty(p.Id) && !string.IsNullOrEmpty(p.Url))
                    gitAdds.Add(new AddGitPackage(p.Id, p.Spec));
            return true;
        }
```

Finally, include `gitAdds` when assembling the action list (near line 200, where `actions.AddRange(...)` calls are). Add after `actions.AddRange(pkgAdds);`:

```csharp
            actions.AddRange(gitAdds);
```

And bump the capacity hint to include `gitAdds.Count`.

- [ ] **Step 4: Run test to verify it passes**

Run in Unity Test Runner (EditMode). Expected: PASS (`MigrationPlannerGitTests`). Existing `MigrationPlannerSafetyTests` will FAIL TO COMPILE because they call the old `Plan` signature — fix them in the next step.

- [ ] **Step 5: Update existing planner callers to pass the method**

The signature change breaks three call sites. Update each:

In `Editor/Tests/MigrationPlannerSafetyTests.cs` — every `MigrationPlanner.Plan(catalog, report, selection, out warnings)` becomes `MigrationPlanner.Plan(catalog, report, selection, InstallMethod.Upm, out warnings)` (add `using PSV.Installer.Common;`).

In `Editor/Wizard/WizardActions.cs:44` — change:
```csharp
            var plan = MigrationPlanner.Plan(load.Catalog, report, new SingleSelection(componentId), out var warnings);
```
to:
```csharp
            var plan = MigrationPlanner.Plan(load.Catalog, report, new SingleSelection(componentId),
                InstallMethodState.Get(), out var warnings);
```
Add `using PSV.Installer.Common;`.

In `Editor/Wizard/AutoInstaller.cs` — both `MigrationPlanner.Plan(...)` calls (in `StartAll` and `InstallOne`) get `InstallMethodState.Get()` inserted before `out`. Add `using PSV.Installer.Common;`.

- [ ] **Step 6: Run all tests**

Run in Unity Test Runner (EditMode) → Run All. Expected: PASS (all suites compile and pass).

- [ ] **Step 7: Commit**

```bash
git add Editor/Migrator/MigrationPlanner.cs Editor/Wizard/WizardActions.cs Editor/Wizard/AutoInstaller.cs Editor/Tests/MigrationPlannerGitTests.cs Editor/Tests/MigrationPlannerSafetyTests.cs Editor/Tests/ScanReportFactory.cs
git commit -m "feat(installer): MigrationPlanner git mode (chain of AddGitPackage, UPM fallback)"
```

---

## Task 6: Detect git-installed components

**Files:**
- Modify: `Editor/Scanner/StateClassifier.cs`
- Modify: `Editor/Wizard/ComponentStatusProvider.cs`
- Test: `Editor/Tests/GitDetectionTests.cs` (create)

A git-URL value in `manifest.dependencies` must classify as "installed", NOT crash the version-compare (Package) and NOT be reported as `ScopeMissing` (External — git installs have no scoped registry).

- [ ] **Step 1: Write the failing test**

Create `Editor/Tests/GitDetectionTests.cs`:

```csharp
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using PSV.Installer.Catalog;
using PSV.Installer.Scanner;

namespace PSV.Installer.Tests
{
    public class GitDetectionTests
    {
        [Test]
        public void IsGitSpec_recognises_git_urls()
        {
            Assert.IsTrue(StateClassifier.IsGitSpec("https://github.com/x/y.git#1.0.0"));
            Assert.IsTrue(StateClassifier.IsGitSpec("git@github.com:x/y.git"));
            Assert.IsFalse(StateClassifier.IsGitSpec("1.15.14"));
            Assert.IsFalse(StateClassifier.IsGitSpec(""));
        }

        [Test]
        public void External_with_git_url_is_UpmCurrent_not_ScopeMissing()
        {
            var manifest = ManifestData.FromJObject(JObject.Parse(
                "{\"dependencies\":{\"com.tenjin.sdk\":\"https://github.com/CAS-Publishing/tenjin-sdk.git#1.15.14\"}}"));
            var rec = new ExternalRecord
            {
                Id = "com.tenjin.sdk", DisplayName = "Tenjin",
                Scopes = new System.Collections.Generic.List<string> { "com.tenjin" } // not registered
            };

            var res = StateClassifier.Classify(rec, manifest);

            Assert.AreEqual(ExternalState.UpmCurrent, res.State); // git install → installed, scope irrelevant
        }

        [Test]
        public void Package_with_git_url_is_UpmCurrent_not_crash()
        {
            var manifest = ManifestData.FromJObject(JObject.Parse(
                "{\"dependencies\":{\"com.x\":\"https://github.com/a/b.git#1.0.0\"}}"));
            var rec = new PackageRecord { Id = "com.x", DisplayName = "X", RecommendedVersion = "2.0.0" };

            var res = StateClassifier.Classify(rec, manifest, System.Array.Empty<string>());

            Assert.AreEqual(PackageState.UpmCurrent, res.State); // no version-compare on a URL
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run in Unity Test Runner (EditMode). Expected: FAIL — `IsGitSpec` does not exist; External returns `ScopeMissing`; Package version-compare throws on the URL.

- [ ] **Step 3: Implement the git-spec guard**

In `Editor/Scanner/StateClassifier.cs`, add the helper (public so the test and presentation can use it):

```csharp
        /// <summary>True when a manifest dependency value is a git URL rather than a semver version.</summary>
        public static bool IsGitSpec(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            return value.Contains("://") || value.Contains(".git") || value.StartsWith("git@");
        }
```

In `Classify(ExternalRecord ...)`, right after the `deps.TryGetValue(record.Id, out var version)` block confirms presence (before the scope check), add:

```csharp
            // A git-URL install has no scoped registry by design — treat presence as installed.
            if (IsGitSpec(version))
                return new ExternalScanResult(record.Id, record.DisplayName, ExternalState.UpmCurrent, version);
```

In `Classify(PackageRecord ...)`, in the `if (hasCanonical)` branch, guard before `ClassifyVersion`:

```csharp
            if (hasCanonical)
            {
                if (IsGitSpec(canonicalVersion))
                    return new PackageScanResult(record.Id, record.DisplayName,
                        PackageState.UpmCurrent, canonicalVersion, null, EmptyPaths);

                var state = ClassifyVersion(canonicalVersion, record.MinVersion, record.RecommendedVersion);
                ...
```

- [ ] **Step 4: Surface "Installed (git)" in the UI**

In `Editor/Wizard/ComponentStatusProvider.cs`, in `FromExternal` and `FromPackage`, after the status is built, append a git marker. Simplest: at the end of each method, before `return s;`, add:

```csharp
            if (PSV.Installer.Scanner.StateClassifier.IsGitSpec(s.Version) && s.StatusText == "Installed")
                s.StatusText = "Installed (git)";
```

(For `FromExternal` the installed case sets `StatusText = "Installed"`; for `FromPackage` the `UpmCurrent` case sets `"Installed"`.)

- [ ] **Step 5: Run test to verify it passes**

Run in Unity Test Runner (EditMode). Expected: PASS (`GitDetectionTests` + all existing).

- [ ] **Step 6: Commit**

```bash
git add Editor/Scanner/StateClassifier.cs Editor/Wizard/ComponentStatusProvider.cs Editor/Tests/GitDetectionTests.cs
git commit -m "feat(installer): detect git-URL dependencies as installed (git)"
```

---

## Task 7: Welcome method selector (UI)

**Files:**
- Modify: `Editor/Wizard/Uxml/Welcome.uxml`
- Modify: `Editor/Wizard/Screens/WelcomeScreen.cs`

No unit test (UXML + EditorPrefs UI) — owner verifies visually.

- [ ] **Step 1: Add the method cards to Welcome.uxml**

In `Editor/Wizard/Uxml/Welcome.uxml`, insert this block inside `cas-body`, immediately AFTER the `cas-sub` description label and BEFORE the CAS-ID section (`<ui:VisualElement style="margin-top: 18px;">`):

```xml
            <ui:VisualElement style="margin-top: 18px;">
                <ui:Label text="Installation method" class="cas-section-title" />

                <ui:VisualElement name="method-upm" class="cas-method">
                    <ui:VisualElement name="radio-upm" class="cas-radio cas-radio--on cas-method__radio">
                        <ui:VisualElement class="cas-radio__dot" />
                    </ui:VisualElement>
                    <ui:VisualElement>
                        <ui:Label text="UPM (Recommended)" class="cas-method__title" />
                        <ui:Label text="Install via scoped registry" class="cas-method__desc" />
                    </ui:VisualElement>
                </ui:VisualElement>

                <ui:VisualElement name="method-git" class="cas-method">
                    <ui:VisualElement name="radio-git" class="cas-radio cas-method__radio">
                        <ui:VisualElement class="cas-radio__dot" />
                    </ui:VisualElement>
                    <ui:VisualElement>
                        <ui:Label text="Git URL" class="cas-method__title" />
                        <ui:Label text="Install via Git repository (latest tagged version)" class="cas-method__desc" />
                    </ui:VisualElement>
                </ui:VisualElement>
            </ui:VisualElement>
```

- [ ] **Step 2: Wire the radios in WelcomeScreen.cs**

In `Editor/Wizard/Screens/WelcomeScreen.cs`, add `using PSV.Installer.Common;`. Add fields:

```csharp
        private readonly VisualElement _methodUpm, _methodGit, _radioUpm, _radioGit;
        private InstallMethod _method;
```

In the constructor, after the CAS field lookups, add:

```csharp
            _methodUpm = Root.Q<VisualElement>("method-upm");
            _methodGit = Root.Q<VisualElement>("method-git");
            _radioUpm  = Root.Q<VisualElement>("radio-upm");
            _radioGit  = Root.Q<VisualElement>("radio-git");
            _method    = InstallMethodState.Get();
```

In `OnEnter`, inside the `if (!_bound)` block, register clicks:

```csharp
                _methodUpm?.RegisterCallback<ClickEvent>(_ => SelectMethod(InstallMethod.Upm));
                _methodGit?.RegisterCallback<ClickEvent>(_ => SelectMethod(InstallMethod.Git));
```

At the end of `OnEnter` (after `ShowPlatform(_platform);`), reflect the current method:

```csharp
            ShowMethod(_method);
```

Add the methods:

```csharp
        private void SelectMethod(InstallMethod method)
        {
            _method = method;
            InstallMethodState.Set(method);
            ShowMethod(method);
        }

        private void ShowMethod(InstallMethod method)
        {
            SetRadio(_radioUpm, method == InstallMethod.Upm);
            SetRadio(_radioGit, method == InstallMethod.Git);
        }

        private static void SetRadio(VisualElement radio, bool on)
        {
            if (radio == null) return;
            if (on) radio.AddToClassList("cas-radio--on");
            else    radio.RemoveFromClassList("cas-radio--on");
        }
```

- [ ] **Step 3: Owner verifies in Unity**

Owner: open `PSV Game Studio → Wizard (Restart Intro)`. Expected: Welcome shows "Installation method" with two cards; UPM selected by default; clicking Git moves the radio dot; the choice persists (reopen → same selection).

- [ ] **Step 4: Commit**

```bash
git add Editor/Wizard/Uxml/Welcome.uxml Editor/Wizard/Screens/WelcomeScreen.cs
git commit -m "feat(installer): restore UPM/Git method selector on Welcome"
```

---

## Task 8: Catalog data — CAS + Tenjin git blocks

**Files:**
- Modify: `com.psvgamestudio.installer.metadata/catalog.json`

- [ ] **Step 1: Add git blocks**

In `dev/Packages/com.psvgamestudio.installer.metadata/catalog.json`, add a `git` block to the CAS and Tenjin external records (Firebase gets none → UPM fallback). For CAS (after its `config` array, inside the CAS object):

```json
      "git": {
        "packages": [
          { "id": "com.cleversolutions.ads.unity", "url": "https://github.com/cleveradssolutions/CAS-Unity.git", "tag": "4.7.0" }
        ]
      }
```

For Tenjin (inside the `com.tenjin.sdk` object):

```json
      "git": {
        "packages": [
          { "id": "com.tenjin.sdk", "url": "https://github.com/CAS-Publishing/tenjin-sdk.git", "tag": "1.15.14" }
        ]
      }
```

Bump `catalogVersion` (e.g. `0.0.2-preview.11`).

- [ ] **Step 2: Owner verifies the git URLs resolve**

Owner (prerequisite first — mirror `com.tenjin.sdk` per the header): in a scratch Unity project, Package Manager → Add package from git URL →
`https://github.com/cleveradssolutions/CAS-Unity.git#4.7.0` and
`https://github.com/CAS-Publishing/tenjin-sdk.git#1.15.14`. Both should resolve and compile.

- [ ] **Step 3: Commit (metadata repo)**

```bash
cd dev/Packages/com.psvgamestudio.installer.metadata
git add catalog.json
git commit -m "feat(metadata): git blocks for CAS (official) + Tenjin (mirror)"
```

---

## Task 9: End-to-end git install (owner, in Unity)

No code — a manual verification gate that ties the pieces together. Owner runs in a scratch project:

- [ ] **Step 1:** Open the wizard → Welcome → choose **Git** → enter a CAS ID → Next → Express ("Make everything for me").
- [ ] **Step 2:** Confirm the dialog lists `Add com.cleversolutions.ads.unity` and `Add com.tenjin.sdk` as git URLs (no "Register scope"), and Firebase as a normal UPM add (fallback) with a warning.
- [ ] **Step 3:** After apply, open `Packages/manifest.json`. Expected: `com.cleversolutions.ads.unity` and `com.tenjin.sdk` values are git URLs with `#tag`; Firebase is a version with a `com.google` scoped registry; CAS/Tenjin did NOT add their scopes.
- [ ] **Step 4:** Components tab shows CAS and Tenjin as "Installed (git)".
- [ ] **Step 5:** Switch Welcome back to UPM, in another scratch project, and confirm the old behaviour (registry + version) is unchanged.

If all pass, the sub-project A feature is complete.

---

## Notes for the implementer

- **Firebase stays UPM in git mode** because it has no `git` block — `TryPlanGit` returns false and the planner falls back to the existing UPM path, adding a `PlannerWarning`. That warning already surfaces in the Apply/Express confirm dialog (`BuildSummary`/`AutoInstaller.BuildSummary` print warnings). No extra UI work needed for the fallback marker beyond that warning line.
- **Cross-method switching** (a component already installed via the other method) is intentionally not handled — detection shows the state, but `AddGitPackage`/`AddPackage` are idempotent on a present id, so nothing is duplicated or clobbered.
- **PSV packages** (analytics/crashlytics/etc.) are not default components and not in this plan; they depend on Firebase (sub-project B).
