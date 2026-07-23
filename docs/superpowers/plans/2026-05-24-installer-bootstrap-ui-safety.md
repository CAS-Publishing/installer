# Installer Infra Hardening — Bootstrap + UI Safety (Plan 3 of 3)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** Close the last infra cluster: the editor-load bootstrap stops breaking CI / stealing focus / spamming the console / looping on a bad catalog; the scanner stops silently reporting "nothing installed" on a broken manifest; and the window gives trustworthy feedback (linked split checkboxes, a result banner that survives the domain reload, a stale-report warning, a friendly "all up to date" state).

**Architecture:** Three phases. **A — Bootstrap:** guard `Application.isBatchMode`/play-mode; make `CatalogLoader.Load` return a typed result (`Ok`/`NotInstalled`/`Unreadable`) with a `schemaVersion` upper-bound so a malformed/too-new catalog is surfaced (not reinstalled in a loop); poll `Client.Add` and gate network work behind a per-session `SessionState` flag. **B — Scanner read:** `ManifestProbe` reads through Plan-1 `ManifestIO`; a broken manifest becomes a typed `ScanReport.ManifestError` the UI shows, not a silent-empty report. **C — UI:** linked split-group checkboxes (all-or-none), a `[SerializeField]` apply-result banner surviving the reload, a stale-report indicator, and a friendly empty state.

**Tech Stack:** Unity 2022.3, C#, Newtonsoft.Json, Unity Test Framework. Builds on Plan-1 `ManifestIO`/`SemVer` and Plan-2 helpers (all on `main`).

**Decisions locked:** keep direct manifest write (Apply stays synchronous — the fix is a *persistent banner*, not a `Client.Add` rewrite); `SupportedSchemaVersion = 1`; non-git clients already blocked by Plan-2 `GitGuard` (unchanged here).

**Testability note:** much of this is Unity-bound (`Application.isBatchMode`, `SessionState`, `Client.Add`, `EditorWindow`/IMGUI). Those tasks are **implemented with exact code + verified in Unity** (no automated test possible). The pure, extractable pieces — `CatalogLoader.ParseAndValidate`, `ManifestProbe.ReadFrom` mapping, `SplitSelection.SetGroup`, the "has-actionable" predicate — get EditMode unit tests.

---

## File structure

| File | Change |
|---|---|
| `Editor/Catalog/CatalogLoader.cs` | `Load()` → typed `CatalogLoadResult`; pure `ParseAndValidate` + `SupportedSchemaVersion`. |
| `Editor/Catalog/CatalogUpdater.cs` | Add `TrackInstall(AddRequest, label)` polling helper. |
| `Editor/Bootstrap.cs` | batchmode guard; consume typed load result; session-gated, polled auto-update. |
| `Editor/MetadataAutoInstall.cs` | per-session guard; poll the install request. |
| `Editor/Ui/InstallerWindow.cs` | consume typed load result; play-mode popup guard; result banner; stale indicator. |
| `Editor/Scanner/ManifestProbe.cs` | read via `ManifestIO`; `Readable`/`ReadError`; `ReadFrom(path)` + `FromJObject`. |
| `Editor/Scanner/ScanReport.cs` | add `string ManifestError`. |
| `Editor/Scanner/Scanner.cs` | propagate manifest error into the report. |
| `Editor/Ui/InstallerWindowReportView.cs` | manifest-error banner; linked split checkboxes; friendly empty state. |
| `Editor/Common/SplitSelection.cs` | **New.** Pure all-or-none group toggle. |
| `Editor/Tests/ScanReportFactory.cs` | update ctor call for the new `ManifestError` param. |
| `Editor/Tests/CatalogLoaderTests.cs` | **New** (ParseAndValidate). |
| `Editor/Tests/ManifestProbeTests.cs` | **New** (ReadFrom mapping). |
| `Editor/Tests/SplitSelectionTests.cs` | **New**. |
| `Editor/Tests/ReportViewLogicTests.cs` | **New** (has-actionable predicate). |

> **.meta / test-run:** same as Plans 1-2 — implementers commit source only; human focuses Unity (generates `.meta`, compiles) and runs `Test Runner → EditMode → Run All`; then `.meta` is committed.

---

# PHASE A — Bootstrap safety

## Task A1: Typed `CatalogLoader.Load` + schema validation (pure `ParseAndValidate` + tests)

**Files:** Modify `Editor/Catalog/CatalogLoader.cs`; Test `Editor/Tests/CatalogLoaderTests.cs`.

- [ ] **Step 1: Write the failing tests**

`Editor/Tests/CatalogLoaderTests.cs`:

```csharp
using NUnit.Framework;
using PSV.Installer.Catalog;

namespace PSV.Installer.Tests
{
    public sealed class CatalogLoaderTests
    {
        [Test]
        public void Valid_schema1_parses_ok()
        {
            var json = "{ \"schemaVersion\": 1, \"catalogVersion\": \"1.0.0\", \"packages\": [] }";
            var status = CatalogLoader.ParseAndValidate(json, out var cat, out _);
            Assert.AreEqual(CatalogParseStatus.Ok, status);
            Assert.IsNotNull(cat);
            Assert.AreEqual("1.0.0", cat.CatalogVersion);
        }

        [Test]
        public void Missing_schema_defaults_to_zero_and_is_ok()
        {
            var status = CatalogLoader.ParseAndValidate("{ \"catalogVersion\": \"1\" }", out var cat, out _);
            Assert.AreEqual(CatalogParseStatus.Ok, status);
            Assert.IsNotNull(cat);
        }

        [Test]
        public void Malformed_json_is_malformed()
        {
            var status = CatalogLoader.ParseAndValidate("{ not json", out var cat, out var err);
            Assert.AreEqual(CatalogParseStatus.Malformed, status);
            Assert.IsNull(cat);
            Assert.IsNotEmpty(err);
        }

        [Test]
        public void Empty_string_is_malformed()
        {
            Assert.AreEqual(CatalogParseStatus.Malformed, CatalogLoader.ParseAndValidate("   ", out _, out _));
        }

        [Test]
        public void Future_schema_is_unsupported()
        {
            var json = "{ \"schemaVersion\": 999, \"catalogVersion\": \"9.9.9\" }";
            var status = CatalogLoader.ParseAndValidate(json, out var cat, out var err);
            Assert.AreEqual(CatalogParseStatus.UnsupportedSchema, status);
            Assert.IsNull(cat);
            Assert.IsNotEmpty(err);
        }
    }
}
```

