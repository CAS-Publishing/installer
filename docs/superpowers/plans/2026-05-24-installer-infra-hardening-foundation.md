# Installer Infra Hardening — Foundation (Plan 1 of 3)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the two shared utilities (`SemVer`, `ManifestIO`) and the EditMode test harness that the Migrator-safety (Plan 2) and Bootstrap-hardening (Plan 3) plans depend on, replacing the project's ad-hoc version compare and adding a robust, atomic, format-preserving manifest read/write path.

**Architecture:** Two new pure/static helpers live under a new `Editor/Common/` folder (`namespace PSV.Installer.Common`). `SemVer` replaces the `System.Version`+ordinal logic inside `CatalogUpdater.IsNewer`. `ManifestIO` centralises manifest.json parsing (tolerant of comments, typed error on malformed JSON instead of a silent-empty fallback) and writing (atomic temp+`.bak`+`File.Replace`, 2-space indent, key order preserved via `JObject`). A new EditMode test assembly `PSV.Installer.Editor.Tests` makes the pure logic verifiable in Unity Test Runner. Plan 2 wires `ManifestIO` into `ManifestWriter`/`ManifestProbe`; this plan only builds and unit-tests the primitives + the one regression-safe `IsNewer` swap.

**Tech Stack:** Unity 2022.3, C#, Newtonsoft.Json (`com.unity.nuget.newtonsoft-json` 3.2.1, already a dependency), Unity Test Framework (NUnit) for EditMode tests.

**Decisions locked (from review 2026-05-24):** keep direct manifest write (no switch to `Client.AddAndRemove`); manifest write = JObject round-trip (comments dropped, keys/values/order preserved, 2-space); malformed JSON → typed `ParseError`, never silent-empty.

**Out of scope for this plan (Plan 2/3):** path containment, git-precondition delete, apply ordering, split enforcement, bootstrap guards, UI changes. This plan adds primitives + tests only and changes exactly one behavioural site (`IsNewer`).

---

## File structure

| File | Responsibility |
|---|---|
| `Editor/Tests/PSV.Installer.Editor.Tests.asmdef` | EditMode test assembly (references main asmdef + TestAssemblies) |
| `Editor/Tests/SmokeTest.cs` | One trivial test proving the harness runs |
| `Editor/Common/SemVer.cs` | Semver parse/compare; detects non-version dependency specs |
| `Editor/Tests/SemVerTests.cs` | Unit tests for `SemVer` |
| `Editor/Common/ManifestIO.cs` | Tolerant manifest read (typed result) + atomic write |
| `Editor/Tests/ManifestIOTests.cs` | Unit tests for `ManifestIO` |
| `Editor/Catalog/CatalogUpdater.cs` (modify `:65-83`) | `IsNewer` delegates to `SemVer.Compare` |
| `Editor/Tests/CatalogUpdaterTests.cs` | Regression tests pinning `IsNewer` behaviour |

> **Unity .meta note:** after creating each `.cs`/`.asmdef`, focus the Unity Editor once so it generates the `.meta` files, then `git add` both the source and its `.meta` (this repo tracks `.meta`). Do not hand-author GUIDs.

> **Running tests:** open `Window → General → Test Runner → EditMode → Run All` (or run the named fixture). Each test step below gives the fixture/test name and expected result. A CLI alternative (`Unity -batchmode -projectPath <dev> -runTests -testPlatform EditMode -testResults results.xml`) becomes safe only after Plan 3 adds the batchmode guard; until then prefer the Test Runner window.

---

## Task 1: EditMode test assembly + smoke test

**Files:**
- Create: `Editor/Tests/PSV.Installer.Editor.Tests.asmdef`
- Create: `Editor/Tests/SmokeTest.cs`

- [ ] **Step 1: Create the test assembly definition**

`Editor/Tests/PSV.Installer.Editor.Tests.asmdef`:

