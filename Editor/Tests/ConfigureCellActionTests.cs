using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class ConfigureCellActionTests
    {
        private const string Cas = "com.cleversolutions.ads.unity";
        private const string Firebase = "com.google.firebase.analytics";
        private const string Tenjin = "com.tenjin.sdk";

        [Test] public void Cas_AnyState_OpensCasSettings()
        {
            Assert.AreEqual(ConfigCellAction.OpenCasSettings, ConfigureScreen.ResolveCellAction(Cas, true));
            Assert.AreEqual(ConfigCellAction.OpenCasSettings, ConfigureScreen.ResolveCellAction(Cas, false));
        }

        [Test] public void Tenjin_AnyState_OpensCasSettings()
        {
            Assert.AreEqual(ConfigCellAction.OpenCasSettings, ConfigureScreen.ResolveCellAction(Tenjin, true));
            Assert.AreEqual(ConfigCellAction.OpenCasSettings, ConfigureScreen.ResolveCellAction(Tenjin, false));
        }

        [Test] public void Firebase_ConfiguredPings_MissingLocates()
        {
            Assert.AreEqual(ConfigCellAction.PingFirebaseFile,   ConfigureScreen.ResolveCellAction(Firebase, true));
            Assert.AreEqual(ConfigCellAction.LocateFirebaseFile, ConfigureScreen.ResolveCellAction(Firebase, false));
        }

        [Test] public void UnknownRow_None()
        {
            Assert.AreEqual(ConfigCellAction.None, ConfigureScreen.ResolveCellAction("com.other.sdk", true));
        }
    }
}
