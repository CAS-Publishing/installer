using System.Collections.Generic;
using Newtonsoft.Json;

namespace PSV.Installer.Catalog
{
    public sealed class PackageCatalog
    {
        [JsonProperty("schemaVersion")]
        public int SchemaVersion;

        [JsonProperty("catalogVersion")]
        public string CatalogVersion;

        [JsonProperty("registries")]
        public Dictionary<string, string> Registries;

        [JsonProperty("categories")]
        public List<Category> Categories;

        [JsonProperty("packages")]
        public List<PackageRecord> Packages;

        [JsonProperty("external")]
        public List<ExternalRecord> External;

        [JsonProperty("overrides")]
        public Dictionary<string, object> Overrides;

        /// <summary>
        /// Optional list of legacy packages that need to be removed with no replacement.
        /// Absent in catalog.json → Newtonsoft leaves this null; all callers must
        /// treat null the same as an empty list.  Adding this field is backward-compatible:
        /// older installer versions simply ignore the <c>uninstall</c> key.
        /// </summary>
        [JsonProperty("uninstall")]
        public List<UninstallRecord> Uninstall;
    }

    public sealed class Category
    {
        [JsonProperty("id")]          public string Id;
        [JsonProperty("displayName")] public string DisplayName;
    }

    public sealed class PackageRecord
    {
        [JsonProperty("id")]                 public string Id;
        [JsonProperty("displayName")]        public string DisplayName;
        [JsonProperty("registry")]           public string Registry;
        [JsonProperty("category")]           public string Category;
        [JsonProperty("legacyNpmIds")]       public List<string> LegacyNpmIds;
        [JsonProperty("legacyAssetPaths")]   public List<string> LegacyAssetPaths;
        [JsonProperty("minVersion")]         public string MinVersion;
        [JsonProperty("recommendedVersion")] public string RecommendedVersion;

        /// <summary>Optional post-install configuration requirements (per platform), used to
        /// render the Setup readiness checklist. Absent → no config to verify.</summary>
        [JsonProperty("config")]             public List<ConfigRequirement> Config;

        /// <summary>
        /// Optional git-install chain for this component. When the Git method is chosen, the
        /// installer writes one git-URL dependency per entry here (top-level + transitive),
        /// with no scoped registry. Absent → git method falls back to UPM for this component.
        /// </summary>
        [JsonProperty("git")]                public GitInstall Git;
    }

    public sealed class ExternalRecord
    {
        [JsonProperty("id")]                 public string Id;
        [JsonProperty("displayName")]        public string DisplayName;
        [JsonProperty("registry")]           public string Registry;
        [JsonProperty("scopes")]             public List<string> Scopes;
        [JsonProperty("category")]           public string Category;

        /// <summary>
        /// Optional markers identifying a NON-UPM (e.g. .unitypackage) copy of this SDK in the
        /// project: substrings matched (case-insensitive) against asmdef name/rootNamespace and
        /// precompiled DLL file names found under <c>Assets/</c>. Used to detect a manual install
        /// so the hub doesn't offer Install (which would duplicate it). Match assembly/namespace,
        /// not folder names, so a leftover settings folder is not a false positive.
        /// Absent → no out-of-UPM detection for this external.
        /// </summary>
        [JsonProperty("assetMarkers")]       public List<string> AssetMarkers;

        /// <summary>
        /// Optional manifest dependency ids that are a LEGACY form of this SDK — a different package
        /// that already provides it (e.g. the bundled git package <c>com.psv.tenjin</c> for Tenjin).
        /// When manifest.json contains one of these (and not the canonical <see cref="Id"/>), the SDK
        /// is already working, so the scanner reports <see cref="Scanner.ExternalState.InstalledLegacy"/>
        /// and the hub offers no Install/Migrate (installing the canonical id would duplicate the SDK;
        /// the legacy wrapper's namespace may also differ). Manifest is authoritative, so this also
        /// avoids reflection false-positives. Absent → no legacy recognition.
        /// </summary>
        [JsonProperty("legacyManifestIds")]  public List<string> LegacyManifestIds;

        /// <summary>
        /// Optional git-install chain for this component. When the Git method is chosen, the
        /// installer writes one git-URL dependency per entry here (top-level + transitive),
        /// with no scoped registry. Absent → git method falls back to UPM for this component.
        /// </summary>
        [JsonProperty("git")]                public GitInstall Git;

        /// <summary>
        /// Optional sub-modules that share this SDK's on-disk footprint (e.g. Firebase ships
        /// Analytics / RemoteConfig / Installations under one <c>Assets/Firebase</c> folder).
        /// When migrating, the installer installs the UPM id of EVERY module whose markers are
        /// detected on disk — not just <see cref="Id"/> — so a multi-module SDK isn't reduced to
        /// one module (which would leave the others deleted-but-not-reinstalled). Absent → the
        /// external behaves as a single package keyed by <see cref="Id"/> (unchanged).
        /// </summary>
        [JsonProperty("modules")]            public List<ExternalModule> Modules;

        /// <summary>
        /// Assets-relative folders WHOLLY OWNED by this SDK that migration deletes when moving it to UPM
        /// (its install folder + satellites like EDM / PlayServicesResolver / its own
        /// <c>Editor Default Resources/&lt;sdk&gt;</c> subfolder). Only the ones that actually exist are
        /// deleted, each through the git/path-safety guard. NEVER list a shared folder
        /// (<c>Assets/Plugins</c>, <c>Resources</c>, …) — those are surfaced as a manual-cleanup warning.
        /// This is the ONLY source of migration delete targets — there is no file-walk. Absent → the
        /// migrator reports "couldn't locate, remove manually" (safe no-op).
        /// </summary>
        [JsonProperty("assetRoots")]         public List<string> AssetRoots;

