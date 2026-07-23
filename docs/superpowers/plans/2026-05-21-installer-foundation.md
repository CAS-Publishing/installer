# Installer Foundation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close Phase 1 of the CAS Pub installer ("Foundation") by adding regression test coverage to the already-written catalog model, loader, and updater, and by registering the test assembly with Unity so Test Runner discovers it. After this plan, Phase 1 is verifiable and ready to be extended by Phase 2 (Scanner).

**Architecture:** Phase 1 is a two-package skeleton: `com.psvgamestudio.installer` (Editor-only, contains `Bootstrap`, `CatalogLoader`, `CatalogUpdater`, `Catalog` POCOs) and the sibling data package `com.psvgamestudio.installer.metadata` (carries `catalog.json` + version). The installer reads the catalog from the resolved UPM path of the metadata package and asynchronously polls Verdaccio for newer versions. Production code already exists in the repo; this plan adds characterization tests (the code is too small to rewrite under strict TDD), refactors one method for testability, and wires up the test assembly. Future Phase 2+ work will be strict TDD from the first line.

**Tech Stack:** Unity 2022.3 (Editor-only), C# 9, `com.unity.nuget.newtonsoft-json` 3.2.1, Unity Test Framework 1.1.33 (NUnit), `UnityEditor.PackageManager` API, `UnityEngine.Networking.UnityWebRequest`.

---

## File Structure

### Already in place (not modified by this plan, except CatalogUpdater)
- `Packages/com.psvgamestudio.installer/package.json` — UPM manifest, declares dependency on metadata + Newtonsoft.
- `Packages/com.psvgamestudio.installer/Editor/PSV.Installer.Editor.asmdef` — Editor-only assembly.
- `Packages/com.psvgamestudio.installer/Editor/Bootstrap.cs` — `[InitializeOnLoad]` entry point.
- `Packages/com.psvgamestudio.installer/Editor/Catalog/Catalog.cs` — POCO model with Newtonsoft attributes.
- `Packages/com.psvgamestudio.installer/Editor/Catalog/CatalogLoader.cs` — reads `catalog.json` from the resolved metadata package path.
- `Packages/com.psvgamestudio.installer/Editor/Catalog/CatalogUpdater.cs` — HTTP poll to Verdaccio + `Client.Add` + version comparison.
- `Packages/com.psvgamestudio.installer.metadata/catalog.json` — actual catalog payload (schema only, no PSV packages yet).

### To modify
- `Packages/com.psvgamestudio.installer/Editor/Catalog/CatalogUpdater.cs` — extract the JSON-parsing portion of `CheckRemoteLatestVersion` into a pure `ExtractLatestVersion(string json) → string` static, callable from tests.
- `dev/Packages/manifest.json` — add `com.psvgamestudio.installer` to the existing `testables` array so Unity Test Runner discovers tests in the package.

### To create
- `Packages/com.psvgamestudio.installer/Tests/Editor/PSV.Installer.Tests.Editor.asmdef` — Editor-only test assembly; references prod asmdef, Newtonsoft.Json, TestRunner; `defineConstraints: ["UNITY_INCLUDE_TESTS"]`.
- `Packages/com.psvgamestudio.installer/Tests/Editor/CatalogParseTests.cs` — round-trip JSON parsing tests for the `Catalog` POCO.
- `Packages/com.psvgamestudio.installer/Tests/Editor/CatalogLoaderTests.cs` — verifies the loader finds the embedded metadata package and parses its `catalog.json`.
- `Packages/com.psvgamestudio.installer/Tests/Editor/CatalogUpdaterTests.cs` — `IsNewer` version-comparison matrix and `ExtractLatestVersion` parsing cases.

---

## Task 1: Create test assembly and register it with Unity Test Runner

**Files:**
- Create: `Packages/com.psvgamestudio.installer/Tests/Editor/PSV.Installer.Tests.Editor.asmdef`
- Modify: `dev/Packages/manifest.json` (add to existing `testables` array)

- [ ] **Step 1: Write the test asmdef**

