using UnityEngine;
using UnityEngine.UIElements;

namespace PSV.Installer.Wizard
{
    internal sealed class HubActionsScreen : IWizardScreen
    {
        public string Id => "hub";
        public VisualElement Root { get; }

        private WizardRouter _router;
        private bool _bound;

        public HubActionsScreen()
        {
            Root = new VisualElement();
            WizardAssets.CloneInto("HubActions", Root);

            var version = Root.Q<Label>("hub-version");
            if (version != null) version.text = "v" + WizardAssets.InstallerVersion;
        }

        public void OnEnter(WizardRouter router)
        {
            _router = router;
            if (_bound) return;
            _bound = true;

            BindTile("hub-auto",     () => _router.GoTo("progress"));
            BindTile("hub-settings", () => _router.GoTo("settings"));
            BindTile("hub-reset",    () => Debug.Log("[PSV Installer Wizard] (stub) Reset All Settings"));
            BindTile("hub-tenjin",   () => Debug.Log("[PSV Installer Wizard] (stub) Open Tenjin Dashboard"));

            var docs = Root.Q<Button>("hub-docs");
            if (docs != null) docs.clicked += () => Debug.Log("[PSV Installer Wizard] (stub) Documentation");
            var support = Root.Q<Button>("hub-support");
            if (support != null) support.clicked += () => Debug.Log("[PSV Installer Wizard] (stub) Support");

            var check = Root.Q<Label>("hub-check-updates");
            if (check != null) check.RegisterCallback<ClickEvent>(_ => _router.GoTo("about"));
        }

        private void BindTile(string name, System.Action onClick)
        {
            var tile = Root.Q<VisualElement>(name);
            if (tile != null) tile.RegisterCallback<ClickEvent>(_ => onClick());
        }
    }
}
