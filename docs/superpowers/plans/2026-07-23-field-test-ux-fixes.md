# Field-Test UX Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix triage items 3/7/8/9: CAS menu names with ellipsis, stale component rows after install, metadata-install retry so the Hub auto-opens without a restart, and a top-level "CAS Hub" menu.

**Architecture:** Four independent surgical fixes; only item 8 adds a new (small) class. Spec: `docs/superpowers/specs/2026-07-23-field-test-ux-fixes-design.md`.

**Tech Stack:** Unity 2022.3 Editor C#, NUnit EditMode tests.

## Global Constraints

- Repo: `E:\workspace\casai\dev\Packages\com.psvgamestudio.installer`, branch `feat/installer-wizard-ui` (HEAD `d4ee08b`). Conventional Commits `fix(installer):` / `feat(installer):` / `docs(installer):` + trailer `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>`.
- NO CLI test runner — tests are transcribed for owner-run; never claim they pass.
- New `.cs` files need sibling `.meta` with fresh random 32-hex lowercase GUID.
- `Editor/Wizard/` is a separate asmdef referencing core `PSV.Installer.Editor`; core never references Wizard.
- No version bump — `package.json` stays `0.0.1-preview.38`.

---

### Task 1: CAS menu candidates with ellipsis

**Files:**
- Modify: `Editor/Wizard/CasNativeSettings.cs`
- Test: `Editor/Tests/CasNativeSettingsMenuTests.cs` (new, with `.meta`)

**Interfaces:**
- Produces: `internal static string[] CasNativeSettings.MenuCandidates(string platform)`.

- [ ] **Step 1: Failing test**

```csharp
using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class CasNativeSettingsMenuTests
    {
        [Test]
        public void Android_YieldsPlainThenEllipsis()
        {
            var c = CasNativeSettings.MenuCandidates("Android");
            Assert.AreEqual(new[]
            {
                "Assets/CleverAdsSolutions/Android Settings",
                "Assets/CleverAdsSolutions/Android Settings...",
            }, c);
        }

        [Test]
        public void Ios_YieldsPlainThenEllipsis()
        {
            var c = CasNativeSettings.MenuCandidates("iOS");
            Assert.AreEqual(new[]
            {
                "Assets/CleverAdsSolutions/iOS Settings",
                "Assets/CleverAdsSolutions/iOS Settings...",
            }, c);
        }
    }
}
```

Note: `CasNativeSettings` is in the Wizard asmdef; the Tests asmdef already references `PSV.Installer.Wizard.Editor` and the Wizard `AssemblyInfo` has `InternalsVisibleTo` for tests (verify by reading `Editor/Wizard/Properties/AssemblyInfo.cs`; if the tests IVT is missing there, add it alongside the existing entries).

- [ ] **Step 2: Implement** — in `CasNativeSettings`, add the builder and use it in `Open` (replace the single `menu` string + `ExecuteMenuItem` pair):

```csharp
        /// <summary>Menu-path candidates for a platform: CAS &lt; 4.7 uses "Android Settings",
        /// 4.7+ renamed the items to "Android Settings..." — try both, oldest first.</summary>
        internal static string[] MenuCandidates(string platform)
        {
            var name = platform == "iOS" ? "iOS Settings" : "Android Settings";
            return new[]
            {
                "Assets/CleverAdsSolutions/" + name,
                "Assets/CleverAdsSolutions/" + name + "...",
            };
        }
```

and in `Open`:

```csharp
                foreach (var menu in MenuCandidates(platform))
                    if (EditorApplication.ExecuteMenuItem(menu)) return;
```

- [ ] **Step 3: Commit**

```bash
git add Editor/Wizard/CasNativeSettings.cs Editor/Tests/CasNativeSettingsMenuTests.cs Editor/Tests/CasNativeSettingsMenuTests.cs.meta
git commit -m "fix(installer): open CAS native settings on both old and 4.7+ menu names"
```

---

### Task 2: Invalidate component cache after wizard actions

**Files:**
- Modify: `Editor/Wizard/Screens/ComponentsScreen.cs` (both button callbacks)

- [ ] **Step 1: Implement.** In the action-button callback (`var changed = c.GitInstalled ? ... : WizardActions.Apply(...)`) replace `if (changed) Rebuild();` with:

