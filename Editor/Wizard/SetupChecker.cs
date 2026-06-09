using System.IO;
using PSV.Installer.Catalog;
using UnityEditor;
using UnityEngine;

namespace PSV.Installer.Wizard
{
    internal enum ReqStatus
    {
        Missing,        // file / settings asset absent
        NotConfigured,  // present but empty / placeholder value
        Configured,     // good
        NotApplicable,  // nothing to check (unknown kind, no params)
    }

    /// <summary>Result of evaluating one <see cref="ConfigRequirement"/>.</summary>
    internal sealed class ReqResult
    {
        public ReqStatus Status;
        public string Value;   // what's there (key / id / file path), if any
        public string Error;   // short explanation when missing / not configured
    }

    /// <summary>
    /// Generic handlers for the config requirement kinds the installer understands. The catalog
    /// (metadata) declares the requirements declaratively; this evaluates them against the project.
    /// Read-only — never mutates the project.
    /// </summary>
    internal static class SetupChecker
    {
        public static ReqResult Evaluate(ConfigRequirement req)
        {
            if (req == null) return new ReqResult { Status = ReqStatus.NotApplicable };
            switch (req.Kind)
            {
                case "assetFile":          return CheckAssetFile(req);
                case "settingsAssetField": return CheckSettingsField(req);
                default:
                    return new ReqResult { Status = ReqStatus.NotApplicable, Error = $"unknown kind '{req.Kind}'" };
            }
        }

        private static ReqResult CheckAssetFile(ConfigRequirement req)
        {
            if (string.IsNullOrEmpty(req.FileName))
                return new ReqResult { Status = ReqStatus.NotApplicable, Error = "no fileName" };

            try
            {
                var matches = Directory.GetFiles(Application.dataPath, req.FileName, SearchOption.AllDirectories);
                if (matches.Length > 0)
                {
                    var rel = "Assets" + matches[0].Substring(Application.dataPath.Length).Replace('\\', '/');
                    return new ReqResult { Status = ReqStatus.Configured, Value = rel };
                }
            }
            catch (System.Exception e)
            {
                return new ReqResult { Status = ReqStatus.Missing, Error = e.Message };
            }

            return new ReqResult { Status = ReqStatus.Missing, Error = $"{req.FileName} not found in Assets/" };
        }

        private static ReqResult CheckSettingsField(ConfigRequirement req)
        {
            var asset = LocateAsset(req);
            if (asset == null)
                return new ReqResult { Status = ReqStatus.Missing, Error = "settings asset not found" };

            if (string.IsNullOrEmpty(req.Field))
                return new ReqResult { Status = ReqStatus.Configured, Value = "settings present" };

            var prop = new SerializedObject(asset).FindProperty(req.Field);
            if (prop == null)
                return new ReqResult { Status = ReqStatus.NotConfigured, Error = $"field '{req.Field}' not found" };

            var value = ReadValue(prop);
            var isPlaceholder = !string.IsNullOrEmpty(req.Placeholder) &&
                                string.Equals(value, req.Placeholder, System.StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(value) || isPlaceholder)
                return new ReqResult { Status = ReqStatus.NotConfigured, Value = value, Error = "not set" };

            return new ReqResult { Status = ReqStatus.Configured, Value = value };
        }

        private static Object LocateAsset(ConfigRequirement req)
            => LocateAsset(req.AssetPath, req.AssetType);

        /// <summary>Locates a settings asset by explicit path or by ScriptableObject type name.</summary>
        internal static Object LocateAsset(string assetPath, string assetType)
        {
            if (!string.IsNullOrEmpty(assetPath))
                return AssetDatabase.LoadAssetAtPath<Object>(assetPath);

            if (!string.IsNullOrEmpty(assetType))
            {
                var guids = AssetDatabase.FindAssets("t:" + assetType);
                if (guids.Length > 0)
                    return AssetDatabase.LoadAssetAtPath<Object>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
            return null;
        }

        private static string ReadValue(SerializedProperty prop)
        {
            if (prop.propertyType == SerializedPropertyType.String)
                return prop.stringValue;

            if (prop.isArray)
            {
                if (prop.arraySize == 0) return null;
                var first = prop.GetArrayElementAtIndex(0);
                return first.propertyType == SerializedPropertyType.String
                    ? first.stringValue
                    : $"[{prop.arraySize} item(s)]";
            }

            return null;
        }
    }
}
