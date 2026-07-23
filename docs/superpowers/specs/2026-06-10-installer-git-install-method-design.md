# Installer — Git install method (design)

- **Date:** 2026-06-10
- **Status:** Approved (brainstorming), ready for implementation plan
- **Branch:** `feat/installer-wizard-ui`
- **Scope:** Sub-project A only — git infrastructure + the two self-contained default components
  **CAS** (official git) and **Tenjin** (our CAS-Publishing mirror). Firebase (and our PSV packages
  that depend on Firebase) are sub-project B, a separate later spec.

## Motivation

Client field feedback (#1): clients install SDKs in different ways, and many prefer a **Git URL**
over a scoped UPM registry. The hub currently writes only scoped-registry + version entries to
`Packages/manifest.json`. We add a second, equivalent install path: the user picks a global method
on the first screen, and when **Git** is chosen the installer writes **git-URL dependencies** instead
of registry entries.

This restores the install-method selector that existed on the Welcome screen before the
preview.15 merge (commit `13b1519` dropped it), but reframed around real client reality and our new
GitHub release mirrors.

## Locked decisions

1. **Global method, not per-component.** A single UPM / Git selector on Welcome applies to ALL
   components (Express and per-row alike).
2. **Two options only: UPM and Git.** `.unitypackage` is not a manifest-writing method, so it is not
   a global method here.
3. **Git URL source per component:** use the SDK's **official git repo where one exists** (CAS →
   `cleveradssolutions/CAS-Unity`, Tenjin → `tenjin/tenjin-unity-sdk`); for components without an
   official git repo, use **our own CAS-Publishing mirror** built from the npm tarball (Firebase, and
   our own `com.psvgamestudio.*` packages).
4. **Clean git: no scoped registry in git mode.** Every transitive dependency is also written as a
   git-URL entry, so Unity never falls back to a registry. This is achieved by explicitly listing the
   full dependency chain in the manifest (see Data flow).
5. **Catalog is the source of truth** for git URLs + tags (data-driven), kept separate from UPM
   versions — official git tags differ from registry versions (e.g. Tenjin official git `v1.13.3`
   vs our Verdaccio `1.15.14`).
6. **No URL input field.** Unlike the old Welcome screen, the user does not type a git URL — every
   component's URL comes from the catalog. Less surface for error.
7. **Switching an already-installed component between UPM and git is out of scope** for this spec —
   the states are surfaced but no auto cross-action is taken (avoids duplicates).

## How Unity resolves git dependencies (the mechanism)

A git dependency in `manifest.json` is `id → url#tag`:
```json
"com.tenjin.sdk": "https://github.com/tenjin/tenjin-unity-sdk.git#v1.13.3"
```
Unity clones the repo, reads its `package.json`, and resolves each of ITS `dependencies` **by id**:
first against the entries already in `manifest.dependencies`, then scoped registries, else error.

**Clean git therefore requires every package in the chain (top-level + all transitive) to be present
in `manifest.dependencies` as its own git-URL entry.** Then Unity finds each id directly in the
manifest (already a git URL) and never consults a registry.

- Self-contained component (Tenjin, CAS) → one entry.
- Chained component (Firebase, sub-project B) → one entry per link, e.g.:
  ```json
  "com.google.firebase.analytics": "https://github.com/CAS-Publishing/firebase-analytics.git#13.1.0",
  "com.google.firebase.app":       "https://github.com/CAS-Publishing/firebase-app.git#13.1.0"
  ```

## Catalog schema (data-driven)

`ExternalRecord` and `PackageRecord` gain an optional `git` block holding the **flat chain** of
packages to write for that component in git mode:

```json
"id": "com.cleversolutions.ads.unity",
"git": {
  "packages": [
    { "id": "com.cleversolutions.ads.unity", "url": "https://github.com/cleveradssolutions/CAS-Unity.git", "tag": "4.7.0" }
  ]
}
```

- CAS / Tenjin → a one-entry list (self-contained).
- A component with **no `git` block** (Firebase, until sub-project B) → git mode falls back to UPM
  for that component, with an explicit marker.
- Tags here are the **git tags** (may differ from UPM versions); they are not derived from
  `recommendedVersion`.

## Components & changes (sub-project A)

