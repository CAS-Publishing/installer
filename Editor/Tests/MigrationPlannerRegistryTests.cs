using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PSV.Installer.Catalog;
using PSV.Installer.Common;
using PSV.Installer.Migrator;
using PSV.Installer.Scanner;

namespace PSV.Installer.Tests
{
    public class MigrationPlannerRegistryTests
    {
        private sealed class All : ISelectionSet
        {
            public bool IsSelected(string id) => true;
            public VersionTarget GetTarget(string id) => VersionTarget.Recommended;
        }

        private static PackageCatalog CatalogWith(PackageRecord rec) => new PackageCatalog
        {
            Registries = new Dictionary<string, string> { { "psv", "https://npm.psvgamestudio.com/" } },
            Packages = new List<PackageRecord> { rec },
        };

        private static IReadOnlyList<MigrationAction> PlanFor(PackageRecord rec, PackageState state)
        {
            var catalog = CatalogWith(rec);
            var report = ScanReportFactory.With(
                new[] { ScanReportFactory.Pkg(rec.Id, state) });

            return MigrationPlanner.Plan(catalog, report, new All(), InstallMethod.Upm, out _);
        }

        [Test]
        public void NotInstalled_EmitsRegistryBeforeAdd()
        {
            var rec = new PackageRecord { Id = "com.psvgamestudio.analytics", Registry = "psv", RecommendedVersion = "0.0.1-preview.3" };
            var plan = PlanFor(rec, PackageState.NotInstalled);
            var reg = plan.OfType<AddScopedRegistry>().Single();
            Assert.AreEqual("https://npm.psvgamestudio.com/", reg.Url);
            Assert.AreEqual("com.psvgamestudio.analytics", reg.Scope); // default scope = record id
            Assert.Less(plan.ToList().IndexOf(reg), plan.ToList().IndexOf(plan.OfType<AddPackage>().Single()));
        }

        [Test]
        public void ExplicitScopes_AreUsedVerbatim()
        {
            var rec = new PackageRecord { Id = "com.psvgamestudio.analytics", Registry = "psv",
                RecommendedVersion = "0.0.1-preview.3", Scopes = new List<string> { "com.psvgamestudio" } };
            var plan = PlanFor(rec, PackageState.NotInstalled);
            Assert.AreEqual("com.psvgamestudio", plan.OfType<AddScopedRegistry>().Single().Scope);
        }

        [Test]
        public void ScopeMissing_EmitsRegistryOnly_NoAdd()
        {
            var rec = new PackageRecord { Id = "com.psvgamestudio.analytics", Registry = "psv", RecommendedVersion = "0.0.1-preview.3" };
            var plan = PlanFor(rec, PackageState.ScopeMissing);
            Assert.IsTrue(plan.OfType<AddScopedRegistry>().Any());
            Assert.IsFalse(plan.OfType<AddPackage>().Any());
            Assert.IsFalse(plan.OfType<UpdatePackageVersion>().Any());
        }

        [Test]
        public void MissingRegistryKey_NoRegistryAction_NoCrash()
        {
            var rec = new PackageRecord { Id = "com.psvgamestudio.analytics", RecommendedVersion = "0.0.1-preview.3" }; // Registry null
            var plan = PlanFor(rec, PackageState.NotInstalled);
            Assert.IsFalse(plan.OfType<AddScopedRegistry>().Any());
            Assert.IsTrue(plan.OfType<AddPackage>().Any()); // add still emitted; resolve is Unity's problem, warned in catalog authoring
        }
    }
}
