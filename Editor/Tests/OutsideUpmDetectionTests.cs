using NUnit.Framework;
using Newtonsoft.Json.Linq;
using PSV.Installer.Catalog;
using PSV.Installer.Scanner;

namespace PSV.Installer.Tests
{
    public class OutsideUpmDetectionTests
    {
        // ── AssetInstallProbe.MatchesAny (pure substring matching) ──

        [Test]
        public void MatchesAny_substring_case_insensitive()
        {
            var markers = new[] { "CleverAdsSolutions" };
            Assert.IsTrue(AssetInstallProbe.MatchesAny("CleverAdsSolutions.Unity", markers));
            Assert.IsTrue(AssetInstallProbe.MatchesAny("cleveradssolutions.common", markers));
            Assert.IsFalse(AssetInstallProbe.MatchesAny("Firebase.App", markers));
        }

        [Test]
        public void MatchesAny_false_on_null_or_empty()
        {
            var markers = new[] { "Tenjin" };
            Assert.IsFalse(AssetInstallProbe.MatchesAny(null, markers));
            Assert.IsFalse(AssetInstallProbe.MatchesAny("", markers));
            Assert.IsFalse(AssetInstallProbe.MatchesAny("Tenjin.Runtime", null));
        }

        // ── Global-namespace type detection (Tenjin regression) ──
        // Tenjin's Unity SDK declares its `Tenjin` / `BaseTenjin` classes in the GLOBAL
        // namespace. The old probe collected only t.Namespace, so those types were dropped
        // and a manually-installed Tenjin went undetected — the installer then added a
        // duplicate UPM copy, producing the CS0434 "namespace vs type 'Tenjin'" conflict.

        [Test]
        public void TypeIdentifier_uses_simple_name_for_global_namespace_types()
        {
            Assert.AreEqual("Tenjin", AssetInstallProbe.TypeIdentifier(null, "Tenjin"));
            Assert.AreEqual("BaseTenjin", AssetInstallProbe.TypeIdentifier("", "BaseTenjin"));
        }

        [Test]
        public void TypeIdentifier_uses_namespace_for_namespaced_types()
        {
            Assert.AreEqual("Tenjin.Runtime", AssetInstallProbe.TypeIdentifier("Tenjin.Runtime", "TenjinSDK"));
            Assert.AreEqual("Firebase.Analytics", AssetInstallProbe.TypeIdentifier("Firebase.Analytics", "FirebaseAnalytics"));
        }

        [Test]
        public void GlobalNamespace_Tenjin_type_is_detected_by_marker()
        {
            var ids = new System.Collections.Generic.HashSet<string>
            {
                AssetInstallProbe.TypeIdentifier(null, "Tenjin"),
            };
            Assert.IsTrue(AssetInstallProbe.IsPresentInIdentifiers(ids, new[] { "Tenjin" }));
        }

        // ── StateClassifier: external out-of-UPM detection ──

        [Test]
        public void External_outsideUpm_when_absent_and_detected()
        {
            var manifest = ManifestData.FromJObject(new JObject());
            var rec = new ExternalRecord { Id = "com.x", DisplayName = "X" };

            var res = StateClassifier.Classify(rec, manifest, detectedOutsideUpm: true);

            Assert.AreEqual(ExternalState.InstalledOutsideUpm, res.State);
        }

        [Test]
        public void External_notInstalled_when_absent_and_not_detected()
        {
            var manifest = ManifestData.FromJObject(new JObject());
            var rec = new ExternalRecord { Id = "com.x", DisplayName = "X" };

            var res = StateClassifier.Classify(rec, manifest, detectedOutsideUpm: false);

            Assert.AreEqual(ExternalState.NotInstalled, res.State);
        }

        [Test]
        public void External_manifest_wins_over_disk_detection()
        {
            // In manifest → UPM is the truth; a disk copy detection is ignored (no false dupe state).
            var manifest = ManifestData.FromJObject(JObject.Parse("{\"dependencies\":{\"com.x\":\"1.0.0\"}}"));
            var rec = new ExternalRecord { Id = "com.x", DisplayName = "X", Scopes = null };

            var res = StateClassifier.Classify(rec, manifest, detectedOutsideUpm: true);

            Assert.AreEqual(ExternalState.UpmCurrent, res.State);
        }

