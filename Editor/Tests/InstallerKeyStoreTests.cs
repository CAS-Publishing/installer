using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class InstallerKeyStoreTests
    {
        private const string Comp = "test.component";
        private const string Plat = "Android";

        [SetUp]
        public void Setup() => InstallerKeyStore.Set(Comp, Plat, null);

        [TearDown]
        public void Cleanup() => InstallerKeyStore.Set(Comp, Plat, null);

        [Test]
        public void Set_then_Get_roundtrips_trimmed()
        {
            InstallerKeyStore.Set(Comp, Plat, "  com.app.id  ");
            Assert.AreEqual("com.app.id", InstallerKeyStore.Get(Comp, Plat));
        }

        [Test]
        public void Set_empty_deletes_key()
        {
            InstallerKeyStore.Set(Comp, Plat, "x");
            InstallerKeyStore.Set(Comp, Plat, "");
            // Key is deleted, so GetOrDefault returns the fallback, not "".
            Assert.AreEqual("sentinel", InstallerKeyStore.GetOrDefault(Comp, Plat, "sentinel"));
        }

        [Test]
        public void GetOrDefault_returns_fallback_when_unset()
        {
            Assert.AreEqual("fallback", InstallerKeyStore.GetOrDefault(Comp, Plat, "fallback"));
        }

        [Test]
        public void GetOrDefault_returns_stored_when_set()
        {
            InstallerKeyStore.Set(Comp, Plat, "stored");
            Assert.AreEqual("stored", InstallerKeyStore.GetOrDefault(Comp, Plat, "fallback"));
        }

        [Test]
        public void GetOrDefault_null_fallback_returns_empty()
        {
            Assert.AreEqual("", InstallerKeyStore.GetOrDefault(Comp, Plat, null));
        }
    }
}
