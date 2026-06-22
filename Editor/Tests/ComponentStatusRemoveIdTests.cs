using NUnit.Framework;
using PSV.Installer.Scanner;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    /// <summary>
    /// The wizard "Remove" button must target the package id that is ACTUALLY in
    /// Packages/manifest.json, not the canonical catalog id. When a package is present under a
    /// legacy id (e.g. Tenjin installed as the git package <c>com.psv.tenjin</c>), removing the
    /// canonical <c>com.tenjin.sdk</c> is a silent no-op — the documented "delete does nothing" bug.
    /// </summary>
    public class ComponentStatusRemoveIdTests
    {
        private static readonly (string Id, string Name, string Sub, string Logo) TenjinDesc =
            ("com.tenjin.sdk", "Tenjin SDK", "Attribution", "tenjin");

        // InstalledLegacy external: manifest holds the legacy id → Remove must target that id.
        [Test]
        public void External_installedLegacy_InstalledId_is_legacy_manifest_id()
        {
            var ext = new ExternalScanResult(
                "com.tenjin.sdk", "Tenjin SDK",
                ExternalState.InstalledLegacy, "1.1.0", "com.psv.tenjin");

            var s = ComponentStatusProvider.FromExternal(TenjinDesc, ext);

            Assert.AreEqual("com.psv.tenjin", s.InstalledId);
        }

        // Canonical UPM install: Remove targets the canonical id.
        [Test]
        public void External_upmCurrent_InstalledId_is_canonical_id()
        {
            var ext = new ExternalScanResult(
                "com.tenjin.sdk", "Tenjin SDK",
                ExternalState.UpmCurrent, "1.15.14-psv.1");

            var s = ComponentStatusProvider.FromExternal(TenjinDesc, ext);

            Assert.AreEqual("com.tenjin.sdk", s.InstalledId);
        }

        // Sibling case: a PSV package present under its legacy npm id → Remove targets the legacy id.
        [Test]
        public void Package_legacyUpm_InstalledId_is_detected_legacy_npm_id()
        {
            var desc = ("com.psvgamestudio.analytics", "Analytics", "Analytics", (string)null);
            var pkg = new PackageScanResult(
                "com.psvgamestudio.analytics", "Analytics",
                PackageState.LegacyUpm, "1.0.0", "com.psv.analytics", null);

            var s = ComponentStatusProvider.FromPackage(desc, pkg);

            Assert.AreEqual("com.psv.analytics", s.InstalledId);
        }
    }
}
