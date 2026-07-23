# Installer Wizard — UX refinements Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refine the CAS Hub Installer wizard per owner feedback — merge the first two screens (drop the install-method picker), capture CAS IDs up front and apply them post-install, drop the dead Auto-Init column, and replace the update auto-popup with a first-run-only open + an About-tab update badge.

**Architecture:** UI Toolkit wizard under `Editor/Wizard/` (assembly `PSV.Installer.Wizard.Editor`, which references core `PSV.Installer.Editor`). Screens are `IWizardScreen` instances driven by `WizardRouter`. New per-project key store (EditorPrefs) holds CAS IDs entered before CAS is installed; a new `CasIdApplier` writes them into the CAS settings asset (`managerIds`) once the asset exists. Verification is **visual, in Unity, by the owner** — the agent cannot compile or run the Editor. Pure logic gets EditMode tests (run by the owner in the Test Runner).

**Tech Stack:** Unity 2022.3.62f3, C# editor code, UI Toolkit (UXML/USS), UnityEditor APIs (SerializedObject, EditorPrefs, PlayerSettings, PackageManager.Client), NUnit EditMode tests.

**Spec:** `docs/superpowers/specs/2026-06-01-installer-wizard-ux-refinements-design.md`

---

## File Structure

**New files**
- `Editor/Wizard/InstallerKeyStore.cs` — per-project EditorPrefs store for SDK keys/ids, keyed by `componentId + platform`; plus `BundleId(group)` default from PlayerSettings.
- `Editor/Wizard/CasIdApplier.cs` — writes stored CAS IDs into the CAS settings `managerIds` when the asset exists; idempotent (`ShouldWrite`).
- `Editor/Wizard/Properties/AssemblyInfo.cs` — `InternalsVisibleTo("PSV.Installer.Editor.Tests")` so the pure helpers are testable.
- `Editor/Tests/InstallerKeyStoreTests.cs` — EditMode tests for the key store.
- `Editor/Tests/CasIdApplierTests.cs` — EditMode tests for `ShouldWrite`.

**Modified files**
- `Editor/Wizard/Uxml/Welcome.uxml` — rewrite: drop method picker + Git URL; add two CAS ID fields + Express/Manual cards + manual warning.
- `Editor/Wizard/Screens/WelcomeScreen.cs` — rewrite: fold in Express/Manual + warning, CAS ID prefill/persist, Continue routing.
- `Editor/Wizard/InstallerWizardWindow.cs` — drop `integration` from `ScreenOrder`/registration; first-run-only auto-open gate; About-tab update badge + throttled background check.
- `Editor/Wizard/Screens/ComponentsScreen.cs` — remove the Auto Init column.
- `Editor/Wizard/Screens/ProgressScreen.cs` — call `CasIdApplier.ApplyPending()` when the auto-install run completes.
- `Editor/Wizard/Uss/theme.uss` — add `.cas-tab__badge` dot style.
- `Editor/Tests/PSV.Installer.Editor.Tests.asmdef` — add reference to `PSV.Installer.Wizard.Editor`.

**Deleted files**
- `Editor/Wizard/Uxml/IntegrationMode.uxml` (+ `.meta`)
- `Editor/Wizard/Screens/IntegrationModeScreen.cs` (+ `.meta`)

> **Meta-file note:** Unity generates `.meta` for new files on focus and removes orphans. The agent cannot run Unity, so new files' `.meta` are created when the owner opens the Editor; deletions must `git rm` any existing `.meta`. The owner commits regenerated `.meta` during visual verification.

> **Build/run note:** No CLI build exists. "Verify it fails / passes" for EditMode tests means the owner runs them in `Window → General → Test Runner` (EditMode). UI changes are verified by the owner opening `PSV Game Studio → Wizard`. Each task lists the exact owner-side check.

---

## Task 1: Generic key store + EditMode test plumbing

**Files:**
- Create: `Editor/Wizard/InstallerKeyStore.cs`
- Create: `Editor/Wizard/Properties/AssemblyInfo.cs`
- Modify: `Editor/Tests/PSV.Installer.Editor.Tests.asmdef`
- Test: `Editor/Tests/InstallerKeyStoreTests.cs`

- [ ] **Step 1: Create the key store**

`Editor/Wizard/InstallerKeyStore.cs`:

