using System.Collections.Generic;
using PSV.Installer.Common;
using UnityEngine.UIElements;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// First screen. Configures the CAS ID for ONE platform per pass. The selected platform
    /// defaults to the active build target (iOS→iOS, else Android) and is switchable via the
    /// Android/iOS segments. The field starts empty unless CAS already has a real managerId for
    /// the selected platform (then it's prefilled). Input is validated per platform (Android =
    /// bundle id, iOS = numeric); Next is locked until the value is valid. On Next only the
    /// selected platform's id is persisted and applied, then the Integration screen is shown.
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
        private readonly Label _placeholder;
        private readonly VisualElement _methodUpm, _methodGit, _radioUpm, _radioGit;
        private InstallMethod _method;

        private string _platform;   // the single platform this pass configures
        private string _regex;      // active platform's validation pattern

        // The per-platform validation regex/hint comes from the catalog (CatalogLoader.Load() is
        // uncached). Memoise it per platform so toggling the Android/iOS segment back and forth doesn't
        // re-parse the catalog on every click. (The seed/existing-id is intentionally NOT cached — it
        // must reflect the asset's current managerId.)
        private readonly Dictionary<string, (string regex, string hint)> _valCache =
            new Dictionary<string, (string regex, string hint)>();

        public WelcomeScreen()
        {
            Root = new VisualElement();
            WizardAssets.CloneInto("Welcome", Root);

            _field      = Root.Q<TextField>("welcome-casid");
            _tabAndroid = Root.Q<Button>("welcome-tab-android");
            _tabIos     = Root.Q<Button>("welcome-tab-ios");
            _next       = Root.Q<Button>("welcome-next");
            _placeholder = Root.Q<Label>("welcome-casid-ph");

            _methodUpm = Root.Q<VisualElement>("method-upm");
            _methodGit = Root.Q<VisualElement>("method-git");
            _radioUpm  = Root.Q<VisualElement>("radio-upm");
            _radioGit  = Root.Q<VisualElement>("radio-git");
            _method    = InstallMethodState.Get();

            // Default to the project's active build target; the user may switch.
            _platform = PlatformDetect.ActivePlatform();

            var version = Root.Q<Label>("welcome-version");
            if (version != null) version.text = "v" + WizardAssets.InstallerVersion;
        }

        // Memoised per-platform catalog regex/hint lookup (see _valCache).
        private (string regex, string hint) ValidationFor(string platform)
        {
            if (!_valCache.TryGetValue(platform, out var v))
            {
                v = CasIdValidation.For(platform);
                _valCache[platform] = v;
            }
            return v;
        }

        // The field starts EMPTY on (re)open: a previously-typed-but-unapplied id is NOT restored
        // (feedback #2). Only a REAL existing CAS managerId prefills it (feedback #2.2).
        private static string Seed(string platform) =>
            ResolveSeed(InstallerKeyStore.Get(CasId, platform), CasIdApplier.ReadExisting(platform));

        /// <summary>
        /// Seed policy for the CAS-ID field: the real existing CAS managerId only. The stored value
        /// is intentionally ignored — passed in solely to document (and lock via test) that it is
        /// dropped, not forgotten. Pure/testable.
        /// </summary>
        internal static string ResolveSeed(string storedValue, string existingCasId) =>
            existingCasId ?? "";

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
                _field?.RegisterValueChangedCallback(_ => { RefreshNext(); UpdatePlaceholder(); });
                if (_next != null) _next.clicked += OnNext;
            }

            // A build-target switch may request a specific platform (overrides the construction-time
            // default, which matters when this window was already open). One-shot.
            var requested = InstallerWizardWindow.ConsumeRequestedPlatform();
            if (!string.IsNullOrEmpty(requested)) _platform = requested;

            ShowPlatform(_platform);
            ShowMethod(_method);
        }

        private void SelectPlatform(string platform)
        {
            if (_platform == platform) return;
            ShowPlatform(platform);
        }

        private void ShowPlatform(string platform)
        {
            _platform = platform;

            // Resolve this platform's validation pattern + prefill its real existing id (if any).
            var (regex, hint) = ValidationFor(platform);
            _regex = regex;
            if (_placeholder != null) _placeholder.text = hint;
            if (_field != null) _field.SetValueWithoutNotify(Seed(platform));
            UpdatePlaceholder();

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
            _next?.SetEnabled(CasIdValidation.IsValid(_field?.value, _regex));
        }

        // The placeholder hint shows only while the field is empty.
        private void UpdatePlaceholder()
        {
            if (_placeholder == null) return;
            _placeholder.style.display =
                string.IsNullOrEmpty(_field?.value) ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnNext()
        {
            // One platform per pass: the user deliberately entered this id, so FORCE-write it for the
            // selected platform — overwriting any leftover managerId (NOT gated by the placeholder
            // check) — and persist it for a later (re)install. SetManagerId does both.
            CasIdApplier.SetManagerId(_platform, _field?.value);

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