Create `Packages/com.psvgamestudio.installer/Tests/Editor/PSV.Installer.Tests.Editor.asmdef`:

```json
{
  "name": "PSV.Installer.Tests.Editor",
  "rootNamespace": "PSV.Installer.Tests",
  "references": [
    "PSV.Installer.Editor",
    "Newtonsoft.Json",
    "UnityEngine.TestRunner",
    "UnityEditor.TestRunner"
  ],
  "includePlatforms": ["Editor"],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": true,
  "precompiledReferences": ["nunit.framework.dll"],
  "autoReferenced": false,
  "defineConstraints": ["UNITY_INCLUDE_TESTS"],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- [ ] **Step 2: Register the package as testable**

Edit `dev/Packages/manifest.json`. The existing `testables` array already contains `com.psvgamestudio.pub.debug`; add ours so the array becomes:

```json
  "testables": [
    "com.psvgamestudio.pub.debug",
    "com.psvgamestudio.installer"
  ],
```

- [ ] **Step 3: Open Unity, let it recompile, verify Test Runner shows the empty suite**

Open `E:\workspace\casai\dev` in Unity 2022.3.62f3.
Open `Window → General → Test Runner`.
Expected: in the **EditMode** tab a new node `PSV.Installer.Tests.Editor` appears, currently empty.
If the node does not appear: verify the asmdef file was saved with no BOM/encoding issue, verify the package name in `manifest.json` matches exactly.

- [ ] **Step 4: Commit**

```bash
git -C E:/workspace/casai/dev/Packages/com.psvgamestudio.installer add Tests/Editor/PSV.Installer.Tests.Editor.asmdef
git -C E:/workspace/casai/dev/Packages/com.psvgamestudio.installer commit -m "test: add Editor test assembly skeleton"
git -C E:/workspace/casai/dev add Packages/manifest.json
git -C E:/workspace/casai/dev commit -m "chore: register installer package as testable"
```

---

## Task 2: Refactor CatalogUpdater for testability

**Files:**
- Modify: `Packages/com.psvgamestudio.installer/Editor/Catalog/CatalogUpdater.cs`

Goal: extract the JSON-decoding part of `CheckRemoteLatestVersion` into a pure static method `ExtractLatestVersion(string json) → string?` so tests can exercise it without HTTP. No behaviour change visible from outside.

- [ ] **Step 1: Rewrite CatalogUpdater.cs**

Replace `Packages/com.psvgamestudio.installer/Editor/Catalog/CatalogUpdater.cs` with:

```csharp
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine.Networking;

namespace PSV.Installer.Catalog
{
    internal static class CatalogUpdater
    {
        public const string PsvRegistryRoot = "https://npm.psvgamestudio.com/";
        private const int TimeoutSeconds = 10;

        // Fetches the registry document for installer.metadata and reports back
        // the version under dist-tags.latest. Non-blocking; uses UnityWebRequest's
        // async completion callback.
        public static void CheckRemoteLatestVersion(Action<string> onSuccess, Action<string> onFailure = null)
        {
            var url = PsvRegistryRoot + CatalogLoader.MetadataPackageName;
            var request = UnityWebRequest.Get(url);
            request.timeout = TimeoutSeconds;
            request.SetRequestHeader("Accept", "application/json");

            var op = request.SendWebRequest();
            op.completed += _ =>
            {
                try
                {
                    if (request.result != UnityWebRequest.Result.Success)
                    {
                        onFailure?.Invoke(request.error ?? request.result.ToString());
                        return;
                    }

                    var latest = ExtractLatestVersion(request.downloadHandler.text);
                    if (string.IsNullOrEmpty(latest))
                    {
                        onFailure?.Invoke("dist-tags.latest missing in registry response.");
                        return;
                    }

                    onSuccess(latest);
                }
                catch (Exception e)
                {
                    onFailure?.Invoke(e.Message);
                }
                finally
                {
                    request.Dispose();
                }
            };
        }

