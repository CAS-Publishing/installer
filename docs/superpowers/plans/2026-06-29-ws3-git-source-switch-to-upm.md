# WS-3 — git source clarity + Switch to UPM — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax.

**Goal:** A git-installed external (CAS/Tenjin/Firebase) shows as "Installed (git)" with an explicit **Switch to UPM** action (no misleading Fix), and the version cell reads "git" instead of a generic "local" (feedback #4.1 / #4.2).

**Architecture:** `FromExternal` already labels git installs "Installed (git)"; extend that branch to expose a "Switch to UPM" action via a new `ComponentStatus.GitInstalled` flag. `ComponentsScreen` routes that action to a new `WizardActions.SwitchToUpm` (a dedicated git→registry switch, since the normal planner treats a git install as already-current and would do nothing). `FriendlyVersion` distinguishes git from file/embedded.

**Tech Stack:** Unity 2022.3 Editor, C# UPM editor package, UI Toolkit, NUnit, manifest.json migration actions.

**Decision source:** `docs/superpowers/specs/2026-06-29-installer-feedback-round2-decisions.md` (#4.1, #4.2).

## Global Constraints

- Only EXTERNAL records (catalog `external`: CAS/Tenjin/Firebase) get Switch to UPM. `FromPackage` (dev submodules) keeps the label only — no switch.
- Switch to UPM = add the scoped registry + `AddPackage(id, recommendedVersion)`, overwriting the git-URL dependency. Behind a confirm dialog.
- Git rows keep their Remove button (the manifest still has the git dependency to remove).
- No CLI/headless runner: window/manifest behaviour is OWNER-RUN. `FromExternal` mapping + `FriendlyVersion` are unit-tested.
- Conventional Commits, `feat(installer):`. Installer branch `feat/installer-wizard-ui`. (No metadata change.)

---

### Task 1: "Installed (git)" → Switch to UPM action + accurate version label

**Files:**
- Modify: `Editor/Wizard/ComponentStatusProvider.cs` (`ComponentStatus` + `FromExternal` git branch)
- Modify: `Editor/Wizard/Screens/ComponentsScreen.cs` (`FriendlyVersion` → internal, git label)
- Test: `Editor/Tests/GitSourceStatusTests.cs` (+ `.meta` guid `b1c2d3e4f5a60718293a4b5c6d7e8f90`)

**Interfaces:**
- Produces: `ComponentStatus.GitInstalled` (bool); `FromExternal` sets `GitInstalled=true` + `ActionText="Switch to UPM"` + `Actionable=true` for a git external; `ComponentsScreen.FriendlyVersion` becomes `internal` and returns `"git"` for git specs.

- [ ] **Step 1: Write the failing test**

Create `Editor/Tests/GitSourceStatusTests.cs`:

```csharp
using NUnit.Framework;
using PSV.Installer.Wizard;
using PSV.Installer.Scanner;

namespace PSV.Installer.Tests
{
    public class GitSourceStatusTests
    {
        private static readonly (string, string, string, string) Cas =
            ("com.cleversolutions.ads.unity", "CAS SDK", "Ads", "cas");

        [Test]
        public void Git_external_offers_switch_to_upm()
        {
            var e = new ExternalScanResult("com.cleversolutions.ads.unity", "CAS SDK",
                ExternalState.UpmCurrent, "https://github.com/cleveradssolutions/CAS-Unity.git#4.7.4");
            var s = ComponentStatusProvider.FromExternal(Cas, e);
            Assert.IsTrue(s.GitInstalled);
            Assert.AreEqual("Installed (git)", s.StatusText);
            Assert.AreEqual("Switch to UPM", s.ActionText);
            Assert.IsTrue(s.Actionable);
        }

        [Test]
        public void Registry_external_is_up_to_date()
        {
            var e = new ExternalScanResult("com.cleversolutions.ads.unity", "CAS SDK",
                ExternalState.UpmCurrent, "4.7.4");
            var s = ComponentStatusProvider.FromExternal(Cas, e);
            Assert.IsFalse(s.GitInstalled);
            Assert.AreEqual("Installed", s.StatusText);
            Assert.AreEqual("Up to date", s.ActionText);
            Assert.IsFalse(s.Actionable);
        }

        [Test] public void FriendlyVersion_git_is_git()
            => Assert.AreEqual("git", ComponentsScreen.FriendlyVersion("https://x/y.git#1.2.3"));

        [Test] public void FriendlyVersion_file_is_local()
            => Assert.AreEqual("local", ComponentsScreen.FriendlyVersion("file:../x"));

        [Test] public void FriendlyVersion_semver_passthrough()
            => Assert.AreEqual("4.7.4", ComponentsScreen.FriendlyVersion("4.7.4"));
    }
}
```

Create `Editor/Tests/GitSourceStatusTests.cs.meta`:
```
fileFormatVersion: 2
guid: b1c2d3e4f5a60718293a4b5c6d7e8f90
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

- [ ] **Step 2: Run to verify it fails**

Unity Test Runner → EditMode → `GitSourceStatusTests`. Expected: FAIL — `ComponentStatus.GitInstalled` and the internal `FriendlyVersion` don't exist yet.

- [ ] **Step 3: Add the `GitInstalled` flag**

In `Editor/Wizard/ComponentStatusProvider.cs`, in the `ComponentStatus` class, after `public bool OutsideUpm;`, add:

```csharp
        public bool   GitInstalled; // installed via a git-URL dependency → offer Switch to UPM
```

- [ ] **Step 4: Extend the `FromExternal` git branch**

In `Editor/Wizard/ComponentStatusProvider.cs`, in `FromExternal`, replace the trailing git-label block:

```csharp
            if (PSV.Installer.Scanner.StateClassifier.IsGitSpec(s.Version) && s.StatusText == "Installed")
                s.StatusText = "Installed (git)";
            return s;
```

with:

```csharp
            if (PSV.Installer.Scanner.StateClassifier.IsGitSpec(s.Version) && s.StatusText == "Installed")
            {
                // Git-installed: a valid install, but offer an explicit Switch to UPM rather than a
                // misleading "Up to date"/Fix. (Switch is optional — a muted, non-warning action.)
                s.StatusText = "Installed (git)";
                s.ActionText = "Switch to UPM";
                s.ActionVariant = "muted";
                s.Actionable = true;
                s.GitInstalled = true;
            }
            return s;
```

- [ ] **Step 5: Make `FriendlyVersion` internal + distinguish git**

In `Editor/Wizard/Screens/ComponentsScreen.cs`, replace:

```csharp
        // Embedded/git dependencies have a long, non-semver spec (e.g. "file:...") — show
        // a short "local" label instead so the status cell stays readable.
        private static string FriendlyVersion(string v)
        {
            if (string.IsNullOrEmpty(v)) return null;
            if (v.StartsWith("file:") || v.StartsWith("git") || v.Contains("://")) return "local";
            return v;
        }
```

with:

```csharp
        // Non-semver specs get a short source label so the status cell stays readable:
        // a git dependency reads "git" (matches the "Installed (git)" status); file:/embedded → "local".
        internal static string FriendlyVersion(string v)
        {
            if (string.IsNullOrEmpty(v)) return null;
            if (PSV.Installer.Scanner.StateClassifier.IsGitSpec(v)) return "git";
            if (v.StartsWith("file:") || v.Contains("://")) return "local";
            return v;
        }
```

- [ ] **Step 6: Run to verify it passes** — Expected: PASS (5/5).

- [ ] **Step 7: Commit**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer"
git add Editor/Wizard/ComponentStatusProvider.cs Editor/Wizard/Screens/ComponentsScreen.cs Editor/Tests/GitSourceStatusTests.cs Editor/Tests/GitSourceStatusTests.cs.meta
git commit -m "feat(installer): git external shows Switch to UPM + 'git' version label (#4.1/#4.2)"
```

---

### Task 2: `WizardActions.SwitchToUpm` + Components action routing

**Files:**
- Modify: `Editor/Wizard/WizardActions.cs` (add `SwitchToUpm`)
- Modify: `Editor/Wizard/Screens/ComponentsScreen.cs` (route the git action)

**Interfaces:**
- Produces: `public static bool WizardActions.SwitchToUpm(string componentId, string displayName)`.
- Consumes: existing `CatalogLoader`, `ExternalRecord`, `AddScopedRegistry`, `AddPackage`, `MigrationRunner`, and the private `ResolveRegistryUrl` (already in `WizardActions`).

- [ ] **Step 1: Implement `SwitchToUpm`**

In `Editor/Wizard/WizardActions.cs`, add (after `Apply`):

```csharp
        /// <summary>
        /// Switches a git-URL-installed external to the scoped-registry package: adds the registry
        /// scope(s) and AddPackage(id, recommendedVersion), which overwrites the git dependency in
        /// manifest.json. The normal planner treats a git install as already-current, so this is a
        /// dedicated path. Behind a confirm dialog.
        /// </summary>
        public static bool SwitchToUpm(string componentId, string displayName)
        {
            var load = CatalogLoader.Load();
            if (load.Status != CatalogLoadStatus.Ok)
            {
                EditorUtility.DisplayDialog("PSV Installer",
                    "Catalog is unavailable — cannot switch. " +
                    (load.Error ?? "Make sure the metadata package is installed."), "OK");
                return false;
            }

            var catalog = load.Catalog;
            ExternalRecord rec = null;
            if (catalog.External != null)
                foreach (var e in catalog.External)
                    if (e != null && e.Id == componentId) { rec = e; break; }
            if (rec == null)
            {
                EditorUtility.DisplayDialog("PSV Installer",
                    $"{displayName} is not an external record in the catalog — cannot switch.", "OK");
                return false;
            }

            var version = !string.IsNullOrEmpty(rec.RecommendedVersion) ? rec.RecommendedVersion : rec.MinVersion;
            if (string.IsNullOrEmpty(version))
            {
                EditorUtility.DisplayDialog("PSV Installer",
                    $"No version configured for {displayName} in the catalog — cannot switch.", "OK");
                return false;
            }

            var plan = new List<MigrationAction>();
            if (rec.Scopes != null)
            {
                var url = ResolveRegistryUrl(catalog, rec);
                foreach (var scope in rec.Scopes)
                    if (!string.IsNullOrEmpty(scope))
                        plan.Add(new AddScopedRegistry(rec.Registry ?? string.Empty, url, scope));
            }
            plan.Add(new AddPackage(rec.Id, version));

            if (!EditorUtility.DisplayDialog("PSV Installer",
                    $"Switch {displayName} from a git URL to the registry version {version}?\n\n" +
                    "This replaces the git dependency in manifest.json with the scoped-registry package.",
                    "Switch", "Cancel"))
                return false;

            var result = new MigrationRunner().Apply(plan);
            if (result.Success)
                Debug.Log($"[PSV Installer Wizard] Switched {displayName} to UPM ({rec.Id}@{version}). " +
                          "Unity will resolve packages now.");
            else
                EditorUtility.DisplayDialog("PSV Installer",
                    $"Switch failed for {displayName}:\n• " + string.Join("\n• ", result.Failures), "OK");
            return true;
        }
```

(`List<MigrationAction>` needs `System.Collections.Generic` — already imported in `WizardActions.cs`.)

- [ ] **Step 2: Route the git action in `ComponentsScreen`**

In `Editor/Wizard/Screens/ComponentsScreen.cs`, replace:

```csharp
                var changed = c.OutsideUpm
                    ? WizardActions.MigrateExternal(c.Id, c.DisplayName)
                    : WizardActions.Apply(c.Id, c.DisplayName);
```

with:

```csharp
                var changed = c.GitInstalled
                    ? WizardActions.SwitchToUpm(c.Id, c.DisplayName)
                    : c.OutsideUpm
                        ? WizardActions.MigrateExternal(c.Id, c.DisplayName)
                        : WizardActions.Apply(c.Id, c.DisplayName);
```

- [ ] **Step 3: Owner-run verification**

Install CAS via git URL (Welcome → Git method, or a manual git dependency). The CAS row shows "Installed (git)", version "git", an enabled **Switch to UPM** button and a Remove button. Clicking Switch to UPM rewrites the manifest to the registry version + scope; re-scan shows "Installed" / "Up to date".

- [ ] **Step 4: Commit**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer"
git add Editor/Wizard/WizardActions.cs Editor/Wizard/Screens/ComponentsScreen.cs
git commit -m "feat(installer): Switch to UPM action for git-installed externals (#4.1)"
```

---

### Task 3: Version bump

**Files:** installer `package.json`, `CHANGELOG.md`.

- [ ] **Step 1: Bump + changelog**

`package.json`: `0.0.1-preview.30` → `0.0.1-preview.31`. CHANGELOG top entry `## [0.0.1-preview.31] - 2026-06-29`: "Git-installed PSV SDKs now show 'Installed (git)' with a Switch to UPM action and a 'git' version label, instead of a misleading Fix/'local' (#4.1, #4.2)."

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer"
git add package.json CHANGELOG.md
git commit -m "chore(installer): release notes for git Switch to UPM (preview.31)"
```

---

## Self-Review

- **Spec coverage:** "Installed (git)" + Switch to UPM → Task 1 (label/flag/action) + Task 2 (SwitchToUpm + routing); accurate source label (#4.2) → Task 1 (`FriendlyVersion` git→"git"); no misleading Fix → git case uses a muted "Switch to UPM", not "Fix".
- **Placeholder scan:** none — full code/tests; `SwitchToUpm` reuses the established AddScopedRegistry+AddPackage pattern from `MigrateExternal`.
- **Type consistency:** `ComponentStatus.GitInstalled`, `ComponentStatusProvider.FromExternal`, `ComponentsScreen.FriendlyVersion` (now internal), `WizardActions.SwitchToUpm(string,string)` — consistent across tasks.
