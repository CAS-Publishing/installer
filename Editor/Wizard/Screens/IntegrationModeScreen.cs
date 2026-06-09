using UnityEngine.UIElements;

namespace PSV.Installer.Wizard
{
    internal sealed class IntegrationModeScreen : IWizardScreen
    {
        public string Id => "integration";
        public VisualElement Root { get; }

        private WizardRouter _router;
        private bool _bound;
        private bool _auto = true;
        private readonly VisualElement _autoCard, _manualCard, _warning;

        public IntegrationModeScreen()
        {
            Root = new VisualElement();
            WizardAssets.CloneInto("IntegrationMode", Root);

            _autoCard   = Root.Q<VisualElement>("integ-auto");
            _manualCard = Root.Q<VisualElement>("integ-manual");
            _warning    = Root.Q<VisualElement>("integ-warning");

            // The warning only applies to manual integration — hidden while "auto" (the default).
            if (_warning != null) _warning.style.display = DisplayStyle.None;

            var warningText = Root.Q<Label>("integ-warning-text");
            if (warningText != null)
                warningText.text = "<b>Warning!</b> Manual integration may lead to incorrect configuration " +
                                   "and unpredictable behavior. Are you sure you know what you're doing?";
        }

        public void OnEnter(WizardRouter router)
        {
            _router = router;
            if (_bound) return;
            _bound = true;

            _autoCard?.RegisterCallback<ClickEvent>(_ => SetAuto(true));
            _manualCard?.RegisterCallback<ClickEvent>(_ => SetAuto(false));

            var cancel = Root.Q<Button>("integ-cancel");
            if (cancel != null) cancel.clicked += () => _router.GoTo("welcome");

            var cont = Root.Q<Button>("integ-continue");
            if (cont != null) cont.clicked += () =>
            {
                if (_auto)
                {
                    // Express: StartAll shows a confirm dialog and marks the intro done only once the
                    // user commits — so cancelling that dialog leaves them on this screen.
                    AutoInstaller.StartAll(_router);
                }
                else
                {
                    InstallerWizardWindow.IntroDone = true; // past the first-run intro → later opens land on tabs
                    _router.GoTo("components");             // manual: pick per component
                }
            };
        }

        private void SetAuto(bool auto)
        {
            _auto = auto;
            if (auto)
            {
                _autoCard?.AddToClassList("cas-card--green");
                _manualCard?.RemoveFromClassList("cas-card--blue");
            }
            else
            {
                _autoCard?.RemoveFromClassList("cas-card--green");
                _manualCard?.AddToClassList("cas-card--blue");
            }

            // Warning appears only for manual integration.
            if (_warning != null)
                _warning.style.display = auto ? DisplayStyle.None : DisplayStyle.Flex;
        }
    }
}
