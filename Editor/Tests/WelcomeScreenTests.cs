using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class WelcomeScreenTests
    {
        [Test]
        public void ResolveSeed_ignores_stored_value()
        {
            // A previously-typed-but-unapplied id must NOT repopulate the field (feedback #2).
            Assert.AreEqual("", WelcomeScreen.ResolveSeed("previously-typed", null));
        }

        [Test]
        public void ResolveSeed_uses_existing_cas_id()
        {
            // A real, already-configured CAS managerId IS prefilled (feedback #2.2).
            Assert.AreEqual("real-cas-id", WelcomeScreen.ResolveSeed("anything", "real-cas-id"));
        }

        [Test]
        public void ResolveSeed_empty_when_no_existing()
        {
            Assert.AreEqual("", WelcomeScreen.ResolveSeed(null, null));
        }
    }
}