```csharp
using UnityEditor;
using UnityEngine;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// Per-project store for SDK keys/ids entered in the wizard before the owning package is
    /// installed (so they can be applied to the package's settings afterwards). Keyed by
    /// componentId + platform, so future per-SDK keys (e.g. Gadsme) reuse the same mechanism.
    /// Backed by EditorPrefs (machine-global) — scoped to THIS project via the data path.
    /// </summary>
    internal static class InstallerKeyStore
    {
        private static string Key(string componentId, string platform) =>
            "PSV.Installer.Wizard.Key:" + Application.dataPath + ":" + componentId + "." + platform;

        public static string Get(string componentId, string platform) =>
            EditorPrefs.GetString(Key(componentId, platform), "");

        public static void Set(string componentId, string platform, string value)
        {
            if (string.IsNullOrEmpty(value)) EditorPrefs.DeleteKey(Key(componentId, platform));
            else EditorPrefs.SetString(Key(componentId, platform), value.Trim());
        }

        public static string GetOrDefault(string componentId, string platform, string fallback)
        {
            var v = Get(componentId, platform);
            return string.IsNullOrEmpty(v) ? (fallback ?? "") : v;
        }

        /// <summary>The project's application bundle identifier for a platform (CAS ID default).</summary>
        public static string BundleId(BuildTargetGroup group)
        {
            try { return PlayerSettings.GetApplicationIdentifier(group) ?? ""; }
            catch { return ""; }
        }
    }
}
```

- [ ] **Step 2: Expose wizard internals to the test assembly**