- [ ] **Step 2: Run to verify failure** — compile error: `ParseAndValidate`/`CatalogParseStatus` undefined.

- [ ] **Step 3: Implement** — replace the entire `Editor/Catalog/CatalogLoader.cs` with:

```csharp
using System;
using System.IO;
using Newtonsoft.Json;
using UnityEditor.PackageManager;
using UnityEngine;

namespace PSV.Installer.Catalog
{
    /// <summary>Outcome of locating + reading the metadata catalog.</summary>
    public enum CatalogLoadStatus
    {
        /// <summary>Catalog found, parsed, and schema-compatible.</summary>
        Ok,
        /// <summary>Metadata package is not registered in this project (first run → install).</summary>
        NotInstalled,
        /// <summary>Metadata package IS present but catalog.json is missing/malformed/too-new.
        /// Must NOT trigger a reinstall — that would loop forever.</summary>
        Unreadable,
    }

    /// <summary>Result of <see cref="CatalogLoader.Load"/>.</summary>
    public readonly struct CatalogLoadResult
    {
        public CatalogLoadStatus Status { get; }
        public PackageCatalog Catalog { get; }
        public string Source { get; }
        public string Error { get; }

        private CatalogLoadResult(CatalogLoadStatus s, PackageCatalog c, string src, string err)
        { Status = s; Catalog = c; Source = src; Error = err; }

        public static CatalogLoadResult Ok(PackageCatalog c, string src) => new CatalogLoadResult(CatalogLoadStatus.Ok, c, src, null);
        public static CatalogLoadResult NotInstalled() => new CatalogLoadResult(CatalogLoadStatus.NotInstalled, null, null, null);
        public static CatalogLoadResult Unreadable(string src, string err) => new CatalogLoadResult(CatalogLoadStatus.Unreadable, null, src, err);
    }

    /// <summary>Outcome of parsing+validating raw catalog JSON (pure).</summary>
    public enum CatalogParseStatus { Ok, Malformed, UnsupportedSchema }

    internal static class CatalogLoader
    {
        public const string MetadataPackageName = "com.psvgamestudio.installer.metadata";
        public const string CatalogFileName = "catalog.json";

        /// <summary>Highest catalog schemaVersion this installer build understands.
        /// A catalog declaring a higher number is surfaced as Unreadable rather than parsed
        /// against assumptions that may no longer hold.</summary>
        public const int SupportedSchemaVersion = 1;

        /// <summary>
        /// Locates the metadata package, reads catalog.json, and classifies the outcome.
        /// Never throws. NotInstalled → caller may install; Unreadable → caller must surface
        /// the error and NOT reinstall; Ok → use <see cref="CatalogLoadResult.Catalog"/>.
        /// </summary>
        public static CatalogLoadResult Load()
        {
            foreach (var pkg in PackageInfo.GetAllRegisteredPackages())
            {
                if (pkg.name != MetadataPackageName) continue;

                var path = Path.Combine(pkg.resolvedPath, CatalogFileName);
                if (!File.Exists(path))
                    return CatalogLoadResult.Unreadable(path, $"{CatalogFileName} missing at {pkg.resolvedPath}");

                string json;
                try { json = File.ReadAllText(path); }
                catch (Exception e) { return CatalogLoadResult.Unreadable(path, $"read failed: {e.Message}"); }

                var status = ParseAndValidate(json, out var cat, out var err);
                return status == CatalogParseStatus.Ok
                    ? CatalogLoadResult.Ok(cat, path)
                    : CatalogLoadResult.Unreadable(path, err);
            }

            return CatalogLoadResult.NotInstalled();
        }

        /// <summary>
        /// Pure parse + schema-compatibility check. <paramref name="catalog"/> is non-null only
        /// when the return is <see cref="CatalogParseStatus.Ok"/>.
        /// </summary>
        public static CatalogParseStatus ParseAndValidate(string json, out PackageCatalog catalog, out string error)
        {
            catalog = null;
            error = null;

            if (string.IsNullOrWhiteSpace(json)) { error = "catalog file is empty"; return CatalogParseStatus.Malformed; }

            PackageCatalog parsed;
            try { parsed = JsonConvert.DeserializeObject<PackageCatalog>(json); }
            catch (JsonException e) { error = e.Message; return CatalogParseStatus.Malformed; }

            if (parsed == null) { error = "catalog deserialised to null"; return CatalogParseStatus.Malformed; }

            if (parsed.SchemaVersion > SupportedSchemaVersion)
            {
                error = $"catalog schemaVersion {parsed.SchemaVersion} is newer than supported ({SupportedSchemaVersion}); update the installer";
                return CatalogParseStatus.UnsupportedSchema;
            }

            catalog = parsed;
            return CatalogParseStatus.Ok;
        }
    }
}
```

- [ ] **Step 4: Run to verify pass** — `CatalogLoaderTests` all PASS.

- [ ] **Step 5: Commit**

