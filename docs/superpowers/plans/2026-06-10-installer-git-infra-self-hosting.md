# Git Infra Self-Hosting (sub-project C) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let a git-installed installer fetch its metadata catalog via git (no scoped registry), so a fully-git client has zero `com.psvgamestudio` scoped registry in their manifest.

**Architecture:** The installer reads its own `PackageInfo.source` — if it was installed via git, it fetches metadata via a git URL (`CAS-Publishing/installer-metadata.git`, untagged = `main`) instead of Verdaccio + scoped registry. The registry path is untouched. The Welcome component-method selector is independent of this infra decision.

**Tech Stack:** Unity 2022.3 C# (Editor), UnityEditor.PackageManager (`PackageInfo`, `Client.Add`).

> **Running/verifying:** C is almost entirely Unity-API glue (`PackageInfo.source`, `Client.Add`) — there is little pure logic to unit-test. Verification is INTEGRATION, in Unity, by the owner (no CLI runner). A subagent writes the code but cannot run Unity. Treat "verify" steps as owner-run in Unity.

> **Prerequisite for external git clients (owner, release-time):** the `installer` and `installer-metadata` mirrors must be **public** for a client to `git`-add them without credentials. During testing they're private (works for the owner via `gh auth setup-git`). Flip with `gh repo edit CAS-Publishing/<repo> --visibility public --accept-visibility-change-consequences` when shipping.

---

## File structure

| File | Responsibility |
|---|---|
| `Editor/Common/InstallerSource.cs` (create) | `IsGit()` — was the installer installed via git? |
| `Editor/Catalog/CatalogUpdater.cs` (modify) | `MetadataGitUrl` constant + `InstallGit()` (Client.Add the metadata git URL). |
| `Editor/MetadataAutoInstall.cs` (modify) | Git branch: fetch metadata via git, skip scoped-registry + Verdaccio probe. |
| `Editor/Bootstrap.cs` (modify) | `MaybeAutoUpdate` git branch: re-add the metadata git URL (no Verdaccio version probe). |
| `Editor/Wizard/Screens/AboutScreen.cs` (modify) | Git mode: show a manual git-update instruction instead of the Verdaccio check/auto-update. |

---

## Task 1: InstallerSource.IsGit()

**Files:**
- Create: `Editor/Common/InstallerSource.cs`

- [ ] **Step 1: Create the helper**

Create `Editor/Common/InstallerSource.cs`:

```csharp
using UnityEditor.PackageManager;

namespace PSV.Installer.Common
{
    /// <summary>
    /// How the installer package itself was installed in this project. Used to decide how to fetch
    /// the metadata catalog (git-installed installer → fetch metadata via git, no scoped registry).
    /// </summary>
    public static class InstallerSource
    {
        /// <summary>
        /// True when the installer package was added via a git URL (PackageSource.Git). Anything else
        /// — registry, embedded (dev project), local — returns false → the registry path is used.
        /// Never throws.
        /// </summary>
        public static bool IsGit()
        {
            try
            {
                var info = PackageInfo.FindForAssembly(typeof(InstallerSource).Assembly);
                return info != null && info.source == PackageSource.Git;
            }
            catch
            {
                return false; // can't tell → safe default is the registry path
            }
        }
    }
}
```

- [ ] **Step 2: Commit**

```bash
git add Editor/Common/InstallerSource.cs
git commit -m "feat(installer): InstallerSource.IsGit() self-source detection"
```

---

## Task 2: CatalogUpdater — metadata git URL + InstallGit

**Files:**
- Modify: `Editor/Catalog/CatalogUpdater.cs`

- [ ] **Step 1: Add the constant and the git install method**

In `Editor/Catalog/CatalogUpdater.cs`, add the constant next to `PsvRegistryRoot` (line 14):

