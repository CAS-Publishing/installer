using NUnit.Framework;
using PSV.Installer.Scanner;
using PSV.Installer.Ui;

namespace PSV.Installer.Tests
{
    public sealed class ReportViewLogicTests
    {
        [Test]
        public void All_current_has_no_actionable()
        {
            var report = ScanReportFactory.With(new[]
            {
                ScanReportFactory.Pkg("com.a", PackageState.UpmCurrent),
                ScanReportFactory.Pkg("com.b", PackageState.UpmCurrent),
            });
            Assert.IsFalse(InstallerWindowReportView.HasAnyActionable(report));
        }

        [Test]
        public void A_not_installed_package_is_actionable()
        {
            var report = ScanReportFactory.With(new[]
            {
                ScanReportFactory.Pkg("com.a", PackageState.NotInstalled),
            });
            Assert.IsTrue(InstallerWindowReportView.HasAnyActionable(report));
        }

        [Test]
        public void A_legacy_package_is_actionable()
        {
            var report = ScanReportFactory.With(new[]
            {
                ScanReportFactory.PkgLegacy("com.a", "legacy.id"),
            });
            Assert.IsTrue(InstallerWindowReportView.HasAnyActionable(report));
        }
    }
}