```bash
git add Editor/Catalog/CatalogLoader.cs Editor/Tests/CatalogLoaderTests.cs
git commit -m "feat(installer): typed CatalogLoader.Load + schemaVersion upper-bound (no reinstall loop on broken/too-new catalog)"
```

---

## Task A2: Bootstrap consumes typed result + batchmode guard + session-gated polled auto-update

**Files:** Modify `Editor/Bootstrap.cs`, `Editor/Catalog/CatalogUpdater.cs`. (Unity-verified — no automated test.)

- [ ] **Step 1: Add the polling helper to `CatalogUpdater`**

In `Editor/Catalog/CatalogUpdater.cs`, add these `using`s if absent (`UnityEditor` for `EditorApplication`) and add this method to the class:

```csharp
        // Polls an AddRequest to completion on the editor update loop and logs the outcome,
        // so a failed/slow Client.Add surfaces instead of vanishing (fire-and-forget).
        public static void TrackInstall(AddRequest request, string label)
        {
            if (request == null) return;

            void Poll()
            {
                if (!request.IsCompleted) return;
                UnityEditor.EditorApplication.update -= Poll;

                if (request.Status == StatusCode.Success)
                    UnityEngine.Debug.Log($"[PSV Installer] {label} installed: {request.Result?.packageId}");
                else
                    UnityEngine.Debug.LogWarning($"[PSV Installer] {label} install failed: {request.Error?.message}");
            }

            UnityEditor.EditorApplication.update += Poll;
        }
```

(`AddRequest`, `StatusCode` are already imported via `UnityEditor.PackageManager` / `UnityEditor.PackageManager.Requests`.)

- [ ] **Step 2: Rewrite `Bootstrap.cs`**

Replace the entire `Editor/Bootstrap.cs` with:

```csharp
using PSV.Installer.Catalog;
using PSV.Installer.Scanner;
using PSV.Installer.Ui;
using UnityEditor;
using UnityEngine;

namespace PSV.Installer
{
    [InitializeOnLoad]
    internal static class Bootstrap
    {
        private const string LogPrefix = "[PSV Installer]";
        private const string UpdateProbedKey = "PSV.Installer.UpdateProbedThisSession";

        static Bootstrap()
        {
            EditorApplication.delayCall += RunOnce;
        }

        private static void RunOnce()
        {
            EditorApplication.delayCall -= RunOnce;

            // Never run network/UI/manifest mutation in headless CI — it stalls and
            // mutates the committed manifest mid-build.
            if (Application.isBatchMode) return;

            var load = CatalogLoader.Load();
            switch (load.Status)
            {
                case CatalogLoadStatus.NotInstalled:
                    MetadataAutoInstall.Run();
                    return;

                case CatalogLoadStatus.Unreadable:
                    // Metadata IS present but catalog.json is broken/too-new — surface it,
                    // do NOT reinstall (that would loop every reload).
                    Debug.LogError(
                        $"{LogPrefix} Metadata catalog present but unreadable: {load.Error}. " +
                        "Not reinstalling — fix or manually reinstall com.psvgamestudio.installer.metadata.");
                    return;
            }

            var catalog = load.Catalog;
            Debug.Log(
                $"{LogPrefix} Catalog v{catalog.CatalogVersion} loaded from {load.Source} " +
                $"({(catalog.Packages?.Count ?? 0)} packages, {(catalog.External?.Count ?? 0)} external).");

            MaybeAutoUpdate(load.Source, catalog);

            var report = ProjectScanner.Scan(catalog);
            InstallerWindow.ShowIfReportChanged(report);
        }

        private static void MaybeAutoUpdate(string path, PackageCatalog catalog)
        {
            // Embedded metadata (dev project) always wins over the registry copy →
            // Client.Add would loop. Skip auto-update there.
            var isEmbedded = path != null && !path.Replace('\\', '/').Contains("/Library/PackageCache/");
            if (isEmbedded)
            {
                Debug.Log($"{LogPrefix} Embedded metadata detected; skipping Verdaccio auto-update.");
                return;
            }

            // Probe the registry at most once per editor session — prevents a warning on
            // every domain reload while offline.
            if (SessionState.GetBool(UpdateProbedKey, false)) return;
            SessionState.SetBool(UpdateProbedKey, true);

            CatalogUpdater.CheckRemoteLatestVersion(
                onSuccess: latest =>
                {
                    if (CatalogUpdater.IsNewer(latest, catalog.CatalogVersion))
                    {
                        Debug.Log($"{LogPrefix} Newer catalog available: {latest} (installed: {catalog.CatalogVersion}). Updating…");
                        CatalogUpdater.TrackInstall(CatalogUpdater.InstallVersion(latest), "Catalog");
                    }
                    else
                    {
                        Debug.Log($"{LogPrefix} Catalog is up to date ({catalog.CatalogVersion}).");
                    }
                },
                onFailure: err =>
                {
                    Debug.LogWarning($"{LogPrefix} Could not check registry for catalog updates: {err}");
                });
        }
    }
}
```

- [ ] **Step 3: Verify in Unity** — focus Unity, confirm it compiles and the console shows the catalog-load log once (not repeated every reload). No automated test (Unity-bound).

- [ ] **Step 4: Commit**

```bash
git add Editor/Bootstrap.cs Editor/Catalog/CatalogUpdater.cs
git commit -m "fix(installer): bootstrap batchmode guard, typed load result (no reinstall loop), session-gated + polled auto-update"
```

---

## Task A3: `MetadataAutoInstall` per-session guard + polled install

**Files:** Modify `Editor/MetadataAutoInstall.cs`. (Unity-verified.)

- [ ] **Step 1: Add a session guard and poll the install**

