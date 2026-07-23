# Welcome single-platform pass — Implementation Plan (Part 1 of 2)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the Welcome screen configure ONE platform per pass — defaulting to the active Unity build target, switchable — with strict per-platform CAS-ID validation and an in-field placeholder hint.

**Architecture:** Replace the Welcome screen's dual-buffer (`_android`/`_ios`) model with a single active platform that initialises from `EditorUserBuildSettings.activeBuildTarget`. Two new pure helpers — `PlatformDetect` (build target → platform) and `CasIdValidation` (regex/hint resolution + validity) — keep logic testable; the catalog optionally overrides the built-in regex/hint, with code-side defaults as fallback.

**Tech Stack:** Unity 2022.3.62f3 Editor, C# UPM editor package, UI Toolkit (UXML/USS), NUnit (EditMode), JSON catalog.

**Spec:** `docs/superpowers/specs/2026-06-29-welcome-single-platform-and-target-switch-autoopen-design.md` (Part 1). Part 2 (build-target-switch auto-open, merged WS-2) is a separate plan.

## Global Constraints

- One platform configured per pass; only the selected platform's id is persisted/applied on `Next`.
- Default selected platform = active build target: `iOS`→iOS, anything else (incl. Android and all desktop targets)→Android.
- Validation is STRICT and locks `Next`. The CAS test value `demo` is NOT accepted.
- Android pattern (bundle/reverse-DNS): `^[a-zA-Z][a-zA-Z0-9_]*(\.[a-zA-Z][a-zA-Z0-9_]*)+$`, hint `com.company.gamename`.
- iOS pattern (numeric): `^[0-9]+$`, hint `1234567890`.
- Regex/hint are data-driven from the catalog `config` entries, with the code-side defaults above as fallback when the catalog omits them.
- Preserve Task-1 policies: field empty unless a REAL existing CAS managerId prefills it (`ResolveSeed`).
- No CLI/headless test runner: EditMode tests run via Unity Test Runner; window/visual checks are OWNER-RUN. Pure helpers (`PlatformDetect.FromBuildTarget`, `CasIdValidation.IsValid`, `CasIdValidation.Resolve`) are the unit-tested core.
- Conventional Commits, `feat(installer):` / `chore(metadata):` scope.
- Installer repo: `E:/workspace/casai/dev/Packages/com.psvgamestudio.installer` (branch `feat/installer-wizard-ui`). Metadata repo: `E:/workspace/casai/dev/Packages/com.psvgamestudio.installer.metadata` (branch `main`).

---

### Task 1: `PlatformDetect` helper

Maps the active build target to a platform string. Pure core + a live accessor.

**Files:**
- Create: `Editor/Wizard/PlatformDetect.cs`
- Test: `Editor/Tests/PlatformDetectTests.cs`

**Interfaces:**
- Produces: `internal static string PlatformDetect.FromBuildTarget(UnityEditor.BuildTarget target)` → `"iOS"` | `"Android"`; `public static string PlatformDetect.ActivePlatform()` → live active target mapped via `FromBuildTarget`.

- [ ] **Step 1: Write the failing test**

Create `Editor/Tests/PlatformDetectTests.cs`:

