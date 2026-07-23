# PSV Game Studio Installer

One-click Unity installer for the **PSV / CAS publishing SDK**. It scans your project, migrates
legacy installs to UPM, registers the required scoped registries, installs and updates the PSV
packages (CAS, Tenjin, Firebase Analytics, …) and keeps everything up to date — **without you ever
editing `Packages/manifest.json` by hand**.

- **Editor-only.** Nothing ships in your player build.
- **Unity 2022.3+.**
- Driven by a separate data package, `com.psvgamestudio.installer.metadata`, so new packages and
  migration rules reach you in the background without an installer update.

---

## Installing

Three supported ways in:

- **A — Via UPM (npm scoped registry).** The normal path for projects already on UPM. Two
  interchangeable routes: the Unity UI (A1) or editing `manifest.json` (A2).
- **B — Via Git URL.** No registry to register; Unity pulls the package straight from the public
  GitHub mirror. Good for a quick try or CI.
- **C — Via the bootstrap `.unitypackage`.** Zero-config onboarding for legacy clients.

Whichever you pick, on first run the installer pulls its metadata catalog and opens the wizard
automatically — you never edit `manifest.json` for the SDK components themselves.

> You only ever register the `com.psvgamestudio` scope (route A) to get the installer itself. The
> extra scopes for CAS / Tenjin / Firebase (`com.cleversolutions`, `com.tenjin`, `com.google`) are
> added **for you** by the installer when you install those components.

### A. Via UPM (npm scoped registry)

#### A1 — Unity UI

1. **Add the scoped registry.** `Edit → Project Settings → Package Manager → Scoped Registries → +`

   | Field | Value |
   |---|---|
   | Name | `PSV Game Studio` |
   | URL | `https://npm.psvgamestudio.com/` |
   | Scope(s) | `com.psvgamestudio` |

   Click **Save / Apply**.

2. **Add the package.** `Window → Package Manager → +  → Add package by name…`

   ```
   com.psvgamestudio.installer
   ```

   Leave the version field empty to get the latest (or type a version, e.g. `0.0.1-preview.39`).

#### A2 — Edit `manifest.json`

Add the registry and the dependency to `Packages/manifest.json` directly:

```jsonc
{
  "scopedRegistries": [
    {
      "name": "PSV Game Studio",
      "url": "https://npm.psvgamestudio.com/",
      "scopes": [ "com.psvgamestudio" ]
    }
  ],
  "dependencies": {
    "com.psvgamestudio.installer": "0.0.1-preview.39"
  }
}
```

Save the file — Unity resolves the installer (and its Newtonsoft Json dependency) on focus.

### B. Via Git URL

No scoped registry needed — the package is mirrored to a public GitHub repo. Two interchangeable
routes again:

#### B1 — Unity UI

`Window → Package Manager → +  → Add package from git URL…`, then paste:

```
https://github.com/CAS-Publishing/installer.git
```

Omit the suffix to track the mirror's default branch (the latest release), or pin a specific
release with `#<version>`:

```
https://github.com/CAS-Publishing/installer.git#0.0.1-preview.39
```

#### B2 — Edit `manifest.json`

Add a git dependency (no `scopedRegistries` entry required):

```jsonc
{
  "dependencies": {
    "com.psvgamestudio.installer": "https://github.com/CAS-Publishing/installer.git#0.0.1-preview.39"
  }
}
```

> Git installs land in `Library/PackageCache` and are **not** auto-updated by the **About** tab's
> self-update (that path is UPM-only). To move to a newer release, change the `#<version>` tag.

### C. Via the bootstrap `.unitypackage` (zero-config)