```csharp
        /// <summary>
        /// Public git mirror of the metadata catalog. Untagged → Unity resolves the repo's default
        /// branch (main = the latest mirror release), so new packages/rules reach git clients without
        /// an installer release (the same role the registry "latest" plays).
        /// </summary>
        public const string MetadataGitUrl = "https://github.com/CAS-Publishing/installer-metadata.git";
```

Add this method next to `InstallVersion` (line 28):

```csharp
        // Queues a UPM Add for the metadata package via its git mirror (no registry, no version).
        public static AddRequest InstallGit()
        {
            return Client.Add(MetadataGitUrl);
        }
```

- [ ] **Step 2: Commit**

```bash
git add Editor/Catalog/CatalogUpdater.cs
git commit -m "feat(installer): metadata git mirror URL + CatalogUpdater.InstallGit"
```

---

## Task 3: MetadataAutoInstall git branch

**Files:**
- Modify: `Editor/MetadataAutoInstall.cs`

The current `Run` does: `EnsureScopedRegistry()` then `CheckRemoteLatestVersion → TrackInstall(InstallVersion(version))`. In git mode we skip both the scoped registry and the version probe and install straight from git.

- [ ] **Step 1: Add the git branch**

In `Editor/MetadataAutoInstall.cs`, add `using PSV.Installer.Common;` at the top. Then in `Run`, right after the once-per-session guard block (the `if (SessionState.GetBool(InstallAttemptedKey, false)) return;` line) and BEFORE `Debug.Log($"{LogPrefix} metadata package not detected; installing…");`, insert:

```csharp
            // Git-installed installer → fetch metadata over git too (no scoped registry, no Verdaccio
            // probe). Keeps a fully-git client free of any com.psvgamestudio scoped registry.
            if (InstallerSource.IsGit())
            {
                Debug.Log($"{LogPrefix} metadata not detected; installing via git mirror…");
                try
                {
                    // Success: do NOT set the guard — IsMetadataInstalled() short-circuits future calls.
                    CatalogUpdater.TrackInstall(CatalogUpdater.InstallGit(), "Metadata (git)");
                }
                catch (Exception e)
                {
                    SessionState.SetBool(InstallAttemptedKey, true); // failed — throttle until restart
                    Debug.LogWarning($"{LogPrefix} Metadata git Client.Add failed: {e.Message}");
                }
                return;
            }
```

(The existing registry logic below is untouched — it runs only when `IsGit()` is false.)

- [ ] **Step 2: Verify (owner, in Unity)**

Owner: in a project where the installer is git-installed, on a fresh domain reload the console logs "installing via git mirror…" and `manifest.json` gains NO `com.psvgamestudio` scoped registry (metadata appears as a git dependency after resolve). In the dev/embedded project, behaviour is unchanged (registry path).

- [ ] **Step 3: Commit**

```bash
git add Editor/MetadataAutoInstall.cs
git commit -m "feat(installer): fetch metadata via git when installer is git-installed"
```

---

## Task 4: Bootstrap auto-update git branch

**Files:**
- Modify: `Editor/Bootstrap.cs`

`MaybeAutoUpdate` currently skips embedded metadata, then once per session probes Verdaccio for a newer catalog and installs it. In git mode there's no Verdaccio version to probe — "make current" is simply re-adding the metadata git URL (Unity re-resolves `main`).

- [ ] **Step 1: Add the git branch**

In `Editor/Bootstrap.cs`, `MaybeAutoUpdate(string path, PackageCatalog catalog, bool force = false)`: after the `isEmbedded` early-return block and BEFORE the once-per-session probe guard (`if (!force && SessionState.GetBool(UpdateProbedKey, false)) return;`), insert:

