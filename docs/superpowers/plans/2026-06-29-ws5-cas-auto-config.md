# WS-5 — CAS auto-config — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Let the user set CAS ad formats, audience, and network set (OptimalAds/FamiliesAds) from the installer's Configuration CAS row, writing to the per-platform CAS settings asset (creating it if missing) and activating the chosen CAS mediation solution.

**Architecture:** Pure bit/audience helpers (tested) + a `CasSettingsWriter` (asset read/write/create, reusing the `CasIdApplier` SerializedObject pattern) + a `CasMediation` reflection wrapper over CAS's `DependencyManager` (best-effort, graceful fallback) + Configuration-row UI, gated by a catalog `adFormats` flag.

**Tech Stack:** Unity 2022.3 Editor, C# UPM editor package, UI Toolkit, SerializedObject asset writes, reflection, JSON catalog.

**Spec:** `docs/superpowers/specs/2026-06-29-ws5-cas-auto-config-design.md`.

## Global Constraints

- CAS settings asset: `Assets/CleverAdsSolutions/Resources/CASSettings<Platform>.asset`; fields `allowedAdFlags` (int bitmask) and `audienceTagged` (int). `AdFlags`: Banner=1, Interstitial=2, Rewarded=4, AppOpen=8. `Audience`: Mixed=0, Children=1, NotChildren=2.
- Network set: Families → audience Children(1) + activate solution `"FamiliesAds"`; Optimal → audience NotChildren(2) + activate `"OptimalAds"`.
- The reflection (`CasMediation`) is best-effort: on any missing CAS type/member it logs a warning and returns false — asset-field writes still apply. NEVER throw on a CAS API change.
- Controls render only on the CAS row, gated by the catalog `adFormats` flag; Configuration already shows only installed components.
- No CLI/headless runner: asset writes, reflection, and UI are OWNER-RUN. Pure helpers (`AdFlagsBits`, `CasAudience`) are unit-tested. Every new `.cs` gets a committed `.meta` (32-hex GUID).
- Conventional Commits, `feat(installer):` / `chore(metadata):`. Installer branch `feat/installer-wizard-ui`; metadata branch `chore/cas-pin-4.7.4`.

---

### Task 1: `AdFlagsBits` (pure bit helpers)

**Files:**
- Create: `Editor/Wizard/AdFlagsBits.cs`
- Create: `Editor/Wizard/AdFlagsBits.cs.meta` (guid `b2f4a1c08d3e4a5e9c1b6d7e8f0a2c34`)
- Test: `Editor/Tests/AdFlagsBitsTests.cs` (+ `.meta` guid `c3a5b2d19e4f5b6f0d2c7e8f9a1b3d45`)

**Interfaces:**
- Produces: `int AdFlagsBits.{Banner,Interstitial,Rewarded,AppOpen}` (1,2,4,8); `bool AdFlagsBits.HasFlag(int mask, int flag)`; `int AdFlagsBits.WithFlag(int mask, int flag, bool on)`.

- [ ] **Step 1: Write the failing test**

Create `Editor/Tests/AdFlagsBitsTests.cs`:

```csharp
using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class AdFlagsBitsTests
    {
        [Test] public void Values() { Assert.AreEqual(1, AdFlagsBits.Banner); Assert.AreEqual(2, AdFlagsBits.Interstitial); Assert.AreEqual(4, AdFlagsBits.Rewarded); Assert.AreEqual(8, AdFlagsBits.AppOpen); }
        [Test] public void HasFlag_true() => Assert.IsTrue(AdFlagsBits.HasFlag(AdFlagsBits.Banner | AdFlagsBits.Rewarded, AdFlagsBits.Rewarded));
        [Test] public void HasFlag_false() => Assert.IsFalse(AdFlagsBits.HasFlag(AdFlagsBits.Banner, AdFlagsBits.Rewarded));
        [Test] public void WithFlag_set() => Assert.AreEqual(AdFlagsBits.Banner | AdFlagsBits.Rewarded, AdFlagsBits.WithFlag(AdFlagsBits.Banner, AdFlagsBits.Rewarded, true));
        [Test] public void WithFlag_clear() => Assert.AreEqual(AdFlagsBits.Banner, AdFlagsBits.WithFlag(AdFlagsBits.Banner | AdFlagsBits.Rewarded, AdFlagsBits.Rewarded, false));
        [Test] public void WithFlag_clear_absent_noop() => Assert.AreEqual(AdFlagsBits.Banner, AdFlagsBits.WithFlag(AdFlagsBits.Banner, AdFlagsBits.Rewarded, false));
    }
}
```