```csharp
                    if (changed)
                    {
                        // The provider's session cache assumed installs always domain-reload before
                        // the next read — not true for pure manifest writes, so drop it explicitly
                        // or the row keeps its pre-action state until a manual Refresh.
                        ComponentStatusProvider.InvalidateCache();
                        Rebuild();
                    }
```

Same change in the Remove-button callback (`if (WizardActions.Remove(...)) Rebuild();` → guard + invalidate + rebuild).

- [ ] **Step 2: Commit**

```bash
git add Editor/Wizard/Screens/ComponentsScreen.cs
git commit -m "fix(installer): re-scan component rows right after an action, no manual Refresh"
```

---

### Task 3: Metadata install retry (auto-open without restart)

**Files:**
- Create: `Editor/MetadataInstallRetry.cs` (+ `.meta`) — core asmdef, namespace `PSV.Installer`
- Modify: `Editor/MetadataAutoInstall.cs` (both transient-failure branches)
- Test: `Editor/Tests/MetadataInstallRetryTests.cs` (new, with `.meta`)

**Interfaces:**
- Produces: `internal static class MetadataInstallRetry` with `internal static void Arm()`, `internal static bool IsArmed`, `internal static int Attempts`, `internal static void ResetForTests()`.

- [ ] **Step 1: Failing tests**

```csharp
using NUnit.Framework;
using PSV.Installer;

namespace PSV.Installer.Tests
{
    public class MetadataInstallRetryTests
    {
        [SetUp] public void SetUp() => MetadataInstallRetry.ResetForTests();
        [TearDown] public void TearDown() => MetadataInstallRetry.ResetForTests();

        [Test]
        public void Arm_SetsArmedAndCountsAttempt()
        {
            MetadataInstallRetry.Arm();
            Assert.IsTrue(MetadataInstallRetry.IsArmed);
            Assert.AreEqual(1, MetadataInstallRetry.Attempts);
        }

        [Test]
        public void Arm_WhileArmed_DoesNotDoubleCount()
        {
            MetadataInstallRetry.Arm();
            MetadataInstallRetry.Arm();
            Assert.AreEqual(1, MetadataInstallRetry.Attempts);
        }

        [Test]
        public void Arm_StopsAtMaxAttempts()
        {
            for (var i = 0; i < 10; i++)
            {
                MetadataInstallRetry.Arm();
                MetadataInstallRetry.DisarmForTests();
            }
            Assert.AreEqual(5, MetadataInstallRetry.Attempts);
            MetadataInstallRetry.Arm();
            Assert.IsFalse(MetadataInstallRetry.IsArmed);
        }
    }
}
```

- [ ] **Step 2: Implement `MetadataInstallRetry.cs`**

```csharp
using UnityEditor;
using UnityEditor.PackageManager;

namespace PSV.Installer
{
    /// <summary>
    /// Self-heal for a TRANSIENT metadata-install failure ("exclusive access" while the Package
    /// Manager is busy during a fresh project import). The old behaviour waited for "the next
    /// domain reload" — which a quiet editor never produces, stranding metadata (and the Hub
    /// auto-open) until an editor restart. Armed only by <see cref="MetadataAutoInstall"/> on a
    /// transient failure; re-runs the install when the Package Manager finishes whatever it was
    /// doing (<see cref="Events.registeredPackages"/>) or after a short timer, whichever first.
    /// Capped per domain-reload epoch (statics reset on reload; every reload re-enters Bootstrap).
    /// </summary>
    internal static class MetadataInstallRetry
    {
        private const int MaxAttempts = 5;
        private const double DelaySeconds = 5.0;

        private static int _attempts;
        private static bool _armed;
        private static double _nextAt;

        internal static bool IsArmed => _armed;
        internal static int Attempts => _attempts;

        internal static void Arm()
        {
            if (_armed || _attempts >= MaxAttempts) return;
            _armed = true;
            _attempts++;
            _nextAt = EditorApplication.timeSinceStartup + DelaySeconds;
            EditorApplication.update += Tick;
            Events.registeredPackages += OnPackagesChanged;
        }

        private static void OnPackagesChanged(PackageRegistrationEventArgs args) => TriggerNow();

        private static void Tick()
        {
            if (EditorApplication.timeSinceStartup < _nextAt) return;
            TriggerNow();
        }

        private static void TriggerNow()
        {
            Disarm();
            MetadataAutoInstall.Run(); // idempotent: no-ops once metadata is installed
        }

        private static void Disarm()
        {
            if (!_armed) return;
            _armed = false;
            EditorApplication.update -= Tick;
            Events.registeredPackages -= OnPackagesChanged;
        }

        internal static void DisarmForTests() => Disarm();

        internal static void ResetForTests()
        {
            Disarm();
            _attempts = 0;
        }
    }
}
```

