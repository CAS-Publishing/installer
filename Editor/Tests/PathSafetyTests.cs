using System.IO;
using NUnit.Framework;
using PSV.Installer.Common;

namespace PSV.Installer.Tests
{
    public sealed class PathSafetyTests
    {
        private static string Root => Path.Combine(Path.GetTempPath(), "psvroot", "Assets");

        [Test]
        public void Contained_subpath_resolves()
        {
            var ok = PathSafety.TryResolveContained(Root, "Plugins/Foo", out var resolved, out var err);
            Assert.IsTrue(ok, err);
            StringAssert.EndsWith("Assets/Plugins/Foo", resolved.Replace('\\', '/'));
        }

        [Test]
        public void Dot_segments_inside_are_allowed()
        {
            var ok = PathSafety.TryResolveContained(Root, "Plugins/./Bar", out var resolved, out _);
            Assert.IsTrue(ok);
            StringAssert.EndsWith("Assets/Plugins/Bar", resolved.Replace('\\', '/'));
        }

        [TestCase("../Outside")]
        [TestCase("../../etc")]
        [TestCase("Plugins/../../../etc")]
        [TestCase("Plugins\\..\\..\\etc")]
        public void Escaping_paths_are_rejected(string rel)
        {
            var ok = PathSafety.TryResolveContained(Root, rel, out var resolved, out var err);
            Assert.IsFalse(ok);
            Assert.IsNull(resolved);
            Assert.IsNotEmpty(err);
        }

        [Test]
        public void Rooted_absolute_path_is_rejected()
        {
            var ok = PathSafety.TryResolveContained(Root, Path.GetTempPath(), out _, out var err);
            Assert.IsFalse(ok);
            Assert.IsNotEmpty(err);
        }

        [Test]
        public void Empty_relative_is_rejected()
        {
            Assert.IsFalse(PathSafety.TryResolveContained(Root, "", out _, out _));
            Assert.IsFalse(PathSafety.TryResolveContained(Root, "   ", out _, out _));
        }

        [Test]
        public void Root_itself_is_rejected()
        {
            Assert.IsFalse(PathSafety.TryResolveContained(Root, ".", out _, out var err));
            Assert.IsNotEmpty(err);
        }

        [Test]
        public void Sibling_prefix_path_is_rejected()
        {
            // root ".../Assets" must not match a sibling ".../AssetsExtra".
            var ok = PathSafety.TryResolveContained(Root, "../AssetsExtra/x", out var resolved, out var err);
            Assert.IsFalse(ok);
            Assert.IsNull(resolved);
            Assert.IsNotEmpty(err);
        }
    }
}