In `Editor/MetadataAutoInstall.cs`, add `using UnityEditor;` (for `SessionState`) and a key constant beside the others:
```csharp
        private const string InstallAttemptedKey = "PSV.Installer.MetadataInstallAttemptedThisSession";
```
At the very top of `Run()` (before the `IsMetadataInstalled()` check), add:
```csharp
            // Attempt the first-time install at most once per session: a failing probe
            // (offline / auth) must not re-queue Client.Add and re-warn on every reload.
            if (SessionState.GetBool(InstallAttemptedKey, false))
                return;
            SessionState.SetBool(InstallAttemptedKey, true);
```
In the `onSuccess` branch, replace the bare `CatalogUpdater.InstallVersion(version);` call with a tracked one:
```csharp
                    try
                    {
                        CatalogUpdater.TrackInstall(
                            CatalogUpdater.InstallVersion(version), "Metadata");
                    }
                    catch (Exception e)
                    {
                        Debug.LogWarning($"{LogPrefix} Client.Add failed: {e.Message}");
                    }
```

- [ ] **Step 2: Verify in Unity** — compiles; on a project without metadata, the install is attempted once and its result is logged via the poll. No automated test.

- [ ] **Step 3: Commit**

```bash
git add Editor/MetadataAutoInstall.cs
git commit -m "fix(installer): metadata self-install attempted once per session + polled for failure"
```

---

# PHASE B — Scanner read path

## Task B1: `ManifestProbe` via `ManifestIO` + typed manifest error surfaced

**Files:** Modify `Editor/Scanner/ManifestProbe.cs`, `Editor/Scanner/ScanReport.cs`, `Editor/Scanner/Scanner.cs`, `Editor/Ui/InstallerWindowReportView.cs`, `Editor/Tests/ScanReportFactory.cs`; Test `Editor/Tests/ManifestProbeTests.cs`.

- [ ] **Step 1: Write the failing tests**

`Editor/Tests/ManifestProbeTests.cs`:

```csharp
using System.IO;
using NUnit.Framework;
using PSV.Installer.Scanner;

namespace PSV.Installer.Tests
{
    public sealed class ManifestProbeTests
    {
        private string _dir;

        [SetUp] public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "psvmp_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_dir);
        }
        [TearDown] public void TearDown() { if (Directory.Exists(_dir)) Directory.Delete(_dir, true); }

        private string Write(string content)
        {
            var p = Path.Combine(_dir, "manifest.json");
            File.WriteAllText(p, content);
            return p;
        }

        [Test]
        public void Valid_manifest_is_readable_with_deps_and_scopes()
        {
            var p = Write("{ \"dependencies\": { \"com.x\": \"1.0.0\" }, " +
                          "\"scopedRegistries\": [ { \"name\": \"PSV\", \"url\": \"https://r/\", \"scopes\": [\"com.psv\"] } ] }");
            var data = ManifestProbe.ReadFrom(p);
            Assert.IsTrue(data.Readable);
            Assert.IsTrue(data.Dependencies.ContainsKey("com.x"));
            Assert.AreEqual("1.0.0", data.Dependencies["com.x"]);
            Assert.IsTrue(data.HasRegisteredScope("com.psv"));
        }

        [Test]
        public void Comment_bearing_manifest_is_readable()
        {
            var p = Write("{\n  // ok\n  \"dependencies\": { \"com.x\": \"1.0.0\" }\n}");
            var data = ManifestProbe.ReadFrom(p);
            Assert.IsTrue(data.Readable);
            Assert.AreEqual("1.0.0", data.Dependencies["com.x"]);
        }

        [Test]
        public void Malformed_manifest_is_not_readable()
        {
            var p = Write("{ \"dependencies\": { ");
            var data = ManifestProbe.ReadFrom(p);
            Assert.IsFalse(data.Readable);
            Assert.IsNotEmpty(data.ReadError);
            Assert.AreEqual(0, data.Dependencies.Count); // empty, but flagged unreadable — NOT a false "nothing installed"
        }

        [Test]
        public void Missing_file_is_not_readable()
        {
            var data = ManifestProbe.ReadFrom(Path.Combine(_dir, "nope.json"));
            Assert.IsFalse(data.Readable);
            Assert.IsNotEmpty(data.ReadError);
        }
    }
}
```

- [ ] **Step 2: Run to verify failure** — compile error: `ManifestProbe.ReadFrom` / `ManifestData.Readable` / `ReadError` undefined.

