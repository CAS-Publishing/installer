using System;
using System.Collections.Generic;
using NUnit.Framework;
using PSV.Installer.Scanner;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    /// <summary>
    /// Fix 1 (2026-07-23 firebase-migration-and-registry-fix): the Firebase Main-components row
    /// must offer "Migrate" when com.psv.firebase.base still provides the SDK AND a split group
    /// covers it — otherwise ExternalState.InstalledLegacy renders "Installed (legacy)" with no
    /// action (dead entry point (b): the wrapper still provides the SDK, but the row gives the
    /// user no way to trigger the compound migration).
    /// </summary>
    public class ComponentStatusLegacySplitPromotionTests
    {
        private const string Legacy = "com.psv.firebase.base";

        private static readonly (string Id, string Name, string Sub, string Logo) FirebaseDesc =
            ("com.google.firebase.analytics", "Firebase Analytics", "Analytics", "firebase");

        private static readonly (string Id, string Name, string Sub, string Logo) TenjinDesc =
            ("com.tenjin.sdk", "Tenjin SDK", "Attribution", "tenjin");

        // Internal ctor — same-assembly test asmdef (IVT), as LegacySplitRoutingTests does.
        private static ScanReport ReportWithGroup(string legacyId) =>
            new ScanReport(
                "v",
                DateTime.UtcNow,
                new List<PackageScanResult>(),
                new List<ExternalScanResult>(),
                new List<UninstallScanResult>(),
                new List<MigrationGroup>
                {
                    new MigrationGroup(legacyId, new List<string>
                    {
                        "com.psvgamestudio.analytics", "com.psvgamestudio.remoteconfig",
                    }),
                },
                "hash");

        [Test]
        public void InstalledLegacy_WithMatchingSplitGroup_PromotesToActionableMigrate()
        {
            var ext = new ExternalScanResult(
                "com.google.firebase.analytics", "Firebase Analytics",
                ExternalState.InstalledLegacy, null, Legacy);
            var s = ComponentStatusProvider.FromExternal(FirebaseDesc, ext);

            ComponentStatusProvider.PromoteLegacySplit(s, ext, ReportWithGroup(Legacy));

            Assert.AreEqual("yellow", s.Tone);
            Assert.AreEqual("Needs migration", s.StatusText);
            Assert.AreEqual("Migrate", s.ActionText);
            Assert.AreEqual("warn", s.ActionVariant);
            Assert.IsTrue(s.Actionable);
            // Remove must still target the legacy id — FromExternal already set this; promotion
            // must not touch it.
            Assert.AreEqual(Legacy, s.InstalledId);
        }

        // Deviation lock-in: a legacy wrapper with NO split group (e.g. Tenjin's com.psv.tenjin)
        // keeps the existing "Installed (legacy)" / no-action behavior — installing the canonical
        // id would duplicate the SDK, and there is no compound migration to route into.
        [Test]
        public void InstalledLegacy_WithoutMatchingSplitGroup_StaysNonActionable()
        {
            var ext = new ExternalScanResult(
                "com.tenjin.sdk", "Tenjin SDK",
                ExternalState.InstalledLegacy, "1.1.0", "com.psv.tenjin");
            var s = ComponentStatusProvider.FromExternal(TenjinDesc, ext);

            // A split group exists in the report, but for a DIFFERENT legacy id — must not match.
            ComponentStatusProvider.PromoteLegacySplit(s, ext, ReportWithGroup(Legacy));

            Assert.AreEqual("Installed (legacy)", s.StatusText);
            Assert.IsFalse(s.Actionable);
            Assert.AreEqual("com.psv.tenjin", s.InstalledId);
        }

        [Test]
        public void NonLegacyState_Untouched()
        {
            var ext = new ExternalScanResult(
                "com.google.firebase.analytics", "Firebase Analytics",
                ExternalState.UpmCurrent, "13.1.0-psv.1");
            var s = ComponentStatusProvider.FromExternal(FirebaseDesc, ext);

            ComponentStatusProvider.PromoteLegacySplit(s, ext, ReportWithGroup(Legacy));

            Assert.AreEqual("Installed", s.StatusText);
            Assert.IsFalse(s.Actionable);
        }
    }
}
