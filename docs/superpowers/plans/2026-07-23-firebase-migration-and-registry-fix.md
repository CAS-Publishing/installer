# Firebase Legacy Migration + Package Registry Fix — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Catalog packages (adapters) always get a scoped registry when installed; broken manifests self-heal via a "Needs registry"/Fix state; `com.psv.firebase.base` migrates to native Firebase + adapters through one compound plan from both wizard entry points.

**Architecture:** Extend `PackageRecord` with optional `scopes`/`requires`/`detectMarkers`; teach `StateClassifier`+`MigrationPlanner` registry awareness for packages; add a pure `FirebaseMigrationPlan` builder invoked by `WizardActions` with a redirect predicate so both Main and Additional component rows route into the same compound migration.

**Tech Stack:** Unity 2022.3.62f3 Editor C#, Newtonsoft.Json, NUnit (Unity Test Framework EditMode tests in `Editor/Tests/`).

**Spec:** `docs/superpowers/specs/2026-07-23-firebase-migration-and-registry-fix-design.md` — read it first.

## Global Constraints

- Repo: `E:\workspace\casai\dev\Packages\com.psvgamestudio.installer` (git submodule; commit HERE, branch `feat/installer-wizard-ui`). Metadata tasks are in `E:\workspace\casai\dev\Packages\com.psvgamestudio.installer.metadata` (separate repo, branch `main`).
- Conventional Commits with scope: `feat(installer):`, `fix(installer):`, `test(installer):`, `chore(metadata):`.
- Code style: block namespaces `PSV.Installer.*`, XML doc comments in English, no file-scoped namespaces, no `var` abuse beyond existing style.
- New `.cs` files under `Editor/` need `.meta` files — create them with a fresh 32-hex GUID (`guid: <32 hex>`, `fileFormatVersion: 2`, `MonoImporter`) mirroring a sibling `.meta`.
- Tests cannot run from CLI — there is no Unity CLI runner in this repo. Each "run tests" step means: compile-safe review now, actual run in Unity Test Runner at the final verification task. Do NOT claim tests pass before that.
- `com.psv.core` is NEVER installed, resolved, or scoped by the installer — presence check only.
- Never touch the generic split-backstop behaviour in `MigrationPlanner.Plan` (lines ~165-206).

---

### Task 1: Catalog schema — `scopes`, `requires`, `detectMarkers` on `PackageRecord`

**Files:**
- Modify: `Editor/Catalog/Catalog.cs` (class `PackageRecord`, after the `RecommendedVersion` field, before `Config`)
- Test: `Editor/Tests/CatalogPackageRecordFieldsTests.cs` (new, with `.meta`)

**Interfaces:**
- Produces: `PackageRecord.Scopes : List<string>` (nullable), `PackageRecord.Requires : List<string>` (nullable), `PackageRecord.DetectMarkers : List<string>` (nullable). All later tasks consume these exact names.

- [ ] **Step 1: Write the failing test**

```csharp
using NUnit.Framework;
using Newtonsoft.Json;
using PSV.Installer.Catalog;

namespace PSV.Installer.Tests
{
    public class CatalogPackageRecordFieldsTests
    {
        [Test]
        public void PackageRecord_Deserializes_Scopes_Requires_DetectMarkers()
        {
            const string json = @"{
                ""id"": ""com.psvgamestudio.remoteconfig"",
                ""registry"": ""psv"",
                ""scopes"": [""com.psvgamestudio""],
                ""requires"": [""com.psv.core""],
                ""detectMarkers"": [""Firebase.RemoteConfig""]
            }";
            var rec = JsonConvert.DeserializeObject<PackageRecord>(json);
            Assert.AreEqual("com.psvgamestudio", rec.Scopes[0]);
            Assert.AreEqual("com.psv.core", rec.Requires[0]);
            Assert.AreEqual("Firebase.RemoteConfig", rec.DetectMarkers[0]);
        }

        [Test]
        public void PackageRecord_FieldsAbsent_AreNull()
        {
            var rec = JsonConvert.DeserializeObject<PackageRecord>(@"{ ""id"": ""x"" }");
            Assert.IsNull(rec.Scopes);
            Assert.IsNull(rec.Requires);
            Assert.IsNull(rec.DetectMarkers);
        }
    }
}
```

- [ ] **Step 2: Add the three fields to `PackageRecord`**

```csharp
        /// <summary>
        /// Optional scoped-registry scopes to register when installing this package. Absent →
        /// the planner defaults to the package id itself (an exact-id scope is always correct).
        /// </summary>
        [JsonProperty("scopes")]             public List<string> Scopes;

        /// <summary>
        /// Optional package ids that must ALREADY be present in the project (manifest dependency
        /// or embedded under Packages/) before this package is offered. Presence check ONLY — the
        /// installer never installs, resolves, or registers a scope for a required id
        /// (e.g. com.psv.core is git-distributed and not on the registry).
        /// </summary>
        [JsonProperty("requires")]           public List<string> Requires;

        /// <summary>
        /// Optional loaded-type markers (matched like <see cref="ExternalModule.AssetMarkers"/>)
        /// gating this package during a compound legacy migration: absent → always installed with
        /// its split group; present → installed only when a marker matches a loaded identifier
        /// (e.g. the remoteconfig adapter only when Firebase.RemoteConfig types are loaded).
        /// </summary>
        [JsonProperty("detectMarkers")]      public List<string> DetectMarkers;
```

- [ ] **Step 3: Review compile-safety (fields referenced only by new code); commit**

```bash
git add Editor/Catalog/Catalog.cs Editor/Tests/CatalogPackageRecordFieldsTests.cs Editor/Tests/CatalogPackageRecordFieldsTests.cs.meta
git commit -m "feat(installer): catalog schema — scopes/requires/detectMarkers on PackageRecord"
```

---

### Task 2: `ManifestData.HasScopeCovering` — Unity-semantics scope match

**Files:**
- Modify: `Editor/Scanner/ManifestProbe.cs` (class `ManifestData`, next to `HasRegisteredScope`)
- Test: `Editor/Tests/ManifestProbeTests.cs` (append)

**Interfaces:**
- Produces: `internal bool ManifestData.HasScopeCovering(string packageId)` — true when any registered scope equals `packageId` OR is a dot-prefix of it (Unity resolves a package against a registry when `id == scope || id.StartsWith(scope + ".")`).

- [ ] **Step 1: Write the failing tests** (append to `ManifestProbeTests.cs`, matching its existing fixture style — it builds `ManifestData` via `ManifestData.FromJObject(JObject.Parse(...))`)

