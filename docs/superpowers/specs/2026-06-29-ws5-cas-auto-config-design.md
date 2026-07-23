# WS-5 — CAS auto-config (ad formats / audience / network set) — Design

> **Status:** Owner-approved decisions (2026-06-29). Mechanism = reflection into CAS `DependencyManager`;
> UI lives in the Configuration tab's CAS row. Feedback **#4.5**. Next: writing-plans.

## Problem

On automatic setup the installer configures only the CAS managerId; the user still has to open CAS's
own settings to pick ad formats, audience, and the network set (OptimalAds vs FamiliesAds). #4.5 asks the
installer to offer these. Also, the per-platform CAS settings asset is sometimes MISSING (iOS especially →
the "settings asset not found" symptom), so there's nothing to write to.

## What the installer sets (verified against CAS 4.7.4)

All on the `CASSettings<Platform>` ScriptableObject (`Assets/CleverAdsSolutions/Resources/CASSettings{Android,iOS}.asset`):

- **Ad formats** → `allowedAdFlags` (int bitmask, enum `AdFlags`): `Banner=1, Interstitial=2, Rewarded=4, AppOpen=8`.
- **Audience** → `audienceTagged` (int, enum `Audience`: `Mixed=0, Children, NotChildren` — confirm ordinals in Unity).
- **Network set OptimalAds/FamiliesAds** → NOT an asset field. It is a CAS **mediation solution** activated via the
  CAS editor `DependencyManager`. Families ↔ children audience, Optimal ↔ adult.
- **Asset existence** → create `CASSettings<Platform>.asset` if missing (template: `m_Script` guid
  `cd2f38c563828458c8e900006c010cd2`, the fields seen on the Android asset).

## UI (Configuration tab, CAS row)

Extend the CAS row (which already shows the editable managerId from WS-1) with, per platform:

- Ad-format toggles: Banner / Interstitial / Rewarded / AppOpen — seeded from the current `allowedAdFlags`.
- An audience/network choice: **Optimal (adult)** vs **Families (children)** — a two-option segmented control,
  seeded from the current `audienceTagged`.

Changing a toggle writes `allowedAdFlags`; changing the audience/network writes `audienceTagged` AND triggers the
mediation-solution activation (below). All writes go to the per-platform asset (creating it first if missing).

## Components

- **`CasSettingsWriter`** (new, `PSV.Installer.Wizard`) — owns the asset reads/writes:
  - `int ReadAdFlags(string platform)` / `int ReadAudience(string platform)` — current values (0 when no asset).
  - `void SetAdFlags(string platform, int flags)` — write `allowedAdFlags`, creating the asset if missing.
  - `void SetAudience(string platform, int audience)` — write `audienceTagged`, creating the asset if missing.
  - `Object EnsureAsset(string platform)` — return the CAS settings asset for the platform, creating it from
    the template when absent. Asset path/type come from the catalog CAS `config` (the existing `assetPath`).
  - Reuses the existing `SetupChecker.LocateAsset` + `SerializedObject` write pattern from `CasIdApplier`.
- **`AdFlagsBits`** (pure, testable) — bit helpers: `WithFlag(int mask, int flag, bool on)`, `HasFlag(int mask, int flag)`,
  and the `Banner/Interstitial/Rewarded/AppOpen` int constants. Unit-tested.
- **`CasMediation`** (new) — reflection wrapper over CAS's `DependencyManager`:
  - `bool SelectSolution(string platform, bool families)` — best-effort: via reflection,
    `DependencyManager.Create(BuildTarget, Audience, false)`, find the solution in `.solutions` whose `id`
    equals `"OptimalAds"`/`"FamiliesAds"` (constants `adsOptimalName`/`adsFamiliesName`), call
    `ActivateDependencies(platform, manager)`. Returns false (logs a warning) when the CAS type/method isn't
    found — the asset-field writes still succeed, so a CAS API change degrades gracefully instead of throwing.
  - The exact reflected member names are documented here and MUST be confirmed in Unity by the owner (this is
    the one piece that cannot be headless-tested and is the most version-fragile).
- **`SetupScreen`** — renders the format toggles + audience/network control on the CAS row (gated, like WS-1's
  editable cell, by the catalog flags so only CAS gets them); wires changes to the writer + `CasMediation`.

## Data (catalog)

Add to the CAS `config` rows a small declaration so the controls are data-driven (mirrors `editable`):

- `adFormats: true` — the row offers the ad-format toggles + audience/network control. Default false → no controls.
- (Field names `allowedAdFlags` / `audienceTagged` are CAS-stable; the writer targets them directly rather than
  declaring each in the catalog — keeps the catalog change minimal.)

## Reflection contract (CAS `DependencyManager`, to confirm in Unity)

```
Type: CAS.UEditor.DependencyManager (CAS editor assembly) — confirm full namespace
  static DependencyManager Create(BuildTarget platform, Audience audience, bool deepInit)
  field/prop Dependency[] solutions
  const string adsOptimalName = "OptimalAds", adsFamiliesName = "FamiliesAds"
Type: Dependency
  string id
  void ActivateDependencies(BuildTarget platform, DependencyManager mgr)
```

`Audience` for the Create call: families → Children, optimal → NotChildren (matching the asset's `audienceTagged`).

## Edge cases

- **CAS not installed:** Configuration shows only installed components, so the controls never render — no asset, no reflection.
- **Reflection failure (CAS API moved):** `CasMediation.SelectSolution` returns false + logs; the format/audience asset
  writes still apply. The wizard never throws on a CAS upgrade.
- **Missing asset:** `EnsureAsset` creates it from the template before writing.
- **Audience ↔ network coupling:** selecting Families sets `audienceTagged=Children` AND activates FamiliesAds; Optimal
  sets `audienceTagged=NotChildren` (or Mixed) AND activates OptimalAds. One control, both effects.

## Testing

- `AdFlagsBits` pure unit tests (toggle on/off, has-flag, multiple flags) — EditMode.
- Audience mapping (families↔Children, optimal↔NotChildren) as a pure function — EditMode.
- Asset create/write, reflection activation, and the UI: **owner-run in Unity** (no headless runner; reflection needs the CAS package).

## Relationship to prior work

- Builds on WS-1 (`CasIdApplier.SetManagerId`, the editable CAS row, the catalog `editable` flag) — same Configuration row,
  same asset-write pattern.
- `EnsureAsset` generalises the manual `CASSettingsiOS.asset` creation done in dev on 2026-06-29.
