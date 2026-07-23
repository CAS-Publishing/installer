# Migrator asset-roots safety — design

**Date:** 2026-06-17
**Status:** approved (brainstorm)

## Problem

When migrating an out-of-UPM SDK (Firebase, Tenjin, …) to UPM, `MigrateExternal` calls
`AssetInstallProbe.FindRootsForMigration(markers)` to decide which `Assets/` folders to delete. That
method walks **every** file under `Assets/` and flags a hit when a file's namespace **or file name**
contains a marker substring (e.g. `"Firebase"`, `"Tenjin"`), then maps the hit to its **top-level**
`Assets/` folder via `TopRoot`.

This is dangerous: a client's own gameplay script (e.g. `Assets/Scripts/FirebaseLogger.cs`, or any class
in a `namespace …Firebase…`) matches the marker, and `TopRoot` resolves it to `Assets/Scripts` — so the
migrator proposes (and on confirm, deletes) the **entire** user folder. Custom scripts that merely
mention an SDK are common in games, so the migrator can wipe the whole game.

Root causes:
- Match on weak signals (`.cs` namespace/file-name substring), not on the SDK's own package content.
- `TopRoot` escalates any deep match to a top-level folder.
- Short markers (`"Firebase"`, `"Tenjin"`) match user code easily.

## Constraints (confirmed with owner)

- The SDKs we migrate (Firebase, Tenjin, EDM) **always install into their standard folders** in client
  projects (`Assets/Firebase`, `Assets/Tenjin`, `Assets/ExternalDependencyManager`, …) — not relocated.
- No hardcoded "never delete" denylist backstop is wanted; explicit catalog-declared folders plus the
  user confirmation are sufficient.

## Approach — explicit catalog-declared SDK roots; remove the file-walk

Migration deletes **only** folders the catalog explicitly declares as owned by the SDK, and **only those
that actually exist** on disk. No heuristic file-walk. The deletion target can therefore never be a user
folder — there is no code path that maps a stray script to a folder.

### 1. Catalog model (`Editor/Catalog/Catalog.cs`)

`ExternalRecord` gains `assetRoots` (`List<string>`): Assets-relative folders the SDK owns and that
migration may delete. `assetMarkers` stays — used **only** for presence detection (reflection over loaded
types; unchanged). `extraCleanupPaths` is folded into `assetRoots` (the primary-vs-satellite distinction
no longer matters once all are deleted the same way) and removed.

Rule for catalog authors: `assetRoots` must list folders **wholly owned by the SDK** (its install folder
+ its satellite folders like EDM / PlayServicesResolver / its own `Editor Default Resources/<sdk>`
subfolder). **Never** list a shared folder (`Assets/Plugins`, `Resources`, …).

### 2. Migration logic (`Editor/Wizard/WizardActions.cs`)

`MigrateExternal` replaces `FindRootsForMigration(rec.AssetMarkers)` + the `extraCleanupPaths` step with:

```
deletePaths = AssetProbe.FindExisting(rec.AssetRoots)   // only declared roots that exist
if (deletePaths is empty) → existing "appears installed manually but files couldn't be located —
                            remove it yourself, then Install the UPM version" dialog (safe no-op)
```

Everything else is unchanged: the shared-`Assets/Plugins` warn list (`FindLooseFiles`, manual cleanup
only), the downgrade guard, the custom `MigrateConfirmWindow`, delete-first-then-install ordering, and the
git/path-safety guards on every delete.

### 3. Remove the dangerous code (`Editor/Scanner/AssetInstallProbe.cs`)

Delete `FindRootsForMigration` and the private helpers it alone used: `FirstNamespace`, `TopRoot`,
`ReadAsmdef`, and the namespace regex. Keep `MatchesAny` (used by presence detection + `FindLooseFiles`),
`CollectLoadedIdentifiers` / `IsPresentInIdentifiers` / `TypeIdentifier` (presence), `FindLooseFiles`
(Plugins warn), and `ReadStaticVersion` (downgrade guard).

### 4. Catalog data (`catalog.json`, metadata package)

Add `assetRoots`, remove `extraCleanupPaths`:
- Firebase: `["Firebase", "ExternalDependencyManager", "PlayServicesResolver", "Editor Default Resources/Firebase"]`
- Tenjin: `["Tenjin"]`
- CAS: `["CleverAdsSolutions"]`

Bump `catalogVersion` to `0.0.2-preview.15`.

### 5. Tests

- `ExternalRecord.assetRoots` parses from catalog JSON (backward-compatible: absent → null).
- No test is needed to prove the walk is gone — it's deleted code; the safety is structural (deletion
  targets come only from `assetRoots`).

## Data flow

scan (markers/reflection, unchanged) → `InstalledOutsideUpm` → user clicks Migrate →
`MigrateConfirmWindow` shows install-set + `deletes = FindExisting(assetRoots)` + Plugins warn →
on confirm → delete declared existing roots → install UPM modules.

## Safety guarantee

The set of folders migration can delete is now **exactly** `catalog.assetRoots ∩ {folders that exist}`.
There is no file-walk and no `TopRoot` escalation, so no stray user script can ever cause a user folder
to be targeted. Worst case of a bad catalog entry is bounded to whatever a human typed into `assetRoots`
(reviewed in the metadata repo), not an emergent match against arbitrary project files.

## Failure mode

If an SDK is detected as present (reflection) but its declared `assetRoots` don't exist on disk
(non-standard layout — per owner, rare), migration shows the existing safe "couldn't locate — remove
manually" dialog and changes nothing. Safe degradation.

## Out of scope

- Presence detection stays reflection/marker-based (the global-namespace Tenjin fix already landed).
- No "folder contains SDK signature" sanity check (owner chose explicit folders only).
- Tenjin downgrade-guard version member (separate follow-up).
