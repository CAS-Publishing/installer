using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PSV.Installer.Catalog;
using PSV.Installer.Common;
using PSV.Installer.Migrator;
using PSV.Installer.Scanner;

namespace PSV.Installer.Tests
{
    public class MigrationPlannerGitTests
    {
        private sealed class OneId : ISelectionSet
        {
            private readonly string _id;
            public OneId(string id) { _id = id; }
            public bool IsSelected(string id) => id == _id;
            public VersionTarget GetTarget(string id) => VersionTarget.Recommended;
        }

        private static PackageCatalog CatalogWithGitExternal() => new PackageCatalog
        {
            Registries = new Dictionary<string, string> { { "psv", "https://npm.psvgamestudio.com/" } },
            External = new List<ExternalRecord>
            {
                new ExternalRecord
                {
                    Id = "com.tenjin.sdk", DisplayName = "Tenjin", Registry = "psv",
                    Scopes = new List<string> { "com.tenjin" },
                    RecommendedVersion = "1.15.14",
                    Git = new GitInstall
                    {
                        Packages = new List<GitPackage>
                        {
                            new GitPackage { Id = "com.tenjin.sdk", Url = "https://github.com/CAS-Publishing/tenjin-sdk.git", Tag = "1.15.14" }
                        }
                    }
                }
            }
        };

        [Test]
        public void GitMode_emits_AddGitPackage_for_each_chain_entry_and_no_registry()
        {
            var catalog = CatalogWithGitExternal();
            var report = ScanReportFactory.WithExternals(new[] {
                ScanReportFactory.Ext("com.tenjin.sdk", ExternalState.NotInstalled) });

            var plan = MigrationPlanner.Plan(catalog, report, new OneId("com.tenjin.sdk"),
                InstallMethod.Git, out _);

            Assert.AreEqual(1, plan.OfType<AddGitPackage>().Count());
            Assert.AreEqual("https://github.com/CAS-Publishing/tenjin-sdk.git#1.15.14",
                plan.OfType<AddGitPackage>().Single().Spec);
            Assert.IsFalse(plan.OfType<AddScopedRegistry>().Any(), "git mode must not register a scope");
            Assert.IsFalse(plan.OfType<AddPackage>().Any(), "git mode must not add a registry version");
        }

        [Test]
        public void UpmMode_unchanged_still_emits_registry_and_version()
        {
            var catalog = CatalogWithGitExternal();
            var report = ScanReportFactory.WithExternals(new[] {
                ScanReportFactory.Ext("com.tenjin.sdk", ExternalState.NotInstalled) });

            var plan = MigrationPlanner.Plan(catalog, report, new OneId("com.tenjin.sdk"),
                InstallMethod.Upm, out _);

            Assert.IsTrue(plan.OfType<AddPackage>().Any());
            Assert.IsFalse(plan.OfType<AddGitPackage>().Any());
        }
    }
}
