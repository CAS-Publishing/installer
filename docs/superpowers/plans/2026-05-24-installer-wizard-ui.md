# CAS Hub Installer Wizard UI — Implementation Plan (Iteration 1)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **VERIFICATION IS VISUAL.** Subagents cannot run Unity. There are no automated tests for UXML/USS layout. Each task's "verify" step means: **the human opens the new EditorWindow in Unity and confirms the screen renders per the design**. The C# must compile (Unity compiles on focus); the UXML/USS must load without console errors. Do not invent unit tests for visuals.

**Goal:** Build a NEW, parallel Unity EditorWindow that reproduces the 8-screen "CAS Hub Installer" wizard design (`docs/design/`) using UI Toolkit (UXML templates + USS stylesheets + thin C# controllers), with a working screen-to-screen navigation system and pure stub data — leaving the existing IMGUI `InstallerWindow` completely untouched.

**Architecture:** A single `InstallerWizardWindow : EditorWindow` loads a shell UXML (branded title bar + content host) and a shared `theme.uss`. A `WizardRouter` shows exactly one screen `VisualElement` at a time inside the content host and exposes `GoTo(id)` / `Back()`. Each of the 8 screens is its own `*.uxml` template + a thin `*Screen.cs` controller that builds from the UXML, binds stub data, and wires its buttons to the router. A dev-only screen picker in the header lets the reviewer jump to any screen. No Scanner/Migrator/CatalogUpdater logic is wired in this iteration — every action is a stub (`Debug.Log` + a router transition).

**Tech Stack:** Unity 2022.3 UI Toolkit (`UnityEngine.UIElements` + `UnityEditor.UIElements`), UXML, USS (with USS custom-property variables), C# (Editor assembly `PSV.Installer.Editor`). Icons: simple chrome glyphs done in pure USS; pictographic "hero" icons + vendor logos shipped as PNG textures rasterized from the design's SVG sources. Font: Inter (OFL) bundled as a `.ttf` FontAsset, with the editor default as fallback.

---

## Locked decisions (the "how" the owner delegated)

These were decided by the architect; the owner may veto any of them at review.

1. **UI Toolkit, not UGUI.** UGUI `.prefab` requires a Canvas GameObject living in a scene — wrong for an EditorWindow. UI Toolkit's `.uxml`/`.uss` are file assets (the editor-native "prefab"/template equivalent), edited in UI Builder, rendered directly into `rootVisualElement`. Nothing touches a scene.
2. **Parallel window.** The existing `Editor/Ui/InstallerWindow.cs` (IMGUI) is NOT modified. New code lives under `Editor/Wizard/`. New menu item `PSV Game Studio/Installer Wizard (Preview)`. Both windows coexist.
3. **Stubs only.** Iteration 1 = layout + style + navigation + placeholder data. Wiring real logic (Auto Init, per-row actions, Scanner/Migrator/CatalogUpdater) is iteration 2.
4. **Window is fixed-size portrait 480×560** (matches the design canvas `W=480, H=560`). `minSize = maxSize = (480, 560)`.
5. **Drop two mockup-only artifacts:** the floating step-number badge (a canvas annotation) and the fake window "X" is repurposed to call `Close()` (Unity already draws the docking tab; the branded title bar is kept for look).
6. **Gradients → solid colors.** USS in 2022.3 has no `linear-gradient`. The design's gradient buttons are approximated with solid accent colors (`#4C7EFF` primary hover `#5A8BFF`, default `#4A4A4A`). Visually negligible.
7. **Spinner animation** uses a tiny C# scheduler tick rotating the element (UI Toolkit 2022.3 has no `@keyframes`). This is behavior, not layout — acceptable.
8. **Icons in two tiers** (see Task 3). Chrome glyphs (dots, check, radio, checkbox, chevron, close, progress bar) = pure USS shapes, zero assets. Hero icons (robot, gears, wand, dashboard, reset, download, big-check, external) + 7 vendor logos = PNG textures rasterized from authored SVGs (no runtime dependency). If the build environment has no SVG rasterizer, fall back to Unity built-in editor icons as placeholders for the hero tier and note it for the owner — do NOT add `com.unity.vectorgraphics` to the published `package.json` without owner sign-off.

## Design source of truth

The pixel-accurate spec for every screen is the design bundle copied into the repo:

- `docs/design/screens.jsx` — all 8 screens (this plan cites exact line ranges).
- `docs/design/chrome.jsx` — `COLORS`, `WindowChrome`, `Button` variants, `Pill`, `FootRule`, `FontStack`.
- `docs/design/icons.jsx` — every icon's SVG path data (used verbatim to author the PNG sources).
- `docs/design/CAS Hub Installer.html` — canvas harness (`ART_W=500, ART_H=580`, screen list/order).

When a task says "reproduce per design lines X–Y", open that range in `screens.jsx` and match text, spacing, colors, and element order.

## Color tokens (from `docs/design/chrome.jsx` COLORS + per-screen literals)

Defined once as USS variables on `.cas-window` (Task 2):

```
--cas-window-bg:    #383838;
--cas-titlebar-bg:  #4D4D4D;
--cas-panel:        #3C3C3C;
--cas-panel-deep:   #2D2D2D;
--cas-card:         #333333;
--cas-border:       #1F1F1F;
--cas-border-soft:  #2A2A2A;
--cas-text:         #D3D3D3;
--cas-text-strong:  #FFFFFF;
--cas-text-muted:   #8E8E8E;
--cas-accent:       #4C7EFF;
--cas-accent-hi:    #5A8BFF;
--cas-accent-deep:  #3B6AE0;
--cas-green:        #22A06B;
--cas-green-dot:    #4ADE80;
--cas-yellow:       #E0A030;
--cas-yellow-btn:   #D8893F;
--cas-red:          #D8534F;
--cas-warning-bg:   #3A3320;
--cas-warning-bd:   #7A6420;
```

## Navigation graph (the "система переходів" — designed here, NOT in the mockup)

Screen ids (used by `WizardRouter.GoTo`): `welcome`, `integration`, `components`, `hub`, `progress`, `done`, `update`, `settings`.

Entry screen: `welcome`. The dev screen-picker can jump to any id at any time.

| Screen | Control | Goes to |
|---|---|---|
| welcome | Next | integration |
| integration | Cancel | welcome |
| integration | "I understand, continue" | `progress` if the **auto** card is selected; `components` if **manual** is selected |
| components | Refresh | stub (`Debug.Log`), stays |
| components | Continue (added footer primary button — design has none) | progress |
| hub | "Make everything for me" tile | progress |
| hub | "Open CAS Settings" tile | settings |
| hub | "Reset All Settings" tile | stub log, stays |
| hub | "Open Tenjin Dashboard" tile | stub log, stays |
| hub | "Check for Updates" link | update |
| hub | Documentation / Support | stub log, stays |
| progress | Cancel | hub |
| progress | (dev) "Simulate finish" footer button | done |
| done | "Open CAS Settings" | settings |
| done | "View Documentation" | stub log, stays |
| done | Close | `window.Close()` |
| update | Later | hub |
| update | Update Now | stub log, stays |
| settings | breadcrumb "Redirect"/back | hub |
| settings | "Open CAS Settings Window" | stub log, stays |

`Back()` uses an internal history stack (router records each `GoTo`).

---

## File Structure

All new files under the package, in a new `Editor/Wizard/` tree. Nothing outside `Editor/Wizard/` (and `package.json`/font/icon assets) is modified.

```
Editor/Wizard/
  InstallerWizardWindow.cs     — EditorWindow; CreateGUI loads shell UXML + theme USS, builds router, registers 8 screens, builds dev picker
  WizardRouter.cs              — screen registry, content host, GoTo(id)/Back(), history stack
  IWizardScreen.cs             — interface: string Id { get; }; VisualElement Root { get; }; void OnEnter(WizardRouter router)
  WizardAssets.cs              — const package-relative paths + helpers LoadTree(name)/LoadStyle(name)/LoadIcon(name)
  StubData.cs                  — static placeholder data (component rows, progress steps, versions) used by screens
  Screens/
    WelcomeScreen.cs
    IntegrationModeScreen.cs
    ComponentsScreen.cs
    HubActionsScreen.cs
    ProgressScreen.cs
    DoneScreen.cs
    UpdateScreen.cs
    SettingsRedirectScreen.cs
  Uxml/
    WizardShell.uxml           — title bar + dev-picker slot + #cas-content host
    Welcome.uxml
    IntegrationMode.uxml
    Components.uxml
    HubActions.uxml
    Progress.uxml
    Done.uxml
    Update.uxml
    SettingsRedirect.uxml
  Uss/
    theme.uss                  — variables, typography, buttons, cards, badges, dots, inputs, table, tiles, steps, progress, icon base + per-icon modifiers, chrome glyphs
  Icons/                       — PNG hero icons + vendor logos (Task 3), plus their .svg sources under Icons/src/
  Fonts/
    Inter-Regular.ttf, Inter-Medium.ttf, Inter-SemiBold.ttf, Inter-Bold.ttf  (+ generated FontAsset if needed)
```

A note on `.meta` files: Unity generates a `.meta` for each new asset when the editor regains focus. The reviewer must focus Unity once after files are created so imports happen; commits include the generated `.meta` files (commit discipline: stage `.meta` only after the reviewer confirms the window loads cleanly).

---

## Task 0: Branch + bring the design bundle into the repo

**Files:**
- Already done by the planner: `docs/design/*` (design bundle copied in).
- No code yet.

- [ ] **Step 1: Create the feature branch**

We are on `main`. Per repo policy, branch before any work.

Run:
```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer"
git checkout -b feat/installer-wizard-ui
```
Expected: `Switched to a new branch 'feat/installer-wizard-ui'`

- [ ] **Step 2: Confirm the design reference is present**

Run:
```bash
ls docs/design
```
Expected: `CAS Hub Installer.html  chrome.jsx  design-canvas.jsx  icons.jsx  screens.jsx  uploads  _chat-transcript.md  _handoff-readme.md`

- [ ] **Step 3: Commit the design reference**

```bash
git add docs/design
git commit -m "docs(installer): vendor CAS Hub Installer design bundle for wizard UI work"
```

---

## Task 1: Window shell + router + dev picker (empty screens)

Produces an openable window: branded title bar, a dev screen-picker dropdown, and an empty content host. No screens yet — `GoTo` on an unknown id is a no-op log.

**Files:**
- Create: `Editor/Wizard/WizardAssets.cs`
- Create: `Editor/Wizard/IWizardScreen.cs`
- Create: `Editor/Wizard/WizardRouter.cs`
- Create: `Editor/Wizard/InstallerWizardWindow.cs`
- Create: `Editor/Wizard/Uxml/WizardShell.uxml`
- Create: `Editor/Wizard/Uss/theme.uss` (minimal stub here; fully fleshed in Task 2)

- [ ] **Step 1: Create `WizardAssets.cs`**

```csharp
using UnityEditor;
using UnityEngine.UIElements;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// Central package-relative asset paths for the wizard UI, plus typed loaders.
    /// UPM mounts the package under the virtual "Packages/&lt;name&gt;/" path, so these
    /// AssetDatabase loads work regardless of where the package lives on disk.
    /// </summary>
    internal static class WizardAssets
    {
        public const string Root  = "Packages/com.psvgamestudio.installer/Editor/Wizard";
        public const string Uxml  = Root + "/Uxml";
        public const string Uss   = Root + "/Uss";
        public const string Icons = Root + "/Icons";

        public static VisualTreeAsset LoadTree(string name)
            => AssetDatabase.LoadAssetAtPath<VisualTreeAsset>($"{Uxml}/{name}.uxml");

        public static StyleSheet LoadStyle(string name)
            => AssetDatabase.LoadAssetAtPath<StyleSheet>($"{Uss}/{name}.uss");
    }
}
```

- [ ] **Step 2: Create `IWizardScreen.cs`**

```csharp
using UnityEngine.UIElements;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// One wizard screen. <see cref="Root"/> is the screen's VisualElement (built from its
    /// own UXML); the router parents it into the content host and toggles its visibility.
    /// <see cref="OnEnter"/> fires each time the screen becomes visible (refresh stub data,
    /// reset transient UI state).
    /// </summary>
    internal interface IWizardScreen
    {
        string Id { get; }
        VisualElement Root { get; }
        void OnEnter(WizardRouter router);
    }
}
```

- [ ] **Step 3: Create `WizardRouter.cs`**

```csharp
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// Holds the screen registry and the content host. Shows exactly one screen at a time
    /// by toggling display:flex/none. Keeps a history stack for Back().
    /// </summary>
    internal sealed class WizardRouter
    {
        private readonly VisualElement _host;
        private readonly Dictionary<string, IWizardScreen> _screens = new Dictionary<string, IWizardScreen>();
        private readonly Stack<string> _history = new Stack<string>();
        private string _current;

        public WizardRouter(VisualElement contentHost)
        {
            _host = contentHost;
        }

        public string Current => _current;
        public IEnumerable<string> ScreenIds => _screens.Keys;

        public void Register(IWizardScreen screen)
        {
            _screens[screen.Id] = screen;
            screen.Root.style.display = DisplayStyle.None;
            screen.Root.style.flexGrow = 1f;
            if (screen.Root.parent != _host)
                _host.Add(screen.Root);
        }

        /// <summary>Navigate to a screen, pushing the previous one onto the history stack.</summary>
        public void GoTo(string id)
        {
            if (!_screens.TryGetValue(id, out var next))
            {
                Debug.LogWarning($"[PSV Installer Wizard] No screen registered with id '{id}'.");
                return;
            }

            if (_current != null && _current != id)
                _history.Push(_current);

            Show(id, next);
        }

        /// <summary>Pop history; if empty, no-op.</summary>
        public void Back()
        {
            if (_history.Count == 0) return;
            var id = _history.Pop();
            if (_screens.TryGetValue(id, out var screen))
                Show(id, screen);
        }

        /// <summary>Jump without recording history (used by the dev picker).</summary>
        public void Preview(string id)
        {
            if (_screens.TryGetValue(id, out var screen))
                Show(id, screen);
        }

        private void Show(string id, IWizardScreen screen)
        {
            if (_current != null && _screens.TryGetValue(_current, out var prev))
                prev.Root.style.display = DisplayStyle.None;

            screen.Root.style.display = DisplayStyle.Flex;
            _current = id;
            screen.OnEnter(this);
        }
    }
}
```

- [ ] **Step 4: Create `Editor/Wizard/Uss/theme.uss` (minimal — expanded in Task 2)**

```css
.cas-window {
    --cas-window-bg: #383838;
    --cas-titlebar-bg: #4D4D4D;
    --cas-border: #1F1F1F;
    --cas-text: #D3D3D3;
    --cas-text-strong: #FFFFFF;
    --cas-accent: #4C7EFF;
    flex-grow: 1;
    background-color: var(--cas-window-bg);
    color: var(--cas-text);
}

.cas-titlebar {
    height: 28px;
    background-color: var(--cas-titlebar-bg);
    border-bottom-width: 1px;
    border-bottom-color: var(--cas-border);
    flex-direction: row;
    align-items: center;
    justify-content: space-between;
    padding-left: 10px;
    padding-right: 10px;
}

.cas-titlebar__title { color: #E0E0E0; font-size: 12px; }

.cas-titlebar__logo {
    width: 14px; height: 14px; border-radius: 3px;
    background-color: #1F3A66; color: #fff; font-size: 7px;
    -unity-text-align: middle-center; -unity-font-style: bold;
    margin-right: 8px;
}

.cas-devbar {
    flex-direction: row; align-items: center;
    background-color: #2D2D2D;
    border-bottom-width: 1px; border-bottom-color: var(--cas-border);
    padding: 3px 8px;
}
.cas-devbar__label { color: #6E6E6E; font-size: 10px; margin-right: 6px; }

.cas-content { flex-grow: 1; }
```

- [ ] **Step 5: Create `Editor/Wizard/Uxml/WizardShell.uxml`**

```xml
<ui:UXML xmlns:ui="UnityEngine.UIElements" xmlns:ue="UnityEditor.UIElements">
    <ui:VisualElement name="cas-window" class="cas-window">
        <ui:VisualElement name="cas-titlebar" class="cas-titlebar">
            <ui:VisualElement style="flex-direction: row; align-items: center;">
                <ui:Label text="CAS" class="cas-titlebar__logo" />
                <ui:Label text="CleverAdsSolutions Hub Installer" class="cas-titlebar__title" />
            </ui:VisualElement>
            <ui:Button name="cas-close" text="✕" class="cas-titlebar__close" />
        </ui:VisualElement>

        <ui:VisualElement name="cas-devbar" class="cas-devbar">
            <ui:Label text="DEV · jump to screen:" class="cas-devbar__label" />
            <ui:DropdownField name="cas-dev-picker" />
        </ui:VisualElement>

        <ui:VisualElement name="cas-content" class="cas-content" />
    </ui:VisualElement>
</ui:UXML>
```

- [ ] **Step 6: Create `InstallerWizardWindow.cs` (no screens registered yet)**

```csharp
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace PSV.Installer.Wizard
{
    /// <summary>
    /// New UI-Toolkit installer window (preview). Runs in parallel to the existing IMGUI
    /// <c>InstallerWindow</c>; shares no state with it. Iteration 1 = layout + navigation +
    /// stub data only.
    /// </summary>
    public sealed class InstallerWizardWindow : EditorWindow
    {
        private static readonly Vector2 FixedSize = new Vector2(480, 560);

        private WizardRouter _router;

        [MenuItem("PSV Game Studio/Installer Wizard (Preview)")]
        public static void Open()
        {
            var window = GetWindow<InstallerWizardWindow>(true, "CAS Hub Installer", true);
            window.minSize = FixedSize;
            window.maxSize = FixedSize;
            window.Show();
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            root.styleSheets.Add(WizardAssets.LoadStyle("theme"));

            var shell = WizardAssets.LoadTree("WizardShell");
            shell.CloneTree(root);

            root.Q<Button>("cas-close").clicked += Close;

            var content = root.Q<VisualElement>("cas-content");
            _router = new WizardRouter(content);

            RegisterScreens(_router);   // empty in Task 1; filled in Tasks 4–11
            BuildDevPicker(root);

            // Open on the entry screen if any screens are registered.
            foreach (var id in _router.ScreenIds) { _router.Preview(id); break; }
        }

        // Filled in by later tasks (one _router.Register(new XScreen()) per screen).
        private void RegisterScreens(WizardRouter router) { }

        private void BuildDevPicker(VisualElement root)
        {
            var picker = root.Q<DropdownField>("cas-dev-picker");
            var ids = new List<string>(_router.ScreenIds);
            picker.choices = ids;
            if (ids.Count > 0) picker.index = 0;
            picker.RegisterValueChangedCallback(evt =>
            {
                if (!string.IsNullOrEmpty(evt.newValue))
                    _router.Preview(evt.newValue);
            });
        }
    }
}
```

- [ ] **Step 7: Verify in Unity (human)**

Focus Unity so it imports the new assets and compiles. Open **PSV Game Studio → Installer Wizard (Preview)**.
Expected: a fixed 480×560 window with a dark `#4D4D4D` title bar reading "CleverAdsSolutions Hub Installer", a "✕" button (closes the window), a thin dev bar with an (empty) dropdown, and an empty dark content area. No console errors.

- [ ] **Step 8: Commit (after the human confirms it loads)**

```bash
git add Editor/Wizard package.json 2>/dev/null; git add Editor/Wizard
git commit -m "feat(installer): wizard window shell + router + dev screen picker (UI Toolkit)"
```
(Stage the generated `.meta` files too once Unity has created them.)

---

## Task 2: Full theme USS + reusable component classes

Flesh out `theme.uss` with every reusable class the screens need, plus the Inter font. Build a temporary "kitchen sink" screen to eyeball the primitives, then delete it.

**Files:**
- Modify: `Editor/Wizard/Uss/theme.uss` (replace the Task-1 stub with the full sheet)
- Create: `Editor/Wizard/Fonts/Inter-*.ttf` (download OFL Inter)
- Create (temporary, deleted in Step 6): `Editor/Wizard/Uxml/_KitchenSink.uxml`, `Editor/Wizard/Screens/_KitchenSinkScreen.cs`

- [ ] **Step 1: Add the Inter font files**

Download Inter (OFL-1.1) and place `Inter-Regular.ttf`, `Inter-Medium.ttf`, `Inter-SemiBold.ttf`, `Inter-Bold.ttf` under `Editor/Wizard/Fonts/`.
Run:
```bash
mkdir -p Editor/Wizard/Fonts
curl -sL -o Editor/Wizard/Fonts/Inter-Regular.ttf  "https://github.com/rsms/inter/raw/master/docs/font-files/Inter-Regular.ttf"
curl -sL -o Editor/Wizard/Fonts/Inter-Medium.ttf   "https://github.com/rsms/inter/raw/master/docs/font-files/Inter-Medium.ttf"
curl -sL -o Editor/Wizard/Fonts/Inter-SemiBold.ttf "https://github.com/rsms/inter/raw/master/docs/font-files/Inter-SemiBold.ttf"
curl -sL -o Editor/Wizard/Fonts/Inter-Bold.ttf     "https://github.com/rsms/inter/raw/master/docs/font-files/Inter-Bold.ttf"
ls -l Editor/Wizard/Fonts
```
Expected: four `.ttf` files, each > 250 KB. If the download fails (offline), skip the font binding in Step 2 (`-unity-font-definition`) and rely on the editor default font — note it for the owner; everything else is unaffected.

- [ ] **Step 2: Replace `theme.uss` with the full stylesheet**

Use the color tokens listed in this plan's "Color tokens" section. Implement these class families (selectors and the design lines they come from):

- `.cas-window` — declare all `--cas-*` variables (full token list above); set `-unity-font-definition: url("../Fonts/Inter-Regular.ttf");` when the font exists.
- Typography: `.cas-h1` (19px, semibold, white, centered), `.cas-h2` (17px semibold white), `.cas-muted` (12px `--cas-text-muted`), `.cas-label` (12px `#C7C7C7`). (`screens.jsx` headings, e.g. lines 42, 130, 278.)
- `.cas-titlebar*`, `.cas-devbar*`, `.cas-content` — keep from Task 1.
- Buttons (`docs/design/chrome.jsx` lines 111–155): `.cas-btn` (base: padding 6px 14px, radius 3px, 12px, row, centered, border `#1F1F1F`), `.cas-btn--primary` (bg `--cas-accent-deep`, hover `--cas-accent-hi`, text white), `.cas-btn--default` (bg `#4A4A4A`), `.cas-btn--soft` (bg `#3A3A3A`), `.cas-btn--ghost` (transparent, border `#2A2A2A`), `.cas-btn--warn` (bg `--cas-yellow-btn`). Hover via `.cas-btn--primary:hover` etc.
- `.cas-footer` (`FootRule` + footer row, `screens.jsx` 111–117): top border `--cas-border`, row, padding `10px 18px`, `justify-content: space-between`.
- Cards (`screens.jsx` 135–225): `.cas-card` (bg `--cas-card`, border `--cas-border-soft`, radius 6px, padding 14px, row, gap via margins), `.cas-card--green` (border `--cas-green`, bg rgba green), `.cas-card--blue` (border `--cas-accent`, bg rgba blue).
- Badges/pills (`chrome.jsx` 157–182): `.cas-badge`, `.cas-badge--green/--yellow/--red`.
- Status dots (`screens.jsx` 317, 384–391): `.cas-dot` (6–8px round), `.cas-dot--green` (`--cas-green-dot`), `--yellow`, `--red`.
- Inputs (`screens.jsx` 77–91): `.cas-input` (bg `#2A2A2A`, border `#1D1D1D`, radius 3px, padding `7px 9px`, color `#D5D5D5`).
- Chrome glyphs as pure USS (no assets):
  - `.cas-radio` / `.cas-radio--on` (14px circle, border `#5A5A5A`; inner 6px `--cas-accent` dot when on). (`icons.jsx` 62–67.)
  - `.cas-check` / `.cas-check--on` (12.5px rounded square; when on bg `--cas-accent` + a `✓` Label child). (`icons.jsx` 69–76.)
  - `.cas-chevron` (a 6px box with right+bottom borders rotated 45° → ">"). (`icons.jsx` 47–51.)
  - `.cas-checkmark` (green ✓ — a Label "✓" colored `--cas-green-dot`). (`screens.jsx` 529–533.)
- Table (`screens.jsx` 283–374): `.cas-table`, `.cas-table__head` (bg `#2C2C2C`, top+bottom border), `.cas-row` (row, padding `7px 10px`), `.cas-row--alt` (bg `#303030`).
- Hub tiles (`screens.jsx` 435–474): `.cas-tile` (bg `--cas-card`, radius 5px, padding `12px 14px`, row), `.cas-tile--primary` (bg `--cas-accent-deep`, border `#1D3E9A`), `.cas-tile__iconbox` (44px rounded), `.cas-tile__iconbox--ghost` (bg `#454545`).
- Progress (`screens.jsx` 516–567): `.cas-step` (row, padding `8px 4px`, bottom border `#2C2C2C`), `.cas-progress` (height 6px, bg `#2A2A2A`, radius 3px, border `#1F1F1F`), `.cas-progress__fill` (bg `--cas-accent`).
- Boxes: `.cas-warning` (bg `--cas-warning-bg`, border `--cas-warning-bd`, `screens.jsx` 228–249), `.cas-infobox` (rgba accent, `screens.jsx` 645–660), `.cas-tip` (bg `#2A2A2A`, `screens.jsx` 730–743).
- Icon base: `.cas-icon` (background-size contain, `-unity-background-image-tint-color: white`) + per-icon modifiers added in Task 3.

- [ ] **Step 3: Create the temporary kitchen-sink screen**

`Editor/Wizard/Uxml/_KitchenSink.uxml` — one of each: every button variant, a card (selected + unselected), each badge, each dot, a radio (on/off), a checkbox (on/off), an input, a chevron, the progress bar at 56%. `Editor/Wizard/Screens/_KitchenSinkScreen.cs` — implements `IWizardScreen` (`Id = "_sink"`), loads `_KitchenSink.uxml`.

- [ ] **Step 4: Temporarily register it**

In `InstallerWizardWindow.RegisterScreens`, add `router.Register(new _KitchenSinkScreen());`.

- [ ] **Step 5: Verify in Unity (human)**

Reopen the window, pick `_sink` in the dev dropdown. Expected: all primitives render with the design palette; Inter font visible (text crisper than editor default); buttons show hover color change; the on/off radio + checkbox + the 56% progress bar look right. No console errors.

- [ ] **Step 6: Remove the kitchen sink, commit**

Delete `_KitchenSink.uxml`, `_KitchenSinkScreen.cs` (and their `.meta`), remove the temporary `Register` line.
```bash
git add Editor/Wizard
git commit -m "feat(installer): full wizard theme USS (tokens, buttons, cards, badges, inputs, glyphs) + Inter font"
```

---

## Task 3: Hero icons + vendor logos (PNG, rasterized from authored SVGs)

**Files:**
- Create: `Editor/Wizard/Icons/src/*.svg` (authored from `docs/design/icons.jsx` path data)
- Create: `Editor/Wizard/Icons/*.png` (rasterized output)
- Modify: `Editor/Wizard/Uss/theme.uss` (add per-icon `.cas-icon--*` modifiers)

Hero icons needed (from `icons.jsx`): `robot` (109–120), `gear-big` (123–133), `wand` (136–144), `dashboard` (206–214), `reset` (216–221), `gear-tile` (223–228), `download-big` (230–237), `download` (85–91), `big-check` (239–244), `external` (100–106), `settings` (93–98), `refresh` (53–60). Vendor logos (`icons.jsx` 155–204): `logo-cas`, `logo-tenjin`, `logo-unity`, `logo-google`, `logo-meta`, `logo-iron`, `logo-iap`.

- [ ] **Step 1: Establish a rasterizer**

Run (try in order, stop at first success):
```bash
python -c "import cairosvg; print('cairosvg ok')" 2>/dev/null || pip install cairosvg 2>/dev/null && python -c "import cairosvg; print('cairosvg ok')"
```
If Python/cairosvg is unavailable, try Node:
```bash
npx --yes svgexport --version 2>/dev/null && echo "svgexport ok"
```
If neither works: **stop and fall back** — skip Steps 2–4, instead in Step 5 map each `.cas-icon--*` to a Unity built-in editor icon placeholder (e.g. in C# `EditorGUIUtility.IconContent("...")` is IMGUI-only, so for UI Toolkit use `Background.FromTexture2D(EditorGUIUtility.FindTexture("..."))` assigned to the element's `style.backgroundImage`). Note the placeholder fallback for the owner and proceed; real icons become a follow-up.

- [ ] **Step 2: Author the SVG sources**

For each icon, create `Editor/Wizard/Icons/src/<name>.svg` using the exact `<path>`/`<circle>`/`<rect>` geometry from `docs/design/icons.jsx`, wrapped in `<svg xmlns="http://www.w3.org/2000/svg" viewBox="...">` with the same viewBox as the JSX component. Stroke icons: `stroke="#FFFFFF"` (tinted later); colored logos keep their literal fills (e.g. Google `#4285F4`, Meta `#0866FF`).

Example (`robot`, from `icons.jsx` 109–120, viewBox 0 0 48 48):
```xml
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 48 48" fill="none">
  <rect x="11" y="16" width="26" height="22" rx="4" stroke="#fff" stroke-width="1.8"/>
  <path d="M24 10v6" stroke="#fff" stroke-width="1.8" stroke-linecap="round"/>
  <circle cx="24" cy="9" r="2" fill="#fff"/>
  <circle cx="19" cy="26" r="2.2" fill="#4ADE80"/>
  <circle cx="29" cy="26" r="2.2" fill="#4ADE80"/>
  <path d="M20 32h8" stroke="#fff" stroke-width="1.6" stroke-linecap="round"/>
  <path d="M11 24H7v8h4" stroke="#fff" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/>
  <path d="M37 24h4v8h-4" stroke="#fff" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/>
</svg>
```

- [ ] **Step 3: Rasterize to PNG at 2× for crispness**

With cairosvg (output 64px for 32px display icons, 128px for the big-check/download-big):
```bash
cd Editor/Wizard/Icons
for f in src/*.svg; do
  n=$(basename "$f" .svg)
  python -c "import cairosvg; cairosvg.svg2png(url='$f', write_to='$n.png', output_width=128, output_height=128)"
done
ls -l *.png
```
Expected: one transparent PNG per icon.

- [ ] **Step 4: Confirm import settings**

After Unity imports the PNGs (focus the editor), they default to `Texture` type — fine for `background-image`. No sprite slicing needed. (If any icon must keep its literal colors, in its `.png.meta` ensure it is not tinted by USS — see Step 5.)

- [ ] **Step 5: Add `.cas-icon--*` modifiers to `theme.uss`**

```css
.cas-icon { -unity-background-scale-mode: scale-to-fit; }
.cas-icon--robot       { background-image: url("../Icons/robot.png"); }
.cas-icon--gear-big    { background-image: url("../Icons/gear-big.png"); }
/* …one per hero icon… */
.cas-logo--cas         { background-image: url("../Icons/logo-cas.png"); }
/* …one per vendor logo… (logos keep their own colors — do NOT set tint) */
```
For white stroke icons that sit on colored tiles, set `-unity-background-image-tint-color: white;` on `.cas-icon` (already white in SVG, so tinting is a no-op but keeps intent explicit). Vendor logos must NOT inherit a tint — give them a separate base class `.cas-logo` without tint.

- [ ] **Step 6: Verify (human)**

Temporarily drop a few `<ui:VisualElement class="cas-icon cas-icon--robot" style="width:34px;height:34px;"/>` into the kitchen sink (or any screen) and confirm they render. Remove the temp markup.

- [ ] **Step 7: Commit**

```bash
git add Editor/Wizard/Icons Editor/Wizard/Uss/theme.uss
git commit -m "feat(installer): wizard hero icons + vendor logos (PNG from authored SVG) + USS bindings"
```

---

## Tasks 4–11: The eight screens

Each screen task has the same shape:
1. Create `Uxml/<Screen>.uxml` reproducing the design lines cited, using the Task-2 classes.
2. Create `Screens/<Screen>Screen.cs` implementing `IWizardScreen` — load the UXML, bind stub data, wire buttons to the router per the navigation graph.
3. Register it in `InstallerWizardWindow.RegisterScreens`.
4. Human verifies via the dev picker.
5. Commit.

The controller skeleton (identical pattern for all eight; shown once here, repeated with the screen's own id/UXML/wiring in each task):

```csharp
using UnityEngine.UIElements;

namespace PSV.Installer.Wizard
{
    internal sealed class WelcomeScreen : IWizardScreen
    {
        public string Id => "welcome";
        public VisualElement Root { get; }

        public WelcomeScreen()
        {
            Root = new VisualElement();
            WizardAssets.LoadTree("Welcome").CloneTree(Root);
        }

        public void OnEnter(WizardRouter router)
        {
            Root.Q<Button>("welcome-next").clicked -= () => { };   // (re-bind safely — see note)
        }
    }
}
```

**Binding note (applies to every screen):** wire button callbacks **once in the constructor** (after `CloneTree`), not in `OnEnter`, to avoid stacking duplicate handlers each time the screen is shown. `OnEnter` is only for refreshing data/visual state. Capture the `router` by storing it in a field on first `OnEnter`, or — cleaner — pass nothing and have the constructor take no router, then in `OnEnter` set a `_router` field and bind on first entry guarded by a `_bound` bool. Use this pattern:

```csharp
private WizardRouter _router;
private bool _bound;

public void OnEnter(WizardRouter router)
{
    _router = router;
    if (_bound) return;
    _bound = true;
    Root.Q<Button>("welcome-next").clicked += () => _router.GoTo("integration");
}
```

Use the `_router/_bound` pattern in all eight controllers.

Add `Editor/Wizard/StubData.cs` before Task 6 (Components needs it):

```csharp
using System.Collections.Generic;

namespace PSV.Installer.Wizard
{
    internal static class StubData
    {
        public sealed class ComponentRow
        {
            public string Logo;     // logo modifier suffix, e.g. "cas"
            public string Name;
            public string Sub;
            public string Tone;     // "green" | "yellow" | "red"
            public string Status;   // "Installed" | "Not Installed"
            public string Action;   // "Up to date" | "Install" | "Update"
            public string ActVar;   // "soft" | "primary" | "warn"
            public bool   Init;
        }

        // Mirrors docs/design/screens.jsx lines 265–273.
        public static readonly List<ComponentRow> Components = new List<ComponentRow>
        {
            new ComponentRow { Logo="cas",    Name="CAS SDK",                   Sub="Core SDK",         Tone="green",  Status="Installed",     Action="Up to date", ActVar="soft",    Init=true  },
            new ComponentRow { Logo="tenjin", Name="Tenjin SDK",               Sub="Attribution",      Tone="green",  Status="Installed",     Action="Up to date", ActVar="soft",    Init=true  },
            new ComponentRow { Logo="unity",  Name="Unity Ads",                Sub="Ads Network",      Tone="red",    Status="Not Installed", Action="Install",    ActVar="primary", Init=true  },
            new ComponentRow { Logo="google", Name="Google Mobile Ads",        Sub="Ads Network",      Tone="yellow", Status="Installed",     Action="Update",     ActVar="warn",    Init=true  },
            new ComponentRow { Logo="meta",   Name="Facebook Audience Network",Sub="Ads Network",      Tone="red",    Status="Not Installed", Action="Install",    ActVar="primary", Init=false },
            new ComponentRow { Logo="iron",   Name="IronSource LevelPlay",     Sub="Offerwall",        Tone="green",  Status="Installed",     Action="Up to date", ActVar="soft",    Init=true  },
            new ComponentRow { Logo="iap",    Name="Unity IAP",                Sub="In-App Purchases", Tone="green",  Status="Installed",     Action="Up to date", ActVar="soft",    Init=true  },
        };

        public sealed class ProgressStep { public string Label; public string State; public string Right; }

        // Mirrors docs/design/screens.jsx lines 500–510.
        public static readonly List<ProgressStep> ProgressSteps = new List<ProgressStep>
        {
            new ProgressStep { Label="Checking system requirements",         State="done" },
            new ProgressStep { Label="Resolving dependencies",               State="done" },
            new ProgressStep { Label="Installing CAS SDK",                   State="done" },
            new ProgressStep { Label="Installing Tenjin SDK",                State="done" },
            new ProgressStep { Label="Installing Unity Ads",                 State="active", Right="Installing…" },
            new ProgressStep { Label="Installing Google Mobile Ads",         State="wait" },
            new ProgressStep { Label="Installing Facebook Audience Network", State="wait" },
            new ProgressStep { Label="Configuring settings",                 State="wait" },
            new ProgressStep { Label="Initializing components",              State="wait" },
        };

        public const string InstallerVersion = "1.2.3";
        public const string LatestVersion    = "1.2.5";
        public const int    ProgressPercent  = 56;
    }
}
```

---

### Task 4: Welcome screen

**Design:** `docs/design/screens.jsx` lines 11–120. Logo lockup (CAS box + divider + "CAS / CleverAdsSolutions"), H1 "Welcome to CAS Hub Installer", sub-paragraph, "Installation method" with 3 radio options (git/upm/pkg), "Git URL" input prefilled `https://github.com/CleverAdsSolutions/cas-hub.git`, "Load Information" default button, footer `v1.2.3` + primary "Next".

**Files:** Create `Uxml/Welcome.uxml`, `Screens/WelcomeScreen.cs`. Modify `InstallerWizardWindow.cs`.

- [ ] **Step 1: `Uxml/Welcome.uxml`** — reproduce lines 11–117 with Task-2 classes. The 3 method rows use `.cas-radio` (first `--on`). Element names: `welcome-load`, `welcome-next`. Footer uses `.cas-footer` with a `v1.2.3` `.cas-muted` label + `.cas-btn.cas-btn--primary` named `welcome-next`.
- [ ] **Step 2: `Screens/WelcomeScreen.cs`** — `Id="welcome"`; `_router/_bound` pattern; on `welcome-next` → `_router.GoTo("integration")`; on `welcome-load` → `Debug.Log("[PSV Installer Wizard] (stub) Load Information")`. Radio selection: clicking a row moves the `--on` class (stub-local state, no logic).
- [ ] **Step 3:** In `RegisterScreens`: `router.Register(new WelcomeScreen());` (place first so it's the entry screen).
- [ ] **Step 4: Verify (human)** — dev-pick `welcome`; matches design; Next jumps to a (currently empty) `integration` or logs "no screen" until Task 5; Load Information logs.
- [ ] **Step 5: Commit** — `feat(installer): wizard Welcome screen`.

### Task 5: Integration mode screen

**Design:** lines 125–258. H2 "Integration mode" + sub. Auto card (green, robot icon, "Make everything for me", "Recommended" badge) selected by default; manual card (blue when selected, gear-big icon, "I will do it myself"). Warning box. Footer: "Cancel" + primary "I understand, continue".

**Files:** `Uxml/IntegrationMode.uxml`, `Screens/IntegrationModeScreen.cs`. Modify window.

- [ ] **Step 1: UXML** — two `.cas-card` (auto `--green` selected, manual). Icon boxes use `.cas-icon--robot` / `.cas-icon--gear-big`. `.cas-warning` box (lines 228–249). Names: `integ-auto`, `integ-manual`, `integ-cancel`, `integ-continue`.
- [ ] **Step 2: Controller** — `Id="integration"`; track selected card in a field (`_auto = true`). Clicking `integ-auto`/`integ-manual` toggles the `--green`/`--blue` selected classes. `integ-cancel` → `_router.GoTo("welcome")`. `integ-continue` → `_router.GoTo(_auto ? "progress" : "components")`.
- [ ] **Step 3:** Register. **Step 4:** Verify (cards toggle; continue branches by selection). **Step 5:** Commit `feat(installer): wizard Integration mode screen`.

### Task 6: Components Overview screen

**Design:** lines 264–395. H2 + sub. Table head (`Component | Status | Action | Auto Init`), 7 rows from `StubData.Components` (status dot + logo + name/sub, status text, action button by `ActVar`, `.cas-check` for Auto Init), zebra striping. Footer: soft "Refresh" (with refresh icon) + a colored-dot legend. **Add** a primary "Continue" button to the footer (design has none — needed for nav; see graph).

**Files:** `Uxml/Components.uxml`, `Screens/ComponentsScreen.cs`. Requires `StubData.cs` (add it now if not present). Modify window.

- [ ] **Step 1: UXML** — static head + an empty `#components-rows` container the controller fills; footer Refresh (`components-refresh`) + legend + `components-continue` primary.
- [ ] **Step 2: Controller** — `Id="components"`; in ctor, loop `StubData.Components`, build each row VisualElement (dot class by `Tone`, `.cas-logo--<Logo>`, name/sub labels, status label colored by tone, action `.cas-btn--<ActVar>`, `.cas-check`/`--on` by `Init`), append to `#components-rows`, alternate `.cas-row--alt`. Per-row action buttons log a stub. `components-refresh` logs. `components-continue` → `_router.GoTo("progress")`.
- [ ] **Step 3:** Register. **Step 4:** Verify (7 rows, correct dots/badges/buttons/checks; zebra). **Step 5:** Commit `feat(installer): wizard Components Overview screen + stub data`.

### Task 7: Hub Actions screen

**Design:** lines 401–493. H2 "Hub Actions". 4 tiles (first `--primary` blue with wand icon; others ghost icon-box with reset/gear-tile/dashboard). Footer: ghost "Documentation" + "Support" (left), version + "Check for Updates" link (right).

**Files:** `Uxml/HubActions.uxml`, `Screens/HubActionsScreen.cs`. Modify window.

- [ ] **Step 1: UXML** — 4 `.cas-tile` (names `hub-auto`, `hub-reset`, `hub-settings`, `hub-tenjin`), each with `.cas-tile__iconbox` + `.cas-icon--*` + title/desc + chevron. Footer buttons `hub-docs`, `hub-support`, link `hub-check-updates`, version label `v1.2.3`.
- [ ] **Step 2: Controller** — `Id="hub"`; `hub-auto`→`GoTo("progress")`, `hub-settings`→`GoTo("settings")`, `hub-check-updates`→`GoTo("update")`, `hub-reset`/`hub-tenjin`/`hub-docs`/`hub-support`→`Debug.Log` stub.
- [ ] **Step 3:** Register. **Step 4:** Verify. **Step 5:** Commit `feat(installer): wizard Hub Actions screen`.

### Task 8: Installation Progress screen

**Design:** lines 499–575. H2. Step list from `StubData.ProgressSteps` (done=green ✓, active=spinner + right label, wait=hollow circle + "Waiting"). Progress bar at 56% + "56%" label. Footer: centered "Cancel". **Add** a dev "Simulate finish" button (per graph) to reach `done`.

**Files:** `Uxml/Progress.uxml`, `Screens/ProgressScreen.cs`. Modify window.

- [ ] **Step 1: UXML** — empty `#progress-steps` container; `.cas-progress` with a `.cas-progress__fill` child whose `width` is set to 56% in the controller; "56%" label; footer `progress-cancel` + `progress-finish` (dev).
- [ ] **Step 2: Controller** — `Id="progress"`; build steps from `StubData.ProgressSteps`. For `active`, create a `.cas-icon` spinner element and animate it with a scheduler: `spinner.schedule.Execute(() => spinner.style.rotate = new Rotate(Angle.Degrees(_angle += 12))).Every(33);` started in `OnEnter`, and store the `IVisualElementScheduledItem` to `.Pause()` when leaving (track via a field; pause at start of `OnEnter` re-entry to avoid duplicates, or guard with `_spinnerStarted`). Set fill width: `Root.Q("progress-fill").style.width = Length.Percent(StubData.ProgressPercent);`. `progress-cancel`→`GoTo("hub")`; `progress-finish`→`GoTo("done")`.
- [ ] **Step 3:** Register. **Step 4:** Verify (spinner visibly rotates; bar at 56%). **Step 5:** Commit `feat(installer): wizard Installation Progress screen + spinner`.

### Task 9: All Done screen

**Design:** lines 581–607. Centered big green check (`.cas-icon--big-check`, 104px), H2 "All Done!" (22px), sub, three full-width buttons: soft "Open CAS Settings", soft "View Documentation", primary "Close".

**Files:** `Uxml/Done.uxml`, `Screens/DoneScreen.cs`. Modify window.

- [ ] **Step 1: UXML** — names `done-settings`, `done-docs`, `done-close`.
- [ ] **Step 2: Controller** — `Id="done"`; `done-settings`→`GoTo("settings")`, `done-docs`→stub log, `done-close`→ find the host window and `Close()` (store a delegate: the window passes a `System.Action onClose` into the screen, or the screen calls `EditorWindow.GetWindow<InstallerWizardWindow>().Close()`). Use the latter for simplicity: `done-close`→`InstallerWizardWindow.CloseActive()` (add a static helper that focuses+closes the window).
- [ ] **Step 3:** Add `public static void CloseActive()` to the window (`if (HasOpenInstances<InstallerWizardWindow>()) GetWindow<InstallerWizardWindow>().Close();`). Register. **Step 4:** Verify. **Step 5:** Commit `feat(installer): wizard All Done screen`.

### Task 10: Update Installer screen

**Design:** lines 612–668. H2 "Update Installer" + sub. Left: current 1.2.3 / latest 1.2.5 (green). Right: `.cas-icon--download-big`. "What's new" bullet list. Info box (lines 645–660). Footer: "Later" + primary "Update Now".

**Files:** `Uxml/Update.uxml`, `Screens/UpdateScreen.cs`. Modify window.

- [ ] **Step 1: UXML** — versions from `StubData.InstallerVersion`/`LatestVersion` (can be literal in UXML for stub); names `update-later`, `update-now`.
- [ ] **Step 2: Controller** — `Id="update"`; `update-later`→`GoTo("hub")`, `update-now`→stub log.
- [ ] **Step 3:** Register. **Step 4:** Verify. **Step 5:** Commit `feat(installer): wizard Update Installer screen`.

### Task 11: CAS Settings Redirect screen

**Design:** lines 673–746. Breadcrumb "CAS Settings › Redirect". H2 "CAS SDK Settings" + sub. Soft full-width "Open CAS Settings Window" (gear + external icon) next to a white CAS logo card. "Here you can configure:" bullet list. Tip box (lines 730–743).

**Files:** `Uxml/SettingsRedirect.uxml`, `Screens/SettingsRedirectScreen.cs`. Modify window.

- [ ] **Step 1: UXML** — names `settings-open`, breadcrumb back element `settings-back`.
- [ ] **Step 2: Controller** — `Id="settings"`; `settings-open`→stub log, `settings-back`→`GoTo("hub")`.
- [ ] **Step 3:** Register. **Step 4:** Verify. **Step 5:** Commit `feat(installer): wizard CAS Settings Redirect screen`.

---

## Task 12: Navigation pass + optional fade transition

**Files:** Modify `Editor/Wizard/WizardRouter.cs`, possibly `theme.uss`.

- [ ] **Step 1: Walk the full graph (human + agent)** — from `welcome`, click through every transition in the navigation-graph table; confirm each lands on the right screen and `Back()` (if a Back affordance exists) and the dev picker still work. Fix any mis-wired button name.
- [ ] **Step 2: Optional fade** — in `WizardRouter.Show`, after setting `display:Flex`, set `screen.Root.style.opacity = 0` then `screen.Root.experimental.animation.Start(0f, 1f, 120, (e, v) => e.style.opacity = v);`. Keep it subtle; remove if it feels laggy.
- [ ] **Step 3: Commit** — `feat(installer): wizard navigation pass + screen fade`.

---

## Task 13: Finish

- [ ] **Step 1: Final human walkthrough** — open the window fresh, walk all 8 screens via both the natural buttons and the dev picker. Confirm: fixed 480×560, Inter font, no console errors, all icons present (or documented placeholders), all transitions correct.
- [ ] **Step 2: Confirm the IMGUI window is untouched** — `git diff main -- Editor/Ui/` is empty. Open the old **PSV Game Studio → Installer** and confirm it still works.
- [ ] **Step 3: Stage all generated `.meta`** and commit anything outstanding.
- [ ] **Step 4:** Use **superpowers:finishing-a-development-branch** to merge `feat/installer-wizard-ui` → `main` (or open a PR), per the owner's choice.

---

## Self-Review (architect)

**Spec coverage** — all 8 design screens have a task (4–11); chrome/title bar (Task 1); theme + primitives (Task 2); icons + logos (Task 3); navigation system (graph + Task 12); stubs only / parallel window / fixed size / no IMGUI changes (locked decisions + Task 13 Step 2). The owner's iteration-1 definition ("дизайн + система переходів + чисті заглушки, поточне вікно лишити") is fully covered.

**Placeholder scan** — no "TBD/handle errors later". The one conditional is the icon rasterizer fallback (Task 3 Step 1), which is an explicit, documented branch, not a vague placeholder.

**Type consistency** — `IWizardScreen` (`Id`, `Root`, `OnEnter(WizardRouter)`) is used uniformly by all eight controllers; `WizardRouter.GoTo/Back/Preview/Register/ScreenIds` and `WizardAssets.LoadTree/LoadStyle` names are stable across tasks; screen ids (`welcome/integration/components/hub/progress/done/update/settings`) match the navigation graph and the `GoTo` calls; `StubData` member names (`Components`, `ProgressSteps`, `InstallerVersion`, `LatestVersion`, `ProgressPercent`) match their consumers (Tasks 6, 8, 10).

**Known risks called out:** (a) Inter download may fail offline → editor font fallback; (b) no SVG rasterizer → built-in icon placeholders + owner note (do not add a published dependency unsanctioned); (c) gradients/keyframes unsupported in 2022.3 USS → solid colors + C# spinner tick.