        // Pure: parses a registry package document and returns the value of
        // dist-tags.latest, or null if missing/malformed. Public for tests.
        public static string ExtractLatestVersion(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var doc = JsonConvert.DeserializeObject<RegistryPackageDocument>(json);
                if (doc?.DistTags == null) return null;
                return doc.DistTags.TryGetValue("latest", out var v) && !string.IsNullOrEmpty(v) ? v : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        // Queues a UPM Add for the given metadata version. Caller may track the
        // request status if needed; for the auto-refresh path we fire-and-forget.
        public static AddRequest InstallVersion(string version)
        {
            return Client.Add($"{CatalogLoader.MetadataPackageName}@{version}");
        }

        // Compares two semver-ish strings. Falls back to ordinal compare for
        // pre-release suffixes Unity's Version doesn't understand.
        public static bool IsNewer(string remote, string local)
        {
            if (string.IsNullOrEmpty(remote) || string.IsNullOrEmpty(local)) return false;

            var remoteCore = remote.Split('-')[0];
            var localCore = local.Split('-')[0];

            if (Version.TryParse(remoteCore, out var r) && Version.TryParse(localCore, out var l))
            {
                if (r != l) return r > l;
                // Versions equal: a non-prerelease wins over a prerelease.
                var remoteIsPre = remote.Contains('-');
                var localIsPre = local.Contains('-');
                if (remoteIsPre != localIsPre) return localIsPre;
                return string.Compare(remote, local, StringComparison.Ordinal) > 0;
            }

            return string.Compare(remote, local, StringComparison.Ordinal) > 0;
        }

        private sealed class RegistryPackageDocument
        {
            [JsonProperty("name")]
            public string Name;

            [JsonProperty("dist-tags")]
            public Dictionary<string, string> DistTags;
        }
    }
}
```

- [ ] **Step 2: Let Unity recompile, verify no errors in console**

Switch to Unity, wait for compile, watch the Console.
Expected: no compile errors. The `[PSV Installer] Catalog v0.0.1 loaded ...` log line still appears on next domain reload.

- [ ] **Step 3: Commit**

```bash
git -C E:/workspace/casai/dev/Packages/com.psvgamestudio.installer add Editor/Catalog/CatalogUpdater.cs
git -C E:/workspace/casai/dev/Packages/com.psvgamestudio.installer commit -m "refactor(updater): extract ExtractLatestVersion for testability"
```

---

## Task 3: Tests for Catalog model JSON parsing — happy path

**Files:**
- Create: `Packages/com.psvgamestudio.installer/Tests/Editor/CatalogParseTests.cs`

- [ ] **Step 1: Write the failing test**

Create `Packages/com.psvgamestudio.installer/Tests/Editor/CatalogParseTests.cs`:

```csharp
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using PSV.Installer.Catalog;

namespace PSV.Installer.Tests
{
    public class CatalogParseTests
    {
        private const string FullJson = @"
        {
          ""schemaVersion"": 1,
          ""catalogVersion"": ""1.2.3"",
          ""registries"": {
            ""psv"": ""https://npm.psvgamestudio.com/"",
            ""openupm"": ""https://package.openupm.com""
          },
          ""categories"": [
            { ""id"": ""core"", ""displayName"": ""Core"" }
          ],
          ""packages"": [
            {
              ""id"": ""com.psvgamestudio.crashlytics"",
              ""displayName"": ""PSV Crashlytics"",
              ""registry"": ""psv"",
              ""category"": ""crash"",
              ""legacyNpmIds"": [""com.psv.crashlytics""],
              ""legacyAssetPaths"": [""Assets/PSV/Crashlytics""],
              ""minVersion"": ""0.2.0"",
              ""recommendedVersion"": ""0.2.1""
            }
          ],
          ""external"": [
            {
              ""id"": ""com.cleversolutions.ads.mediation"",
              ""displayName"": ""CAS Mediation"",
              ""registry"": ""openupm"",
              ""scopes"": [""com.cleversolutions""],
              ""category"": ""ad""
            }
          ]
        }";

