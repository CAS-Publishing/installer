using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// Ensures the custom Android build templates exist in <c>Assets/Plugins/Android/</c> by copying the
    /// running Editor's defaults. Only creates missing files (never overwrites). Graceful: a missing
    /// default source is logged and skipped — never throws, never half-breaks the project.
    /// </summary>
    internal static class AndroidBuildFix
    {
        // Resolved at runtime so it matches the running Editor version, wherever it's installed.
        private static string AndroidPlayerDir => Path.Combine(
            EditorApplication.applicationContentsPath, "PlaybackEngines", "AndroidPlayer");

        // Per-file Editor default source CANDIDATES — tried in order, first that exists wins. This is
        // what makes the Fix work across Unity versions (2022.3 and 6000+): the gradle templates keep
        // their name + the GradleTemplates dir on both, but the Custom Main Manifest default moved
        // (Unity 6: Apk/UnityManifest.xml; older layouts: GradleTemplates/ or Apk/AndroidManifest.xml).
        private static IEnumerable<string> EditorSourceCandidates(string destFile)
        {
            var ap = AndroidPlayerDir;
            if (string.Equals(destFile, "AndroidManifest.xml", System.StringComparison.OrdinalIgnoreCase))
            {
                yield return Path.Combine(ap, "Apk", "UnityManifest.xml");
                yield return Path.Combine(ap, "Tools", "GradleTemplates", "AndroidManifest.xml");
                yield return Path.Combine(ap, "Apk", "AndroidManifest.xml");
            }
            else
            {
                yield return Path.Combine(ap, "Tools", "GradleTemplates", destFile);
            }
        }

        // Minimal valid main manifest — written only if no Editor default manifest is found (Unity
        // layout differences). Unity merges its own activity/content into it.
        private const string MinimalMainManifest =
            "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
            "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\">\n" +
            "    <application />\n" +
            "</manifest>\n";

        private static string DestDirAbs => Path.Combine(
            Application.dataPath, "Plugins", "Android");

        /// <summary>Required templates currently absent from the project.</summary>
        public static List<string> MissingNow()
        {
            var present = new List<string>();
            if (Directory.Exists(DestDirAbs))
                foreach (var f in Directory.GetFiles(DestDirAbs))
                    present.Add(Path.GetFileName(f));
            return AndroidBuildTemplates.Missing(present);
        }

        /// <summary>
        /// Creates any missing Android build template from the Editor default. Returns how many were
        /// created. Logs (and skips) a template whose Editor default can't be found.
        /// </summary>
        public static int Ensure()
        {
            var missing = MissingNow();
            if (missing.Count == 0) return 0;

            Directory.CreateDirectory(DestDirAbs);
            var created = 0;
            foreach (var file in missing)
            {
                var dest = Path.Combine(DestDirAbs, file);
                string foundSrc = null;
                foreach (var cand in EditorSourceCandidates(file))
                    if (File.Exists(cand)) { foundSrc = cand; break; }
                try
                {
                    if (foundSrc != null)
                    {
                        File.Copy(foundSrc, dest, overwrite: false);
                        created++;
                    }
                    else if (string.Equals(file, "AndroidManifest.xml", System.StringComparison.OrdinalIgnoreCase))
                    {
                        // No Editor default manifest on any known path — write a minimal valid one so
                        // the Custom Main Manifest toggle still turns on.
                        File.WriteAllText(dest, MinimalMainManifest);
                        created++;
                    }
                    else
                    {
                        Debug.LogWarning($"[CAS Hub] No Editor default found for '{file}' (looked under " +
                            $"{AndroidPlayerDir}). Skipped — enable it manually in Player Settings if your build needs it.");
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning($"[CAS Hub] Couldn't create '{file}': {e.Message}");
                }
            }
            if (created > 0)
            {
                AssetDatabase.Refresh();
                Debug.Log($"[CAS Hub] Enabled {created} Android build template(s) under Assets/Plugins/Android.");
            }
            return created;
        }
    }
}
