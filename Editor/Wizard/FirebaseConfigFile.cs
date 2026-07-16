using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// Validates and copies a Firebase config file (google-services.json / GoogleService-Info.plist)
    /// picked by the user into the project's Assets/ folder, for the Configure screen's Firebase panel
    /// "Locate file…" button. Never throws — failures come back as an error string.
    /// </summary>
    internal static class FirebaseConfigFile
    {
        private const string AndroidFileName = "google-services.json";
        private const string IosFileName = "GoogleService-Info.plist";

        /// <summary>
        /// Pure check of the file NAME (case-insensitive, name only — no existence/content check).
        /// Null = ok; otherwise an error string naming the expected files.
        /// </summary>
        public static string Validate(string path)
        {
            if (string.IsNullOrEmpty(path)) return "No file selected.";

            var name = Path.GetFileName(path);
            if (string.Equals(name, AndroidFileName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(name, IosFileName, StringComparison.OrdinalIgnoreCase))
                return null;

            return $"Expected \"{AndroidFileName}\" or \"{IosFileName}\", got \"{name}\".";
        }

        /// <summary>
        /// Validates <paramref name="sourcePath"/>'s file name, then copies it to the root of
        /// <paramref name="assetsDir"/> (project-relative, e.g. "Assets") and imports it. Never
        /// overwrites — if the destination already exists this returns an error instead. Null = ok.
        /// The Validate call lives INSIDE the try below (not before it) — Path.GetFileName can throw
        /// on invalid path characters, and this method's contract is to never throw, only return an
        /// error string.
        /// </summary>
        public static string ValidateAndCopy(string sourcePath, string assetsDir)
        {
            try
            {
                var err = Validate(sourcePath);
                if (err != null) return err;

                var fileName = Path.GetFileName(sourcePath);
                var dir = string.IsNullOrEmpty(assetsDir) ? "Assets" : assetsDir.TrimEnd('/', '\\');
                var destRelative = dir + "/" + fileName;

                var projectRoot = Directory.GetParent(Application.dataPath).FullName;
                var destFull = Path.Combine(projectRoot, destRelative.Replace('/', Path.DirectorySeparatorChar));

                if (File.Exists(destFull))
                    return $"{fileName} already exists in {dir}/ — remove it first if you want to replace it.";

                File.Copy(sourcePath, destFull, overwrite: false);
                AssetDatabase.ImportAsset(destRelative);
                return null;
            }
            catch (Exception e)
            {
                return $"Could not copy the file: {e.Message}";
            }
        }
    }
}
