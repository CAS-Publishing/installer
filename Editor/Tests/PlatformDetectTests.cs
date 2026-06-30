using NUnit.Framework;
using UnityEditor;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class PlatformDetectTests
    {
        [Test] public void iOS_target_maps_to_iOS()
            => Assert.AreEqual("iOS", PlatformDetect.FromBuildTarget(BuildTarget.iOS));

        [Test] public void Android_target_maps_to_Android()
            => Assert.AreEqual("Android", PlatformDetect.FromBuildTarget(BuildTarget.Android));

        [Test] public void Desktop_target_defaults_to_Android()
            => Assert.AreEqual("Android", PlatformDetect.FromBuildTarget(BuildTarget.StandaloneWindows64));
    }
}