```csharp
        [Test]
        public void HasScopeCovering_ExactId_True()
        {
            var m = FromJson(@"{ ""dependencies"": {}, ""scopedRegistries"": [
                { ""name"": ""psv"", ""url"": ""https://npm.psvgamestudio.com/"", ""scopes"": [""com.psvgamestudio.analytics""] } ] }");
            Assert.IsTrue(m.HasScopeCovering("com.psvgamestudio.analytics"));
        }

        [Test]
        public void HasScopeCovering_PrefixScope_True()
        {
            var m = FromJson(@"{ ""dependencies"": {}, ""scopedRegistries"": [
                { ""name"": ""psv"", ""url"": ""https://npm.psvgamestudio.com/"", ""scopes"": [""com.psvgamestudio""] } ] }");
            Assert.IsTrue(m.HasScopeCovering("com.psvgamestudio.analytics"));
        }

        [Test]
        public void HasScopeCovering_UnrelatedPrefix_False()
        {
            // "com.psv" scope must NOT cover "com.psvgamestudio.analytics" (not a dot-boundary prefix).
            var m = FromJson(@"{ ""dependencies"": {}, ""scopedRegistries"": [
                { ""name"": ""psv"", ""url"": ""https://npm.psvgamestudio.com/"", ""scopes"": [""com.psv""] } ] }");
            Assert.IsFalse(m.HasScopeCovering("com.psvgamestudio.analytics"));
        }

        [Test]
        public void HasScopeCovering_NoRegistries_False()
        {
            var m = FromJson(@"{ ""dependencies"": {} }");
            Assert.IsFalse(m.HasScopeCovering("com.psvgamestudio.analytics"));
        }
```

If `ManifestProbeTests.cs` has no `FromJson` helper, add one: `private static ManifestData FromJson(string json) => ManifestData.FromJObject(Newtonsoft.Json.Linq.JObject.Parse(json));`

- [ ] **Step 2: Implement in `ManifestData`**

```csharp
        /// <summary>
        /// True when any registered scoped-registry scope COVERS <paramref name="packageId"/> under
        /// Unity's matching rule: the id equals the scope, or the scope is a dot-boundary prefix of
        /// the id ("com.psvgamestudio" covers "com.psvgamestudio.analytics"; "com.psv" does not).
        /// </summary>
        public bool HasScopeCovering(string packageId)
        {
            if (string.IsNullOrEmpty(packageId)) return false;
            foreach (var reg in ScopedRegistries)
                foreach (var s in reg.Scopes)
                {
                    if (string.IsNullOrEmpty(s)) continue;
                    if (string.Equals(packageId, s, StringComparison.OrdinalIgnoreCase)) return true;
                    if (packageId.StartsWith(s + ".", StringComparison.OrdinalIgnoreCase)) return true;
                }
            return false;
        }
```

- [ ] **Step 3: Commit**

```bash
git add Editor/Scanner/ManifestProbe.cs Editor/Tests/ManifestProbeTests.cs
git commit -m "feat(installer): ManifestData.HasScopeCovering — Unity scope-match semantics"
```

---

### Task 3: `PackageState.ScopeMissing` classification + "Needs registry" row

**Files:**
- Modify: `Editor/Scanner/ScanReport.cs` (enum `PackageState` — append member at the END)
- Modify: `Editor/Scanner/StateClassifier.cs` (`Classify(PackageRecord, ...)` — canonical branch)
- Modify: `Editor/Wizard/ComponentStatusProvider.cs` (`FromPackage` switch — new case)
- Test: `Editor/Tests/PackageScopeMissingTests.cs` (new, with `.meta`)

**Interfaces:**
- Consumes: `ManifestData.HasScopeCovering(string)` (Task 2), `PackageRecord.Scopes` (Task 1).
- Produces: `PackageState.ScopeMissing` enum member; classifier requires a covering scope for the canonical-semver branch; `ComponentStatus.StatusText == "Needs registry"` with `ActionText "Fix"` (`ComponentsViewMap` already maps "Needs registry" → `RowAction.Fix` — no ViewMap change).

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using PSV.Installer.Catalog;
using PSV.Installer.Scanner;

namespace PSV.Installer.Tests
{
    public class PackageScopeMissingTests
    {
        private static ManifestData Manifest(string json) => ManifestData.FromJObject(JObject.Parse(json));

        private static PackageRecord Adapter() => new PackageRecord
        {
            Id = "com.psvgamestudio.analytics",
            Registry = "psv",
            RecommendedVersion = "0.0.1-preview.3",
        };

        [Test]
        public void CanonicalDep_NoRegistry_ClassifiesScopeMissing()
        {
            // The three broken tester projects: dependency written, no scoped registry at all.
            var m = Manifest(@"{ ""dependencies"": { ""com.psvgamestudio.analytics"": ""0.0.1-preview.3"" } }");
            var r = StateClassifier.Classify(Adapter(), m, null);
            Assert.AreEqual(PackageState.ScopeMissing, r.State);
            Assert.AreEqual("0.0.1-preview.3", r.DetectedVersion);
        }

        [Test]
        public void CanonicalDep_CoveringScope_ClassifiesUpmCurrent()
        {
            var m = Manifest(@"{ ""dependencies"": { ""com.psvgamestudio.analytics"": ""0.0.1-preview.3"" },
                ""scopedRegistries"": [ { ""name"": ""psv"", ""url"": ""https://npm.psvgamestudio.com/"",
                                          ""scopes"": [""com.psvgamestudio""] } ] }");
            var r = StateClassifier.Classify(Adapter(), m, null);
            Assert.AreEqual(PackageState.UpmCurrent, r.State);
        }

        [Test]
        public void GitDep_NoRegistry_StaysUpmCurrent()
        {
            // A git-URL dependency needs no registry — never flag it.
            var m = Manifest(@"{ ""dependencies"": { ""com.psvgamestudio.analytics"": ""https://x.git#1"" } }");
            var r = StateClassifier.Classify(Adapter(), m, null);
            Assert.AreEqual(PackageState.UpmCurrent, r.State);
        }

