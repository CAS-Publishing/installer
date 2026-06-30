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

