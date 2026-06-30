using NUnit.Framework;
using PSV.Installer.Scanner;

namespace PSV.Installer.Tests
{
    public class PluginFileMatchTests
    {
        [Test] public void Exact_match() => Assert.IsTrue(AssetInstallProbe.MatchesFilePattern("libFirebaseCppApp.a", "libFirebaseCppApp.a"));
        [Test] public void Exact_mismatch() => Assert.IsFalse(AssetInstallProbe.MatchesFilePattern("libOther.a", "libFirebaseCppApp.a"));
        [Test] public void Prefix_wildcard() => Assert.IsTrue(AssetInstallProbe.MatchesFilePattern("libFirebaseCppAnalytics.a", "libFirebaseCpp*"));
        [Test] public void Suffix_wildcard() => Assert.IsTrue(AssetInstallProbe.MatchesFilePattern("libFirebaseCppApp.a", "*.a"));
        [Test] public void Mid_wildcard() => Assert.IsTrue(AssetInstallProbe.MatchesFilePattern("libFirebaseCppApp.a", "libFirebase*App.a"));
        [Test] public void Wildcard_no_false_positive() => Assert.IsFalse(AssetInstallProbe.MatchesFilePattern("libUnrelated.a", "libFirebaseCpp*"));
        [Test] public void Case_insensitive() => Assert.IsTrue(AssetInstallProbe.MatchesFilePattern("LIBFIREBASECPPAPP.A", "libFirebaseCppApp.a"));
        [Test] public void Null_safe() => Assert.IsFalse(AssetInstallProbe.MatchesFilePattern(null, "x"));

        // IsSafePluginPattern — the deletion-safety gate that rejects over-broad patterns.
        [Test] public void Safe_exact_name() => Assert.IsTrue(AssetInstallProbe.IsSafePluginPattern("libFirebaseCppApp.a"));
        [Test] public void Safe_long_prefix_wildcard() => Assert.IsTrue(AssetInstallProbe.IsSafePluginPattern("libFirebaseCpp*"));
        [Test] public void Unsafe_bare_star() => Assert.IsFalse(AssetInstallProbe.IsSafePluginPattern("*"));
        [Test] public void Unsafe_extension_glob() => Assert.IsFalse(AssetInstallProbe.IsSafePluginPattern("*.a"));
        [Test] public void Unsafe_short_prefix() => Assert.IsFalse(AssetInstallProbe.IsSafePluginPattern("lib*"));
        [Test] public void Unsafe_two_stars() => Assert.IsFalse(AssetInstallProbe.IsSafePluginPattern("libFirebase*App*"));
        [Test] public void Unsafe_null() => Assert.IsFalse(AssetInstallProbe.IsSafePluginPattern(null));
    }
}