- [ ] **Step 3: Rewrite `Editor/Scanner/ManifestProbe.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using PSV.Installer.Common;
using UnityEngine;

namespace PSV.Installer.Scanner
{
    /// <summary>
    /// Reads and parses the client project's Packages/manifest.json via the shared
    /// <see cref="ManifestIO"/> (tolerant of comments). A missing or malformed manifest is
    /// flagged unreadable rather than masqueraded as an empty manifest, so the scanner never
    /// produces a false "nothing installed".
    /// </summary>
    internal static class ManifestProbe
    {
        /// <summary>Reads the manifest for the current Unity project.</summary>
        public static ManifestData Read() => ReadFrom(ResolveManifestPath());

        /// <summary>Reads and maps the manifest at <paramref name="manifestPath"/>.</summary>
        internal static ManifestData ReadFrom(string manifestPath)
        {
            var r = ManifestIO.Read(manifestPath);
            switch (r.Status)
            {
                case ManifestReadStatus.FileMissing:
                    return ManifestData.Unreadable($"manifest.json not found at {manifestPath}");
                case ManifestReadStatus.ParseError:
                    return ManifestData.Unreadable($"manifest.json could not be parsed: {r.Error}");
                default:
                    return ManifestData.FromJObject(r.Root);
            }
        }

        private static string ResolveManifestPath()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, "Packages", "manifest.json");
        }
    }

    /// <summary>Immutable snapshot of a parsed manifest.json.</summary>
    internal sealed class ManifestData
    {
        /// <summary>package-id → version-string. Never null; empty when unreadable.</summary>
        public IReadOnlyDictionary<string, string> Dependencies { get; }

        /// <summary>Every scoped-registry entry. Never null; empty when unreadable.</summary>
        public IReadOnlyList<RegisteredScope> ScopedRegistries { get; }

        /// <summary>True when the manifest was read and parsed successfully.</summary>
        public bool Readable { get; }

        /// <summary>Human-readable reason when <see cref="Readable"/> is false; otherwise null.</summary>
        public string ReadError { get; }

        private ManifestData(
            IReadOnlyDictionary<string, string> dependencies,
            IReadOnlyList<RegisteredScope> scopedRegistries,
            bool readable,
            string readError)
        {
            Dependencies = dependencies;
            ScopedRegistries = scopedRegistries;
            Readable = readable;
            ReadError = readError;
        }

        public static ManifestData Unreadable(string error) =>
            new ManifestData(new Dictionary<string, string>(), new List<RegisteredScope>(), false, error);

        public static ManifestData FromJObject(JObject root)
        {
            var deps = new Dictionary<string, string>();
            if (root?["dependencies"] is JObject d)
                foreach (var p in d.Properties())
                    deps[p.Name] = p.Value?.Type == JTokenType.String ? p.Value.Value<string>() : p.Value?.ToString();

            var regs = new List<RegisteredScope>();
            if (root?["scopedRegistries"] is JArray arr)
                foreach (var tok in arr)
                    if (tok is JObject o)
                    {
                        var scopes = new List<string>();
                        if (o["scopes"] is JArray sa)
                            foreach (var s in sa)
                            {
                                var v = s?.Value<string>();
                                if (!string.IsNullOrEmpty(v)) scopes.Add(v);
                            }
                        regs.Add(new RegisteredScope(
                            o["name"]?.Value<string>() ?? string.Empty,
                            o["url"]?.Value<string>() ?? string.Empty,
                            scopes));
                    }

            return new ManifestData(deps, regs, true, null);
        }

        /// <summary>True if any registered scoped-registry entry declares the given scope.</summary>
        public bool HasRegisteredScope(string scope)
        {
            if (string.IsNullOrEmpty(scope)) return false;
            foreach (var reg in ScopedRegistries)
                foreach (var s in reg.Scopes)
                    if (string.Equals(s, scope, StringComparison.OrdinalIgnoreCase))
                        return true;
            return false;
        }
    }

    /// <summary>One scoped-registry entry from manifest.json.</summary>
    internal sealed class RegisteredScope
    {
        public string Name { get; }
        public string Url { get; }
        public IReadOnlyList<string> Scopes { get; }

        public RegisteredScope(string name, string url, List<string> scopes)
        {
            Name = name;
            Url = url;
            Scopes = scopes;
        }
    }
}
```

> This removes the old Newtonsoft `RawManifest`/`RawScopedRegistry` models — `StateClassifier` only uses `ManifestData.Dependencies` and `HasRegisteredScope`, both preserved. If the compiler reports `RawScopedRegistry` referenced elsewhere, search and update; it should not be (it was private to `ManifestProbe`).

- [ ] **Step 4: Add `ManifestError` to `ScanReport`**

In `Editor/Scanner/ScanReport.cs`, add a property to `ScanReport` and a constructor parameter. Add after the `Hash` property:
```csharp
        /// <summary>Non-null when manifest.json could not be read/parsed — the report's package
        /// states are then meaningless and the UI should surface this instead.</summary>
        public string ManifestError { get; }
```
Change the constructor to accept and assign it (add the parameter at the END):
```csharp
        internal ScanReport(
            string catalogVersion,
            DateTime scannedAtUtc,
            IReadOnlyList<PackageScanResult> packages,
            IReadOnlyList<ExternalScanResult> external,
            IReadOnlyList<UninstallScanResult> uninstalls,
            IReadOnlyList<MigrationGroup> splitGroups,
            string hash,
            string manifestError = null)
        {
            CatalogVersion = catalogVersion;
            ScannedAtUtc   = scannedAtUtc;
            Packages       = packages    ?? new List<PackageScanResult>();
            External       = external    ?? new List<ExternalScanResult>();
            Uninstalls     = uninstalls  ?? new List<UninstallScanResult>();
            SplitGroups    = splitGroups ?? new List<MigrationGroup>();
            Hash           = hash;
            ManifestError  = manifestError;
        }
```
(The `= null` default keeps the Plan-2 `ScanReportFactory` 7-arg reflection call valid, but update it anyway in Step 6 for clarity.)

- [ ] **Step 5: Propagate in `ProjectScanner.Scan`**

In `Editor/Scanner/Scanner.cs`, right after `var manifest = ManifestProbe.Read();` add:
```csharp
            var manifestError = manifest.Readable ? null : manifest.ReadError;
```
Change the final `return new ScanReport(...)` to pass it as the last argument:
```csharp
            return new ScanReport(
                catalog.CatalogVersion,
                DateTime.UtcNow,
                packages,
                externals,
                uninstalls,
                splitGroups,
                hash,
                manifestError);
```

- [ ] **Step 6: Update the test factory and surface the error in the view**

In `Editor/Tests/ScanReportFactory.cs`, change the `Build` reflection arg array to include the new trailing `null` so it stays explicit:
```csharp
                new object[] { "v", DateTime.UtcNow, packages.ToList(), new List<ExternalScanResult>(), new List<UninstallScanResult>(), groups, "hash", null },
```
(Reflection binds by the exact arg count; the constructor now has 8 parameters even though the last has a default.)

