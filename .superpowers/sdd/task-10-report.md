# Task 10 report — Main Components table (PDF terminology, Remove column, Additional components)

## Files touched

- **New** `Editor/Wizard/ComponentsViewMap.cs` (guid `c0b9938cfd954130beda660fcaa284d7`) — pure
  `RowAction` enum + `ComponentRowVm` + `ComponentsViewMap.Map(ComponentStatus, string recommendedVersion)`.
- **New** `Editor/Tests/ComponentsViewMapTests.cs` (guid `84b107c47fad438b807ee892dbb79b49`) —
  the 4 brief tests verbatim + 10 more (TooOld, null-recommended hint, needs-migration, git,
  legacy deviation, mixed-install, needs-registry, not-in-catalog, manual-install-remove-disabled).
- **Modified** `Editor/Wizard/ComponentStatusProvider.cs` — added `TryGetAdditionalStatuses`
  (+ `BuildAdditionalStatuses`, `IsOwnPackage`, `ToDescriptor`, its own session-cache fields,
  wired into `InvalidateCache`) and `ResolveRecommendedVersion`.
- **Modified** `Editor/Wizard/Screens/ComponentsScreen.cs` — rows now come from `ComponentsViewMap`;
  5-column layout (SDK/Version/Status/Action/Remove); renders both the main and the additional table
  via a shared `Fill(host, tryGetFn)` helper.
- **Modified** `Editor/Wizard/Uxml/Components.uxml` — `Main Components` header, 5-column head, green
  "Connect to Hub" info panel, `ADDITIONAL COMPONENTS` section + second table.
- **Modified** `Editor/Wizard/Uss/theme.uss` — `.cas-col-version`/`.cas-col-remove` columns,
  `.cas-col-action` switched to a column layout (button above hint), `.cas-btn--danger`,
  `.cas-infobox--green`, `.cas-logo--generic` (reuses the pre-existing, previously-unused
  `Icons/gear-tile.png` — no new binary asset added).

## Mapping table (`ComponentsViewMap.Map`)

| `ComponentStatus.StatusText` (scanner) | PDF `StatusText` | `Action` | `ActionText` | `ActionHint` | `RemoveEnabled` |
|---|---|---|---|---|---|
| `Installed` | `Up to date` | `None` | — | — | `Installed` (true) |
| `Update available` / `Too old` | `Update required` | `Update` | `Update` | `"to v" + recommendedVersion` (null → hidden) | `Installed` |
| `Installed (manual)` (OutsideUpm) | `Manual install` | `ConnectToHub` | `Connect to Hub` | — | **false** (deviation, see below) |
| `Needs migration` (Package legacy/asset) | `Manual install` | `ConnectToHub` | `Connect to Hub` | — | `Installed` |
| `Installed (git)` | `Manual install` | `ConnectToHub` | `Connect to Hub` | — | `Installed` (true — a git dep IS a manifest entry) |
| `Installed (legacy)` | `Manual install` | **`None`** (deviation) | — | detected legacy id (`status.ActionText`) | **true** (forced) |
| `Mixed install` | `Mixed install` | `Fix` | `Fix` | — | `Installed` |
| `Needs registry` | `Needs registry` | `Fix` | `Fix` | — | `Installed` |
| `Not Installed` | `Not installed` | `Install` | `Install` | — | false |
| `Not in catalog` | `Not in catalog` | `None` | — | — | **false** (forced) |

General rule implemented: `RemoveEnabled = status.Installed && !status.OutsideUpm`, with two explicit
overrides (legacy → true, not-in-catalog → false via `Installed==false` already).

### Deviations from the brief's literal text (both intentional, both documented in code comments)

1. **`Installed (legacy)` → `RowAction.None`, not `ConnectToHub`.** The brief itself flagged this as
   a "CHECK" item. Confirmed: a legacy wrapper already provides the SDK under a *different* manifest
   id (e.g. Tenjin under `com.psv.tenjin`). Routing it through Connect-to-Hub would call
   `WizardActions.MigrateExternal`/`SwitchToUpm` for the *canonical* id, installing it **alongside**
   the legacy one — a duplicate SDK, not a connect. `ComponentStatus.ActionText` already carries the
   detected legacy id for this status (`FromExternal`, unchanged), so it's reused directly as
   `ActionHint` — no new field needed.

