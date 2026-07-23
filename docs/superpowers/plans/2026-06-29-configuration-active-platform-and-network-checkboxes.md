# Configuration active-platform scope + network checkboxes — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Scope the installer's Configuration screen to the active build target's platform (whole screen), and replace the mutually-exclusive Optimal|Families mediation toggle with two independent CAS-solution checkboxes.

**Architecture:** All changes live in the wizard UI layer (`SetupScreen`, `Setup.uxml`) plus the best-effort CAS reflection helper (`CasMediation`). The active platform comes from the existing `PlatformDetect.ActivePlatform()`. CAS solution state is read/written through CAS's editor `DependencyManager` via reflection (no hard assembly dependency).

**Tech Stack:** Unity Editor (UI Toolkit / UIElements), C#, NUnit (Unity Test Framework, EditMode). Unity **6000.2.6f2** in dev; package must also run on **2022.3**.

## Global Constraints

- No hard compile-time dependency on CAS editor assemblies — `CasMediation` stays reflection-only and best-effort (logs `[PSV Installer] CAS network-set not applied (...)`, returns `false`, never throws).
- Every commit must compile on both Unity 2022.3 and 6000.x — do not remove a public method while a caller still references it.
- Reflection code is not unit-testable; it is verified by the owner in Unity. Only pure logic gets unit tests.
- New `.cs` / `.md` files need a sibling `.meta` with a 32-hex-char `guid` (Unity drops files without meta on import).
- The CAS solution name strings are `"OptimalAds"` (adult / Optimal) and `"FamiliesAds"` (child-directed / Families). `families == true` → `"FamiliesAds"`.
- Tests live in `Editor/Tests/`, namespace `PSV.Installer.Tests`; they see `internal` members of `PSV.Installer.Wizard`.

---

### Task 1: `PickForPlatform` pure helper + unit test

A pure helper that selects one of two per-platform values from the platform string. Used by Task 4 to pick the active platform's status cells; isolated here so it is unit-testable without constructing UI.

**Files:**
- Modify: `Editor/Wizard/Screens/SetupScreen.cs` (add an `internal static` helper)
- Create: `Editor/Tests/SetupScreenPlatformTests.cs`
- Create: `Editor/Tests/SetupScreenPlatformTests.cs.meta`

**Interfaces:**
- Produces: `internal static T SetupScreen.PickForPlatform<T>(T android, T ios, string platform)` — returns `ios` when `platform == "iOS"`, otherwise `android`.

- [ ] **Step 1: Write the failing test**

Create `Editor/Tests/SetupScreenPlatformTests.cs`:

```csharp
using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class SetupScreenPlatformTests
    {
        [Test] public void iOS_picks_ios_value()
            => Assert.AreEqual("I", SetupScreen.PickForPlatform("A", "I", "iOS"));

        [Test] public void Android_picks_android_value()
            => Assert.AreEqual("A", SetupScreen.PickForPlatform("A", "I", "Android"));

        [Test] public void Unknown_platform_defaults_to_android_value()
            => Assert.AreEqual("A", SetupScreen.PickForPlatform("A", "I", "Whatever"));
    }
}
```

Create `Editor/Tests/SetupScreenPlatformTests.cs.meta`:

```
fileFormatVersion: 2
guid: 4f8a2c1d9e6b47338a05c2e1d7b9043f
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

- [ ] **Step 2: Add a compiling stub so the test fails (not errors)**

In `Editor/Wizard/Screens/SetupScreen.cs`, add near the other private static helpers (e.g. just above `SetSegActive`):

```csharp
internal static T PickForPlatform<T>(T android, T ios, string platform) => default;
```

- [ ] **Step 3: Run the test — verify it FAILS**

In Unity: `Window → General → Test Runner → EditMode → Run All` (or run the `SetupScreenPlatformTests` class).
Expected: the three tests FAIL (stub returns `default`, i.e. `null`, not `"A"`/`"I"`).

- [ ] **Step 4: Implement the helper**

Replace the stub with:

```csharp
internal static T PickForPlatform<T>(T android, T ios, string platform) =>
    platform == "iOS" ? ios : android;
