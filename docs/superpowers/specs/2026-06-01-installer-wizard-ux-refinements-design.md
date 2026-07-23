# Installer Wizard — UX refinements (iteration 2)

**Date:** 2026-06-01
**Branch:** `feat/installer-wizard-ui`
**Status:** approved (design), pending implementation plan

Owner feedback on the CAS Hub Installer wizard produced four refinements. This spec
captures the agreed scope. Verification is **visual, in Unity, by the owner** — the
agent cannot run the Editor.

## Background (current code state, verified)

- `Welcome.uxml` / `WelcomeScreen.cs` — first screen. Has an "Installation method" radio
  group (Git URL / UPM / Unitypackage; Git + Unitypackage disabled, UPM-only), a Git URL
  field, and a stub "Load Information" button. `Next` → `integration`.
- `IntegrationMode.uxml` / `IntegrationModeScreen.cs` — second screen. Express
  ("Make everything for me") vs Manual ("I will do it myself"); manual shows a warning.
  `Continue` sets `IntroDone=true`, then Express → `AutoInstaller.StartAll`, Manual → `components`.
- `ComponentsScreen.cs` — components table. Has an "Auto Init" column that is a **disabled,
  cosmetic checkbox** with no backing logic.
- `SetupChecker.cs` + catalog `config` — CAS `managerIds` is checked **after install** in the
  Configuration tab, reading `Assets/CleverAdsSolutions/Resources/CASSettingsAndroid.asset`
  and `CASSettingsiOS.asset` (`field: managerIds`, `placeholder: "demo"`). The settings asset
  only exists **after** the CAS package is installed.
- `Bootstrap.cs` (core) — on editor init, scans and calls `ShowInstaller(report)` =
  `InstallerWizardWindow.ShowIfReportChanged`, which **auto-opens the window whenever the
  scan-report hash changes** (i.e. on every package-state change, not just first run).
- `AboutScreen.cs` — installer self-update. Latest version is checked **only when the About
  tab is opened**; no background check, no badge.

## Scope

### 1. First screen — merge Welcome + Integration into one screen

Remove the installation-method picker entirely (the installer itself was already obtained via
Git/UPM/.unitypackage — there is no reason to ask how to install its *components*). Delete the
Git URL field and the "Load Information" stub. Delete the separate Integration screen
(`IntegrationMode.uxml` + `IntegrationModeScreen.cs`); fold its Express/Manual choice and the
manual-mode warning into `WelcomeScreen`.

New first screen layout:

```
┌──────────────────────────────────┐
│        CAS Hub Installer          │
│   Install & configure CAS Hub     │
│                                   │
│   CAS ID — Android [____________] │   ← managerIds (CASSettingsAndroid)
│   CAS ID — iOS     [____________] │   ← managerIds (CASSettingsiOS)
│                                   │
│  ┌─────────────────────────────┐ │
│  │ 🤖 Express install           │ │  green, recommended
│  │ Install & configure all      │ │
│  └─────────────────────────────┘ │
│  ┌─────────────────────────────┐ │
│  │ ⚙ Manual selection           │ │  blue when picked + manual warning
│  │ Pick components yourself     │ │
│  └─────────────────────────────┘ │
│  v0.0.1            [Continue]     │
└──────────────────────────────────┘
```

Behavior:
- Clicking a card selects the mode (Express default, green; Manual blue + warning).
- `Continue` sets `IntroDone = true`, then:
  - Express → `AutoInstaller.StartAll(router)`
  - Manual → `router.GoTo("components")`
- The screen keeps id `welcome` (entry screen). The `integration` screen is removed from
  `ScreenOrder`, `RegisterScreens`, and any `GoTo("integration")` / back-targets.

### 2. CAS ID — two platform fields, prefilled, applied after install

- Two fields = `managerIds` for `CASSettingsAndroid.asset` / `CASSettingsiOS.asset`.
- **Prefill** each field from the project's application bundle identifier:
  `PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android)` and
  `...(BuildTargetGroup.iOS)`. In ~99% of cases the CAS ID equals the app bundle id; the
  fields stay editable for the exceptions.
- Entered values are stored per-project in `EditorPrefs`, in a **generic key store** keyed by
  `<componentId>.<platform>` (e.g. `com.cleversolutions.ads.unity.Android`) so a future Gadsme
  SDK key can reuse the same mechanism without rework. Gadsme itself is **not** implemented now.
- New `CasIdApplier`: once CAS is installed and the platform settings asset exists, writes the
  stored value into `managerIds` (via `SerializedObject`) when the field is empty or `"demo"`.
  Runs automatically after install — both in the Express flow and when CAS is installed via a
  row button in Manual mode. The Configuration tab then reports "Configured".
- CAS ID fields are **optional** — empty fields do not block Express. If left empty, the
  Configuration tab continues to flag `managerIds` as "not set" (existing behavior). (Owner
  approved optional as the default.)

### 3. CAS Auto-Init — defer entirely

Remove the non-functional "Auto Init" column from `ComponentsScreen`. Revisit when ad-format
sync with 1C exists. No networks/formats activation in this iteration.

### 4. Installer update — badge, not auto-popup

- **Auto-open the window only on first run** of a project (`!IntroDone`). After the user has
  passed the first screen, the window **never auto-pops** again. Concretely:
  `ShowIfReportChanged` (the `Bootstrap.ShowInstaller` hook) returns early when `IntroDone` is
  true, so the existing "open on every scan-report change" behavior stops for returning users.
- **Update badge**: when the window opens, do a background latest-version check
  (`CatalogUpdater.CheckLatestVersion`) at most once per session (throttled via `SessionState`).
  If a newer installer version exists, show a dot badge on the **About** tab. The existing
  in-window About banner + Update button remain.

## Out of scope (explicitly unchanged)

- Metadata self-heal / `Bootstrap.EnsureMetadata` — unchanged.
- Gadsme as a package — only the generic key store is laid down; no Gadsme logic.
- Async Apply, other tabs (Components/Configuration internals), self-update mechanics — unchanged.

## Files touched (anticipated)

- `Editor/Wizard/Uxml/Welcome.uxml` — rewrite (drop picker/giturl, add two CAS ID fields + cards).
- `Editor/Wizard/Screens/WelcomeScreen.cs` — fold in Express/Manual + warning; remove method radios.
- `Editor/Wizard/Uxml/IntegrationMode.uxml`, `Editor/Wizard/Screens/IntegrationModeScreen.cs` — delete.
- `Editor/Wizard/InstallerWizardWindow.cs` — drop `integration` from `ScreenOrder`/registration;
  About-tab badge; background update check throttle.
- `Editor/Wizard/Screens/ComponentsScreen.cs` — remove Auto Init column.
- `Editor/Wizard/Screens/AboutScreen.cs` — expose update-available state for the badge (or share via a small helper).
- `Editor/Wizard/CasIdApplier.cs` (new) + a generic EditorPrefs key store (new or in an existing helper).
- `Editor/Wizard/AutoInstaller.cs` / `ProgressScreen.cs` — trigger `CasIdApplier` after CAS resolves.
- `Editor/Wizard/Uss/theme.uss` — badge dot style; first-screen field styles if needed.

## Verification

Owner opens the wizard in Unity and confirms: first screen shows two prefilled CAS ID fields +
Express/Manual cards (no method picker); Express writes managerIds into CAS settings after
install; Components has no Auto Init column; window does not auto-pop for a returning project;
About shows an update badge when a newer installer version is published.
