using UnityEditor;
using UnityEngine;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// Per-project store for SDK keys/ids entered in the wizard before the owning package is
    /// installed (so they can be applied to the package's settings afterwards). Keyed by
    /// componentId + platform, so future per-SDK keys (e.g. Gadsme) reuse the same mechanism.
    /// Backed by EditorPrefs (machine-global) — scoped to THIS project via the data path.
    /// </summary>
    internal static class InstallerKeyStore
    {
        private static string Key(string componentId, string platform) =>
            "PSV.Installer.Wizard.Key:" + Application.dataPath + ":" + componentId + "." + platform;

        public static string Get(string componentId, string platform) =>
            EditorPrefs.GetString(Key(componentId, platform), "");

        public static void Set(string componentId, string platform, string value)
        {
            if (string.IsNullOrEmpty(value)) EditorPrefs.DeleteKey(Key(componentId, platform));
            else EditorPrefs.SetString(Key(componentId, platform), value.Trim());
        }

        public static string GetOrDefault(string componentId, string platform, string fallback)
        {
            var v = Get(componentId, platform);
            return string.IsNullOrEmpty(v) ? (fallback ?? "") : v;
        }
    }
}
