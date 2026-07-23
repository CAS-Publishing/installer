using System.Collections.Generic;
using NUnit.Framework;
using PSV.Installer.Migrator;

namespace PSV.Installer.Tests
{
    public class RequirementGateTests
    {
        private static readonly Dictionary<string, string> NoDeps = new Dictionary<string, string>();

        [Test] public void NullRequires_IsSatisfied() =>
            Assert.IsNull(RequirementGate.FirstMissing(null, NoDeps, _ => false));

        [Test] public void RequirementInManifest_IsSatisfied() =>
            Assert.IsNull(RequirementGate.FirstMissing(new[] { "com.psv.core" },
                new Dictionary<string, string> { { "com.psv.core", "https://gitlab/x.git" } }, _ => false));

        [Test] public void RequirementEmbedded_IsSatisfied() =>
            Assert.IsNull(RequirementGate.FirstMissing(new[] { "com.psv.core" }, NoDeps,
                id => id == "com.psv.core"));

        [Test] public void RequirementAbsent_ReturnsMissingId() =>
            Assert.AreEqual("com.psv.core",
                RequirementGate.FirstMissing(new[] { "com.psv.core" }, NoDeps, _ => false));
    }
}
