using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class AdFlagsBitsTests
    {
        [Test] public void Values() { Assert.AreEqual(1, AdFlagsBits.Banner); Assert.AreEqual(2, AdFlagsBits.Interstitial); Assert.AreEqual(4, AdFlagsBits.Rewarded); Assert.AreEqual(8, AdFlagsBits.AppOpen); }
        [Test] public void HasFlag_true() => Assert.IsTrue(AdFlagsBits.HasFlag(AdFlagsBits.Banner | AdFlagsBits.Rewarded, AdFlagsBits.Rewarded));
        [Test] public void HasFlag_false() => Assert.IsFalse(AdFlagsBits.HasFlag(AdFlagsBits.Banner, AdFlagsBits.Rewarded));
        [Test] public void WithFlag_set() => Assert.AreEqual(AdFlagsBits.Banner | AdFlagsBits.Rewarded, AdFlagsBits.WithFlag(AdFlagsBits.Banner, AdFlagsBits.Rewarded, true));
        [Test] public void WithFlag_clear() => Assert.AreEqual(AdFlagsBits.Banner, AdFlagsBits.WithFlag(AdFlagsBits.Banner | AdFlagsBits.Rewarded, AdFlagsBits.Rewarded, false));
        [Test] public void WithFlag_clear_absent_noop() => Assert.AreEqual(AdFlagsBits.Banner, AdFlagsBits.WithFlag(AdFlagsBits.Banner, AdFlagsBits.Rewarded, false));
    }
}