- [ ] **Step 3: Arm on transient failures.** In `MetadataAutoInstall.Run`, both transient branches currently do `if (!transient) SessionState.SetBool(InstallAttemptedKey, true);` + warning. Add `if (transient) MetadataInstallRetry.Arm();` right after the guard in BOTH the git path and the UPM `onSuccess` catch, and update both warning texts `"transient — will retry after reload"` → `"transient — retrying shortly"`.

- [ ] **Step 4: Commit**

```bash
git add Editor/MetadataInstallRetry.cs Editor/MetadataInstallRetry.cs.meta Editor/MetadataAutoInstall.cs Editor/Tests/MetadataInstallRetryTests.cs Editor/Tests/MetadataInstallRetryTests.cs.meta
git commit -m "fix(installer): retry transient metadata install — Hub auto-opens without editor restart"
```

---

### Task 4: Top-level "CAS Hub" menu + changelog

**Files:**
- Modify: `Editor/Wizard/InstallerWizardWindow.cs` (next to the existing `[MenuItem]`s)
- Modify: `CHANGELOG.md` (extend the `0.0.1-preview.38` entry)

- [ ] **Step 1: Menu item**

```csharp
        /// <summary>Top-level entry — testers looked for the Hub in the main menu bar, not under
        /// Assets/CleverAdsSolutions (which reads as the ad-SDK settings menu). Same window.</summary>
        [MenuItem("CAS Hub/Open Hub")]
        public static void OpenFromMainMenu() => Open();
```

- [ ] **Step 2: CHANGELOG** — append bullets to the existing `0.0.1-preview.38` entry (match its style): CAS native settings open on both pre-4.7 and 4.7+ menu names; component rows refresh immediately after Install/Update/Fix/Remove; transient metadata-install failures retry in-session so the Hub auto-opens without an editor restart; new top-level "CAS Hub → Open Hub" menu.

- [ ] **Step 3: Commit**

```bash
git add Editor/Wizard/InstallerWizardWindow.cs CHANGELOG.md
git commit -m "feat(installer): top-level CAS Hub menu; changelog for UX fixes"
```

---

### Task 6: Clickable Configuration cells

**Files:**
- Modify: `Editor/Wizard/Screens/ConfigureScreen.cs`
- Modify: the wizard USS file that defines `.cas-setup-cell` (find via Grep under `Editor/Wizard/` — likely `Editor/Wizard/Uxml/*.uss`)
- Modify: `CHANGELOG.md` (one more bullet in the `0.0.1-preview.38` entry)
- Test: `Editor/Tests/ConfigureCellActionTests.cs` (new, with `.meta`)

**Interfaces:**
- Produces: `internal enum ConfigCellAction { None, OpenCasSettings, PingFirebaseFile, LocateFirebaseFile }` and `internal static ConfigCellAction ConfigureScreen.ResolveCellAction(string rowId, bool configured)` (pure): CAS id / Tenjin id → `OpenCasSettings` regardless of `configured`; Firebase id → `configured ? PingFirebaseFile : LocateFirebaseFile`; anything else → `None`.

- [ ] **Step 1: Failing tests**