```json
{
    "name": "PSV.Installer.Editor.Tests",
    "rootNamespace": "PSV.Installer.Tests",
    "references": [
        "PSV.Installer.Editor"
    ],
    "optionalUnityReferences": [
        "TestAssemblies"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

- [ ] **Step 2: Write the smoke test**

`Editor/Tests/SmokeTest.cs`:

```csharp
using NUnit.Framework;

namespace PSV.Installer.Tests
{
    public sealed class SmokeTest
    {
        [Test]
        public void Harness_Runs()
        {
            Assert.Pass("EditMode test harness is wired.");
        }
    }
}
```

- [ ] **Step 3: Run it**

Focus Unity so it compiles + generates `.meta`. Open `Window → General → Test Runner → EditMode`. Expected: `PSV.Installer.Tests.SmokeTest.Harness_Runs` appears and PASSES.

- [ ] **Step 4: Commit**

```bash
git add Editor/Tests/PSV.Installer.Editor.Tests.asmdef Editor/Tests/PSV.Installer.Editor.Tests.asmdef.meta Editor/Tests/SmokeTest.cs Editor/Tests/SmokeTest.cs.meta Editor/Tests.meta
git commit -m "test(installer): add EditMode test assembly + smoke test"
```

---

## Task 2: SemVer comparison utility

**Files:**
- Create: `Editor/Common/SemVer.cs`
- Test: `Editor/Tests/SemVerTests.cs`

- [ ] **Step 1: Write the failing tests**

`Editor/Tests/SemVerTests.cs`:

```csharp
using NUnit.Framework;
using PSV.Installer.Common;

namespace PSV.Installer.Tests
{
    public sealed class SemVerTests
    {
        // ── IsVersion ──────────────────────────────────────────────
        [TestCase("1.2.3", true)]
        [TestCase("1.0", true)]
        [TestCase("1", true)]
        [TestCase("v1.2.3", true)]
        [TestCase("1.2.3-rc.1", true)]
        [TestCase("1.2.3+build5", true)]
        [TestCase("file:../MyPkg", false)]
        [TestCase("https://github.com/x/y.git#v2", false)]
        [TestCase("git@github.com:x/y.git", false)]
        [TestCase("latest", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void IsVersion_classifies(string input, bool expected)
        {
            Assert.AreEqual(expected, SemVer.IsVersion(input));
        }

        // ── Compare: numeric, not lexical ──────────────────────────
        [Test] public void Compare_equal() => Assert.AreEqual(0, SemVer.Compare("1.0.0", "1.0.0"));
        [Test] public void Compare_numeric_minor() => Assert.Less(SemVer.Compare("1.2.0", "1.10.0"), 0);
        [Test] public void Compare_major() => Assert.Greater(SemVer.Compare("2.0.0", "1.9.9"), 0);
        [Test] public void Compare_pads_two_component() => Assert.AreEqual(0, SemVer.Compare("1.0", "1.0.0"));
        [Test] public void Compare_pads_one_component() => Assert.AreEqual(0, SemVer.Compare("1", "1.0.0"));

        // ── Compare: prerelease & build metadata per semver ────────
        [Test] public void Release_beats_prerelease() => Assert.Greater(SemVer.Compare("1.0.0", "1.0.0-rc.1"), 0);
        [Test] public void Prerelease_numeric_identifiers() => Assert.Less(SemVer.Compare("1.0.0-rc.2", "1.0.0-rc.10"), 0);
        [Test] public void Prerelease_alpha_ordinal() => Assert.Less(SemVer.Compare("1.0.0-alpha", "1.0.0-beta"), 0);
        [Test] public void Build_metadata_ignored() => Assert.AreEqual(0, SemVer.Compare("1.0.0+build9", "1.0.0"));
        [Test] public void Leading_v_stripped() => Assert.AreEqual(0, SemVer.Compare("v1.2.3", "1.2.3"));
    }
}
```

- [ ] **Step 2: Run to verify failure**

Test Runner → EditMode. Expected: compile error / all `SemVer*` tests FAIL — `SemVer` does not exist yet.

- [ ] **Step 3: Implement `SemVer`**

`Editor/Common/SemVer.cs`:

```csharp
using System;

namespace PSV.Installer.Common
{
    /// <summary>
    /// Minimal SemVer 2.0.0 comparison sufficient for UPM version strings.
    /// Tolerates 1- and 2-component cores (padded with zeros), a leading 'v',
    /// and surrounding whitespace. Build metadata (<c>+...</c>) is ignored per spec.
    /// Non-version dependency specs (<c>file:</c>, git/https URLs, <c>latest</c>) are
    /// reported by <see cref="IsVersion"/> so callers never feed them to ordinal compare.
    /// </summary>
    public static class SemVer
    {
        /// <summary>True when <paramref name="value"/> parses as a numeric semver core.</summary>
        public static bool IsVersion(string value)
        {
            return TryParse(value, out _, out _, out _, out _);
        }