2. **`RemoveEnabled = status.Installed && !status.OutsideUpm`** — the brief's literal general rule is
   `RemoveEnabled = status.Installed`, which is `true` for `Installed (manual)` too. I did NOT
   implement it literally: an out-of-UPM (manual) install has **no `manifest.json` entry at all**, so
   `WizardActions.Remove` → `RemovePackage` → `ManifestWriter.ApplyRemovePackage` returns `false`
   (idempotent no-op, confirmed by reading that method) — the button would look live but silently do
   nothing, which is exactly the "delete does nothing" bug this codebase has explicit prior art
   guarding against (`ComponentStatusRemoveIdTests`). The previous screen already hid Remove for this
   case for the same reason (`c.Installed && !c.OutsideUpm`). I kept that safety net and added a
   locked-in test (`ManualInstall_RemoveDisabled`). Flagging this as the one place I diverged from
   the brief's literal wording; happy to flip it if the no-op is actually the intended UX (e.g. as a
   silent "no-op is harmless" stance) — but as implemented it's the safer default and the brief's own
   tests never assert `RemoveEnabled` for that case, so nothing regresses.

Everything else matches the brief's mapping exactly, including keeping click-dispatch **unchanged**
(`c.GitInstalled ? SwitchToUpm : c.OutsideUpm ? MigrateExternal : Apply` — the same three-way branch
as before; only the button's rendered *text* changed via `vm.ActionText`/`vm.Action`).

## `TryGetAdditionalStatuses` — source fields

- Iterates `catalog.Packages` then `catalog.External`, skipping any id in `DefaultIds` or starting
  with `com.psvgamestudio.installer` (hub + metadata catalog aren't user-facing "components"), and
  deduping by id (first-wins) in case a catalog authoring mistake lists the same id under both
  `Packages` and `External`.
- Consumes the **same** `ProjectScanner.Scan(catalog)` report as the main list (it already scans
  every catalog entry, not just the 4 defaults — confirmed by reading `Scanner.cs`), then the same
  `FromPackage`/`FromExternal`/`NotInCatalog` mapping functions as `BuildStatuses` — no duplicated
  classification logic. **Correction (post-review):** the first version of this task called
  `ProjectScanner.Scan` independently from both `BuildStatuses` and `BuildAdditionalStatuses` — two
  full scans (reflection over loaded assemblies + disk probes) on every cold render/Refresh, despite
  this paragraph's original "reuses the same report" claim. Fixed in the follow-up commit
  (`fix(installer): share one project scan between main and additional component status builds`):
  a private `GetScan(PackageCatalog)` lazily runs `ProjectScanner.Scan` once per
  `InvalidateCache()` window and both builders call it, so a cold Refresh now scans exactly once.
  The scan cache (`_cachedScan`/`_hasScanCache`) is cleared in `InvalidateCache()` alongside the two
  existing view-level caches; public API (`TryGetStatuses`/`TryGetAdditionalStatuses` signatures,
  caching semantics, error propagation on catalog-load failure) is unchanged.
- **Display name**: `PackageRecord.DisplayName` / `ExternalRecord.DisplayName`, falling back to the
  raw id when absent.
- **Sub**: `PackageRecord.Category` / `ExternalRecord.Category` is a raw category *id* (e.g. `"ads"`),
  not display text. I resolve it through `catalog.Categories[].DisplayName` (built once as a
  dictionary per call) so it reads like the defaults' hand-written subs (e.g. `"Ads / Mediation"`)
  instead of a raw slug; falls back to the raw category id if no matching `Category` record exists,
  then to `""` if the record has no category at all.
- **Logo**: the catalog carries no per-package logo/icon data, so every additional-component row gets
  a literal `"generic"` logo (`.cas-logo--generic`, the repurposed unused `gear-tile.png`) rather than
  a per-id heuristic guess. Session-cached exactly like `TryGetStatuses`, invalidated together in
  `InvalidateCache()`.

## `recommendedVersion` resolution

Added `ComponentStatusProvider.ResolveRecommendedVersion(string id)`: loads the (session-cached)
catalog, scans `Packages` then `External` for a matching `Id`, returns
`RecommendedVersion ?? MinVersion` (mirrors the exact fallback `MigrationPlanner`/`WizardActions`
already use — e.g. `SwitchToUpm`'s `!string.IsNullOrEmpty(rec.RecommendedVersion) ? rec.RecommendedVersion : rec.MinVersion`).
Returns `null` when the catalog is unavailable or the id isn't a catalog entry — `ComponentsViewMap`
already treats a null `recommendedVersion` as "hide the hint" (test: `UpdateAvailable_NullRecommended_HintIsNull`),
so this degrades gracefully rather than failing the row render. Called once per row in
`ComponentsScreen.BuildRow` (cheap: `CatalogLoader.Load()` is itself session-cached).

## UXML/USS structure

Single `ScrollView` now wraps: Main-Components table head → `components-rows` → status legend
(re-worded to the PDF terms: "Up to date"/"Update required"/"Not installed") → green info panel
(exact copy verbatim) → `ADDITIONAL COMPONENTS` section label → second table head →
`additional-rows`. Title/subtitle stay outside the scroll (unchanged from before).

Decision: previously the table head was a separate, non-scrolling sibling of the scrollable rows
(a "sticky header"). With two tables + a legend + an info panel + a section label now sharing the
screen, I folded both table heads into the single ScrollView instead of engineering two independent
sticky regions — simpler, and the screen already had a fixed title above it. Net effect: the column
headers now scroll away with a long Additional Components list instead of staying pinned. Flagging
as a minor UX trade-off, easy to revisit if it's felt in practice.

5-column grid: `.cas-col-comp` (1.4) / `.cas-col-version` (0.6) / `.cas-col-status` (0.9) /
`.cas-col-action` (1, now `flex-direction: column` so the hint stacks under the button) /
`.cas-col-remove` (0.9). Remove button is always rendered (never hidden) and disabled via
`vm.RemoveEnabled`, per the brief ("disabled when `!RemoveEnabled`", not hidden) — this is a
behavior change from the old screen, which hid Remove entirely for out-of-UPM rows; now it's shown
but disabled for that case (see deviation #2 above).

## Compile risks

- No CLI/Unity build was run (per task constraints — no Unity launch). Verified: brace/paren balance
  on all 4 touched/created `.cs` files, well-formed UXML (`xml.dom.minidom`), balanced USS braces.
- `ComponentsViewMap.cs` lives in `Editor/Wizard/` (same folder/assembly as `ComponentStatusProvider.cs`),
  so it picks up the existing `[assembly: InternalsVisibleTo("PSV.Installer.Editor.Tests")]` in
  `Editor/Wizard/Properties/AssemblyInfo.cs` automatically — no asmdef changes needed. Confirmed by
  precedent: `ComponentStatusRemoveIdTests.cs` already references internal `ComponentStatus`/
  `ComponentStatusProvider` members from the Tests assembly successfully.
- `ComponentsScreen.Fill`'s private delegate `TryGetStatusesFn` is satisfied by both
  `ComponentStatusProvider.TryGetStatuses` and `TryGetAdditionalStatuses` via method-group
  conversion — signatures are identical (`bool(out List<ComponentStatus>, out string)`).
- New `.meta` GUIDs (`c0b9938c…`, `84b107c4…`) checked for uniqueness against the whole repo
  (`grep -rl` found exactly 1 match each — the new files themselves).
- `gear-tile.png` confirmed present with its own `.meta` and previously unreferenced by any `.uss`/`.cs`
  (`grep` for `gear-tile` returned nothing before this change) — safe to repurpose, no new binary asset.

## Test summary

14 tests in `ComponentsViewMapTests` (4 verbatim from the brief + 10 added), all pure/offline
(no Unity APIs, no catalog/scan access — `ComponentsViewMap.Map` takes a plain `ComponentStatus` +
a `string`). Could not run them (no Unity/Test-Runner CLI available in this environment per the task's
"do NOT launch Unity" constraint) — recommend running Test Runner (`EditMode`) before merge, plus a
manual look at the Components tab (main + additional tables, green info panel, Remove SDK styling).

## Code review fix: Scan cache key on catalog identity

The shared `GetScan(PackageCatalog catalog)` cache (lines 122–130) was ignoring its `catalog` argument
once warmed — if `CatalogLoader.InvalidateCache()` was called independently, a caller holding a NEW
catalog object would receive a scan computed against the OLD catalog.

**Fix:** Added a `private static PackageCatalog _cachedScanCatalog;` field to track the catalog the
cache was computed from. The cache is now valid only when both `_hasScanCache` is true AND
`ReferenceEquals(_cachedScanCatalog, catalog)` — i.e., the same catalog object. On each fresh scan,
`_cachedScanCatalog` is updated alongside `_cachedScan`. In `InvalidateCache()`, both are cleared together.

**Guard logic verified:** The fix catches the `AutoInstaller.StartAll` zero-plan sequence (scan warmed on
catalog A → CatalogLoader independently invalidated → new load returns catalog B → `GetScan(B)` now rescans
correctly instead of returning stale results from A). Comment explains the reason.
