using UnityEditor;
using UnityEngine;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// Opens CAS.AI's own native settings window for a platform (where CAS ID, ad formats,
    /// mediation networks, and the Tenjin key actually live now that the Configuration screen
    /// no longer writes any of that itself). Best-effort: tries the CAS menu item first, falls
    /// back to pinging the platform's settings asset in the Project window, and never throws —
    /// this is a convenience shortcut, not a critical path.
    /// </summary>
    internal static class CasNativeSettings
    {
        public static void Open(string platform)
        {
            try
            {
                var menu = "Assets/CleverAdsSolutions/" + (platform == "iOS" ? "iOS Settings" : "Android Settings");
                if (EditorApplication.ExecuteMenuItem(menu)) return;

                var asset = AssetDatabase.LoadMainAssetAtPath(
                    "Assets/CleverAdsSolutions/Resources/CASSettings" + platform + ".asset");
                if (asset != null)
                {
                    Selection.activeObject = asset;
                    EditorGUIUtility.PingObject(asset);
                }
                else
                {
                    Debug.Log($"[CAS Hub] CAS settings for {platform} aren't available yet — " +
                              "install/configure CAS first.");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[CAS Hub] Could not open native CAS settings for {platform}: {e.Message}");
            }
        }
    }
}