In `Editor/Ui/InstallerWindowReportView.cs`, at the very top of `Draw(...)`, right after `EnsureStyles();` and BEFORE the `if (report == null)` block stays first, insert the manifest-error guard immediately after the null check:
```csharp
            if (!string.IsNullOrEmpty(report.ManifestError))
            {
                EditorGUILayout.HelpBox(
                    $"Packages/manifest.json could not be read:\n{report.ManifestError}\n\n" +
                    "Fix the manifest, then click \"Run scan\". (States below are not shown because the " +
                    "manifest is unreadable — this is NOT an empty project.)",
                    MessageType.Error);
                return;
            }
```
Place it so the order is: `EnsureStyles();` → `if (report == null) {...}` → the new `ManifestError` guard → the rest.

- [ ] **Step 7: Run to verify pass** — `ManifestProbeTests` PASS; full Run All stays green (incl. Plan-2 tests that build `ScanReport` via the factory).

- [ ] **Step 8: Commit**

```bash
git add Editor/Scanner/ManifestProbe.cs Editor/Scanner/ScanReport.cs Editor/Scanner/Scanner.cs Editor/Ui/InstallerWindowReportView.cs Editor/Tests/ManifestProbeTests.cs Editor/Tests/ScanReportFactory.cs
git commit -m "fix(installer): scanner reads manifest via ManifestIO; broken manifest surfaced as error, not silent-empty"
```

---

# PHASE C — UI

## Task C1: Linked split-group checkboxes (pure helper + tests + view wiring)

**Files:** Create `Editor/Common/SplitSelection.cs`, `Editor/Tests/SplitSelectionTests.cs`; modify `Editor/Ui/InstallerWindowReportView.cs`.

- [ ] **Step 1: Write the failing tests**

`Editor/Tests/SplitSelectionTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using PSV.Installer.Common;

namespace PSV.Installer.Tests
{
    public sealed class SplitSelectionTests
    {
        [Test]
        public void Select_adds_all_members_without_duplicates()
        {
            var selected = new List<string> { "com.a" };
            SplitSelection.SetGroup(selected, new[] { "com.a", "com.b", "com.c" }, true);
            CollectionAssert.AreEquivalent(new[] { "com.a", "com.b", "com.c" }, selected);
        }

        [Test]
        public void Deselect_removes_all_members()
        {
            var selected = new List<string> { "com.a", "com.b", "com.c", "other" };
            SplitSelection.SetGroup(selected, new[] { "com.a", "com.b", "com.c" }, false);
            CollectionAssert.AreEquivalent(new[] { "other" }, selected);
        }

        [Test]
        public void Null_inputs_are_safe()
        {
            Assert.DoesNotThrow(() => SplitSelection.SetGroup(null, new[] { "x" }, true));
            var s = new List<string>();
            Assert.DoesNotThrow(() => SplitSelection.SetGroup(s, null, true));
        }
    }
}
```

- [ ] **Step 2: Run to verify failure** — compile error: `SplitSelection` undefined.

- [ ] **Step 3: Implement**

`Editor/Common/SplitSelection.cs`:

```csharp
using System.Collections.Generic;

namespace PSV.Installer.Common
{
    /// <summary>
    /// All-or-none selection for split-migration groups: toggling any member ticks or unticks
    /// the whole group, so a user can never apply a partial split (which would strip a legacy
    /// package without installing all its replacements).
    /// </summary>
    public static class SplitSelection
    {
        public static void SetGroup(List<string> selected, IReadOnlyList<string> members, bool select)
        {
            if (selected == null || members == null) return;
            foreach (var id in members)
            {
                if (string.IsNullOrEmpty(id)) continue;
                if (select)
                {
                    if (!selected.Contains(id)) selected.Add(id);
                }
                else
                {
                    selected.Remove(id);
                }
            }
        }
    }
}
```

- [ ] **Step 4: Run to verify pass** — `SplitSelectionTests` PASS.

- [ ] **Step 5: Wire into the view (Unity-verified)**

In `Editor/Ui/InstallerWindowReportView.cs`:

(a) add `using PSV.Installer.Common;` to the using block.

(b) change `DrawPackageRow`'s signature to accept optional linked siblings:
```csharp
        private void DrawPackageRow(PackageScanResult pkg, PackageRecord record, int indent,
            IReadOnlyList<string> linkedSiblings = null)
```
and replace its toggle handler (the `if (nowSelected != wasSelected) { ... }` block) with:
```csharp
                    if (nowSelected != wasSelected)
                    {
                        if (linkedSiblings != null && linkedSiblings.Count > 0)
                            SplitSelection.SetGroup(_selected, linkedSiblings, nowSelected);
                        else if (nowSelected) Select(pkg.Id);
                        else Deselect(pkg.Id);
                    }
```

(c) in `DrawMigrateSection`, inside the `foreach (var group in activeSplitGroups)` loop, compute the actionable members once per group and pass them to the actionable rows. Replace the inner member loop:
```csharp
                    // Members that are actually selectable (UpmCurrent siblings are shown
                    // disabled and excluded). Toggling any one ticks/unticks them all.
                    var actionable = new List<string>();
                    foreach (var pkgId in group.PackageIds)
                        if (pkgById.TryGetValue(pkgId, out var r) && r.State != PackageState.UpmCurrent)
                            actionable.Add(pkgId);

                    foreach (var pkgId in group.PackageIds)
                    {
                        if (!pkgById.TryGetValue(pkgId, out var result)) continue;
                        catalogPkgById.TryGetValue(pkgId, out var record);

                        if (result.State == PackageState.UpmCurrent)
                            DrawSplitCurrentRow(result, indent: 2);
                        else
                            DrawPackageRow(result, record, indent: 2, linkedSiblings: actionable);
                    }
```

- [ ] **Step 6: Verify in Unity** — with a split group in legacy state, ticking any member ticks all actionable members; unticking unticks all. No further automated test.