        [Test]
        public void Parses_FullCatalog_PopulatesAllFields()
        {
            var c = JsonConvert.DeserializeObject<Catalog>(FullJson);

            Assert.That(c, Is.Not.Null);
            Assert.That(c.SchemaVersion, Is.EqualTo(1));
            Assert.That(c.CatalogVersion, Is.EqualTo("1.2.3"));

            Assert.That(c.Registries, Has.Count.EqualTo(2));
            Assert.That(c.Registries["psv"], Is.EqualTo("https://npm.psvgamestudio.com/"));

            Assert.That(c.Categories, Has.Count.EqualTo(1));
            Assert.That(c.Categories[0].Id, Is.EqualTo("core"));

            Assert.That(c.Packages, Has.Count.EqualTo(1));
            var p = c.Packages[0];
            Assert.That(p.Id, Is.EqualTo("com.psvgamestudio.crashlytics"));
            Assert.That(p.LegacyNpmIds, Is.EqualTo(new[] { "com.psv.crashlytics" }));
            Assert.That(p.LegacyAssetPaths, Is.EqualTo(new[] { "Assets/PSV/Crashlytics" }));
            Assert.That(p.MinVersion, Is.EqualTo("0.2.0"));
            Assert.That(p.RecommendedVersion, Is.EqualTo("0.2.1"));

            Assert.That(c.External, Has.Count.EqualTo(1));
            Assert.That(c.External[0].Scopes.Single(), Is.EqualTo("com.cleversolutions"));
        }
    }
}
```

- [ ] **Step 2: Run the test**

In Unity Test Runner (EditMode tab) → right-click `PSV.Installer.Tests.Editor → CatalogParseTests → Parses_FullCatalog_PopulatesAllFields` → Run.
Expected: PASS (the production `Catalog` POCO is already wired with `[JsonProperty]` attributes that match the keys above).

- [ ] **Step 3: Commit**

```bash
git -C E:/workspace/casai/dev/Packages/com.psvgamestudio.installer add Tests/Editor/CatalogParseTests.cs
git -C E:/workspace/casai/dev/Packages/com.psvgamestudio.installer commit -m "test(catalog): parse happy-path catalog JSON"
```

---

## Task 4: Tests for Catalog model JSON parsing — edge cases

**Files:**
- Modify: `Packages/com.psvgamestudio.installer/Tests/Editor/CatalogParseTests.cs` (append new test methods)

- [ ] **Step 1: Append edge-case tests**

Add the following test methods to the `CatalogParseTests` class inside `CatalogParseTests.cs`, immediately after `Parses_FullCatalog_PopulatesAllFields`:

```csharp
        [Test]
        public void Parses_MinimalCatalog_OmittedListsAreNull()
        {
            const string minimal = @"
            {
              ""schemaVersion"": 1,
              ""catalogVersion"": ""0.0.1"",
              ""registries"": {}
            }";

            var c = JsonConvert.DeserializeObject<Catalog>(minimal);

            Assert.That(c.SchemaVersion, Is.EqualTo(1));
            Assert.That(c.CatalogVersion, Is.EqualTo("0.0.1"));
            Assert.That(c.Registries, Has.Count.EqualTo(0));
            Assert.That(c.Categories, Is.Null);
            Assert.That(c.Packages, Is.Null);
            Assert.That(c.External, Is.Null);
        }

