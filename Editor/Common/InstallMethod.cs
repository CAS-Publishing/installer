using UnityEditor;
using UnityEngine;

namespace PSV.Installer.Common
{
    /// <summary>How the installer writes component dependencies into manifest.json.</summary>
    public enum InstallMethod
    {
        /// <summary>Scoped registry + version (the default).</summary>
        Upm,
        /// <summary>git-URL dependencies (clean, no scoped registry).</summary>
        Git,
    }

    /// <summary>
    /// Per-project persistence of the chosen install method. EditorPrefs is machine-global, so the
    /// key includes the project data path to scope it to THIS project (mirrors the IntroDone pattern).
    /// </summary>
    public static class InstallMethodState
    {
        private static string Key => "PSV.Installer.InstallMethod:" + Application.dataPath;

        public static InstallMethod Get() =>
            EditorPrefs.GetInt(Key, (int)InstallMethod.Upm) == (int)InstallMethod.Git
                ? InstallMethod.Git : InstallMethod.Upm;

        public static void Set(InstallMethod method) => EditorPrefs.SetInt(Key, (int)method);
    }
}
