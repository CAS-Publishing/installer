# Installer Self-Bootstrap Implementation Plan (Phase 1.5)

> **For agentic workers:** high-level spec. No code dictation below `MetadataAutoInstall.Run` contract. Implementer makes layout, error-style, and ordering decisions.

**Goal:** decouple installer from `installer.metadata` as a UPM dependency. The installer must work after both (a) UPM install and (b) `.unitypackage` import. On first run, if metadata is not present, installer registers our scoped registry and installs the latest metadata via `Client.Add`.

**Architecture:** new `MetadataAutoInstall` orchestrator. Bootstrap forwards to it when `CatalogLoader.Load` returns null. `installer/package.json` drops the `metadata` dependency. Metadata package version bumps to `0.0.1-preview.1` for first Verdaccio publish.

**Tech stack:** unchanged. Use `Newtonsoft.Json.Linq` (`JObject`, `JArray`) for manifest mutation — preserves unknown fields and is already shipped via `com.unity.nuget.newtonsoft-json`. Re-use existing `CatalogUpdater.CheckRemoteLatestVersion` for HTTP query against Verdaccio (it already reads `dist-tags.latest`, which is what user chose for distribution).

---

## Acceptance Criteria

### Self-install flow

When Unity loads the installer assembly and `CatalogLoader.Load` returns null (metadata package not registered in this project):

1. **Scoped registry ensured.** `manifest.json` at `<project>/Packages/manifest.json` must end up with a `scopedRegistries` entry that:
   - Has URL `https://npm.psvgamestudio.com/`
   - Has scope `com.psvgamestudio` (in addition to any pre-existing scopes the user may have)
   - All other manifest fields and registries the user already had — untouched.
   - Idempotent: if such a registry block already exists, do nothing.

2. **Version queried.** Hit `https://npm.psvgamestudio.com/com.psvgamestudio.installer.metadata`, read `dist-tags.latest`. Re-use `CatalogUpdater.CheckRemoteLatestVersion` — it already does this.

3. **Package installed.** Call `Client.Add($"com.psvgamestudio.installer.metadata@{version}")`. Fire-and-forget — UPM resolves it on its own; Unity will reimport and the next `Bootstrap` cycle will see the catalog.

4. **Failure handling.** Any failure (network down, manifest unreadable, Client.Add error) → `Debug.LogWarning` with clear prefix + reason, no exception leak. Operation is idempotent: next Unity load tries again.

### Existing path (metadata already installed)

Untouched — Bootstrap continues to call `CatalogLoader.Load` then `CatalogUpdater.CheckRemoteLatestVersion` as before.

### Public surface

```
namespace PSV.Installer
{
    internal static class MetadataAutoInstall
    {
        public static void Run();
    }
}
```

Everything else (helpers, internal flow, ordering) is the implementer's call.

---

## File Structure (recommended)

- `Editor/MetadataAutoInstall.cs` — new orchestrator.
- `Editor/Bootstrap.cs` — modify: when catalog is null, call `MetadataAutoInstall.Run()` instead of just logging a warning.
- `Packages/com.psvgamestudio.installer/package.json` — modify: remove the `dependencies.com.psvgamestudio.installer.metadata` entry.
- `Packages/com.psvgamestudio.installer.metadata/package.json` — modify: bump `version` from `0.0.1` to `0.0.1-preview.1`.
- `Packages/com.psvgamestudio.installer.metadata/CHANGELOG.md` — modify: add an entry noting the version bump and that this is the first preview release.

If the implementer chooses to extract manifest-mutation into a separate helper file (e.g. `Editor/Manifest/ManifestEditor.cs`), that's fine — `MetadataAutoInstall` should remain the orchestrator.

---

## Constraints

- **No tests this pass.** Same calibration rule as Phase 2.
- **Editor-only.** Lives in the existing `PSV.Installer.Editor` assembly.
- **Re-use, don't duplicate.** `CatalogUpdater.CheckRemoteLatestVersion` already polls Verdaccio's `dist-tags.latest` — use it. Don't write a second HTTP path.
- **Manifest write must preserve unknown fields.** Use `JObject` round-trip, not POCO deserialize/reserialize. Other tools and Unity itself add fields here.
- **Call `AssetDatabase.Refresh()` after writing manifest.json** so UPM observes the new registry without a manual restart.
- **Idempotent.** Running Bootstrap twice in a row must not double-add the registry or trigger a second install if Client.Add is still pending.
- **No `Newtonsoft.Json` PropertyName attributes on JObject access** — use literal field names (`"scopedRegistries"`, `"url"`, `"scopes"`).

---

## Out of Scope

- `npm publish` of metadata — separate step done after this plan completes.
- Any push to remote git.
- UI changes.
- Removing the embedded metadata package from `dev/Packages/` — keeps working for local dev.
- Handling the case where the user manually edits `manifest.json` mid-bootstrap — best-effort.

---

## Verification (manual, by Alexandr)

After implementation:
1. `npm publish` of `metadata@0.0.1-preview.1` will be done by the controller (me) after the implementer's work is committed and reviewed.
2. Alexandr opens `E:\workspace\casai\dev` in Unity. Existing embedded metadata package will still be detected (Bootstrap takes the "already installed" path). Expected log: `[PSV Installer] Catalog v0.0.1-preview.1 loaded from ... (0 packages, 1 external).`
3. To verify the self-install path, Alexandr can temporarily rename `dev/Packages/com.psvgamestudio.installer.metadata/` → `~com.psvgamestudio.installer.metadata.bak/` (Unity ignores tilde-prefixed folders), let Unity recompile, and watch the console:
   - `[PSV Installer] metadata package not detected; performing first-time bootstrap.`
   - `[PSV Installer] Added scoped registry …` (only if not already present in manifest.json)
   - `[PSV Installer] Installing metadata package: com.psvgamestudio.installer.metadata@0.0.1-preview.1…`
   - UPM resolves it from Verdaccio, Unity reloads, and on next cycle: `[PSV Installer] Catalog v0.0.1-preview.1 loaded ...`
4. After verification, Alexandr renames the folder back to remove the registry-installed copy and continue with the embedded one for dev.

---

## Commit Policy

Per logical unit. Conventional Commits:
- `feat(bootstrap): self-install metadata when not present` — the new `MetadataAutoInstall` + Bootstrap wiring.
- `chore(installer): drop metadata UPM dependency` — `installer/package.json`.
- `chore(metadata): bump to 0.0.1-preview.1` — `metadata/package.json` + CHANGELOG entry.

(Two repos involved — `installer` and `installer.metadata`. Commit in each independently.)

---

## Self-Review for the Implementer

Before reporting:
- Manifest writer uses `JObject` round-trip, not POCO — verify by opening a real `manifest.json` mentally and confirming nothing unrelated would be lost.
- Idempotency: two consecutive `MetadataAutoInstall.Run()` calls with no Unity reload between them must not produce two new entries.
- Error path: every external call (file read, file write, HTTP, Client.Add) wrapped — none leak exceptions to `EditorApplication.delayCall`.
- `using` directives include `System.Linq` if you use any Linq helpers.
- No accidental change to `CatalogUpdater` public surface.