        [Test]
        public void Parses_CatalogWithUnknownFields_IgnoresThem()
        {
            const string withExtras = @"
            {
              ""schemaVersion"": 1,
              ""catalogVersion"": ""0.0.1"",
              ""registries"": {},
              ""futureField"": { ""nested"": [1, 2, 3] }
            }";

            var c = JsonConvert.DeserializeObject<Catalog>(withExtras);

            Assert.That(c, Is.Not.Null);
            Assert.That(c.CatalogVersion, Is.EqualTo("0.0.1"));
        }
```

- [ ] **Step 2: Run the two new tests**

In Unity Test Runner → run `Parses_MinimalCatalog_OmittedListsAreNull` and `Parses_CatalogWithUnknownFields_IgnoresThem`.
Expected: both PASS. Newtonsoft is tolerant of unknown fields by default and leaves omitted reference-type collections as `null`.

- [ ] **Step 3: Commit**

```bash
git -C E:/workspace/casai/dev/Packages/com.psvgamestudio.installer add Tests/Editor/CatalogParseTests.cs
git -C E:/workspace/casai/dev/Packages/com.psvgamestudio.installer commit -m "test(catalog): parse minimal and extra-fields catalog JSON"
```

---

## Task 5: Tests for CatalogLoader against the embedded metadata package

**Files:**
- Create: `Packages/com.psvgamestudio.installer/Tests/Editor/CatalogLoaderTests.cs`

The `installer.metadata` package is embedded in `dev/Packages/`, so its `catalog.json` is on disk. These tests treat that file as a fixture — they assert the loader finds it and parses it into the version we currently ship (`0.0.1`).

- [ ] **Step 1: Write the loader test file**

Create `Packages/com.psvgamestudio.installer/Tests/Editor/CatalogLoaderTests.cs`:

```csharp
using NUnit.Framework;
using PSV.Installer.Catalog;

namespace PSV.Installer.Tests
{
    public class CatalogLoaderTests
    {
        [Test]
        public void Load_FindsEmbeddedMetadata_ReturnsCatalogWithExpectedVersion()
        {
            var catalog = CatalogLoader.Load(out var source);

            Assert.That(catalog, Is.Not.Null,
                "CatalogLoader.Load returned null. The metadata package may not be installed in dev/Packages or catalog.json may be missing.");
            Assert.That(source, Does.EndWith("catalog.json"));
            Assert.That(catalog.SchemaVersion, Is.EqualTo(1));
            Assert.That(catalog.CatalogVersion, Is.EqualTo("0.0.1"));
        }

        [Test]
        public void Load_EmbeddedCatalog_ContainsCasExternalEntry()
        {
            var catalog = CatalogLoader.Load(out _);

            Assert.That(catalog.External, Is.Not.Null);
            Assert.That(catalog.External.Exists(e => e.Id == "com.cleversolutions.ads.mediation"), Is.True,
                "Expected the embedded catalog to register the CAS Mediation external entry on OpenUPM.");
        }