        // ── ReadStaticVersion: reflection version read (downgrade guard) ──
        // Reads a manually-installed SDK's own reported version so migration can warn before
        // downgrading a newer manual copy to the catalog-pinned UPM version.

        [Test]
        public void ReadStaticVersion_reads_static_const_string()
        {
            Assert.AreEqual("13.5.0",
                AssetInstallProbe.ReadStaticVersion("PSV.Installer.Tests.FakeSdkVersion", "SdkVersion"));
        }

        [Test]
        public void ReadStaticVersion_null_when_type_or_member_absent()
        {
            Assert.IsNull(AssetInstallProbe.ReadStaticVersion("PSV.Installer.Tests.NoSuchType", "SdkVersion"));
            Assert.IsNull(AssetInstallProbe.ReadStaticVersion("PSV.Installer.Tests.FakeSdkVersion", "NoSuchMember"));
            Assert.IsNull(AssetInstallProbe.ReadStaticVersion(null, "SdkVersion"));
            Assert.IsNull(AssetInstallProbe.ReadStaticVersion("PSV.Installer.Tests.FakeSdkVersion", ""));
        }

        // ── InstalledLegacy: a legacy manifest package provides the SDK ──
        // A bundled legacy package (e.g. com.psv.tenjin) in the manifest means the SDK already works;
        // the hub must report InstalledLegacy and offer no action (canonical install would duplicate it).

        [Test]
        public void External_installedLegacy_when_legacy_id_in_manifest()
        {
            var manifest = ManifestData.FromJObject(JObject.Parse(
                "{\"dependencies\":{\"com.psv.tenjin\":\"https://x.git#1.1.0\"}}"));
            var rec = new ExternalRecord
            {
                Id = "com.tenjin.sdk", DisplayName = "Tenjin",
                LegacyManifestIds = new System.Collections.Generic.List<string> { "com.psv.tenjin" },
            };

            var res = StateClassifier.Classify(rec, manifest, detectedOutsideUpm: false);

            Assert.AreEqual(ExternalState.InstalledLegacy, res.State);
            Assert.AreEqual("com.psv.tenjin", res.DetectedLegacyId);
        }

        [Test]
        public void External_legacy_wins_over_reflection_detection()
        {
            // Manifest legacy id beats a reflection out-of-UPM hit → no false-positive, no dup install.
            var manifest = ManifestData.FromJObject(JObject.Parse(
                "{\"dependencies\":{\"com.psv.tenjin\":\"https://x.git#1.1.0\"}}"));
            var rec = new ExternalRecord
            {
                Id = "com.tenjin.sdk", DisplayName = "Tenjin",
                LegacyManifestIds = new System.Collections.Generic.List<string> { "com.psv.tenjin" },
            };

            var res = StateClassifier.Classify(rec, manifest, detectedOutsideUpm: true);

            Assert.AreEqual(ExternalState.InstalledLegacy, res.State);
        }

        [Test]
        public void External_canonical_id_wins_over_legacy()
        {
            // Already on the canonical SDK → UpmCurrent, not InstalledLegacy.
            var manifest = ManifestData.FromJObject(JObject.Parse(
                "{\"dependencies\":{\"com.tenjin.sdk\":\"1.15.14\",\"com.psv.tenjin\":\"https://x.git#1.1.0\"}}"));
            var rec = new ExternalRecord
            {
                Id = "com.tenjin.sdk", DisplayName = "Tenjin", Scopes = null,
                LegacyManifestIds = new System.Collections.Generic.List<string> { "com.psv.tenjin" },
            };

            var res = StateClassifier.Classify(rec, manifest, detectedOutsideUpm: false);

            Assert.AreEqual(ExternalState.UpmCurrent, res.State);
        }
    }

    /// <summary>Stand-in for an SDK that exposes its version via a static const string (e.g. Firebase's
    /// <c>Firebase.VersionInfo.SdkVersion</c>), so <see cref="AssetInstallProbe.ReadStaticVersion"/> is
    /// testable without a real SDK loaded.</summary>
    internal static class FakeSdkVersion
    {
        public const string SdkVersion = "13.5.0";
    }
}
