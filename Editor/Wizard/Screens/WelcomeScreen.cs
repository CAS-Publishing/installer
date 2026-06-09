using UnityEditor;
using UnityEngine.UIElements;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// First screen. Captures the CAS ID per platform (prefilled from the project's bundle
    /// identifier), then routes to the Integration-mode screen where the user picks Express/Manual.
    /// </summary>
    internal sealed class WelcomeScreen : IWizardScreen
    {
        private const string CasId = "com.cleversolutions.ads.unity";

        public string Id => "welcome";
        public VisualElement Root { get; }

        private WizardRouter _router;
        private bool _bound;

        private readonly TextField _casAndroid, _casIos;

        public WelcomeScreen()
        {
            Root = new VisualElement();
            WizardAssets.CloneInto("Welcome", Root);

            _casAndroid = Root.Q<TextField>("welcome-casid-android");
            _casIos     = Root.Q<TextField>("welcome-casid-ios");

            // Prefill each CAS ID from a previously entered value, else the project bundle id.
            if (_casAndroid != null)
                _casAndroid.value = InstallerKeyStore.GetOrDefault(CasId, "Android",
                    InstallerKeyStore.BundleId(BuildTargetGroup.Android));
            if (_casIos != null)
                _casIos.value = InstallerKeyStore.GetOrDefault(CasId, "iOS",
                    InstallerKeyStore.BundleId(BuildTargetGroup.iOS));

            var version = Root.Q<Label>("welcome-version");
            if (version != null) version.text = "v" + WizardAssets.InstallerVersion;
        }

        public void OnEnter(WizardRouter router)
        {
            _router = router;
            if (_bound) return;
            _bound = true;

            var next = Root.Q<Button>("welcome-next");
            if (next != null) next.clicked += OnNext;
        }

        private void OnNext()
        {
            // Persist the entered CAS IDs so CasIdApplier can write them to CAS settings after install.
            InstallerKeyStore.Set(CasId, "Android", _casAndroid?.value);
            InstallerKeyStore.Set(CasId, "iOS", _casIos?.value);

            _router.GoTo("integration");
        }
    }
}