```csharp
            // Git-installed installer → metadata comes from the git mirror's main branch. "Make
            // current" = re-add the git URL so Unity re-resolves main; there's no Verdaccio version
            // to probe. Honour the same once-per-session throttle (force bypasses it).
            if (PSV.Installer.Common.InstallerSource.IsGit())
            {
                if (!force && SessionState.GetBool(UpdateProbedKey, false)) return;
                SessionState.SetBool(UpdateProbedKey, true);
                Debug.Log($"{LogPrefix} Re-resolving metadata from git mirror (main).");
                CatalogUpdater.TrackInstall(CatalogUpdater.InstallGit(), "Metadata (git)");
                return;
            }
```

- [ ] **Step 2: Verify (owner, in Unity)**

Owner: in a git-installed project, opening the wizard / hitting Refresh re-resolves metadata from git (console log), without probing Verdaccio. Registry projects: unchanged.

- [ ] **Step 3: Commit**

```bash
git add Editor/Bootstrap.cs
git commit -m "feat(installer): git-mode metadata refresh re-resolves the mirror main"
```

---

## Task 5: About tab — git-mode update instruction

**Files:**
- Modify: `Editor/Wizard/Screens/AboutScreen.cs`

In git mode the installer can't resolve a "latest tag" from Verdaccio, so the About tab shows the current version plus a manual instruction instead of the auto-update flow.

- [ ] **Step 1: Branch the update flow**

In `Editor/Wizard/Screens/AboutScreen.cs`, add `using PSV.Installer.Common;` at the top. Change `CheckForUpdates()` so it short-circuits in git mode — at the very start of the method body, before the existing `_latestVersion = null;` line, insert:

```csharp
            if (InstallerSource.IsGit())
            {
                if (_latest != null)  _latest.text = "git";
                if (_update != null)  _update.style.display = DisplayStyle.None;
                SetStatus("Installed via git. To update, change the installer git URL to a newer " +
                          "tag in Packages/manifest.json (Package Manager → installer → version).", null);
                return;
            }
```

(The existing Verdaccio check + `DoUpdate` path below runs only when `IsGit()` is false.)

- [ ] **Step 2: Verify (owner, in Unity)**

Owner: in a git-installed project, the About tab shows "git" for latest, no Update button, and the manual instruction. In a registry project, the normal check/update flow is unchanged.

- [ ] **Step 3: Commit**

```bash
git add Editor/Wizard/Screens/AboutScreen.cs
git commit -m "feat(installer): About tab shows git-update instruction in git mode"
```

---

## Task 6: End-to-end verification (owner, in Unity)

No code — the integration gate that proves C works. Owner runs in a FRESH scratch project (mirrors must be reachable — public, or the owner's git credentials configured):

- [ ] **Step 1:** Package Manager → Add package from git URL → `https://github.com/CAS-Publishing/installer.git#0.0.1-preview.15`.
- [ ] **Step 2:** Wait for resolve + domain reload. Console logs "installing via git mirror…"; the wizard opens.
- [ ] **Step 3:** Open `Packages/manifest.json`. Expected: `com.psvgamestudio.installer` and `com.psvgamestudio.installer.metadata` are **git URLs**; there is **NO `com.psvgamestudio` scoped registry**.
- [ ] **Step 4:** Welcome defaults to the **Git** method; complete Express. CAS/Tenjin install as git URLs; manifest has **zero scoped registries** (Firebase, if selected, is the only one that would add a `com.google` registry via UPM fallback).
- [ ] **Step 5:** About tab shows the git-update instruction (no auto-update).
- [ ] **Step 6 (regression):** In the dev/authoring project (embedded installer) and a registry-bootstrap client, confirm metadata still installs via Verdaccio + scoped registry exactly as before — C must not change the registry path.

If all pass, sub-project C is complete and "git only" is truly registry-free.

---

## Notes for the implementer

- `IsGit()` is the single decision point — every branch keys off it. Embedded (dev) and registry installs both return false, so the existing behaviour is the default and only a genuinely git-installed installer takes the new paths.
- Do not change the Welcome component-method selector — infra method (this plan) and component method (sub-project A) are deliberately independent.
- The metadata git URL is intentionally untagged (`main`) — do not add a `#tag`.
