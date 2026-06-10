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
    }
}