        [Test]
        public void ExplicitRecordScopes_AreChecked()
        {
            var rec = Adapter();
            rec.Scopes = new List<string> { "com.psvgamestudio" };
            var m = Manifest(@"{ ""dependencies"": { ""com.psvgamestudio.analytics"": ""0.0.1-preview.3"" },
                ""scopedRegistries"": [ { ""name"": ""o"", ""url"": ""https://other/"", ""scopes"": [""com.other""] } ] }");
            Assert.AreEqual(PackageState.ScopeMissing, StateClassifier.Classify(rec, m, null).State);
        }
    }
}
```

- [ ] **Step 2: Append enum member** (END of `PackageState` in `ScanReport.cs`)

```csharp
        /// <summary>
        /// In manifest under the canonical id (semver), but NO registered scoped-registry scope
        /// covers it — Unity cannot resolve it ("Package cannot be found"). Fix = add the scope.
        /// </summary>
        ScopeMissing,
```

- [ ] **Step 3: Classifier change** — in `StateClassifier.Classify(PackageRecord, ...)`, replace the canonical-semver branch body (currently `var state = ClassifyVersion(...)`) with a scope check first:

```csharp
            // ── Canonical UPM states ──────────────────────────────────────────
            if (hasCanonical)
            {
                if (IsGitSpec(canonicalVersion))
                    return new PackageScanResult(record.Id, record.DisplayName,
                        PackageState.UpmCurrent, canonicalVersion, null, EmptyPaths);

                // A semver dependency is only resolvable when a registered scope covers the id.
                // record.Scopes (catalog) is authoritative when present; otherwise the id itself
                // must be covered (exact or prefix scope). Mirrors ExternalState.ScopeMissing.
                bool covered;
                if (record.Scopes != null && record.Scopes.Count > 0)
                {
                    covered = false;
                    foreach (var scope in record.Scopes)
                        if (!string.IsNullOrEmpty(scope) && manifest.HasRegisteredScope(scope))
                        { covered = true; break; }
                }
                else
                {
                    covered = manifest.HasScopeCovering(record.Id);
                }

                if (!covered)
                    return new PackageScanResult(record.Id, record.DisplayName,
                        PackageState.ScopeMissing, canonicalVersion, null, EmptyPaths);

                var state = ClassifyVersion(canonicalVersion, record.MinVersion, record.RecommendedVersion);
                return new PackageScanResult(
                    record.Id, record.DisplayName,
                    state,
                    canonicalVersion,
                    null,
                    EmptyPaths);
            }
```

- [ ] **Step 4: UI mapping** — in `ComponentStatusProvider.FromPackage`, add a case BEFORE `NotInstalled`:

```csharp
                case PackageState.ScopeMissing:
                    s.Tone = "yellow"; s.StatusText = "Needs registry"; s.ActionText = "Fix";      s.ActionVariant = "warn";    s.Actionable = true;  break;
```

- [ ] **Step 5: Commit**

```bash
git add Editor/Scanner/ScanReport.cs Editor/Scanner/StateClassifier.cs Editor/Wizard/ComponentStatusProvider.cs Editor/Tests/PackageScopeMissingTests.cs Editor/Tests/PackageScopeMissingTests.cs.meta
git commit -m "feat(installer): PackageState.ScopeMissing — detect unresolvable catalog packages"
```

---

### Task 4: `PlanForPackage` emits registry actions (+ Fix plan for ScopeMissing)

**Files:**
- Modify: `Editor/Migrator/MigrationPlanner.cs` (`Plan` call site line ~130, `PlanForPackage`, new helper)
- Test: `Editor/Tests/MigrationPlannerRegistryTests.cs` (new, with `.meta`)

**Interfaces:**
- Consumes: `PackageRecord.Scopes` (Task 1), `PackageState.ScopeMissing` (Task 3), existing `AddScopedRegistry(name, url, scope)` action.
- Produces: `PlanForPackage(result, record, catalog, target, backups, removes, regAdds, pkgAdds, removedIds)` — note two NEW parameters `catalog` and `regAdds`; helper `internal static void EmitPackageRegistry(PackageRecord record, PackageCatalog catalog, List<MigrationAction> regAdds)`.

- [ ] **Step 1: Write the failing tests.** Use the existing fixture helpers from `ScanReportFactory.cs` if they fit; otherwise construct `ScanReport` inputs the way `MigrationPlannerSafetyTests.cs` does (read that file first and copy its construction pattern). Core assertions:

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
    public class MigrationPlannerRegistryTests
    {
        private sealed class All : ISelectionSet
        {
            public bool IsSelected(string id) => true;
            public VersionTarget GetTarget(string id) => VersionTarget.Recommended;
        }

        private static PackageCatalog CatalogWith(PackageRecord rec) => new PackageCatalog
        {
            Registries = new Dictionary<string, string> { { "psv", "https://npm.psvgamestudio.com/" } },
            Packages = new List<PackageRecord> { rec },
        };

        // Build a ScanReport containing exactly one PackageScanResult — copy the internal-ctor
        // access pattern used by ScanReportFactory / MigrationPlannerSafetyTests.

        [Test]
        public void NotInstalled_EmitsRegistryBeforeAdd()
        {
            var rec = new PackageRecord { Id = "com.psvgamestudio.analytics", Registry = "psv", RecommendedVersion = "0.0.1-preview.3" };
            var plan = PlanFor(rec, PackageState.NotInstalled);
            var reg = plan.OfType<AddScopedRegistry>().Single();
            Assert.AreEqual("https://npm.psvgamestudio.com/", reg.Url);
            Assert.AreEqual("com.psvgamestudio.analytics", reg.Scope); // default scope = record id
            Assert.Less(plan.ToList().IndexOf(reg), plan.ToList().IndexOf(plan.OfType<AddPackage>().Single()));
        }

        [Test]
        public void ExplicitScopes_AreUsedVerbatim()
        {
            var rec = new PackageRecord { Id = "com.psvgamestudio.analytics", Registry = "psv",
                RecommendedVersion = "0.0.1-preview.3", Scopes = new List<string> { "com.psvgamestudio" } };
            var plan = PlanFor(rec, PackageState.NotInstalled);
            Assert.AreEqual("com.psvgamestudio", plan.OfType<AddScopedRegistry>().Single().Scope);
        }

        [Test]
        public void ScopeMissing_EmitsRegistryOnly_NoAdd()
        {
            var rec = new PackageRecord { Id = "com.psvgamestudio.analytics", Registry = "psv", RecommendedVersion = "0.0.1-preview.3" };
            var plan = PlanFor(rec, PackageState.ScopeMissing);
            Assert.IsTrue(plan.OfType<AddScopedRegistry>().Any());
            Assert.IsFalse(plan.OfType<AddPackage>().Any());
            Assert.IsFalse(plan.OfType<UpdatePackageVersion>().Any());
        }

        [Test]
        public void MissingRegistryKey_NoRegistryAction_NoCrash()
        {
            var rec = new PackageRecord { Id = "com.psvgamestudio.analytics", RecommendedVersion = "0.0.1-preview.3" }; // Registry null
            var plan = PlanFor(rec, PackageState.NotInstalled);
            Assert.IsFalse(plan.OfType<AddScopedRegistry>().Any());
            Assert.IsTrue(plan.OfType<AddPackage>().Any()); // add still emitted; resolve is Unity's problem, warned in catalog authoring
        }

        // PlanFor(rec, state) helper: build catalog + single-result report, call MigrationPlanner.Plan(..., InstallMethod.Upm, out _).
    }
}
```

- [ ] **Step 2: Implement.** In `MigrationPlanner`:

1. Change the call at line ~130 to `PlanForPackage(result, record, catalog, target, backups, removes, regAdds, pkgAdds, removedIds);`
2. Extend `PlanForPackage` signature with `PackageCatalog catalog` (after `record`) and `List<MigrationAction> regAdds` (after `removes`).
3. Add the helper + wire it into every add/update state:

```csharp
        /// <summary>
        /// Ensures the scoped registry for a catalog package before its AddPackage/Update action.
        /// Scope comes from <c>record.Scopes</c>, defaulting to the package id itself (an exact-id
        /// scope is always correct and never over-captures). No registry key configured → no action
        /// (git installs and authoring gaps must not crash planning). ManifestWriter merges by URL,
        /// so re-emitting for an existing registry block is idempotent.
        /// </summary>
        internal static void EmitPackageRegistry(
            PackageRecord record, PackageCatalog catalog, List<MigrationAction> regAdds)
        {
            if (record == null || string.IsNullOrEmpty(record.Registry)) return;

            string url = null;
            if (catalog?.Registries != null && catalog.Registries.TryGetValue(record.Registry, out var mapped))
                url = mapped;
            if (string.IsNullOrEmpty(url)) url = record.Registry.Contains("://") ? record.Registry : null;
            if (string.IsNullOrEmpty(url)) return;

            var scopes = (record.Scopes != null && record.Scopes.Count > 0)
                ? (IEnumerable<string>)record.Scopes
                : new[] { record.Id };
            foreach (var scope in scopes)
                if (!string.IsNullOrEmpty(scope))
                    regAdds.Add(new AddScopedRegistry(record.Registry, url, scope));
        }
```

In `PlanForPackage`'s switch: call `EmitPackageRegistry(record, catalog, regAdds);` immediately before each `pkgAdds.Add(...)` in the `NotInstalled`, `UpmOutdated`/`UpmBelowMin`, `LegacyUpm`, `LegacyAssets`, and `Conflict` cases (guard with the same `targetVersion != null` where applicable so a version-less record emits neither), and add a new case:

```csharp
                case PackageState.ScopeMissing:
                    // Dependency already in manifest but unresolvable — Fix adds only the scope.
                    EmitPackageRegistry(record, catalog, regAdds);
                    break;
```

- [ ] **Step 3: Compile-safety review; commit**

```bash
git add Editor/Migrator/MigrationPlanner.cs Editor/Tests/MigrationPlannerRegistryTests.cs Editor/Tests/MigrationPlannerRegistryTests.cs.meta
git commit -m "fix(installer): PlanForPackage ensures scoped registry — fixes 'Package cannot be found'"
```

---

### Task 5: `requires` gate for Additional components

**Assembly note:** `Editor/Wizard/` is a SEPARATE asmdef (`PSV.Installer.Wizard.Editor`) that references the core `PSV.Installer.Editor` (everything else under `Editor/`). `RequirementGate` is consumed by BOTH the Wizard provider and the core `FirebaseMigrationPlan` (Task 6), so it MUST live in the core assembly — namespace `PSV.Installer.Migrator` — never in `Editor/Wizard/`.

**Files:**
- Create: `Editor/Migrator/RequirementGate.cs` (+ `.meta`) — namespace `PSV.Installer.Migrator`
- Modify: `Editor/Wizard/ComponentStatusProvider.cs` (`BuildAdditionalStatuses` — package loop; add `using PSV.Installer.Migrator;`)
- Test: `Editor/Tests/RequirementGateTests.cs` (new, with `.meta`)

**Interfaces:**
- Consumes: `PackageRecord.Requires` (Task 1).
- Produces: `internal static class PSV.Installer.Migrator.RequirementGate` with `internal static string FirstMissing(IReadOnlyList<string> requires, IReadOnlyDictionary<string, string> dependencies, System.Func<string, bool> embeddedExists)` → null when satisfied, else the first missing id. Task 6's plan builder uses the same method.

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using PSV.Installer.Migrator;

namespace PSV.Installer.Tests
{
    public class RequirementGateTests
    {
        private static readonly Dictionary<string, string> NoDeps = new Dictionary<string, string>();

        [Test] public void NullRequires_IsSatisfied() =>
            Assert.IsNull(RequirementGate.FirstMissing(null, NoDeps, _ => false));

        [Test] public void RequirementInManifest_IsSatisfied() =>
            Assert.IsNull(RequirementGate.FirstMissing(new[] { "com.psv.core" },
                new Dictionary<string, string> { { "com.psv.core", "https://gitlab/x.git" } }, _ => false));

        [Test] public void RequirementEmbedded_IsSatisfied() =>
            Assert.IsNull(RequirementGate.FirstMissing(new[] { "com.psv.core" }, NoDeps,
                id => id == "com.psv.core"));

        [Test] public void RequirementAbsent_ReturnsMissingId() =>
            Assert.AreEqual("com.psv.core",
                RequirementGate.FirstMissing(new[] { "com.psv.core" }, NoDeps, _ => false));
    }
}
```

- [ ] **Step 2: Implement `RequirementGate`**

```csharp
using System;
using System.Collections.Generic;

namespace PSV.Installer.Migrator
{
    /// <summary>
    /// Presence gate for catalog packages that need another package already in the project
    /// (e.g. adapters need com.psv.core — a git-distributed legacy package the installer can
    /// never install itself). Pure: the caller supplies manifest dependencies and an
    /// embedded-folder probe.
    /// </summary>
    internal static class RequirementGate
    {
        /// <summary>Null when every requirement is present (manifest dependency OR embedded
        /// package folder); otherwise the first missing id, for the row hint.</summary>
        internal static string FirstMissing(
            IReadOnlyList<string> requires,
            IReadOnlyDictionary<string, string> dependencies,
            Func<string, bool> embeddedExists)
        {
            if (requires == null || requires.Count == 0) return null;
            foreach (var id in requires)
            {
                if (string.IsNullOrEmpty(id)) continue;
                if (dependencies != null && dependencies.ContainsKey(id)) continue;
                if (embeddedExists != null && embeddedExists(id)) continue;
                return id;
            }
            return null;
        }
    }
}
```

- [ ] **Step 3: Wire into `BuildAdditionalStatuses`.** In the `catalog.Packages` loop, after `var d = ToDescriptor(...)` and status creation, override gated rows. Read the manifest once before the loop:

```csharp
            var manifest = ManifestProbe.Read();
            System.Func<string, bool> embedded = id =>
                System.IO.Directory.Exists(System.IO.Path.Combine(
                    System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "..")),
                    "Packages", id));
```

and replace the `statuses.Add(...)` line for packages with:

```csharp
                    var status = pkgById.TryGetValue(rec.Id, out var p) ? FromPackage(d, p) : NotInCatalog(d);
                    var missing = RequirementGate.FirstMissing(rec.Requires, manifest.Dependencies, embedded);
                    if (missing != null && !status.Installed)
                    {
                        // Not offered without its prerequisite; if it's somehow already installed,
                        // leave the real state visible instead of masking it.
                        status.Tone = "grey"; status.StatusText = "Requires " + missing;
                        status.ActionText = "—"; status.ActionVariant = "muted"; status.Actionable = false;
                    }
                    statuses.Add(status);
```

- [ ] **Step 4: Commit**

```bash
git add Editor/Migrator/RequirementGate.cs Editor/Migrator/RequirementGate.cs.meta Editor/Wizard/ComponentStatusProvider.cs Editor/Tests/RequirementGateTests.cs Editor/Tests/RequirementGateTests.cs.meta
git commit -m "feat(installer): requires gate — adapters hidden without com.psv.core"
```

---

### Task 6: `FirebaseMigrationPlan` — pure compound plan builder

**Files:**
- Create: `Editor/Migrator/FirebaseMigrationPlan.cs` (+ `.meta`)
- Create: `Editor/Migrator/ExternalInstallSet.cs` (+ `.meta`) — the pure `ResolveInstallSet` overload EXTRACTED from `WizardActions` (Wizard asmdef) into the core assembly, because `FirebaseMigrationPlan` (core) cannot reference the Wizard assembly (Wizard → core, never the reverse)
- Modify: `Editor/Wizard/WizardActions.cs` — both `ResolveInstallSet` overloads delegate to `ExternalInstallSet.Resolve` (public reflection overload stays in WizardActions; keep the `internal static` wrappers so `MigrateExternalGroupTests` keeps compiling)
- Test: `Editor/Tests/FirebaseMigrationPlanTests.cs` (new, with `.meta`)

**Interfaces:**
- Consumes: `MigrationGroup` (ScanReport.cs), `PackageCatalog`/`PackageRecord`/`ExternalRecord`/`UninstallRecord` (Catalog.cs), `AssetInstallProbe.IsPresentInIdentifiers`, `RequirementGate.FirstMissing` (Task 5), action types (MigrationAction.cs).
- Produces (extraction): `internal static List<AddPackage> ExternalInstallSet.Resolve(ExternalRecord rec, string baseVersion, ICollection<string> loadedIdentifiers)` in namespace `PSV.Installer.Migrator` — byte-for-byte the body of the current pure `WizardActions.ResolveInstallSet(ExternalRecord, string, ICollection<string>)`.
- Produces:

```csharp
internal sealed class FirebaseMigrationPlanResult
{
    public List<MigrationAction> Actions;   // removes → registries → adds, ready for MigrationRunner
    public List<string> Warnings;           // human-readable, shown in the confirm dialog
}
internal static class FirebaseMigrationPlan
{
    internal static FirebaseMigrationPlanResult Build(
        PackageCatalog catalog,
        MigrationGroup group,                                  // legacy id + replacement package ids
        IReadOnlyDictionary<string, string> dependencies,      // manifest deps snapshot
        ICollection<string> loadedIdentifiers,                 // AssetInstallProbe.CollectLoadedIdentifiers()
        Func<string, bool> embeddedExists);                    // Packages/<id> folder probe
}
```

- [ ] **Step 1: Write the failing tests — the detection matrix.** Shared fixture:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PSV.Installer.Catalog;
using PSV.Installer.Migrator;
using PSV.Installer.Scanner;

namespace PSV.Installer.Tests
{
    public class FirebaseMigrationPlanTests
    {
        private const string Legacy = "com.psv.firebase.base";
        private const string PsvEdm = "com.psv.unity.edm";

        private static PackageCatalog Catalog() => new PackageCatalog
        {
            Registries = new Dictionary<string, string>
            {
                { "psv", "https://npm.psvgamestudio.com/" },
            },
            Packages = new List<PackageRecord>
            {
                new PackageRecord { Id = "com.psvgamestudio.analytics", Registry = "psv",
                    LegacyNpmIds = new List<string> { Legacy },
                    Scopes = new List<string> { "com.psvgamestudio" },
                    Requires = new List<string> { "com.psv.core" },
                    RecommendedVersion = "0.0.1-preview.3" },
                new PackageRecord { Id = "com.psvgamestudio.remoteconfig", Registry = "psv",
                    LegacyNpmIds = new List<string> { Legacy },
                    Scopes = new List<string> { "com.psvgamestudio" },
                    Requires = new List<string> { "com.psv.core" },
                    DetectMarkers = new List<string> { "Firebase.RemoteConfig" },
                    RecommendedVersion = "0.0.1-preview.2" },
            },
            External = new List<ExternalRecord>
            {
                new ExternalRecord { Id = "com.google.firebase.analytics", Registry = "psv",
                    Scopes = new List<string> { "com.google" },
                    LegacyManifestIds = new List<string> { Legacy },
                    RecommendedVersion = "13.1.0-psv.1",
                    Modules = new List<ExternalModule>
                    {
                        new ExternalModule { Id = "com.google.firebase.analytics",     AssetMarkers = new List<string> { "Firebase.Analytics" } },
                        new ExternalModule { Id = "com.google.firebase.remote-config", AssetMarkers = new List<string> { "Firebase.RemoteConfig" } },
                    } },
            },
            Uninstall = new List<UninstallRecord>
            {
                new UninstallRecord { LegacyNpmIds = new List<string> { PsvEdm } },
            },
        };

        private static MigrationGroup Group() => // internal ctor — same-assembly test asmdef, as ScanReportFactory does
            new MigrationGroup(Legacy, new List<string> { "com.psvgamestudio.analytics", "com.psvgamestudio.remoteconfig" });

        private static Dictionary<string, string> Deps(bool core, bool edm) 
        {
            var d = new Dictionary<string, string> { { Legacy, "https://gitlab/fb.git" } };
            if (core) d["com.psv.core"] = "https://gitlab/core.git";
            if (edm)  d[PsvEdm] = "1.0.0";
            return d;
        }

        private static readonly string[] AnalyticsOnly = { "Firebase.Analytics.FirebaseAnalytics" };
        private static readonly string[] AnalyticsAndRc = { "Firebase.Analytics.FirebaseAnalytics", "Firebase.RemoteConfig.FirebaseRemoteConfig" };

        [Test]
        public void FullMatrix_CoreAndRc_RemovesLegacyAndEdm_InstallsAll()
        {
            var r = FirebaseMigrationPlan.Build(Catalog(), Group(), Deps(core: true, edm: true), AnalyticsAndRc, _ => false);
            var removes = r.Actions.OfType<RemovePackage>().Select(a => a.Id).ToList();
            CollectionAssert.AreEquivalent(new[] { Legacy, PsvEdm }, removes);
            var adds = r.Actions.OfType<AddPackage>().Select(a => a.Id).ToList();
            CollectionAssert.Contains(adds, "com.google.firebase.analytics");
            CollectionAssert.Contains(adds, "com.google.firebase.remote-config");
            CollectionAssert.Contains(adds, "com.psvgamestudio.analytics");
            CollectionAssert.Contains(adds, "com.psvgamestudio.remoteconfig");
            // Order: every remove before every registry, every registry before every add.
            int lastRemove = r.Actions.FindLastIndex(a => a is RemovePackage);
            int firstReg   = r.Actions.FindIndex(a => a is AddScopedRegistry);
            int firstAdd   = r.Actions.FindIndex(a => a is AddPackage);
            Assert.Less(lastRemove, firstReg);
            Assert.Less(firstReg, firstAdd);
        }

        [Test]
        public void AnalyticsOnly_SkipsRemoteConfigAdapterAndModule()
        {
            var r = FirebaseMigrationPlan.Build(Catalog(), Group(), Deps(core: true, edm: true), AnalyticsOnly, _ => false);
            var adds = r.Actions.OfType<AddPackage>().Select(a => a.Id).ToList();
            CollectionAssert.Contains(adds, "com.psvgamestudio.analytics");
            CollectionAssert.DoesNotContain(adds, "com.psvgamestudio.remoteconfig");
            CollectionAssert.DoesNotContain(adds, "com.google.firebase.remote-config");
        }

        [Test]
        public void NoCore_NativeOnly_WithWarning()
        {
            var r = FirebaseMigrationPlan.Build(Catalog(), Group(), Deps(core: false, edm: true), AnalyticsAndRc, _ => false);
            var adds = r.Actions.OfType<AddPackage>().Select(a => a.Id).ToList();
            CollectionAssert.DoesNotContain(adds, "com.psvgamestudio.analytics");
            CollectionAssert.DoesNotContain(adds, "com.psvgamestudio.remoteconfig");
            CollectionAssert.Contains(adds, "com.google.firebase.analytics");
            Assert.IsTrue(r.Warnings.Any(w => w.Contains("com.psv.core")));
        }

        [Test]
        public void NoEdmInManifest_NoEdmRemove()
        {
            var r = FirebaseMigrationPlan.Build(Catalog(), Group(), Deps(core: true, edm: false), AnalyticsAndRc, _ => false);
            CollectionAssert.DoesNotContain(r.Actions.OfType<RemovePackage>().Select(a => a.Id).ToList(), PsvEdm);
        }

        [Test]
        public void LegacyAbsent_EmptyPlan()
        {
            var r = FirebaseMigrationPlan.Build(Catalog(), Group(),
                new Dictionary<string, string>(), AnalyticsAndRc, _ => false);
            Assert.IsEmpty(r.Actions);
        }

        [Test]
        public void CoreEmbedded_CountsAsPresent()
        {
            var r = FirebaseMigrationPlan.Build(Catalog(), Group(), Deps(core: false, edm: false), AnalyticsOnly,
                id => id == "com.psv.core");
            CollectionAssert.Contains(r.Actions.OfType<AddPackage>().Select(a => a.Id).ToList(),
                "com.psvgamestudio.analytics");
        }
    }
}
```

- [ ] **Step 2: Implement `FirebaseMigrationPlan.cs`**

```csharp
using System;
using System.Collections.Generic;
using PSV.Installer.Catalog;
using PSV.Installer.Scanner;

namespace PSV.Installer.Migrator
{
    /// <summary>Result of <see cref="FirebaseMigrationPlan.Build"/>: ordered actions + warnings.</summary>
    internal sealed class FirebaseMigrationPlanResult
    {
        public List<MigrationAction> Actions = new List<MigrationAction>();
        public List<string> Warnings = new List<string>();
    }

    /// <summary>
    /// Pure builder for the compound legacy-Firebase migration: remove the legacy wrapper
    /// (com.psv.firebase.base) and any catalog-listed uninstalls present (com.psv.unity.edm),
    /// then install the native Firebase modules detected in the project plus the adapter
    /// packages of the split group (detectMarkers-gated, requires-gated). Built directly —
    /// NOT through MigrationPlanner — so the partial-split backstop cannot drop the removal;
    /// this builder IS the complete migration the backstop protects against losing.
    /// No I/O and no Unity APIs: caller supplies manifest deps, loaded identifiers, and the
    /// embedded-package probe.
    /// </summary>
    internal static class FirebaseMigrationPlan
    {
        internal static FirebaseMigrationPlanResult Build(
            PackageCatalog catalog,
            MigrationGroup group,
            IReadOnlyDictionary<string, string> dependencies,
            ICollection<string> loadedIdentifiers,
            Func<string, bool> embeddedExists)
        {
            var r = new FirebaseMigrationPlanResult();
            if (catalog == null || group == null || dependencies == null) return r;
            if (!dependencies.ContainsKey(group.LegacyId)) return r; // nothing to migrate

            var removes = new List<MigrationAction> { new RemovePackage(group.LegacyId) };
            var regs    = new List<MigrationAction>();
            var adds    = new List<MigrationAction>();
            var seenRegScopes = new HashSet<string>(); // "url|scope" dedup across natives + adapters

            // ── Catalog uninstalls present in manifest (e.g. com.psv.unity.edm) ──
            if (catalog.Uninstall != null)
                foreach (var u in catalog.Uninstall)
                    if (u?.LegacyNpmIds != null)
                        foreach (var lid in u.LegacyNpmIds)
                            if (!string.IsNullOrEmpty(lid) && lid != group.LegacyId && dependencies.ContainsKey(lid))
                                removes.Add(new RemovePackage(lid));

            // ── Native Firebase: the external record that names our legacy id ──
            ExternalRecord native = null;
            if (catalog.External != null)
                foreach (var e in catalog.External)
                    if (e?.LegacyManifestIds != null && e.LegacyManifestIds.Contains(group.LegacyId))
                    { native = e; break; }

            if (native != null)
            {
                var baseVersion = !string.IsNullOrEmpty(native.RecommendedVersion) ? native.RecommendedVersion : native.MinVersion;
                if (!string.IsNullOrEmpty(baseVersion))
                {
                    EmitRegistry(catalog, native.Registry, native.Scopes, native.Id, regs, seenRegScopes);
                    foreach (var add in ExternalInstallSet.Resolve(native, baseVersion, loadedIdentifiers))
                        adds.Add(add);
                }
                else
                    r.Warnings.Add($"No version configured for {native.Id} in the catalog — native modules skipped.");
            }
            else
                r.Warnings.Add($"No external catalog record is linked to {group.LegacyId} — native modules skipped.");

            // ── Adapters: the split group's replacement packages ──
            foreach (var pkgId in group.PackageIds)
            {
                PackageRecord rec = null;
                if (catalog.Packages != null)
                    foreach (var p in catalog.Packages)
                        if (p != null && p.Id == pkgId) { rec = p; break; }
                if (rec == null) continue;

                var missing = RequirementGate.FirstMissing(rec.Requires, dependencies, embeddedExists);
                if (missing != null)
                {
                    r.Warnings.Add($"{rec.Id} skipped — requires {missing}, which is not in the project.");
                    continue;
                }

                if (rec.DetectMarkers != null && rec.DetectMarkers.Count > 0 &&
                    !AssetInstallProbe.IsPresentInIdentifiers(loadedIdentifiers ?? Array.Empty<string>(), rec.DetectMarkers))
                    continue; // feature not used in this project — don't add the adapter

                var version = !string.IsNullOrEmpty(rec.RecommendedVersion) ? rec.RecommendedVersion : rec.MinVersion;
                if (string.IsNullOrEmpty(version))
                {
                    r.Warnings.Add($"{rec.Id} skipped — no version configured in the catalog.");
                    continue;
                }

                EmitRegistry(catalog, rec.Registry, rec.Scopes, rec.Id, regs, seenRegScopes);
                adds.Add(new AddPackage(rec.Id, version));
            }

            r.Actions.AddRange(removes);
            r.Actions.AddRange(regs);
            r.Actions.AddRange(adds);
            return r;
        }

        private static void EmitRegistry(
            PackageCatalog catalog, string registryKey, IReadOnlyList<string> scopes, string fallbackScope,
            List<MigrationAction> regs, HashSet<string> seen)
        {
            if (string.IsNullOrEmpty(registryKey)) return;
            string url = null;
            if (catalog?.Registries != null && catalog.Registries.TryGetValue(registryKey, out var mapped))
                url = mapped;
            if (string.IsNullOrEmpty(url)) url = registryKey.Contains("://") ? registryKey : null;
            if (string.IsNullOrEmpty(url)) return;

            var effective = (scopes != null && scopes.Count > 0) ? scopes : new[] { fallbackScope };
            foreach (var scope in effective)
                if (!string.IsNullOrEmpty(scope) && seen.Add(url + "|" + scope))
                    regs.Add(new AddScopedRegistry(registryKey, url, scope));
        }
    }
}
```

Note: `AssetInstallProbe.IsPresentInIdentifiers` takes `IEnumerable<string>`; `ExternalInstallSet` and `RequirementGate` are internal in the SAME core asmdef as `FirebaseMigrationPlan` — no Wizard reference anywhere in this file.

- [ ] **Step 3: Extract `ExternalInstallSet.Resolve`** — new file `Editor/Migrator/ExternalInstallSet.cs`, namespace `PSV.Installer.Migrator`, containing the pure overload's body moved VERBATIM from `WizardActions.ResolveInstallSet(ExternalRecord, string, ICollection<string>)`; in `WizardActions` both overloads become one-line delegations (`=> ExternalInstallSet.Resolve(rec, baseVersion, loaded);` — the public reflection overload still calls `AssetInstallProbe.CollectLoadedIdentifiers()` first).

- [ ] **Step 4: Commit**

```bash
git add Editor/Migrator/FirebaseMigrationPlan.cs Editor/Migrator/FirebaseMigrationPlan.cs.meta Editor/Migrator/ExternalInstallSet.cs Editor/Migrator/ExternalInstallSet.cs.meta Editor/Wizard/WizardActions.cs Editor/Tests/FirebaseMigrationPlanTests.cs Editor/Tests/FirebaseMigrationPlanTests.cs.meta
git commit -m "feat(installer): FirebaseMigrationPlan — compound legacy migration builder"
```

---

### Task 7: Routing — `WizardActions.Apply` redirects into the compound migration

**Files:**
- Create: `Editor/Migrator/LegacySplitRouting.cs` (+ `.meta`)
- Modify: `Editor/Wizard/WizardActions.cs` (top of `Apply`, new `MigrateFirebaseLegacy`)
- Test: `Editor/Tests/LegacySplitRoutingTests.cs` (new, with `.meta`)

**Interfaces:**
- Consumes: `ScanReport.SplitGroups`, `ScanReport.External` (`ExternalScanResult.DetectedLegacyId`), `FirebaseMigrationPlan.Build` (Task 6), existing `MigrationRunner`, `BuildSummary`, `AssetInstallProbe.CollectLoadedIdentifiers`, `ManifestProbe.Read`.
- Produces: `internal static MigrationGroup LegacySplitRouting.FindGroupFor(ScanReport report, IReadOnlyDictionary<string, string> dependencies, string componentId)` — non-null when `componentId`'s action must run the compound migration; `WizardActions.MigrateFirebaseLegacy(MigrationGroup group, string displayName)`.

- [ ] **Step 1: Write the failing routing tests**

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using PSV.Installer.Migrator;
using PSV.Installer.Scanner;

namespace PSV.Installer.Tests
{
    public class LegacySplitRoutingTests
    {
        private const string Legacy = "com.psv.firebase.base";

