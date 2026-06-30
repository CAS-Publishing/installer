using NUnit.Framework;
using PSV.Installer.Wizard;
using PSV.Installer.Scanner;

namespace PSV.Installer.Tests
{
    public class GitSourceStatusTests
    {
        private static readonly (string, string, string, string) Cas =
            ("com.cleversolutions.ads.unity", "CAS SDK", "Ads", "cas");

        [Test]
        public void Git_external_offers_switch_to_upm()
        {
            var e = new ExternalScanResult("com.cleversolutions.ads.unity", "CAS SDK",
                ExternalState.UpmCurrent, "https://github.com/cleveradssolutions/CAS-Unity.git#4.7.4");
            var s = ComponentStatusProvider.FromExternal(Cas, e);
            Assert.IsTrue(s.GitInstalled);
            Assert.AreEqual("Installed (git)", s.StatusText);
            Assert.AreEqual("Switch to UPM", s.ActionText);
            Assert.IsTrue(s.Actionable);
        }

        [Test]
        public void Registry_external_is_up_to_date()
        {
            var e = new ExternalScanResult("com.cleversolutions.ads.unity", "CAS SDK",
                ExternalState.UpmCurrent, "4.7.4");
            var s = ComponentStatusProvider.FromExternal(Cas, e);
            Assert.IsFalse(s.GitInstalled);
            Assert.AreEqual("Installed", s.StatusText);
            Assert.AreEqual("Up to date", s.ActionText);
            Assert.IsFalse(s.Actionable);
        }

        [Test] public void FriendlyVersion_git_is_git()
            => Assert.AreEqual("git", ComponentsScreen.FriendlyVersion("https://x/y.git#1.2.3"));

        [Test] public void FriendlyVersion_file_is_local()
            => Assert.AreEqual("local", ComponentsScreen.FriendlyVersion("file:../x"));

        [Test] public void FriendlyVersion_semver_passthrough()
            => Assert.AreEqual("4.7.4", ComponentsScreen.FriendlyVersion("4.7.4"));
    }
}
