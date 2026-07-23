using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PSV.Installer.Catalog;
using PSV.Installer.Migrator;
using PSV.Installer.Scanner;

namespace PSV.Installer.Tests
{
    public class FirebaseMigrationPlanTests
    {
        private const string Legacy = "com.psv.firebase.base";
        private const string PsvEdm = "com.psv.unity.edm";

        private static PackageCatalog Catalog() => new PackageCatalog
        {
            Registries = new Dictionary<string, string>
            {
                { "psv", "https://npm.psvgamestudio.com/" },
            },
            Packages = new List<PackageRecord>
            {
                new PackageRecord { Id = "com.psvgamestudio.analytics", Registry = "psv",
                    LegacyNpmIds = new List<string> { Legacy },
                    Scopes = new List<string> { "com.psvgamestudio" },
                    Requires = new List<string> { "com.psv.core" },
                    RecommendedVersion = "0.0.1-preview.3" },
                new PackageRecord { Id = "com.psvgamestudio.remoteconfig", Registry = "psv",
                    LegacyNpmIds = new List<string> { Legacy },
                    Scopes = new List<string> { "com.psvgamestudio" },
                    Requires = new List<string> { "com.psv.core" },
                    DetectMarkers = new List<string> { "Firebase.RemoteConfig" },
                    RecommendedVersion = "0.0.1-preview.2" },
            },
            External = new List<ExternalRecord>
            {
                new ExternalRecord { Id = "com.google.firebase.analytics", Registry = "psv",
                    Scopes = new List<string> { "com.google" },
                    LegacyManifestIds = new List<string> { Legacy },
                    RecommendedVersion = "13.1.0-psv.1",
                    Modules = new List<ExternalModule>
                    {
                        new ExternalModule { Id = "com.google.firebase.analytics",     AssetMarkers = new List<string> { "Firebase.Analytics" } },
                        new ExternalModule { Id = "com.google.firebase.remote-config", AssetMarkers = new List<string> { "Firebase.RemoteConfig" } },
                    } },
            },
            Uninstall = new List<UninstallRecord>
            {
                new UninstallRecord { LegacyNpmIds = new List<string> { PsvEdm } },
            },
        };

        private static MigrationGroup Group() => // internal ctor — same-assembly test asmdef, as ScanReportFactory does
            new MigrationGroup(Legacy, new List<string> { "com.psvgamestudio.analytics", "com.psvgamestudio.remoteconfig" });

        private static Dictionary<string, string> Deps(bool core, bool edm)
        {
            var d = new Dictionary<string, string> { { Legacy, "https://gitlab/fb.git" } };
            if (core) d["com.psv.core"] = "https://gitlab/core.git";
            if (edm) d[PsvEdm] = "1.0.0";
            return d;
        }

        private static readonly string[] AnalyticsOnly = { "Firebase.Analytics.FirebaseAnalytics" };
        private static readonly string[] AnalyticsAndRc = { "Firebase.Analytics.FirebaseAnalytics", "Firebase.RemoteConfig.FirebaseRemoteConfig" };

        [Test]
        public void FullMatrix_CoreAndRc_RemovesLegacyAndEdm_InstallsAll()
        {
            var r = FirebaseMigrationPlan.Build(Catalog(), Group(), Deps(core: true, edm: true), AnalyticsAndRc, _ => false);
            var removes = r.Actions.OfType<RemovePackage>().Select(a => a.Id).ToList();
            CollectionAssert.AreEquivalent(new[] { Legacy, PsvEdm }, removes);
            var adds = r.Actions.OfType<AddPackage>().Select(a => a.Id).ToList();
            CollectionAssert.Contains(adds, "com.google.firebase.analytics");
            CollectionAssert.Contains(adds, "com.google.firebase.remote-config");
            CollectionAssert.Contains(adds, "com.psvgamestudio.analytics");
            CollectionAssert.Contains(adds, "com.psvgamestudio.remoteconfig");
            // Order: every remove before every registry, every registry before every add.
            int lastRemove = r.Actions.FindLastIndex(a => a is RemovePackage);
            int firstReg   = r.Actions.FindIndex(a => a is AddScopedRegistry);
            int firstAdd   = r.Actions.FindIndex(a => a is AddPackage);
            Assert.Less(lastRemove, firstReg);
            Assert.Less(firstReg, firstAdd);
        }

        [Test]
        public void AnalyticsOnly_SkipsRemoteConfigAdapterAndModule()
        {
            var r = FirebaseMigrationPlan.Build(Catalog(), Group(), Deps(core: true, edm: true), AnalyticsOnly, _ => false);
            var adds = r.Actions.OfType<AddPackage>().Select(a => a.Id).ToList();
            CollectionAssert.Contains(adds, "com.psvgamestudio.analytics");
            CollectionAssert.DoesNotContain(adds, "com.psvgamestudio.remoteconfig");
            CollectionAssert.DoesNotContain(adds, "com.google.firebase.remote-config");
        }

        [Test]
        public void NoCore_NativeOnly_WithWarning()
        {
            var r = FirebaseMigrationPlan.Build(Catalog(), Group(), Deps(core: false, edm: true), AnalyticsAndRc, _ => false);
            var adds = r.Actions.OfType<AddPackage>().Select(a => a.Id).ToList();
            CollectionAssert.DoesNotContain(adds, "com.psvgamestudio.analytics");
            CollectionAssert.DoesNotContain(adds, "com.psvgamestudio.remoteconfig");
            CollectionAssert.Contains(adds, "com.google.firebase.analytics");
            Assert.IsTrue(r.Warnings.Any(w => w.Contains("com.psv.core")));
        }

        [Test]
        public void NoEdmInManifest_NoEdmRemove()
        {
            var r = FirebaseMigrationPlan.Build(Catalog(), Group(), Deps(core: true, edm: false), AnalyticsAndRc, _ => false);
            CollectionAssert.DoesNotContain(r.Actions.OfType<RemovePackage>().Select(a => a.Id).ToList(), PsvEdm);
        }

        [Test]
        public void LegacyAbsent_EmptyPlan()
        {
            var r = FirebaseMigrationPlan.Build(Catalog(), Group(),
                new Dictionary<string, string>(), AnalyticsAndRc, _ => false);
            Assert.IsEmpty(r.Actions);
        }

        [Test]
        public void CoreEmbedded_CountsAsPresent()
        {
            var r = FirebaseMigrationPlan.Build(Catalog(), Group(), Deps(core: false, edm: false), AnalyticsOnly,
                id => id == "com.psv.core");
            CollectionAssert.Contains(r.Actions.OfType<AddPackage>().Select(a => a.Id).ToList(),
                "com.psvgamestudio.analytics");
        }

        // Fix 2 (2026-07-23 firebase-migration-and-registry-fix): a STALE catalog — one whose
        // External list has no record linking group.LegacyId via LegacyManifestIds — must not
        // silently remove com.psv.firebase.base and add ONLY the adapters (stripping Firebase from
        // the project with no native replacement). Build must abort with an empty plan instead of
        // continuing to removes/adapters.
        [Test]
        public void NoLinkedExternal_AbortsWithEmptyPlan()
        {
            var catalog = Catalog();
            catalog.External[0].LegacyManifestIds = null; // stale catalog: no linkage to Legacy anymore

            var r = FirebaseMigrationPlan.Build(catalog, Group(), Deps(core: true, edm: true), AnalyticsAndRc, _ => false);

            Assert.IsEmpty(r.Actions);
            Assert.IsTrue(r.Warnings.Any(w => w.Contains(Legacy)));
        }
    }
}