        /// <summary>
        /// Optional static string member exposing the SDK's own version at runtime, used to detect a
        /// DOWNGRADE before migrating a manual (Assets) install to the catalog-pinned UPM version.
        /// Read by reflection from the LOADED manual copy as <c>VersionType.VersionField</c> (a static
        /// field / property / const string, e.g. Firebase: <c>"Firebase.VersionInfo"</c> + <c>"SdkVersion"</c>).
        /// When the on-disk version is newer than the version we'd install, the migrate dialog warns.
        /// Absent / type or member not found → no downgrade check (migration proceeds as before).
        /// </summary>
        [JsonProperty("versionType")]        public string VersionType;
        [JsonProperty("versionField")]       public string VersionField;

        /// <summary>
        /// Recommended version string used by the migrator when generating
        /// <c>AddPackage</c> actions for this external. Optional — when absent,
        /// <see cref="MinVersion"/> is used; when both are absent the planner emits
        /// a <c>PlannerWarning</c> instead of an <c>AddPackage</c> action.
        /// </summary>
        [JsonProperty("recommendedVersion")] public string RecommendedVersion;

        /// <summary>Minimum acceptable version. Fallback for the migrator when
        /// <see cref="RecommendedVersion"/> is absent.</summary>
        [JsonProperty("minVersion")]         public string MinVersion;

        /// <summary>Optional post-install configuration requirements (per platform), used to
        /// render the Setup readiness checklist. Absent → no config to verify.</summary>
        [JsonProperty("config")]             public List<ConfigRequirement> Config;
    }

    /// <summary>
    /// One declarative post-install configuration requirement for a component, evaluated by the
    /// installer's Setup checker. Data-driven: the catalog (metadata package) declares WHAT each
    /// component needs; the installer ships the generic handlers for each <see cref="Kind"/>.
    /// </summary>
    public sealed class ConfigRequirement
    {
        /// <summary>"Android" | "iOS" | null (any/both) — which column the requirement belongs to.</summary>
        [JsonProperty("platform")] public string Platform;

        /// <summary>Requirement kind the installer knows how to check:
        /// "assetFile" (a file must exist in Assets) | "settingsAssetField" (a ScriptableObject
        /// field must be set).</summary>
        [JsonProperty("kind")] public string Kind;

        /// <summary>Short human label for the row/cell.</summary>
        [JsonProperty("label")] public string Label;

        // ── assetFile ──
        /// <summary>File name to locate anywhere under Assets/ (e.g. "google-services.json").</summary>
        [JsonProperty("fileName")] public string FileName;

        /// <summary>Optional help URL shown when the file is missing (e.g. Firebase console).</summary>
        [JsonProperty("help")] public string Help;

        // ── settingsAssetField ──
        /// <summary>ScriptableObject type name to locate via AssetDatabase (e.g. "TenjinSettings").</summary>
        [JsonProperty("assetType")] public string AssetType;

        /// <summary>Explicit asset path (alternative to <see cref="AssetType"/>),
        /// e.g. "Assets/CleverAdsSolutions/Resources/CASSettingsAndroid.asset".</summary>
        [JsonProperty("assetPath")] public string AssetPath;

        /// <summary>Serialized field name that must be non-empty (e.g. "androidKey", "managerIds").</summary>
        [JsonProperty("field")] public string Field;

        /// <summary>A value that counts as "not configured" (e.g. the CAS placeholder "demo").</summary>
        [JsonProperty("placeholder")] public string Placeholder;

        /// <summary>Optional Unity menu path to open the relevant settings window (e.g. CAS).</summary>
        [JsonProperty("openMenu")] public string OpenMenu;
    }

    /// <summary>
    /// Describes a legacy package that should be removed from the client project
    /// without any replacement. The catalog entry lists the old npm ids that may
    /// still appear in manifest.json; the migrator removes them.
    /// </summary>
    public sealed class UninstallRecord
    {
        /// <summary>
        /// One or more legacy npm package ids that should be removed from
        /// <c>manifest.json</c> (e.g. <c>["com.psv.unity.edm"]</c>).
        /// </summary>
        [JsonProperty("legacyNpmIds")]
        public List<string> LegacyNpmIds;

        /// <summary>
        /// Human-readable explanation of why the package is being uninstalled
        /// (e.g. "arrives transitively via firebase — no separate entry needed").
        /// Informational only; not used by the planner.
        /// </summary>
        [JsonProperty("reason")]
        public string Reason;
    }

    /// <summary>
    /// One sub-module of a multi-module external (see <see cref="ExternalRecord.Modules"/>).
    /// Detected independently by its own <see cref="AssetMarkers"/>; installed at
    /// <see cref="RecommendedVersion"/> when present, otherwise the parent record's version.
    /// </summary>
    public sealed class ExternalModule
    {
        [JsonProperty("id")]                 public string Id;
        [JsonProperty("assetMarkers")]       public List<string> AssetMarkers;

        /// <summary>Optional per-module version override; null → inherit the parent external's version.</summary>
        [JsonProperty("recommendedVersion")] public string RecommendedVersion;
    }

    /// <summary>The flat set of packages to write as git-URL dependencies for one component.</summary>
    public sealed class GitInstall
    {
        [JsonProperty("packages")] public List<GitPackage> Packages;
    }

    /// <summary>One git-URL dependency: id plus repo URL plus tag.</summary>
    public sealed class GitPackage
    {
        [JsonProperty("id")]  public string Id;
        [JsonProperty("url")] public string Url;
        [JsonProperty("tag")] public string Tag;

        /// <summary>The manifest dependency value: <c>url#tag</c>.</summary>
        public string Spec => string.IsNullOrEmpty(Tag) ? Url : $"{Url}#{Tag}";
    }
}