        /// <summary>
        /// Returns &lt;0 if a &lt; b, 0 if equal, &gt;0 if a &gt; b.
        /// Non-versions sort below versions; two non-versions compare ordinally.
        /// </summary>
        public static int Compare(string a, string b)
        {
            var aOk = TryParse(a, out var aMaj, out var aMin, out var aPat, out var aPre);
            var bOk = TryParse(b, out var bMaj, out var bMin, out var bPat, out var bPre);

            if (!aOk && !bOk) return string.CompareOrdinal(a ?? string.Empty, b ?? string.Empty);
            if (!aOk) return -1;
            if (!bOk) return 1;

            if (aMaj != bMaj) return aMaj.CompareTo(bMaj);
            if (aMin != bMin) return aMin.CompareTo(bMin);
            if (aPat != bPat) return aPat.CompareTo(bPat);

            // Equal cores: a version with no prerelease outranks one with prerelease.
            var aHasPre = !string.IsNullOrEmpty(aPre);
            var bHasPre = !string.IsNullOrEmpty(bPre);
            if (aHasPre != bHasPre) return aHasPre ? -1 : 1;
            if (!aHasPre) return 0;

            return ComparePrerelease(aPre, bPre);
        }

        // ── Parsing ────────────────────────────────────────────────

        private static bool TryParse(string raw, out int major, out int minor, out int patch, out string prerelease)
        {
            major = minor = patch = 0;
            prerelease = null;
            if (string.IsNullOrWhiteSpace(raw)) return false;

            var s = raw.Trim();
            if (s.Length > 1 && (s[0] == 'v' || s[0] == 'V')) s = s.Substring(1);

            // Drop build metadata.
            var plus = s.IndexOf('+');
            if (plus >= 0) s = s.Substring(0, plus);

            // Split off prerelease.
            var dash = s.IndexOf('-');
            if (dash >= 0)
            {
                prerelease = s.Substring(dash + 1);
                s = s.Substring(0, dash);
            }

            var parts = s.Split('.');
            if (parts.Length == 0 || parts.Length > 3) return false;

            if (!TryComponent(parts, 0, out major)) return false;
            if (!TryComponent(parts, 1, out minor)) return false;
            if (!TryComponent(parts, 2, out patch)) return false;
            return true;
        }

        private static bool TryComponent(string[] parts, int index, out int value)
        {
            value = 0;
            if (index >= parts.Length) return true; // missing component → 0 (pad)
            var p = parts[index];
            if (p.Length == 0) return false;
            return int.TryParse(p, out value) && value >= 0;
        }

        private static int ComparePrerelease(string a, string b)
        {
            var aIds = a.Split('.');
            var bIds = b.Split('.');
            var n = Math.Min(aIds.Length, bIds.Length);

            for (var i = 0; i < n; i++)
            {
                var cmp = ComparePrereleaseId(aIds[i], bIds[i]);
                if (cmp != 0) return cmp;
            }
            // All shared identifiers equal: fewer identifiers sorts lower.
            return aIds.Length.CompareTo(bIds.Length);
        }