```csharp
using NUnit.Framework;
using UnityEditor;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class PlatformDetectTests
    {
        [Test] public void iOS_target_maps_to_iOS()
            => Assert.AreEqual("iOS", PlatformDetect.FromBuildTarget(BuildTarget.iOS));

        [Test] public void Android_target_maps_to_Android()
            => Assert.AreEqual("Android", PlatformDetect.FromBuildTarget(BuildTarget.Android));

        [Test] public void Desktop_target_defaults_to_Android()
            => Assert.AreEqual("Android", PlatformDetect.FromBuildTarget(BuildTarget.StandaloneWindows64));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Unity Test Runner → EditMode → run `PlatformDetectTests`.
Expected: FAIL — `PlatformDetect` does not exist.

- [ ] **Step 3: Implement**

Create `Editor/Wizard/PlatformDetect.cs`:

```csharp
using UnityEditor;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// Maps the project's active build target to the platform string the wizard uses
    /// ("Android" | "iOS"). Any non-iOS target (Android and all desktop/other targets)
    /// maps to "Android" — the single-platform Welcome pass defaults there and stays switchable.
    /// </summary>
    internal static class PlatformDetect
    {
        /// <summary>Pure mapping: iOS → "iOS", everything else → "Android". Testable.</summary>
        internal static string FromBuildTarget(BuildTarget target) =>
            target == BuildTarget.iOS ? "iOS" : "Android";

        /// <summary>The platform for the project's current active build target.</summary>
        public static string ActivePlatform() =>
            FromBuildTarget(EditorUserBuildSettings.activeBuildTarget);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Unity Test Runner → EditMode → run `PlatformDetectTests`. Expected: PASS (3/3).

- [ ] **Step 5: Commit**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer"
git add Editor/Wizard/PlatformDetect.cs Editor/Tests/PlatformDetectTests.cs
git commit -m "feat(installer): add PlatformDetect (active build target → platform)"
```

---

### Task 2: `CasIdValidation` + catalog model fields

Per-platform regex/hint defaults, a pure validity check, a pure resolver (catalog-or-default), a live catalog reader, and the `Regex`/`Hint` fields on the catalog model.

**Files:**
- Create: `Editor/Wizard/CasIdValidation.cs`
- Modify: `Editor/Catalog/Catalog.cs` (add `Regex` + `Hint` to `ConfigRequirement`, after `Placeholder` at line 216)
- Test: `Editor/Tests/CasIdValidationTests.cs`

**Interfaces:**
- Consumes: `PSV.Installer.Catalog.CatalogLoader.Load()` → `{ Status, Catalog }`; `CatalogLoadStatus.Ok`; `Catalog.External` (list of `ExternalRecord` with `.Id`, `.Config`); `ConfigRequirement` with `.Kind`, `.Platform`, and the new `.Regex`, `.Hint`.
- Produces:
  - `internal const string CasIdValidation.AndroidRegex / IosRegex / AndroidHint / IosHint`
  - `internal static bool CasIdValidation.IsValid(string value, string regex)`
  - `internal static (string regex, string hint) CasIdValidation.Resolve(string platform, string catalogRegex, string catalogHint)`
  - `public static (string regex, string hint) CasIdValidation.For(string platform)`

- [ ] **Step 1: Write the failing tests**

Create `Editor/Tests/CasIdValidationTests.cs`:

```csharp
using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class CasIdValidationTests
    {
        [Test] public void Android_bundle_is_valid()
            => Assert.IsTrue(CasIdValidation.IsValid("com.company.game", CasIdValidation.AndroidRegex));

        [Test] public void Android_trims_whitespace()
            => Assert.IsTrue(CasIdValidation.IsValid("  com.company.game  ", CasIdValidation.AndroidRegex));

        [Test] public void Android_single_segment_invalid()
            => Assert.IsFalse(CasIdValidation.IsValid("com", CasIdValidation.AndroidRegex));

        [Test] public void Android_demo_invalid()
            => Assert.IsFalse(CasIdValidation.IsValid("demo", CasIdValidation.AndroidRegex));

        [Test] public void Android_numeric_segment_invalid()
            => Assert.IsFalse(CasIdValidation.IsValid("1.2", CasIdValidation.AndroidRegex));

        [Test] public void iOS_numeric_is_valid()
            => Assert.IsTrue(CasIdValidation.IsValid("1234567890", CasIdValidation.IosRegex));

        [Test] public void iOS_demo_invalid()
            => Assert.IsFalse(CasIdValidation.IsValid("demo", CasIdValidation.IosRegex));

        [Test] public void iOS_alphanumeric_invalid()
            => Assert.IsFalse(CasIdValidation.IsValid("12a", CasIdValidation.IosRegex));

        [Test] public void Empty_invalid()
            => Assert.IsFalse(CasIdValidation.IsValid("", CasIdValidation.AndroidRegex));

        [Test] public void Null_invalid()
            => Assert.IsFalse(CasIdValidation.IsValid(null, CasIdValidation.IosRegex));

        [Test] public void Resolve_uses_catalog_when_present()
        {
            var (regex, hint) = CasIdValidation.Resolve("Android", "^x$", "myhint");
            Assert.AreEqual("^x$", regex);
            Assert.AreEqual("myhint", hint);
        }

        [Test] public void Resolve_falls_back_to_android_defaults()
        {
            var (regex, hint) = CasIdValidation.Resolve("Android", null, "");
            Assert.AreEqual(CasIdValidation.AndroidRegex, regex);
            Assert.AreEqual(CasIdValidation.AndroidHint, hint);
        }

        [Test] public void Resolve_falls_back_to_ios_defaults()
        {
            var (regex, hint) = CasIdValidation.Resolve("iOS", null, null);
            Assert.AreEqual(CasIdValidation.IosRegex, regex);
            Assert.AreEqual(CasIdValidation.IosHint, hint);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Unity Test Runner → EditMode → run `CasIdValidationTests`. Expected: FAIL — `CasIdValidation` does not exist.

- [ ] **Step 3: Add the catalog model fields**

In `Editor/Catalog/Catalog.cs`, inside `ConfigRequirement`, directly after the `Placeholder` field (line 216), add:

```csharp
        /// <summary>Optional per-platform validation regex for the wizard input (e.g. CAS bundle/numeric).</summary>
        [JsonProperty("regex")] public string Regex;

        /// <summary>Optional placeholder hint text shown in the empty wizard input.</summary>
        [JsonProperty("hint")] public string Hint;
```

- [ ] **Step 4: Implement `CasIdValidation`**

Create `Editor/Wizard/CasIdValidation.cs`:

```csharp
using System;
using System.Text.RegularExpressions;
using PSV.Installer.Catalog;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// Per-platform CAS-ID validation for the Welcome screen. Android ids are app bundle ids
    /// (reverse-DNS); iOS ids are numeric App Store ids. The catalog may override the regex/hint
    /// per platform; otherwise these code-side defaults apply. The CAS test value "demo" matches
    /// neither pattern, so it cannot pass (strict validation).
    /// </summary>
    internal static class CasIdValidation
    {
        private const string CasId = "com.cleversolutions.ads.unity";

        internal const string AndroidRegex = @"^[a-zA-Z][a-zA-Z0-9_]*(\.[a-zA-Z][a-zA-Z0-9_]*)+$";
        internal const string IosRegex     = @"^[0-9]+$";
        internal const string AndroidHint  = "com.company.gamename";
        internal const string IosHint      = "1234567890";

        /// <summary>True when the trimmed value is non-empty and matches the pattern. Pure/testable.</summary>
        internal static bool IsValid(string value, string regex)
        {
            if (string.IsNullOrEmpty(value)) return false;
            var v = value.Trim();
            return v.Length > 0 && Regex.IsMatch(v, regex);
        }

        /// <summary>
        /// Effective regex/hint for a platform: a non-empty catalog value wins, else the platform
        /// default. Pure/testable.
        /// </summary>
        internal static (string regex, string hint) Resolve(string platform, string catalogRegex, string catalogHint)
        {
            var iosPlatform = string.Equals(platform, "iOS", StringComparison.OrdinalIgnoreCase);
            var defRegex = iosPlatform ? IosRegex : AndroidRegex;
            var defHint  = iosPlatform ? IosHint  : AndroidHint;
            return (string.IsNullOrEmpty(catalogRegex) ? defRegex : catalogRegex,
                    string.IsNullOrEmpty(catalogHint)  ? defHint  : catalogHint);
        }

        /// <summary>Reads CAS regex/hint from the catalog for a platform, falling back to defaults.</summary>
        public static (string regex, string hint) For(string platform)
        {
            string catRegex = null, catHint = null;
            var load = CatalogLoader.Load();
            if (load.Status == CatalogLoadStatus.Ok && load.Catalog?.External != null)
            {
                foreach (var e in load.Catalog.External)
                {
                    if (e == null || e.Id != CasId || e.Config == null) continue;
                    foreach (var req in e.Config)
                    {
                        if (req == null || req.Kind != "settingsAssetField") continue;
                        if (!string.Equals(req.Platform, platform, StringComparison.OrdinalIgnoreCase)) continue;
                        catRegex = req.Regex; catHint = req.Hint; break;
                    }
                }
            }
            return Resolve(platform, catRegex, catHint);
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Unity Test Runner → EditMode → run `CasIdValidationTests`. Expected: PASS (13/13).

- [ ] **Step 6: Commit**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer"
git add Editor/Wizard/CasIdValidation.cs Editor/Catalog/Catalog.cs Editor/Tests/CasIdValidationTests.cs
git commit -m "feat(installer): add per-platform CAS-ID validation (regex/hint, catalog-overridable)"
```

---

### Task 3: WelcomeScreen single-platform rewrite

Replace the dual-buffer swap with a single active platform defaulting to the active build target, gated by `CasIdValidation`. Persist/apply only the selected platform on `Next`.

**Files:**
- Modify: `Editor/Wizard/Screens/WelcomeScreen.cs` (full rewrite of the body below)
- Modify: `package.json` (version bump)
- Modify: `CHANGELOG.md` (new entry)

**Interfaces:**
- Consumes: `PlatformDetect.ActivePlatform()`; `CasIdValidation.For(platform)`, `CasIdValidation.IsValid(value, regex)`; existing `WelcomeScreen.ResolveSeed`, `CasIdApplier.ReadExisting`, `CasIdApplier.ApplyPending`, `InstallerKeyStore.Get/Set`, `InstallMethodState`.
- Produces: the same `welcome-casid` / `welcome-tab-android` / `welcome-tab-ios` / `welcome-next` element ids (placeholder element added in Task 4).

- [ ] **Step 1: Rewrite `WelcomeScreen.cs`**

Replace the entire body of `Editor/Wizard/Screens/WelcomeScreen.cs` with:

```csharp
using PSV.Installer.Common;
using UnityEngine.UIElements;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// First screen. Configures the CAS ID for ONE platform per pass. The selected platform
    /// defaults to the active build target (iOS→iOS, else Android) and is switchable via the
    /// Android/iOS segments. The field starts empty unless CAS already has a real managerId for
    /// the selected platform (then it's prefilled). Input is validated per platform (Android =
    /// bundle id, iOS = numeric); Next is locked until the value is valid. On Next only the
    /// selected platform's id is persisted and applied, then the Integration screen is shown.
    /// </summary>
    internal sealed class WelcomeScreen : IWizardScreen
    {
        private const string CasId = "com.cleversolutions.ads.unity";

        public string Id => "welcome";
        public VisualElement Root { get; }

        private WizardRouter _router;
        private bool _bound;

        private readonly TextField _field;
        private readonly Button _tabAndroid, _tabIos, _next;
        private readonly VisualElement _methodUpm, _methodGit, _radioUpm, _radioGit;
        private InstallMethod _method;

        private string _platform;   // the single platform this pass configures
        private string _regex;      // active platform's validation pattern

        public WelcomeScreen()
        {
            Root = new VisualElement();
            WizardAssets.CloneInto("Welcome", Root);

            _field      = Root.Q<TextField>("welcome-casid");
            _tabAndroid = Root.Q<Button>("welcome-tab-android");
            _tabIos     = Root.Q<Button>("welcome-tab-ios");
            _next       = Root.Q<Button>("welcome-next");

            _methodUpm = Root.Q<VisualElement>("method-upm");
            _methodGit = Root.Q<VisualElement>("method-git");
            _radioUpm  = Root.Q<VisualElement>("radio-upm");
            _radioGit  = Root.Q<VisualElement>("radio-git");
            _method    = InstallMethodState.Get();

            // Default to the project's active build target; the user may switch.
            _platform = PlatformDetect.ActivePlatform();

            var version = Root.Q<Label>("welcome-version");
            if (version != null) version.text = "v" + WizardAssets.InstallerVersion;
        }

        // The field starts EMPTY on (re)open: a previously-typed-but-unapplied id is NOT restored
        // (feedback #2). Only a REAL existing CAS managerId prefills it (feedback #2.2).
        private static string Seed(string platform) =>
            ResolveSeed(InstallerKeyStore.Get(CasId, platform), CasIdApplier.ReadExisting(platform));

        /// <summary>
        /// Seed policy for the CAS-ID field: the real existing CAS managerId only. The stored value
        /// is intentionally ignored — passed in solely to document (and lock via test) that it is
        /// dropped, not forgotten. Pure/testable.
        /// </summary>
        internal static string ResolveSeed(string storedValue, string existingCasId) =>
            existingCasId ?? "";

        public void OnEnter(WizardRouter router)
        {
            _router = router;
            if (!_bound)
            {
                _bound = true;

                _methodUpm?.RegisterCallback<ClickEvent>(_ => SelectMethod(InstallMethod.Upm));
                _methodGit?.RegisterCallback<ClickEvent>(_ => SelectMethod(InstallMethod.Git));
                _tabAndroid?.RegisterCallback<ClickEvent>(_ => SelectPlatform("Android"));
                _tabIos?.RegisterCallback<ClickEvent>(_ => SelectPlatform("iOS"));
                _field?.RegisterValueChangedCallback(_ => RefreshNext());
                if (_next != null) _next.clicked += OnNext;
            }

            ShowPlatform(_platform);
            ShowMethod(_method);
        }

        private void SelectPlatform(string platform)
        {
            if (_platform == platform) return;
            ShowPlatform(platform);
        }

        private void ShowPlatform(string platform)
        {
            _platform = platform;

            // Resolve this platform's validation pattern + prefill its real existing id (if any).
            var (regex, _) = CasIdValidation.For(platform);
            _regex = regex;
            if (_field != null) _field.SetValueWithoutNotify(Seed(platform));

            SetSegActive(_tabAndroid, platform == "Android");
            SetSegActive(_tabIos, platform == "iOS");
            RefreshNext();
        }

        private static void SetSegActive(Button seg, bool active)
        {
            if (seg == null) return;
            if (active) seg.AddToClassList("cas-seg__item--active");
            else        seg.RemoveFromClassList("cas-seg__item--active");
        }

        private void RefreshNext()
        {
            _next?.SetEnabled(CasIdValidation.IsValid(_field?.value, _regex));
        }

        private void OnNext()
        {
            // One platform per pass: persist ONLY the selected platform's id, then apply now in case
            // CAS is already installed (otherwise it'd land only on the next Components rebuild).
            InstallerKeyStore.Set(CasId, _platform, _field?.value);
            CasIdApplier.ApplyPending();

            _router.GoTo("integration");
        }

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
    }
}
```

- [ ] **Step 2: Update the existing WelcomeScreen tests for the removed `CanProceed`**

`CanProceed` is removed (replaced by `CasIdValidation.IsValid`). In `Editor/Tests/WelcomeScreenTests.cs`, DELETE the four `CanProceed_*` tests (`CanProceed_false_when_both_empty`, `CanProceed_true_when_android_only`, `CanProceed_true_when_ios_only`, `CanProceed_true_when_both`). KEEP the three `ResolveSeed_*` tests. The file's `using` lines stay unchanged.

- [ ] **Step 3: Bump version and changelog**

In `package.json`: `"version": "0.0.1-preview.26",` → `"version": "0.0.1-preview.27",`

In `CHANGELOG.md`, add a new top entry above `## [0.0.1-preview.26]`:

```markdown
## [0.0.1-preview.27] - 2026-06-29

- **Welcome single-platform pass (#2):** the Welcome screen now configures ONE platform per pass,
  defaulting to the active build target (switchable). The CAS-ID field is validated per platform
  (Android = bundle id, iOS = numeric); `Next` is locked until the value is valid, and the CAS test
  value `demo` is rejected. New `PlatformDetect` and `CasIdValidation` helpers; `CanProceed` removed.
  Supersedes the unreleased preview.26 (which it includes) — publish preview.27.
```

- [ ] **Step 4: Run the EditMode tests**

Unity Test Runner → EditMode → run `PSV.Installer.Editor.Tests`. Expected: PASS — `PlatformDetectTests`, `CasIdValidationTests`, and the surviving `ResolveSeed_*` all green; no `CanProceed_*` remain.

- [ ] **Step 5: Commit**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer"
git add Editor/Wizard/Screens/WelcomeScreen.cs Editor/Tests/WelcomeScreenTests.cs package.json CHANGELOG.md
git commit -m "feat(installer): Welcome configures one platform per pass with per-platform validation (#2)"
```

---

### Task 4: In-field placeholder hint

Show a semi-transparent hint inside the empty CAS-ID field, indicating the expected format; hide it once the user types.

**Files:**
- Modify: `Editor/Wizard/Uxml/Welcome.uxml` (wrap the field + add the placeholder label, line 50)
- Modify: `Editor/Wizard/Uss/theme.uss` (add `.cas-input-wrap` + `.cas-input__ph`, after the `.cas-input` block at line 108)
- Modify: `Editor/Wizard/Screens/WelcomeScreen.cs` (bind + toggle the placeholder)

**Interfaces:**
- Consumes: `CasIdValidation.For(platform)` (the `hint` half, previously discarded in Task 3's `ShowPlatform`).
- Produces: element id `welcome-casid-ph` (the placeholder Label).

- [ ] **Step 1: Wrap the field in UXML**

In `Editor/Wizard/Uxml/Welcome.uxml`, replace this single line (line 50):

```xml
                <ui:TextField name="welcome-casid" class="cas-input" />
```

with:

```xml
                <ui:VisualElement class="cas-input-wrap">
                    <ui:TextField name="welcome-casid" class="cas-input" />
                    <ui:Label name="welcome-casid-ph" picking-mode="Ignore" class="cas-input__ph" />
                </ui:VisualElement>
```

- [ ] **Step 2: Add the USS**

In `Editor/Wizard/Uss/theme.uss`, after the `.cas-input .unity-base-text-field__input { ... }` block (ends at line 108), add:

```css
.cas-input-wrap { position: relative; }
.cas-input__ph {
    position: absolute;
    left: 10px;
    top: 8px;
    color: rgba(213, 213, 213, 0.35);
    font-size: 12px;
    -unity-font-style: italic;
}
```

- [ ] **Step 3: Bind and toggle the placeholder in WelcomeScreen**

In `Editor/Wizard/Screens/WelcomeScreen.cs`:

(a) Add a field next to the other readonly fields (after `_next`):

```csharp
        private readonly Label _placeholder;
```

(b) In the constructor, after `_next = Root.Q<Button>("welcome-next");`, add:

```csharp
            _placeholder = Root.Q<Label>("welcome-casid-ph");
```

(c) Change the field value-changed registration in `OnEnter` from:

```csharp
                _field?.RegisterValueChangedCallback(_ => RefreshNext());
```

to:

```csharp
                _field?.RegisterValueChangedCallback(_ => { RefreshNext(); UpdatePlaceholder(); });
```

(d) In `ShowPlatform`, capture the hint and set it. Replace:

```csharp
            var (regex, _) = CasIdValidation.For(platform);
            _regex = regex;
            if (_field != null) _field.SetValueWithoutNotify(Seed(platform));
```

with:

```csharp
            var (regex, hint) = CasIdValidation.For(platform);
            _regex = regex;
            if (_placeholder != null) _placeholder.text = hint;
            if (_field != null) _field.SetValueWithoutNotify(Seed(platform));
            UpdatePlaceholder();
```

(e) Add the toggle method (next to `RefreshNext`):

```csharp
        // The placeholder hint shows only while the field is empty.
        private void UpdatePlaceholder()
        {
            if (_placeholder == null) return;
            _placeholder.style.display =
                string.IsNullOrEmpty(_field?.value) ? DisplayStyle.Flex : DisplayStyle.None;
        }
```

- [ ] **Step 4: Owner-run visual check**

Open `dev/` in Unity, open `PSV → Installer…`. Confirm: the field shows a faint italic hint (`com.company.gamename` on Android / `1234567890` on iOS) when empty; it disappears on typing and reappears when cleared; switching the segment swaps the hint.

- [ ] **Step 5: Commit**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer"
git add Editor/Wizard/Uxml/Welcome.uxml Editor/Wizard/Uss/theme.uss Editor/Wizard/Screens/WelcomeScreen.cs
git commit -m "feat(installer): in-field placeholder hint for the CAS-ID input"
```

---

### Task 5: Populate catalog regex/hint (metadata repo)

Make the validation data-driven by declaring the CAS regex/hint in the catalog. The wizard already falls back to code defaults, so this is an override that keeps the catalog authoritative.

**Files:**
- Modify: `E:/workspace/casai/dev/Packages/com.psvgamestudio.installer.metadata/catalog.json` (the two CAS `config` rows, lines 53-54)
- Modify: `E:/workspace/casai/dev/Packages/com.psvgamestudio.installer.metadata/package.json` (version)
- Modify: `E:/workspace/casai/dev/Packages/com.psvgamestudio.installer.metadata/CHANGELOG.md`

**Interfaces:**
- Produces: CAS `config` rows carrying `regex` + `hint` matching the code defaults. Consumed by `CasIdValidation.For`.

- [ ] **Step 1: Branch the metadata repo**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer.metadata"
git checkout main && git pull --ff-only 2>/dev/null; git checkout -b chore/cas-validation-hints
```

(If branch `chore/cas-pin-4.7.4` from the earlier batch is unmerged, base this on `main`; the two changes touch different lines and merge cleanly.)

- [ ] **Step 2: Add regex/hint to the CAS config rows**

In `catalog.json`, the CAS record's `config` array currently has two rows (Android line 53, iOS line 54). Add `"regex"` and `"hint"` to each. The Android row becomes:

```json
        { "platform": "Android", "kind": "settingsAssetField", "assetPath": "Assets/CleverAdsSolutions/Resources/CASSettingsAndroid.asset", "field": "managerIds", "placeholder": "demo", "label": "CAS ID", "openMenu": "Assets/CleverAdsSolutions/Settings", "regex": "^[a-zA-Z][a-zA-Z0-9_]*(\\.[a-zA-Z][a-zA-Z0-9_]*)+$", "hint": "com.company.gamename" },
```

The iOS row becomes:

```json
        { "platform": "iOS",     "kind": "settingsAssetField", "assetPath": "Assets/CleverAdsSolutions/Resources/CASSettingsiOS.asset",     "field": "managerIds", "placeholder": "demo", "label": "CAS ID", "openMenu": "Assets/CleverAdsSolutions/Settings", "regex": "^[0-9]+$", "hint": "1234567890" }
```

(Note the doubled backslashes `\\.` — JSON escaping of the regex `\.`)

- [ ] **Step 3: Verify the JSON and the regex round-trip**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer.metadata"
python -c "import json;d=json.load(open('catalog.json'));cfg=[e for e in d['external'] if e['id']=='com.cleversolutions.ads.unity'][0]['config'];[print(c['platform'],c['regex'],c['hint']) for c in cfg]"
```

Expected output:
```
Android ^[a-zA-Z][a-zA-Z0-9_]*(\.[a-zA-Z][a-zA-Z0-9_]*)+$ com.company.gamename
iOS ^[0-9]+$ 1234567890
```

- [ ] **Step 4: Bump version and changelog**

In `package.json`: `"version": "0.0.2-preview.19",` → `"version": "0.0.2-preview.20",`
(If the earlier `chore/cas-pin-4.7.4` is still unmerged, this assumes it lands first; otherwise bump from whatever `main` carries.)

In `CHANGELOG.md`, add a top entry:

```markdown
## [0.0.2-preview.20] - 2026-06-29

- **CAS-ID validation hints:** declare per-platform `regex` + `hint` on the CAS `config` rows
  (Android bundle / iOS numeric) so the installer's Welcome validation is catalog-driven.
```

- [ ] **Step 5: Commit**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer.metadata"
git add catalog.json package.json CHANGELOG.md
git commit -m "chore(metadata): declare CAS-ID regex/hint per platform"
```

---

## Self-Review

- **Spec coverage (Part 1):** one-platform-per-pass → Task 3 (`OnNext` writes only `_platform`); default = active build target → Task 1 + Task 3 (`PlatformDetect.ActivePlatform`); switchable → Task 3 (`SelectPlatform`); strict per-platform validation locking Next, `demo` rejected → Task 2 + Task 3 (`IsValid`/`RefreshNext`); placeholder hint → Task 4; data-driven regex/hint with fallback → Task 2 (`Resolve`/`For`) + Task 5 (catalog); #2 empty / #2.2 prefill preserved → Task 3 (`Seed`/`ResolveSeed`). Part 2 (target-switch auto-open) is intentionally a separate plan.
- **Placeholder scan:** none — every step carries complete code/commands.
- **Type consistency:** `PlatformDetect.FromBuildTarget`/`ActivePlatform`, `CasIdValidation.IsValid(value, regex)`/`Resolve(platform, catalogRegex, catalogHint)`/`For(platform)`, `ConfigRequirement.Regex`/`Hint`, element id `welcome-casid-ph` — all used consistently across Tasks 1-5. `CanProceed` is removed in Task 3 and its tests deleted in the same task (no dangling reference).
