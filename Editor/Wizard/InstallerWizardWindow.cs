using System;
using PSV.Installer.Scanner;
using PSV.Installer.Ui;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// New UI-Toolkit installer window (preview). Runs in parallel to the existing IMGUI
    /// <c>InstallerWindow</c> and shares no state with it. Iteration 1 = layout + navigation +
    /// stub data only; no Scanner/Migrator/CatalogUpdater logic is wired here.
    /// </summary>
    public sealed class InstallerWizardWindow : EditorWindow
    {
        // Comfortable default; the window is resizable. Min keeps the layout from collapsing;
        // height floor matches the design so non-scrolling screens never clip.
        private static readonly Vector2 DefaultSize = new Vector2(480, 560);
        private static readonly Vector2 MinSize     = new Vector2(440, 560);
        private static readonly Vector2 MaxSize     = new Vector2(4000, 4000);

        // Order is the wizard order; element [0] is the entry screen. The 3-step intro flow is
        // ready → progress → configure → done; components/about are post-intro tabs.
        private static readonly string[] ScreenOrder =
            { "ready", "progress", "configure", "done", "components", "about" };

        // Persists the current screen across domain reloads (e.g. the reload UPM triggers after
        // an install) so the wizard returns to where the user was instead of jumping back to Ready.
        // SessionState survives reloads and resets on editor restart.
        private const string CurrentScreenKey = "PSV.Installer.Wizard.CurrentScreen";

        // Screens that appear as top tabs; everything else is a full-screen flow state.
        // Ready/Progress/Done are first-run-only flow screens — once set up, the user never
        // returns to the install picker, so they are NOT tabs.
        private static readonly string[] TabScreens = { "components", "configure", "about" };

        // Per-project key — EditorPrefs is machine-global, so include the project path to keep
        // the intro flag scoped to THIS project (each client project is first-run on its own).
        private static string IntroDoneKey => "PSV.Installer.Wizard.IntroDone:" + Application.dataPath;

        /// <summary>True once the user has passed the first-run intro (Ready → Progress → Configure
        /// → Done), per project, so later opens land on the tabs instead of Ready.</summary>
        internal static bool IntroDone
        {
            get => EditorPrefs.GetBool(IntroDoneKey, false);
            set => EditorPrefs.SetBool(IntroDoneKey, value);
        }

        private WizardRouter _router;
        private VisualElement _tabBar;
        private Button _tabComponents;
        private Button _tabConfigure;
        private Button _tabAbout;
        private VisualElement _aboutBadge;

        private VisualElement _stepper;
        private Label _step1;
        private Label _step2;
        private Label _step3;

        [MenuItem("Assets/CleverAdsSolutions/Hub")]
        public static void Open()
        {
            var window = GetWindow<InstallerWizardWindow>(true, "CAS.AI Publishing Hub", true);
            window.minSize = MinSize;
            window.maxSize = MaxSize;

            // Give a comfy initial size without shrinking a window the user already enlarged.
            var r = window.position;
            if (r.width < DefaultSize.x || r.height < DefaultSize.y)
                window.position = new Rect(r.x, r.y,
                    Mathf.Max(r.width, DefaultSize.x), Mathf.Max(r.height, DefaultSize.y));

            window.Show();
        }

        /// <summary>
        /// Reopens the wizard at the first-run Ready screen, clearing the per-project intro flag.
        /// Ready/Progress are not tabs (you don't normally return to the install picker), so this
        /// is the way back to redo setup from scratch.
        /// </summary>
        [MenuItem("Assets/CleverAdsSolutions/Hub (Restart Intro)")]
        public static void OpenFirstRun()
        {
            IntroDone = false;
            SessionState.SetString(CurrentScreenKey, "ready"); // don't restore a previous tab
            Open();
            if (HasOpenInstances<InstallerWizardWindow>())
                GetWindow<InstallerWizardWindow>()._router?.GoTo("ready");
        }

        /// <summary>
        /// Opens the wizard on the Configuration screen (the build-target watcher uses this when CAS
        /// is installed but the newly active platform's CAS id isn't configured yet). Does NOT clear
        /// <see cref="IntroDone"/> — it is a targeted "configure this platform" prompt, not a first-run
        /// reset. Unlike the old Welcome-preselect flow, no platform id needs to be threaded through —
        /// the Configuration screen re-scans and shows per-platform status itself on every OnEnter.
        /// </summary>
        public static void OpenAtConfigure()
        {
            // Already open ON Configure: a build-target switch must not re-enter and force a redundant
            // OnEnter/rescan on a screen the user is already looking at (WizardRouter.Show always
            // re-invokes OnEnter, even for the current screen). Leave them be — the Configure screen
            // rescans on its own Refresh/OnEnter cadence anyway. (HasOpenInstances first, so
            // GetWindow never creates a window just to check.)
            if (HasOpenInstances<InstallerWizardWindow>() &&
                GetWindow<InstallerWizardWindow>()._router?.Current == "configure")
                return;

            SessionState.SetString(CurrentScreenKey, "configure");
            Open();
            // A fresh Open() already previewed Configure (ResolveStartScreen → "configure", since we
            // just set CurrentScreenKey) and ran OnEnter once — only navigate an ALREADY-open window
            // that's on a DIFFERENT screen, so a brand-new window never gets a redundant double
            // OnEnter/rescan.
            if (HasOpenInstances<InstallerWizardWindow>())
            {
                var router = GetWindow<InstallerWizardWindow>()._router;
                if (router != null && router.Current != "configure")
                    router.GoTo("configure");
            }
        }

        /// <summary>Closes the window if open — used by the All Done screen's Close button.</summary>
        public static void CloseActive()
        {
            if (HasOpenInstances<InstallerWizardWindow>())
                GetWindow<InstallerWizardWindow>().Close();
        }

        /// <summary>
        /// Opens the wizard only when the scan report differs from the last one shown
        /// (shared hash gate via <see cref="ScanReportStore"/>, so it doesn't re-pop on every
        /// reload). Bootstrap calls this through its UI hook.
        /// </summary>
        public static void ShowIfReportChanged(ScanReport report)
        {
            if (report == null) return;

            // First run: always open (idempotent; record hash AFTER so an install/self-delete reload
            // that kills the just-opened window can't latch the hash and suppress later opens).
            if (!IntroDone)
            {
                Open();
                ScanReportStore.SetLastShownHash(report.Hash);
                return;
            }

            // After first-run: auto-open ONLY when the installer drove the change (a manifest mutation
            // set the one-shot signal). Unrelated/manual UPM changes never re-pop the window — updates
            // surface via the About badge instead. ResolveStartScreen restores the saved screen
            // (Components for a normal post-intro reopen).
            if (!PSV.Installer.Common.InstallReloadSignal.ConsumePending()) return;

            Open();
            ScanReportStore.SetLastShownHash(report.Hash);
        }

        private void CreateGUI()
        {
            // Opening the wizard self-heals: make sure the metadata catalog is present (install it
            // if missing) and current (check the registry for a newer one). Async — never blocks the
            // window, and works regardless of whether the auto-open path ran.
            PSV.Installer.Bootstrap.EnsureMetadata();

            var root = rootVisualElement;

            root.styleSheets.Add(WizardAssets.LoadStyle("theme"));
            ApplyFont(root);

            var shell = WizardAssets.LoadTree("WizardShell");
            shell.CloneTree(root);

            var content = root.Q<VisualElement>("cas-content");
            _router = new WizardRouter(content, OnScreenShown);

            RegisterScreens(_router);
            BuildTabBar(root);
            RefreshUpdateBadge();

            // The Configuration tab only makes sense once something is installed (you configure
            // installed packages). Recomputed each time the window is built — installs trigger a reload.
            if (_tabConfigure != null)
                _tabConfigure.style.display = AnyComponentInstalled() ? DisplayStyle.Flex : DisplayStyle.None;

            _router.Preview(ResolveStartScreen());

            // If a recommended-install click armed a resume while the metadata catalog was still
            // installing, and the catalog is now present, continue that install automatically. The
            // catalog-Ok check short-circuits ConsumeResume, so the flag is preserved (not burned)
            // when the catalog still isn't ready — a later open retries. Deferred to delayCall so the
            // confirm modal doesn't run mid-CreateGUI.
            if (PSV.Installer.Catalog.CatalogLoader.Load().Status == PSV.Installer.Catalog.CatalogLoadStatus.Ok
                && AutoInstaller.ConsumeResume())
            {
                var router = _router;
                EditorApplication.delayCall += () => { if (router != null) AutoInstaller.StartAll(router); };
            }
        }

        private static bool AnyComponentInstalled()
        {
            if (ComponentStatusProvider.TryGetStatuses(out var statuses, out _))
                foreach (var s in statuses)
                    if (s.Installed) return true;
            return false;
        }

        private static string ResolveStartScreen()
        {
            // Mid-session restore (e.g. after the install-triggered domain reload).
            var saved = SessionState.GetString(CurrentScreenKey, "");
            return ResolveStartScreenPure(saved, IntroDone);
        }

        /// <summary>
        /// Pure decision logic for <see cref="ResolveStartScreen"/> — no SessionState/EditorPrefs
        /// reads, so it's directly testable. <paramref name="saved"/> is valid (present in
        /// <see cref="ScreenOrder"/>) → restore it, EXCEPT once past the intro a saved "ready" is
        /// redirected to "components" (past the intro you don't return to the install picker).
        /// Invalid/empty/stale ids (e.g. "welcome"/"integration" from a pre-Task-5 session, no longer
        /// in ScreenOrder) fall through to the fresh-open rule: first run shows Ready, afterwards
        /// lands on the Components tab.
        /// </summary>
        internal static string ResolveStartScreenPure(string saved, bool introDone)
        {
            if (!string.IsNullOrEmpty(saved) && Array.IndexOf(ScreenOrder, saved) >= 0)
            {
                if (introDone && saved == "ready") return "components";
                return saved;
            }

            return introDone ? "components" : "ready";
        }

        private static void ApplyFont(VisualElement root)
        {
            // Bind Inter in C# (not via USS url()) so a missing font can never break the
            // stylesheet — it simply falls back to the editor default. Font is inherited.
            var font = AssetDatabase.LoadAssetAtPath<Font>(WizardAssets.Fonts + "/Inter-Regular.ttf");
            if (font != null)
                root.style.unityFontDefinition = new StyleFontDefinition(font);
        }

        private void RegisterScreens(WizardRouter router)
        {
            // Welcome/Integration/HubActions/SettingsRedirect were removed (Task 9) — the 3-step intro
            // flow (Ready → Progress → Configure → Done) replaces them; CAS ID/audience/ad-format
            // configuration now lives entirely in CAS.AI's own settings window.
            router.Register(new ReadyScreen());
            router.Register(new ComponentsScreen());
            router.Register(new ProgressScreen());
            router.Register(new DoneScreen());
            router.Register(new ConfigureScreen());
            router.Register(new AboutScreen());
        }

        private void OnScreenShown(string id)
        {
            SessionState.SetString(CurrentScreenKey, id);
            UpdateTabBar(id);
        }

        private void BuildTabBar(VisualElement root)
        {
            _tabBar        = root.Q<VisualElement>("cas-tabbar");
            _tabComponents = root.Q<Button>("tab-components");
            _tabConfigure  = root.Q<Button>("tab-configure");
            _tabAbout      = root.Q<Button>("tab-about");

            _stepper = root.Q<VisualElement>("cas-stepper");
            _step1   = root.Q<Label>("step-install");
            _step2   = root.Q<Label>("step-configure");
            _step3   = root.Q<Label>("step-done");

            if (_tabComponents != null) _tabComponents.clicked += () => _router.GoTo("components");
            if (_tabConfigure != null)  _tabConfigure.clicked  += () => _router.GoTo("configure");
            if (_tabAbout != null)      _tabAbout.clicked      += () => _router.GoTo("about");

            if (_tabAbout != null)
            {
                // A Unity Button is a TextElement: once it gets a child element, its own `text` no
                // longer drives the element's width and the caption clips ("About" → "Abou"). Move the
                // caption into a child Label (it inherits color/font from .cas-tab, incl. the active
                // state) so the button sizes to the label, then overlay the absolutely-positioned badge.
                var label = new Label(_tabAbout.text) { pickingMode = PickingMode.Ignore };
                label.AddToClassList("cas-tab__label");
                _tabAbout.text = string.Empty;
                _tabAbout.Add(label);

                _aboutBadge = new VisualElement();
                _aboutBadge.AddToClassList("cas-tab__badge");
                _tabAbout.Add(_aboutBadge);
            }
        }

        // Shows the stepper header while still inside the pre-intro 3-step flow (Ready/Progress/
        // Configure/Done, before IntroDone flips) and the tab bar otherwise. "configure" is
        // shared between step 2 of the flow and the post-intro Configuration tab — IntroDone (not
        // the id alone) decides which header applies, so re-entering the Configure tab after the intro
        // shows tabs like Components/About do, not a step-2-of-3 stepper.
        private void UpdateTabBar(string id)
        {
            var step = WizardStepper.StepFor(id);
            var showStepper = step.HasValue && !IntroDone;
            var isTab = !showStepper && Array.IndexOf(TabScreens, id) >= 0;

            if (_tabBar != null)
                _tabBar.style.display = isTab ? DisplayStyle.Flex : DisplayStyle.None;
            if (_stepper != null)
                _stepper.style.display = showStepper ? DisplayStyle.Flex : DisplayStyle.None;

            if (showStepper) UpdateStepper(step.Value);

            SetTabActive(_tabComponents, id == "components");
            SetTabActive(_tabConfigure, id == "configure");
            SetTabActive(_tabAbout, id == "about");
        }

        private static void SetTabActive(Button tab, bool active)
        {
            if (tab == null) return;
            if (active) tab.AddToClassList("cas-tab--active");
            else        tab.RemoveFromClassList("cas-tab--active");
        }

        private void UpdateStepper(int currentStep)
        {
            SetStep(_step1, "Install", 1, currentStep);
            SetStep(_step2, "Configure", 2, currentStep);
            SetStep(_step3, "Done", 3, currentStep);
        }

        // Pending: "N Label" in muted grey. Active (currentStep == index): "N Label" in accent blue.
        // Passed (currentStep > index): "✓ Label" in green.
        private static void SetStep(Label label, string name, int index, int currentStep)
        {
            if (label == null) return;

            label.RemoveFromClassList("cas-introstep--active");
            label.RemoveFromClassList("cas-introstep--done");

            if (currentStep > index)
            {
                label.text = "✓ " + name;
                label.AddToClassList("cas-introstep--done");
            }
            else
            {
                label.text = index + " " + name;
                if (currentStep == index) label.AddToClassList("cas-introstep--active");
            }
        }

        // Shows a dot on the About tab when a newer installer version is published. Probes the
        // registry at most once per editor session (SessionState), caches the result so reopening
        // the window within the session reflects it without re-checking.
        private void RefreshUpdateBadge()
        {
            // Git-installed installer: no Verdaccio version to compare against, and About shows a
            // manual git-update instruction — so never show the "update available" badge here.
            if (PSV.Installer.Common.InstallerSource.IsGit()) { SetBadge(false); return; }

            SetBadge(SessionState.GetBool(UpdateBadgeState.AvailableKey, false));

            if (SessionState.GetBool(UpdateBadgeState.ProbedKey, false)) return;
            SessionState.SetBool(UpdateBadgeState.ProbedKey, true);

            PSV.Installer.Catalog.CatalogUpdater.CheckLatestVersion(UpdateBadgeState.PackageId,
                onSuccess: latest =>
                {
                    var available = PSV.Installer.Catalog.CatalogUpdater.IsNewer(latest, WizardAssets.InstallerVersion);
                    SessionState.SetBool(UpdateBadgeState.AvailableKey, available);
                    SetBadge(available);
                },
                onFailure: _ => { /* leave the badge as-is on a failed probe */ });
        }

        private void SetBadge(bool on)
        {
            if (_aboutBadge == null) return;
            if (on) _aboutBadge.AddToClassList("cas-tab__badge--on");
            else    _aboutBadge.RemoveFromClassList("cas-tab__badge--on");
        }
    }
}
