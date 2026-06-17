using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    /// <summary>
    /// Covers the pure Resources-folder detection used by SetupChecker.LocateAsset's by-name fallback.
    /// A Resources-loaded settings asset (e.g. CAS) can be relocated to ANY Resources folder and the SDK
    /// still finds it via Resources.Load — so the fallback restricts its by-name search to Resources
    /// folders, mirroring runtime behaviour and avoiding false positives from stray copies elsewhere.
    /// </summary>
    public class SetupCheckerTests
    {
        [Test]
        public void IsUnderResources_true_for_resources_segment()
        {
            Assert.IsTrue(SetupChecker.IsUnderResources("Assets/CleverAdsSolutions/Resources/CASSettingsAndroid.asset"));
            Assert.IsTrue(SetupChecker.IsUnderResources("Assets/Resources/X.asset"));
            Assert.IsTrue(SetupChecker.IsUnderResources("Assets/Foo/Resources/Sub/X.asset"));
        }

        [Test]
        public void IsUnderResources_false_when_no_resources_folder()
        {
            Assert.IsFalse(SetupChecker.IsUnderResources("Assets/Foo/Bar/X.asset"));
            // Segment boundary: "MyResources" is a different folder, not a Resources special folder.
            Assert.IsFalse(SetupChecker.IsUnderResources("Assets/MyResources/X.asset"));
            // Unity treats only an exactly-cased "Resources" folder as special.
            Assert.IsFalse(SetupChecker.IsUnderResources("Assets/resources/X.asset"));
        }

        [Test]
        public void IsUnderResources_false_on_null_or_empty()
        {
            Assert.IsFalse(SetupChecker.IsUnderResources(null));
            Assert.IsFalse(SetupChecker.IsUnderResources(""));
        }
    }
}