        private static int ComparePrereleaseId(string a, string b)
        {
            var aNum = int.TryParse(a, out var ai);
            var bNum = int.TryParse(b, out var bi);

            if (aNum && bNum) return ai.CompareTo(bi);   // numeric identifiers compare numerically
            if (aNum != bNum) return aNum ? -1 : 1;      // numeric identifiers sort below alphanumeric
            return string.CompareOrdinal(a, b);          // both alphanumeric → ASCII order
        }
    }
}
```

- [ ] **Step 4: Run to verify pass**

Test Runner → EditMode → `SemVerTests`. Expected: all PASS.

- [ ] **Step 5: Commit**

```bash
git add Editor/Common/SemVer.cs Editor/Common/SemVer.cs.meta Editor/Common.meta Editor/Tests/SemVerTests.cs Editor/Tests/SemVerTests.cs.meta
git commit -m "feat(installer): add SemVer compare util with prerelease + non-version handling"
```

---

## Task 3: Route `CatalogUpdater.IsNewer` through `SemVer`

**Files:**
- Modify: `Editor/Catalog/CatalogUpdater.cs:65-83`
- Test: `Editor/Tests/CatalogUpdaterTests.cs`

- [ ] **Step 1: Write the failing regression tests**

`Editor/Tests/CatalogUpdaterTests.cs`:

```csharp
using NUnit.Framework;
using PSV.Installer.Catalog;

namespace PSV.Installer.Tests
{
    public sealed class CatalogUpdaterTests
    {
        [Test] public void Newer_patch() => Assert.IsTrue(CatalogUpdater.IsNewer("1.0.1", "1.0.0"));
        [Test] public void Not_newer_lower() => Assert.IsFalse(CatalogUpdater.IsNewer("1.0.0", "1.0.1"));
        [Test] public void Not_newer_equal() => Assert.IsFalse(CatalogUpdater.IsNewer("1.0.0", "1.0.0"));
        [Test] public void Release_newer_than_its_prerelease() => Assert.IsTrue(CatalogUpdater.IsNewer("1.0.0", "1.0.0-rc.1"));
        [Test] public void Numeric_prerelease_order() => Assert.IsTrue(CatalogUpdater.IsNewer("1.0.0-rc.10", "1.0.0-rc.2"));
        [Test] public void Null_or_empty_guard() => Assert.IsFalse(CatalogUpdater.IsNewer("", "1.0.0"));
        // Regression: a non-version local spec must NOT be treated as up-to-date/newer.
        [Test] public void Non_version_local_not_newer() => Assert.IsFalse(CatalogUpdater.IsNewer("file:../x", "1.0.0"));
        [Test] public void Remote_version_newer_than_non_version_local() => Assert.IsTrue(CatalogUpdater.IsNewer("2.0.0", "file:../x"));
    }
}
```

- [ ] **Step 2: Run to verify failure**

Test Runner → EditMode → `CatalogUpdaterTests`. Expected: `Numeric_prerelease_order` and the two non-version cases FAIL under the current ordinal logic (`Remote_version_newer_than_non_version_local` and `Non_version_local_not_newer`).

- [ ] **Step 3: Replace `IsNewer` body**

In `Editor/Catalog/CatalogUpdater.cs`, add `using PSV.Installer.Common;` at the top, then replace the method at lines 63-83:

```csharp
        // Compares two semver-ish strings via the shared SemVer comparator.
        // Non-version specs (file:/git/https/latest) are never "newer".
        public static bool IsNewer(string remote, string local)
        {
            if (string.IsNullOrEmpty(remote) || string.IsNullOrEmpty(local)) return false;
            if (!SemVer.IsVersion(remote)) return false;
            return SemVer.Compare(remote, local) > 0;
        }
```

- [ ] **Step 4: Run to verify pass**

Test Runner → EditMode → `CatalogUpdaterTests`. Expected: all PASS.

- [ ] **Step 5: Commit**

```bash
git add Editor/Catalog/CatalogUpdater.cs Editor/Tests/CatalogUpdaterTests.cs Editor/Tests/CatalogUpdaterTests.cs.meta
git commit -m "fix(installer): route IsNewer through SemVer; non-version specs never newer"
```

---

## Task 4: `ManifestIO.Read` — tolerant parse with typed result

**Files:**
- Create: `Editor/Common/ManifestIO.cs`
- Test: `Editor/Tests/ManifestIOTests.cs`

- [ ] **Step 1: Write the failing tests**

`Editor/Tests/ManifestIOTests.cs`:

```csharp
using System.IO;
using NUnit.Framework;
using PSV.Installer.Common;

namespace PSV.Installer.Tests
{
    public sealed class ManifestIOReadTests
    {
        private string _dir;

