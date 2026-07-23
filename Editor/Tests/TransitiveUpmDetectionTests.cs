using NUnit.Framework;
using Newtonsoft.Json.Linq;
using PSV.Installer.Catalog;
using PSV.Installer.Scanner;

namespace PSV.Installer.Tests
{
    // Field regression (2026-07-23): after the tenjin adapter install, com.tenjin.sdk arrives as a
    // TRANSITIVE dependency — absent from manifest.json but registered in the Package Manager and
    // its types loaded. The reflection probe then flagged it InstalledOutsideUpm ("installed
    // manually"), and Connect to Hub dead-ended with "known folders weren't found under Assets/".
    // A package registered with UPM (directly or transitively) must classify as a UPM install.
    public class TransitiveUpmDetectionTests
    {
        private static ManifestData Manifest(string json) => ManifestData.FromJObject(JObject.Parse(json));

        private static ExternalRecord Tenjin() => new ExternalRecord
        {
            Id = "com.tenjin.sdk",
            DisplayName = "Tenjin SDK",
            Registry = "psv",
            RecommendedVersion = "1.15.14-psv.2",
        };

        [Test]
        public void RegisteredTransitively_ClassifiesUpmCurrent_NotManual()
        {
            var m = Manifest(@"{ ""dependencies"": { ""com.psvgamestudio.tenjin"": ""2.0.0-preview.2"" } }");
            var r = StateClassifier.Classify(Tenjin(), m, detectedOutsideUpm: true,
                registeredUpmVersion: "1.15.14-psv.2");
            Assert.AreEqual(ExternalState.UpmCurrent, r.State);
            Assert.AreEqual("1.15.14-psv.2", r.DetectedVersion);
        }

        [Test]
        public void NotRegistered_OutsideUpmDetectionUnchanged()
        {
            var m = Manifest(@"{ ""dependencies"": {} }");
            var r = StateClassifier.Classify(Tenjin(), m, detectedOutsideUpm: true,
                registeredUpmVersion: null);
            Assert.AreEqual(ExternalState.InstalledOutsideUpm, r.State);
        }

        [Test]
        public void ManifestDependency_StillWinsOverRegisteredVersion()
        {
            // Explicit manifest entry is authoritative — the registered version must not override
            // the direct dependency's own classification (incl. its scope check).
            var m = Manifest(@"{ ""dependencies"": { ""com.tenjin.sdk"": ""1.15.14-psv.2"" },
                ""scopedRegistries"": [ { ""name"": ""psv"", ""url"": ""https://npm.psvgamestudio.com/"",
                                          ""scopes"": [""com.tenjin""] } ] }");
            var rec = Tenjin();
            rec.Scopes = new System.Collections.Generic.List<string> { "com.tenjin" };
            var r = StateClassifier.Classify(rec, m, detectedOutsideUpm: false,
                registeredUpmVersion: "1.15.14-psv.2");
            Assert.AreEqual(ExternalState.UpmCurrent, r.State);
        }

        [Test]
        public void LegacyManifestId_StillWinsOverRegisteredVersion()
        {
            // A legacy wrapper in the manifest keeps InstalledLegacy semantics even if the SDK is
            // also registered transitively (the wrapper is what the user must migrate away from).
            var m = Manifest(@"{ ""dependencies"": { ""cas.pub.tenjin"": ""https://gitlab/x.git"" } }");
            var rec = Tenjin();
            rec.LegacyManifestIds = new System.Collections.Generic.List<string> { "cas.pub.tenjin" };
            var r = StateClassifier.Classify(rec, m, detectedOutsideUpm: true,
                registeredUpmVersion: "1.15.14");
            Assert.AreEqual(ExternalState.InstalledLegacy, r.State);
            Assert.AreEqual("cas.pub.tenjin", r.DetectedLegacyId);
        }
    }
}
