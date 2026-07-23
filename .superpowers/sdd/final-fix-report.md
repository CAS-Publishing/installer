# CAS.AI Publishing Hub redesign — final whole-branch review fixes

Branch: `feat/installer-wizard-ui`. Four findings from the final whole-branch review, fixed below.

## 1. ProgressScreen stub fallback reachable by real users (IMPORTANT)

**Root cause.** `ProgressScreen.OnEnter` fell through to `EnterStubMode()` (fabricated
`StubData.ProgressSteps` rows + hardcoded 56% bar) whenever none of the three persisted states —
failure, completed, active — were true. That "none of the above" state is reachable in normal use:
`ConfigureScreen`'s Back button (`_router.Back()`, wired in `ConfigureScreen.OnEnter`) pops the
history stack back to "progress" after `ProgressScreen.OnContinue` already called
`AutoInstaller.Clear()` (which clears Active/Completed/Failed all at once). So
install-complete → Continue → Configure → Back rendered fake, ever-56%-done fabricated data to a
real user.

**Fix.** In `Editor/Wizard/Screens/ProgressScreen.cs`, the `OnEnter` else-branch now calls
`_router.GoTo("ready")` instead of `EnterStubMode()` — same pattern `OnCancel` already uses. Since
`EnterStubMode`/`StubData` had no other caller (confirmed by grep — the `WizardRouter.Preview`
"dev picker" mentioned in a code comment doesn't exist as an actual UI switcher; `Preview` is only
used once, for `ResolveStartScreen()`), `EnterStubMode` is dead code and was deleted along with
`Editor/Wizard/StubData.cs` + `.meta`. Stale comments referencing "stub data" / `EnterStubMode` in
`ProgressScreen.cs` and `AutoInstaller.cs` were reworded to describe the new redirect instead.

**Grep evidence.**
```
$ grep -rn "StubData\|EnterStubMode" --include=*.cs .
(no matches)
```

**Trace verification (read-through, no Unity run — see Verification below for why):**
- **install-complete → Continue → configure → Back:** `Drive()` calls `AutoInstaller.MarkCompleted()`
  + `ShowComplete()` on completion (does NOT clear state). `OnContinue` then calls
  `AutoInstaller.Clear()` (zeroes Active/Completed/Failed) and `_router.GoTo("configure")` — this
  pushes "progress" onto `WizardRouter`'s history stack. `ConfigureScreen`'s Back button calls
  `_router.Back()`, which pops "progress" and re-enters `ProgressScreen.OnEnter`. All three checks
  (`TryGetFailure`, `IsCompleted`, `IsActive`) are now false (cleared) → new else branch →
  `GoTo("ready")`. **Lands on Ready, sane.**
- **Mid-install reload still resumes:** unaffected — `IsActive` is set by `StartAll()` before the
  reload and survives it via `SessionState`; `OnEnter` still takes the `EnterAutoMode()` branch
  first, resuming the poll. No code in this branch changed.
- **Failed state still shows failure panel:** unaffected — `AutoInstaller.MarkFailed` persists
  `FailedIdKey`/`FailedDetailKey` via `SessionState`, and `TryGetFailure` is checked first (before
  even `IsCompleted`/`IsActive`), routing to `EnterFailedMode` regardless of the else-branch change.

**Covering tests (owner-run in Unity — no CLI runner available in this environment):**
- `Editor/Tests/AutoInstallProgressTests.cs` — covers `AutoInstallProgress.FirstUnresolved` /
  `IsStepOverdue` (pure logic used by `Drive`/watchdog). Does **not** touch `StubData` or
  `EnterStubMode` (verified: neither symbol appears in the file), so nothing needed adapting/removing
  there — no stub-only tests existed to begin with.
- No existing automated test exercises `ProgressScreen.OnEnter`'s branch selection (it's UI-Toolkit
  screen wiring, not covered by the EditMode suite here) — this is the one item in this batch that
  needs a manual owner check in the Editor: run the install-complete → Continue → Configure → Back
  sequence and confirm it lands on Ready, not a fake progress bar.

## 2. Batch-confirm dialog omits EDM4U (IMPORTANT)

**Fix.** `Editor/Wizard/AutoInstaller.cs:271` (`BuildSummary`) now reads:
`"Install all default components (CAS SDK, Tenjin, Firebase Analytics, EDM4U)?"`.

Grepped `AutoInstaller.cs` for any other hardcoded 3-component enumeration — the previously-flagged
`BuildSummary` line was the only one in that file. Also updated one adjacent doc-comment in
`Editor/Wizard/ComponentStatusProvider.cs:36` ("Reads the default client component set (CAS SDK,
Tenjin, Firebase Analytics, EDM4U)...") since it's the same class of stale enumeration one line
away from the reviewed code, and free to fix while in there.

**Grep evidence.**
```
$ grep -n "CAS SDK, Tenjin\|Tenjin, Firebase" Editor/Wizard/AutoInstaller.cs Editor/Wizard/ComponentStatusProvider.cs
Editor/Wizard/AutoInstaller.cs:271:            sb.AppendLine("Install all default components (CAS SDK, Tenjin, Firebase Analytics, EDM4U)?");
Editor/Wizard/ComponentStatusProvider.cs:36:    /// Reads the default client component set (CAS SDK, Tenjin, Firebase Analytics, EDM4U) from the
```
(Other 3-component mentions found repo-wide are in docs/CHANGELOG/SDD planning artifacts, not code —
left untouched, out of scope.)

