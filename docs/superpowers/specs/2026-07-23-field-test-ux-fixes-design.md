# Field-test UX fixes (triage items 3, 7, 8, 9) — design

**Date:** 2026-07-23. Companion to `2026-07-23-firebase-migration-and-registry-fix-design.md`; ships in the same `0.0.1-preview.38` release.

## Verified root causes

- **Item 3 (Android Settings error):** `CasNativeSettings.Open` executes the menu path without a trailing ellipsis; CAS ≥ 4.7.x names the items `"Android Settings..."` / `"iOS Settings..."`. `ExecuteMenuItem` returns false / logs an error, and only the asset-ping fallback runs.
- **Item 7 (Install button needs manual Refresh):** `WizardActions.*` write the manifest, `ComponentsScreen` calls `Rebuild()`, but `ComponentStatusProvider`'s session cache is never invalidated — the domain reload the cache comment relies on is asynchronous (or absent when no scripts recompile), so rows render stale state until manual Refresh.
- **Item 8 (Hub auto-opens only after project restart):** first-run chain is `Bootstrap.RunOnce → MetadataAutoInstall.Run → (metadata installs) → next domain reload → auto-open`. On a transient `Client.Add` failure ("exclusive access" during first import) the code logs "will retry after reload" — but nothing guarantees another reload ever happens, so metadata (and the auto-open) stall until an editor restart.
- **Item 9:** Hub entry points live only under `Assets/CleverAdsSolutions/`, which testers read as the CAS ad-SDK settings menu.

## Design

1. **Menu candidates (item 3).** `CasNativeSettings` builds both candidates per platform — `"Assets/CleverAdsSolutions/<P> Settings"` and the same + `"..."` — and tries `ExecuteMenuItem` on each before the existing asset-ping fallback. Pure candidate builder, unit-tested; works with old and new CAS SDKs.
2. **Cache invalidation on action (item 7).** In `ComponentsScreen`, every action callback that returns `changed == true` (action button AND Remove button) calls `ComponentStatusProvider.InvalidateCache()` before `Rebuild()`. Catalog cache is NOT touched — network re-probe stays behind the explicit Refresh button.
3. **Metadata retry (item 8).** New `MetadataInstallRetry` (core, Editor-only): armed by `MetadataAutoInstall` on a TRANSIENT failure only. Retries `MetadataAutoInstall.Run()` when `UnityEditor.PackageManager.Events.registeredPackages` fires or after a ~5 s `EditorApplication.update` timer, capped at 5 attempts per domain-reload epoch (statics reset on reload — acceptable, each reload re-enters Bootstrap anyway). Terminal failures (offline, auth) keep the existing once-per-session throttle. `Run()` is already idempotent via `IsMetadataInstalled()`.
4. **Top-level menu (item 9).** New Unity main-menu item **"CAS Hub → Open Hub"** → `InstallerWizardWindow.Open()`. Unity requires one submenu level, so this is the minimum click count. Existing `Assets/CleverAdsSolutions/*` items stay for compatibility.

5. **Clickable Configuration cells (owner follow-up, same release).** Table cells in the Configuration screen become clickable, per-platform (the clicked column decides Android/iOS):
   - CAS row (both ✓ and ⚠): `CasNativeSettings.Open(platform)` — the CAS window creates its own settings asset on first open, so "missing → create, present → open" is one action.
   - Firebase row: config file present → ping/select it in the Project window; missing → the existing Locate-file flow (per-platform dialog title, `FirebaseConfigFile.ValidateAndCopy`, refresh on success).
   - Tenjin row (key cell, when the field is supported): `CasNativeSettings.Open(platform)` — the key lives in the CAS window.
   - Cells get a hover affordance (cursor/highlight) so clickability is discoverable. The action panels below the table stay unchanged. Pure action-resolution helper is unit-tested; the DOM wiring is owner-verified.

No version bump (already `0.0.1-preview.38` unreleased); CHANGELOG entry extended.

## Testing

- Pure tests: menu-candidate builder; `MetadataInstallRetry` attempt cap + disarm behavior (EditMode).
- Items 7/9 are editor-glue: covered by the owner-run smoke (install a component → row updates without Refresh; "CAS Hub" menu present and opens the window).