        // Build a ScanReport with: SplitGroups = [{Legacy → analytics, remoteconfig}] and an
        // ExternalScanResult for com.google.firebase.analytics with State=InstalledLegacy,
        // DetectedLegacyId=Legacy. Use the internal ctors as ScanReportFactory does.

        [Test]
        public void AdapterId_WithLegacyInManifest_Routes()
        {
            var report = Report();
            var deps = new Dictionary<string, string> { { Legacy, "git" } };
            Assert.IsNotNull(LegacySplitRouting.FindGroupFor(report, deps, "com.psvgamestudio.analytics"));
            Assert.IsNotNull(LegacySplitRouting.FindGroupFor(report, deps, "com.psvgamestudio.remoteconfig"));
        }

        [Test]
        public void ExternalId_WhoseDetectedLegacyIsTheGroupLegacy_Routes()
        {
            var report = Report();
            var deps = new Dictionary<string, string> { { Legacy, "git" } };
            Assert.IsNotNull(LegacySplitRouting.FindGroupFor(report, deps, "com.google.firebase.analytics"));
        }

        [Test]
        public void LegacyNotInManifest_NoRouting()
        {
            var report = Report();
            Assert.IsNull(LegacySplitRouting.FindGroupFor(report, new Dictionary<string, string>(), "com.psvgamestudio.analytics"));
        }