- [ ] **Step 2: Run to verify it fails** — Unity Test Runner → EditMode → `AdFlagsBitsTests`. Expected: FAIL (no `AdFlagsBits`).

- [ ] **Step 3: Implement**

Create `Editor/Wizard/AdFlagsBits.cs`:

```csharp
namespace PSV.Installer.Wizard
{
    /// <summary>Pure bit helpers for the CAS <c>allowedAdFlags</c> bitmask. Values mirror CAS's
    /// <c>AdFlags</c> enum: Banner=1, Interstitial=2, Rewarded=4, AppOpen=8.</summary>
    internal static class AdFlagsBits
    {
        public const int Banner = 1;
        public const int Interstitial = 2;
        public const int Rewarded = 4;
        public const int AppOpen = 8;

        public static bool HasFlag(int mask, int flag) => (mask & flag) == flag && flag != 0;

        public static int WithFlag(int mask, int flag, bool on) => on ? (mask | flag) : (mask & ~flag);
    }
}
```

Create `Editor/Wizard/AdFlagsBits.cs.meta`:
```
fileFormatVersion: 2
guid: b2f4a1c08d3e4a5e9c1b6d7e8f0a2c34
```
Create `Editor/Tests/AdFlagsBitsTests.cs.meta`:
```
fileFormatVersion: 2
guid: c3a5b2d19e4f5b6f0d2c7e8f9a1b3d45
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

- [ ] **Step 4: Run to verify it passes** — Expected: PASS (6/6).

- [ ] **Step 5: Commit**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer"
git add Editor/Wizard/AdFlagsBits.cs Editor/Wizard/AdFlagsBits.cs.meta Editor/Tests/AdFlagsBitsTests.cs Editor/Tests/AdFlagsBitsTests.cs.meta
git commit -m "feat(installer): AdFlagsBits pure helpers for CAS allowedAdFlags (#4.5)"
```

---

### Task 2: `CasAudience` (pure network↔audience mapping)

**Files:**
- Create: `Editor/Wizard/CasAudience.cs` (+ `.meta` guid `d4b6c3e2af506c7f1e3d8a9b0c2d4e56`)
- Test: `Editor/Tests/CasAudienceTests.cs` (+ `.meta` guid `e5c7d4f3b0617d8a2f4e9b0c1d3e5f67`)

**Interfaces:**
- Produces: `int CasAudience.Mixed=0, Children=1, NotChildren=2`; `int CasAudience.ForFamilies(bool families)` → families?Children:NotChildren; `bool CasAudience.IsFamilies(int audience)` → audience==Children.

- [ ] **Step 1: Write the failing test**

Create `Editor/Tests/CasAudienceTests.cs`:

```csharp
using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class CasAudienceTests
    {
        [Test] public void Families_is_children() => Assert.AreEqual(CasAudience.Children, CasAudience.ForFamilies(true));
        [Test] public void Optimal_is_notchildren() => Assert.AreEqual(CasAudience.NotChildren, CasAudience.ForFamilies(false));
        [Test] public void IsFamilies_children_true() => Assert.IsTrue(CasAudience.IsFamilies(CasAudience.Children));
        [Test] public void IsFamilies_notchildren_false() => Assert.IsFalse(CasAudience.IsFamilies(CasAudience.NotChildren));
        [Test] public void IsFamilies_mixed_false() => Assert.IsFalse(CasAudience.IsFamilies(CasAudience.Mixed));
    }
}
```

