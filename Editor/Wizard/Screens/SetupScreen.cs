using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// Configuration — a per-platform (Android / iOS) readiness checklist for the INSTALLED
    /// components. Each cell shows whether the required config is present, what's there, and any
    /// error, and is CLICKABLE: it opens that platform's settings asset (e.g. CAS has separate
    /// Android/iOS assets) or the help page. Read-only checks; not-installed components are hidden.
    /// </summary>
    internal sealed class SetupScreen : IWizardScreen
    {
        public string Id => "setup";
        public VisualElement Root { get; }

        private WizardRouter _router;
        private bool _bound;
        private readonly VisualElement _rowsHost;
        private readonly Label _summary;
        private readonly Label _thPlat;

        public SetupScreen()
        {
            Root = new VisualElement();
            WizardAssets.CloneInto("Setup", Root);
            _rowsHost = Root.Q<VisualElement>("setup-rows");
            _summary  = Root.Q<Label>("setup-summary");
            _thPlat   = Root.Q<Label>("setup-th-plat");
        }

        public void OnEnter(WizardRouter router)
        {
            _router = router;
            if (!_bound)
            {
                _bound = true;
                var refresh = Root.Q<Button>("setup-refresh");
                if (refresh != null) refresh.clicked += RefreshFromDisk;
                var back = Root.Q<Button>("setup-back");
                if (back != null) back.clicked += () => _router.GoTo("components");
            }

            Rebuild();
        }

        // The Refresh button forces a re-read: drop the session caches so the scan + catalog are
        // re-evaluated from disk (otherwise Refresh would just re-render the already-cached state).
        private void RefreshFromDisk()
        {
            PSV.Installer.Catalog.CatalogLoader.InvalidateCache();
            ComponentStatusProvider.InvalidateCache();
            Rebuild();
        }

        private void Rebuild()
        {
            if (_rowsHost == null) return;
            _rowsHost.Clear();

            var platform = PlatformDetect.ActivePlatform();
            if (_thPlat != null) _thPlat.text = platform;

            // Android build readiness: missing gradle/manifest templates make a fresh project fail to
            // build with a non-obvious cause — surface it with a one-click Enable. Only relevant when
            // Android is the active target: on an iOS project the banner (and its Enable button, which
            // would write Android templates) is noise/wrong, so it follows the screen's platform scope.
            if (platform == "Android" && AndroidBuildFix.MissingNow() is var missingAndroid && missingAndroid.Count > 0)
            {
                var banner = new VisualElement();
                banner.AddToClassList("cas-androidbanner");
                var msg = new Label($"⚠ {missingAndroid.Count} Android build template(s) missing — " +
                                    "the project may not build until they're enabled.");
                msg.AddToClassList("cas-androidbanner__msg");
                banner.Add(msg);
                var fix = new Button(() => { AndroidBuildFix.Ensure(); Rebuild(); })
                    { text = "Enable Android build settings" };
                fix.AddToClassList("cas-btn");
                fix.AddToClassList("cas-btn--primary");
                banner.Add(fix);
                _rowsHost.Add(banner);
            }

            if (!SetupModel.TryBuild(out var rows, out var error))
            {
                AddNote(error);
                if (_summary != null) _summary.text = string.Empty;
                return;
            }

            var attention = 0;
            var installed = 0;
            foreach (var row in rows)
            {
                if (!row.Installed) continue; // Configuration lists only installed components
                _rowsHost.Add(BuildRow(row, alt: installed % 2 == 1, platform));
                installed++;
                attention += CountAttention(PickForPlatform(row.Android, row.IOS, platform));
            }

            if (installed == 0)
            {
                AddNote("No components installed yet — install them on the Components tab first.");
                if (_summary != null) _summary.text = string.Empty;
                return;
            }

            UpdateSummary(installed, attention);
        }

        private void AddNote(string text)
        {
            var note = new Label(text);
            note.AddToClassList("cas-empty-note");
            _rowsHost.Add(note);
        }

        private void UpdateSummary(int installed, int attention)
        {
            if (_summary == null) return;
            _summary.RemoveFromClassList("cas-setup-summary--ok");
            _summary.RemoveFromClassList("cas-setup-summary--warn");

            if (attention == 0)
            {
                _summary.text = "✓ All installed components are configured.";
                _summary.AddToClassList("cas-setup-summary--ok");
            }
            else
            {
                _summary.text = $"⚠ {attention} item(s) need attention before building.";
                _summary.AddToClassList("cas-setup-summary--warn");
            }
        }

        private static int CountAttention(List<SetupModel.Cell> cells)
        {
            var n = 0;
            foreach (var c in cells)
                if (c.Result != null &&
                    (c.Result.Status == ReqStatus.Missing || c.Result.Status == ReqStatus.NotConfigured))
                    n++;
            return n;
        }

        private static VisualElement BuildRow(SetupModel.Row row, bool alt, string platform)
        {
            var el = new VisualElement();
            el.AddToClassList("cas-setup-row");
            if (alt) el.AddToClassList("cas-setup-row--alt");

            var comp = new VisualElement();
            comp.AddToClassList("cas-setup-col-comp");

            var head = new VisualElement();
            head.AddToClassList("cas-row");
            if (!string.IsNullOrEmpty(row.Logo))
            {
                var logo = new VisualElement();
                logo.AddToClassList("cas-comp__logo");
                logo.AddToClassList("cas-logo");
                logo.AddToClassList("cas-logo--" + row.Logo);
                head.Add(logo);
            }
            var name = new Label(row.Name);
            name.AddToClassList("cas-comp__name");
            head.Add(name);
            comp.Add(head);

            // Status line: 2-column grid (Component | active platform), read-only.
            var grid = new VisualElement();
            grid.AddToClassList("cas-setup-row__grid");
            grid.Add(comp);
            grid.Add(BuildPlatformColumn(PickForPlatform(row.Android, row.IOS, platform)));
            el.Add(grid);

            // CAS gets a dedicated, labelled config card BELOW the status grid (formats + audience),
            // for the active platform only.
            if (row.AdFormats)
                el.Add(BuildCasConfig(platform));
            return el;
        }

        private static VisualElement BuildPlatformColumn(List<SetupModel.Cell> cells)
        {
            var col = new VisualElement();
            col.AddToClassList("cas-setup-col-plat");

            if (cells == null || cells.Count == 0)
            {
                var dash = new Label("—");
                dash.AddToClassList("cas-muted-cell");
                col.Add(dash);
                return col;
            }

            foreach (var cell in cells)
                col.Add(BuildCell(cell));
            return col;
        }

        private static VisualElement BuildCell(SetupModel.Cell cell)
        {
            var line = new VisualElement();
            line.AddToClassList("cas-setup-cell");

            var tone = ToneFor(cell.Result?.Status ?? ReqStatus.NotApplicable);

            var dot = new VisualElement();
            dot.AddToClassList("cas-dot");
            dot.AddToClassList("cas-dot--sm");
            dot.AddToClassList("cas-dot--" + tone);
            line.Add(dot);

            var txt = new Label(TextFor(cell));
            txt.AddToClassList("cas-setup-cell__txt");
            txt.AddToClassList("cas-status--" + tone);
            line.Add(txt);

            // Read-only: each cell opens THIS platform's settings/help (CAS has separate Android/iOS
            // assets). CAS ID is entered on Welcome, not edited here.
            line.AddToClassList("cas-setup-cell--link");
            line.tooltip = cell.Req != null && cell.Req.Kind == "assetFile"
                ? "Open the file or how to get it"
                : "Open settings";
            line.RegisterCallback<ClickEvent>(_ => DoCellAction(cell));

            return line;
        }

        // Opens the settings for ONE platform cell: the found file, the relevant settings asset,
        // or the help page — whichever applies to this requirement.
        private static void DoCellAction(SetupModel.Cell cell)
        {
            var req = cell.Req;
            if (req == null) return;

            if (req.Kind == "assetFile")
            {
                // If the file is present, ping it; otherwise open the help page on how to get it.
                if (cell.Result != null && cell.Result.Status == ReqStatus.Configured &&
                    !string.IsNullOrEmpty(cell.Result.Value))
                {
                    var file = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(cell.Result.Value);
                    if (file != null) { Selection.activeObject = file; EditorGUIUtility.PingObject(file); return; }
                }
                if (!string.IsNullOrEmpty(req.Help)) { Application.OpenURL(req.Help); return; }
                Debug.Log($"[PSV Installer Wizard] {req.FileName} is not present in the project yet.");
                return;
            }

            // settingsAssetField — ping the specific (per-platform) settings asset.
            var asset = SetupChecker.LocateAsset(req.AssetPath, req.AssetType);
            if (asset != null)
            {
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            }
            else
            {
                Debug.Log("[PSV Installer Wizard] Settings asset for this platform isn't created yet — " +
                          "it appears once the package generates it (configure/build).");
            }
        }

        private static string ToneFor(ReqStatus status)
        {
            switch (status)
            {
                case ReqStatus.Configured:    return "green";
                case ReqStatus.NotConfigured: return "yellow";
                case ReqStatus.Missing:       return "red";
                default:                      return "grey";
            }
        }

        private static string TextFor(SetupModel.Cell cell)
        {
            var label = string.IsNullOrEmpty(cell.Label) ? "" : cell.Label + ": ";
            var r = cell.Result;
            if (r == null) return label + "—";

            switch (r.Status)
            {
                case ReqStatus.Configured:
                    return label + (string.IsNullOrEmpty(r.Value) ? "set ✓" : r.Value);
                case ReqStatus.NotConfigured:
                    return label + (string.IsNullOrEmpty(r.Value) ? "not set" : r.Value + " (not set)");
                case ReqStatus.Missing:
                    return label + (string.IsNullOrEmpty(r.Error) ? "missing" : r.Error);
                default:
                    return label + (string.IsNullOrEmpty(r.Error) ? "—" : r.Error);
            }
        }

        // CAS-only configuration card for the active platform: ad formats as a 2-up grid and the
        // mediation network sets as side-by-side checkboxes, so the card fills the width and reads as
        // two compact groups instead of one tall single-column list. The platform is folded into the
        // title (the status grid above already names it), dropping a redundant header row.
        private static VisualElement BuildCasConfig(string platform)
        {
            var card = new VisualElement();
            card.AddToClassList("cas-cfg");

            var title = new Label("CAS CONFIGURATION · " + platform);
            title.AddToClassList("cas-cfg__title");
            card.Add(title);

            // Ad formats — 2×2 grid (flex-wrap) to use the horizontal space and stay short.
            card.Add(GroupLabel("Ad formats", spaced: false));
            var flags = CasSettingsWriter.ReadAdFlags(platform);
            var grid = new VisualElement();
            grid.AddToClassList("cas-cfg__grid");
            grid.Add(Half(FormatToggle(platform, "Banner", AdFlagsBits.Banner, flags)));
            grid.Add(Half(FormatToggle(platform, "Interstitial", AdFlagsBits.Interstitial, flags)));
            grid.Add(Half(FormatToggle(platform, "Rewarded", AdFlagsBits.Rewarded, flags)));
            grid.Add(Half(FormatToggle(platform, "App Open", AdFlagsBits.AppOpen, flags)));
            card.Add(grid);

            // Mediation network SETS as INDEPENDENT checkboxes side by side, mirroring CAS's own model:
            // OptimalAds and FamiliesAds can each be on/off independently. Each toggle activates/disables
            // that one solution via CasMediation; on a reflection failure the toggle reverts so the UI
            // never claims a state that isn't on disk.
            card.Add(GroupLabel("Mediation networks", spaced: true));
            var bt = platform == "iOS" ? UnityEditor.BuildTarget.iOS : UnityEditor.BuildTarget.Android;
            var nets = new VisualElement();
            nets.AddToClassList("cas-cfg__row");
            nets.Add(Half(SolutionToggle("Optimal", bt, families: false)));
            nets.Add(Half(SolutionToggle("Families", bt, families: true)));
            card.Add(nets);

            var hint = new Label("Optimal = full adult network set · Families = child-directed set");
            hint.AddToClassList("cas-cfg__hint");
            card.Add(hint);
            return card;
        }

        private static Label GroupLabel(string text, bool spaced)
        {
            var l = new Label(text);
            l.AddToClassList("cas-cfg__grouplabel");
            if (spaced) l.AddToClassList("cas-cfg__grouplabel--spaced");
            return l;
        }

        // Marks a toggle as a half-width grid cell (two per row).
        private static Toggle Half(Toggle t) { t.AddToClassList("cas-cfg__toggle--half"); return t; }

        private static Toggle FormatToggle(string platform, string label, int flag, int currentMask)
        {
            var t = new Toggle(label) { value = AdFlagsBits.HasFlag(currentMask, flag) };
            t.AddToClassList("cas-cfg__toggle");
            t.RegisterValueChangedCallback(e =>
                CasSettingsWriter.SetAdFlags(platform, AdFlagsBits.WithFlag(CasSettingsWriter.ReadAdFlags(platform), flag, e.newValue)));
            return t;
        }

        // One CAS mediation solution as an independent checkbox. Initial state is the best-effort
        // installed-read; on a failed apply the value reverts so the box reflects on-disk reality.
        private static Toggle SolutionToggle(string label, UnityEditor.BuildTarget platform, bool families)
        {
            var t = new Toggle(label) { value = CasMediation.IsSolutionInstalled(platform, families) };
            t.AddToClassList("cas-cfg__toggle");
            t.RegisterValueChangedCallback(e =>
            {
                if (!CasMediation.SetSolution(platform, families, e.newValue))
                    t.SetValueWithoutNotify(!e.newValue);
            });
            return t;
        }

        internal static T PickForPlatform<T>(T android, T ios, string platform) =>
            platform == "iOS" ? ios : android;
    }
}
