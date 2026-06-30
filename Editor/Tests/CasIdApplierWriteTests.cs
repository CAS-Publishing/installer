using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class CasIdApplierWriteTests
    {
        [Test] public void Normalize_trims() => Assert.AreEqual("abc", CasIdApplier.NormalizeManagerId("  abc  "));
        [Test] public void Normalize_null_is_empty() => Assert.AreEqual("", CasIdApplier.NormalizeManagerId(null));
        [Test] public void Normalize_keeps_inner() => Assert.AreEqual("1234567890", CasIdApplier.NormalizeManagerId("1234567890"));
    }
}