        [Test]
        public void UnrelatedComponent_NoRouting()
        {
            var report = Report();
            var deps = new Dictionary<string, string> { { Legacy, "git" } };
            Assert.IsNull(LegacySplitRouting.FindGroupFor(report, deps, "com.tenjin.sdk"));
        }
    }
}
```

- [ ] **Step 2: Implement `LegacySplitRouting`**

```csharp
using System.Collections.Generic;
using PSV.Installer.Scanner;

namespace PSV.Installer.Migrator
{
    /// <summary>
    /// Decides when a per-component wizard action must run the COMPOUND legacy migration
    /// instead of the generic single-component plan: the manifest still contains a split-group
    /// legacy id (e.g. com.psv.firebase.base) and the clicked component is either one of the
    /// group's replacement packages OR the external whose scan detected that legacy id
    /// (both wizard entry points → one migration). Pure.
    /// </summary>
    internal static class LegacySplitRouting
    {
        internal static MigrationGroup FindGroupFor(
            ScanReport report,
            IReadOnlyDictionary<string, string> dependencies,
            string componentId)
        {
            if (report?.SplitGroups == null || dependencies == null || string.IsNullOrEmpty(componentId))
                return null;

            foreach (var group in report.SplitGroups)
            {
                if (group == null || !dependencies.ContainsKey(group.LegacyId)) continue;

                foreach (var pkgId in group.PackageIds)
                    if (pkgId == componentId) return group;

                if (report.External != null)
                    foreach (var e in report.External)
                        if (e != null && e.Id == componentId && e.DetectedLegacyId == group.LegacyId)
                            return group;
            }
            return null;
        }
    }
}
```

- [ ] **Step 3: Wire into `WizardActions`.** In `Apply`, after the scan (`var report = ProjectScanner.Scan(load.Catalog);`) insert:

```csharp
            // Compound legacy migration takes over BOTH entry points: installing an adapter or the
            // Firebase row while com.psv.firebase.base is still in the manifest must migrate the
            // whole family at once — a single-component plan would either duplicate the SDK or be
            // stripped by the partial-split backstop (the "Fix does nothing" loop).
            var manifest = ManifestProbe.Read();
            var splitGroup = LegacySplitRouting.FindGroupFor(report, manifest.Dependencies, componentId);
            if (splitGroup != null)
                return MigrateFirebaseLegacy(splitGroup, displayName, load.Catalog, manifest);
```

Add the method:

```csharp
        /// <summary>
        /// Runs the compound legacy-Firebase migration (see <see cref="FirebaseMigrationPlan"/>):
        /// one confirm dialog listing removes + registry + installs, then a single
        /// MigrationRunner apply. Returns true when anything was attempted (caller re-scans).
        /// </summary>
        internal static bool MigrateFirebaseLegacy(
            Scanner.MigrationGroup group, string displayName,
            Catalog.PackageCatalog catalog, Scanner.ManifestData manifest)
        {
            System.Func<string, bool> embedded = id =>
                System.IO.Directory.Exists(System.IO.Path.Combine(
                    System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "..")),
                    "Packages", id));

            var built = FirebaseMigrationPlan.Build(
                catalog, group, manifest.Dependencies,
                AssetInstallProbe.CollectLoadedIdentifiers(), embedded);

            if (built.Actions.Count == 0)
            {
                EditorUtility.DisplayDialog("CAS.AI Publishing Hub",
                    $"Nothing to migrate for {displayName} — {group.LegacyId} is not in manifest.json.", "OK");
                return false;
            }

            var warnings = new List<PlannerWarning>();
            foreach (var w in built.Warnings) warnings.Add(new PlannerWarning(group.LegacyId, w));

            if (!EditorUtility.DisplayDialog("CAS.AI Publishing Hub",
                    BuildSummary(displayName, built.Actions, warnings), "Apply", "Cancel"))
                return false;

            var result = new MigrationRunner().Apply(built.Actions);
            if (result.Success)
                Debug.Log($"[CAS Hub] Migrated {group.LegacyId} → native Firebase + adapters " +
                          $"({result.ExecutedCount} action(s)). Unity will resolve packages now.");
            else
                EditorUtility.DisplayDialog("CAS.AI Publishing Hub",
                    $"Migration failed for {displayName}:\n• " + string.Join("\n• ", result.Failures) +
                    "\n\nNo backup is kept — use 'git status' / 'git restore .' to inspect or revert.", "OK");
            return true;
        }
