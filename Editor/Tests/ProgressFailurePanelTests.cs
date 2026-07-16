using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class ProgressFailurePanelTests
    {
        [Test]
        public void FailureModel_CarriesStepAndHint()
        {
            var m = ProgressFailureModel.From("Firebase Analytics", "Could not resolve package.");
            Assert.AreEqual("Firebase Analytics — installation failed", m.Title);
            StringAssert.Contains("Check your internet connection", m.Message);
        }

        [Test]
        public void FailureModel_LogCarriesRawError()
        {
            // Log is what "Copy log" puts on the clipboard — the raw error, not the
            // user-facing hint appended to Message.
            var m = ProgressFailureModel.From("Tenjin SDK", "Network unreachable.");
            Assert.AreEqual("Network unreachable.", m.Log);
        }
    }
}
