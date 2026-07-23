using System;
using System.Collections.Generic;
using NUnit.Framework;
using PSV.Installer.Migrator;
using PSV.Installer.Scanner;

namespace PSV.Installer.Tests
{
    public class LegacySplitRoutingTests
    {
        private const string Legacy = "com.psv.firebase.base";

        // Build a ScanReport with: SplitGroups = [{Legacy → analytics, remoteconfig}] and an
        // ExternalScanResult for com.google.firebase.analytics with State=InstalledLegacy,
        // DetectedLegacyId=Legacy. Internal ctors — same-assembly test asmdef (IVT), as
        // ScanReportFactory does; extended inline here rather than in ScanReportFactory itself.
        private static ScanReport Report() =>
            new ScanReport(
                "v",
                DateTime.UtcNow,
                new List<PackageScanResult>(),
                new List<ExternalScanResult>
                {
                    new ExternalScanResult(
                        "com.google.firebase.analytics", "Firebase Analytics",
                        ExternalState.InstalledLegacy, null, Legacy),
                },
                new List<UninstallScanResult>(),
                new List<MigrationGroup>
                {
                    new MigrationGroup(Legacy, new List<string>
                    {
                        "com.psvgamestudio.analytics",
                        "com.psvgamestudio.remoteconfig",
                    }),
                },
                "hash");

        [Test]
        public void AdapterId_WithLegacyInManifest_Routes()
        {
            var report = Report();
            var deps = new Dictionary<string, string> { { Legacy, "git" } };
            Assert.IsNotNull(LegacySplitRouting.FindGroupFor(report, deps, "com.psvgamestudio.analytics"));
            Assert.IsNotNull(LegacySplitRouting.FindGroupFor(report, deps, "com.psvgamestudio.remoteconfig"));
        }

        [Test]
        public void ExternalId_WhoseDetectedLegacyIsTheGroupLegacy_Routes()
        {
            var report = Report();
            var deps = new Dictionary<string, string> { { Legacy, "git" } };
            Assert.IsNotNull(LegacySplitRouting.FindGroupFor(report, deps, "com.google.firebase.analytics"));
        }

        [Test]
        public void LegacyNotInManifest_NoRouting()
        {
            var report = Report();
            Assert.IsNull(LegacySplitRouting.FindGroupFor(report, new Dictionary<string, string>(), "com.psvgamestudio.analytics"));
        }

        [Test]
        public void UnrelatedComponent_NoRouting()
        {
            var report = Report();
            var deps = new Dictionary<string, string> { { Legacy, "git" } };
            Assert.IsNull(LegacySplitRouting.FindGroupFor(report, deps, "com.tenjin.sdk"));
        }
    }
}