        [SetUp] public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "psvio_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_dir);
        }

        [TearDown] public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        private string Write(string name, string content)
        {
            var p = Path.Combine(_dir, name);
            File.WriteAllText(p, content);
            return p;
        }

        [Test]
        public void Missing_file_reports_FileMissing()
        {
            var r = ManifestIO.Read(Path.Combine(_dir, "nope.json"));
            Assert.AreEqual(ManifestReadStatus.FileMissing, r.Status);
            Assert.IsNull(r.Root);
        }

        [Test]
        public void Valid_manifest_reads_ok()
        {
            var p = Write("manifest.json", "{ \"dependencies\": { \"com.x\": \"1.0.0\" } }");
            var r = ManifestIO.Read(p);
            Assert.AreEqual(ManifestReadStatus.Ok, r.Status);
            Assert.AreEqual("1.0.0", (string)r.Root["dependencies"]["com.x"]);
        }

        [Test]
        public void Comments_are_tolerated()
        {
            var p = Write("manifest.json",
                "{\n  // leading comment\n  \"dependencies\": { \"com.x\": \"1.0.0\" } /* trailing */\n}");
            var r = ManifestIO.Read(p);
            Assert.AreEqual(ManifestReadStatus.Ok, r.Status);
            Assert.AreEqual("1.0.0", (string)r.Root["dependencies"]["com.x"]);
        }

        [Test]
        public void Trailing_comma_reports_ParseError_not_empty()
        {
            var p = Write("manifest.json", "{ \"dependencies\": { \"com.x\": \"1.0.0\", } }");
            var r = ManifestIO.Read(p);
            Assert.AreEqual(ManifestReadStatus.ParseError, r.Status);
            Assert.IsNull(r.Root);
            Assert.IsNotEmpty(r.Error);
        }
    }
}
```

- [ ] **Step 2: Run to verify failure**

Test Runner → EditMode → `ManifestIOReadTests`. Expected: compile error — `ManifestIO`/`ManifestReadStatus` do not exist.

- [ ] **Step 3: Implement `ManifestIO.Read` (and the result types)**

`Editor/Common/ManifestIO.cs`:

```csharp
using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PSV.Installer.Common
{
    /// <summary>Outcome of <see cref="ManifestIO.Read"/>.</summary>
    public enum ManifestReadStatus
    {
        /// <summary>Parsed successfully; <see cref="ManifestReadResult.Root"/> is non-null.</summary>
        Ok,
        /// <summary>File does not exist.</summary>
        FileMissing,
        /// <summary>File exists but could not be read or parsed; <see cref="ManifestReadResult.Error"/> explains.</summary>
        ParseError,
    }

    /// <summary>Result of reading a manifest.json. Distinguishes "absent" and "broken"
    /// from "empty" so callers never treat a malformed manifest as "nothing installed".</summary>
    public readonly struct ManifestReadResult
    {
        public ManifestReadStatus Status { get; }
        /// <summary>Parsed root object; non-null only when <see cref="Status"/> is <see cref="ManifestReadStatus.Ok"/>.</summary>
        public JObject Root { get; }
        public string Error { get; }

        private ManifestReadResult(ManifestReadStatus status, JObject root, string error)
        {
            Status = status; Root = root; Error = error;
        }

        public static ManifestReadResult Ok(JObject root) => new ManifestReadResult(ManifestReadStatus.Ok, root, null);
        public static ManifestReadResult Missing() => new ManifestReadResult(ManifestReadStatus.FileMissing, null, null);
        public static ManifestReadResult Failed(string error) => new ManifestReadResult(ManifestReadStatus.ParseError, null, error);
    }

    /// <summary>
    /// Single robust read/write path for Packages/manifest.json.
    /// Reading tolerates JavaScript-style comments; malformed JSON yields a typed
    /// <see cref="ManifestReadStatus.ParseError"/> rather than a silent empty manifest.
    /// </summary>
    public static class ManifestIO
    {
        private static readonly JsonLoadSettings LoadSettings = new JsonLoadSettings
        {
            CommentHandling = CommentHandling.Ignore,
            LineInfoHandling = LineInfoHandling.Ignore,
        };

        /// <summary>Reads and parses the manifest at <paramref name="path"/>.</summary>
        public static ManifestReadResult Read(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return ManifestReadResult.Missing();

            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (Exception e)
            {
                return ManifestReadResult.Failed($"read failed: {e.Message}");
            }

            try
            {
                var root = JObject.Parse(text, LoadSettings);
                return ManifestReadResult.Ok(root);
            }
            catch (JsonException e)
            {
                return ManifestReadResult.Failed($"parse failed: {e.Message}");
            }
        }
    }
}
```

- [ ] **Step 4: Run to verify pass**

Test Runner → EditMode → `ManifestIOReadTests`. Expected: all 4 PASS.

- [ ] **Step 5: Commit**

```bash
git add Editor/Common/ManifestIO.cs Editor/Common/ManifestIO.cs.meta Editor/Tests/ManifestIOTests.cs Editor/Tests/ManifestIOTests.cs.meta
git commit -m "feat(installer): ManifestIO.Read with tolerant parse + typed error result"
```

---

## Task 5: `ManifestIO.WriteAtomic` — atomic write with `.bak`, key order preserved

**Files:**
- Modify: `Editor/Common/ManifestIO.cs` (add `WriteAtomic`)
- Modify: `Editor/Tests/ManifestIOTests.cs` (add `ManifestIOWriteTests`)

- [ ] **Step 1: Write the failing tests**

Append to `Editor/Tests/ManifestIOTests.cs`:

```csharp
namespace PSV.Installer.Tests
{
    using System.IO;
    using NUnit.Framework;
    using Newtonsoft.Json.Linq;
    using PSV.Installer.Common;

