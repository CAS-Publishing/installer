using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class SetupScreenPlatformTests
    {
        [Test] public void iOS_picks_ios_value()
            => Assert.AreEqual("I", SetupScreen.PickForPlatform("A", "I", "iOS"));

        [Test] public void Android_picks_android_value()
            => Assert.AreEqual("A", SetupScreen.PickForPlatform("A", "I", "Android"));

        [Test] public void Unknown_platform_defaults_to_android_value()
            => Assert.AreEqual("A", SetupScreen.PickForPlatform("A", "I", "Whatever"));
    }
}