- [ ] **Step 7: Commit**

```bash
git add Editor/Common/SplitSelection.cs Editor/Tests/SplitSelectionTests.cs Editor/Ui/InstallerWindowReportView.cs
git commit -m "feat(installer): linked split-group checkboxes (all-or-none) + pure SplitSelection helper"
```

---

## Task C2: Persistent apply-result banner (survives domain reload)

**Files:** Modify `Editor/Ui/InstallerWindow.cs`. (Unity-verified.)

- [ ] **Step 1: Add serialized result fields**

In `Editor/Ui/InstallerWindow.cs`, add beside `_selected`/`_targets`:
```csharp
        // Apply result, serialized so it survives the domain reload that AssetDatabase.Refresh
        // triggers after a manifest write — otherwise the user gets no confirmation at all.
        [SerializeField] private string _resultMessage;
        [SerializeField] private int    _resultKind; // 0 = none, 1 = success, 2 = error
```

- [ ] **Step 2: Render the banner**

Add this method and call it at the top of `DrawBody` (right after `BeginScrollView` + the first `Space`, before `_reportView?.Draw`):
```csharp
        private void DrawResultBanner()
        {
            if (string.IsNullOrEmpty(_resultMessage)) return;

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.HelpBox(_resultMessage, _resultKind == 2 ? MessageType.Error : MessageType.Info);
                if (GUILayout.Button("Dismiss", GUILayout.Width(80)))
                {
                    _resultMessage = null;
                    _resultKind = 0;
                }
            }
            EditorGUILayout.Space(4);
        }
```
In `DrawBody`:
```csharp
        private void DrawBody()
        {
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);
            EditorGUILayout.Space(4);
            DrawResultBanner();
            _reportView?.Draw(_report, _catalog);
            EditorGUILayout.Space(4);
            EditorGUILayout.EndScrollView();
        }
```

- [ ] **Step 3: Set the banner in `OnApplySelected`**

Replace the post-Apply block (the `if (!result.Success) { ...dialog... }` plus the trailing `RunScan(); Repaint();`) with:
```csharp
            var result = _migrator.Apply(plan);

            _busy = false;

            if (result.Success)
            {
                _resultMessage = $"Applied {result.ExecutedCount} action(s) successfully.";
                _resultKind = 1;
            }
            else
            {
                _resultMessage = "Apply failed:\n• " + string.Join("\n• ", result.Failures) +
                    "\n\nNo backup is kept — use 'git status' / 'git restore .' to inspect and revert.";
                _resultKind = 2;
            }

            RunScan();
            Repaint();
```
(The banner replaces the transient modal; it persists across the reload so the user always sees the outcome.)

- [ ] **Step 4: Verify in Unity** — apply a small plan; after the reimport/reload the window shows a green "Applied N action(s)" banner (or red on failure) with a Dismiss button. No automated test.

- [ ] **Step 5: Commit**

```bash
git add Editor/Ui/InstallerWindow.cs
git commit -m "feat(installer): persistent apply-result banner that survives the post-apply domain reload"
```

---

## Task C3: Stale-report indicator

**Files:** Modify `Editor/Ui/InstallerWindow.cs`. (Unity-verified.)

- [ ] **Step 1: Capture the manifest fingerprint at scan time**

Add a serialized field beside the others:
```csharp
        // Manifest last-write ticks captured at scan time; if the file changes afterwards the
        // displayed report is stale (acting on it plans against out-of-date state).
        [SerializeField] private long _scannedManifestTicks;
```
Add a helper:
```csharp
        private static string ManifestPath()
        {
            var root = System.IO.Path.GetDirectoryName(Application.dataPath);
            return root == null ? null : System.IO.Path.Combine(root, "Packages", "manifest.json");
        }

        private static long ManifestTicks()
        {
            try
            {
                var p = ManifestPath();
                return p != null && System.IO.File.Exists(p)
                    ? System.IO.File.GetLastWriteTimeUtc(p).Ticks : 0;
            }
            catch { return 0; }
        }
```
In `RunScan()`, after `_report = ProjectScanner.Scan(_catalog);` add:
```csharp
            _scannedManifestTicks = ManifestTicks();
```

- [ ] **Step 2: Show the staleness banner**

Add a method and call it in `DrawBody` right after `DrawResultBanner();`:
```csharp
        private void DrawStaleBanner()
        {
            if (_report == null) return;
            if (_scannedManifestTicks == 0) return;
            if (ManifestTicks() == _scannedManifestTicks) return;

            EditorGUILayout.HelpBox(
                "Packages/manifest.json changed since the last scan — this view may be out of date. " +
                "Click \"Run scan\" to refresh before applying.",
                MessageType.Warning);
            EditorGUILayout.Space(4);
        }
```

- [ ] **Step 3: Verify in Unity** — open the window, edit `manifest.json` by hand; a yellow "changed since last scan" banner appears until Run scan. No automated test.

- [ ] **Step 4: Commit**

```bash
git add Editor/Ui/InstallerWindow.cs
git commit -m "feat(installer): stale-report indicator when manifest changes after a scan"
```

---

## Task C4: Friendly "all up to date" empty state (pure predicate + test + view)

**Files:** Modify `Editor/Ui/InstallerWindowReportView.cs`; Test `Editor/Tests/ReportViewLogicTests.cs`.

- [ ] **Step 1: Write the failing test**

`Editor/Tests/ReportViewLogicTests.cs`:

