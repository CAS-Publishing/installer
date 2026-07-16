using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// "Ready to install" — step 1 of the 3-step install flow. Lists the default component set
    /// with live install state (target version to install, or an "already installed" confirmation)
    /// and drives either the batch install (<see cref="AutoInstaller.StartAll"/>) or, once every
    /// default component is already present, hands off straight to the configure step.
    /// </summary>
    internal sealed class ReadyScreen : IWizardScreen
    {
        public string Id => "ready";
        public VisualElement Root { get; }

        // Sub-copy overrides for rows whose default catalog blurb doesn't fit this screen's framing
        // (both SDKs are wired automatically here — nothing for the developer to configure).
        private const string TenjinId = "com.tenjin.sdk";
        private const string TenjinSubOverride = "Attribution — handled on our end, nothing for you to configure";
        private const string FirebaseId = "com.google.firebase.analytics";
        private const string FirebaseSubOverride = "Analytics — events wired automatically via CAS SDK";

        private bool _bound;
        private readonly VisualElement _list;
        private readonly Button _advanced;
        private readonly Button _primary;
        private ReadyModel _model;

        public ReadyScreen()
        {
            Root = new VisualElement();
            WizardAssets.CloneInto("Ready", Root);
            _list = Root.Q<VisualElement>("ready-list");
            _advanced = Root.Q<Button>("btn-advanced");
            _primary = Root.Q<Button>("btn-primary");
        }

        public void OnEnter(WizardRouter router)
        {
            if (!_bound)
            {
                _bound = true;

                if (_advanced != null)
                    _advanced.clicked += () =>
                    {
                        InstallerWizardWindow.IntroDone = true;
                        router.GoTo("components");
                    };

                if (_primary != null)
                    _primary.clicked += () =>
                    {
                        if (_model != null && _model.AllInstalled)
                            router.GoTo("configure");
                        else
                            AutoInstaller.StartAll(router); // drives install + navigates to progress itself
                    };
            }

            // Live re-scan every time the screen is shown so the status reflects the current
            // project state (mirrors ComponentsScreen).
            Rebuild();
        }

        private void Rebuild()
        {
            if (_list == null) return;
            _list.Clear();

            if (!ComponentStatusProvider.TryGetStatuses(out var statuses, out var error))
            {
                ShowError(error);
                if (_primary != null) _primary.text = "Install";
                return;
            }

            _model = ReadyModel.Build(statuses);

            // Overlay presentation-only Sub text for Tenjin/Firebase rows. Mutating the freshly-built
            // ReadyRow list (not the ComponentStatus objects, which TryGetStatuses documents as a
            // shared, read-only, session-cached list) so this never leaks into other screens.
            for (var i = 0; i < statuses.Count && i < _model.Rows.Count; i++)
            {
                var id = statuses[i].Id;
                if (id == TenjinId) _model.Rows[i].Sub = TenjinSubOverride;
                else if (id == FirebaseId) _model.Rows[i].Sub = FirebaseSubOverride;
            }

            var idx = 0;
            foreach (var row in _model.Rows)
            {
                _list.Add(BuildRow(row, alt: idx % 2 == 1));
                idx++;
            }

            if (_primary != null) _primary.text = _model.PrimaryButtonText;
        }

        private static VisualElement BuildRow(ReadyRow r, bool alt)
        {
            var row = new VisualElement();
            row.AddToClassList("cas-row-grid");
            if (alt) row.AddToClassList("cas-row-grid--alt");

            var comp = new VisualElement();
            comp.AddToClassList("cas-col-comp");

            var txt = new VisualElement();
            var nameLabel = new Label(r.Name);
            nameLabel.AddToClassList("cas-comp__name");
            var subLabel = new Label(r.Sub);
            subLabel.AddToClassList("cas-comp__sub");
            txt.Add(nameLabel);
            txt.Add(subLabel);
            comp.Add(txt);

            var right = new Label(r.RightText);
            right.AddToClassList("cas-status");
            right.AddToClassList(r.AlreadyInstalled ? "cas-status--green" : "cas-status--grey");
            right.style.flexGrow = 1f;
            right.style.unityTextAlign = TextAnchor.MiddleRight;

            row.Add(comp);
            row.Add(right);
            return row;
        }

        private void ShowError(string error)
        {
            var note = new Label(error);
            note.AddToClassList("cas-empty-note");
            _list.Add(note);

            var retry = new Button(OnRetry) { text = "Retry" };
            retry.AddToClassList("cas-btn");
            retry.AddToClassList("cas-btn--soft");
            retry.AddToClassList("cas-btn--sm");
            retry.style.marginLeft = 12;
            retry.style.marginTop = 4;
            _list.Add(retry);
        }

        // Retry = also pull/update metadata from the registry (install if missing, check for a
        // newer catalog), then re-scan the local project state and re-render. Mirrors
        // ComponentsScreen.OnRefresh.
        private void OnRetry()
        {
            PSV.Installer.Catalog.CatalogLoader.InvalidateCache();
            ComponentStatusProvider.InvalidateCache();
            PSV.Installer.Bootstrap.EnsureMetadata();
            Rebuild();
        }
    }
}