- [ ] **Step 2: Run to verify it fails** — Expected: FAIL (no `CasAudience`).

- [ ] **Step 3: Implement**

Create `Editor/Wizard/CasAudience.cs`:

```csharp
namespace PSV.Installer.Wizard
{
    /// <summary>Pure mapping between the installer's Optimal/Families choice and CAS's
    /// <c>audienceTagged</c> int (enum Audience: Mixed=0, Children=1, NotChildren=2).</summary>
    internal static class CasAudience
    {
        public const int Mixed = 0;
        public const int Children = 1;
        public const int NotChildren = 2;

        /// <summary>Families → Children, Optimal → NotChildren.</summary>
        public static int ForFamilies(bool families) => families ? Children : NotChildren;

        /// <summary>True when the stored audience is the Families (children) value.</summary>
        public static bool IsFamilies(int audience) => audience == Children;
    }
}
```

Create the two `.meta` files: `CasAudience.cs.meta` (2-line, guid `d4b6c3e2af506c7f1e3d8a9b0c2d4e56`) and `CasAudienceTests.cs.meta` (full MonoImporter block as in Task 1, guid `e5c7d4f3b0617d8a2f4e9b0c1d3e5f67`).

- [ ] **Step 4: Run to verify it passes** — Expected: PASS (5/5).