```csharp
using NUnit.Framework;
using PSV.Installer.Scanner;
using PSV.Installer.Ui;

namespace PSV.Installer.Tests
{
    public sealed class ReportViewLogicTests
    {
        [Test]
        public void All_current_has_no_actionable()
        {
            var report = ScanReportFactory.With(new[]
            {
                ScanReportFactory.Pkg("com.a", PackageState.UpmCurrent),
                ScanReportFactory.Pkg("com.b", PackageState.UpmCurrent),
            });
            Assert.IsFalse(InstallerWindowReportView.HasAnyActionable(report));
        }

        [Test]
        public void A_not_installed_package_is_actionable()
        {
            var report = ScanReportFactory.With(new[]
            {
                ScanReportFactory.Pkg("com.a", PackageState.NotInstalled),
            });
            Assert.IsTrue(InstallerWindowReportView.HasAnyActionable(report));
        }

        [Test]
        public void A_legacy_package_is_actionable()
        {
            var report = ScanReportFactory.With(new[]
            {
                ScanReportFactory.PkgLegacy("com.a", "legacy.id"),
            });
            Assert.IsTrue(InstallerWindowReportView.HasAnyActionable(report));
        }
    }
}
```

> `InstallerWindowReportView` is `internal`; the test assembly reaches it via the Plan-1 `InternalsVisibleTo`. `HasAnyActionable` must be `internal static` (added below).

- [ ] **Step 2: Run to verify failure** — compile error: `HasAnyActionable` undefined.

- [ ] **Step 3: Implement the predicate + render the empty state**

In `Editor/Ui/InstallerWindowReportView.cs`, add this method (e.g. near the lookup helpers):
```csharp
        /// <summary>
        /// True when the report contains at least one row the user can act on — anything that
        /// is not already UpmCurrent: a package to install/update/migrate, an external that's
        /// not current, or an uninstall. Drives the friendly "all up to date" empty state.
        /// </summary>
        internal static bool HasAnyActionable(ScanReport report)
        {
            if (report == null) return false;

            if (report.Packages != null)
                foreach (var p in report.Packages)
                    if (p != null && p.State != PackageState.UpmCurrent)
                        return true;

            if (report.External != null)
                foreach (var e in report.External)
                    if (e != null && e.State != ExternalState.UpmCurrent)
                        return true;

            if (report.Uninstalls != null)
                foreach (var u in report.Uninstalls)
                    if (u != null && u.State == UninstallState.InstalledNeedsRemoval)
                        return true;

            return false;
        }
```
In `Draw(...)`, after the five `DrawXSection(...)` calls (at the end of the method), add:
```csharp
            if (!HasAnyActionable(report))
            {
                EditorGUILayout.Space(6);
                EditorGUILayout.HelpBox(
                    "All PSV packages are up to date — nothing to install or migrate.",
                    MessageType.Info);
            }
```

- [ ] **Step 4: Run to verify pass** — `ReportViewLogicTests` PASS.

- [ ] **Step 5: Verify in Unity** — a fully-migrated project shows the reassuring message instead of a blank body.

- [ ] **Step 6: Commit**

```bash
git add Editor/Ui/InstallerWindowReportView.cs Editor/Tests/ReportViewLogicTests.cs
git commit -m "feat(installer): friendly 'all up to date' empty state + HasAnyActionable predicate"
```

---

## Self-review (done while writing)

- **Spec coverage:** batchmode/play-mode guard → A2 (+ play-mode popup note); poll Client.Add → A2/A3; per-session guard → A2/A3; schema upper-bound + parse-fail≠not-installed → A1/A2; ManifestProbe via ManifestIO + surfaced error → B1; linked split checkboxes → C1; persistent result banner → C2; stale indicator → C3; empty state → C4.
- **Play-mode popup:** `InstallerWindow.ShowIfReportChanged` is only ever called from `Bootstrap.RunOnce`, which now early-returns in batch mode. A play-mode focus-steal guard is folded into A2's bootstrap (RunOnce runs on `delayCall` at load; if stronger play-mode suppression is wanted, add `if (EditorApplication.isPlayingOrWillChangePlaymode) return;` to the top of `ShowIfReportChanged` — included as an optional one-liner there during A2 review).
- **Type consistency:** `CatalogLoadResult`/`CatalogLoadStatus`/`CatalogParseStatus`, `ParseAndValidate(json,out cat,out err)`, `CatalogUpdater.TrackInstall(req,label)`, `ManifestProbe.ReadFrom`, `ManifestData.{Readable,ReadError,FromJObject,Unreadable}`, `ScanReport.ManifestError` (8-arg ctor), `SplitSelection.SetGroup`, `InstallerWindowReportView.HasAnyActionable` are referenced identically across tasks. `InstallerWindow.EnsureCatalog` MUST be updated in A2 to consume the new `Load()` result — see note below.
- **Cross-cutting:** `InstallerWindow.EnsureCatalog` currently calls `CatalogLoader.Load(out _)`. After A1 the signature is `Load()` → `CatalogLoadResult`. Update `EnsureCatalog` to: `var load = CatalogLoader.Load(); _catalog = load.Status == CatalogLoadStatus.Ok ? load.Catalog : null;`. This edit belongs in **Task A2** (same commit that introduces the typed result's first consumer) — added explicitly here so it isn't missed.

---

## Execution notes

- Phase A: A1 (unit-tested) → A2 (incl. the `EnsureCatalog` update) → A3. Unity-verify after A2/A3.
- Phase B: B1 — unit-tested mapping + Unity-verify the error banner. Touches `ScanReport` ctor → re-run Plan-2 tests (ScanReportFactory updated).
- Phase C: C1 (helper tested) → C2 → C3 → C4 (predicate tested). Mostly Unity-verified UX.
- After all tasks: human runs `Test Runner → EditMode → Run All`, then `.meta` committed, then `finishing-a-development-branch`.

## After this plan

Infrastructure is closed (Plans 1-3). Remaining work is **design polishing** of the window (layout, spacing, copy, visual hierarchy) — a separate effort the owner flagged to do next.
