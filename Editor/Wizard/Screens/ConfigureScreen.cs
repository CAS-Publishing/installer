using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// Configuration — a read-only, per-platform (Android / iOS) detection table for the INSTALLED
    /// components. Each cell shows whether the required config is present; nothing here writes to
    /// the project — CAS ID, ad formats, mediation networks, and the Tenjin key now live entirely in
    /// CAS.AI's own settings window (<see cref="CasNativeSettings"/> opens it). Continue is gated by
    /// <see cref="ConfigureGate"/>'s one-platform-is-enough rule: at least one of Android/iOS must be
    /// both used (some installed component has requirements for it) and fully configured.
    /// </summary>
    internal sealed class ConfigureScreen : IWizardScreen
    {
        public string Id => "configure";
        public VisualElement Root { get; }

        private const string CasId = "com.cleversolutions.ads.unity";
        private const string FirebaseId = "com.google.firebase.analytics";
        private const string TenjinId = "com.tenjin.sdk";

        private WizardRouter _router;
        private bool _bound;
        private readonly VisualElement _rowsHost;
        private readonly VisualElement _panelsHost;
        private readonly VisualElement _gateNote;
        private readonly Label _title;
        private readonly Label _subtitle;
        private readonly Button _btnContinue;

        public ConfigureScreen()
        {
            Root = new VisualElement();
            WizardAssets.CloneInto("Configure", Root);
            _rowsHost    = Root.Q<VisualElement>("config-table");
            _panelsHost  = Root.Q<VisualElement>("action-panels");
            _gateNote    = Root.Q<VisualElement>("gate-note");
            _title       = Root.Q<Label>("configure-title");
            _subtitle    = Root.Q<Label>("configure-subtitle");
            _btnContinue = Root.Q<Button>("btn-continue");
        }

        public void OnEnter(WizardRouter router)
        {
            _router = router;
            if (!_bound)
            {
                _bound = true;
                var refresh = Root.Q<Button>("btn-refresh");
                if (refresh != null) refresh.clicked += RefreshFromDisk;
                var back = Root.Q<Button>("btn-back");
                if (back != null) back.clicked += () => _router.Back();
                if (_btnContinue != null) _btnContinue.clicked += () => _router.GoTo("done");
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
            _panelsHost?.Clear();

            var platform = PlatformDetect.ActivePlatform();

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
                SetHeader(false, string.Empty);
                SetContinueEnabled(false);
                if (_gateNote != null) _gateNote.style.display = DisplayStyle.None;
                return;
            }

            var installed = 0;
            var idx = 0;
            // Rows render generically from the catalog's config requirements, except Tenjin (no
            // catalog config declared for it): its cells come from TenjinKeyDetect.Probe instead —
            // see BuildRow/BuildTenjinColumn below.
            foreach (var row in rows)
            {
                if (!row.Installed) continue; // Configuration lists only installed components
                _rowsHost.Add(BuildRow(row, alt: idx % 2 == 1));
                idx++;
                installed++;
            }

            if (installed == 0)
            {
                AddNote("No components installed yet — install them on the Components tab first.");
                SetHeader(false, string.Empty);
                SetContinueEnabled(false);
                if (_gateNote != null) _gateNote.style.display = DisplayStyle.None;
                return;
            }

            var readiness = BuildReadiness(rows);
            var gateOpen = ConfigureGate.CanContinue(readiness);

            RenderActionPanels(rows, platform);
            UpdateHeader(readiness, gateOpen);
            SetContinueEnabled(gateOpen);
            if (_gateNote != null) _gateNote.style.display = gateOpen ? DisplayStyle.None : DisplayStyle.Flex;
        }

        private void AddNote(string text)
        {
            var note = new Label(text);
            note.AddToClassList("cas-empty-note");
            _rowsHost.Add(note);
        }

        private void SetHeader(bool complete, string subtitle)
        {
            if (_title != null) _title.text = complete ? "Configuration complete" : "Configuration";
            if (_subtitle != null) _subtitle.text = subtitle;
        }

        private void SetContinueEnabled(bool enabled)
        {
            _btnContinue?.SetEnabled(enabled);
        }

        // Header/subtitle copy per mockups #8/#9: gate closed → generic "resolve items" prompt; gate
        // open + BOTH platforms fully ready → the "all done" copy; gate open via the one-platform
        // rule (the other platform unused, or used but not fully configured) → the "one platform is
        // enough" copy.
        private void UpdateHeader(List<PlatformReadiness> readiness, bool gateOpen)
        {
            if (!gateOpen)
            {
                SetHeader(false,
                    "Review the plugin configuration status below. Complete the required items for " +
                    "at least one platform to continue.");
                return;
            }

            var bothComplete = true;
            foreach (var p in readiness)
                if (!(p.Used && p.AllOk)) bothComplete = false;

            SetHeader(true, bothComplete
                ? "Plugin setup is complete and all required items are ready. You can continue to the final check."
                : "One platform is configured and ready. Many projects ship on only Android or only iOS, so this is enough to continue.");
        }

        // Builds the two PlatformReadiness entries ConfigureGate needs, from the SAME rows the table
        // just rendered: Used = some installed component declares requirements for that platform;
        // AllOk = every one of those active requirements is Configured. NotApplicable is informational
        // only (nothing the checker knows how to verify) — it does NOT block AllOk, matching the old
        // SetupScreen.CountAttention semantics. Tenjin has no catalog config (its cells come from
        // TenjinKeyDetect instead of SetupModel.Cell), so it's folded in separately below.
        private static List<PlatformReadiness> BuildReadiness(List<SetupModel.Row> rows)
        {
            bool androidUsed = false, androidOk = true;
            bool iosUsed = false, iosOk = true;

            foreach (var row in rows)
            {
                if (!row.Installed) continue;

                if (row.Android.Count > 0)
                {
                    androidUsed = true;
                    foreach (var c in row.Android)
                        if (!IsOkOrNotApplicable(c.Result)) androidOk = false;
                }
                if (row.IOS.Count > 0)
                {
                    iosUsed = true;
                    foreach (var c in row.IOS)
                        if (!IsOkOrNotApplicable(c.Result)) iosOk = false;
                }

                if (row.Id == TenjinId)
                {
                    ApplyTenjinReadiness("Android", ref androidUsed, ref androidOk);
                    ApplyTenjinReadiness("iOS", ref iosUsed, ref iosOk);
                }
            }

            return new List<PlatformReadiness>
            {
                new PlatformReadiness { Platform = "Android", Used = androidUsed, AllOk = androidOk },
                new PlatformReadiness { Platform = "iOS",     Used = iosUsed,     AllOk = iosOk },
            };
        }

        private static bool IsOkOrNotApplicable(ReqResult result) =>
            result != null && (result.Status == ReqStatus.Configured || result.Status == ReqStatus.NotApplicable);

        // Tenjin key feature-detect: FieldSupported=false (older CAS versions without the field) is
        // purely informational and never touches Used/AllOk. FieldSupported=true makes the platform
        // "used", and an empty key blocks AllOk for that platform (surfaced via the Tenjin action panel).
        private static void ApplyTenjinReadiness(string platform, ref bool used, ref bool ok)
        {
            var probe = TenjinKeyDetect.Probe(platform);
            if (!probe.FieldSupported) return;
            used = true;
            if (string.IsNullOrEmpty(probe.Key)) ok = false;
        }

        // ── Action panels (CAS / Firebase) ────────────────────────────────────

        private void RenderActionPanels(List<SetupModel.Row> rows, string platform)
        {
            if (_panelsHost == null) return;

            var casRow = FindRow(rows, CasId);
            if (casRow != null && RowNeedsAttention(casRow))
                _panelsHost.Add(BuildCasPanel());

            var tenjinRow = FindRow(rows, TenjinId);
            if (tenjinRow != null && tenjinRow.Installed)
            {
                var probe = TenjinKeyDetect.Probe(platform);
                if (probe.FieldSupported && string.IsNullOrEmpty(probe.Key))
                    _panelsHost.Add(BuildTenjinPanel(platform));
            }

            var firebaseRow = FindRow(rows, FirebaseId);
            if (firebaseRow != null && RowNeedsAttention(firebaseRow))
                _panelsHost.Add(BuildFirebasePanel());
        }

        private static SetupModel.Row FindRow(List<SetupModel.Row> rows, string id)
        {
            foreach (var r in rows)
                if (r.Id == id) return r;
            return null;
        }

        // True when the (installed) row has at least one active requirement, on either platform, that
        // isn't Configured yet. Uses the same IsOkOrNotApplicable rule as BuildReadiness/AllOk — a
        // NotApplicable cell is informational only, so it must not trigger a warning panel while the
        // gate/header already treat it as fine (otherwise the two would disagree).
        private static bool RowNeedsAttention(SetupModel.Row row)
        {
            if (!row.Installed) return false;
            foreach (var c in row.Android)
                if (!IsOkOrNotApplicable(c.Result)) return true;
            foreach (var c in row.IOS)
                if (!IsOkOrNotApplicable(c.Result)) return true;
            return false;
        }

        private static VisualElement BuildCasPanel()
        {
            var card = ActionCard(
                "⚠ CAS plugin configuration required",
                "CAS ID, ad formats, mediation networks, and the Tenjin key are configured in the plugin settings.");

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;

            var android = ActionButton("Android settings", () => CasNativeSettings.Open("Android"));
            android.style.marginRight = 8;
            buttons.Add(android);
            buttons.Add(ActionButton("iOS settings", () => CasNativeSettings.Open("iOS")));

            card.Add(buttons);
            return card;
        }

        private static VisualElement BuildTenjinPanel(string platform)
        {
            var card = ActionCard(
                "⚠ Tenjin SDK Key required",
                "The Tenjin SDK Key enables attribution and automatic AttributionInfo reporting. Add it in CAS Settings to continue.");

            card.Add(ActionButton("Open CAS Settings", () => CasNativeSettings.Open(platform)));
            return card;
        }

        private VisualElement BuildFirebasePanel()
        {
            var card = ActionCard(
                "⚠ Firebase Analytics — configuration file missing",
                "google-services.json is required for Android. Download it from Firebase Console → Project Settings → Your apps.");

            var buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;

            var openConsole = ActionButton("Open Firebase Console",
                () => Application.OpenURL("https://console.firebase.google.com/"));
            openConsole.style.marginRight = 8;
            buttons.Add(openConsole);

            var locate = ActionButton("Locate file…", LocateFirebaseConfigFile);
            buttons.Add(locate);

            card.Add(buttons);
            return card;
        }

        // "Locate file…" — user picks google-services.json / GoogleService-Info.plist from anywhere on
        // disk; validated by name and copied into Assets/. Errors surface in a dialog; success re-runs
        // the same refresh path as the Refresh button so the table/panels reflect the new file.
        private void LocateFirebaseConfigFile()
        {
            var path = EditorUtility.OpenFilePanel("Select google-services.json", "", "");
            if (string.IsNullOrEmpty(path)) return; // user cancelled the panel

            var err = FirebaseConfigFile.ValidateAndCopy(path, "Assets");
            if (err != null)
            {
                EditorUtility.DisplayDialog("CAS.AI Publishing Hub", err, "OK");
                return;
            }

            RefreshFromDisk();
        }

        private static VisualElement ActionCard(string title, string body)
        {
            var card = new VisualElement();
            card.AddToClassList("cas-card");
            card.AddToClassList("cas-card--warn");
            card.style.flexDirection = FlexDirection.Column;
            card.style.alignItems = Align.FlexStart;
            card.style.marginTop = 10;

            var titleLabel = new Label(title);
            titleLabel.AddToClassList("cas-card__title");
            card.Add(titleLabel);

            var bodyLabel = new Label(body);
            bodyLabel.AddToClassList("cas-card__desc");
            bodyLabel.style.marginBottom = 10;
            card.Add(bodyLabel);

            return card;
        }

        private static Button ActionButton(string text, System.Action onClick)
        {
            var btn = onClick != null ? new Button(onClick) { text = text } : new Button { text = text };
            btn.AddToClassList("cas-btn");
            btn.AddToClassList("cas-btn--soft");
            btn.AddToClassList("cas-btn--sm");
            return btn;
        }

        // ── Table rows ─────────────────────────────────────────────────────────

        private static VisualElement BuildRow(SetupModel.Row row, bool alt)
        {
            var el = new VisualElement();
            el.AddToClassList("cas-setup-row");
            if (alt) el.AddToClassList("cas-setup-row--alt");

            var comp = new VisualElement();
            comp.AddToClassList("cas-setup-col-comp");
            if (!string.IsNullOrEmpty(row.Logo))
            {
                var logo = new VisualElement();
                logo.AddToClassList("cas-comp__logo");
                logo.AddToClassList("cas-logo");
                logo.AddToClassList("cas-logo--" + row.Logo);
                comp.Add(logo);
            }
            var name = new Label(row.Name);
            name.AddToClassList("cas-comp__name");
            comp.Add(name);

            var grid = new VisualElement();
            grid.AddToClassList("cas-setup-row__grid");
            grid.Add(comp);
            if (row.Id == TenjinId)
            {
                grid.Add(BuildTenjinColumn("Android"));
                grid.Add(BuildTenjinColumn("iOS"));
            }
            else
            {
                grid.Add(BuildPlatformColumn(row.Android));
                grid.Add(BuildPlatformColumn(row.IOS));
            }
            el.Add(grid);
            return el;
        }

        // Tenjin has no catalog config requirement (older CAS versions don't even have the field), so
        // its column is feature-detected directly instead of going through SetupModel.Cell/BuildCell:
        // FieldSupported && key set → green "configured"; FieldSupported && empty → yellow "not
        // configured" (an active, blocking requirement — see ApplyTenjinReadiness/BuildTenjinPanel);
        // !FieldSupported (older CAS) → grey informational text, never a warning.
        private static VisualElement BuildTenjinColumn(string platform)
        {
            var col = new VisualElement();
            col.AddToClassList("cas-setup-col-plat");

            var probe = TenjinKeyDetect.Probe(platform);
            if (!probe.FieldSupported)
            {
                var handled = new Label("Handled on our end");
                handled.AddToClassList("cas-muted-cell");
                col.Add(handled);
                return col;
            }

            var line = new VisualElement();
            line.AddToClassList("cas-setup-cell");

            var ok = !string.IsNullOrEmpty(probe.Key);
            var tone = ok ? "green" : "yellow";

            var dot = new VisualElement();
            dot.AddToClassList("cas-dot");
            dot.AddToClassList("cas-dot--sm");
            dot.AddToClassList("cas-dot--" + tone);
            line.Add(dot);

            var txt = new Label(ok ? "✓ Key configured" : "⚠ Key not configured");
            txt.AddToClassList("cas-setup-cell__txt");
            txt.AddToClassList("cas-status--" + tone);
            line.Add(txt);

            col.Add(line);
            return col;
        }

        private static VisualElement BuildPlatformColumn(List<SetupModel.Cell> cells)
        {
            var col = new VisualElement();
            col.AddToClassList("cas-setup-col-plat");

            if (cells == null || cells.Count == 0)
            {
                var notUsed = new Label("Not used");
                notUsed.AddToClassList("cas-muted-cell");
                col.Add(notUsed);
                return col;
            }

            foreach (var cell in cells)
                col.Add(BuildCell(cell));
            return col;
        }

        // Read-only: green "✓ <label>" when Configured, otherwise a yellow "⚠ <label>" warning —
        // Missing/NotConfigured/NotApplicable all need the same action (finish it in the plugin's
        // own settings), so the table doesn't distinguish them further.
        private static VisualElement BuildCell(SetupModel.Cell cell)
        {
            var line = new VisualElement();
            line.AddToClassList("cas-setup-cell");

            var ok = cell.Result != null && cell.Result.Status == ReqStatus.Configured;
            var tone = ok ? "green" : "yellow";

            var dot = new VisualElement();
            dot.AddToClassList("cas-dot");
            dot.AddToClassList("cas-dot--sm");
            dot.AddToClassList("cas-dot--" + tone);
            line.Add(dot);

            var label = string.IsNullOrEmpty(cell.Label) ? "—" : cell.Label;
            var txt = new Label((ok ? "✓ " : "⚠ ") + label);
            txt.AddToClassList("cas-setup-cell__txt");
            txt.AddToClassList("cas-status--" + tone);
            line.Add(txt);

            return line;
        }
    }
}
