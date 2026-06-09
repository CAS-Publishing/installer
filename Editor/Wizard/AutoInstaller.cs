using System.Collections.Generic;
using System.Text;
using PSV.Installer.Catalog;
using PSV.Installer.Migrator;
using PSV.Installer.Scanner;
using UnityEditor;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// "Make everything for me" — installs the default components ONE AT A TIME (step by step),
    /// driven by the Progress screen. Each <see cref="InstallOne"/> writes the manifest and
    /// triggers a UPM resolve + domain reload; after the reload the Progress driver re-evaluates
    /// what's resolved and kicks off the next component, until all are done → Done.
    ///
    /// State lives in SessionState so it survives those reloads:
    ///   • Active        — an auto-install run is in progress.
    ///   • IssuedIndex   — highest component index whose install has been issued (so the driver
    ///                     doesn't re-issue the same install while it's still resolving).
    /// </summary>
    internal static class AutoInstaller
    {
        private const string ActiveKey = "PSV.Installer.Wizard.AutoInstallActive";
        private const string IssuedKey = "PSV.Installer.Wizard.AutoInstallIssuedIndex";

        public static bool IsActive => SessionState.GetBool(ActiveKey, false);

        public static int IssuedIndex
        {
            get => SessionState.GetInt(IssuedKey, -1);
            set => SessionState.SetInt(IssuedKey, value);
        }

        public static void Clear()
        {
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetInt(IssuedKey, -1);
        }

        /// <summary>Selects a fixed id set at the recommended version target.</summary>
        private sealed class IdSetSelection : ISelectionSet
        {
            private readonly HashSet<string> _ids;
            public IdSetSelection(IEnumerable<string> ids) { _ids = new HashSet<string>(ids); }
            public bool IsSelected(string id) => _ids.Contains(id);
            public VersionTarget GetTarget(string id) => VersionTarget.Recommended;
        }

        /// <summary>
        /// Confirms, then begins the step-by-step run: marks active and routes to Progress, whose
        /// driver performs the installs one component at a time. When everything is already
        /// installed, routes straight to Done. No installs happen here — the Progress driver owns
        /// the sequence so it can survive the per-step domain reloads.
        /// </summary>
        public static void StartAll(WizardRouter router)
        {
            var load = CatalogLoader.Load();
            if (load.Status != CatalogLoadStatus.Ok)
            {
                EditorUtility.DisplayDialog("PSV Installer",
                    "Catalog is unavailable — cannot install. " +
                    (load.Error ?? "Make sure the metadata package is installed."), "OK");
                return;
            }

            var report = ProjectScanner.Scan(load.Catalog);
            var plan = MigrationPlanner.Plan(
                load.Catalog, report, new IdSetSelection(ComponentStatusProvider.DefaultIds), out var warnings);

            if (plan.Count == 0)
            {
                Clear();
                InstallerWizardWindow.IntroDone = true; // nothing to install → intro is complete
                router.GoTo("done");
                return;
            }

            if (!EditorUtility.DisplayDialog("PSV Installer",
                    BuildSummary(plan, warnings), "Install all", "Cancel"))
                return; // cancelled → stay on the first screen; intro not marked done

            InstallerWizardWindow.IntroDone = true; // committed to install → past the intro
            SessionState.SetBool(ActiveKey, true);
            IssuedIndex = -1;
            router.GoTo("progress");
        }

        /// <summary>
        /// Installs/updates a single component (no dialog — the batch was confirmed in StartAll).
        /// Writes the manifest + triggers resolve/reload.
        /// </summary>
        public static ApplyResult InstallOne(string id)
        {
            var load = CatalogLoader.Load();
            if (load.Status != CatalogLoadStatus.Ok)
                return new ApplyResult(false, 0, new[] { "Catalog unavailable." });

            var report = ProjectScanner.Scan(load.Catalog);
            var plan = MigrationPlanner.Plan(load.Catalog, report, new IdSetSelection(new[] { id }), out _);
            return new MigrationRunner().Apply(plan);
        }

        private static string BuildSummary(IReadOnlyList<MigrationAction> plan, IReadOnlyList<PlannerWarning> warnings)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Install all default components (CAS SDK, Tenjin, Firebase Analytics)?");
            sb.AppendLine("They will be installed one at a time.");
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
            sb.Append("Writes directly to Packages/manifest.json. " +
                      "Make sure you have a clean git state — use 'git restore .' to undo if needed.");
            return sb.ToString();
        }
    }
}
