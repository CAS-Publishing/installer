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

        public SetupScreen()
        {
            Root = new VisualElement();
            WizardAssets.CloneInto("Setup", Root);
            _rowsHost = Root.Q<VisualElement>("setup-rows");
            _summary  = Root.Q<Label>("setup-summary");
        }

        public void OnEnter(WizardRouter router)
        {
            _router = router;
            if (!_bound)
            {
                _bound = true;
                var refresh = Root.Q<Button>("setup-refresh");
                if (refresh != null) refresh.clicked += Rebuild;
                var back = Root.Q<Button>("setup-back");
                if (back != null) back.clicked += () => _router.GoTo("components");
            }

            Rebuild();
        }

        private void Rebuild()
        {
            if (_rowsHost == null) return;
            _rowsHost.Clear();

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
                _rowsHost.Add(BuildRow(row, alt: installed % 2 == 1));
                installed++;
                attention += CountAttention(row.Android) + CountAttention(row.IOS);
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

        private static VisualElement BuildRow(SetupModel.Row row, bool alt)
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

            el.Add(comp);
            el.Add(BuildPlatformColumn(row.Android));
            el.Add(BuildPlatformColumn(row.IOS));
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

            // Each cell opens THIS platform's settings/help (CAS has separate Android/iOS assets).
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
    }
}
