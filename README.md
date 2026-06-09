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

There are two supported ways in. Pick **A** for any project that already uses UPM; pick **B** for
legacy projects you want to onboard with zero manual steps.

### A. Via UPM (scoped registry)

This is the normal path. You add the PSV registry once, then add the package by name.

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

   Leave the version field empty to get the latest. Unity resolves the installer and its
   dependencies (Newtonsoft Json); the installer then pulls its metadata catalog and opens the
   wizard automatically on first run.

> You only need the `com.psvgamestudio` scope to install the installer itself. The extra scopes for
> CAS / Tenjin / Firebase (`com.cleversolutions`, `com.tenjin`, `com.google`) are added **for you**
> by the installer when you install those components — you don't have to register them manually.

#### Equivalent manual `manifest.json`

If you prefer editing the manifest directly, the registry + dependency look like this:

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
    "com.psvgamestudio.installer": "0.0.1-preview.15"
  }
}
```

### B. Via the bootstrap `.unitypackage` (zero-config)

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

Open it any time from the menu:

```
PSV Game Studio → Wizard
```

The window is a tabbed wizard:

| Tab | What it does |
|---|---|
| **Welcome** | Choose **Express** (install the recommended PSV/CAS stack in one go) or **Manual** (pick components yourself). Enter your **CAS App IDs** (Android / iOS) — they're prefilled from the project's bundle identifier and written into the CAS settings automatically once CAS is installed. |
| **Components** | The live catalog of PSV / CAS packages with per-row state (installed / update available / missing). Install, update, or refresh from here. |
| **Configuration** | Per-platform readiness for the components you've installed, with inline actions to fix anything that isn't set up. |
| **About** | Shows the installed installer version, checks the registry for a newer one, and **self-updates** in place. A red dot on the **About** tab means an update is available (checked once per session). |

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