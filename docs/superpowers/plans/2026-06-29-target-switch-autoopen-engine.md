# Build-target-switch auto-open + WS-2 engine — Implementation Plan (Part 2 of 2)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the installer auto-open reliably after an installer-driven install (landing on Components), and auto-open at Welcome — preselecting the newly-active platform — when the build target switches to an unconfigured CAS platform.

**Architecture:** Two triggers on one engine. Trigger 1 fixes the `IntroDone` gate in `ShowIfReportChanged` so it reopens after first-run, but only for installer-driven changes — signalled by a session flag set in `MigrationRunner.Apply`. Trigger 2 adds an `IActiveBuildTargetChanged` watcher that, on a switch to Android/iOS with CAS installed but that platform unconfigured, opens the wizard at Welcome with the platform preselected via a session hint the Welcome screen consumes.

**Tech Stack:** Unity 2022.3.62f3 Editor, C# UPM editor package, `UnityEditor.Build.IActiveBuildTargetChanged`, `SessionState`, NUnit (EditMode).

**Spec:** `docs/superpowers/specs/2026-06-29-welcome-single-platform-and-target-switch-autoopen-design.md` (Part 2). Builds on Part 1 (already implemented): `PlatformDetect`, single-platform `WelcomeScreen`, `CasIdApplier.ReadExisting`.

## Key mechanism decisions (the spec left these open — flagged for review)

1. **"Installer-driven" detection (Trigger 1):** a session flag `InstallReloadSignal`, set in `MigrationRunner.Apply` whenever a manifest mutation succeeds, consumed once by `ShowIfReportChanged`. This is what lets auto-open fire after our installs but NOT on unrelated manual UPM edits (the original reason `IntroDone` gated everything).
2. **Platform handoff to an already-open window (Trigger 2):** a session hint `RequestPlatform`, set by `OpenAtWelcome(platform)` and consumed in `WelcomeScreen.OnEnter`. A fresh window would already default to the new target via `PlatformDetect`, but an already-open window keeps its constructor-time platform — the hint makes the preselection deterministic in both cases.

## Global Constraints

- Trigger 1 lands on the **Components** tab (via the existing `ResolveStartScreen` post-intro path); Trigger 2 lands on **Welcome**.
- Trigger 1 must NOT auto-reopen on unrelated/manual UPM changes after first-run — only on installer-driven manifest mutations.
- Trigger 2 condition: the new active target is Android or iOS, AND CAS is installed, AND that platform's CAS id is unconfigured (`CasIdApplier.ReadExisting(platform)` is null/empty). Any other target → do nothing.
- `OpenAtWelcome` must NOT clear `IntroDone` (it is a targeted prompt, not a first-run reset).
- `SessionState` APIs are Unity-only and cannot run headless — helpers that use them are verified by review + owner-run, not unit tests. Pure decision logic (`BuildSwitchPolicy.ShouldOpenOnSwitch`) IS unit-tested.
- No CLI/headless test runner: EditMode tests + all window/build-target behaviour are OWNER-RUN.
- Conventional Commits, `feat(installer):` scope. Installer repo: `E:/workspace/casai/dev/Packages/com.psvgamestudio.installer` (branch `feat/installer-wizard-ui`).

---

### Task 1: `InstallReloadSignal` (session flag)

A one-shot session flag: an installer-driven manifest change happened, a reload is expected.

**Files:**
- Create: `Editor/Common/InstallReloadSignal.cs`

**Interfaces:**
- Produces: `public static void InstallReloadSignal.MarkPending()`; `public static bool InstallReloadSignal.ConsumePending()` (returns whether it was set, and clears it). Namespace `PSV.Installer.Common`.

- [ ] **Step 1: Implement**

Create `Editor/Common/InstallReloadSignal.cs`:

