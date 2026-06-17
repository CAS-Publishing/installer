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
            var plan = MigrationPlanner.Plan(load.Catalog, report, new SingleSelection(componentId), InstallMethodState.Get(), out var warnings);

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

        /// <summary>
        /// Removes a single component from <c>Packages/manifest.json</c> (one <see cref="RemovePackage"/>
        /// action) behind a confirm dialog. Doubles as a recovery path for a botched install. The
        /// scoped registry is left in place (harmless, and other PSV packages may still need it).
        /// Returns true when a removal was attempted (success OR failure) so the caller re-scans;
        /// false when cancelled.
        /// </summary>
        public static bool Remove(string componentId, string displayName)
        {
            var plan = new List<MigrationAction> { new RemovePackage(componentId) };

            if (!EditorUtility.DisplayDialog("PSV Installer",
                    $"Remove {displayName}?\n\n  • Remove {componentId}\n\n" +
                    "This writes directly to Packages/manifest.json. " +
                    "Make sure you have a clean git state — use 'git restore .' to undo if needed.",
                    "Remove", "Cancel"))
                return false;

            var result = new MigrationRunner().Apply(plan);
            if (result.Success)
            {
                Debug.Log($"[PSV Installer Wizard] Removed {displayName} ({componentId}). " +
                          "Unity will resolve packages now.");
            }
            else
            {
                EditorUtility.DisplayDialog("PSV Installer",
                    $"Remove failed for {displayName}:\n• " + string.Join("\n• ", result.Failures) +
                    "\n\nNo backup is kept — use 'git status' / 'git restore .' to inspect or revert.", "OK");
            }

            return true; // re-scan regardless: reflect the real resulting state
        }

        /// <summary>
        /// Migrates an external that was installed OUTSIDE UPM (e.g. via .unitypackage) to the UPM
        /// package: deletes the detected Assets/ folder(s) FIRST (guarded by git/path safety), and
        /// only on success registers the scope + adds the UPM package. Two steps on purpose — a
        /// single manifest-first plan would add the UPM package even if the delete is blocked
        /// (untracked files), leaving a duplicate. Returns true when anything was attempted.
        /// </summary>
        public static bool MigrateExternal(string componentId, string displayName)
        {
            var load = CatalogLoader.Load();
            if (load.Status != CatalogLoadStatus.Ok)
            {
                EditorUtility.DisplayDialog("PSV Installer",
                    "Catalog is unavailable — cannot migrate. " +
                    (load.Error ?? "Make sure the metadata package is installed."), "OK");
                return false;
            }

            var catalog = load.Catalog;
            ExternalRecord rec = null;
            if (catalog.External != null)
                foreach (var e in catalog.External)
                    if (e != null && e.Id == componentId) { rec = e; break; }
            if (rec == null)
            {
                EditorUtility.DisplayDialog("PSV Installer",
                    $"{displayName} is not an external record in the catalog — cannot migrate.", "OK");
                return false;
            }

            var baseVersion = !string.IsNullOrEmpty(rec.RecommendedVersion) ? rec.RecommendedVersion : rec.MinVersion;
            if (string.IsNullOrEmpty(baseVersion))
            {
                EditorUtility.DisplayDialog("PSV Installer",
                    $"No version configured for {displayName} in the catalog — cannot migrate.", "OK");
                return false;
            }

            // Which UPM package(s) to install. A multi-module SDK (rec.Modules — e.g. Firebase ships
            // Analytics/RemoteConfig/Installations under one Assets/Firebase folder) installs EVERY
            // module whose markers are detected on disk, so deleting the shared folder doesn't drop
            // modules the project still uses. A single-package external installs just rec.Id.
            var installs = ResolveInstallSet(rec, baseVersion);

            // Presence was detected by reflection (loaded types); locate the actual folder(s)
            // on demand for deletion. Reflection can see the SDK without a pin-pointable folder
            // (e.g. odd layout) — then we can't auto-delete, so ask the user to remove it manually.
            var roots = AssetInstallProbe.FindRootsForMigration(rec.AssetMarkers);
            if (roots.Count == 0)
            {
                EditorUtility.DisplayDialog("PSV Installer",
                    $"{displayName} appears to be installed manually, but its files couldn't be " +
                    "located under Assets/ (no matching asmdef / DLL / script folder). Remove the " +
                    "manual copy yourself, then use Install to add the UPM version.", "OK");
                return false;
            }

            // Extra SDK-owned satellite folders the .unitypackage drops OUTSIDE the primary root
            // (EDM, the SDK's own Editor Default Resources subfolder…). Only existing ones are kept;
            // shared roots (Assets/Plugins) are never in this list — they're warned about below.
            var extraRoots = AssetProbe.FindExisting(rec.ExtraCleanupPaths);
            var deletePaths = new List<string>(roots);
            foreach (var p in extraRoots)
                if (!deletePaths.Contains(p)) deletePaths.Add(p);

            // Shared folder (Assets/Plugins) may hold this SDK's native libs ALONGSIDE other SDKs' —
            // never auto-deleted. Surface matching files so the user can prune them by hand.
            var sharedLeftovers = AssetInstallProbe.FindLooseFiles(rec.AssetMarkers, "Plugins");

            var sb = new StringBuilder();
            sb.AppendLine($"Migrate {displayName} from a manual install to UPM?");
            sb.AppendLine();
            sb.AppendLine("Install via UPM:");
            foreach (var a in installs) sb.AppendLine($"  • {a.Id}@{a.Version}");
            sb.AppendLine();
            sb.AppendLine("Delete the manual copy:");
            foreach (var r in deletePaths) sb.AppendLine($"  • Assets/{r}");
            if (sharedLeftovers.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("⚠ Shared folder — NOT deleted automatically; remove these by hand after migrating:");
                foreach (var f in sharedLeftovers) sb.AppendLine($"  • Assets/{f}");
            }
            sb.AppendLine();
            sb.Append("Files are removed via git (they must be committed/clean to be recoverable). " +
                      "Use 'git restore .' to undo if needed.");

            if (!EditorUtility.DisplayDialog("PSV Installer", sb.ToString(), "Migrate", "Cancel"))
                return false;

            // STEP 1 — delete the manual copy FIRST. If git can't recover it (untracked/dirty), this
            // fails before any manifest change, so we never end up with both copies (a duplicate).
            var deletePlan = new List<MigrationAction>();
            foreach (var r in deletePaths) deletePlan.Add(new BackupAndDeletePath(r));

            var del = new MigrationRunner().Apply(deletePlan);
            if (!del.Success)
            {
                EditorUtility.DisplayDialog("PSV Installer",
                    $"Couldn't remove the manual copy of {displayName}:\n• " +
                    string.Join("\n• ", del.Failures) +
                    "\n\nCommit those files to git first (so they're recoverable), or delete them " +
                    "manually, then migrate again. manifest.json was NOT changed.", "OK");
                return true; // re-scan: state is unchanged but the user acted
            }

            // STEP 2 — UPM install (registry scope + every resolved module). Safe now the copy is gone.
            var addPlan = new List<MigrationAction>();
            if (rec.Scopes != null)
            {
                var url = ResolveRegistryUrl(catalog, rec);
                foreach (var scope in rec.Scopes)
                    if (!string.IsNullOrEmpty(scope))
                        addPlan.Add(new AddScopedRegistry(rec.Registry ?? string.Empty, url, scope));
            }
            foreach (var a in installs) addPlan.Add(a);

            var add = new MigrationRunner().Apply(addPlan);
            if (add.Success)
            {
                var installed = new StringBuilder();
                foreach (var a in installs)
                {
                    if (installed.Length > 0) installed.Append(", ");
                    installed.Append($"{a.Id}@{a.Version}");
                }
                Debug.Log($"[PSV Installer Wizard] Migrated {displayName} to UPM ({installed}). " +
                          "Unity will resolve packages now.");
                if (sharedLeftovers.Count > 0)
                    Debug.LogWarning($"[PSV Installer Wizard] {displayName}: files in the shared Assets/Plugins " +
                        "folder were NOT deleted — remove them by hand to avoid conflicts: Assets/" +
                        string.Join(", Assets/", sharedLeftovers));
            }
            else
            {
                EditorUtility.DisplayDialog("PSV Installer",
                    $"The manual copy was removed, but the UPM install of {displayName} failed:\n• " +
                    string.Join("\n• ", add.Failures) +
                    "\n\nRun Install again from the Components tab.", "OK");
            }
            return true;
        }

        /// <summary>
        /// Resolves the set of UPM packages to install when migrating <paramref name="rec"/>. For a
        /// multi-module external it returns one <see cref="AddPackage"/> per module whose markers are
        /// detected among the loaded types (at <c>module.RecommendedVersion ?? baseVersion</c>); for a
        /// single-package external it returns just <c>rec.Id@baseVersion</c>. Never empty — if a
        /// multi-module record somehow matches no module (the external was still flagged as present),
        /// it falls back to the primary id so a migration never installs nothing.
        /// </summary>
        internal static List<AddPackage> ResolveInstallSet(ExternalRecord rec, string baseVersion)
        {
            // Reflection over loaded types only when the record actually declares modules.
            var loaded = (rec.Modules != null && rec.Modules.Count > 0)
                ? (ICollection<string>)AssetInstallProbe.CollectLoadedIdentifiers()
                : null;
            return ResolveInstallSet(rec, baseVersion, loaded);
        }

        /// <summary>
        /// Pure core of <see cref="ResolveInstallSet(ExternalRecord,string)"/>: resolves the install
        /// set against an explicit <paramref name="loadedIdentifiers"/> set (no reflection), so it is
        /// unit-testable. See the public overload for the contract.
        /// </summary>
        internal static List<AddPackage> ResolveInstallSet(
            ExternalRecord rec, string baseVersion, ICollection<string> loadedIdentifiers)
        {
            var installs = new List<AddPackage>();

            if (rec.Modules != null && rec.Modules.Count > 0 && loadedIdentifiers != null)
            {
                var seen = new HashSet<string>();
                foreach (var m in rec.Modules)
                {
                    if (m == null || string.IsNullOrEmpty(m.Id)) continue;
                    if (!AssetInstallProbe.IsPresentInIdentifiers(loadedIdentifiers, m.AssetMarkers)) continue;
                    if (!seen.Add(m.Id)) continue;
                    var v = !string.IsNullOrEmpty(m.RecommendedVersion) ? m.RecommendedVersion : baseVersion;
                    installs.Add(new AddPackage(m.Id, v));
                }
            }

            if (installs.Count == 0)
                installs.Add(new AddPackage(rec.Id, baseVersion)); // single-package or safety fallback

            return installs;
        }

        private static string ResolveRegistryUrl(PackageCatalog catalog, ExternalRecord rec)
        {
            var key = rec.Registry;
            if (string.IsNullOrEmpty(key)) return string.Empty;
            if (catalog?.Registries != null && catalog.Registries.TryGetValue(key, out var url))
                return url ?? string.Empty;
            return key; // raw URL in the catalog
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
            sb.Append("This writes directly to Packages/manifest.json. " +
                      "Make sure you have a clean git state — use 'git restore .' to undo if needed.");
            return sb.ToString();
        }
    }
}
