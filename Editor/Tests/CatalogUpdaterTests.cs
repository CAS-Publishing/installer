using NUnit.Framework;
using PSV.Installer.Catalog;

namespace PSV.Installer.Tests
{
    public sealed class CatalogUpdaterTests
    {
        [Test] public void Newer_patch() => Assert.IsTrue(CatalogUpdater.IsNewer("1.0.1", "1.0.0"));
        [Test] public void Not_newer_lower() => Assert.IsFalse(CatalogUpdater.IsNewer("1.0.0", "1.0.1"));
        [Test] public void Not_newer_equal() => Assert.IsFalse(CatalogUpdater.IsNewer("1.0.0", "1.0.0"));
        [Test] public void Release_newer_than_its_prerelease() => Assert.IsTrue(CatalogUpdater.IsNewer("1.0.0", "1.0.0-rc.1"));
        [Test] public void Numeric_prerelease_order() => Assert.IsTrue(CatalogUpdater.IsNewer("1.0.0-rc.10", "1.0.0-rc.2"));
        [Test] public void Null_or_empty_guard() => Assert.IsFalse(CatalogUpdater.IsNewer("", "1.0.0"));
        // Regression: a non-version local spec must NOT be treated as up-to-date/newer.
        [Test] public void Non_version_local_not_newer() => Assert.IsFalse(CatalogUpdater.IsNewer("file:../x", "1.0.0"));
        [Test] public void Remote_version_newer_than_non_version_local() => Assert.IsTrue(CatalogUpdater.IsNewer("2.0.0", "file:../x"));
    }
}
