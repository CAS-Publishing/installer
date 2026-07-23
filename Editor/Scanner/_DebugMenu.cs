// Temporary debug entry point — will be removed in Phase 3 when the real UI takes over.
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PSV.Installer.Catalog;
using UnityEditor;
using UnityEngine;

namespace PSV.Installer.Scanner
{
    internal static class DebugMenu
    {
        [MenuItem("Assets/CleverAdsSolutions/Hub Debug/Run Scan")]
        private static void RunScan()
        {
            var load = CatalogLoader.Load();
            if (load.Status != CatalogLoadStatus.Ok)
            {
                Debug.LogWarning("[CAS Hub] Run Scan: catalog could not be loaded " +
                                 $"({load.Status}: {load.Error}). " +
                                 "Make sure com.psvgamestudio.installer.metadata is installed.");
                return;
            }

            var catalog = load.Catalog;
            Debug.Log($"[CAS Hub] Running scan against catalog v{catalog.CatalogVersion} from {load.Source}…");

            var report = ProjectScanner.Scan(catalog);

            var json = JsonConvert.SerializeObject(report, Formatting.Indented, new StringEnumConverter());

            Debug.Log($"[CAS Hub] ScanReport:\n{json}");
        }
    }
}