        [Test]
        public void Load_EmbeddedCatalog_RegistersPsvAndOpenUpmRegistries()
        {
            var catalog = CatalogLoader.Load(out _);

            Assert.That(catalog.Registries, Is.Not.Null);
            Assert.That(catalog.Registries.ContainsKey("psv"), Is.True);
            Assert.That(catalog.Registries.ContainsKey("openupm"), Is.True);
        }
    }
}
```

- [ ] **Step 2: Run the three loader tests**

Unity Test Runner → `CatalogLoaderTests` → Run.
Expected: all three PASS. If `Load_FindsEmbeddedMetadata_...` fails with `null`: confirm `Packages/com.psvgamestudio.installer.metadata/catalog.json` is present and the `installer.metadata` package shows up under `Window → Package Manager → In Project`.

- [ ] **Step 3: Commit**

```bash
git -C E:/workspace/casai/dev/Packages/com.psvgamestudio.installer add Tests/Editor/CatalogLoaderTests.cs
git -C E:/workspace/casai/dev/Packages/com.psvgamestudio.installer commit -m "test(loader): assert embedded metadata is loadable"
```

---

## Task 6: Tests for CatalogUpdater.IsNewer version comparison

**Files:**
- Create: `Packages/com.psvgamestudio.installer/Tests/Editor/CatalogUpdaterTests.cs`

- [ ] **Step 1: Write the IsNewer test file**

Create `Packages/com.psvgamestudio.installer/Tests/Editor/CatalogUpdaterTests.cs`:

```csharp
using NUnit.Framework;
using PSV.Installer.Catalog;

namespace PSV.Installer.Tests
{
    public class CatalogUpdaterTests
    {
        [TestCase("0.0.2", "0.0.1", true,  TestName = "newer_patch")]
        [TestCase("0.1.0", "0.0.9", true,  TestName = "newer_minor")]
        [TestCase("1.0.0", "0.9.9", true,  TestName = "newer_major")]
        [TestCase("0.0.1", "0.0.1", false, TestName = "equal_stable")]
        [TestCase("0.0.1", "0.0.2", false, TestName = "older")]
        [TestCase("0.0.1", null,    false, TestName = "local_null")]
        [TestCase(null,    "0.0.1", false, TestName = "remote_null")]
        [TestCase("",      "0.0.1", false, TestName = "remote_empty")]
        [TestCase("0.0.2-preview", "0.0.1", true,  TestName = "prerelease_newer_core")]
        [TestCase("0.0.1-preview", "0.0.1", false, TestName = "prerelease_loses_to_stable_same_core")]
        [TestCase("0.0.1", "0.0.1-preview", true,  TestName = "stable_wins_over_prerelease_same_core")]
        [TestCase("0.0.1-preview.2", "0.0.1-preview.1", true,  TestName = "prerelease_ordinal_newer")]
        public void IsNewer_Cases(string remote, string local, bool expected)
        {
            Assert.That(CatalogUpdater.IsNewer(remote, local), Is.EqualTo(expected));
        }
    }
}
```

- [ ] **Step 2: Run all IsNewer cases**

Unity Test Runner → `CatalogUpdaterTests → IsNewer_Cases` → Run.
Expected: all 12 cases PASS.
If a prerelease case fails: check that `CatalogUpdater.IsNewer` keeps the equal-core branch that returns `localIsPre` when only one side has a `-suffix`. (This is the line `if (remoteIsPre != localIsPre) return localIsPre;`.)

- [ ] **Step 3: Commit**

```bash
git -C E:/workspace/casai/dev/Packages/com.psvgamestudio.installer add Tests/Editor/CatalogUpdaterTests.cs
git -C E:/workspace/casai/dev/Packages/com.psvgamestudio.installer commit -m "test(updater): cover IsNewer with stable+prerelease matrix"
```

---

## Task 7: Tests for CatalogUpdater.ExtractLatestVersion

**Files:**
- Modify: `Packages/com.psvgamestudio.installer/Tests/Editor/CatalogUpdaterTests.cs` (append new test methods)

- [ ] **Step 1: Append parsing tests**

Add the following methods to the `CatalogUpdaterTests` class, immediately after `IsNewer_Cases`:

```csharp
        [Test]
        public void ExtractLatestVersion_FromValidDoc_ReturnsLatestTag()
        {
            const string doc = @"
            {
              ""name"": ""com.psvgamestudio.installer.metadata"",
              ""dist-tags"": { ""latest"": ""0.0.3"", ""beta"": ""0.1.0-pre"" }
            }";

            Assert.That(CatalogUpdater.ExtractLatestVersion(doc), Is.EqualTo("0.0.3"));
        }

        [Test]
        public void ExtractLatestVersion_MissingLatestTag_ReturnsNull()
        {
            const string doc = @"
            {
              ""name"": ""com.psvgamestudio.installer.metadata"",
              ""dist-tags"": { ""beta"": ""0.1.0-pre"" }
            }";

            Assert.That(CatalogUpdater.ExtractLatestVersion(doc), Is.Null);
        }

        [Test]
        public void ExtractLatestVersion_MissingDistTags_ReturnsNull()
        {
            const string doc = @"{ ""name"": ""x"" }";
            Assert.That(CatalogUpdater.ExtractLatestVersion(doc), Is.Null);
        }

        [Test]
        public void ExtractLatestVersion_MalformedJson_ReturnsNull()
        {
            Assert.That(CatalogUpdater.ExtractLatestVersion("{not json"), Is.Null);
        }

