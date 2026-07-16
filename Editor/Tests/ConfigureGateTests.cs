using System.Collections.Generic;
using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class ConfigureGateTests
    {
        private static PlatformReadiness P(string name, bool used, bool ok) =>
            new PlatformReadiness { Platform = name, Used = used, AllOk = ok };

        [Test] public void BothIncomplete_Blocked() =>
            Assert.IsFalse(ConfigureGate.CanContinue(new List<PlatformReadiness>
                { P("Android", true, false), P("iOS", true, false) }));

        [Test] public void OnePlatformReady_Enough() =>
            Assert.IsTrue(ConfigureGate.CanContinue(new List<PlatformReadiness>
                { P("Android", true, true), P("iOS", true, false) }));

        [Test] public void UnusedPlatformDoesNotCount() =>
            Assert.IsFalse(ConfigureGate.CanContinue(new List<PlatformReadiness>
                { P("Android", false, true), P("iOS", true, false) }));
    }
}