```csharp
using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class ConfigureCellActionTests
    {
        private const string Cas = "com.cleversolutions.ads.unity";
        private const string Firebase = "com.google.firebase.analytics";
        private const string Tenjin = "com.tenjin.sdk";

        [Test] public void Cas_AnyState_OpensCasSettings()
        {
            Assert.AreEqual(ConfigCellAction.OpenCasSettings, ConfigureScreen.ResolveCellAction(Cas, true));
            Assert.AreEqual(ConfigCellAction.OpenCasSettings, ConfigureScreen.ResolveCellAction(Cas, false));
        }

        [Test] public void Tenjin_AnyState_OpensCasSettings()
        {
            Assert.AreEqual(ConfigCellAction.OpenCasSettings, ConfigureScreen.ResolveCellAction(Tenjin, true));
            Assert.AreEqual(ConfigCellAction.OpenCasSettings, ConfigureScreen.ResolveCellAction(Tenjin, false));
        }

        [Test] public void Firebase_ConfiguredPings_MissingLocates()
        {
            Assert.AreEqual(ConfigCellAction.PingFirebaseFile,   ConfigureScreen.ResolveCellAction(Firebase, true));
            Assert.AreEqual(ConfigCellAction.LocateFirebaseFile, ConfigureScreen.ResolveCellAction(Firebase, false));
        }

        [Test] public void UnknownRow_None()
        {
            Assert.AreEqual(ConfigCellAction.None, ConfigureScreen.ResolveCellAction("com.other.sdk", true));
        }
    }
}
```

- [ ] **Step 2: Implement.** In `ConfigureScreen`:
  1. Add the enum + pure `ResolveCellAction` (internal static, next to the row builders).
  2. Thread context: `BuildRow` and the column/cell builders become instance methods (they need `LocateFirebaseConfigFile`/`RefreshFromDisk`); pass `row.Id` and the platform string down: `BuildPlatformColumn(row.Id, row.Android, "Android")` etc.
  3. `BuildCell(rowId, platform, cell)`: resolve the action; when != None, register a click handler and add class `cas-setup-cell--click`:
     - `OpenCasSettings` → `CasNativeSettings.Open(platform)`
     - `PingFirebaseFile` → find the config file under `Assets/` (read `SetupChecker.cs` first — reuse its assetFile lookup helper if accessible, otherwise a minimal `Directory.GetFiles(Application.dataPath, fileName, SearchOption.AllDirectories)` + convert to an asset path) and `Selection.activeObject` + `EditorGUIUtility.PingObject` it; file name per platform: `google-services.json` (Android) / `GoogleService-Info.plist` (iOS)
     - `LocateFirebaseFile` → generalize `LocateFirebaseConfigFile()` to `LocateFirebaseConfigFile(string platform)` (dialog title names the per-platform file); keep the panel's existing button calling it with the ACTIVE platform to preserve current behavior
     - `configured` for the resolver = `cell.Result?.Status == ReqStatus.Configured`
  4. `BuildTenjinColumn(platform)`: when `probe.FieldSupported`, make the cell line clickable the same way (`OpenCasSettings`).
  5. USS: add a `.cas-setup-cell--click` rule near the existing `.cas-setup-cell` styles — `cursor: link;` plus a subtle hover background consistent with the file's conventions (reuse an existing hover variable/color from that USS).
  6. CHANGELOG: bullet — Configuration table statuses are clickable: CAS/Tenjin cells open the CAS settings window, Firebase cells ping the config file or open the locate dialog when it's missing.

- [ ] **Step 3: Commit**

```bash
git add Editor/Wizard/Screens/ConfigureScreen.cs Editor/Tests/ConfigureCellActionTests.cs Editor/Tests/ConfigureCellActionTests.cs.meta CHANGELOG.md <uss file>
git commit -m "feat(installer): clickable Configuration statuses — open/create CAS settings and Firebase config"
```

---

### Task 5 (renumbered — runs LAST): Owner verification (gate before release)

- [ ] Unity compile + EditMode suite (new: `CasNativeSettingsMenuTests`, `MetadataInstallRetryTests`; full regression).
- [ ] Smoke: Install a component → row updates without Refresh; "CAS Hub → Open Hub" in main menu opens the wizard; Android Settings button works on CAS 4.6.6 project; fresh-project metadata install self-heals (hard to force — code-review level acceptance is fine).
- [ ] Release after owner go: sign + publish installer `0.0.1-preview.38` + metadata `0.0.2-preview.27`.

## Self-Review Notes

- Spec coverage: item 3 → Task 1; item 7 → Task 2; item 8 → Task 3; item 9 → Task 4; release gate → Task 5.
- Type check: `MenuCandidates` internal in Wizard asmdef — Tests asmdef must see Wizard internals (verify IVT in `Editor/Wizard/Properties/AssemblyInfo.cs`, add if missing — precedent exists from the redesign cycle).
- `MetadataInstallRetry` in core references only `UnityEditor`/`PackageManager` — no Wizard dependency.
