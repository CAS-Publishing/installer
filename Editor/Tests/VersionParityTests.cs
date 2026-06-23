using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    /// <summary>
    /// Version parity on migrate: when a manual (Assets) SDK copy reports a version NEWER than the
    /// catalog pin, migrating to UPM must install THAT exact version (just changing the source), never
    /// silently downgrade. The classic case is Firebase 13.6.0 on disk vs a 13.1.0 catalog pin.
    /// </summary>
    public class VersionParityTests
    {
        [Test]
        public void EffectiveVersion_prefers_newer_onDisk_for_parity()
        {
            // Firebase on disk 13.6.0, catalog pin 13.1.0 → install 13.6.0 (no downgrade).
            Assert.AreEqual("13.6.0", WizardActions.ResolveEffectiveVersion("13.1.0", "13.6.0"));
        }

        [Test]
        public void EffectiveVersion_keeps_base_when_onDisk_not_newer()
        {
            // Pin is the floor: an older or equal on-disk version never drags the install below the pin.
            Assert.AreEqual("13.1.0", WizardActions.ResolveEffectiveVersion("13.1.0", "13.0.0"));
            Assert.AreEqual("13.1.0", WizardActions.ResolveEffectiveVersion("13.1.0", "13.1.0"));
        }

        [Test]
        public void EffectiveVersion_falls_back_to_base_when_onDisk_missing_or_invalid()
        {
            Assert.AreEqual("13.1.0", WizardActions.ResolveEffectiveVersion("13.1.0", null));
            Assert.AreEqual("13.1.0", WizardActions.ResolveEffectiveVersion("13.1.0", ""));
            Assert.AreEqual("13.1.0", WizardActions.ResolveEffectiveVersion("13.1.0", "not-a-version"));
        }
    }
}