For legacy clients (or anyone you don't want to walk through registry setup), distribute the tiny
**bootstrap `.unitypackage`**. See [Distributing via the bootstrapper](#distributing-via-the-bootstrapper)
below for what it does and how it's built.

1. `Assets → Import Package → Custom Package…` and pick `PSVInstaller-Bootstrap.unitypackage`.
2. On import it writes the scoped registry **and** the installer dependency into `manifest.json`,
   then forces a UPM resolve. The installer takes over, installs its metadata, and opens the wizard.
3. Once the installer + metadata are in place, the bootstrap **deletes itself** — nothing is left
   behind in the consumer project.

---

## Using the installer

The window is branded **CAS.AI Publishing Hub**. Open it any time from the menu:

```
CAS Hub → Open Hub
```

(or `Assets → CleverAdsSolutions → Hub`; `Assets → CleverAdsSolutions → Hub (Restart Intro)` clears
the per-project first-run flag and restarts the intro flow below.)

### First run

A brand-new project walks through a short intro flow, then never returns to it:

1. **Ready** — a component picker (CAS SDK, Tenjin SDK, Firebase Analytics, EDM4U) with live
   install state and an **Install** button that installs everything not already present.
2. **Progress** — installs the components one at a time, surviving the domain reloads Unity
   triggers between package installs.
3. **Configuration** — see below.
4. **Done** — a checklist confirming what's ready, with links back into **Components** or to
   `https://cas.ai`.

### Everyday tabs

Once the intro is done, the window opens on three tabs:

| Tab | What it does |
|---|---|
| **Components** | **Main components** (CAS SDK, Tenjin SDK, Firebase Analytics, EDM4U) plus an **Additional components** section listing catalog packages — including the `PSV Analytics (Firebase)` and `PSV Remote Config (Firebase)` adapters (shown only once `com.psv.core` is present) and `PSV Tenjin (Attribution)`. Each row's action is **Install** / **Update** / **Connect to Hub** (migrate a legacy, manual, or git install to UPM) / **Fix** (e.g. a missing scoped registry) / **Remove SDK**. A legacy `com.psv.firebase.base` install is offered a compound migration straight to native Firebase + its adapters, from either the Firebase row or an adapter row. Statuses refresh automatically after every action, or on demand via **Refresh**. |
| **Configuration** | A read-only, per-platform (Android / iOS) readiness table for the components you've installed. Cells are clickable: CAS and Tenjin cells open **CAS.AI's own native settings window** (CAS ID, ad formats, mediation networks, and the Tenjin key all live there now — the installer no longer writes CAS settings itself), Firebase cells ping the existing config file or open a locate dialog when it's missing. Action panels below the table surface anything that still needs attention, and an Android build-templates banner offers a one-click **Enable** when gradle/manifest templates are missing. **Continue** unlocks once at least one platform is fully configured — most projects only ship on one. |
| **About** | Shows the installed installer version, checks the registry for a newer one, and **self-updates** in place (UPM installs only — a git install updates by changing the `#<version>` tag in `manifest.json`). A red dot on the **About** tab means an update is available (checked once per session). |

### Updating the installer

The **About** tab checks `https://npm.psvgamestudio.com/` for the highest published installer
version. If a newer one exists, an **Update** button appears — clicking it installs the new version
via UPM and triggers a domain reload into it. The update badge clears automatically afterwards.

The metadata catalog (`com.psvgamestudio.installer.metadata`) updates **silently in the background**
on Editor load, so new packages and migration rules show up without any action from you.

---

## Distributing via the bootstrapper

The installer is delivered to fresh/legacy projects as a small **bootstrap `.unitypackage`** rather
than the full package. The bootstrap carries no UPM dependencies (no Newtonsoft), so it imports
cleanly anywhere; it just wires up the registry and pulls the real installer from Verdaccio.

**What the bootstrap does on import** (`PSVInstallerBootstrap`):

1. If the installer isn't present, it writes the `PSV Game Studio` scoped registry + the
   `com.psvgamestudio.installer` dependency into `Packages/manifest.json` (backing the file up to
   `manifest.json.bak` first) and calls `Client.Resolve()`.
2. It pins the exact installer version from a sibling `version.txt` (falls back to the registry's
   latest if absent).
3. Once the installer **and** its metadata package are both resolved (i.e. the first run finished),
   the bootstrap removes its own `Assets/PSVInstallerBootstrap` folder. It never deletes itself in
   the authoring project (guarded by the `PSV_INSTALLER_DEV` define).

**Building a new bootstrap** (authoring project only — `dev/`):

1. Make sure the embedded `com.psvgamestudio.installer` is at the version you want clients to get.
2. Run the menu **`PSV Game Studio → Build Bootstrap .unitypackage`**. It pins the installed
   installer version into `Assets/PSVInstallerBootstrap/version.txt` and exports the folder as a
   `.unitypackage`.
3. Ship the resulting `PSVInstaller-Bootstrap.unitypackage`.

---

## Architecture in one paragraph

The installer never hardcodes which packages exist. It depends on the sibling data package
`com.psvgamestudio.installer.metadata`, which carries `catalog.json` — the full list of PSV
packages, their legacy npm ids, the legacy `Assets/` paths to clean up, and the registries (PSV
Verdaccio + OpenUPM for CAS). On Editor load the installer checks Verdaccio for a newer metadata
version and updates that dependency in the background, so new migration rules can ship without a new
installer release.

---

## License

See [`LICENSE.md`](LICENSE.md).
```