- [ ] **Step 5: Commit**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer"
git add Editor/Wizard/CasAudience.cs Editor/Wizard/CasAudience.cs.meta Editor/Tests/CasAudienceTests.cs Editor/Tests/CasAudienceTests.cs.meta
git commit -m "feat(installer): CasAudience pure mapping (Optimal/Families ↔ audienceTagged) (#4.5)"
```

---

### Task 3: `CasSettingsWriter` (asset read/write/create)

**Files:**
- Create: `Editor/Wizard/CasSettingsWriter.cs` (+ `.meta` guid `f6d8e5a4c1728e9b3a5f0c1d2e4f6a78`)

**Interfaces:**
- Produces: `int CasSettingsWriter.ReadAdFlags(string platform)`, `int ReadAudience(string platform)`, `void SetAdFlags(string platform, int flags)`, `void SetAudience(string platform, int audience)`, `UnityEngine.Object EnsureAsset(string platform)`.
- Consumes: CAS `config` from the catalog (the `settingsAssetField` row's `assetPath`/`assetType`); `SetupChecker.LocateAsset`.

- [ ] **Step 1: Implement**

Create `Editor/Wizard/CasSettingsWriter.cs`:

```csharp
using System.IO;
using PSV.Installer.Catalog;
using UnityEditor;
using UnityEngine;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// Reads/writes the CAS per-platform settings asset fields the installer manages
    /// (<c>allowedAdFlags</c>, <c>audienceTagged</c>) and creates the asset when missing. Reuses the
    /// catalog CAS config (assetPath/assetType) and the SerializedObject pattern from CasIdApplier.
    /// </summary>
    internal static class CasSettingsWriter
    {
        private const string CasId = "com.cleversolutions.ads.unity";
        // Fallback CAS settings script guid (CAS 4.7.x) — used only when no sibling asset exists to copy it from.
        private const string FallbackScriptGuid = "cd2f38c563828458c8e900006c010cd2";

        private static ConfigRequirement FindCasField(string platform)
        {
            var load = CatalogLoader.Load();
            if (load.Status != CatalogLoadStatus.Ok || load.Catalog?.External == null) return null;
            foreach (var e in load.Catalog.External)
            {
                if (e == null || e.Id != CasId || e.Config == null) continue;
                foreach (var req in e.Config)
                    if (req != null && req.Kind == "settingsAssetField" &&
                        string.Equals(req.Platform, platform, System.StringComparison.OrdinalIgnoreCase))
                        return req;
            }
            return null;
        }

        public static int ReadAdFlags(string platform) => ReadInt(platform, "allowedAdFlags");
        public static int ReadAudience(string platform) => ReadInt(platform, "audienceTagged");

        private static int ReadInt(string platform, string field)
        {
            var req = FindCasField(platform);
            if (req == null) return 0;
            var asset = SetupChecker.LocateAsset(req.AssetPath, req.AssetType);
            if (asset == null) return 0;
            var prop = new SerializedObject(asset).FindProperty(field);
            return prop != null && prop.propertyType == SerializedPropertyType.Integer ? prop.intValue : 0;
        }

        public static void SetAdFlags(string platform, int flags) => WriteInt(platform, "allowedAdFlags", flags);
        public static void SetAudience(string platform, int audience) => WriteInt(platform, "audienceTagged", audience);

        private static void WriteInt(string platform, string field, int value)
        {
            var req = FindCasField(platform);
            if (req == null) return;
            var asset = EnsureAssetAt(req.AssetPath);
            if (asset == null) return;
            var so = new SerializedObject(asset);
            var prop = so.FindProperty(field);
            if (prop == null || prop.propertyType != SerializedPropertyType.Integer) return;
            prop.intValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();
        }

        public static Object EnsureAsset(string platform)
        {
            var req = FindCasField(platform);
            return req == null ? null : EnsureAssetAt(req.AssetPath);
        }

        // Loads the asset at the catalog path, creating it from the CAS settings template when absent.
        private static Object EnsureAssetAt(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            var existing = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (existing != null) return existing;

            var scriptGuid = SiblingScriptGuid(assetPath) ?? FallbackScriptGuid;
            var name = Path.GetFileNameWithoutExtension(assetPath);
            var yaml =
                "%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!114 &11400000\nMonoBehaviour:\n" +
                "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n" +
                "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: {fileID: 0}\n" +
                "  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: " + scriptGuid + ", type: 3}\n" +
                "  m_Name: " + name + "\n  m_EditorClassIdentifier:\n  testAdMode: 0\n  managerIds:\n  - demo\n" +
                "  allowedAdFlags: 0\n  audienceTagged: 0\n  bannerSize: 0\n  bannerRefresh: 30\n" +
                "  interstitialInterval: 30\n  loadingMode: 2\n  debugMode: 0\n  trackLocationEnabled: 0\n" +
                "  interWhenNoRewardedAd: 1\n";

            var dir = Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(assetPath, yaml);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<Object>(assetPath);
        }

        // The CAS settings script guid is shared across platforms — reuse a sibling asset's guid when present.
        private static string SiblingScriptGuid(string assetPath)
        {
            var dir = Path.GetDirectoryName(assetPath);
            if (string.IsNullOrEmpty(dir)) return null;
            foreach (var guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { dir }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (p == assetPath) continue;
                var name = Path.GetFileNameWithoutExtension(p);
                if (name != null && name.StartsWith("CASSettings"))
                {
                    var metaText = File.Exists(p + ".meta") ? File.ReadAllText(p) : null;
                    // Read the m_Script guid out of the asset YAML itself.
                    if (metaText != null)
                    {
                        var marker = "guid: ";
                        var idx = metaText.IndexOf("m_Script:", System.StringComparison.Ordinal);
                        if (idx >= 0)
                        {
                            var g = metaText.IndexOf(marker, idx, System.StringComparison.Ordinal);
                            if (g >= 0)
                            {
                                g += marker.Length;
                                var end = metaText.IndexOf(',', g);
                                if (end > g) return metaText.Substring(g, end - g).Trim();
                            }
                        }
                    }
                }
            }
            return null;
        }
    }
}
```

- [ ] **Step 2: Create the `.meta`**

`Editor/Wizard/CasSettingsWriter.cs.meta`:
```
fileFormatVersion: 2
guid: f6d8e5a4c1728e9b3a5f0c1d2e4f6a78
```

- [ ] **Step 3: Owner-run verification**

In Unity with CAS installed: `CasSettingsWriter.SetAdFlags("Android", 5)` writes `allowedAdFlags: 5`; `SetAudience("iOS", 1)` creates `CASSettingsiOS.asset` (if missing) with `audienceTagged: 1`; reads return the written values.

- [ ] **Step 4: Commit**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer"
git add Editor/Wizard/CasSettingsWriter.cs Editor/Wizard/CasSettingsWriter.cs.meta
git commit -m "feat(installer): CasSettingsWriter (allowedAdFlags/audienceTagged + asset create) (#4.5)"
```

