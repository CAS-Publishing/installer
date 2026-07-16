using PSV.Installer.Catalog;
using UnityEditor;

namespace PSV.Installer.Wizard
{
    /// <summary>Result of probing the Tenjin SDK key field on a platform's CAS settings asset.</summary>
    internal sealed class TenjinKeyProbe
    {
        /// <summary>True when the running CAS version exposes a Tenjin key field at all. False means
        /// the row is informational only ("Handled on our end") and never blocks <see cref="PlatformReadiness.AllOk"/>.</summary>
        public bool FieldSupported;

        /// <summary>The key's current value (may be empty/null even when <see cref="FieldSupported"/>).</summary>
        public string Key;
    }

    /// <summary>
    /// Feature-detects whether the installed CAS version has a Tenjin SDK key field on its per-platform
    /// settings asset, and reads its current value. Older CAS versions don't have this field yet — in
    /// that case the Configure screen shows an informational "Handled on our end" row instead of a
    /// blocking requirement. Read-only; never throws.
    /// </summary>
    internal static class TenjinKeyDetect
    {
        private const string CasId = "com.cleversolutions.ads.unity";

        /// <summary>Candidate field names tried in order, oldest-first, across CAS versions.</summary>
        public static readonly string[] CandidateFieldNames = { "tenjinKey", "tenjinSdkKey", "tenjinAppKey" };

        public static TenjinKeyProbe Probe(string platform)
        {
            try
            {
                var asset = LocateCasSettingsAsset(platform);
                if (asset == null) return new TenjinKeyProbe { FieldSupported = false, Key = null };

                var so = new SerializedObject(asset);
                foreach (var fieldName in CandidateFieldNames)
                {
                    var prop = so.FindProperty(fieldName);
                    if (prop == null) continue;
                    if (prop.propertyType != SerializedPropertyType.String) continue;
                    return new TenjinKeyProbe { FieldSupported = true, Key = prop.stringValue };
                }

                return new TenjinKeyProbe { FieldSupported = false, Key = null };
            }
            catch
            {
                // Feature-detect only — never let a reflection/asset hiccup break the Configure screen.
                return new TenjinKeyProbe { FieldSupported = false, Key = null };
            }
        }

        // Duplicated from CasSettingsReader.ReadExisting rather than shared: that method's loop is
        // tangled with req.Field/Placeholder handling for the CAS managerId, which has nothing to do
        // with this probe. Both walk the same catalog CAS "settingsAssetField" requirement for the
        // platform to locate the same CASSettings<Platform> asset (AssetPath/AssetType), since the
        // Tenjin key lives on that same asset.
        private static UnityEngine.Object LocateCasSettingsAsset(string platform)
        {
            var load = CatalogLoader.Load();
            if (load.Status != CatalogLoadStatus.Ok || load.Catalog?.External == null) return null;

            ExternalRecord cas = null;
            foreach (var e in load.Catalog.External)
                if (e != null && e.Id == CasId) { cas = e; break; }
            if (cas?.Config == null) return null;

            foreach (var req in cas.Config)
            {
                // Same shape of guard CasSettingsReader uses: kind + a non-empty field name, since a
                // "settingsAssetField" entry without a Field is meaningless for a field lookup.
                if (req == null || req.Kind != "settingsAssetField" || string.IsNullOrEmpty(req.Field)) continue;
                if (!string.Equals(req.Platform, platform, System.StringComparison.OrdinalIgnoreCase)) continue;
                return SetupChecker.LocateAsset(req.AssetPath, req.AssetType);
            }
            return null;
        }
    }
}
