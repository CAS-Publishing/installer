using System.Collections.Generic;
using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class ReadyModelTests
    {
        private static ComponentStatus S(string name, bool installed, string version) =>
            new ComponentStatus { Id = name, DisplayName = name, Installed = installed, Version = version };

        [Test]
        public void MixedInstall_PrimaryIsInstall_RowsShowState()
        {
            var m = ReadyModel.Build(new List<ComponentStatus>
                { S("CAS SDK", false, null), S("Tenjin SDK", true, "1.19.3") });
            Assert.AreEqual("Install", m.PrimaryButtonText);
            Assert.IsFalse(m.AllInstalled);
            Assert.IsFalse(m.Rows[0].AlreadyInstalled);
            Assert.AreEqual("✓ Already installed (1.19.3)", m.Rows[1].RightText);
        }

        [Test]
        public void AllInstalled_PrimaryIsContinue()
        {
            var m = ReadyModel.Build(new List<ComponentStatus>
                { S("CAS SDK", true, "4.0.0"), S("Tenjin SDK", true, "1.19.3") });
            Assert.AreEqual("Continue", m.PrimaryButtonText);
            Assert.IsTrue(m.AllInstalled);
        }
    }
}
