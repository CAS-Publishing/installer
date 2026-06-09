using PSV.Installer.Catalog;
using UnityEditor;
using UnityEngine;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// Writes CAS IDs captured on the first screen into the CAS settings assets (managerIds) once
    /// the CAS package is installed and its per-platform settings asset exists. Idempotent: only
    /// overwrites an empty/placeholder value, never a real one the user already set. Reads the CAS
    /// config requirements (asset path + field + placeholder) from the catalog, like SetupChecker.
    /// </summary>
    internal static class CasIdApplier
    {
        private const string CasId = "com.cleversolutions.ads.unity";

        /// <summary>True when a stored id should overwrite the current asset value.</summary>
        internal static bool ShouldWrite(string current, string stored, string placeholder)
        {
            if (string.IsNullOrEmpty(stored)) return false;
            if (string.IsNullOrEmpty(current)) return true;
            return !string.IsNullOrEmpty(placeholder) &&
                   string.Equals(current, placeholder, System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Applies any pending CAS IDs to the CAS settings assets. Safe to call often.</summary>
        public static void ApplyPending()
        {
            // Fast exit: nothing captured means nothing to apply. Avoids catalog disk I/O on every
            // Components rebuild / window open in projects that never entered a CAS ID.
            if (string.IsNullOrEmpty(InstallerKeyStore.Get(CasId, "Android")) &&
                string.IsNullOrEmpty(InstallerKeyStore.Get(CasId, "iOS")))
                return;

            var load = CatalogLoader.Load();
            if (load.Status != CatalogLoadStatus.Ok || load.Catalog?.External == null) return;

            ExternalRecord cas = null;
            foreach (var e in load.Catalog.External)
                if (e != null && e.Id == CasId) { cas = e; break; }
            if (cas?.Config == null) return;

            var changed = false;
            foreach (var req in cas.Config)
            {
                if (req == null || req.Kind != "settingsAssetField" || string.IsNullOrEmpty(req.Field)) continue;

                var stored = InstallerKeyStore.Get(CasId, req.Platform);
                if (string.IsNullOrEmpty(stored)) continue;

                var asset = SetupChecker.LocateAsset(req.AssetPath, req.AssetType);
                if (asset == null) continue; // CAS not installed / asset not created yet

                var so = new SerializedObject(asset);
                var prop = so.FindProperty(req.Field);
                if (prop == null) continue;

                if (WriteIfNeeded(prop, stored, req.Placeholder))
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(asset);
                    changed = true;
                    Debug.Log($"[PSV Installer] Applied CAS ID for {req.Platform}: {stored}");
                }
            }

            if (changed) AssetDatabase.SaveAssets();
        }

        // managerIds is a string list — write a single-element list when the slot is empty/placeholder.
        private static bool WriteIfNeeded(SerializedProperty prop, string stored, string placeholder)
        {
            if (prop.isArray)
            {
                var current = prop.arraySize > 0 ? FirstString(prop) : null;
                if (!ShouldWrite(current, stored, placeholder)) return false;
                if (prop.arraySize == 0) prop.arraySize = 1;
                var first = prop.GetArrayElementAtIndex(0);
                if (first.propertyType != SerializedPropertyType.String) return false;
                first.stringValue = stored;
                return true;
            }
            if (prop.propertyType == SerializedPropertyType.String)
            {
                if (!ShouldWrite(prop.stringValue, stored, placeholder)) return false;
                prop.stringValue = stored;
                return true;
            }
            return false;
        }

        private static string FirstString(SerializedProperty arrayProp)
        {
            var first = arrayProp.GetArrayElementAtIndex(0);
            return first.propertyType == SerializedPropertyType.String ? first.stringValue : null;
        }
    }
}