---

### Task 4: `CasMediation` (reflection over DependencyManager)

**Files:**
- Create: `Editor/Wizard/CasMediation.cs` (+ `.meta` guid `a7e9f6b5d2839f0c4b6a1d2e3f5a7b89`)

**Interfaces:**
- Produces: `bool CasMediation.SelectSolution(UnityEditor.BuildTarget platform, bool families)` — best-effort; true on success, false (logged) when the CAS API isn't found.

- [ ] **Step 1: Implement**

Create `Editor/Wizard/CasMediation.cs`:

```csharp
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// Activates a CAS mediation solution (OptimalAds / FamiliesAds) via reflection into CAS's editor
    /// <c>DependencyManager</c>. Best-effort: any missing CAS type/member logs a warning and returns
    /// false, so a CAS API change never throws — the installer's asset-field writes still apply.
    /// The reflected members (Create / solutions / Dependency.id / ActivateDependencies) must be
    /// confirmed in Unity by the owner; this code never assumes they exist.
    /// </summary>
    internal static class CasMediation
    {
        private const string OptimalId = "OptimalAds";
        private const string FamiliesId = "FamiliesAds";

        public static bool SelectSolution(BuildTarget platform, bool families)
        {
            try
            {
                var dmType = FindType("DependencyManager");
                if (dmType == null) return Warn("DependencyManager type not found");

                // CAS Audience enum: Mixed=0, Children=1, NotChildren=2.
                var audienceType = FindType("Audience");
                if (audienceType == null) return Warn("Audience type not found");
                var audienceVal = Enum.ToObject(audienceType, families ? 1 : 2);

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

                var wantedId = families ? FamiliesId : OptimalId;
                foreach (var sol in solutions)
                {
                    if (sol == null) continue;
                    var idMember = (MemberInfo)sol.GetType().GetField("id") ?? sol.GetType().GetProperty("id");
                    var id = GetValue(idMember, sol) as string;
                    if (id != wantedId) continue;

                    var activate = sol.GetType().GetMethod("ActivateDependencies");
                    if (activate == null) return Warn("Dependency.ActivateDependencies not found");
                    activate.Invoke(sol, new[] { (object)platform, manager });
                    AssetDatabase.SaveAssets();
                    return true;
                }
                return Warn($"solution '{wantedId}' not present");
            }
            catch (Exception e)
            {
                return Warn("reflection error: " + e.Message);
            }
        }

        private static Type FindType(string simpleName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[] types;
                try { types = asm.GetTypes(); } catch { continue; }
                foreach (var t in types)
                    if (t.Name == simpleName && t.Namespace != null && t.Namespace.StartsWith("CAS"))
                        return t;
            }
            return null;
        }

        private static object GetValue(MemberInfo m, object target) =>
            m is FieldInfo f ? f.GetValue(target) : (m as PropertyInfo)?.GetValue(target);

        private static bool Warn(string why)
        {
            Debug.LogWarning("[PSV Installer] CAS network-set not applied (" + why +
                "). Ad formats/audience were still written; pick the network set in CAS settings if needed.");
            return false;
        }
    }
}
```

- [ ] **Step 2: Create the `.meta`**

`Editor/Wizard/CasMediation.cs.meta`:
```
fileFormatVersion: 2
guid: a7e9f6b5d2839f0c4b6a1d2e3f5a7b89
```

