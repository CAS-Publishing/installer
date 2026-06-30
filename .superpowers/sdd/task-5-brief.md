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
