using UnityEngine.UIElements;

namespace PSV.Installer.Wizard
{
    internal sealed class ComponentsScreen : IWizardScreen
    {
        public string Id => "components";
        public VisualElement Root { get; }

        private bool _bound;
        private readonly VisualElement _rowsHost;

        public ComponentsScreen()
        {
            Root = new VisualElement();
            WizardAssets.CloneInto("Components", Root);
            _rowsHost = Root.Q<VisualElement>("components-rows");
        }

        public void OnEnter(WizardRouter router)
        {
            if (!_bound)
            {
                _bound = true;
                var refresh = Root.Q<Button>("components-refresh");
                if (refresh != null) refresh.clicked += OnRefresh;
            }

            // Live re-scan every time the screen is shown so the status reflects the
            // current project state.
            Rebuild();
        }

        // Refresh = also pull/update metadata from the registry (install if missing, check for a
        // newer catalog), then re-scan the local project state and re-render.
        private void OnRefresh()
        {
            PSV.Installer.Bootstrap.EnsureMetadata();
            Rebuild();
        }

        private void Rebuild()
        {
            if (_rowsHost == null) return;

            // If CAS was installed (here or via the row button), fill its managerIds from any
            // CAS ID captured on the first screen. Idempotent — only fills empty/placeholder slots.
            CasIdApplier.ApplyPending();

            _rowsHost.Clear();

            if (!ComponentStatusProvider.TryGetStatuses(out var statuses, out var error))
            {
                var note = new Label(error);
                note.AddToClassList("cas-empty-note");
                _rowsHost.Add(note);
                return;
            }

            var i = 0;
            foreach (var st in statuses)
            {
                _rowsHost.Add(BuildRow(st, alt: i % 2 == 1));
                i++;
            }
        }

        private VisualElement BuildRow(ComponentStatus c, bool alt)
        {
            var row = new VisualElement();
            row.AddToClassList("cas-row-grid");
            if (alt) row.AddToClassList("cas-row-grid--alt");

            // Component column: status dot + logo + name/sub.
            var comp = new VisualElement();
            comp.AddToClassList("cas-col-comp");

            var dot = new VisualElement();
            dot.AddToClassList("cas-dot");
            dot.AddToClassList("cas-dot--sm");
            dot.AddToClassList("cas-dot--" + c.Tone);

            var logo = new VisualElement();
            logo.AddToClassList("cas-comp__logo");
            logo.AddToClassList("cas-logo");
            logo.AddToClassList("cas-logo--" + c.Logo);

            var txt = new VisualElement();
            var nameLabel = new Label(c.DisplayName);
            nameLabel.AddToClassList("cas-comp__name");
            var subLabel = new Label(c.Sub);
            subLabel.AddToClassList("cas-comp__sub");
            txt.Add(nameLabel);
            txt.Add(subLabel);

            comp.Add(dot);
            comp.Add(logo);
            comp.Add(txt);

            // Status column — real state, with detected version when present.
            var statusCol = new VisualElement();
            statusCol.AddToClassList("cas-col-status");
            var ver = FriendlyVersion(c.Version);
            var statusText = string.IsNullOrEmpty(ver) ? c.StatusText : $"{c.StatusText} · {ver}";
            var statusLabel = new Label(statusText);
            statusLabel.AddToClassList("cas-status");
            statusLabel.AddToClassList("cas-status--" + c.Tone);
            statusCol.Add(statusLabel);

            // Action column — real Install/Update via the migrator (behind a confirm dialog);
            // disabled when there's nothing to do.
            var actionCol = new VisualElement();
            actionCol.AddToClassList("cas-col-action");
            var btn = new Button(() =>
            {
                if (WizardActions.Apply(c.Id, c.DisplayName))
                    Rebuild();
            })
            { text = c.ActionText };
            btn.AddToClassList("cas-btn");
            btn.AddToClassList("cas-btn--sm");
            btn.AddToClassList(ActionVariantClass(c.ActionVariant));
            btn.SetEnabled(c.Actionable);
            actionCol.Add(btn);

            row.Add(comp);
            row.Add(statusCol);
            row.Add(actionCol);
            return row;
        }

        // Embedded/git dependencies have a long, non-semver spec (e.g. "file:...") — show
        // a short "local" label instead so the status cell stays readable.
        private static string FriendlyVersion(string v)
        {
            if (string.IsNullOrEmpty(v)) return null;
            if (v.StartsWith("file:") || v.StartsWith("git") || v.Contains("://")) return "local";
            return v;
        }

        private static string ActionVariantClass(string actVar)
        {
            switch (actVar)
            {
                case "primary": return "cas-btn--primary";
                case "warn":    return "cas-btn--warn";
                default:        return "cas-btn--muted";
            }
        }
    }
}