    public sealed class ManifestIOWriteTests
    {
        private string _dir;
        [SetUp] public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "psviow_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_dir);
        }
        [TearDown] public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void Write_then_read_roundtrips()
        {
            var p = Path.Combine(_dir, "manifest.json");
            var root = JObject.Parse("{ \"dependencies\": { \"com.x\": \"1.0.0\" } }");
            ManifestIO.WriteAtomic(p, root);

            var r = ManifestIO.Read(p);
            Assert.AreEqual(ManifestReadStatus.Ok, r.Status);
            Assert.AreEqual("1.0.0", (string)r.Root["dependencies"]["com.x"]);
        }

        [Test]
        public void Overwrite_creates_bak_of_previous()
        {
            var p = Path.Combine(_dir, "manifest.json");
            File.WriteAllText(p, "{ \"dependencies\": { \"old\": \"0.0.1\" } }");

            var root = JObject.Parse("{ \"dependencies\": { \"new\": \"2.0.0\" } }");
            ManifestIO.WriteAtomic(p, root);

            Assert.IsTrue(File.Exists(p + ".bak"), ".bak should hold the previous manifest");
            StringAssert.Contains("old", File.ReadAllText(p + ".bak"));
            StringAssert.Contains("new", File.ReadAllText(p));
            Assert.IsFalse(File.Exists(p + ".tmp"), "temp file must be gone after a successful write");
        }

