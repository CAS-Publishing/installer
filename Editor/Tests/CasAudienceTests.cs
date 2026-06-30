using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class CasAudienceTests
    {
        [Test] public void Families_is_children() => Assert.AreEqual(CasAudience.Children, CasAudience.ForFamilies(true));
        [Test] public void Optimal_is_notchildren() => Assert.AreEqual(CasAudience.NotChildren, CasAudience.ForFamilies(false));
        [Test] public void IsFamilies_children_true() => Assert.IsTrue(CasAudience.IsFamilies(CasAudience.Children));
        [Test] public void IsFamilies_notchildren_false() => Assert.IsFalse(CasAudience.IsFamilies(CasAudience.NotChildren));
        [Test] public void IsFamilies_mixed_false() => Assert.IsFalse(CasAudience.IsFamilies(CasAudience.Mixed));
    }
}
