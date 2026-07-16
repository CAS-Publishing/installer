using System;
using System.IO;
using PSV.Installer.Catalog;
using PSV.Installer.Common;
using PSV.Installer.Migrator;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace PSV.Installer
{
    internal static class MetadataAutoInstall
    {
        private const string LogPrefix = "[CAS Hub]";
        private const string PsvRegistryName = "PSV Game Studio";
        private const string PsvRegistryUrl = CatalogUpdater.PsvRegistryRoot;
        private const string RequiredScope = "com.psvgamestudio";
        private const string InstallAttemptedKey = "PSV.Installer.MetadataInstallAttemptedThisSession";

        /// <summary>
        /// Called by Bootstrap when <see cref="CatalogLoader.Load"/> reports NotInstalled (metadata
        /// package not yet registered in this project). Ensures the PSV scoped registry is present in
        /// Packages/manifest.json, then queries Verdaccio for the latest metadata version and
        /// fires a UPM Client.Add. Fire-and-forget — Unity will reimport and Bootstrap will
        /// succeed on the next domain reload. Idempotent across multiple calls within the same
        /// domain reload.
        /// </summary>
        /// <param name="force">A manual trigger (wizard Refresh / open) clears the once-per-session
        /// failure throttle so the install is actually re-attempted now.</param>
        public static void Run(bool force = false)
        {
            // Already registered → nothing to do.
            if (IsMetadataInstalled()) return;

            if (force) SessionState.SetBool(InstallAttemptedKey, false);

            // The once-per-session guard exists ONLY to stop re-probing the registry on every domain
            // reload when offline/auth fails — it is set only on a genuine failure (below), never up
            // front, so a successful install or a remove+reinstall in the same session isn't blocked
            // until an editor restart.
            if (SessionState.GetBool(InstallAttemptedKey, false))
                return;

            // Git-installed installer → fetch metadata over git too (no scoped registry, no Verdaccio
            // probe). Keeps a fully-git client free of any com.psvgamestudio scoped registry.
            if (InstallerSource.IsGit())
            {
                Debug.Log($"{LogPrefix} metadata not detected; installing via git mirror…");
                try
                {
                    // Success: do NOT set the guard — IsMetadataInstalled() short-circuits future calls.
                    CatalogUpdater.TrackInstall(CatalogUpdater.InstallGit(), "Metadata (git)");
                    // We just resolved metadata from the git mirror's main — don't let the post-install
                    // reload's MaybeAutoUpdate re-resolve the same URL and queue a redundant reload that
                    // could tear down the just-auto-opened wizard.
                    PSV.Installer.Bootstrap.SuppressMetadataUpdateThisSession();
                }
                catch (Exception e)
                {
                    // A brand-new project's initial import keeps the Package Manager busy, so this
                    // first Add often throws a transient "exclusive access … in progress" collision.
                    // Latching the session throttle on that would strand metadata (and the auto-open)
                    // for the whole session — only throttle on a terminal failure; retry transient ones
                    // on the next domain reload.
                    var transient = InstallRetryPolicy.IsTransient(e.Message);
                    if (!transient) SessionState.SetBool(InstallAttemptedKey, true);
                    Debug.LogWarning($"{LogPrefix} Metadata git Client.Add failed " +
                                     $"({(transient ? "transient — will retry after reload" : "terminal — throttled until restart")}): {e.Message}");
                }
                return;
            }

            Debug.Log($"{LogPrefix} metadata package not detected; installing…");

            // Step 1 — ensure scoped registry in manifest.json.
            if (!EnsureScopedRegistry())
            {
                SessionState.SetBool(InstallAttemptedKey, true); // failed — throttle until restart
                return; // warning already logged inside
            }

            // Step 2 — query Verdaccio for latest version, then install.
            CatalogUpdater.CheckRemoteLatestVersion(
                onSuccess: version =>
                {
                    Debug.Log($"{LogPrefix} Installing metadata package: {CatalogLoader.MetadataPackageName}@{version}…");
                    try
                    {
                        // Success path: do NOT set the guard — once installed, IsMetadataInstalled()
                        // short-circuits future calls; if it's later removed we want to retry.
                        CatalogUpdater.TrackInstall(
                            CatalogUpdater.InstallVersion(version), "Metadata");
                    }
                    catch (Exception e)
                    {
                        // Same transient/terminal split as the git path — a PM-busy collision must not
                        // burn the session's one install attempt.
                        var transient = InstallRetryPolicy.IsTransient(e.Message);
                        if (!transient) SessionState.SetBool(InstallAttemptedKey, true);
                        Debug.LogWarning($"{LogPrefix} Client.Add failed " +
                                         $"({(transient ? "transient — will retry after reload" : "terminal — throttled until restart")}): {e.Message}");
                    }
                },
                onFailure: err =>
                {
                    SessionState.SetBool(InstallAttemptedKey, true); // failed — throttle until restart
                    Debug.LogWarning(
                        $"{LogPrefix} Could not query registry for metadata version. " +
                        $"Will retry after an editor restart. Reason: {err}");
                });
        }

        // -----------------------------------------------------------------------
        //  Helpers
        // -----------------------------------------------------------------------

        private static bool IsMetadataInstalled()
        {
            try
            {
                foreach (var pkg in UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages())
                {
                    if (pkg.name == CatalogLoader.MetadataPackageName)
                        return true;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LogPrefix} Could not enumerate registered packages: {e.Message}");
            }
            return false;
        }

        /// <summary>
        /// Delegates to <see cref="ManifestWriter.ApplyActions"/> to ensure the PSV
        /// scoped registry is registered in Packages/manifest.json.
        /// <see cref="ManifestWriter"/> is idempotent: if the registry and scope are
        /// already present it does not rewrite the file.
        /// Returns false on any I/O error (warning already logged).
        /// </summary>
        private static bool EnsureScopedRegistry()
        {
            var manifestPath = GetManifestPath();
            if (manifestPath == null)
            {
                Debug.LogWarning($"{LogPrefix} Could not locate Packages/manifest.json. Aborting bootstrap.");
                return false;
            }

            try
            {
                // AddScopedRegistry is idempotent in ManifestWriter: if the URL already
                // exists with the required scope, nothing is written.
                ManifestWriter.ApplyActions(manifestPath, new MigrationAction[]
                {
                    new AddScopedRegistry(PsvRegistryName, PsvRegistryUrl, RequiredScope),
                });
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LogPrefix} Failed to ensure scoped registry in manifest.json: {e.Message}");
                return false;
            }
        }

        private static string GetManifestPath()
        {
            // Application.dataPath is "<project>/Assets". manifest.json is one level up under "Packages/".
            try
            {
                var projectRoot = Path.GetDirectoryName(UnityEngine.Application.dataPath);
                if (projectRoot == null) return null;
                var path = Path.Combine(projectRoot, "Packages", "manifest.json");
                return File.Exists(path) ? path : null;
            }
            catch
            {
                return null;
            }
        }
    }
}
