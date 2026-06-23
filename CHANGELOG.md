# Changelog

All notable changes to this package will be documented in this file.

## [0.0.1-preview.24] - 2026-06-23

- **Fix: external SDKs installed via `.unitypackage` are detected even when the project doesn't
  compile.** Out-of-UPM detection previously relied solely on reflection over loaded types, which is
  blind when the manual copy fails to compile — exactly the messy projects that most need migrating —
  so e.g. CAS in `Assets/CleverAdsSolutions` read as "not installed" and the hub offered an Install
  that duplicated it (only metadata files landed; the rest conflicted with the existing copy). Added a
  disk fallback: an SDK-identity folder (an `assetRoots` entry whose name matches a marker, so shared
  satellites like EDM/PlayServicesResolver never false-positive) or a signatured scattered file
  anywhere under `Assets/`. The hub now offers Migrate (which removes the manual copy first) instead.
- **Fix: migrating no longer downgrades a newer manual install (version parity).** When the manual
  copy reports a version (via the catalog's `versionType`/`versionField`) NEWER than the catalog pin,
  migration installs THAT exact version from our registry/git — same version, new source — instead of
  the pinned (older) one. The downgrade warning is gone (migration can no longer downgrade). Fixes
  Firebase 13.6.0 being silently downgraded to a 13.1.0 pin.
- Pairs with installer.metadata ≥ 0.0.2-preview.18 (adds Tenjin RUNTIME-script signatures so a Tenjin
  moved to a non-standard path, e.g. `Assets/Scripts/Core/Tenjin`, is removed on migrate).

## [0.0.1-preview.23] - 2026-06-22

- **Fix: migrating a manual (.unitypackage) SDK now removes files it scattered outside its owned
  folders.** New catalog field `legacyAssetFiles` (file name + content markers) lets the migrator
  find SDK files dropped into shared folders (e.g. Tenjin's `BuildPostProcessor.cs` /
  `Dependencies.xml` in `Assets/Editor`) anywhere under `Assets/`, matched by name **and** content
  so a user's same-named file is never touched, and delete them with confirmation. Previously a
  leftover `BuildPostProcessor` re-added a stale `[PostProcessBuild]` and broke the iOS build.
  (Requires metadata that declares `legacyAssetFiles` — ships in installer.metadata ≥ 0.0.2-preview.17.)

## [0.0.1-preview.22] - 2026-06-22

- **Docs:** README now documents all install routes — UPM scoped registry (Unity UI or manual
  `manifest.json`), Git URL (Unity UI or manual), and the bootstrap `.unitypackage`. No code change.

## [0.0.1-preview.21] - 2026-06-22

- **Fix: wizard "Remove" now works for packages installed under a legacy id.** The Remove button
  passed the canonical catalog id, but a package present under a legacy id (e.g. Tenjin as the git
  package `com.psv.tenjin`, or a `LegacyUpm` PSV package) lives under that id in `manifest.json` —
  so removal silently no-op'd and the button appeared dead. `ComponentStatus` now carries the
  actually-installed id and Remove targets it.

## [0.0.1-preview.16] - 2026-06-10

A large release: Welcome UX refinements, out-of-UPM detection, and two new git capabilities.

- **Git install method.** A global **UPM / Git** selector on the first screen. In Git mode the
  installer writes git-URL dependencies (clean, no scoped registry) for components that have a git
  source in the catalog — CAS via its official git, Tenjin via our mirror. Components without a git
  source (Firebase, unless its chain is mirrored) fall back to UPM. Detection treats a git-URL
  dependency as "Installed (git)".
- **Git infrastructure self-hosting.** When the installer itself is installed via a git URL, it
  fetches the metadata catalog via git too (no scoped registry), so a fully-git client ends up with
  zero `com.psvgamestudio` scoped registries. The infra source mirrors how the installer was
  installed; the component method stays a separate choice. About-tab self-update shows a manual
  git instruction in git mode.
- **Out-of-UPM (.unitypackage) detection.** SDKs installed manually via .unitypackage are now
  detected (by loaded-type namespaces — covers asmdef, DLL, and raw scripts) and shown as
  "Installed (manual)" instead of being offered for a duplicate Install. A **Migrate to UPM** action
  removes the manual copy (git-guarded) and installs the UPM version. Prevents the duplicate-package
  breakage during auto-integration.
- **Welcome screen** now uses a single CAS-ID field with Android/iOS tabs (many clients ship one
  platform), empty by default (or prefilled from an existing CAS install), with **Next locked** until
  an id is entered. CAS IDs apply immediately (no need to reopen the wizard).
- **Components: a Remove button** on each installed component — saves manual UPM editing and is a
  recovery path for botched installs.
- **Reliable first-run auto-open** — the wizard no longer fails to open until an editor restart (the
  scan-hash was latched before the window actually opened). New "Wizard (Restart Intro)" menu reopens
  the first screen.

## [0.0.1-preview.15] - 2026-06-02

- **Fixed the About tab caption clipping to "Abou".** The update badge was added as a child of the
  tab button; a Unity `Button` (a `TextElement`) stops sizing to its own `text` once it has a child,
  so the caption was truncated. The caption now lives in a child `Label` and the badge overlays it.
- **Taller "Check for updates" button** on the About tab (dropped `cas-btn--sm` for the default
  button height).
- **Merged the first two wizard screens.** The install-method picker (Git/UPM/.unitypackage) is
  gone — the installer is already obtained that way, so there's no reason to ask again for its
  components. The first screen now shows the Express / Manual choice directly, plus two **CAS ID**
  fields (Android / iOS) prefilled from the project's bundle identifier. The entered IDs are written
  into the CAS settings (`managerIds`) automatically once CAS is installed (Express or manual),
  filling only empty/placeholder slots.
- **Auto-open only on first run.** The wizard no longer pops up on every package-state change for a
  project you've already set up; it opens automatically only the first time. Update availability is
  now surfaced as a dot **badge on the About tab** (checked once per session, cleared right after you
  self-update) instead of an auto-popup.
- **Removed the non-functional Auto-Init column** from Components (deferred until ad-format sync with
  1C exists).

## [0.0.1-preview.14] - 2026-05-27

- **Self-heal: the wizard now ensures the metadata catalog itself.** Opening the wizard and the
  Components **Refresh** button both install the metadata package if it's missing and re-check the
  registry for a newer catalog (`Bootstrap.EnsureMetadata`, bypassing the once-per-session
  throttles). So if the auto-open path is ever interrupted, just open the wizard / hit Refresh — no
  cache deletion or restart needed.
- Removed the noisy per-reload diagnostic logs added in preview.13.

## [0.0.1-preview.13] - 2026-05-27

- **Fix: metadata auto-install no longer permanently skips after an install/remove cycle in the
  same editor session.** The once-per-session guard (whose only purpose is to avoid re-probing the
  registry on every domain reload when offline) was set up front, so even a successful install
  burned the session and a remove+reinstall silently did nothing until an editor restart. It is now
  set only on a genuine failure; success relies on the real package-presence check instead.
- Added diagnostic startup logging (`InitializeOnLoad`, `RunOnce`, `MetadataAutoInstall`) to make
  the boot sequence observable.

## [0.0.1-preview.12] - 2026-05-26

- **Fix: first-time metadata install now picks the newest published version.**
  `CheckRemoteLatestVersion` read `dist-tags.latest`, which Verdaccio doesn't
  advance for prereleases, so a fresh install pulled a stale catalog. It now
  reports the highest published version (same logic as the self-update check).

## [0.0.1-preview.11] - 2026-05-26

- Default components now install the raw **Tenjin SDK** (`com.tenjin.sdk`) instead
  of the `com.psvgamestudio.tenjin` wrapper. Pairs with metadata catalog
  ≥ 0.0.2-preview.8 (Tenjin moved to an external record with the `com.tenjin` scope).

## [0.0.1-preview.10] - 2026-05-26

- New UI Toolkit installer **Wizard** with a tabbed interface: Welcome, Components, Configuration, About.
- **Components** tab — live install/update status and one-click install of CAS SDK, Tenjin, and Firebase Analytics.
- **"Make everything for me"** — installs the default components step by step with live progress.
- **Configuration** tab — per-platform (Android / iOS) readiness checks with quick links to each component's settings.
- **About** tab — shows the installer version, checks the registry for updates, and self-updates.
- Installs resolve without an editor restart; the wizard remembers your place across reloads.

## [0.0.1] - 2026-05-21

- Phase 1 skeleton: package manifest, Editor asmdef, bootstrap hook.
- Declares dependency on `com.psvgamestudio.installer.metadata`.
- No runtime behavior yet.
