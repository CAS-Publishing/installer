using System.IO;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
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

        private static ManifestData FromJson(string json) => ManifestData.FromJObject(JObject.Parse(json));

        [Test]
        public void HasScopeCovering_ExactId_True()
        {
            var m = FromJson(@"{ ""dependencies"": {}, ""scopedRegistries"": [
                { ""name"": ""psv"", ""url"": ""https://npm.psvgamestudio.com/"", ""scopes"": [""com.psvgamestudio.analytics""] } ] }");
            Assert.IsTrue(m.HasScopeCovering("com.psvgamestudio.analytics"));
        }

        [Test]
        public void HasScopeCovering_PrefixScope_True()
        {
            var m = FromJson(@"{ ""dependencies"": {}, ""scopedRegistries"": [
                { ""name"": ""psv"", ""url"": ""https://npm.psvgamestudio.com/"", ""scopes"": [""com.psvgamestudio""] } ] }");
            Assert.IsTrue(m.HasScopeCovering("com.psvgamestudio.analytics"));
        }

        [Test]
        public void HasScopeCovering_UnrelatedPrefix_False()
        {
            // "com.psv" scope must NOT cover "com.psvgamestudio.analytics" (not a dot-boundary prefix).
            var m = FromJson(@"{ ""dependencies"": {}, ""scopedRegistries"": [
                { ""name"": ""psv"", ""url"": ""https://npm.psvgamestudio.com/"", ""scopes"": [""com.psv""] } ] }");
            Assert.IsFalse(m.HasScopeCovering("com.psvgamestudio.analytics"));
        }

        [Test]
        public void HasScopeCovering_NoRegistries_False()
        {
            var m = FromJson(@"{ ""dependencies"": {} }");
            Assert.IsFalse(m.HasScopeCovering("com.psvgamestudio.analytics"));
        }
    }
}
