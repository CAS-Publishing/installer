using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class WelcomeScreenTests
    {
        [Test]
        public void CanProceed_false_when_both_empty()
        {
            Assert.IsFalse(WelcomeScreen.CanProceed(null, null));
            Assert.IsFalse(WelcomeScreen.CanProceed("", ""));
            Assert.IsFalse(WelcomeScreen.CanProceed("   ", "  ")); // whitespace-only doesn't count
        }

        [Test]
        public void CanProceed_true_when_android_only()
        {
            Assert.IsTrue(WelcomeScreen.CanProceed("com.app.android", ""));
        }

        [Test]
        public void CanProceed_true_when_ios_only()
        {
            Assert.IsTrue(WelcomeScreen.CanProceed(null, "com.app.ios"));
        }

        [Test]
        public void CanProceed_true_when_both()
        {
            Assert.IsTrue(WelcomeScreen.CanProceed("a", "b"));
        }
    }
}
