using PSV.Installer.Common;
using UnityEngine.UIElements;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// First screen. Captures the CAS ID with a single field whose value is swapped by the
    /// Android/iOS segments (many clients ship one platform only — two fields confused them).
    /// The field starts empty, unless CAS is already installed with a real managerId, in which
    /// case the existing value is shown. <c>Next</c> is locked until at least one platform has an
    /// id. On <c>Next</c> the ids are persisted and applied eagerly (so an already-installed CAS
    /// picks them up without reopening the wizard), then the Integration screen is shown.
    /// </summary>
    internal sealed class WelcomeScreen : IWizardScreen
    {
        private const string CasId = "com.cleversolutions.ads.unity";

        public string Id => "welcome";
        public VisualElement Root { get; }

        private WizardRouter _router;
        private bool _bound;

        private readonly TextField _field;
        private readonly Button _tabAndroid, _tabIos, _next;
        private readonly VisualElement _methodUpm, _methodGit, _radioUpm, _radioGit;
        private InstallMethod _method;

        // Per-platform values held while the single field shows one at a time.
        private string _android, _ios;
        private string _platform = "Android";

        public WelcomeScreen()
        {
            Root = new VisualElement();
            WizardAssets.CloneInto("Welcome", Root);

            _field      = Root.Q<TextField>("welcome-casid");
            _tabAndroid = Root.Q<Button>("welcome-tab-android");
            _tabIos     = Root.Q<Button>("welcome-tab-ios");
            _next       = Root.Q<Button>("welcome-next");

            _methodUpm = Root.Q<VisualElement>("method-upm");
            _methodGit = Root.Q<VisualElement>("method-git");
            _radioUpm  = Root.Q<VisualElement>("radio-upm");
            _radioGit  = Root.Q<VisualElement>("radio-git");
            _method    = InstallMethodState.Get();

            // Seed each platform: a previously entered value wins, else the id already present in
            // CAS settings (when CAS was installed before the hub), else empty. No bundle-id default.
            _android = Seed("Android");
            _ios     = Seed("iOS");

            var version = Root.Q<Label>("welcome-version");
            if (version != null) version.text = "v" + WizardAssets.InstallerVersion;
        }

        // Stored key first; then any real managerId already in CAS settings; else empty.
        private static string Seed(string platform)
        {
            var stored = InstallerKeyStore.Get(CasId, platform);
            if (!string.IsNullOrEmpty(stored)) return stored;
            return CasIdApplier.ReadExisting(platform) ?? "";
        }

        /// <summary>Next is enabled once at least one platform has an id entered.</summary>
        internal static bool CanProceed(string android, string ios) =>
            !string.IsNullOrEmpty(android?.Trim()) || !string.IsNullOrEmpty(ios?.Trim());

        public void OnEnter(WizardRouter router)
        {
            _router = router;
            if (!_bound)
            {
                _bound = true;

                _methodUpm?.RegisterCallback<ClickEvent>(_ => SelectMethod(InstallMethod.Upm));
                _methodGit?.RegisterCallback<ClickEvent>(_ => SelectMethod(InstallMethod.Git));
                _tabAndroid?.RegisterCallback<ClickEvent>(_ => SelectPlatform("Android"));
                _tabIos?.RegisterCallback<ClickEvent>(_ => SelectPlatform("iOS"));
                _field?.RegisterValueChangedCallback(e =>
                {
                    // Keep the active platform's buffer in sync, then re-evaluate the gate.
                    if (_platform == "Android") _android = e.newValue;
                    else                        _ios     = e.newValue;
                    RefreshNext();
                });
                if (_next != null) _next.clicked += OnNext;
            }

            // (Re)load the current platform into the field and refresh visuals on every entry.
            ShowPlatform(_platform);
            ShowMethod(_method);
        }

        private void SelectPlatform(string platform)
        {
            if (_platform == platform) return;
            // Commit the visible value before switching buffers.
            if (_field != null)
            {
                if (_platform == "Android") _android = _field.value;
                else                        _ios     = _field.value;
            }
            ShowPlatform(platform);
        }

        private void ShowPlatform(string platform)
        {
            _platform = platform;
            if (_field != null) _field.SetValueWithoutNotify(platform == "Android" ? _android : _ios);

            SetSegActive(_tabAndroid, platform == "Android");
            SetSegActive(_tabIos, platform == "iOS");
            RefreshNext();
        }

        private static void SetSegActive(Button seg, bool active)
        {
            if (seg == null) return;
            if (active) seg.AddToClassList("cas-seg__item--active");
            else        seg.RemoveFromClassList("cas-seg__item--active");
        }

        private void RefreshNext()
        {
            _next?.SetEnabled(CanProceed(_android, _ios));
        }

        private void OnNext()
        {
            // Persist both ids so CasIdApplier can write them to CAS settings after install…
            InstallerKeyStore.Set(CasId, "Android", _android);
            InstallerKeyStore.Set(CasId, "iOS", _ios);

            // …and apply now, in case CAS is already installed (otherwise it'd only land on the
            // next Components rebuild / window reopen — the reported "won't apply until reopen" bug).
            CasIdApplier.ApplyPending();

            _router.GoTo("integration");
        }

        private void SelectMethod(InstallMethod method)
        {
            _method = method;
            InstallMethodState.Set(method);
            ShowMethod(method);
        }

        private void ShowMethod(InstallMethod method)
        {
            SetRadio(_radioUpm, method == InstallMethod.Upm);
            SetRadio(_radioGit, method == InstallMethod.Git);
        }

        private static void SetRadio(VisualElement radio, bool on)
        {
            if (radio == null) return;
            if (on) radio.AddToClassList("cas-radio--on");
            else    radio.RemoveFromClassList("cas-radio--on");
        }
    }
}
