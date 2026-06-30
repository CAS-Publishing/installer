using System.Collections.Generic;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// The custom Android build templates a buildable EDM4U project needs under
    /// <c>Assets/Plugins/Android/</c>. Their presence is what enables Unity's "Custom … Template"
    /// and "Custom Main Manifest" toggles, so EDM4U can inject resolved dependencies.
    /// </summary>
    internal static class AndroidBuildTemplates
    {
        public const string PluginsAndroidDir = "Assets/Plugins/Android";

        public static readonly IReadOnlyList<string> Required = new[]
        {
            "mainTemplate.gradle",
            "launcherTemplate.gradle",
            "baseProjectTemplate.gradle",
            "gradleTemplate.properties",
            "settingsTemplate.gradle",
            "AndroidManifest.xml",
        };

        /// <summary>Required template names not present in <paramref name="presentFileNames"/> (case-insensitive). Pure.</summary>
        public static List<string> Missing(IEnumerable<string> presentFileNames)
        {
            var present = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
            if (presentFileNames != null) foreach (var n in presentFileNames) if (!string.IsNullOrEmpty(n)) present.Add(n);
            var missing = new List<string>();
            foreach (var req in Required) if (!present.Contains(req)) missing.Add(req);
            return missing;
        }
    }
}