        [Test]
        public void ExtractLatestVersion_EmptyOrNull_ReturnsNull()
        {
            Assert.That(CatalogUpdater.ExtractLatestVersion(null), Is.Null);
            Assert.That(CatalogUpdater.ExtractLatestVersion(""), Is.Null);
        }
```

- [ ] **Step 2: Run the five new tests**

Unity Test Runner → run the new `ExtractLatestVersion_*` tests.
Expected: all PASS.

- [ ] **Step 3: Commit**

```bash
git -C E:/workspace/casai/dev/Packages/com.psvgamestudio.installer add Tests/Editor/CatalogUpdaterTests.cs
git -C E:/workspace/casai/dev/Packages/com.psvgamestudio.installer commit -m "test(updater): cover ExtractLatestVersion parsing branches"
```

---

## Task 8: Full suite verification and final tidy

**Files:** none modified (verification only).

- [ ] **Step 1: Run the entire EditMode suite**

Unity Test Runner → EditMode tab → click **Run All** at the top.
Expected: every test under `PSV.Installer.Tests.Editor` is green. Counts: `CatalogParseTests` 3, `CatalogLoaderTests` 3, `CatalogUpdaterTests` 17 (12 `IsNewer_Cases` + 5 `ExtractLatestVersion_*`). Total: 23 green.

- [ ] **Step 2: Verify the live updater path still works at editor startup**

Unity → `Edit → Preferences → External Tools → Regenerate project files` (or just trigger a domain reload, e.g. by saving any .cs file).
Expected console lines on next reload:

```
[PSV Installer] Catalog v0.0.1 loaded from .../com.psvgamestudio.installer.metadata/catalog.json (0 packages, 1 external).
[PSV Installer] Catalog is up to date (0.0.1).
```

If instead a warning appears about a newer version: that means someone published `installer.metadata > 0.0.1` to Verdaccio meanwhile — investigate before continuing.

- [ ] **Step 3: Confirm git history is clean and per-task**

```bash
git -C E:/workspace/casai/dev/Packages/com.psvgamestudio.installer log --oneline -10
git -C E:/workspace/casai/dev log --oneline -3
```

Expected: roughly one commit per task above, all on the installer's local repo plus one commit on the umbrella `dev/` repo for the `testables` change. No leftover untracked files in either repo apart from those tracked by `.gitignore`.

---

## What this plan does NOT cover

Out of scope here, deferred to follow-up plans:

- **Phase 2 — Scanner.** Reading client `manifest.json` for legacy/managed npm ids, walking `Assets/` for `legacyAssetPaths` from the catalog, building the typed scan report, and computing the report hash for the auto-popup mute logic.
- **Populating `catalog.json` with real PSV packages** (`crashlytics`, `pub.debug`, future `pub.tenjin`, `pub.publishing-sdk`). Per Alexandr's directive, we agree the infrastructure first and walk each package together later.
- **The auto-popup window** (Phase 3 UI).
- **The migrator + backup + rollback** (Phase 4).
- **Three-channel distribution** (Phase 6).
- **Test matrix and end-user documentation** (Phase 7).
- **A network-touching integration test** that actually hits `https://npm.psvgamestudio.com/com.psvgamestudio.installer.metadata`. The pure parsing function `ExtractLatestVersion` is covered here; the `UnityWebRequest` plumbing in `CheckRemoteLatestVersion` is verified manually at editor startup (Task 8 Step 2) until a real integration harness exists.

---

## Self-Review notes

- Spec coverage: every Phase 1 artefact in the conversation log (skeleton, loader, updater, schema, bootstrap, version-comparison logic, auto-update wiring) is either already in repo or exercised by the new tests; no listed Phase 1 item is left unverified.
- Placeholders scanned: none. Every step contains exact paths, code, commands, and expected outcomes.
- Type consistency: `Catalog`, `Category`, `PackageRecord`, `ExternalRecord`, `CatalogLoader.Load`, `CatalogUpdater.IsNewer`, `CatalogUpdater.ExtractLatestVersion`, `CatalogLoader.MetadataPackageName`, `PSV.Installer.Catalog` namespace, `PSV.Installer.Tests` namespace — referenced identically across all tasks and against the existing source.
