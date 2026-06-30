using System.Collections.Generic;
using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class AndroidBuildTemplatesTests
    {
        [Test] public void Required_has_six()
            => Assert.AreEqual(6, AndroidBuildTemplates.Required.Count);

        [Test] public void Missing_none_when_all_present()
            => Assert.IsEmpty(AndroidBuildTemplates.Missing(new List<string>(AndroidBuildTemplates.Required)));

        [Test] public void Missing_all_when_none_present()
            => Assert.AreEqual(AndroidBuildTemplates.Required.Count,
                               AndroidBuildTemplates.Missing(new List<string>()).Count);

        [Test] public void Missing_is_case_insensitive()
            => Assert.IsEmpty(AndroidBuildTemplates.Missing(new List<string> {
                "MAINTEMPLATE.GRADLE", "launchertemplate.gradle", "baseProjectTemplate.gradle",
                "gradleTemplate.properties", "settingsTemplate.gradle", "androidmanifest.xml" }));

        [Test] public void Missing_reports_the_absent_one()
        {
            var present = new List<string>(AndroidBuildTemplates.Required);
            present.Remove("AndroidManifest.xml");
            var missing = AndroidBuildTemplates.Missing(present);
            Assert.AreEqual(1, missing.Count);
            Assert.AreEqual("AndroidManifest.xml", missing[0]);
        }
    }
}