- [ ] **Step 3: Owner-run verification (critical — reflection)**

In Unity with CAS installed: `CasMediation.SelectSolution(BuildTarget.Android, true)` activates FamiliesAds (CAS settings shows FamiliesAds resolved); `false` activates OptimalAds. If a warning logs instead, capture the exact reflected-member name that wasn't found and adjust (`Create` signature / `solutions` / `id` / `ActivateDependencies` / the `CAS`-prefixed namespace).

- [ ] **Step 4: Commit**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer"
git add Editor/Wizard/CasMediation.cs Editor/Wizard/CasMediation.cs.meta
git commit -m "feat(installer): CasMediation reflection — activate OptimalAds/FamiliesAds (#4.5)"
```

---

### Task 5: Configuration CAS-row controls

**Files:**
- Modify: `Editor/Wizard/Screens/SetupScreen.cs` (render format toggles + audience/network on the CAS row)
- Modify: `Editor/Wizard/Uss/theme.uss` (control styling)

**Interfaces:**
- Consumes: `SetupModel.Row` (needs the component id — see Step 1), the catalog `adFormats` flag, `AdFlagsBits`, `CasAudience`, `CasSettingsWriter`, `CasMediation`.

- [ ] **Step 1: Carry the component id + adFormats onto the row**

In `Editor/Wizard/SetupModel.cs`, add to `Row`: `public string Id;` and `public bool AdFormats;`. In `TryBuild`, set `row.Id = id;` and, when building the row, set `row.AdFormats` from the CAS config: after the `configById.TryGetValue(id, out var reqs)` block, add:

```csharp
                if (reqs != null)
                    foreach (var req in reqs)
                        if (req != null && req.AdFormats) { row.AdFormats = true; break; }
```

(`ConfigRequirement.AdFormats` is added in Task 6's catalog model change — add the field now in `Catalog.cs`: `[JsonProperty("adFormats")] public bool AdFormats;` after `Editable`.)

- [ ] **Step 2: Render the controls in `SetupScreen.BuildRow`**

In `Editor/Wizard/Screens/SetupScreen.cs`, in `BuildRow`, after `el.Add(BuildPlatformColumn(row.IOS));`, append a CAS controls block when `row.AdFormats`:

```csharp
            if (row.AdFormats)
                el.Add(BuildCasControls(row));
```

Add this method to `SetupScreen`:

```csharp
        // CAS-only: ad-format toggles + audience/network choice, per platform, writing to the settings asset.
        private static VisualElement BuildCasControls(SetupModel.Row row)
        {
            var wrap = new VisualElement();
            wrap.AddToClassList("cas-cas-controls");
            wrap.Add(PlatformControls("Android"));
            wrap.Add(PlatformControls("iOS"));
            return wrap;
        }

        private static VisualElement PlatformControls(string platform)
        {
            var box = new VisualElement();
            box.AddToClassList("cas-cas-controls__plat");
            box.Add(new Label(platform) { });

            var flags = CasSettingsWriter.ReadAdFlags(platform);
            box.Add(FormatToggle(platform, "Banner", AdFlagsBits.Banner, flags));
            box.Add(FormatToggle(platform, "Interstitial", AdFlagsBits.Interstitial, flags));
            box.Add(FormatToggle(platform, "Rewarded", AdFlagsBits.Rewarded, flags));
            box.Add(FormatToggle(platform, "AppOpen", AdFlagsBits.AppOpen, flags));

            var families = CasAudience.IsFamilies(CasSettingsWriter.ReadAudience(platform));
            var net = new Toggle("Families (children) ads") { value = families };
            net.RegisterValueChangedCallback(e =>
            {
                CasSettingsWriter.SetAudience(platform, CasAudience.ForFamilies(e.newValue));
                CasMediation.SelectSolution(
                    platform == "iOS" ? UnityEditor.BuildTarget.iOS : UnityEditor.BuildTarget.Android, e.newValue);
            });
            box.Add(net);
            return box;
        }

        private static Toggle FormatToggle(string platform, string label, int flag, int currentMask)
        {
            var t = new Toggle(label) { value = AdFlagsBits.HasFlag(currentMask, flag) };
            t.RegisterValueChangedCallback(e =>
                CasSettingsWriter.SetAdFlags(platform, AdFlagsBits.WithFlag(CasSettingsWriter.ReadAdFlags(platform), flag, e.newValue)));
            return t;
        }
