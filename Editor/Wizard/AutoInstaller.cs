using System.Collections.Generic;
using System.Text;
using PSV.Installer.Catalog;
using PSV.Installer.Common;
using PSV.Installer.Migrator;
using PSV.Installer.Scanner;
using UnityEditor;
using UnityEngine;

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
        private const string DeadlineKey = "PSV.Installer.Wizard.AutoInstallStepDeadline";
        private const string ResumeKey = "PSV.Installer.Wizard.ResumeRecommendedAfterCatalog";

        private const string LogPrefix = "[PSV Installer]";

        public static bool IsActive => SessionState.GetBool(ActiveKey, false);

        /// <summary>
        /// True (consuming the flag) when a recommended-install click hit a not-yet-installed catalog
        /// and armed a resume. The wizard consumes this on its next open once the catalog is present,
        /// to continue the recommended install automatically — so the user needn't click again after
        /// Unity finishes installing the metadata catalog. Returns false (and consumes nothing extra)
        /// when no resume is armed.
        /// </summary>
        public static bool ConsumeResume()
        {
            if (!SessionState.GetBool(ResumeKey, false)) return false;
            SessionState.SetBool(ResumeKey, false);
            return true;
        }

        public static int IssuedIndex
        {
            get => SessionState.GetInt(IssuedKey, -1);
            set => SessionState.SetInt(IssuedKey, value);
        }

        /// <summary>
        /// Wall-clock deadline (<see cref="EditorApplication.timeSinceStartup"/>) by which the
        /// currently-issued step must resolve, or the Progress watchdog surfaces a stall. 0 = unarmed.
        /// Stored in SessionState so it survives the per-step domain reload. Float precision is ample
        /// for a seconds value over one editor session.
        /// </summary>
        public static double StepDeadline
        {
            get => SessionState.GetFloat(DeadlineKey, 0f);
            set => SessionState.SetFloat(DeadlineKey, (float)value);
        }

        public static void Clear()
        {
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetInt(IssuedKey, -1);
            SessionState.SetFloat(DeadlineKey, 0f);
        }

        /// <summary>
        /// True when <paramref name="id"/> is currently a dependency in <c>Packages/manifest.json</c>
        /// (case-insensitive). The driver uses this to confirm an <see cref="InstallOne"/> actually
        /// wrote the component into the manifest — a plan that adds nothing would otherwise leave the
        /// progress poll waiting forever for a package that can never resolve.
        /// </summary>
        public static bool ManifestHasDependency(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            var data = ManifestProbe.Read();
            if (!data.Readable || data.Dependencies == null) return false;
            foreach (var kv in data.Dependencies)
                if (string.Equals(kv.Key, id, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
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
            // Automatic integration should yield a buildable project — ensure the Android build
            // templates exist before EDM4U resolves the installed packages' dependencies.
            AndroidBuildFix.Ensure();

            // The async metadata install may have finished (and its reload run) since the catalog was
            // last read this session — drop the session cache so we don't act on a stale NotInstalled.
            CatalogLoader.InvalidateCache();
            var load = CatalogLoader.Load();
            if (load.Status != CatalogLoadStatus.Ok)
            {
                if (load.Status == CatalogLoadStatus.NotInstalled)
                {
                    // First run — especially a git-installed client — can reach here before the metadata
                    // catalog has finished its asynchronous install + domain reload (the wizard kicks the
                    // install off on open, but it isn't resolvable until Unity reloads). (Re-)trigger the
                    // install now and ARM a resume: when the catalog becomes present the wizard continues
                    // this recommended install automatically (ConsumeResume in CreateGUI), so the user
                    // doesn't have to click again.
                    SessionState.SetBool(ResumeKey, true);
                    PSV.Installer.Bootstrap.EnsureMetadata();
                    EditorUtility.DisplayDialog("PSV Installer",
                        "The package catalog is installing. Unity will reload when it's ready and the " +
                        "installer will continue automatically — no need to click again.", "OK");
                }
                else // Unreadable
                {
                    EditorUtility.DisplayDialog("PSV Installer",
                        "The package catalog is installed but couldn't be read:\n" +
                        (load.Error ?? "unknown error") +
                        "\n\nReinstall com.psvgamestudio.installer.metadata.", "OK");
                }
                return;
            }

            var report = ProjectScanner.Scan(load.Catalog);
            var plan = MigrationPlanner.Plan(
                load.Catalog, report, new IdSetSelection(ComponentStatusProvider.DefaultIds), InstallMethodState.Get(), out var warnings);

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

            var method = InstallMethodState.Get();
            var report = ProjectScanner.Scan(load.Catalog);
            var plan = MigrationPlanner.Plan(load.Catalog, report, new IdSetSelection(new[] { id }), method, out _);

            // Diagnostic: a healthy install for a not-yet-present component must produce at least one
            // Add/AddGit action. An empty (or registry-only) plan here is the documented cause of the
            // auto-install "spinner forever" — the manifest never gains the dependency, so the poll
            // waits on a package that can never resolve. Log the plan so a stall is explainable.
            Debug.Log($"{LogPrefix} InstallOne('{id}', method={method}): plan has {plan.Count} action(s) — " +
                      DescribePlan(plan));

            var result = new MigrationRunner().Apply(plan);
            if (!result.Success)
                Debug.LogWarning($"{LogPrefix} InstallOne('{id}') apply failed: " +
                                 string.Join("; ", result.Failures));
            return result;
        }

        private static string DescribePlan(IReadOnlyList<MigrationAction> plan)
        {
            if (plan == null || plan.Count == 0) return "(empty)";
            var sb = new StringBuilder();
            for (var i = 0; i < plan.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                switch (plan[i])
                {
                    case AddPackage add:           sb.Append($"Add {add.Id}@{add.Version}"); break;
                    case AddGitPackage git:        sb.Append($"AddGit {git.Id} ({git.Spec})"); break;
                    case UpdatePackageVersion upd: sb.Append($"Update {upd.Id}->{upd.Version}"); break;
                    case RemovePackage rem:        sb.Append($"Remove {rem.Id}"); break;
                    case AddScopedRegistry reg:    sb.Append($"Registry {reg.Scope}"); break;
                    case AddScopeToRegistry sc:    sb.Append($"Scope {sc.Scope}"); break;
                    case BackupAndDeletePath del:  sb.Append($"Delete {del.RelativePath}"); break;
                    default:                       sb.Append(plan[i].GetType().Name); break;
                }
            }
            return sb.ToString();
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
                    case AddGitPackage git:        sb.AppendLine($"  • Add {git.Id} (git: {git.Spec})"); break;
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
