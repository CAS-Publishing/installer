using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class CasIdApplierTests
    {
        [Test]
        public void ShouldWrite_false_when_stored_empty()
        {
            Assert.IsFalse(CasIdApplier.ShouldWrite("anything", "", "demo"));
            Assert.IsFalse(CasIdApplier.ShouldWrite("anything", null, "demo"));
        }

        [Test]
        public void ShouldWrite_true_when_current_empty()
        {
            Assert.IsTrue(CasIdApplier.ShouldWrite("", "com.app", "demo"));
            Assert.IsTrue(CasIdApplier.ShouldWrite(null, "com.app", "demo"));
        }

        [Test]
        public void ShouldWrite_true_when_current_is_placeholder()
        {
            Assert.IsTrue(CasIdApplier.ShouldWrite("demo", "com.app", "demo"));
            Assert.IsTrue(CasIdApplier.ShouldWrite("DEMO", "com.app", "demo")); // case-insensitive
        }

        [Test]
        public void ShouldWrite_false_when_current_is_real_value()
        {
            Assert.IsFalse(CasIdApplier.ShouldWrite("com.existing", "com.app", "demo"));
        }
    }
}