**Covering tests:** none automate the dialog string (it's an `EditorUtility.DisplayDialog` body, no
EditMode test asserts on it). Owner should eyeball the "Install all" confirm dialog from Ready once
in the Editor.

## 3. Doubled catalog-error note on Components screen (MINOR)

**Root cause.** `ComponentsScreen.Rebuild()` called `Fill()` twice — once for the Main table, once
for Additional — with the same `TryGetStatuses`/`TryGetAdditionalStatuses` delegates. Both draw from
the same catalog/scan, so a catalog failure fails identically both times, and `Fill` rendered an
identical error `Label` under **both** tables.

**Fix.** `Fill` now takes a `showErrorNote` bool; `Rebuild` passes `true` for the main table and
`false` for additional, so on error the note appears once (under Main) and the Additional section
renders nothing (empty host, same as before for a clean success case with zero additional rows).

**Grep evidence.**
```
$ grep -n "Fill(_rowsHost\|Fill(_additionalRowsHost\|showErrorNote" Editor/Wizard/Screens/ComponentsScreen.cs
            Fill(_rowsHost, ComponentStatusProvider.TryGetStatuses, showErrorNote: true);
            Fill(_additionalRowsHost, ComponentStatusProvider.TryGetAdditionalStatuses, showErrorNote: false);
        private void Fill(VisualElement host, TryGetStatusesFn tryGet, bool showErrorNote)
                if (showErrorNote)
```

**Covering tests:** none — `ComponentsScreen` is UI-Toolkit screen wiring with no EditMode test.
Owner should force a catalog failure (e.g. temporarily rename/corrupt the catalog file) and confirm
the error note now shows once, under Main only.

## 4. Orphan cleanup (MINOR)

Icons — grepped each name (both `Icons/<name>` path form and bare filename/string-literal form)
across `*.cs`/`*.uss`/`*.uxml`/`*.json` before deleting. Confirmed orphaned and deleted (png + .meta):
`robot`, `gear-big`, `wand`, `reset`, `external`, `settings`, `warning`, `dashboard`.

**`gear-tile` was NOT deleted** — it's live: `theme.uss` has
`.cas-logo--generic { background-image: url("../Icons/gear-tile.png"); }`, and
`ComponentStatusProvider.cs:298` returns `"generic"` as the `Logo` value for any additional/catalog
component with no per-package logo, which `ComponentsScreen.BuildRow`/`ComponentsViewMap` turn into
`AddToClassList("cas-logo--" + c.Logo)`. Kept as-is.

**Grep evidence (icons, sample):**
```
$ grep -rn "\"robot\"\|robot\.png" --include=*.cs --include=*.uss --include=*.uxml --include=*.json .
(no matches — repeated per icon name; all 8 clean)
$ grep -rn "cas-logo--generic\|gear-tile" --include=*.cs --include=*.uss .
Editor/Wizard/ComponentStatusProvider.cs:298:            return (id, name, sub, "generic");
Editor/Wizard/Uss/theme.uss:...: .cas-logo--generic { background-image: url("../Icons/gear-tile.png"); }
```

USS blocks in `Editor/Wizard/Uss/theme.uss` — grepped each selector across `*.cs`/`*.uxml` before
removing:
- `.cas-radio`, `.cas-radio__dot`, `.cas-radio--on` — removed (zero references anywhere; the CAS
  in-installer ad-format/audience radio UI they styled was removed in an earlier commit of this
  branch, `refactor(installer)!: remove in-installer CAS configuration`).
- `.cas-tip__hl` — removed (zero references).
- `.cas-cfg`, `.cas-cfg__title`, `.cas-cfg__grouplabel(--spaced)`, `.cas-cfg__grid`, `.cas-cfg__row`,
  `.cas-cfg__toggle(--half)`, `.cas-cfg__radio`, `.cas-cfg__radio-label` — removed (zero references;
  same removed in-installer CAS config UI). **`.cas-cfg__hint` kept** — confirmed live:
  `ComponentsScreen.cs:164` (`hint.AddToClassList("cas-cfg__hint")`) styles the Main Components
  table's action-hint label (e.g. "to v1.12.0" under an Update button).
- `.cas-setup-summary` (base, layout-only) — removed; it was **never applied** as a class anywhere
  (`about-status` in `About.uxml` only carries `cas-sub`). **`.cas-setup-summary--ok` and
  `--warn` kept** — confirmed live: `AboutScreen.cs:133-136` toggles them directly on `_status`.

Did **not** touch `ComponentStatus.ActionVariant`/`Actionable` — not part of this cleanup, and no
references to either were found near the touched files besides existing test usage.

**Grep evidence (USS classes, post-cleanup sanity check):**
```
$ grep -rn "cas-radio|cas-tip__hl|cas-cfg__title|cas-cfg__grid|cas-setup-summary\b" --include=*.cs --include=*.uxml --include=*.uss .
(no matches)
$ grep -n "cas-cfg__hint|cas-setup-summary--ok|cas-setup-summary--warn|cas-logo--generic" Editor/Wizard/Uss/theme.uss
(all four present)
```

**Covering tests:** none apply — icons/USS have no test coverage (visual assets, no logic). No
regression risk beyond "class renders with no matching rule," which the grep sweep rules out.

## Verification

No CLI test runner is available in this environment, and Unity was not launched per instructions.
All four fixes were verified by static reading + exhaustive grep (shown above), not by running the
Editor. **Owner should**, before merging: open the wizard in Unity, run
Ready → (install) → Continue → Configure → Back and confirm it lands on Ready; trigger a catalog
failure once to confirm the Components error note appears only once; eyeball the "Install all"
confirm dialog copy.
