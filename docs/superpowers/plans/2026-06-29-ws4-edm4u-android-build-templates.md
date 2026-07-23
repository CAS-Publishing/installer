# WS-4 — EDM4U / Android build templates auto-enable — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax.

**Goal:** A fresh install yields a buildable Android project: the installer ensures the custom gradle/manifest templates exist (so EDM4U injects the resolved aar deps), auto-enabling them during automatic integration and surfacing a Configuration-tab check with a one-click "Enable Android build settings" Fix (feedback #6).

**Architecture:** A pure `AndroidBuildTemplates` lists the required files in `Assets/Plugins/Android/` and computes which are missing (testable). `AndroidBuildFix.Ensure()` copies any missing template from the running Editor's default GradleTemplates dir (resolved at runtime via `EditorApplication.applicationContentsPath`) — graceful per file: if a default source isn't found it logs and skips, never breaking the project. A Configuration-tab banner shows the missing count + the Fix button; `AutoInstaller.StartAll` calls `Ensure()` so the auto path produces a buildable project.

**Tech Stack:** Unity 2022.3 Editor, C# UPM editor package, UI Toolkit, file IO, NUnit.

**Decision source:** `docs/superpowers/specs/2026-06-29-installer-feedback-round2-decisions.md` (#6).

## Global Constraints

- Templates are file-presence driven: their existence in `Assets/Plugins/Android/` is what enables Unity's "Custom … Template" / "Custom Main Manifest" toggles. The Fix only CREATES missing files; it never overwrites an existing template (so user/EDM4U edits are preserved).
- The Fix copies the running Editor's DEFAULT templates (correct for the Editor version) — it does not hardcode gradle content.
- Graceful degradation: a missing default source → log a warning + skip that one file; never throw, never half-break the project.
- No CLI/headless runner: file copy + UI + the auto hook are OWNER-RUN. The pure `AndroidBuildTemplates.Missing` logic is unit-tested.
- Required files (dest names under `Assets/Plugins/Android/`): `mainTemplate.gradle`, `launcherTemplate.gradle`, `baseProjectTemplate.gradle`, `gradleTemplate.properties`, `settingsTemplate.gradle`, `AndroidManifest.xml`.
- Editor default source dir (resolved at runtime): `<applicationContentsPath>/PlaybackEngines/AndroidPlayer/Tools/GradleTemplates/`. (Owner confirms the per-file presence in Unity; missing ones degrade gracefully.)
- Conventional Commits, `feat(installer):`. Installer branch `feat/installer-wizard-ui`. (No metadata change.)

---

### Task 1: `AndroidBuildTemplates` (pure) + `AndroidBuildFix`

**Files:**
- Create: `Editor/Wizard/AndroidBuildTemplates.cs` (+ `.meta` guid `d3e4f5a6b7c80819203a4b5c6d7e8f02`)
- Create: `Editor/Wizard/AndroidBuildFix.cs` (+ `.meta` guid `e4f5a6b7c8d90819203a4b5c6d7e8f03`)
- Test: `Editor/Tests/AndroidBuildTemplatesTests.cs` (+ `.meta` guid `f5a6b7c8d9e00819203a4b5c6d7e8f04`)

**Interfaces:**
- Produces: `IReadOnlyList<string> AndroidBuildTemplates.Required`; `List<string> AndroidBuildTemplates.Missing(IEnumerable<string> presentFileNames)`; `string AndroidBuildTemplates.PluginsAndroidDir` (Assets-relative); `int AndroidBuildFix.Ensure()` (copies missing defaults, returns count created); `List<string> AndroidBuildFix.MissingNow()` (current missing names).

- [ ] **Step 1: Write the failing test**

Create `Editor/Tests/AndroidBuildTemplatesTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class AndroidBuildTemplatesTests
    {
        [Test] public void Required_has_six()
            => Assert.AreEqual(6, AndroidBuildTemplates.Required.Count);

        [Test] public void Missing_none_when_all_present()
            => Assert.IsEmpty(AndroidBuildTemplates.Missing(new List<string>(AndroidBuildTemplates.Required)));

        [Test] public void Missing_all_when_none_present()
            => Assert.AreEqual(AndroidBuildTemplates.Required.Count,
                               AndroidBuildTemplates.Missing(new List<string>()).Count);

        [Test] public void Missing_is_case_insensitive()
            => Assert.IsEmpty(AndroidBuildTemplates.Missing(new List<string> {
                "MAINTEMPLATE.GRADLE", "launchertemplate.gradle", "baseProjectTemplate.gradle",
                "gradleTemplate.properties", "settingsTemplate.gradle", "androidmanifest.xml" }));

        [Test] public void Missing_reports_the_absent_one()
        {
            var present = new List<string>(AndroidBuildTemplates.Required);
            present.Remove("AndroidManifest.xml");
            var missing = AndroidBuildTemplates.Missing(present);
            Assert.AreEqual(1, missing.Count);
            Assert.AreEqual("AndroidManifest.xml", missing[0]);
        }
    }
}
```

Create the `.meta` (full MonoImporter block, guid `f5a6b7c8d9e00819203a4b5c6d7e8f04`).

- [ ] **Step 2: Run to verify it fails** — Expected: FAIL (no `AndroidBuildTemplates`).

- [ ] **Step 3: Implement `AndroidBuildTemplates`**

Create `Editor/Wizard/AndroidBuildTemplates.cs`:

```csharp
using System.Collections.Generic;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// The custom Android build templates a buildable EDM4U project needs under
    /// <c>Assets/Plugins/Android/</c>. Their presence is what enables Unity's "Custom … Template"
    /// and "Custom Main Manifest" toggles, so EDM4U can inject resolved dependencies.
    /// </summary>
    internal static class AndroidBuildTemplates
    {
        public const string PluginsAndroidDir = "Assets/Plugins/Android";

        public static readonly IReadOnlyList<string> Required = new[]
        {
            "mainTemplate.gradle",
            "launcherTemplate.gradle",
            "baseProjectTemplate.gradle",
            "gradleTemplate.properties",
            "settingsTemplate.gradle",
            "AndroidManifest.xml",
        };

        /// <summary>Required template names not present in <paramref name="presentFileNames"/> (case-insensitive). Pure.</summary>
        public static List<string> Missing(IEnumerable<string> presentFileNames)
        {
            var present = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            if (presentFileNames != null) foreach (var n in presentFileNames) if (!string.IsNullOrEmpty(n)) present.Add(n);
            var missing = new List<string>();
            foreach (var req in Required) if (!present.Contains(req)) missing.Add(req);
            return missing;
        }
    }
}
```

Create its `.meta` (2-line, guid `d3e4f5a6b7c80819203a4b5c6d7e8f02`).

- [ ] **Step 4: Implement `AndroidBuildFix`**

Create `Editor/Wizard/AndroidBuildFix.cs`:

```csharp
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// Ensures the custom Android build templates exist in <c>Assets/Plugins/Android/</c> by copying the
    /// running Editor's defaults. Only creates missing files (never overwrites). Graceful: a missing
    /// default source is logged and skipped — never throws, never half-breaks the project.
    /// </summary>
    internal static class AndroidBuildFix
    {
        // Resolved at runtime so it matches the running Editor version, wherever it's installed.
        private static string EditorTemplatesDir => Path.Combine(
            EditorApplication.applicationContentsPath, "PlaybackEngines", "AndroidPlayer", "Tools", "GradleTemplates");

        private static string DestDirAbs => Path.Combine(
            Application.dataPath, "Plugins", "Android");

        /// <summary>Required templates currently absent from the project.</summary>
        public static List<string> MissingNow()
        {
            var present = new List<string>();
            if (Directory.Exists(DestDirAbs))
                foreach (var f in Directory.GetFiles(DestDirAbs))
                    present.Add(Path.GetFileName(f));
            return AndroidBuildTemplates.Missing(present);
        }

        /// <summary>
        /// Creates any missing Android build template from the Editor default. Returns how many were
        /// created. Logs (and skips) a template whose Editor default can't be found.
        /// </summary>
        public static int Ensure()
        {
            var missing = MissingNow();
            if (missing.Count == 0) return 0;

            Directory.CreateDirectory(DestDirAbs);
            var created = 0;
            foreach (var file in missing)
            {
                var src = Path.Combine(EditorTemplatesDir, file);
                if (!File.Exists(src))
                {
                    Debug.LogWarning($"[PSV Installer] No Editor default found for '{file}' at '{src}'. " +
                        "Skipped — enable it manually in Player Settings if your build needs it.");
                    continue;
                }
                try
                {
                    File.Copy(src, Path.Combine(DestDirAbs, file), overwrite: false);
                    created++;
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[PSV Installer] Couldn't create '{file}': {e.Message}");
                }
            }
            if (created > 0)
            {
                AssetDatabase.Refresh();
                Debug.Log($"[PSV Installer] Enabled {created} Android build template(s) under Assets/Plugins/Android.");
            }
            return created;
        }
    }
}
```

Create its `.meta` (2-line, guid `e4f5a6b7c8d90819203a4b5c6d7e8f03`).

- [ ] **Step 5: Run to verify the pure tests pass** — Expected: PASS (5/5).

- [ ] **Step 6: Commit**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer"
git add Editor/Wizard/AndroidBuildTemplates.cs Editor/Wizard/AndroidBuildTemplates.cs.meta Editor/Wizard/AndroidBuildFix.cs Editor/Wizard/AndroidBuildFix.cs.meta Editor/Tests/AndroidBuildTemplatesTests.cs Editor/Tests/AndroidBuildTemplatesTests.cs.meta
git commit -m "feat(installer): Android build template check + Ensure (copy Editor defaults) (#6)"
```

---

### Task 2: Configuration-tab banner + Enable button

**Files:**
- Modify: `Editor/Wizard/Screens/SetupScreen.cs` (prepend a banner when templates are missing)
- Modify: `Editor/Wizard/Uss/theme.uss` (banner styling)

**Interfaces:**
- Consumes: `AndroidBuildFix.MissingNow()`, `AndroidBuildFix.Ensure()`.

- [ ] **Step 1: Prepend the banner in `SetupScreen.Rebuild`**

In `Editor/Wizard/Screens/SetupScreen.cs`, in `Rebuild()`, right after `_rowsHost.Clear();`, add:

```csharp
            // Android build readiness: missing gradle/manifest templates make a fresh project fail to
            // build with a non-obvious cause — surface it with a one-click Enable.
            var missingAndroid = AndroidBuildFix.MissingNow();
            if (missingAndroid.Count > 0)
            {
                var banner = new VisualElement();
                banner.AddToClassList("cas-androidbanner");
                var msg = new Label($"⚠ {missingAndroid.Count} Android build template(s) missing — " +
                                    "the project may not build until they're enabled.");
                msg.AddToClassList("cas-androidbanner__msg");
                banner.Add(msg);
                var fix = new Button(() => { AndroidBuildFix.Ensure(); Rebuild(); })
                    { text = "Enable Android build settings" };
                fix.AddToClassList("cas-btn");
                fix.AddToClassList("cas-btn--primary");
                banner.Add(fix);
                _rowsHost.Add(banner);
            }
```

- [ ] **Step 2: USS**

In `theme.uss`, add:

```css
.cas-androidbanner { flex-direction: column; padding: 8px 10px; margin-bottom: 6px; background-color: #3A332A; border-left-width: 3px; border-left-color: #E8B463; }
.cas-androidbanner__msg { color: #E8C896; font-size: 11px; white-space: normal; margin-bottom: 6px; }
```

- [ ] **Step 3: Owner-run visual check**

On a project with no Android templates, the Configuration tab shows the amber banner; clicking "Enable Android build settings" creates the templates (Player Settings checkboxes flip on) and the banner disappears on rebuild.

- [ ] **Step 4: Commit**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer"
git add Editor/Wizard/Screens/SetupScreen.cs Editor/Wizard/Uss/theme.uss
git commit -m "feat(installer): Configuration banner + Enable for missing Android templates (#6)"
```

---

### Task 3: Auto-enable during automatic integration

**Files:**
- Modify: `Editor/Wizard/AutoInstaller.cs` (`StartAll` calls `AndroidBuildFix.Ensure()`)

**Interfaces:**
- Consumes: `AndroidBuildFix.Ensure()`.

- [ ] **Step 1: Ensure templates when an auto run starts**

In `Editor/Wizard/AutoInstaller.cs`, in `StartAll`, at the very start of the method body (before any install work), add:

```csharp
            // Automatic integration should yield a buildable project — ensure the Android build
            // templates exist before EDM4U resolves the installed packages' dependencies.
            AndroidBuildFix.Ensure();
```

- [ ] **Step 2: Owner-run verification**

Run an automatic integration on a clean project: after it completes, `Assets/Plugins/Android/` contains the gradle/manifest templates (Player Settings custom-template checkboxes are on), and an Android build proceeds without the missing-template failure.

- [ ] **Step 3: Commit**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer"
git add Editor/Wizard/AutoInstaller.cs
git commit -m "feat(installer): auto-enable Android build templates on automatic integration (#6)"
```

---

### Task 4: Version bump

**Files:** installer `package.json`, `CHANGELOG.md`.

- [ ] **Step 1: Bump + changelog**

`package.json`: `0.0.1-preview.32` → `0.0.1-preview.33`. CHANGELOG top entry `## [0.0.1-preview.33] - 2026-06-29`: "Automatic integration now enables the custom Android gradle/manifest templates (so a fresh project builds), and the Configuration tab flags + one-click-enables any that are missing (#6)."

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer"
git add package.json CHANGELOG.md
git commit -m "chore(installer): release notes for Android build template auto-enable (preview.33)"
```

---

## Self-Review

- **Spec coverage (#6):** auto-enable on automatic integration → Task 3; Configuration check + one-click Fix → Task 2; the copy-defaults mechanism → Task 1 (`AndroidBuildFix.Ensure`); never-overwrite + graceful → Task 1 (`overwrite: false`, skip+warn on missing source).
- **Placeholder scan:** none — pure logic fully coded+tested; the Editor-default copy is owner-verified with documented runtime path + graceful fallback.
- **Type consistency:** `AndroidBuildTemplates.Required`/`Missing`/`PluginsAndroidDir`, `AndroidBuildFix.Ensure()`/`MissingNow()` — consistent across Tasks 1-3.