```

(Note `PlannerWarning` ctor: check its actual signature in `Editor/Migrator/PlannerWarning.cs` and adjust the two-arg call if it differs.)

- [ ] **Step 4: Commit**

```bash
git add Editor/Migrator/LegacySplitRouting.cs Editor/Migrator/LegacySplitRouting.cs.meta Editor/Wizard/WizardActions.cs Editor/Tests/LegacySplitRoutingTests.cs Editor/Tests/LegacySplitRoutingTests.cs.meta
git commit -m "feat(installer): route both wizard entry points into compound Firebase migration"
```

---

### Task 8: Metadata catalog changes (repo `com.psvgamestudio.installer.metadata`)

**Files:**
- Modify: `catalog.json`, `package.json`, `CHANGELOG.md` (all in `E:\workspace\casai\dev\Packages\com.psvgamestudio.installer.metadata`)

**Interfaces:**
- Consumes: schema fields from Task 1 (`scopes`, `requires`, `detectMarkers`), `legacyManifestIds` (existing).

- [ ] **Step 1: Adapter records** — in `catalog.json` `packages[]`, extend both entries:

```json
    {
      "id": "com.psvgamestudio.analytics",
      "displayName": "PSV Analytics (Firebase)",
      "registry": "psv",
      "scopes": ["com.psvgamestudio"],
      "requires": ["com.psv.core"],
      "category": "analytics",
      "legacyNpmIds": ["com.psv.firebase.base"],
      "minVersion": "0.0.1-preview.3",
      "recommendedVersion": "0.0.1-preview.3"
    },
    {
      "id": "com.psvgamestudio.remoteconfig",
      "displayName": "PSV Remote Config (Firebase)",
      "registry": "psv",
      "scopes": ["com.psvgamestudio"],
      "requires": ["com.psv.core"],
      "detectMarkers": ["Firebase.RemoteConfig"],
      "category": "core",
      "legacyNpmIds": ["com.psv.firebase.base"],
      "minVersion": "0.0.1-preview.2",
      "recommendedVersion": "0.0.1-preview.2"
    }
