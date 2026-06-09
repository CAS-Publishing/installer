using System.IO;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
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
        public void Trailing_comma_is_tolerated()
        {
            // Newtonsoft is lenient about trailing commas; for a hand-edited manifest
            // that leniency is desirable — we read the correct data rather than failing.
            var p = Write("manifest.json", "{ \"dependencies\": { \"com.x\": \"1.0.0\", } }");
            var r = ManifestIO.Read(p);
            Assert.AreEqual(ManifestReadStatus.Ok, r.Status);
            Assert.AreEqual("1.0.0", (string)r.Root["dependencies"]["com.x"]);
        }

        [Test]
        public void Unterminated_json_reports_ParseError()
        {
            // A genuinely broken manifest (missing closing brace) must surface as a
            // typed ParseError, never a silent empty/Ok result.
            var p = Write("manifest.json", "{ \"dependencies\": { \"com.x\": \"1.0.0\" }");
            var r = ManifestIO.Read(p);
            Assert.AreEqual(ManifestReadStatus.ParseError, r.Status);
            Assert.IsNull(r.Root);
            Assert.IsNotEmpty(r.Error);
        }

        [Test]
        public void Array_root_reports_ParseError()
        {
            var p = Write("manifest.json", "[]");
            var r = ManifestIO.Read(p);
            Assert.AreEqual(ManifestReadStatus.ParseError, r.Status);
            Assert.IsNotEmpty(r.Error);
        }
    }

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
            Assert.IsFalse(File.Exists(p + ".tmp"), "temp file must be gone after first-write");
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