```

- [ ] **Step 5: Run the test — verify it PASSES**

In Unity Test Runner, re-run `SetupScreenPlatformTests`.
Expected: all three PASS.

- [ ] **Step 6: Commit**

```bash
git add Editor/Wizard/Screens/SetupScreen.cs Editor/Tests/SetupScreenPlatformTests.cs Editor/Tests/SetupScreenPlatformTests.cs.meta
git commit -m "test(installer): add SetupScreen.PickForPlatform helper + tests"
```

---

### Task 2: `CasMediation` per-solution API (additive)

Add per-solution activate/disable + installed-read, mirroring CAS's independent-checkbox model. **Additive only** — the old `SelectSolution` / `IsFamiliesActive` stay until Task 5 removes them, so this commit still compiles (`SetupScreen` still calls the old API).

**Files:**
- Modify: `Editor/Wizard/CasMediation.cs`

**Interfaces:**
- Consumes: existing private helpers `FindType`, `GetValue`, `Warn`, `RefreshInspector`, constants `OptimalId`/`FamiliesId`.
- Produces:
  - `public static bool CasMediation.SetSolution(BuildTarget platform, bool families, bool enable)`
  - `public static bool CasMediation.IsSolutionInstalled(BuildTarget platform, bool families)`

- [ ] **Step 1: Add `SetSolution`**

In `Editor/Wizard/CasMediation.cs`, add this method (after `SelectSolution`). It reuses the same Create/find-by-name reflection but acts on a single solution and chooses activate vs. disable:

```csharp
/// <summary>
/// Activates or disables ONE CAS mediation solution (OptimalAds / FamiliesAds) independently,
/// mirroring CAS's own per-solution checkboxes. Best-effort: a missing CAS type/member logs a
/// warning and returns false, never throws. Does not touch the other solution.
/// </summary>
public static bool SetSolution(BuildTarget platform, bool families, bool enable)
{
    try
    {
        var dmType = FindType("DependencyManager");
        if (dmType == null) return Warn("DependencyManager type not found");

        var audienceType = FindType("Audience");
        if (audienceType == null) return Warn("Audience type not found");
        var audienceVal = Enum.ToObject(audienceType, CasAudience.ForFamilies(families));

        var create = dmType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static,
            null, new[] { typeof(BuildTarget), audienceType, typeof(bool) }, null);
        if (create == null) return Warn("DependencyManager.Create(BuildTarget,Audience,bool) not found");

        var manager = create.Invoke(null, new object[] { platform, audienceVal, false });
        if (manager == null) return Warn("DependencyManager.Create returned null");

        var solutionsMember = dmType.GetField("solutions") != null
            ? (MemberInfo)dmType.GetField("solutions")
            : dmType.GetProperty("solutions");
        var solutions = GetValue(solutionsMember, manager) as Array;
        if (solutions == null) return Warn("solutions not found");

        var wantName = families ? FamiliesId : OptimalId;
        object target = null;
        foreach (var sol in solutions)
        {
            if (sol == null) continue;
            var nameMember = (MemberInfo)sol.GetType().GetField("name") ?? sol.GetType().GetProperty("name");
            if (GetValue(nameMember, sol) as string == wantName) { target = sol; break; }
        }
        if (target == null) return Warn($"solution '{wantName}' not present");

        var methodName = enable ? "ActivateDependencies" : "DisableDependencies";
        var method = target.GetType().GetMethod(methodName);
        if (method == null) return Warn("Dependency." + methodName + " not found");
        method.Invoke(target, new[] { (object)platform, manager });

        AssetDatabase.Refresh();
        RefreshInspector();
        return true;
    }
    catch (Exception e)
    {
        return Warn("reflection error: " + e.Message);
    }
}
```

- [ ] **Step 2: Add `IsSolutionInstalled`**

Add (after `IsFamiliesActive`). It generalises `IsFamiliesActive` to either solution:

```csharp
/// <summary>
/// Best-effort read: true when the given solution (Families or Optimal) is installed
/// (IsInstalled() == installedVersion present). Returns false on any reflection failure.
/// </summary>
public static bool IsSolutionInstalled(BuildTarget platform, bool families)
{
    try
    {
        var dmType = FindType("DependencyManager");
        var audienceType = FindType("Audience");
        if (dmType == null || audienceType == null) return false;

        var create = dmType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static,
            null, new[] { typeof(BuildTarget), audienceType, typeof(bool) }, null);
        if (create == null) return false;

        var manager = create.Invoke(null, new object[] { platform, Enum.ToObject(audienceType, 0), false });
        if (manager == null) return false;

        var solutionsMember = dmType.GetField("solutions") != null
            ? (MemberInfo)dmType.GetField("solutions")
            : dmType.GetProperty("solutions");
        if (!(GetValue(solutionsMember, manager) is Array solutions)) return false;

        var wantName = families ? FamiliesId : OptimalId;
        foreach (var sol in solutions)
        {
            if (sol == null) continue;
            var nameMember = (MemberInfo)sol.GetType().GetField("name") ?? sol.GetType().GetProperty("name");
            if (GetValue(nameMember, sol) as string != wantName) continue;
            var isInstalled = sol.GetType().GetMethod("IsInstalled");
            return isInstalled != null && isInstalled.Invoke(sol, null) is bool b && b;
        }
        return false;
    }
    catch { return false; }
}
```

- [ ] **Step 3: Verify compilation in Unity**

Switch to Unity and let it recompile (or `Assets → Reimport`). Expected: no compile errors in the Console; both old and new methods present.

- [ ] **Step 4: Commit**

```bash
git add Editor/Wizard/CasMediation.cs
git commit -m "feat(installer): per-solution CasMediation API (SetSolution/IsSolutionInstalled)"
```

---

### Task 3: `Setup.uxml` — single platform header label

Replace the two fixed `Android` / `iOS` header labels with one named label the screen fills per active platform.

**Files:**
- Modify: `Editor/Wizard/Setup.uxml`

**Interfaces:**
- Produces: a `Label` named `setup-th-plat` (classes `cas-th cas-setup-col-plat`) inside `cas-setup-head`.

- [ ] **Step 1: Edit the header row**

In `Editor/Wizard/Setup.uxml`, replace this block:

```xml
            <ui:VisualElement class="cas-setup-head">
                <ui:Label text="Component" class="cas-th cas-setup-col-comp" />
                <ui:Label text="Android" class="cas-th cas-setup-col-plat" />
                <ui:Label text="iOS" class="cas-th cas-setup-col-plat" />
            </ui:VisualElement>