```csharp
using UnityEditor;

namespace PSV.Installer.Common
{
    /// <summary>
    /// Session-scoped, one-shot signal that an installer-driven manifest mutation occurred and a
    /// domain reload is expected. The wizard's auto-open consumes it to reopen after an install
    /// WITHOUT re-popping on unrelated manual UPM changes. Survives the reload (SessionState),
    /// resets on editor restart.
    /// </summary>
    public static class InstallReloadSignal
    {
        private const string Key = "PSV.Installer.ExpectInstallReload";

        /// <summary>Mark that the installer just changed the manifest (a reload will follow).</summary>
        public static void MarkPending() => SessionState.SetBool(Key, true);

        /// <summary>Returns true if the flag was set, clearing it (one-shot).</summary>
        public static bool ConsumePending()
        {
            var pending = SessionState.GetBool(Key, false);
            if (pending) SessionState.EraseBool(Key);
            return pending;
        }
    }
}
```

- [ ] **Step 2: Compile check (owner-run)**

This uses `SessionState` (Unity-only) and has no headless test. Confirm it compiles when Unity reloads (owner-run); reviewed for correctness in the meantime.

- [ ] **Step 3: Commit**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer"
git add Editor/Common/InstallReloadSignal.cs
git commit -m "feat(installer): add InstallReloadSignal (installer-driven reload flag)"
```

---

### Task 2: Set the signal on installer-driven manifest mutations

**Files:**
- Modify: `Editor/Migrator/Migrator.cs` (after the successful manifest mutation at line 68)

**Interfaces:**
- Consumes: `PSV.Installer.Common.InstallReloadSignal.MarkPending()` (Task 1).

- [ ] **Step 1: Mark pending after a successful manifest mutation**

In `Editor/Migrator/Migrator.cs`, inside `Apply`, the manifest mutation block currently reads:

```csharp
                try
                {
                    ManifestWriter.ApplyActions(_manifestPath, manifestMutations);
                    executedCount += manifestMutations.Count;
                }
```

Change it to:

```csharp
                try
                {
                    ManifestWriter.ApplyActions(_manifestPath, manifestMutations);
                    executedCount += manifestMutations.Count;
                    // An installer-driven manifest change happened → a UPM domain reload will follow.
                    // Signal it so the wizard auto-reopens (to Components) even after first-run intro.
                    PSV.Installer.Common.InstallReloadSignal.MarkPending();
                }
```

- [ ] **Step 2: Owner-run compile check**

Confirm `Migrator.cs` compiles (it now references `PSV.Installer.Common`, already a sibling editor namespace).

- [ ] **Step 3: Commit**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer"
git add Editor/Migrator/Migrator.cs
git commit -m "feat(installer): signal installer-driven reload after manifest mutations"
```

---

### Task 3: Fix the `IntroDone` auto-open gate (Trigger 1)

After first-run, auto-open only for installer-driven changes; first-run behaviour unchanged. Lands on Components via the existing `ResolveStartScreen`.

**Files:**
- Modify: `Editor/Wizard/InstallerWizardWindow.cs` (`ShowIfReportChanged`, lines 100-114)

**Interfaces:**
- Consumes: `InstallReloadSignal.ConsumePending()` (Task 1); existing `Open()`, `ScanReportStore.SetLastShownHash`, `IntroDone`, `ResolveStartScreen` (lands on Components post-intro).

- [ ] **Step 1: Replace `ShowIfReportChanged`**

Replace the method body (lines 100-114) with:

```csharp
        public static void ShowIfReportChanged(ScanReport report)
        {
            if (report == null) return;

            // First run: always open (idempotent; record hash AFTER so an install/self-delete reload
            // that kills the just-opened window can't latch the hash and suppress later opens).
            if (!IntroDone)
            {
                Open();
                ScanReportStore.SetLastShownHash(report.Hash);
                return;
            }

            // After first-run: auto-open ONLY when the installer drove the change (a manifest mutation
            // set the one-shot signal). Unrelated/manual UPM changes never re-pop the window — updates
            // surface via the About badge instead. ResolveStartScreen lands this open on Components.
            if (!PSV.Installer.Common.InstallReloadSignal.ConsumePending()) return;

            Open();
            ScanReportStore.SetLastShownHash(report.Hash);
        }
```

- [ ] **Step 2: Owner-run verification (in the Part-1+2 release)**

