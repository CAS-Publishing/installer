# Configuration: active-platform scope + independent network-set checkboxes

**Date:** 2026-06-29
**Branch:** `feat/installer-wizard-ui`
**Status:** design approved, pending spec review

## Problem

The installer's **Configuration** screen (`SetupScreen`) currently:

1. **Shows both platforms at once** — the status grid has `Component | Android | iOS`
   columns and the CAS card renders an Android *and* an iOS sub-panel. This is noise:
   a project is built for one active target at a time, and the rest of the wizard
   (Welcome / CAS-ID) already configures **one platform per pass**, defaulting to the
   active build target. Configuration should follow the same principle.
2. **Models the CAS mediation set as a mutually-exclusive `Optimal | Families`
   toggle.** CAS itself models these as **independent checkboxes** — a project may have
   OptimalAds installed, FamiliesAds installed, both, or neither. Forcing a mutex
   misrepresents CAS's own model and surprised the field tester.

## Goals

- Configuration shows **only the active build target's platform** (whole screen:
  status grid column *and* CAS card), resolved from `PlatformDetect.ActivePlatform()`.
- The `Optimal` / `Families` mediation sets become **two independent checkboxes**,
  each toggling that one CAS solution on/off without touching the other.

## Non-goals

- No manual Android/iOS switch on Configuration (decided: strictly follow the active
  build target; `BuildTargetWatcher` and the Refresh button already rebuild).
- No change to ad-format toggles or audience handling.
- No change to how CAS IDs are captured (still Welcome, one platform per pass).
- Not mirroring CAS's full inspector — only the Optimal/Families checkbox semantics.

## Reference behaviour ("like the CAS ID")

`WelcomeScreen` already configures **one platform per pass**, defaulting to
`PlatformDetect.ActivePlatform()`. Configuration adopts the same single-platform
principle, minus the manual switch.

## Design

### Part 1 — Configuration scoped to the active platform (whole screen)

- **`SetupScreen.Rebuild()`** resolves `var platform = PlatformDetect.ActivePlatform()`
  fresh on every rebuild (so a build-target switch, which rebuilds the wizard, and the
  Refresh button both pick up the current target).
- **`Setup.uxml`** — the static header row `cas-setup-head` currently has two fixed
  labels `Android` and `iOS`. Replace them with a single **named** label
  `setup-th-plat` (keeping the `cas-th cas-setup-col-comp` "Component" label). The
  screen sets `setup-th-plat.text = platform` on rebuild.
- **`BuildRow`** builds only the active platform's status column. A pure helper
  `PickCells(SetupModel.Row row, string platform)` returns `row.IOS` when
  `platform == "iOS"`, else `row.Android`. The 3-column grid becomes
  `Component | <platform>`.
- **`BuildCasConfig`** renders only `PlatformConfig(platform)` instead of both
  `"Android"` and `"iOS"`.
- **`CountAttention`** is summed over the active platform's cells only (so the
  "N items need attention" summary reflects what's shown).
- **USS:** no change. `.cas-setup-row__grid` and `.cas-cfg__plats` are flexbox rows;
  a single platform column / panel (`flex-grow: 1`) fills the available width.

### Part 2 — Independent Optimal / Families checkboxes

In `SetupScreen.PlatformConfig`, replace the mutex segment (the `optBtn` / `famBtn`
buttons plus the `Highlight` / `SetSegActive` logic) with two independent `Toggle`
controls under the existing "Mediation networks" group:

- `Optimal` toggle, initial `value = CasMediation.IsSolutionInstalled(bt, families: false)`
- `Families` toggle, initial `value = CasMediation.IsSolutionInstalled(bt, families: true)`

On value change of either toggle:

```
if (!CasMediation.SetSolution(bt, families, e.newValue))
    toggle.SetValueWithoutNotify(!e.newValue);   // revert: reflection failed, don't lie
```