| Area | Change |
|---|---|
| `Editor/Catalog/Catalog.cs` | `ExternalRecord` + `PackageRecord` += `GitInstall` (block with `Packages: [{Id, Url, Tag}]`). |
| `Editor/Migrator/MigrationAction.cs` | New `AddGitPackage(id, urlWithTag)` action. |
| `Editor/Migrator/ManifestWriter.cs` | Handle `AddGitPackage` → write `"id": "url#tag"` into `dependencies`. |
| `Editor/Migrator/MigrationPlanner.cs` | Aware of the chosen method. Git mode: for a component with a `git` block, emit `AddGitPackage` for each chain entry, **no** `AddScopedRegistry`. No git block → UPM plan + warning. |
| Install-method state | New `InstallMethod` enum (Upm / Git), stored per-project (EditorPrefs, like `IntroDone`). |
| `Editor/Scanner/StateClassifier.cs` | A dependency value containing `://` or `.git` → `Installed (git)`; guard version-compare against URLs. |
| `Editor/Wizard/Uxml/Welcome.uxml` | Restore the vertical method cards (UPM / Git), no URL field. |
| `Editor/Wizard/Screens/WelcomeScreen.cs` | Radio-toggle logic + persist the method. |
| `Editor/Wizard/Screens/ComponentsScreen.cs` | Show "Installed (git)" when the manifest value is a git URL. |
| `Editor/Wizard/ComponentStatusProvider.cs` | Surface the git-installed state. |

The selector reuses existing USS (`cas-method`, `cas-radio`, `cas-radio__dot`, `cas-section-title`,
`cas-method__title/desc`) — still present in `theme.uss`, no restoration needed.

## UI

Welcome screen (CAS-ID capture stays; method added above it):
```
[CAS logo]  CAS Hub Installer

Installation method
  ○ Git URL            Install via Git repository (latest tagged version)
  ● UPM (Recommended)  Install via scoped registry

CAS ID   [Android][iOS]
         [__________________]
                       [ Next ]
```
The chosen method persists per-project and drives every install action (Express + per-row).

## Method × current-state matrix

| Current state \ mode | UPM mode | Git mode |
|---|---|---|
| Not installed | AddPackage (registry + version) | AddGitPackage (chain) |
| Installed via UPM | Up to date / Update | show "UPM-installed" (no auto cross-action) |
| Installed via git | show "git-installed" | Up to date / update to newer tag |

Only the two diagonal cases are wired in sub-project A. Cross cases (UPM↔git switch) are visible but
take no auto action — switching an installed component is deferred (like Remove/Migrate for #4).

## Firebase in git mode (sub-project A behaviour)

Firebase has no `git` block yet (its chain is sub-project B). In git mode Firebase is installed via
**UPM**, shown with an explicit "git unavailable — installed via UPM" marker. It is the only
exception to clean-git until sub-project B lands.

## Edge cases

- Already-installed component (UPM or git) is not duplicated — detection gates it.
- Git tags ≠ UPM versions — tag lives in the catalog `git` block, independent of `recommendedVersion`.
- A broken/unreachable git URL surfaces as a UPM resolve failure (same path as a failed AddPackage);
  no special handling beyond the existing apply-result dialog.

## Testing

- `AddGitPackage` → `ManifestWriter` writes `"id": "url#tag"` (unit).
- `MigrationPlanner` git mode → emits `AddGitPackage` for each chain entry and **zero**
  registry actions (unit).
- `MigrationPlanner` git mode, component without a `git` block → UPM-fallback plan + warning (unit).
- `StateClassifier` → a git-URL dependency classifies as installed-via-git, not a version-compare
  crash (unit).
- Install-method persistence round-trips per project (unit).
- Visual (owner, in Unity): selector renders; choosing Git then Express writes git URLs to manifest;
  Components shows "Installed (git)".

## Decomposition

- **Sub-project A (this spec):** git infrastructure + CAS / Tenjin / our PSV packages. Firebase =
  UPM fallback.
- **Sub-project B (later spec):** Firebase git chain — mirror app + analytics + native `.tgz`,
  wire git deps between them. Overlaps with the #6 EDM4U feedback. Does not block A.

## Resolved (were open during brainstorming)

- **Default-component git sources (final):**
  - CAS → official git `https://github.com/cleveradssolutions/CAS-Unity.git#4.7.0` (self-contained).
  - Tenjin → **our mirror** `https://github.com/CAS-Publishing/tenjin-sdk.git#1.15.14`. Chosen over
    the official git (`v1.13.3`, which lags) for parity with UPM. The `com.tenjin.sdk@1.15.14`
    Verdaccio tarball was verified clean (35 `.meta`, both asmdefs) so the mirror installs correctly.
  - Firebase → no git block → UPM fallback (sub-project B).
- **Prerequisite (catalog population step):** mirror `com.tenjin.sdk@1.15.14` →
  `CAS-Publishing/tenjin-sdk` via `ci/scripts/publish-to-github.ps1` (a NEW repo, distinct from the
  existing `tenjin` repo which holds the `com.psvgamestudio.tenjin` wrapper). The owner runs this
  (publish push is owner-run).
- **PSV packages are out of scope for A:** analytics/crashlytics/remoteconfig/tenjin-wrapper depend on
  Firebase (and `com.psv.core`), so their clean-git chain needs sub-project B. They are not default
  hub components anyway. Self-contained ones (installer, metadata, debug) can be added later if we
  ever offer git-install for PSV packages themselves; not part of A.

## Out of scope

- Firebase git chain (sub-project B).
- Switching an already-installed component between UPM and git.
- `.unitypackage` as an install method.
