using System.Collections.Generic;
using PSV.Installer.Catalog;
using PSV.Installer.Scanner;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// View-model for one component row on the Components Overview screen,
    /// derived from a live project scan (read-only this iteration).
    /// </summary>
    internal sealed class ComponentStatus
    {
        public string Id;
        public string DisplayName;
        public string Sub;
        public string Logo;          // logo modifier suffix → .cas-logo--<Logo>
        public string Tone;          // "green" | "yellow" | "red" | "grey"
        public string StatusText;
        public string ActionText;
        public string ActionVariant; // "muted" | "primary" | "warn"
        public string Version;       // detected version, may be null
        public bool   Actionable;    // false when nothing to do (button disabled)
        public bool   Installed;     // true when the package is present (any non-NotInstalled state)
        public bool   OutsideUpm;    // detected via .unitypackage/manual, not UPM → action = Migrate
    }

    /// <summary>
    /// Reads the default client component set (CAS SDK, Tenjin, Firebase Analytics) from the
    /// live catalog + project scan. Read-only: no project mutation. Reuses the existing
    /// <see cref="CatalogLoader"/> / <see cref="ProjectScanner"/> backend.
    ///
    /// Note: install state is the package-manager truth (manifest.json). A leftover settings
    /// folder under Assets/ (e.g. CAS config) is NOT treated as "installed" — only the actual
    /// package counts.
    /// </summary>
    internal static class ComponentStatusProvider
    {
        // Default client components. Display name / sub / logo are wizard-side (presentation);
        // installation state + version come from the live scan against the catalog.
        private static readonly (string Id, string Name, string Sub, string Logo)[] Defaults =
        {
            ("com.cleversolutions.ads.unity", "CAS SDK",            "Ads / Mediation", "cas"),
            ("com.tenjin.sdk",                "Tenjin SDK",         "Attribution",     "tenjin"),
            ("com.google.firebase.analytics", "Firebase Analytics", "Analytics",       "firebase"),
        };

        /// <summary>Package ids of the default component set, in display order.</summary>
        public static IReadOnlyList<string> DefaultIds
        {
            get
            {
                var ids = new List<string>(Defaults.Length);
                foreach (var d in Defaults) ids.Add(d.Id);
                return ids;
            }
        }

        /// <summary>Display name + logo suffix for a default component id (presentation-side).</summary>
        public static bool TryGetDefaultDisplay(string id, out string name, out string logo)
        {
            foreach (var d in Defaults)
                if (d.Id == id) { name = d.Name; logo = d.Logo; return true; }
            name = id;
            logo = null;
            return false;
        }

        /// <summary>
        /// Returns one <see cref="ComponentStatus"/> per default component. On catalog
        /// failure returns false with a human-readable <paramref name="error"/>.
        /// </summary>
        public static bool TryGetStatuses(out List<ComponentStatus> statuses, out string error)
        {
            statuses = new List<ComponentStatus>();
            error = null;

            var load = CatalogLoader.Load();
            if (load.Status != CatalogLoadStatus.Ok)
            {
                error = load.Status == CatalogLoadStatus.NotInstalled
                    ? "Catalog metadata package is not installed yet — live component status is unavailable."
                    : $"Catalog could not be read: {load.Error}";
                return false;
            }

            var report = ProjectScanner.Scan(load.Catalog);

            var pkgById = new Dictionary<string, PackageScanResult>();
            if (report.Packages != null)
                foreach (var p in report.Packages)
                    if (p != null) pkgById[p.Id] = p;

            var extById = new Dictionary<string, ExternalScanResult>();
            if (report.External != null)
                foreach (var e in report.External)
                    if (e != null) extById[e.Id] = e;

            foreach (var d in Defaults)
            {
                if (pkgById.TryGetValue(d.Id, out var p))
                    statuses.Add(FromPackage(d, p));
                else if (extById.TryGetValue(d.Id, out var e))
                    statuses.Add(FromExternal(d, e));
                else
                    statuses.Add(NotInCatalog(d));
            }

            return true;
        }

        private static ComponentStatus Base(in (string Id, string Name, string Sub, string Logo) d) =>
            new ComponentStatus { Id = d.Id, DisplayName = d.Name, Sub = d.Sub, Logo = d.Logo };

        private static ComponentStatus FromPackage(in (string Id, string Name, string Sub, string Logo) d, PackageScanResult p)
        {
            var s = Base(d);
            s.Version = p.DetectedVersion;
            s.Installed = p.State != PackageState.NotInstalled;
            switch (p.State)
            {
                case PackageState.UpmCurrent:
                    s.Tone = "green";  s.StatusText = "Installed";       s.ActionText = "Up to date"; s.ActionVariant = "muted";   s.Actionable = false; break;
                case PackageState.UpmOutdated:
                    s.Tone = "yellow"; s.StatusText = "Update available"; s.ActionText = "Update";    s.ActionVariant = "warn";    s.Actionable = true;  break;
                case PackageState.UpmBelowMin:
                    s.Tone = "red";    s.StatusText = "Too old";          s.ActionText = "Update";    s.ActionVariant = "warn";    s.Actionable = true;  break;
                case PackageState.LegacyUpm:
                case PackageState.LegacyAssets:
                    s.Tone = "yellow"; s.StatusText = "Needs migration";  s.ActionText = "Migrate";   s.ActionVariant = "warn";    s.Actionable = true;  break;
                case PackageState.Conflict:
                    s.Tone = "red";    s.StatusText = "Mixed install";    s.ActionText = "Fix";       s.ActionVariant = "warn";    s.Actionable = true;  break;
                case PackageState.NotInstalled:
                default:
                    s.Tone = "red";    s.StatusText = "Not Installed";    s.ActionText = "Install";   s.ActionVariant = "primary"; s.Actionable = true;  break;
            }
            if (PSV.Installer.Scanner.StateClassifier.IsGitSpec(s.Version) && s.StatusText == "Installed")
                s.StatusText = "Installed (git)";
            return s;
        }

        private static ComponentStatus FromExternal(in (string Id, string Name, string Sub, string Logo) d, ExternalScanResult e)
        {
            var s = Base(d);
            s.Version = e.DetectedVersion;
            s.Installed = e.State != ExternalState.NotInstalled;
            switch (e.State)
            {
                case ExternalState.UpmCurrent:
                    s.Tone = "green";  s.StatusText = "Installed";      s.ActionText = "Up to date"; s.ActionVariant = "muted";   s.Actionable = false; break;
                case ExternalState.ScopeMissing:
                    s.Tone = "yellow"; s.StatusText = "Needs registry"; s.ActionText = "Fix";        s.ActionVariant = "warn";    s.Actionable = true;  break;
                case ExternalState.InstalledOutsideUpm:
                    s.Tone = "yellow"; s.StatusText = "Installed (manual)"; s.ActionText = "Migrate to UPM"; s.ActionVariant = "warn"; s.Actionable = true;
                    s.OutsideUpm = true; break;
                case ExternalState.NotInstalled:
                default:
                    s.Tone = "red";    s.StatusText = "Not Installed";  s.ActionText = "Install";    s.ActionVariant = "primary"; s.Actionable = true;  break;
            }
            if (PSV.Installer.Scanner.StateClassifier.IsGitSpec(s.Version) && s.StatusText == "Installed")
                s.StatusText = "Installed (git)";
            return s;
        }

        private static ComponentStatus NotInCatalog(in (string Id, string Name, string Sub, string Logo) d)
        {
            var s = Base(d);
            s.Tone = "grey"; s.StatusText = "Not in catalog"; s.ActionText = "—"; s.ActionVariant = "muted"; s.Actionable = false;
            return s;
        }
    }
}
