using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class DoneChecklistModelTests
    {
        [Test]
        public void CasLine_WithId_IncludesParenthetical()
        {
            var line = DoneChecklistModel.CasLine("abc123");
            Assert.AreEqual("✓ CAS SDK — mediation ready (abc123)", line.Text);
            Assert.IsFalse(line.Warn);
        }

        [Test]
        public void CasLine_NullOrEmpty_OmitsParenthetical()
        {
            Assert.AreEqual("✓ CAS SDK — mediation ready", DoneChecklistModel.CasLine(null).Text);
            Assert.AreEqual("✓ CAS SDK — mediation ready", DoneChecklistModel.CasLine("").Text);
        }

        [Test]
        public void TenjinLine_FieldNotSupported_IsInformationalGreen()
        {
            var line = DoneChecklistModel.TenjinLine(fieldSupported: false, key: null);
            Assert.AreEqual("✓ Tenjin — handled on our end", line.Text);
            Assert.IsFalse(line.Warn);
        }

        [Test]
        public void TenjinLine_SupportedWithKey_IsGreen()
        {
            var line = DoneChecklistModel.TenjinLine(fieldSupported: true, key: "tenjin-key");
            Assert.AreEqual("✓ Tenjin — attribution key configured", line.Text);
            Assert.IsFalse(line.Warn);
        }

        [Test]
        public void TenjinLine_SupportedButEmpty_WarnsYellow()
        {
            var line = DoneChecklistModel.TenjinLine(fieldSupported: true, key: "");
            Assert.AreEqual("⚠ Tenjin — attribution key missing", line.Text);
            Assert.IsTrue(line.Warn);
        }

        [Test]
        public void FirebaseLine_Configured_IsGreen()
        {
            var line = DoneChecklistModel.FirebaseLine(true);
            Assert.AreEqual("✓ Firebase — analytics connected", line.Text);
            Assert.IsFalse(line.Warn);
        }

        [Test]
        public void FirebaseLine_NotConfigured_WarnsYellow()
        {
            var line = DoneChecklistModel.FirebaseLine(false);
            Assert.AreEqual("⚠ Firebase — configuration file missing", line.Text);
            Assert.IsTrue(line.Warn);
        }
    }
}
