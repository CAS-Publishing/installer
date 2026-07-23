using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class CasNativeSettingsMenuTests
    {
        [Test]
        public void Android_YieldsEllipsisThenPlain()
        {
            var c = CasNativeSettings.MenuCandidates("Android");
            Assert.AreEqual(new[]
            {
                "Assets/CleverAdsSolutions/Android Settings...",
                "Assets/CleverAdsSolutions/Android Settings",
            }, c);
        }

        [Test]
        public void Ios_YieldsEllipsisThenPlain()
        {
            var c = CasNativeSettings.MenuCandidates("iOS");
            Assert.AreEqual(new[]
            {
                "Assets/CleverAdsSolutions/iOS Settings...",
                "Assets/CleverAdsSolutions/iOS Settings",
            }, c);
        }
    }
}
