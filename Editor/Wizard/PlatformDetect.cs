using UnityEditor;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// Maps the project's active build target to the platform string the wizard uses
    /// ("Android" | "iOS"). Any non-iOS target (Android and all desktop/other targets)
    /// maps to "Android" — the single-platform Welcome pass defaults there and stays switchable.
    /// </summary>
    internal static class PlatformDetect
    {
        /// <summary>Pure mapping: iOS → "iOS", everything else → "Android". Testable.</summary>
        internal static string FromBuildTarget(BuildTarget target) =>
            target == BuildTarget.iOS ? "iOS" : "Android";

        /// <summary>The platform for the project's current active build target.</summary>
        public static string ActivePlatform() =>
            FromBuildTarget(EditorUserBuildSettings.activeBuildTarget);
    }
}
