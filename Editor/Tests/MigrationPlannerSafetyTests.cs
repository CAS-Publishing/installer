using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PSV.Installer.Catalog;
using PSV.Installer.Common;
using PSV.Installer.Migrator;
using PSV.Installer.Scanner;

namespace PSV.Installer.Tests
{
    public sealed class MigrationPlannerSafetyTests
    {
        // Minimal selection stub.
        private sealed class Sel : ISelectionSet
        {
            private readonly HashSet<string> _ids;
            public Sel(params string[] ids) { _ids = new HashSet<string>(ids); }
            public bool IsSelected(string id) => _ids.Contains(id);
            public VersionTarget GetTarget(string id) => VersionTarget.Recommended;
        }

        private static PackageRecord Rec(string id, string rec, string min) =>
            new PackageRecord { Id = id, RecommendedVersion = rec, MinVersion = min };

        // ── Empty-version guard ───────────────────────────────────────────
        [Test]
        public void Empty_version_record_produces_no_AddPackage()
        {
            var catalog = new PackageCatalog { Packages = new List<PackageRecord> { Rec("com.x", "", null) } };
            var report = ScanReportFactory.With(
                new[] { ScanReportFactory.Pkg("com.x", PackageState.NotInstalled) });

            var plan = MigrationPlanner.Plan(catalog, report, new Sel("com.x"), InstallMethod.Upm, out _);
            Assert.IsFalse(plan.OfType<AddPackage>().Any(), "empty version must not yield an AddPackage");
        }

        [Test]
        public void Valid_version_record_produces_AddPackage()
        {
            var catalog = new PackageCatalog { Packages = new List<PackageRecord> { Rec("com.x", "1.2.3", null) } };
            var report = ScanReportFactory.With(
                new[] { ScanReportFactory.Pkg("com.x", PackageState.NotInstalled) });

            var plan = MigrationPlanner.Plan(catalog, report, new Sel("com.x"), InstallMethod.Upm, out _);
            var add = plan.OfType<AddPackage>().Single();
            Assert.AreEqual("1.2.3", add.Version);
        }

        // ── Partial-split backstop ────────────────────────────────────────
        [Test]
        public void Full_split_selection_removes_legacy()
        {
            var catalog = new PackageCatalog { Packages = new List<PackageRecord> {
                Rec("com.a", "1.0.0", null), Rec("com.b", "1.0.0", null) } };
            var report = ScanReportFactory.WithSplit(
                new[] {
                    ScanReportFactory.PkgLegacy("com.a", "legacy.base"),
                    ScanReportFactory.PkgLegacy("com.b", "legacy.base"),
                },
                new MigrationGroup("legacy.base", new[] { "com.a", "com.b" }));

            var plan = MigrationPlanner.Plan(catalog, report, new Sel("com.a", "com.b"), InstallMethod.Upm, out var warnings);
            Assert.IsTrue(plan.OfType<RemovePackage>().Any(r => r.Id == "legacy.base"));
            Assert.IsFalse(warnings.OfType<PartialSplitWarning>().Any());
        }

        [Test]
        public void Partial_split_selection_drops_the_legacy_remove_and_warns()
        {
            var catalog = new PackageCatalog { Packages = new List<PackageRecord> {
                Rec("com.a", "1.0.0", null), Rec("com.b", "1.0.0", null) } };
            var report = ScanReportFactory.WithSplit(
                new[] {
                    ScanReportFactory.PkgLegacy("com.a", "legacy.base"),
                    ScanReportFactory.PkgLegacy("com.b", "legacy.base"),
                },
                new MigrationGroup("legacy.base", new[] { "com.a", "com.b" }));

            // Only com.a selected.
            var plan = MigrationPlanner.Plan(catalog, report, new Sel("com.a"), InstallMethod.Upm, out var warnings);

            Assert.IsFalse(plan.OfType<RemovePackage>().Any(r => r.Id == "legacy.base"),
                "must NOT remove legacy when its full replacement set isn't selected");
            Assert.IsTrue(plan.OfType<AddPackage>().Any(a => a.Id == "com.a"),
                "the selected replacement is still added");
            Assert.IsTrue(warnings.OfType<PartialSplitWarning>().Any(w => w.LegacyId == "legacy.base"));
        }
    }
}