```

with:

```xml
            <ui:VisualElement class="cas-setup-head">
                <ui:Label text="Component" class="cas-th cas-setup-col-comp" />
                <ui:Label name="setup-th-plat" class="cas-th cas-setup-col-plat" />
            </ui:VisualElement>
```

- [ ] **Step 2: Verify in Unity**

Open `PSV → Installer…`, go to the Configuration tab. Expected: the header shows `Component` and one (currently blank — filled in Task 4) platform column; no compile/UXML errors in the Console.

- [ ] **Step 3: Commit**

```bash
git add Editor/Wizard/Setup.uxml
git commit -m "feat(installer): single platform header label in Configuration"
```

---

### Task 4: `SetupScreen` — scope the whole screen to the active platform

Resolve the active platform once per rebuild; fill the header; render only that platform's status column and CAS panel; count attention for that platform only.

**Files:**
- Modify: `Editor/Wizard/Screens/SetupScreen.cs`

**Interfaces:**
- Consumes: `PlatformDetect.ActivePlatform()`, `SetupScreen.PickForPlatform<T>` (Task 1), the `setup-th-plat` label (Task 3).
- Produces: `BuildCasConfig(string platform)` and `BuildRow(SetupModel.Row, bool, string platform)` now take the active platform.

- [ ] **Step 1: Cache the header label**

Add a field next to `_summary` and query it in the constructor.

Field (near `private readonly Label _summary;`):

```csharp
        private readonly Label _thPlat;
```

In the constructor, after `_summary = Root.Q<Label>("setup-summary");`:

```csharp
            _thPlat   = Root.Q<Label>("setup-th-plat");
```

- [ ] **Step 2: Resolve platform + fill header at the top of `Rebuild()`**

In `Rebuild()`, immediately after `_rowsHost.Clear();`:

```csharp
            var platform = PlatformDetect.ActivePlatform();
            if (_thPlat != null) _thPlat.text = platform;
```

- [ ] **Step 3: Pass the platform into the row loop and attention count**

Replace the row loop body in `Rebuild()`:

```csharp
            foreach (var row in rows)
            {
                if (!row.Installed) continue; // Configuration lists only installed components
                _rowsHost.Add(BuildRow(row, alt: installed % 2 == 1));
                installed++;
                attention += CountAttention(row.Android) + CountAttention(row.IOS);
            }
```

with:

```csharp
            foreach (var row in rows)
            {
                if (!row.Installed) continue; // Configuration lists only installed components
                _rowsHost.Add(BuildRow(row, alt: installed % 2 == 1, platform));
                installed++;
                attention += CountAttention(PickForPlatform(row.Android, row.IOS, platform));
            }
