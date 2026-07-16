using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class ComponentStatusProviderDefaultsTests
    {
        [Test]
        public void DefaultIds_ContainsEdm4uLast()
        {
            var ids = ComponentStatusProvider.DefaultIds;
            Assert.AreEqual(4, ids.Count);
            Assert.AreEqual("com.google.external-dependency-manager", ids[3]);
        }

        [Test]
        public void Edm4uDisplay_IsMapped()
        {
            Assert.IsTrue(ComponentStatusProvider.TryGetDefaultDisplay(
                "com.google.external-dependency-manager", out var name, out _));
            Assert.AreEqual("External Dependency Manager (EDM4U)", name);
        }
    }
}
