using System;
using System.Collections.Generic;
using System.Linq;
using PSV.Installer.Scanner;

namespace PSV.Installer.Tests
{
    internal static class ScanReportFactory
    {
        public static PackageScanResult Pkg(string id, PackageState state) =>
            (PackageScanResult)Activator.CreateInstance(
                typeof(PackageScanResult),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new object[] { id, id, state, null, null, null }, null);

        public static PackageScanResult PkgLegacy(string id, string legacyNpmId) =>
            (PackageScanResult)Activator.CreateInstance(
                typeof(PackageScanResult),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new object[] { id, id, PackageState.LegacyUpm, "1.0.0", legacyNpmId, null }, null);

        public static ScanReport With(IEnumerable<PackageScanResult> packages) =>
            Build(packages, Array.Empty<MigrationGroup>());

        public static ScanReport WithSplit(IEnumerable<PackageScanResult> packages, params MigrationGroup[] groups) =>
            Build(packages, groups);

        private static ScanReport Build(IEnumerable<PackageScanResult> packages, IReadOnlyList<MigrationGroup> groups) =>
            (ScanReport)Activator.CreateInstance(
                typeof(ScanReport),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null,
                new object[] { "v", DateTime.UtcNow, packages.ToList(), new List<ExternalScanResult>(), new List<UninstallScanResult>(), groups, "hash", null },
                null);

        public static ExternalScanResult Ext(string id, ExternalState state, string detectedLegacyId = null) =>
            (ExternalScanResult)Activator.CreateInstance(
                typeof(ExternalScanResult),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null, new object[] { id, id, state, null, detectedLegacyId }, null);

        public static ScanReport WithExternals(IEnumerable<ExternalScanResult> externals) =>
            (ScanReport)Activator.CreateInstance(
                typeof(ScanReport),
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
                null,
                new object[] { "v", DateTime.UtcNow, new List<PackageScanResult>(), externals.ToList(),
                    new List<UninstallScanResult>(), Array.Empty<MigrationGroup>(), "hash", null },
                null);
    }
}
