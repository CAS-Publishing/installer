using System.Collections.Generic;
using System.Text;
using PSV.Installer.Catalog;
using PSV.Installer.Migrator;
using PSV.Installer.Scanner;
using UnityEditor;
using UnityEngine;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// Real per-component Install/Update actions for the wizard. Reuses the existing
    /// <see cref="MigrationPlanner"/> + <see cref="MigrationRunner"/> pipeline, scoped to a
    /// single component, behind a confirm dialog (it writes to Packages/manifest.json).
    /// </summary>
    internal static class WizardActions
    {
        /// <summary>Selects exactly one id at the recommended version target.</summary>
        private sealed class SingleSelection : ISelectionSet
        {
            private readonly string _id;
            public SingleSelection(string id) { _id = id; }
            public bool IsSelected(string id) => id == _id;
            public VersionTarget GetTarget(string id) => VersionTarget.Recommended;
        }

        /// <summary>
        /// Plans + applies the migration for a single component. Shows a confirm dialog first.
        /// Returns true when a migration was executed (success OR failure) so the caller should
        /// re-scan; false when cancelled or there was nothing to do.
        /// </summary>
        public static bool Apply(string componentId, string displayName)
        {
            var load = CatalogLoader.Load();
            if (load.Status != CatalogLoadStatus.Ok)
            {
                EditorUtility.DisplayDialog("PSV Installer",
                    "Catalog is unavailable — cannot apply. " +
                    (load.Error ?? "Make sure the metadata package is installed."), "OK");
                return false;
            }

            var report = ProjectScanner.Scan(load.Catalog);
            var plan = MigrationPlanner.Plan(load.Catalog, report, new SingleSelection(componentId), out var warnings);

            if (plan.Count == 0)
            {
                EditorUtility.DisplayDialog("PSV Installer",
                    $"Nothing to apply for {displayName} — it may already be up to date, " +
                    "or the catalog has no version configured for it.", "OK");
                return false;
            }

            if (!EditorUtility.DisplayDialog("PSV Installer",
                    BuildSummary(displayName, plan, warnings), "Apply", "Cancel"))
                return false;

            var result = new MigrationRunner().Apply(plan);
            if (result.Success)
            {
                Debug.Log($"[PSV Installer Wizard] Applied {result.ExecutedCount} action(s) for {displayName}. " +
                          "Unity will resolve packages now.");
            }
            else
            {
                EditorUtility.DisplayDialog("PSV Installer",
                    $"Apply failed for {displayName}:\n• " + string.Join("\n• ", result.Failures) +
                    "\n\nNo backup is kept — use 'git status' / 'git restore .' to inspect or revert.", "OK");
            }

            return true; // re-scan regardless: reflect the real resulting state
        }

        private static string BuildSummary(
            string displayName,
            IReadOnlyList<MigrationAction> plan,
            IReadOnlyList<PlannerWarning> warnings)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Apply changes for {displayName}?");
            sb.AppendLine();

            foreach (var a in plan)
            {
                switch (a)
                {
                    case AddPackage add:           sb.AppendLine($"  • Add {add.Id}@{add.Version}"); break;
                    case UpdatePackageVersion upd: sb.AppendLine($"  • Update {upd.Id} → {upd.Version}"); break;
                    case RemovePackage rem:        sb.AppendLine($"  • Remove {rem.Id}"); break;
                    case AddScopedRegistry reg:    sb.AppendLine($"  • Register scope {reg.Scope} ({reg.Url})"); break;
                    case AddScopeToRegistry sc:    sb.AppendLine($"  • Add scope {sc.Scope} to {sc.Url}"); break;
                    case BackupAndDeletePath del:  sb.AppendLine($"  • Delete Assets/{del.RelativePath}"); break;
                }
            }

            if (warnings != null && warnings.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Warnings:");
                foreach (var w in warnings)
                    sb.AppendLine($"  ⚠ {w.Message}");
            }

            sb.AppendLine();
            sb.Append("This writes directly to Packages/manifest.json. " +
                      "Make sure you have a clean git state — use 'git restore .' to undo if needed.");
            return sb.ToString();
        }
    }
}
