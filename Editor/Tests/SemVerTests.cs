using NUnit.Framework;
using PSV.Installer.Common;

namespace PSV.Installer.Tests
{
    public sealed class SemVerTests
    {
        // ── IsVersion ──────────────────────────────────────────────
        [TestCase("1.2.3", true)]
        [TestCase("1.0", true)]
        [TestCase("1", true)]
        [TestCase("v1.2.3", true)]
        [TestCase("1.2.3-rc.1", true)]
        [TestCase("1.2.3+build5", true)]
        [TestCase("file:../MyPkg", false)]
        [TestCase("https://github.com/x/y.git#v2", false)]
        [TestCase("git@github.com:x/y.git", false)]
        [TestCase("latest", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void IsVersion_classifies(string input, bool expected)
        {
            Assert.AreEqual(expected, SemVer.IsVersion(input));
        }

        // ── Compare: numeric, not lexical ──────────────────────────
        [Test] public void Compare_equal() => Assert.AreEqual(0, SemVer.Compare("1.0.0", "1.0.0"));
        [Test] public void Compare_numeric_minor() => Assert.Less(SemVer.Compare("1.2.0", "1.10.0"), 0);
        [Test] public void Compare_major() => Assert.Greater(SemVer.Compare("2.0.0", "1.9.9"), 0);
        [Test] public void Compare_pads_two_component() => Assert.AreEqual(0, SemVer.Compare("1.0", "1.0.0"));
        [Test] public void Compare_pads_one_component() => Assert.AreEqual(0, SemVer.Compare("1", "1.0.0"));

        // ── Compare: prerelease & build metadata per semver ────────
        [Test] public void Release_beats_prerelease() => Assert.Greater(SemVer.Compare("1.0.0", "1.0.0-rc.1"), 0);
        [Test] public void Prerelease_numeric_identifiers() => Assert.Less(SemVer.Compare("1.0.0-rc.2", "1.0.0-rc.10"), 0);
        [Test] public void Prerelease_alpha_ordinal() => Assert.Less(SemVer.Compare("1.0.0-alpha", "1.0.0-beta"), 0);
        [Test] public void Build_metadata_ignored() => Assert.AreEqual(0, SemVer.Compare("1.0.0+build9", "1.0.0"));
        [Test] public void Leading_v_stripped() => Assert.AreEqual(0, SemVer.Compare("v1.2.3", "1.2.3"));
        [Test] public void Prerelease_fewer_ids_less_than_more() => Assert.Less(SemVer.Compare("1.0.0-rc", "1.0.0-rc.1"), 0);
        [Test] public void Prerelease_numeric_sorts_below_alpha() => Assert.Less(SemVer.Compare("1.0.0-1", "1.0.0-alpha"), 0);
    }
}