        [Test]
        public void Key_order_is_preserved()
        {
            var p = Path.Combine(_dir, "manifest.json");
            var root = new JObject
            {
                ["dependencies"] = new JObject(),
                ["scopedRegistries"] = new JArray(),
                ["zzz"] = "last",
            };
            ManifestIO.WriteAtomic(p, root);

            var text = File.ReadAllText(p);
            var iDeps = text.IndexOf("dependencies", System.StringComparison.Ordinal);
            var iReg = text.IndexOf("scopedRegistries", System.StringComparison.Ordinal);
            var iZzz = text.IndexOf("zzz", System.StringComparison.Ordinal);
            Assert.Less(iDeps, iReg);
            Assert.Less(iReg, iZzz);
        }
    }
}
```

- [ ] **Step 2: Run to verify failure**

Test Runner → EditMode → `ManifestIOWriteTests`. Expected: compile error — `ManifestIO.WriteAtomic` does not exist.

- [ ] **Step 3: Implement `WriteAtomic`**

Add to `ManifestIO` in `Editor/Common/ManifestIO.cs` (inside the class, after `Read`):

```csharp
        /// <summary>
        /// Writes <paramref name="root"/> to <paramref name="path"/> atomically:
        /// serialises to a sibling <c>.tmp</c>, then replaces the target via
        /// <see cref="File.Replace(string,string,string)"/> keeping the prior file as
        /// <c>&lt;path&gt;.bak</c>. 2-space indent; key order follows the JObject.
        /// Throws on I/O failure — callers wrap in try/catch.
        /// </summary>
        public static void WriteAtomic(string path, JObject root)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentNullException(nameof(path));
            if (root == null) throw new ArgumentNullException(nameof(root));

            var json = root.ToString(Formatting.Indented); // Newtonsoft default: 2 spaces
            var tmp = path + ".tmp";
            var bak = path + ".bak";

            File.WriteAllText(tmp, json); // UTF-8, no BOM

            if (File.Exists(path))
                File.Replace(tmp, path, bak); // atomic swap, prior contents → .bak
            else
                File.Move(tmp, path);
        }
```

- [ ] **Step 4: Run to verify pass**

Test Runner → EditMode → `ManifestIOWriteTests`. Expected: all 3 PASS. Run All EditMode to confirm no regressions across SemVer/CatalogUpdater/ManifestIO/Smoke.

- [ ] **Step 5: Commit**

```bash
git add Editor/Common/ManifestIO.cs Editor/Tests/ManifestIOTests.cs
git commit -m "feat(installer): ManifestIO.WriteAtomic (temp+.bak+File.Replace, order-preserving)"
```

---

## Self-review (done while writing)

- **Spec coverage:** SemVer (B-cluster version bugs) → Tasks 2-3; ManifestIO tolerant parse + typed error (B-cluster read/write asymmetry) → Task 4; atomic write + `.bak` (A-cluster manifest corruption) → Task 5; test harness (the missing-tests gap) → Task 1. Path containment, apply ordering, git-precondition, split enforcement, bootstrap guards, UI = **deferred to Plan 2/3 by design** (noted in header).
- **Type consistency:** `SemVer.IsVersion`/`SemVer.Compare`, `ManifestReadStatus.{Ok,FileMissing,ParseError}`, `ManifestReadResult.{Status,Root,Error}`, `ManifestIO.{Read,WriteAtomic}` are referenced identically in every task and test.
- **No placeholders:** every code/test/command step is concrete.

---

## Next plans (roadmap — written against this plan's concrete API once it lands)

- **Plan 2 — Migrator safety:** `MigrationRunner` path containment (`Path.GetFullPath` + assets-root `StartsWith`, reject escape); git-precondition before any delete (refuse unless tracked+clean); **manifest-first, delete-last** ordering; `ManifestWriter` switches to `ManifestIO` (atomic, typed errors), case-insensitive dependency idempotency, registry URL normalisation (trailing slash); `MigrationPlanner` partial-split backstop (drop `RemovePackage` when siblings unselected) + empty-version (`IsNullOrEmpty`) guard. Tests for each.
- **Plan 3 — Bootstrap + UI safety:** `Application.isBatchMode` + play-mode guards; poll the `AddRequest` to completion and surface failures; per-session `SessionState` guard against reload loops; catalog `schemaVersion` upper-bound check + distinguish parse-fail from not-installed; UI: **linked split-group checkboxes** (tick one → tick all; untick one → untick all), async Apply with a persistent `[SerializeField]` result banner surviving the domain reload, stale-report indicator, friendly "all up to date" empty state.