```

- [ ] **Step 2: Firebase external record** — add `"legacyManifestIds": ["com.psv.firebase.base"],` to the `com.google.firebase.analytics` external entry (after `"scopes"`). This blocks the naive duplicate Install from Main components AND is the linkage `FirebaseMigrationPlan`/`LegacySplitRouting` use.

- [ ] **Step 3: EDM external record** — add `"legacyManifestIds": ["com.psv.unity.edm"],` to the `com.google.external-dependency-manager` entry (triage item 4: shows "Installed (legacy)" instead of "not used").

- [ ] **Step 4: Version + changelog** — `catalogVersion` and `package.json` `version` → `0.0.2-preview.27`; CHANGELOG entry dated 2026-07-23 noting: adapter scopes/requires/detectMarkers (needs installer ≥ 0.0.1-preview.38 to act on them; older installers ignore), Firebase/EDM legacy linkage. Commit:

```bash
git -C E:\workspace\casai\dev\Packages\com.psvgamestudio.installer.metadata add catalog.json package.json CHANGELOG.md
git -C E:\workspace\casai\dev\Packages\com.psvgamestudio.installer.metadata commit -m "feat(metadata): firebase.base migration linkage, adapter scopes/requires, EDM legacy id"
```

- [ ] **Step 5: CAS rollback — SEPARATE commit** (approved triage item 5): in the `com.cleversolutions.ads.unity` external entry set `"recommendedVersion": "4.6.6"` and the git package `"tag": "4.6.6"` (keep `minVersion` as is). Append to the same CHANGELOG release block.

```bash
git -C E:\workspace\casai\dev\Packages\com.psvgamestudio.installer.metadata commit -am "fix(metadata): pin CAS recommended to stable 4.6.6 (4.7.4 breaks com.psv.adsmanager)"
```

---

### Task 9: Installer version bump + changelog

**Files:**
- Modify: `package.json` (installer), `CHANGELOG.md` (installer)

- [ ] **Step 1:** `package.json` `version` → `0.0.1-preview.38`. CHANGELOG entry (2026-07-23): registry emission for catalog packages + ScopeMissing/Fix self-heal; compound Firebase legacy migration (both entry points); requires gate (`com.psv.core` presence check only). Reference the spec file.
- [ ] **Step 2: Commit**

```bash
git add package.json CHANGELOG.md
git commit -m "chore(installer): bump to 0.0.1-preview.38 — firebase migration + registry fixes"
```

---

### Task 10: Unity verification (owner-assisted) — gate before release

- [ ] **Step 1:** Open `dev/` in Unity 2022.3.62f3, let it compile — zero errors/warnings introduced.
- [ ] **Step 2:** Test Runner → EditMode → run ALL installer tests (`Editor/Tests`). Every new test from Tasks 1-7 must pass; every pre-existing test must still pass (especially `MigrationPlannerSafetyTests`, `MigrateExternalGroupTests` — the split-backstop regression suite).
- [ ] **Step 3:** Manual smoke in the dev project (or a scratch clone): manifest fixture with `com.psv.firebase.base` + `com.psv.unity.edm` + `com.psv.core` git deps → Hub shows Migrate on both entry points → Apply → manifest ends with natives + both adapters + psv registry, legacy + PSV EDM gone. Second fixture: adapter dep without any scoped registry → row shows "Needs registry" → Fix adds the scope.
- [ ] **Step 4:** Report results to the owner. Release (sign via `unity-package-signing` skill → Verdaccio `latest`, installer preview.38 + metadata preview.27) happens ONLY after the owner's explicit go — as per standing release policy.

---

## Self-Review Notes

- Spec coverage: Part 1.1 → Tasks 1, 4; Part 1.2 → Task 3 (+4 ScopeMissing case); Part 1.3 → Tasks 1, 5; Part 2.1 → Task 7 (routing + metadata Step 2 status change); Part 2.2 → Tasks 6, 8; Part 2.3 → Task 6 (direct build); Part 2.5 → Task 7 (Conflict routes through Apply → redirect); Part 3 → Task 8; Testing → per-task tests + Task 10; Rollout → Tasks 9, 10.
- Known judgment call encoded: `MissingRegistryKey_NoRegistryAction_NoCrash` keeps the AddPackage (registry authoring gap ≠ hard block); matches PlanForExternal's tolerance.
- `MigrationGroup` internal ctor and `ScanReport` internal ctor are reachable from tests exactly the way `ScanReportFactory.cs` already does it — implementers must copy that pattern, not change visibility.
