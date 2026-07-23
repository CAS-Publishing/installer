using System.Collections.Generic;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using PSV.Installer.Catalog;
using PSV.Installer.Scanner;

namespace PSV.Installer.Tests
{
    public class PackageScopeMissingTests
    {
        private static ManifestData Manifest(string json) => ManifestData.FromJObject(JObject.Parse(json));

        private static PackageRecord Adapter() => new PackageRecord
        {
            Id = "com.psvgamestudio.analytics",
            Registry = "psv",
            RecommendedVersion = "0.0.1-preview.3",
        };

        [Test]
        public void CanonicalDep_NoRegistry_ClassifiesScopeMissing()
        {
            // The three broken tester projects: dependency written, no scoped registry at all.
            var m = Manifest(@"{ ""dependencies"": { ""com.psvgamestudio.analytics"": ""0.0.1-preview.3"" } }");
            var r = StateClassifier.Classify(Adapter(), m, null);
            Assert.AreEqual(PackageState.ScopeMissing, r.State);
            Assert.AreEqual("0.0.1-preview.3", r.DetectedVersion);
        }

        [Test]
        public void CanonicalDep_CoveringScope_ClassifiesUpmCurrent()
        {
            var m = Manifest(@"{ ""dependencies"": { ""com.psvgamestudio.analytics"": ""0.0.1-preview.3"" },
                ""scopedRegistries"": [ { ""name"": ""psv"", ""url"": ""https://npm.psvgamestudio.com/"",
                                          ""scopes"": [""com.psvgamestudio""] } ] }");
            var r = StateClassifier.Classify(Adapter(), m, null);
            Assert.AreEqual(PackageState.UpmCurrent, r.State);
        }

        [Test]
        public void GitDep_NoRegistry_StaysUpmCurrent()
        {
            // A git-URL dependency needs no registry — never flag it.
            var m = Manifest(@"{ ""dependencies"": { ""com.psvgamestudio.analytics"": ""https://x.git#1"" } }");
            var r = StateClassifier.Classify(Adapter(), m, null);
            Assert.AreEqual(PackageState.UpmCurrent, r.State);
        }

        [Test]
        public void ExplicitRecordScopes_AreChecked()
        {
            var rec = Adapter();
            rec.Scopes = new List<string> { "com.psvgamestudio" };
            var m = Manifest(@"{ ""dependencies"": { ""com.psvgamestudio.analytics"": ""0.0.1-preview.3"" },
                ""scopedRegistries"": [ { ""name"": ""o"", ""url"": ""https://other/"", ""scopes"": [""com.other""] } ] }");
            Assert.AreEqual(PackageState.ScopeMissing, StateClassifier.Classify(rec, m, null).State);
        }

        // Fix 3 (2026-07-23 firebase-migration-and-registry-fix): an adapter installed under an
        // exact-id scope by an OLDER catalog must not flip to "Needs registry" once a newer catalog
        // widens record.Scopes to a broader prefix the manifest doesn't register yet — coverage by
        // id (Unity's own matching rule) is accepted as a fallback alongside the explicit scopes.
        [Test]
        public void ExplicitRecordScopes_MissingButExactIdScopeRegistered_NotScopeMissing()
        {
            var rec = Adapter();
            rec.Scopes = new List<string> { "com.psvgamestudio" }; // catalog now wants the broad scope
            var m = Manifest(@"{ ""dependencies"": { ""com.psvgamestudio.analytics"": ""0.0.1-preview.3"" },
                ""scopedRegistries"": [ { ""name"": ""psv"", ""url"": ""https://npm.psvgamestudio.com/"",
                                          ""scopes"": [""com.psvgamestudio.analytics""] } ] }"); // exact-id, older catalog
            Assert.AreEqual(PackageState.UpmCurrent, StateClassifier.Classify(rec, m, null).State);
        }
    }
}
