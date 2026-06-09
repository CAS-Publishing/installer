using System.Collections.Generic;
using PSV.Installer.Catalog;

namespace PSV.Installer.Scanner
{
    /// <summary>
    /// Pure classification logic: given a <see cref="PackageRecord"/> or <see cref="ExternalRecord"/>
    /// plus probe data, produces the appropriate scan result.
    /// No I/O; no side effects.
    /// </summary>
    internal static class StateClassifier
    {
        // ── PackageRecord classification ─────────────────────────────────────

        /// <summary>
        /// Classifies a <see cref="PackageRecord"/> using pre-collected probe data and
        /// returns the corresponding <see cref="PackageScanResult"/>.
        /// </summary>
        /// <param name="record">Catalog entry being classified.</param>
        /// <param name="manifest">Parsed manifest.json snapshot.</param>
        /// <param name="existingLegacyPaths">
        /// Legacy asset paths that actually exist on disk (already filtered by AssetProbe).
        /// </param>
        public static PackageScanResult Classify(
            PackageRecord record,
            ManifestData manifest,
            IReadOnlyList<string> existingLegacyPaths)
        {
            var deps = manifest.Dependencies;

            // Probe: is the canonical id in manifest?
            deps.TryGetValue(record.Id, out var canonicalVersion);
            bool hasCanonical = canonicalVersion != null;

            // Probe: is any legacy npm id in manifest?
            string detectedLegacyNpmId = null;
            string legacyNpmVersion = null;
            if (record.LegacyNpmIds != null)
            {
                foreach (var lid in record.LegacyNpmIds)
                {
                    if (string.IsNullOrEmpty(lid)) continue;
                    if (deps.TryGetValue(lid, out var lv))
                    {
                        detectedLegacyNpmId = lid;
                        legacyNpmVersion = lv;
                        break; // first hit wins; Conflict check below handles multiples via bool flags
                    }
                }
            }

            bool hasLegacyNpm = detectedLegacyNpmId != null;
            bool hasLegacyAssets = existingLegacyPaths != null && existingLegacyPaths.Count > 0;

            // ── Conflict detection ────────────────────────────────────────────
            // Conflict if:
            //   (a) canonical AND a legacy npm id are both in manifest, OR
            //   (b) any manifest id (canonical or legacy) AND legacy asset paths also present.
            bool anyManifestId = hasCanonical || hasLegacyNpm;
            if ((hasCanonical && hasLegacyNpm) || (anyManifestId && hasLegacyAssets))
            {
                // Prefer canonical version in the result; fall back to legacy npm version.
                var conflictVersion = canonicalVersion ?? legacyNpmVersion;
                return new PackageScanResult(
                    record.Id, record.DisplayName,
                    PackageState.Conflict,
                    conflictVersion,
                    detectedLegacyNpmId,
                    existingLegacyPaths);
            }

            // ── LegacyUpm ─────────────────────────────────────────────────────
            if (hasLegacyNpm)
            {
                return new PackageScanResult(
                    record.Id, record.DisplayName,
                    PackageState.LegacyUpm,
                    legacyNpmVersion,
                    detectedLegacyNpmId,
                    EmptyPaths);
            }

            // ── LegacyAssets ──────────────────────────────────────────────────
            if (hasLegacyAssets && !hasCanonical)
            {
                return new PackageScanResult(
                    record.Id, record.DisplayName,
                    PackageState.LegacyAssets,
                    null,
                    null,
                    existingLegacyPaths);
            }

            // ── Canonical UPM states ──────────────────────────────────────────
            if (hasCanonical)
            {
                var state = ClassifyVersion(canonicalVersion, record.MinVersion, record.RecommendedVersion);
                return new PackageScanResult(
                    record.Id, record.DisplayName,
                    state,
                    canonicalVersion,
                    null,
                    EmptyPaths);
            }

            // ── NotInstalled ──────────────────────────────────────────────────
            return new PackageScanResult(
                record.Id, record.DisplayName,
                PackageState.NotInstalled,
                null, null, EmptyPaths);
        }

        // ── ExternalRecord classification ────────────────────────────────────

        /// <summary>
        /// Classifies an <see cref="ExternalRecord"/> against manifest data.
        /// </summary>
        public static ExternalScanResult Classify(ExternalRecord record, ManifestData manifest)
        {
            var deps = manifest.Dependencies;

            if (!deps.TryGetValue(record.Id, out var version))
            {
                return new ExternalScanResult(
                    record.Id, record.DisplayName,
                    ExternalState.NotInstalled,
                    null);
            }

            // Found in dependencies — check if any required scope is registered.
            bool anyScope = false;
            if (record.Scopes != null)
            {
                foreach (var scope in record.Scopes)
                {
                    if (manifest.HasRegisteredScope(scope))
                    {
                        anyScope = true;
                        break;
                    }
                }
            }
            else
            {
                // No scopes required by catalog — treat as fine.
                anyScope = true;
            }

            var state = anyScope ? ExternalState.UpmCurrent : ExternalState.ScopeMissing;
            return new ExternalScanResult(record.Id, record.DisplayName, state, version);
        }

        // ── Version classification helpers ───────────────────────────────────

        /// <summary>
        /// Given the detected version and the catalog version constraints, determines
        /// the appropriate <see cref="PackageState"/> for a canonically-installed package.
        /// </summary>
        private static PackageState ClassifyVersion(
            string detected,
            string minVersion,
            string recommendedVersion)
        {
            // "No constraints" path — any version is current.
            bool hasMin = !string.IsNullOrEmpty(minVersion);
            bool hasRecommended = !string.IsNullOrEmpty(recommendedVersion);

            if (!hasMin && !hasRecommended)
                return PackageState.UpmCurrent;

            // Check below minimum first (most severe).
            if (hasMin && CatalogUpdater.IsNewer(minVersion, detected))
                return PackageState.UpmBelowMin;

            // At or above min — check vs recommended.
            if (hasRecommended && CatalogUpdater.IsNewer(recommendedVersion, detected))
                return PackageState.UpmOutdated;

            return PackageState.UpmCurrent;
        }

        private static readonly string[] EmptyPaths = System.Array.Empty<string>();
    }
}
