using NUnit.Framework;
using PSV.Installer;

namespace PSV.Installer.Tests
{
    public class MetadataInstallRetryTests
    {
        [SetUp] public void SetUp() => MetadataInstallRetry.ResetForTests();
        [TearDown] public void TearDown() => MetadataInstallRetry.ResetForTests();

        [Test]
        public void Arm_SetsArmedAndCountsAttempt()
        {
            MetadataInstallRetry.Arm();
            Assert.IsTrue(MetadataInstallRetry.IsArmed);
            Assert.AreEqual(1, MetadataInstallRetry.Attempts);
        }

        [Test]
        public void Arm_WhileArmed_DoesNotDoubleCount()
        {
            MetadataInstallRetry.Arm();
            MetadataInstallRetry.Arm();
            Assert.AreEqual(1, MetadataInstallRetry.Attempts);
        }

        [Test]
        public void Arm_StopsAtMaxAttempts()
        {
            for (var i = 0; i < 10; i++)
            {
                MetadataInstallRetry.Arm();
                MetadataInstallRetry.DisarmForTests();
            }
            Assert.AreEqual(5, MetadataInstallRetry.Attempts);
            MetadataInstallRetry.Arm();
            Assert.IsFalse(MetadataInstallRetry.IsArmed);
        }
    }
}
