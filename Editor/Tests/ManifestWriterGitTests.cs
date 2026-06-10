using NUnit.Framework;
using Newtonsoft.Json.Linq;
using PSV.Installer.Migrator;

namespace PSV.Installer.Tests
{
    public class ManifestWriterGitTests
    {
        [Test]
        public void AddGitPackage_writes_url_spec_as_dependency_value()
        {
            var manifest = JObject.Parse("{\"dependencies\":{}}");
            var changed = ManifestWriter.TryApply(manifest, new[] {
                new AddGitPackage("com.tenjin.sdk", "https://github.com/CAS-Publishing/tenjin-sdk.git#1.15.14")
            });

            Assert.IsTrue(changed);
            Assert.AreEqual("https://github.com/CAS-Publishing/tenjin-sdk.git#1.15.14",
                manifest["dependencies"]["com.tenjin.sdk"].Value<string>());
        }

        [Test]
        public void AddGitPackage_is_idempotent_when_id_already_present()
        {
            var manifest = JObject.Parse("{\"dependencies\":{\"com.tenjin.sdk\":\"1.15.14\"}}");
            var changed = ManifestWriter.TryApply(manifest, new[] {
                new AddGitPackage("com.tenjin.sdk", "https://github.com/CAS-Publishing/tenjin-sdk.git#1.15.14")
            });

            Assert.IsFalse(changed);
        }

        [Test]
        public void AddGitPackage_empty_spec_writes_nothing()
        {
            var manifest = JObject.Parse("{\"dependencies\":{}}");
            var changed = ManifestWriter.TryApply(manifest, new[] { new AddGitPackage("com.x", "") });
            Assert.IsFalse(changed);
        }
    }
}
