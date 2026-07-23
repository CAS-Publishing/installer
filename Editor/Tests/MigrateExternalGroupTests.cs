using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PSV.Installer.Catalog;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    /// <summary>
    /// Covers the multi-module external install-set resolution used by Firebase migration
    /// (field report: migrating Firebase deleted Analytics/RemoteConfig/Installations but
    /// reinstalled only Analytics). The pure overload takes an explicit identifier set so the
    /// logic is testable without reflection over the live AppDomain.
    /// </summary>
    public class MigrateExternalGroupTests
    {
        private static ExternalRecord Firebase() => new ExternalRecord
        {
            Id = "com.google.firebase.analytics",
            DisplayName = "Firebase",
            Modules = new List<ExternalModule>
            {
                new ExternalModule { Id = "com.google.firebase.analytics",     AssetMarkers = new List<string> { "Firebase.Analytics" } },
                new ExternalModule { Id = "com.google.firebase.remote-config", AssetMarkers = new List<string> { "Firebase.RemoteConfig" } },
                new ExternalModule { Id = "com.google.firebase.installations", AssetMarkers = new List<string> { "Firebase.Installations" } },
            },
        };

        [Test]
        public void InstallSet_includes_only_detected_modules()
        {
            // Analytics + RemoteConfig present on disk, Installations absent.
            var loaded = new HashSet<string> { "Firebase.Analytics", "Firebase.RemoteConfig" };

            var set = WizardActions.ResolveInstallSet(Firebase(), "13.1.0", loaded);
            var ids = set.Select(a => a.Id).ToList();

            CollectionAssert.AreEquivalent(
                new[] { "com.google.firebase.analytics", "com.google.firebase.remote-config" }, ids);
            CollectionAssert.DoesNotContain(ids, "com.google.firebase.installations");
        }

        [Test]
        public void InstallSet_restores_all_three_when_all_present()
        {
            // The exact field-report scenario: all three modules were installed manually.
            var loaded = new HashSet<string> { "Firebase.Analytics", "Firebase.RemoteConfig", "Firebase.Installations" };

            var set = WizardActions.ResolveInstallSet(Firebase(), "13.1.0", loaded);

            CollectionAssert.AreEquivalent(
                new[]
                {
                    "com.google.firebase.analytics",
                    "com.google.firebase.remote-config",
                    "com.google.firebase.installations",
                },
                set.Select(a => a.Id).ToList());
            Assert.IsTrue(set.All(a => a.Version == "13.1.0"), "modules inherit the parent version");
        }

        [Test]
        public void InstallSet_falls_back_to_primary_when_no_module_detected()
        {
            // External flagged present but no module marker matched (odd layout) → never install nothing.
            var set = WizardActions.ResolveInstallSet(Firebase(), "13.1.0", new HashSet<string>());

            Assert.AreEqual(1, set.Count);
            Assert.AreEqual("com.google.firebase.analytics", set[0].Id);
            Assert.AreEqual("13.1.0", set[0].Version);
        }

        [Test]
        public void InstallSet_single_package_external_unchanged()
        {
            var tenjin = new ExternalRecord { Id = "com.tenjin.sdk", DisplayName = "Tenjin SDK" };

            var set = WizardActions.ResolveInstallSet(tenjin, "1.15.14", null);

            Assert.AreEqual(1, set.Count);
            Assert.AreEqual("com.tenjin.sdk", set[0].Id);
            Assert.AreEqual("1.15.14", set[0].Version);
        }

        [Test]
        public void InstallSet_module_version_override_wins()
        {
            var rec = new ExternalRecord
            {
                Id = "com.x",
                Modules = new List<ExternalModule>
                {
                    new ExternalModule { Id = "com.x.a", AssetMarkers = new List<string> { "X.A" }, RecommendedVersion = "2.0.0" },
                    new ExternalModule { Id = "com.x.b", AssetMarkers = new List<string> { "X.B" } },
                },
            };
            var loaded = new HashSet<string> { "X.A", "X.B" };

            var set = WizardActions.ResolveInstallSet(rec, "1.0.0", loaded);

            Assert.AreEqual("2.0.0", set.First(a => a.Id == "com.x.a").Version);
            Assert.AreEqual("1.0.0", set.First(a => a.Id == "com.x.b").Version);
        }
    }
}
