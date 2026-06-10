using NUnit.Framework;
using Newtonsoft.Json.Linq;
using PSV.Installer.Catalog;
using PSV.Installer.Scanner;

namespace PSV.Installer.Tests
{
    public class GitDetectionTests
    {
        [Test]
        public void IsGitSpec_recognises_git_urls()
        {
            Assert.IsTrue(StateClassifier.IsGitSpec("https://github.com/x/y.git#1.0.0"));
            Assert.IsTrue(StateClassifier.IsGitSpec("git@github.com:x/y.git"));
            Assert.IsFalse(StateClassifier.IsGitSpec("1.15.14"));
            Assert.IsFalse(StateClassifier.IsGitSpec(""));
        }

        [Test]
        public void External_with_git_url_is_UpmCurrent_not_ScopeMissing()
        {
            var manifest = ManifestData.FromJObject(JObject.Parse(
                "{\"dependencies\":{\"com.tenjin.sdk\":\"https://github.com/CAS-Publishing/tenjin-sdk.git#1.15.14\"}}"));
            var rec = new ExternalRecord
            {
                Id = "com.tenjin.sdk", DisplayName = "Tenjin",
                Scopes = new System.Collections.Generic.List<string> { "com.tenjin" }
            };

            var res = StateClassifier.Classify(rec, manifest);

            Assert.AreEqual(ExternalState.UpmCurrent, res.State);
        }

        [Test]
        public void Package_with_git_url_is_UpmCurrent_not_crash()
        {
            var manifest = ManifestData.FromJObject(JObject.Parse(
                "{\"dependencies\":{\"com.x\":\"https://github.com/a/b.git#1.0.0\"}}"));
            var rec = new PackageRecord { Id = "com.x", DisplayName = "X", RecommendedVersion = "2.0.0" };

            var res = StateClassifier.Classify(rec, manifest, System.Array.Empty<string>());

            Assert.AreEqual(PackageState.UpmCurrent, res.State);
        }
    }
}