Keep the existing hint label ("Optimal = full adult network set · Families =
child-directed set").

### Part 3 — `CasMediation` refactor (best-effort reflection, unchanged contract)

Replace the mutex-oriented API with per-solution operations:

- **`bool SetSolution(BuildTarget platform, bool families, bool enable)`** — resolves the
  one solution by `Dependency.name` (`"OptimalAds"` / `"FamiliesAds"`), invokes
  `ActivateDependencies(platform, manager)` when `enable`, else
  `DisableDependencies(platform, manager)`, then calls `RefreshInspector()`. Does **not**
  touch the other solution. Returns `false` (with the existing `[PSV Installer]` warning)
  on any missing CAS type/member; never throws.
- **`bool IsSolutionInstalled(BuildTarget platform, bool families)`** — generalises the
  old `IsFamiliesActive`: finds the named solution and returns its
  `IsInstalled()` (i.e. `installedVersion` present). Returns `false` on any reflection
  failure.
- Remove `SelectSolution` and `IsFamiliesActive` (no remaining callers once `SetupScreen`
  is updated).
- `RefreshInspector()` (`ActiveEditorTracker.sharedTracker.ForceRebuild()`, added
  earlier this session) is retained and called after each `SetSolution`.

**CAS semantics to preserve / accept:**
- `Dependency.ActivateDependencies` starts with `if (locked) return;` — a solution that is
  *locked* (pulled in transitively by an installed adapter) cannot be activated by ticking
  the box. This mirrors CAS's own UI (its toggle is disabled in that case); accept as
  best-effort. We create the manager with `deepInit: false` (as the working code already
  does), so `locked` is not computed on our throwaway manager — activation/deactivation of
  the two top-level solutions is unaffected.
- The checkbox state is `installedVersion`, read from the dependency XML via raw file IO;
  `DisableDependencies` deletes the XML, `ActivateDependencies` writes it.

## Data flow

```
user ticks Optimal/Families toggle
  └─> CasMediation.SetSolution(platform, families, enable)
        ├─ DependencyManager.Create(platform, audience, deepInit:false)   // reflection
        ├─ find solution by name → ActivateDependencies | DisableDependencies
        ├─ AssetDatabase.Refresh()
        └─ RefreshInspector()  // ActiveEditorTracker.ForceRebuild() → CAS inspector re-reads XML
  └─> on false: revert toggle via SetValueWithoutNotify

screen rebuild
  └─> platform = PlatformDetect.ActivePlatform()
        ├─ setup-th-plat.text = platform
        ├─ rows: PickCells(row, platform) → single status column
        └─ CAS card: PlatformConfig(platform) only
              └─ toggle initial values = CasMediation.IsSolutionInstalled(platform, …)
```

## Error handling

- `CasMediation` stays best-effort: any reflection miss logs the existing
  `[PSV Installer] CAS network-set not applied (...)` warning and returns `false`; the
  UI reverts the toggle so it reflects on-disk reality.
- `PickCells` returns an empty/`null` list gracefully (existing `BuildPlatformColumn`
  already renders a `—` muted cell for empty input).

## Testing

- **Unit:** `PickCells(row, platform)` — pure mapping (`"iOS"` → `row.IOS`, else
  `row.Android`), mirroring the existing `PlatformDetectTests` style.
- **Owner-run in Unity (reflection cannot be unit-tested):**
  1. With Android active: Configuration shows only an Android column + Android CAS panel;
     header reads "Android".
  2. Tick `Families` → `CASAndroidFamiliesAdsDependencies.xml` appears; CAS settings
     "Mediation Solutions" shows FamiliesAds checked (live, no reopen). Untick → file
     removed, checkbox clears.
  3. `Optimal` and `Families` operate independently (both can be checked at once).
  4. Switch build target to iOS → Configuration rebuilds to the iOS column + iOS CAS panel.

## Touched files

- `Editor/Wizard/Screens/SetupScreen.cs` — platform scoping, checkboxes, `PickCells`.
- `Editor/Wizard/Setup.uxml` — single `setup-th-plat` header label.
- `Editor/Wizard/CasMediation.cs` — `SetSolution` / `IsSolutionInstalled`; drop
  `SelectSolution` / `IsFamiliesActive`.
- `Editor/Tests/` — `PickCells` unit test (new file + `.meta`).
