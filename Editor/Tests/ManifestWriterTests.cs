using System.IO;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using PSV.Installer.Migrator;

namespace PSV.Installer.Tests
{
    public sealed class ManifestWriterTests
    {
        [Test]
        public void Add_is_idempotent_case_insensitively()
        {
            var m = JObject.Parse("{ \"dependencies\": { \"Com.X\": \"1.0.0\" } }");
            var modified = ManifestWriter.TryApply(m, new MigrationAction[] { new AddPackage("com.x", "2.0.0") });
            Assert.IsFalse(modified, "an existing dependency differing only in case must not be re-added");
            Assert.AreEqual(1, ((JObject)m["dependencies"]).Count, "no duplicate entry");
        }

        [Test]
        public void Add_new_package_inserts_entry()
        {
            var m = JObject.Parse("{ \"dependencies\": {} }");
            var modified = ManifestWriter.TryApply(m, new MigrationAction[] { new AddPackage("com.x", "1.0.0") });
            Assert.IsTrue(modified);
            Assert.AreEqual("1.0.0", (string)m["dependencies"]["com.x"]);
        }

        [Test]
        public void Remove_package_drops_entry()
        {
            var m = JObject.Parse("{ \"dependencies\": { \"com.x\": \"1.0.0\" } }");
            var modified = ManifestWriter.TryApply(m, new MigrationAction[] { new RemovePackage("com.x") });
            Assert.IsTrue(modified);
            Assert.IsNull(m["dependencies"]["com.x"]);
        }

        [Test]
        public void Registry_match_ignores_trailing_slash()
        {
            // Existing registry has NO trailing slash; action URL HAS one → must merge, not duplicate.
            var m = JObject.Parse(
                "{ \"scopedRegistries\": [ { \"name\": \"PSV\", \"url\": \"https://npm.psvgamestudio.com\", \"scopes\": [\"com.a\"] } ] }");
            var modified = ManifestWriter.TryApply(m, new MigrationAction[] {
                new AddScopedRegistry("PSV", "https://npm.psvgamestudio.com/", "com.b") });

            Assert.IsTrue(modified);
            var regs = (JArray)m["scopedRegistries"];
            Assert.AreEqual(1, regs.Count, "must not create a second registry for the same URL modulo trailing slash");
            var scopes = (JArray)regs[0]["scopes"];
            CollectionAssert.AreEquivalent(new[] { "com.a", "com.b" }, scopes.ToObject<string[]>());
        }

        [Test]
        public void New_registry_added_when_url_absent()
        {
            var m = JObject.Parse("{ }");
            var modified = ManifestWriter.TryApply(m, new MigrationAction[] {
                new AddScopedRegistry("PSV", "https://npm.psvgamestudio.com/", "com.a") });
            Assert.IsTrue(modified);
            Assert.AreEqual(1, ((JArray)m["scopedRegistries"]).Count);
        }

        [Test]
        public void Update_changes_existing_version()
        {
            var m = JObject.Parse("{ \"dependencies\": { \"com.x\": \"1.0.0\" } }");
            var modified = ManifestWriter.TryApply(m, new MigrationAction[] { new UpdatePackageVersion("com.x", "2.0.0") });
            Assert.IsTrue(modified);
            Assert.AreEqual("2.0.0", (string)m["dependencies"]["com.x"]);
        }

        [Test]
        public void Update_missing_id_is_noop()
        {
            var m = JObject.Parse("{ \"dependencies\": { \"com.x\": \"1.0.0\" } }");
            var modified = ManifestWriter.TryApply(m, new MigrationAction[] { new UpdatePackageVersion("com.absent", "2.0.0") });
            Assert.IsFalse(modified, "update must not create a missing dependency");
            Assert.IsNull(m["dependencies"]["com.absent"]);
        }

        [Test]
        public void All_noop_returns_false()
        {
            var m = JObject.Parse("{ \"dependencies\": { \"com.x\": \"1.0.0\" } }");
            var modified = ManifestWriter.TryApply(m, new MigrationAction[] { new AddPackage("com.x", "9.9.9") }); // already present
            Assert.IsFalse(modified);
            Assert.AreEqual("1.0.0", (string)m["dependencies"]["com.x"]);
        }

        [Test]
        public void Empty_version_add_is_noop()
        {
            var m = JObject.Parse("{ \"dependencies\": {} }");
            var modified = ManifestWriter.TryApply(m, new MigrationAction[] { new AddPackage("com.x", "") });
            Assert.IsFalse(modified);
            Assert.IsNull(m["dependencies"]["com.x"]);
        }

        [Test]
        public void ApplyActions_throws_on_missing_manifest()
        {
            var p = Path.Combine(Path.GetTempPath(), "psvmw_" + Path.GetRandomFileName(), "manifest.json");
            Assert.Throws<System.InvalidOperationException>(() =>
                ManifestWriter.ApplyActions(p, new MigrationAction[] { new AddPackage("com.x", "1.0.0") }));
        }

        [Test]
        public void ApplyActions_throws_on_malformed_manifest()
        {
            var dir = Path.Combine(Path.GetTempPath(), "psvmw_" + Path.GetRandomFileName());
            Directory.CreateDirectory(dir);
            var p = Path.Combine(dir, "manifest.json");
            File.WriteAllText(p, "{ this is not json");
            try
            {
                Assert.Throws<System.InvalidOperationException>(() =>
                    ManifestWriter.ApplyActions(p, new MigrationAction[] { new AddPackage("com.x", "1.0.0") }));
            }
            finally { Directory.Delete(dir, true); }
        }
    }
}
