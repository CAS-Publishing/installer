using UnityEngine;
using UnityEngine.UIElements;

namespace PSV.Installer.Wizard
{
    internal sealed class SettingsRedirectScreen : IWizardScreen
    {
        public string Id => "settings";
        public VisualElement Root { get; }

        private WizardRouter _router;
        private bool _bound;

        public SettingsRedirectScreen()
        {
            Root = new VisualElement();
            WizardAssets.CloneInto("SettingsRedirect", Root);

            var tip = Root.Q<Label>("settings-tip-text");
            if (tip != null)
                tip.text = "<color=#9CC2FF>Tip:</color> All Hub installer changes here are applied " +
                           "immediately and saved to the project.";
        }

        public void OnEnter(WizardRouter router)
        {
            _router = router;
            if (_bound) return;
            _bound = true;

            var open = Root.Q<Button>("settings-open");
            if (open != null) open.clicked += () => Debug.Log("[PSV Installer Wizard] (stub) Open CAS Settings Window");

            var back = Root.Q<Label>("settings-back");
            if (back != null) back.RegisterCallback<ClickEvent>(_ => _router.GoTo("components"));
        }
    }
}
