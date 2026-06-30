using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class BuildSwitchPolicyTests
    {
        [Test] public void Open_when_installed_and_unconfigured_null()
            => Assert.IsTrue(BuildSwitchPolicy.ShouldOpenOnSwitch(true, null));

        [Test] public void Open_when_installed_and_unconfigured_empty()
            => Assert.IsTrue(BuildSwitchPolicy.ShouldOpenOnSwitch(true, ""));

        [Test] public void No_open_when_already_configured()
            => Assert.IsFalse(BuildSwitchPolicy.ShouldOpenOnSwitch(true, "1234567890"));

        [Test] public void No_open_when_cas_not_installed()
            => Assert.IsFalse(BuildSwitchPolicy.ShouldOpenOnSwitch(false, null));
    }
}
