using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class TenjinKeyDetectTests
    {
        [Test]
        public void FieldNames_AreProbedInOrder()
        {
            CollectionAssert.AreEqual(
                new[] { "tenjinKey", "tenjinSdkKey", "tenjinAppKey" },
                TenjinKeyDetect.CandidateFieldNames);
        }

        [Test]
        public void MissingAsset_ReportsUnsupported()
        {
            var probe = TenjinKeyDetect.Probe("NoSuchPlatform");
            Assert.IsFalse(probe.FieldSupported);
            Assert.IsNull(probe.Key);
        }
    }
}
