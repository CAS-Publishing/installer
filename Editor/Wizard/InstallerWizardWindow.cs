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

        // Order is the wizard order; element [0] is the entry screen.
        private static readonly string[] ScreenOrder =
            { "welcome", "integration", "components", "hub", "progress", "done", "settings", "setup", "about" };

        // Persists the current screen across domain reloads (e.g. the reload UPM triggers after
        // an install) so the wizard returns to where the user was instead of jumping to Welcome.
        // SessionState survives reloads and resets on editor restart.
        private const string CurrentScreenKey = "PSV.Installer.Wizard.CurrentScreen";

        // Set by OpenAtWelcome so the Welcome screen preselects a specific platform on a build-target
        // switch even when the window was already open (its WelcomeScreen kept its construction-time
        // platform). One-shot, consumed in WelcomeScreen.OnEnter.
        private const string RequestPlatformKey = "PSV.Installer.Wizard.RequestPlatform";

        // Screens that appear as top tabs; everything else is a full-screen flow state.
        // Welcome + Integration are first-run-only flow screens — once set up, the user never
        // returns to the install picker, so they are NOT tabs.
        private static readonly string[] TabScreens = { "components", "setup", "about" };

        // Per-project key — EditorPrefs is machine-global, so include the project path to keep
        // the intro flag scoped to THIS project (each client project is first-run on its own).
        private static string IntroDoneKey => "PSV.Installer.Wizard.IntroDone:" + Application.dataPath;

        /// <summary>True once the user has passed the first-run intro (Integration), per project,
        /// so later opens land on the tabs instead of Welcome.</summary>
        internal static bool IntroDone
        {
            get => EditorPrefs.GetBool(IntroDoneKey, false);
            set => EditorPrefs.SetBool(IntroDoneKey, value);
        }

        private WizardRouter _router;
        private VisualElement _tabBar;
        private Button _tabComponents;
        private Button _tabSetup;
        private Button _tabAbout;
        private VisualElement _aboutBadge;

        [MenuItem("PSV Game Studio/Wizard")]
        public static void Open()
        {
            var window = GetWindow<InstallerWizardWindow>(true, "CAS Hub Installer", true);
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
        /// Reopens the wizard at the first-run Welcome screen, clearing the per-project intro flag.
        /// Welcome/Integration are not tabs (you don't normally return to the install picker), so
        /// this is the way back to redo setup — and how to reach Welcome in a project that already
        /// passed the intro.
        /// </summary>
        [MenuItem("PSV Game Studio/Wizard (Restart Intro)")]
        public static void OpenFirstRun()
        {
            IntroDone = false;
            SessionState.SetString(CurrentScreenKey, "welcome"); // don't restore a previous tab
            Open();
            if (HasOpenInstances<InstallerWizardWindow>())
                GetWindow<InstallerWizardWindow>()._router?.GoTo("welcome");
        }

        /// <summary>
        /// Opens the wizard at Welcome with <paramref name="platform"/> preselected (the build-target
        /// watcher uses this). Does NOT clear <see cref="IntroDone"/> — it is a targeted "configure
        /// this platform" prompt, not a first-run reset.
        /// </summary>
        public static void OpenAtWelcome(string platform)
        {
            // Already open ON Welcome: the user is actively configuring a platform — a build-target
            // switch must not reopen/reseed and discard the id they're mid-typing. Leave them be; they
            // can switch the platform tab by hand. (HasOpenInstances first, so GetWindow never creates.)
            if (HasOpenInstances<InstallerWizardWindow>() &&
                GetWindow<InstallerWizardWindow>()._router?.Current == "welcome")
                return;

            SessionState.SetString(RequestPlatformKey, platform ?? string.Empty);
            SessionState.SetString(CurrentScreenKey, "welcome");
            Open();
            // A fresh Open() already previewed Welcome (ResolveStartScreen → welcome) and consumed the
            // request, so only navigate an already-open window that's on a DIFFERENT screen — this
            // avoids re-entering Welcome (a double OnEnter, two redundant catalog reads) on a new window.
            if (HasOpenInstances<InstallerWizardWindow>())
            {
                var router = GetWindow<InstallerWizardWindow>()._router;
                if (router != null && router.Current != "welcome")
                    router.GoTo("welcome");
            }
        }

        /// <summary>Returns and clears the platform requested by <see cref="OpenAtWelcome"/> (one-shot).</summary>
        internal static string ConsumeRequestedPlatform()
        {
            var p = SessionState.GetString(RequestPlatformKey, string.Empty);
            if (!string.IsNullOrEmpty(p)) SessionState.EraseString(RequestPlatformKey);
            return p;
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
            CasIdApplier.ApplyPending();

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

            // The Setup tab only makes sense once something is installed (you configure installed
            // packages). Recomputed each time the window is built — installs trigger a reload.
            if (_tabSetup != null)
                _tabSetup.style.display = AnyComponentInstalled() ? DisplayStyle.Flex : DisplayStyle.None;

            _router.Preview(ResolveStartScreen());
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
            if (!string.IsNullOrEmpty(saved) && Array.IndexOf(ScreenOrder, saved) >= 0)
            {
                // Past the intro, don't RESTORE a first-run-only flow screen (welcome/integration) —
                // those have no tab bar. Exception: a build-target-switch prompt that explicitly
                // requested Welcome (RequestPlatform still pending). Otherwise a window closed on
                // Welcome would reopen tab-less instead of on the Components tab.
                var welcomeRequested = !string.IsNullOrEmpty(SessionState.GetString(RequestPlatformKey, ""));
                if (IntroDone && !welcomeRequested && (saved == "welcome" || saved == "integration"))
                    return "components";
                return saved;
            }

            // Fresh open: first run shows the intro; afterwards land on the Components tab.
            return IntroDone ? "components" : "welcome";
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
            router.Register(new WelcomeScreen());
            router.Register(new IntegrationModeScreen());
            router.Register(new ComponentsScreen());
            router.Register(new HubActionsScreen());
            router.Register(new ProgressScreen());
            router.Register(new DoneScreen());
            router.Register(new SettingsRedirectScreen());
            router.Register(new SetupScreen());
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
            _tabSetup      = root.Q<Button>("tab-setup");
            _tabAbout      = root.Q<Button>("tab-about");

            if (_tabComponents != null) _tabComponents.clicked += () => _router.GoTo("components");
            if (_tabSetup != null)      _tabSetup.clicked      += () => _router.GoTo("setup");
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

        // Show the tab bar only on tab screens; flow screens (Progress/Done/Hub/Settings) are
        // full-screen. Highlights the active tab.
        private void UpdateTabBar(string id)
        {
            var isTab = Array.IndexOf(TabScreens, id) >= 0;
            if (_tabBar != null)
                _tabBar.style.display = isTab ? DisplayStyle.Flex : DisplayStyle.None;

            SetTabActive(_tabComponents, id == "components");
            SetTabActive(_tabSetup, id == "setup");
            SetTabActive(_tabAbout, id == "about");
        }

        private static void SetTabActive(Button tab, bool active)
        {
            if (tab == null) return;
            if (active) tab.AddToClassList("cas-tab--active");
            else        tab.RemoveFromClassList("cas-tab--active");
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
