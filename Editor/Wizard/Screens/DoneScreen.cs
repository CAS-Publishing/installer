using UnityEngine;
using UnityEngine.UIElements;

namespace PSV.Installer.Wizard
{
    internal sealed class DoneScreen : IWizardScreen
    {
        public string Id => "done";
        public VisualElement Root { get; }

        private WizardRouter _router;
        private bool _bound;

        public DoneScreen()
        {
            Root = new VisualElement();
            WizardAssets.CloneInto("Done", Root);
        }

        public void OnEnter(WizardRouter router)
        {
            _router = router;
            if (_bound) return;
            _bound = true;

            var settings = Root.Q<Button>("done-settings");
            if (settings != null) settings.clicked += () => _router.GoTo("setup");

            var docs = Root.Q<Button>("done-docs");
            if (docs != null) docs.clicked += () => Debug.Log("[PSV Installer Wizard] (stub) View Documentation");

            // "Finish" returns to the app (Components tab) rather than closing the window —
            // closing is still available via the window's own X.
            var close = Root.Q<Button>("done-close");
            if (close != null) close.clicked += () => _router.GoTo("components");
        }
    }
}