```

- [ ] **Step 3: USS**

In `theme.uss`, add:

```css
.cas-cas-controls { flex-direction: row; margin-top: 6px; }
.cas-cas-controls__plat { flex-grow: 1; margin-right: 10px; }
```

- [ ] **Step 4: Owner-run visual check**

CAS row in Configuration shows, per platform, four format toggles + a "Families (children) ads" toggle, seeded from the asset; toggling a format writes `allowedAdFlags`; toggling Families writes `audienceTagged` and activates the FamiliesAds/OptimalAds solution (or logs the best-effort warning).

- [ ] **Step 5: Commit**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer"
git add Editor/Wizard/SetupModel.cs Editor/Catalog/Catalog.cs Editor/Wizard/Screens/SetupScreen.cs Editor/Wizard/Uss/theme.uss
git commit -m "feat(installer): CAS ad-format + audience/network controls in Configuration (#4.5)"
```

---

### Task 6: Catalog `adFormats` flag + version bumps

**Files:**
- Modify: metadata `catalog.json` (CAS config rows), `package.json`, `CHANGELOG.md`
- Modify: installer `package.json`, `CHANGELOG.md`

- [ ] **Step 1: Add `"adFormats": true` to both CAS config rows**

In `catalog.json`, on each CAS `config` row (which already carry `regex`/`hint`/`editable`), add `"adFormats": true` after `"editable": true`.

- [ ] **Step 2: Verify JSON**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer.metadata"
python -c "import json;d=json.load(open('catalog.json'));cfg=[e for e in d['external'] if e['id']=='com.cleversolutions.ads.unity'][0]['config'];[print(c['platform'],c.get('adFormats')) for c in cfg]"
```
Expected: `Android True` / `iOS True`.

- [ ] **Step 3: Bump metadata (`0.0.2-preview.21` → `0.0.2-preview.22`) + changelog**, commit `chore(metadata): mark CAS config adFormats (#4.5)` (on branch `chore/cas-pin-4.7.4`).

- [ ] **Step 4: Bump installer (`0.0.1-preview.29` → `0.0.1-preview.30`) + changelog** ("Configuration: CAS ad-format toggles + audience/network set with auto asset-create (#4.5)"), commit `chore(installer): release notes for CAS auto-config (preview.30)`.

---

## Self-Review

- **Spec coverage:** formats → Task 1 (`AdFlagsBits`) + Task 3 (`SetAdFlags`) + Task 5 (toggles); audience/network → Task 2 (`CasAudience`) + Task 3 (`SetAudience`) + Task 4 (`CasMediation`) + Task 5 (control); asset-create → Task 3 (`EnsureAsset`); data-driven gating → Task 5/6 (`adFormats`); graceful reflection fallback → Task 4.
- **Placeholder scan:** none — pure helpers carry full code+tests; Unity-only writers/reflection are explicitly owner-run with documented contracts.
- **Type consistency:** `AdFlagsBits.{Banner,Interstitial,Rewarded,AppOpen,HasFlag,WithFlag}`, `CasAudience.{Mixed,Children,NotChildren,ForFamilies,IsFamilies}`, `CasSettingsWriter.{ReadAdFlags,ReadAudience,SetAdFlags,SetAudience,EnsureAsset}`, `CasMediation.SelectSolution(BuildTarget,bool)`, `ConfigRequirement.AdFormats`, `SetupModel.Row.{Id,AdFormats}` — consistent across tasks.