`Editor/Wizard/Properties/AssemblyInfo.cs`:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("PSV.Installer.Editor.Tests")]
```

- [ ] **Step 3: Reference the wizard assembly from the tests**

Modify `Editor/Tests/PSV.Installer.Editor.Tests.asmdef` — add `"PSV.Installer.Wizard.Editor"` to `references`:

```json
{
    "name": "PSV.Installer.Editor.Tests",
    "rootNamespace": "PSV.Installer.Tests",
    "references": [
        "PSV.Installer.Editor",
        "PSV.Installer.Wizard.Editor",
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll",
        "Newtonsoft.Json.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 4: Write the failing test**

`Editor/Tests/InstallerKeyStoreTests.cs`:

```csharp
using NUnit.Framework;
using PSV.Installer.Wizard;
using UnityEditor;

namespace PSV.Installer.Tests
{
    public class InstallerKeyStoreTests
    {
        private const string Comp = "test.component";
        private const string Plat = "Android";

        [TearDown]
        public void Cleanup() => InstallerKeyStore.Set(Comp, Plat, null);

        [Test]
        public void Set_then_Get_roundtrips_trimmed()
        {
            InstallerKeyStore.Set(Comp, Plat, "  com.app.id  ");
            Assert.AreEqual("com.app.id", InstallerKeyStore.Get(Comp, Plat));
        }

        [Test]
        public void Set_empty_clears_value()
        {
            InstallerKeyStore.Set(Comp, Plat, "x");
            InstallerKeyStore.Set(Comp, Plat, "");
            Assert.AreEqual("", InstallerKeyStore.Get(Comp, Plat));
        }

        [Test]
        public void GetOrDefault_returns_fallback_when_unset()
        {
            Assert.AreEqual("fallback", InstallerKeyStore.GetOrDefault(Comp, Plat, "fallback"));
        }

        [Test]
        public void GetOrDefault_returns_stored_when_set()
        {
            InstallerKeyStore.Set(Comp, Plat, "stored");
            Assert.AreEqual("stored", InstallerKeyStore.GetOrDefault(Comp, Plat, "fallback"));
        }
    }
}
```

- [ ] **Step 5: Owner runs the tests**

Owner: `Window → General → Test Runner → EditMode → Run` the `InstallerKeyStoreTests` class.
Expected: all 4 pass. (Before Step 1–3 compiled, the class would not compile — that is the "fails" state.)

- [ ] **Step 6: Commit**

```bash
git add Editor/Wizard/InstallerKeyStore.cs Editor/Wizard/Properties/AssemblyInfo.cs \
        Editor/Tests/PSV.Installer.Editor.Tests.asmdef Editor/Tests/InstallerKeyStoreTests.cs
git commit -m "feat(installer): per-project key store for pre-install SDK ids + tests"
```

---

## Task 2: CasIdApplier (write CAS IDs into managerIds)

**Files:**
- Create: `Editor/Wizard/CasIdApplier.cs`
- Test: `Editor/Tests/CasIdApplierTests.cs`

Context (verified): catalog `external` record `com.cleversolutions.ads.unity` has `config` entries
`{ platform, kind:"settingsAssetField", assetPath:".../CASSettingsAndroid|iOS.asset", field:"managerIds", placeholder:"demo" }`.
`managerIds` is a serialized **string list** (current value `- demo`). `SetupChecker.LocateAsset(assetPath, assetType)` already resolves the asset. `ConfigRequirement` fields: `Platform`, `Kind`, `Field`, `AssetPath`, `AssetType`, `Placeholder`. `PackageCatalog.External` is `List<ExternalRecord>`; `ExternalRecord.Id`, `.Config`.

- [ ] **Step 1: Write the failing test for the pure decision helper**

`Editor/Tests/CasIdApplierTests.cs`:

```csharp
using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class CasIdApplierTests
    {
        [Test]
        public void ShouldWrite_false_when_stored_empty()
        {
            Assert.IsFalse(CasIdApplier.ShouldWrite("anything", "", "demo"));
            Assert.IsFalse(CasIdApplier.ShouldWrite("anything", null, "demo"));
        }

        [Test]
        public void ShouldWrite_true_when_current_empty()
        {
            Assert.IsTrue(CasIdApplier.ShouldWrite("", "com.app", "demo"));
            Assert.IsTrue(CasIdApplier.ShouldWrite(null, "com.app", "demo"));
        }

        [Test]
        public void ShouldWrite_true_when_current_is_placeholder()
        {
            Assert.IsTrue(CasIdApplier.ShouldWrite("demo", "com.app", "demo"));
            Assert.IsTrue(CasIdApplier.ShouldWrite("DEMO", "com.app", "demo")); // case-insensitive
        }

        [Test]
        public void ShouldWrite_false_when_current_is_real_value()
        {
            Assert.IsFalse(CasIdApplier.ShouldWrite("com.existing", "com.app", "demo"));
        }
    }
}
```

- [ ] **Step 2: Owner runs the test, confirms it fails to compile**

Owner: Test Runner → EditMode. Expected: compile error "CasIdApplier does not exist" (helper not yet written).

- [ ] **Step 3: Create CasIdApplier**

`Editor/Wizard/CasIdApplier.cs`:

```csharp
using PSV.Installer.Catalog;
using UnityEditor;
using UnityEngine;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// Writes CAS IDs captured on the first screen into the CAS settings assets (managerIds) once
    /// the CAS package is installed and its per-platform settings asset exists. Idempotent: only
    /// overwrites an empty/placeholder value, never a real one the user already set. Reads the CAS
    /// config requirements (asset path + field + placeholder) from the catalog, like SetupChecker.
    /// </summary>
    internal static class CasIdApplier
    {
        private const string CasId = "com.cleversolutions.ads.unity";

        /// <summary>True when a stored id should overwrite the current asset value.</summary>
        internal static bool ShouldWrite(string current, string stored, string placeholder)
        {
            if (string.IsNullOrEmpty(stored)) return false;
            if (string.IsNullOrEmpty(current)) return true;
            return !string.IsNullOrEmpty(placeholder) &&
                   string.Equals(current, placeholder, System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Applies any pending CAS IDs to the CAS settings assets. Safe to call often.</summary>
        public static void ApplyPending()
        {
            var load = CatalogLoader.Load();
            if (load.Status != CatalogLoadStatus.Ok || load.Catalog?.External == null) return;

            ExternalRecord cas = null;
            foreach (var e in load.Catalog.External)
                if (e != null && e.Id == CasId) { cas = e; break; }
            if (cas?.Config == null) return;

            var changed = false;
            foreach (var req in cas.Config)
            {
                if (req == null || req.Kind != "settingsAssetField" || string.IsNullOrEmpty(req.Field)) continue;

                var stored = InstallerKeyStore.Get(CasId, req.Platform);
                if (string.IsNullOrEmpty(stored)) continue;

                var asset = SetupChecker.LocateAsset(req.AssetPath, req.AssetType);
                if (asset == null) continue; // CAS not installed / asset not created yet

                var so = new SerializedObject(asset);
                var prop = so.FindProperty(req.Field);
                if (prop == null) continue;

                if (WriteIfNeeded(prop, stored, req.Placeholder))
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(asset);
                    changed = true;
                    Debug.Log($"[PSV Installer] Applied CAS ID for {req.Platform}: {stored}");
                }
            }

            if (changed) AssetDatabase.SaveAssets();
        }

        // managerIds is a string list — write a single-element list when the slot is empty/placeholder.
        private static bool WriteIfNeeded(SerializedProperty prop, string stored, string placeholder)
        {
            if (prop.isArray)
            {
                var current = prop.arraySize > 0 ? FirstString(prop) : null;
                if (!ShouldWrite(current, stored, placeholder)) return false;
                prop.arraySize = 1;
                var first = prop.GetArrayElementAtIndex(0);
                if (first.propertyType != SerializedPropertyType.String) return false;
                first.stringValue = stored;
                return true;
            }
            if (prop.propertyType == SerializedPropertyType.String)
            {
                if (!ShouldWrite(prop.stringValue, stored, placeholder)) return false;
                prop.stringValue = stored;
                return true;
            }
            return false;
        }

        private static string FirstString(SerializedProperty arrayProp)
        {
            var first = arrayProp.GetArrayElementAtIndex(0);
            return first.propertyType == SerializedPropertyType.String ? first.stringValue : null;
        }
    }
}
```

- [ ] **Step 4: Owner runs the test, confirms pass**

Owner: Test Runner → EditMode → run `CasIdApplierTests`. Expected: all 4 pass.

- [ ] **Step 5: Commit**

```bash
git add Editor/Wizard/CasIdApplier.cs Editor/Tests/CasIdApplierTests.cs
git commit -m "feat(installer): CasIdApplier writes captured CAS IDs into managerIds post-install"
```

---

## Task 3: Merge first screen — new Welcome, delete Integration

**Files:**
- Modify: `Editor/Wizard/Uxml/Welcome.uxml` (rewrite)
- Modify: `Editor/Wizard/Screens/WelcomeScreen.cs` (rewrite)
- Modify: `Editor/Wizard/InstallerWizardWindow.cs` (drop `integration`)
- Delete: `Editor/Wizard/Uxml/IntegrationMode.uxml` (+ `.meta`)
- Delete: `Editor/Wizard/Screens/IntegrationModeScreen.cs` (+ `.meta`)

All USS classes used below already exist in `theme.uss` (`cas-lockup*`, `cas-h1`, `cas-sub`, `cas-sub--center`, `cas-label`, `cas-input`, `cas-section-title`, `cas-card`, `cas-card--green`, `cas-card--blue`, `cas-card__iconbox`, `cas-card__iconbox--green`, `cas-card__body`, `cas-card__title`, `cas-card__desc`, `cas-recommended`, `cas-icon--robot`, `cas-icon--gear-big`, `cas-icon--warning`, `cas-warning`, `cas-warning__text`, `cas-footer`, `cas-muted`, `cas-btn`, `cas-btn--primary`).

- [ ] **Step 1: Rewrite Welcome.uxml**

Replace the entire contents of `Editor/Wizard/Uxml/Welcome.uxml` with:

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements">
    <ui:VisualElement class="cas-screen">
        <ui:VisualElement class="cas-body">

            <ui:VisualElement class="cas-lockup">
                <ui:Label text="CAS" class="cas-lockup__box" />
                <ui:VisualElement class="cas-lockup__divider" />
                <ui:VisualElement>
                    <ui:Label text="CAS" class="cas-lockup__main" />
                    <ui:Label text="CleverAdsSolutions" class="cas-lockup__sub" />
                </ui:VisualElement>
            </ui:VisualElement>

            <ui:Label text="Welcome to CAS Hub Installer" class="cas-h1" />
            <ui:Label text="This tool will install and configure all required components for CleverAdsSolutions Hub." class="cas-sub cas-sub--center" />

            <ui:VisualElement style="margin-top: 18px;">
                <ui:Label text="CAS ID — Android" class="cas-label" style="margin-bottom: 6px;" />
                <ui:TextField name="welcome-casid-android" class="cas-input" />
                <ui:Label text="CAS ID — iOS" class="cas-label" style="margin-top: 10px; margin-bottom: 6px;" />
                <ui:TextField name="welcome-casid-ios" class="cas-input" />
            </ui:VisualElement>

            <ui:VisualElement style="margin-top: 18px;">
                <ui:Label text="Installation mode" class="cas-section-title" />

                <ui:VisualElement name="integ-auto" class="cas-card cas-card--green">
                    <ui:VisualElement class="cas-card__iconbox cas-card__iconbox--green">
                        <ui:VisualElement class="cas-icon cas-icon--robot" style="width: 34px; height: 34px;" />
                    </ui:VisualElement>
                    <ui:VisualElement class="cas-card__body">
                        <ui:Label text="Express install" class="cas-card__title" />
                        <ui:Label text="Install and configure all required components automatically." class="cas-card__desc" />
                        <ui:Label text="Recommended" class="cas-recommended" />
                    </ui:VisualElement>
                </ui:VisualElement>

                <ui:VisualElement name="integ-manual" class="cas-card" style="margin-top: 12px;">
                    <ui:VisualElement class="cas-card__iconbox">
                        <ui:VisualElement class="cas-icon cas-icon--gear-big" style="width: 32px; height: 32px;" />
                    </ui:VisualElement>
                    <ui:VisualElement class="cas-card__body">
                        <ui:Label text="Manual selection" class="cas-card__title" />
                        <ui:Label text="Pick and configure components yourself. (For advanced users)" class="cas-card__desc" />
                    </ui:VisualElement>
                </ui:VisualElement>

                <ui:VisualElement name="integ-warning" class="cas-warning">
                    <ui:VisualElement class="cas-icon cas-icon--warning" style="width: 18px; height: 18px; margin-right: 10px;" />
                    <ui:Label name="integ-warning-text" class="cas-warning__text" />
                </ui:VisualElement>
            </ui:VisualElement>

        </ui:VisualElement>

        <ui:VisualElement class="cas-footer">
            <ui:Label name="welcome-version" text="v0.0.0" class="cas-muted" />
            <ui:Button name="welcome-continue" text="Continue" class="cas-btn cas-btn--primary" style="min-width: 100px;" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 2: Rewrite WelcomeScreen.cs**

Replace the entire contents of `Editor/Wizard/Screens/WelcomeScreen.cs` with:

```csharp
using UnityEditor;
using UnityEngine.UIElements;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// First screen (merged entry). Captures the CAS ID per platform (prefilled from the project's
    /// bundle identifier), then routes to Express (auto-install all defaults) or Manual (Components).
    /// Replaces the old installation-method picker and the separate Integration screen.
    /// </summary>
    internal sealed class WelcomeScreen : IWizardScreen
    {
        private const string CasId = "com.cleversolutions.ads.unity";

        public string Id => "welcome";
        public VisualElement Root { get; }

        private WizardRouter _router;
        private bool _bound;
        private bool _auto = true;

        private readonly VisualElement _autoCard, _manualCard, _warning;
        private readonly TextField _casAndroid, _casIos;

        public WelcomeScreen()
        {
            Root = new VisualElement();
            WizardAssets.CloneInto("Welcome", Root);

            _autoCard   = Root.Q<VisualElement>("integ-auto");
            _manualCard = Root.Q<VisualElement>("integ-manual");
            _warning    = Root.Q<VisualElement>("integ-warning");
            _casAndroid = Root.Q<TextField>("welcome-casid-android");
            _casIos     = Root.Q<TextField>("welcome-casid-ios");

            // Warning applies only to manual mode — hidden while "auto" (the default).
            if (_warning != null) _warning.style.display = DisplayStyle.None;
            var warningText = Root.Q<Label>("integ-warning-text");
            if (warningText != null)
                warningText.text = "<b>Warning!</b> Manual integration may lead to incorrect configuration " +
                                   "and unpredictable behavior. Are you sure you know what you're doing?";

            // Prefill each CAS ID from a previously entered value, else the project bundle id.
            if (_casAndroid != null)
                _casAndroid.value = InstallerKeyStore.GetOrDefault(CasId, "Android",
                    InstallerKeyStore.BundleId(BuildTargetGroup.Android));
            if (_casIos != null)
                _casIos.value = InstallerKeyStore.GetOrDefault(CasId, "iOS",
                    InstallerKeyStore.BundleId(BuildTargetGroup.iOS));

            var version = Root.Q<Label>("welcome-version");
            if (version != null) version.text = "v" + WizardAssets.InstallerVersion;
        }

        public void OnEnter(WizardRouter router)
        {
            _router = router;
            if (_bound) return;
            _bound = true;

            _autoCard?.RegisterCallback<ClickEvent>(_ => SetAuto(true));
            _manualCard?.RegisterCallback<ClickEvent>(_ => SetAuto(false));

            var cont = Root.Q<Button>("welcome-continue");
            if (cont != null) cont.clicked += OnContinue;
        }

        private void OnContinue()
        {
            // Persist the entered CAS IDs so CasIdApplier can write them to CAS settings after install.
            InstallerKeyStore.Set(CasId, "Android", _casAndroid?.value);
            InstallerKeyStore.Set(CasId, "iOS", _casIos?.value);

            InstallerWizardWindow.IntroDone = true; // past the first-run intro → later opens land on tabs

            if (_auto) AutoInstaller.StartAll(_router); // install all defaults → Progress → Done
            else _router.GoTo("components");            // manual: pick per component
        }

        private void SetAuto(bool auto)
        {
            _auto = auto;
            if (auto)
            {
                _autoCard?.AddToClassList("cas-card--green");
                _manualCard?.RemoveFromClassList("cas-card--blue");
            }
            else
            {
                _autoCard?.RemoveFromClassList("cas-card--green");
                _manualCard?.AddToClassList("cas-card--blue");
            }
            if (_warning != null)
                _warning.style.display = auto ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
```

- [ ] **Step 3: Drop the integration screen from the window**

In `Editor/Wizard/InstallerWizardWindow.cs`:

Change `ScreenOrder` (line ~24) — remove `"integration"`:

```csharp
        private static readonly string[] ScreenOrder =
            { "welcome", "components", "hub", "progress", "done", "settings", "setup", "about" };
```

In `RegisterScreens` (line ~150), remove the line `router.Register(new IntegrationModeScreen());`:

```csharp
        private void RegisterScreens(WizardRouter router)
        {
            router.Register(new WelcomeScreen());
            router.Register(new ComponentsScreen());
            router.Register(new HubActionsScreen());
            router.Register(new ProgressScreen());
            router.Register(new DoneScreen());
            router.Register(new SettingsRedirectScreen());
            router.Register(new SetupScreen());
            router.Register(new AboutScreen());
        }
```

- [ ] **Step 4: Delete the Integration screen files**

```bash
git rm Editor/Wizard/Uxml/IntegrationMode.uxml Editor/Wizard/Screens/IntegrationModeScreen.cs
# Remove their .meta if tracked (ignore errors if Unity hasn't generated them):
git rm Editor/Wizard/Uxml/IntegrationMode.uxml.meta Editor/Wizard/Screens/IntegrationModeScreen.cs.meta 2>NUL
```

(On the Windows shell, use `2>$null` in PowerShell. If a `.meta` isn't tracked, skip it.)

- [ ] **Step 5: Owner visual verification**

Owner opens `PSV Game Studio → Wizard` in a project where the intro hasn't been completed (or clears the `PSV.Installer.Wizard.IntroDone:*` EditorPref). Expected:
- First screen shows the CAS lockup, two CAS ID fields **prefilled with the project bundle id** (Android/iOS), and two cards (Express green/selected, Manual).
- No installation-method picker, no Git URL field.
- Clicking **Manual** turns it blue and shows the warning; clicking **Express** hides it.
- **Continue** with Express → confirm dialog → Progress; with Manual → Components tab.
- No console errors; no "No screen registered with id 'integration'" warning.

- [ ] **Step 6: Commit**

```bash
git add Editor/Wizard/Uxml/Welcome.uxml Editor/Wizard/Screens/WelcomeScreen.cs Editor/Wizard/InstallerWizardWindow.cs
git commit -m "feat(installer): merge Welcome+Integration into one screen; capture CAS IDs up front

Drop the install-method picker (Git/UPM/.unitypackage) and Git URL field, fold the
Express/Manual choice into the first screen, and add two CAS ID fields prefilled from the
project bundle id. Removes the separate Integration screen."
```

---

## Task 4: Apply captured CAS IDs after install

**Files:**
- Modify: `Editor/Wizard/Screens/ProgressScreen.cs` (auto-install completion)
- Modify: `Editor/Wizard/Screens/ComponentsScreen.cs` (opportunistic apply on rebuild)
- Modify: `Editor/Wizard/InstallerWizardWindow.cs` (opportunistic apply on open)

CAS settings assets are created by CAS after its package resolves, which may be a domain reload
later than the install call. So apply **opportunistically** at the points the user naturally hits
after install — idempotent, only fills empty/placeholder slots.

- [ ] **Step 1: Apply on auto-install completion**

In `Editor/Wizard/Screens/ProgressScreen.cs`, in `Drive(...)`, the "everything resolved" branch (currently lines ~146-152). Add the `CasIdApplier.ApplyPending()` call before routing to Done:

```csharp
            if (next < 0)
            {
                // Everything resolved → write any captured CAS IDs into the settings asset, then done.
                StopPoll();
                AutoInstaller.Clear();
                CasIdApplier.ApplyPending();
                _router.GoTo("done");
                return;
            }
```

- [ ] **Step 2: Apply on Components rebuild**

In `Editor/Wizard/Screens/ComponentsScreen.cs`, at the start of `Rebuild()` (after the null guard), apply pending ids so a manual CAS row-install gets configured on the next re-scan:

```csharp
        private void Rebuild()
        {
            if (_rowsHost == null) return;

            // If CAS was installed (here or via the row button), fill its managerIds from any
            // CAS ID captured on the first screen. Idempotent — only fills empty/placeholder slots.
            CasIdApplier.ApplyPending();

            _rowsHost.Clear();
            ...
```

(Leave the rest of `Rebuild()` unchanged.)

- [ ] **Step 3: Apply on window open**

In `Editor/Wizard/InstallerWizardWindow.cs`, in `CreateGUI()`, right after the existing `Bootstrap.EnsureMetadata();` line, add:

```csharp
            PSV.Installer.Bootstrap.EnsureMetadata();
            CasIdApplier.ApplyPending();
```

- [ ] **Step 4: Owner visual verification (end-to-end)**

In a fresh project: enter a CAS ID on the first screen → Express install. After CAS resolves and the run finishes, open the **Configuration** tab. Expected: CAS Android (and iOS if that asset exists) shows **Configured** with the entered id, not "not set" / "demo". Inspect `Assets/CleverAdsSolutions/Resources/CASSettingsAndroid.asset` → `managerIds` first entry equals the entered id. Re-running does not overwrite a real value.

- [ ] **Step 5: Commit**

```bash
git add Editor/Wizard/Screens/ProgressScreen.cs Editor/Wizard/Screens/ComponentsScreen.cs Editor/Wizard/InstallerWizardWindow.cs
git commit -m "feat(installer): apply captured CAS IDs to managerIds after install (express + manual)"
```

---

## Task 5: Remove the dead Auto-Init column

**Files:**
- Modify: `Editor/Wizard/Screens/ComponentsScreen.cs`

The "Auto Init" column is a disabled, non-functional checkbox. Remove the column and its helper.

- [ ] **Step 1: Drop the Auto Init column from BuildRow**

In `Editor/Wizard/Screens/ComponentsScreen.cs`, in `BuildRow(...)`, delete the Auto Init block (currently lines ~121-126) and its `row.Add(autoCol);` (line ~131). The end of `BuildRow` becomes:

```csharp
            btn.SetEnabled(c.Actionable);
            actionCol.Add(btn);

            row.Add(comp);
            row.Add(statusCol);
            row.Add(actionCol);
            return row;
        }
```

- [ ] **Step 2: Delete the now-unused BuildCheck helper**

In the same file, delete the `BuildCheck(bool on)` method (currently lines ~154-163) — it has no remaining callers.

- [ ] **Step 3: Owner visual verification**

Owner opens the **Components** tab. Expected: rows show Component / Status / Action columns only — no "Auto Init" checkbox column. No console errors. (If the header row / column widths live in `Components.uxml` or `theme.uss` and now look unbalanced, note it for a follow-up; functionally the column is gone.)

- [ ] **Step 4: Commit**

```bash
git add Editor/Wizard/Screens/ComponentsScreen.cs
git commit -m "refactor(installer): remove dead Auto-Init column from Components (deferred until 1C format sync)"
```

---

## Task 6: First-run-only auto-open

**Files:**
- Modify: `Editor/Wizard/InstallerWizardWindow.cs`

Today `ShowIfReportChanged` opens the window whenever the scan-report hash changes (every
package-state change). Owner wants auto-open only on a project's first run; afterwards rely on the
About badge (Task 7).

- [ ] **Step 1: Gate ShowIfReportChanged on IntroDone**

In `Editor/Wizard/InstallerWizardWindow.cs`, update `ShowIfReportChanged` (lines ~83-89):

```csharp
        public static void ShowIfReportChanged(ScanReport report)
        {
            if (report == null) return;
            // Auto-open only on a project's first run. After the user has passed the first screen
            // (IntroDone), never pop the window automatically — surface updates via the About badge.
            if (IntroDone) return;
            if (report.Hash == ScanReportStore.GetLastShownHash()) return;
            ScanReportStore.SetLastShownHash(report.Hash);
            Open();
        }
```

- [ ] **Step 2: Owner visual verification**

Owner test A (first run): in a project where `IntroDone` is false (fresh/empty project, or clear the EditorPref), trigger a domain reload (e.g. recompile). Expected: the wizard auto-opens once.
Owner test B (returning): complete the first screen (sets `IntroDone`), close the window, then add/remove a package or recompile. Expected: the wizard does **not** auto-open.

- [ ] **Step 3: Commit**

```bash
git add Editor/Wizard/InstallerWizardWindow.cs
git commit -m "feat(installer): auto-open wizard only on first run; no popups for returning projects"
```

---

## Task 7: About-tab update badge

**Files:**
- Modify: `Editor/Wizard/Uss/theme.uss` (badge style)
- Modify: `Editor/Wizard/InstallerWizardWindow.cs` (badge element + throttled background check)

When the window opens, check the registry for a newer installer version (once per session) and show
a small dot on the **About** tab if one exists. The in-About banner/Update button already exist.

- [ ] **Step 1: Add the badge style**

Append to `Editor/Wizard/Uss/theme.uss`:

```css
/* ── About-tab update badge ──────────────────────────────────────────────── */
.cas-tab__badge {
    position: absolute;
    top: 6px;
    right: 4px;
    width: 7px;
    height: 7px;
    border-radius: 4px;
    background-color: #E5484D;
    display: none;
}
.cas-tab__badge--on { display: flex; }
```

- [ ] **Step 2: Add the badge element + background check to the window**

In `Editor/Wizard/InstallerWizardWindow.cs`:

Add fields near the other tab fields (after `private Button _tabAbout;`):

```csharp
        private VisualElement _aboutBadge;

        private const string InstallerPackage = "com.psvgamestudio.installer";
        private const string UpdateBadgeProbedKey = "PSV.Installer.Wizard.UpdateBadgeProbed";
        private const string UpdateAvailableKey   = "PSV.Installer.Wizard.UpdateAvailable";
```

At the end of `BuildTabBar(...)`, attach a badge to the About tab:

```csharp
            if (_tabAbout != null)
            {
                _aboutBadge = new VisualElement();
                _aboutBadge.AddToClassList("cas-tab__badge");
                _tabAbout.Add(_aboutBadge);
            }
```

In `CreateGUI()`, after `BuildTabBar(root);`, kick off the update check / reflect the cached result:

```csharp
            BuildTabBar(root);
            RefreshUpdateBadge();
```

Add these methods to the class:

```csharp
        // Shows a dot on the About tab when a newer installer version is published. Probes the
        // registry at most once per editor session (SessionState), caches the result so reopening
        // the window within the session reflects it without re-checking.
        private void RefreshUpdateBadge()
        {
            SetBadge(SessionState.GetBool(UpdateAvailableKey, false));

            if (SessionState.GetBool(UpdateBadgeProbedKey, false)) return;
            SessionState.SetBool(UpdateBadgeProbedKey, true);

            PSV.Installer.Catalog.CatalogUpdater.CheckLatestVersion(InstallerPackage,
                onSuccess: latest =>
                {
                    var available = PSV.Installer.Catalog.CatalogUpdater.IsNewer(latest, WizardAssets.InstallerVersion);
                    SessionState.SetBool(UpdateAvailableKey, available);
                    SetBadge(available);
                },
                onFailure: _ => { /* leave the badge as-is on a failed probe */ });
        }

        private void SetBadge(bool on)
        {
            if (_aboutBadge == null) return;
            if (on) _aboutBadge.AddToClassList("cas-tab__badge--on");
            else    _aboutBadge.RemoveFromClassList("cas-tab__badge--on");
        }
```

(`CatalogUpdater.CheckLatestVersion(string, Action<string>, Action<string>)` and `CatalogUpdater.IsNewer(string, string)` already exist and are used by `AboutScreen`. `WizardAssets.InstallerVersion` is the installed version.)

- [ ] **Step 3: Owner visual verification**

Owner test A (update available): with an installed installer version older than the registry's
highest published version, open the wizard. Expected: a red dot appears on the **About** tab; opening
About shows "Update available …" + the Update button (existing behavior).
Owner test B (up to date): on the latest version, open the wizard. Expected: no dot on About.
Owner confirms the window did **not** auto-pop for the update (Task 6) — the badge is the only signal.

- [ ] **Step 4: Commit**

```bash
git add Editor/Wizard/Uss/theme.uss Editor/Wizard/InstallerWizardWindow.cs
git commit -m "feat(installer): About-tab update badge (background check, once per session)"
```

---

## Task 8: Final pass — spec/plan + changelog

**Files:**
- Modify: `CHANGELOG.md`
- Add: the spec + this plan (currently uncommitted)

- [ ] **Step 1: Add a CHANGELOG entry**

Add a new top section to `CHANGELOG.md` describing this iteration (merged first screen + CAS ID
capture, removed Auto-Init column, first-run-only auto-open + About update badge). Match the
existing changelog heading style (read the file first to match format).

- [ ] **Step 2: Owner full-flow verification**

Owner runs the complete flow in a fresh project: first screen (prefilled CAS IDs, Express/Manual) →
Express → install → Configuration shows the CAS ID applied → no auto-popups afterwards → About badge
behaves. All EditMode tests green (Test Runner → EditMode → Run All).

- [ ] **Step 3: Commit**

```bash
git add CHANGELOG.md docs/superpowers/specs/2026-06-01-installer-wizard-ux-refinements-design.md \
        docs/superpowers/plans/2026-06-01-installer-wizard-ux-refinements.md
git commit -m "docs(installer): UX-refinements spec + plan; changelog entry"
```

> **Publishing note:** This branch is not auto-published. A new preview (`npm publish --tag preview`)
> + manifest bump is needed to test in a separate empty project, per the existing release process —
> out of scope for this plan unless the owner asks.

---

## Self-Review

**Spec coverage:**
- §1 merge first screen → Task 3. ✔
- §2 CAS ID two fields, prefill, apply after install, generic store, optional → Task 1 (store), Task 3 (fields + prefill + persist + non-blocking), Task 2 + Task 4 (apply). ✔
- §3 defer Auto-Init → Task 5. ✔
- §4 first-run-only auto-open + About badge → Task 6 + Task 7. ✔
- Out-of-scope items (metadata self-heal, Gadsme logic, async Apply) → not touched. ✔ (Gadsme: only the generic `componentId.platform` key shape in Task 1 anticipates it.)

**Placeholder scan:** No TBD/TODO; every code step shows full code. UI verification steps are concrete owner actions (the project has no CLI runner — established pattern).

**Type consistency:** `InstallerKeyStore.Get/Set/GetOrDefault/BundleId`, `CasIdApplier.ShouldWrite/ApplyPending`, `SetupChecker.LocateAsset(assetPath, assetType)`, `CatalogUpdater.CheckLatestVersion/IsNewer`, `WizardAssets.InstallerVersion`, `ConfigRequirement.{Platform,Kind,Field,AssetPath,AssetType,Placeholder}`, `ExternalRecord.{Id,Config}`, `PackageCatalog.External` — all match verified code. The CAS id constant `com.cleversolutions.ads.unity` and platform strings `"Android"`/`"iOS"` are used identically in WelcomeScreen (write) and CasIdApplier (read).