On a project past first-run: an installer Install/Migrate → after the reload the window reopens on Components automatically; a manual `Packages/manifest.json` edit (unrelated package) → the window does NOT auto-open. (Owner-run.)

- [ ] **Step 3: Commit**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer"
git add Editor/Wizard/InstallerWizardWindow.cs
git commit -m "feat(installer): auto-reopen after installer-driven install post-intro (#7/WS-2)"
```

---

### Task 4: Switch policy + CAS-installed probe

**Files:**
- Create: `Editor/Wizard/BuildSwitchPolicy.cs`
- Create: `Editor/Wizard/CasPresence.cs`
- Test: `Editor/Tests/BuildSwitchPolicyTests.cs`

**Interfaces:**
- Produces: `internal static bool BuildSwitchPolicy.ShouldOpenOnSwitch(bool casInstalled, string existingId)`; `public static bool CasPresence.IsInstalled()`.
- Consumes: `ComponentStatusProvider.TryGetStatuses(out List<ComponentStatus>, out string)`; `ComponentStatus.Id` / `.Installed`.

- [ ] **Step 1: Write the failing test**

Create `Editor/Tests/BuildSwitchPolicyTests.cs`:

```csharp
using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class BuildSwitchPolicyTests
    {
        [Test] public void Open_when_installed_and_unconfigured_null()
            => Assert.IsTrue(BuildSwitchPolicy.ShouldOpenOnSwitch(true, null));

        [Test] public void Open_when_installed_and_unconfigured_empty()
            => Assert.IsTrue(BuildSwitchPolicy.ShouldOpenOnSwitch(true, ""));

        [Test] public void No_open_when_already_configured()
            => Assert.IsFalse(BuildSwitchPolicy.ShouldOpenOnSwitch(true, "1234567890"));

        [Test] public void No_open_when_cas_not_installed()
            => Assert.IsFalse(BuildSwitchPolicy.ShouldOpenOnSwitch(false, null));
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Unity Test Runner → EditMode → run `BuildSwitchPolicyTests`. Expected: FAIL — `BuildSwitchPolicy` does not exist.

- [ ] **Step 3: Implement `BuildSwitchPolicy`**

Create `Editor/Wizard/BuildSwitchPolicy.cs`:

```csharp
namespace PSV.Installer.Wizard
{
    /// <summary>Decides whether a build-target switch should auto-open the wizard at Welcome.</summary>
    internal static class BuildSwitchPolicy
    {
        /// <summary>
        /// Open iff CAS is installed AND the newly-active platform's CAS id is unconfigured
        /// (null or empty — i.e. <see cref="CasIdApplier.ReadExisting"/> found no real value).
        /// Pure/testable.
        /// </summary>
        internal static bool ShouldOpenOnSwitch(bool casInstalled, string existingId) =>
            casInstalled && string.IsNullOrEmpty(existingId);
    }
}
```

- [ ] **Step 4: Implement `CasPresence`**

Create `Editor/Wizard/CasPresence.cs`:

```csharp
namespace PSV.Installer.Wizard
{
    /// <summary>Whether the CAS package is installed (package-manager truth via the scanner).</summary>
    internal static class CasPresence
    {
        private const string CasId = "com.cleversolutions.ads.unity";

        public static bool IsInstalled()
        {
            if (ComponentStatusProvider.TryGetStatuses(out var statuses, out _))
                foreach (var s in statuses)
                    if (s != null && s.Id == CasId && s.Installed) return true;
            return false;
        }
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

Unity Test Runner → EditMode → run `BuildSwitchPolicyTests`. Expected: PASS (4/4).

- [ ] **Step 6: Commit**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer"
git add Editor/Wizard/BuildSwitchPolicy.cs Editor/Wizard/CasPresence.cs Editor/Tests/BuildSwitchPolicyTests.cs
git commit -m "feat(installer): build-switch policy + CAS-installed probe"
```

---

### Task 5: `OpenAtWelcome` + Welcome platform hint

A window entry that opens at Welcome with a preselected platform (without resetting the intro), and Welcome consuming that hint.

**Files:**
- Modify: `Editor/Wizard/InstallerWizardWindow.cs` (add `RequestPlatformKey`, `OpenAtWelcome`, `ConsumeRequestedPlatform`)
- Modify: `Editor/Wizard/Screens/WelcomeScreen.cs` (`OnEnter` consumes the hint)

**Interfaces:**
- Produces: `public static void InstallerWizardWindow.OpenAtWelcome(string platform)`; `internal static string InstallerWizardWindow.ConsumeRequestedPlatform()`.
- Consumes (in WelcomeScreen): `InstallerWizardWindow.ConsumeRequestedPlatform()`.

- [ ] **Step 1: Add the methods to `InstallerWizardWindow`**

In `Editor/Wizard/InstallerWizardWindow.cs`, after the `CurrentScreenKey` constant (line 30), add:

```csharp
        // Set by OpenAtWelcome so the Welcome screen preselects a specific platform on a build-target
        // switch even when the window was already open (its WelcomeScreen kept its construction-time
        // platform). One-shot, consumed in WelcomeScreen.OnEnter.
        private const string RequestPlatformKey = "PSV.Installer.Wizard.RequestPlatform";
```

Then add these two methods after `OpenFirstRun` (after line 86):

```csharp
        /// <summary>
        /// Opens the wizard at Welcome with <paramref name="platform"/> preselected (the build-target
        /// watcher uses this). Does NOT clear <see cref="IntroDone"/> — it is a targeted "configure
        /// this platform" prompt, not a first-run reset.
        /// </summary>
        public static void OpenAtWelcome(string platform)
        {
            SessionState.SetString(RequestPlatformKey, platform ?? string.Empty);
            SessionState.SetString(CurrentScreenKey, "welcome");
            Open();
            if (HasOpenInstances<InstallerWizardWindow>())
                GetWindow<InstallerWizardWindow>()._router?.GoTo("welcome");
        }

        /// <summary>Returns and clears the platform requested by <see cref="OpenAtWelcome"/> (one-shot).</summary>
        internal static string ConsumeRequestedPlatform()
        {
            var p = SessionState.GetString(RequestPlatformKey, string.Empty);
            if (!string.IsNullOrEmpty(p)) SessionState.EraseString(RequestPlatformKey);
            return p;
        }
```

- [ ] **Step 2: Consume the hint in `WelcomeScreen.OnEnter`**

In `Editor/Wizard/Screens/WelcomeScreen.cs`, in `OnEnter`, replace the final two lines:

```csharp
            ShowPlatform(_platform);
            ShowMethod(_method);
```

with:

```csharp
            // A build-target switch may request a specific platform (overrides the construction-time
            // default, which matters when this window was already open). One-shot.
            var requested = InstallerWizardWindow.ConsumeRequestedPlatform();
            if (!string.IsNullOrEmpty(requested)) _platform = requested;

            ShowPlatform(_platform);
            ShowMethod(_method);
```

- [ ] **Step 3: Owner-run verification**

(Covered with Task 6's end-to-end check.)

- [ ] **Step 4: Commit**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer"
git add Editor/Wizard/InstallerWizardWindow.cs Editor/Wizard/Screens/WelcomeScreen.cs
git commit -m "feat(installer): OpenAtWelcome with preselected platform + Welcome hint consume"
```

---

### Task 6: `BuildTargetWatcher` (Trigger 2)

**Files:**
- Create: `Editor/Wizard/BuildTargetWatcher.cs`

**Interfaces:**
- Consumes: `UnityEditor.Build.IActiveBuildTargetChanged`; `PlatformDetect.FromBuildTarget`; `BuildSwitchPolicy.ShouldOpenOnSwitch`; `CasPresence.IsInstalled`; `CasIdApplier.ReadExisting`; `InstallerWizardWindow.OpenAtWelcome`.

- [ ] **Step 1: Implement**

Create `Editor/Wizard/BuildTargetWatcher.cs`:

```csharp
using UnityEditor;
using UnityEditor.Build;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// When the active build target switches to Android or iOS and CAS is installed but that
    /// platform's CAS id isn't configured yet, auto-opens the wizard at Welcome (preselecting the
    /// new platform) so the user can configure it. Other targets are ignored. Implements Unity's
    /// build-target-changed callback (invoked by the Editor on a successful target switch).
    /// </summary>
    internal sealed class BuildTargetWatcher : IActiveBuildTargetChanged
    {
        public int callbackOrder => 0;

        public void OnActiveBuildTargetChanged(BuildTarget previous, BuildTarget newTarget)
        {
            if (newTarget != BuildTarget.Android && newTarget != BuildTarget.iOS) return;

            var platform = PlatformDetect.FromBuildTarget(newTarget); // "Android" | "iOS"
            if (!BuildSwitchPolicy.ShouldOpenOnSwitch(
                    CasPresence.IsInstalled(),
                    CasIdApplier.ReadExisting(platform)))
                return;

            InstallerWizardWindow.OpenAtWelcome(platform);
        }
    }
}
```

- [ ] **Step 2: Owner-run end-to-end verification**

In a project with CAS installed and only Android configured: switch the build target to iOS (`File → Build Settings → iOS → Switch Platform`). Expected: after the switch the installer opens at Welcome with iOS preselected and an empty, iOS-validated field. Switching back to Android (already configured) does NOT open the window. Switching to a desktop target does nothing.

- [ ] **Step 3: Commit**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer"
git add Editor/Wizard/BuildTargetWatcher.cs
git commit -m "feat(installer): auto-open Welcome on switch to an unconfigured CAS platform (#7)"
```

---

### Task 7: Version bump + changelog

**Files:**
- Modify: `package.json`
- Modify: `CHANGELOG.md`

- [ ] **Step 1: Bump and document**

In `package.json`: `"version": "0.0.1-preview.27",` → `"version": "0.0.1-preview.28",`

In `CHANGELOG.md`, add above `## [0.0.1-preview.27]`:

```markdown
## [0.0.1-preview.28] - 2026-06-29

- **Auto-open engine (#7 / WS-2):** after first-run, the installer reopens automatically following an
  installer-driven install (landing on Components) via a one-shot reload signal — and no longer
  re-pops on unrelated manual UPM changes.
- **Build-target switch (#7):** switching the active build target to Android or iOS, when CAS is
  installed but that platform's CAS id is unconfigured, auto-opens the wizard at Welcome with the new
  platform preselected. Other targets are ignored.
```

- [ ] **Step 2: Commit**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer"
git add package.json CHANGELOG.md
git commit -m "chore(installer): release notes for auto-open engine + target-switch (preview.28)"
```

---

## Self-Review

- **Spec coverage (Part 2):** unified engine / two triggers → Tasks 1-6; Trigger 1 reliability + IntroDone fix + installer-driven-only → Tasks 1-3; lands on Components → existing `ResolveStartScreen` (Task 3 note); Trigger 2 condition (Android/iOS + CAS installed + unconfigured) → Tasks 4+6; preselect new platform incl. already-open window → Task 5 hint; `OpenAtWelcome` doesn't clear `IntroDone` → Task 5; pure `ShouldOpenOnSwitch` unit-tested → Task 4; `IntroDone` gate is the named prerequisite → Task 3.
- **Placeholder scan:** none — every step has complete code/commands. SessionState-backed helpers are explicitly owner-verified (Unity-only API), with the one pure decision unit-tested.
- **Type consistency:** `InstallReloadSignal.MarkPending`/`ConsumePending` (Tasks 1→2,3); `BuildSwitchPolicy.ShouldOpenOnSwitch(bool, string)` (Tasks 4→6 + tests); `CasPresence.IsInstalled()` (Tasks 4→6); `InstallerWizardWindow.OpenAtWelcome(string)`/`ConsumeRequestedPlatform()` (Tasks 5→6 + WelcomeScreen); `PlatformDetect.FromBuildTarget` (from Part 1) — all consistent. `CasIdApplier.ReadExisting(platform)` returns null/empty for unconfigured, matching `ShouldOpenOnSwitch`'s `string.IsNullOrEmpty` check.