```

- [ ] **Step 4: Make `BuildRow` render one platform column**

Change the signature and the grid assembly. Replace:

```csharp
        private static VisualElement BuildRow(SetupModel.Row row, bool alt)
        {
```

with:

```csharp
        private static VisualElement BuildRow(SetupModel.Row row, bool alt, string platform)
        {
```

Then, inside `BuildRow`, replace the grid block:

```csharp
            // Status line: the 3-column grid (Component | Android | iOS), read-only.
            var grid = new VisualElement();
            grid.AddToClassList("cas-setup-row__grid");
            grid.Add(comp);
            grid.Add(BuildPlatformColumn(row.Android));
            grid.Add(BuildPlatformColumn(row.IOS));
            el.Add(grid);

            // CAS gets a dedicated, labelled config card BELOW the status grid (formats + audience),
            // instead of crowding the status columns.
            if (row.AdFormats)
                el.Add(BuildCasConfig());
            return el;
```

with:

```csharp
            // Status line: 2-column grid (Component | active platform), read-only.
            var grid = new VisualElement();
            grid.AddToClassList("cas-setup-row__grid");
            grid.Add(comp);
            grid.Add(BuildPlatformColumn(PickForPlatform(row.Android, row.IOS, platform)));
            el.Add(grid);

            // CAS gets a dedicated, labelled config card BELOW the status grid (formats + audience),
            // for the active platform only.
            if (row.AdFormats)
                el.Add(BuildCasConfig(platform));
            return el;
```

- [ ] **Step 5: Make `BuildCasConfig` render one platform panel**

Replace:

```csharp
        private static VisualElement BuildCasConfig()
        {
            var card = new VisualElement();
            card.AddToClassList("cas-cfg");

            var title = new Label("CAS CONFIGURATION");
            title.AddToClassList("cas-cfg__title");
            card.Add(title);

            var plats = new VisualElement();
            plats.AddToClassList("cas-cfg__plats");
            plats.Add(PlatformConfig("Android"));
            plats.Add(PlatformConfig("iOS"));
            card.Add(plats);
            return card;
        }
```

with:

```csharp
        private static VisualElement BuildCasConfig(string platform)
        {
            var card = new VisualElement();
            card.AddToClassList("cas-cfg");

            var title = new Label("CAS CONFIGURATION");
            title.AddToClassList("cas-cfg__title");
            card.Add(title);

            var plats = new VisualElement();
            plats.AddToClassList("cas-cfg__plats");
            plats.Add(PlatformConfig(platform));
            card.Add(plats);
            return card;
        }
```

- [ ] **Step 6: Verify in Unity**

Open Configuration with **Android** active: header reads `Android`, each installed component shows a single status column, and the CAS card shows one `Android` panel. Switch the build target to **iOS** (File → Build Settings) and reopen/Refresh: everything reflects iOS. No Console errors.

- [ ] **Step 7: Commit**

```bash
git add Editor/Wizard/Screens/SetupScreen.cs
git commit -m "feat(installer): scope Configuration to the active build target platform"
```

---

### Task 5: `SetupScreen` — independent network checkboxes + remove mutex API

Replace the Optimal|Families mutex segment with two independent toggles wired to `CasMediation.SetSolution`, then delete the now-unused mutex methods.

**Files:**
- Modify: `Editor/Wizard/Screens/SetupScreen.cs`
- Modify: `Editor/Wizard/CasMediation.cs`

**Interfaces:**
- Consumes: `CasMediation.SetSolution(BuildTarget, bool, bool)`, `CasMediation.IsSolutionInstalled(BuildTarget, bool)` (Task 2).

- [ ] **Step 1: Replace the mutex segment in `PlatformConfig`**

In `Editor/Wizard/Screens/SetupScreen.cs`, replace this block (from the `// Network SET` comment through `box.Add(seg);` and `Highlight(families);`):

```csharp
            // Network SET (CAS mediation solution), not the audience: Optimal | Families, mutually
            // exclusive. Clicking activates that solution's adapters via CasMediation. Optimal is the
            // default; the highlight reflects the actually-installed solution (best-effort read).
            var bt = platform == "iOS" ? UnityEditor.BuildTarget.iOS : UnityEditor.BuildTarget.Android;
            var families = CasMediation.IsFamiliesActive(bt);

            var seg = new VisualElement();
            seg.AddToClassList("cas-seg");
            var optBtn = new Button { text = "Optimal" };
            var famBtn = new Button { text = "Families" };
            foreach (var b in new[] { optBtn, famBtn })
            {
                b.AddToClassList("cas-btn");
                b.AddToClassList("cas-btn--sm");
                b.AddToClassList("cas-seg__item");
            }

            void Highlight(bool fam) { SetSegActive(optBtn, !fam); SetSegActive(famBtn, fam); }

            optBtn.clicked += () => { if (CasMediation.SelectSolution(bt, false)) Highlight(false); };
            famBtn.clicked += () => { if (CasMediation.SelectSolution(bt, true)) Highlight(true); };

            seg.Add(optBtn);
            seg.Add(famBtn);
            box.Add(seg);
            Highlight(families); // Optimal active by default (FamiliesAds not installed)
```

with:

```csharp
            // Network SETS (CAS mediation solutions) as INDEPENDENT checkboxes, mirroring CAS's own
            // model: OptimalAds and FamiliesAds can each be on/off independently. Each toggle
            // activates/disables that one solution via CasMediation; on a reflection failure the
            // toggle reverts so the UI never claims a state that isn't on disk.
            var bt = platform == "iOS" ? UnityEditor.BuildTarget.iOS : UnityEditor.BuildTarget.Android;
            box.Add(SolutionToggle("Optimal", bt, families: false));
            box.Add(SolutionToggle("Families", bt, families: true));
```

- [ ] **Step 2: Add the `SolutionToggle` helper**

Add next to `FormatToggle` in `SetupScreen.cs`:

```csharp
        // One CAS mediation solution as an independent checkbox. Initial state is the best-effort
        // installed-read; on a failed apply the value reverts so the box reflects on-disk reality.
        private static Toggle SolutionToggle(string label, UnityEditor.BuildTarget platform, bool families)
        {
            var t = new Toggle(label) { value = CasMediation.IsSolutionInstalled(platform, families) };
            t.AddToClassList("cas-cfg__toggle");
            t.RegisterValueChangedCallback(e =>
            {
                if (!CasMediation.SetSolution(platform, families, e.newValue))
                    t.SetValueWithoutNotify(!e.newValue);
            });
            return t;
        }
```

- [ ] **Step 3: Remove the now-unused `SetSegActive`**

`SetSegActive` in `SetupScreen.cs` has no remaining callers. Delete it:

```csharp
        private static void SetSegActive(Button seg, bool active)
        {
            if (active) seg.AddToClassList("cas-seg__item--active");
            else        seg.RemoveFromClassList("cas-seg__item--active");
        }
```

- [ ] **Step 4: Remove the mutex API from `CasMediation`**

In `Editor/Wizard/CasMediation.cs`, delete the now-unused `SelectSolution(BuildTarget, bool)` method (the whole `public static bool SelectSolution(...)` body, including its XML-doc comment) and the `IsFamiliesActive(BuildTarget)` method (the whole `public static bool IsFamiliesActive(...)` body, including its XML-doc comment). Keep `SetSolution`, `IsSolutionInstalled`, `FindType`, `GetValue`, `RefreshInspector`, `Warn`, and the constants.

- [ ] **Step 5: Verify compilation + behaviour in Unity**

Let Unity recompile — expected: no errors, no warnings about missing `SelectSolution`/`IsFamiliesActive`/`SetSegActive`. Then in Configuration:
1. Open CAS Android Settings and leave the window open.
2. Tick `Families` → `Assets/CleverAdsSolutions/Editor/CASAndroidFamiliesAdsDependencies.xml` appears and CAS's "Mediation Solutions → FamiliesAds" checkbox turns on **live**. Untick → file removed, checkbox clears.
3. Confirm `Optimal` and `Families` toggle independently (both can be on at once).

- [ ] **Step 6: Commit**

```bash
git add Editor/Wizard/Screens/SetupScreen.cs Editor/Wizard/CasMediation.cs
git commit -m "feat(installer): independent Optimal/Families network checkboxes (drop mutex)"
```

---

## Self-Review

**Spec coverage:**
- Part 1 (active-platform scope, whole screen) → Tasks 3 + 4 (header, status column, CAS panel, attention count). ✓
- Part 2 (independent checkboxes) → Task 5. ✓
- Part 3 (`CasMediation` refactor: add `SetSolution`/`IsSolutionInstalled`, remove `SelectSolution`/`IsFamiliesActive`, keep `RefreshInspector`) → Tasks 2 (add) + 5 step 4 (remove). ✓
- `PickCells`/`PickForPlatform` unit test → Task 1. ✓
- USS unchanged → confirmed (no task touches `.uss`). ✓

**Placeholder scan:** No TBD/TODO; every code step has full code. ✓

**Type consistency:** `PickForPlatform<T>(T android, T ios, string platform)` defined Task 1, used Tasks 3-step n/a, 4. `SetSolution(BuildTarget, bool families, bool enable)` and `IsSolutionInstalled(BuildTarget, bool families)` defined Task 2, used Task 5. `BuildRow(row, alt, platform)` and `BuildCasConfig(platform)` signatures updated consistently within Task 4. `setup-th-plat` produced Task 3, consumed Task 4. ✓

**Compile-green ordering:** Task 2 is additive (old API kept); the old API is removed in Task 5 step 4 only after its sole caller is rewritten in Task 5 step 1. Every commit compiles. ✓
