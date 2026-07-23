# Installer — git infra self-hosting (sub-project C, design)

- **Date:** 2026-06-10
- **Status:** Approved (brainstorming), ready for implementation plan
- **Branch:** `feat/installer-wizard-ui` (continues sub-projects A + B work)
- **Builds on:** sub-project A (git install method for components). This closes the gap A left:
  the metadata package + installer itself still came from the Verdaccio scoped registry, so
  "git mode" was never truly registry-free.

## Motivation

Sub-project A made the heavy SDK *components* (CAS, Tenjin) install via git. But the hub's own
infrastructure — the `installer` package and the `metadata` package (the catalog) — is still pulled
from Verdaccio via a scoped registry that `MetadataAutoInstall` writes into `manifest.json`. So a
client who wants **only git** still ends up with the `com.psvgamestudio` scoped registry in their
manifest.

This sub-project lets the installer self-host its infrastructure over git, so a fully git client
has **zero scoped registries**: installer (git) → metadata (git) → components (git).

## The chicken-and-egg the design resolves

The Welcome method selector chooses how *components* install — but it runs AFTER the metadata
catalog is already loaded (the catalog is what tells the installer the components' git URLs). So the
metadata package must be fetched BEFORE any user choice. The resolution: **the installer mirrors its
own source.** It asks Unity how *it* was installed and fetches metadata the same way. The infra
method is decided by how the client installed the installer, not by the Welcome selector.

## Locked decisions

1. **Self-detect via `PackageInfo.source`.** The installer reads its own `PackageInfo`
   (`FindForAssembly`) — `PackageSource.Git` means the client entered via git, so metadata is fetched
   via git too. Anything else (`Registry`/`Embedded`/`Local`) → the existing registry path.
2. **Git entry = a plain git URL in Package Manager** — no bootstrap package. The client adds
   `https://github.com/CAS-Publishing/installer.git#<version>` via Unity PM → Add package from git URL.
   (npm needs a `.unitypackage` bootstrap only to set up the scoped registry; git needs nothing.)
3. **metadata git URL = `main`, not a pinned tag.** Metadata exists to ship new packages/rules
   WITHOUT an installer release, so its git URL has no tag and tracks `main` (always the latest
   mirror release), behaving like the registry `latest`. The installer git URL the client pins to a
   tag (chosen hub version); metadata floats.
4. **The metadata git URL is a constant in the installer** (`CAS-Publishing/installer-metadata.git`).
   The installer knows its own org/namespace; this needs no catalog (the catalog lives inside metadata).
5. **Infra method and component method are independent.** A git client lands on the Git option in the
   Welcome selector by default (it came in via git), but can still pick UPM for components — the
   component method stays a separate choice.
6. **Self-update in git mode is deferred** — see Out of scope.

## Data flow (full git client)

```
Client: PM → Add from git URL → CAS-Publishing/installer.git#<ver>
   ↓ InitializeOnLoad (Bootstrap)
Installer: InstallerSource.IsGit()  (PackageInfo.source == Git) → true
   ↓
MetadataAutoInstall (git branch): Client.Add("…/installer-metadata.git")
   — NO EnsureScopedRegistry, NO Verdaccio version probe
   ↓ domain reload
CatalogLoader.Load() OK → wizard opens, Welcome defaults to Git → components install via git (A)
```
Result manifest: installer (git URL), metadata (git URL), components (git URLs). Zero scoped registry.

## Components & changes

| Area | Change |
|---|---|
| `Editor/Common/InstallerSource.cs` (create) | `static bool IsGit()` → `PackageInfo.FindForAssembly(typeof(InstallerSource).Assembly)?.source == PackageSource.Git`. Null/exception → false (registry path). |
| `Editor/MetadataAutoInstall.cs` (modify) | Branch on `InstallerSource.IsGit()`. Git: `Client.Add(MetadataGitUrl)` directly (no `EnsureScopedRegistry`, no `CheckRemoteLatestVersion`). Registry: unchanged. |
| metadata git URL constant | A `const string` (e.g. on `CatalogLoader` or a new `InstallerGit` holder): `https://github.com/CAS-Publishing/installer-metadata.git`. |
| `Editor/Catalog/CatalogUpdater.cs` (modify) | "Make metadata current" in git mode = re-issue `Client.Add(MetadataGitUrl)` (Unity re-clones `main`); skip the Verdaccio version probe. The registry path is unchanged. |
| `Editor/Wizard/Screens/AboutScreen.cs` (modify) | In git mode, replace the self-update button action with a short instruction (current version + "update via the git URL with a newer tag"). No auto self-update in git mode. |

`Bootstrap.EnsureMetadata` already routes through `MetadataAutoInstall.Run` / `MaybeAutoUpdate`, so
the branch lives inside those — `Bootstrap` itself needs no change beyond what it already calls.

## Edge cases

- **Dev project** (installer embedded) → `PackageSource.Embedded` → registry path. C changes nothing
  for the dev/authoring project.
- **Offline / git URL unreachable** → `Client.Add` fails → same handling as a failed registry add
  (logged, retried after the next domain reload via the existing throttle).
- **Private mirrors during testing:** git entry of a PRIVATE repo needs the user's git credentials
  (works for the owner via `gh auth setup-git`). For external clients the mirrors must be **public**
  (a release-time `-Visibility public` flip). This is a prerequisite for shipping C externally, not a
  code concern.

## Testing

- `InstallerSource.IsGit()` is a thin wrapper over a Unity API — not unit-testable in isolation;
  covered by the owner's in-Unity verification.
- Primary verification is integration, in Unity, by the owner:
  1. **Registry path unchanged:** in the dev/authoring project (embedded) and a fresh registry client,
     metadata still installs via Verdaccio + scoped registry exactly as before.
  2. **Git path:** in a fresh project, add `installer.git#<ver>` via PM → installer fetches metadata
     via git (no `com.psvgamestudio` scoped registry appears in manifest) → wizard opens → components
     install via git → manifest has zero scoped registries.

## Out of scope

- **Self-update in git mode** (About tab) — shows a manual git-tag instruction instead of an
  automatic update. A real git self-update (resolving the newest tag) is a later refinement.
- **Sub-project B** (Firebase git chain) — unchanged; Firebase still UPM-fallback.
- **git-bootstrap `.unitypackage`** — explicitly not built; the plain git URL is the entry point.
