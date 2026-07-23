# Task 4 report — ProgressScreen failure panel, Cancel, inline completion

## What was implemented

1. **`ProgressFailureModel`** (`Editor/Wizard/ProgressFailureModel.cs`, guid
   `4a01ba47208c4efa9f9954ce2efa4df1`) — pure, no-Unity mapping from a failed step's
   name/error to the failure panel's copy, exactly as specified in the brief:
   - `Title` = `"{step} — installation failed"`
   - `Message` = `"{error} Check your internet connection."`
   - `Log` = raw `error` (what gets copied to clipboard, undecorated)

2. **Tests** (`Editor/Tests/ProgressFailurePanelTests.cs`, guid
   `f621b3217c65437e98bd95b1c84a09e7`, namespace `PSV.Installer.Tests`):
   - `FailureModel_CarriesStepAndHint` — the brief's Step-1 test verbatim.
   - `FailureModel_LogCarriesRawError` — added to lock down that `Log` is the raw error
     (not the decorated `Message`), since that's the exact string `Copy log` puts on the
     clipboard.
   Both assert directly against `ProgressFailureModel.From(...)`, matching the model's
   field values one-for-one — they compile against the implementation as written (traced
   by hand; no CLI/Editor test runner available in this environment, per constraints).

3. **`Progress.uxml`** — added:
   - `#progress-title` (was an unnamed `cas-h2` Label) and a new `#progress-subtitle`
     (`cas-sub`, hidden by default) so the header can swap to "Installation complete".
   - `#fail-panel` (`cas-card`, red border, hidden by default): `#fail-title`,
     `#fail-message`, `#btn-retry-step` ("Retry step"), `#btn-copy-log` ("Copy log").
   - `#done-panel` (`cas-card cas-card--green`, hidden by default): static
     "✓ All components installed" title + "The project is fully prepared. You can
     continue to configuration and finalize plugin settings." desc — reuses the existing
     card/card--green classes already used elsewhere in the wizard (`IntegrationMode.uxml`).
   - Footer: renamed `#progress-cancel` → `#btn-cancel` (grep-verified nothing else in
     the codebase referenced the old id) and added `#btn-continue` ("Continue", hidden by
     default, primary style).

4. **`ProgressScreen.cs`**:
   - Wires `#btn-cancel` → `OnCancel` (now `AutoInstaller.Clear()` + `GoTo("ready")`,
     was `GoTo("components")`), `#btn-retry-step` → `OnRetryStep`, `#btn-copy-log` →
     `OnCopyLog` (`EditorGUIUtility.systemCopyBuffer = _failure.Log`), `#btn-continue` →
     `OnContinue` (`GoTo("setup")` with a `// TODO(Task 6): becomes "configure"` comment,
     per the controller resolution given for this task).
   - `FailStep(id, detail)` no longer calls `AutoInstaller.Clear()` or shows an
     `EditorUtility.DisplayDialog` / navigates to Components — it now calls
     `ShowFailure(id, detail)`, which builds the `ProgressFailureModel` and displays
     `#fail-panel` inline. `EditorUtility` is still used (unchanged) by `WatchdogTimeout`.
   - `Drive()`'s all-resolved branch (`next < 0`) now calls `ShowComplete()` instead of
     `_router.GoTo("done")` — completion is fully inline on the Progress screen, per the
     brief. The old `DoneScreen`/`"done"` id is untouched (still registered, still used by
     `AutoInstaller.StartAll`'s separate "nothing to install" short-circuit and by
     `HubActionsScreen` — out of this task's scope, left alone).
   - `ResetPanels()` — hides both panels, restores the default title/Cancel-visible
     chrome; called at the top of both `EnterAutoMode()` and `EnterStubMode()` so a
     failure/completion shown on a previous run of the screen doesn't linger into a fresh
     entry.

## How "Retry step" cooperates with the SessionState queue

`AutoInstaller.IssuedIndex` (SessionState-backed, survives domain reloads) is the
driver's "which step have I issued" cursor. In `Drive()`, when a step's `InstallOne`
fails synchronously (either the apply itself fails, or it succeeds but the manifest
doesn't end up carrying the dependency), `IssuedIndex` has **already** been set to
that step's index (`AutoInstaller.IssuedIndex = next;` runs before the
success/failure check). The old code then called `AutoInstaller.Clear()`, which reset
`IssuedIndex` to `-1` and `Active` to `false` — discarding the whole run.

The new `FailStep` does **not** call `Clear()`, so `IssuedIndex` stays pinned at the
failed step and `Active` stays `true`. `OnRetryStep()`:

```csharp
var idx = _targetIds.IndexOf(_failedId);
if (idx >= 0)
    AutoInstaller.IssuedIndex = idx - 1;   // roll back just one
_issueAtTime = 0;
StartPoll();
```

rolls `IssuedIndex` back to `idx - 1` (i.e. "one step behind the failed one"), which is
exactly the state `Drive()` expects for "this step hasn't been issued yet" —
`AutoInstaller.IssuedIndex < next` becomes true again for the *same* `next` (still the
failed step, since `FirstUnresolved` is computed from live UPM state, not from
`IssuedIndex`). `Drive()` then re-runs its normal issue path (pause → `InstallOne(id)`)
for that one id. Steps before it are untouched: they're already resolved in the live
`Client.List` result, so `FirstUnresolved` skips straight past them regardless of
`IssuedIndex`. No `AutoInstaller.cs` changes were needed — `IssuedIndex` was already a
public settable property, satisfying the brief's "if not already there" caveat.

## Files touched

- `Editor/Wizard/ProgressFailureModel.cs` (new, guid `4a01ba47208c4efa9f9954ce2efa4df1`)
- `Editor/Wizard/ProgressFailureModel.cs.meta` (new)
- `Editor/Tests/ProgressFailurePanelTests.cs` (new, guid `f621b3217c65437e98bd95b1c84a09e7`)
- `Editor/Tests/ProgressFailurePanelTests.cs.meta` (new)
- `Editor/Wizard/Screens/ProgressScreen.cs` (modified)
- `Editor/Wizard/Uxml/Progress.uxml` (modified)
- `Editor/Wizard/AutoInstaller.cs` — **not** modified; `IssuedIndex` was already a
  public settable property, sufficient for step-level retry.

## Compile risks / things to verify in the Editor

- `EditorGUIUtility` lives in `UnityEditor`, already `using`-imported in
  `ProgressScreen.cs` (alongside `EditorUtility`) — no new using needed.
- `_targetIds` is `List<string>`, so `.IndexOf(string)` resolves to the standard
  `List<T>.IndexOf` overload — no ambiguity.
- UXML element names were double-checked 1:1 against the `Root.Q<T>("...")` calls added
  in the constructor (`progress-title`, `progress-subtitle`, `btn-cancel`,
  `btn-continue`, `fail-panel`, `fail-title`, `fail-message`, `btn-retry-step`,
  `btn-copy-log`, `done-panel`).
- Reused `.cas-card` / `.cas-card--green` / `.cas-card__title` / `.cas-card__desc`
  classes from `theme.uss` (already used by `IntegrationMode.uxml`) rather than adding
  new CSS, to keep styling consistent with the rest of the wizard. The default
  `.cas-card` is `flex-direction: row; align-items: center` (for an icon+body layout);
  both new panels override to `flex-direction: column; align-items: flex-start` inline
  since neither has an icon box — verify visually in the Editor that this reads well
  (no icon box means the card's `padding: 14px` is the only inset, might want a look).
- **Known edge case, deliberately not fixed (out of scope for this task):** if a domain
  reload happens *while the failure panel is showing* (not caused by our own step —
  e.g. an unrelated background recompile), `OnEnter → EnterAutoMode → ResetPanels()`
  will hide the fail panel on re-entry, and `AutoInstaller.StepDeadline` is not armed
  for the failed step, so `Drive()`'s "already issued, waiting" branch won't watchdog it
  either — the run would sit silently until the user manually retries via Cancel → Ready
  → Install all, or reopens the wizard. This mirrors a pre-existing class of
  reload-timing edge cases in this driver (e.g. `WatchdogTimeout`'s own dialog is also
  reload-fragile) and wasn't something the brief asked to harden.
- No CLI/Editor test runner was available in this environment (per task constraints) —
  the two new tests were traced by hand against `ProgressFailureModel.From` and are
  straightforward string assertions with no Unity API surface, so risk of a silent
  compile/runtime mismatch is low, but a real Test Runner pass is still recommended
  before merge.

## Fix pass (review round 2) — domain-reload resilience

Review came back "Needs fixes" with three Important findings plus one Minor. All four
are addressed below, reasoned through by hand (still no CLI/Editor test runner in this
environment).

### 1. Completion state destroyed by an unrelated domain reload

**Root cause confirmed:** `Drive()` called `AutoInstaller.Clear()` (which sets
`Active = false`) *before* `ShowComplete()`. If any later, unrelated domain reload
recreated the window (e.g. a background asset import finishing late), `CreateGUI` →
`ResolveStartScreen` restored `"progress"` from `SessionState`, `OnEnter` saw
`IsActive == false`, and fell to `EnterStubMode()` — replacing the real "Installation
complete" panel with fake dev-preview data.

**Two options were on the table, per the coordinator's note ("pick the safer one and
justify"):**
- **(a)** A one-shot "just completed" SessionState flag, consumed once in `OnEnter`.
- **(b)** Defer `AutoInstaller.Clear()` until the user actually leaves the completed
  state (Continue/Cancel), and give `OnEnter` a *durable*, re-checkable signal
  (`AutoInstaller.IsCompleted`) instead of a one-shot flag.

**Chose (b) with a durable flag, not a one-shot.** A one-shot "consume and clear" flag
has a correctness trap: the very next time `OnEnter` fires for the Progress screen for
*any* reason — not necessarily the reload this fix targets — it gets consumed. E.g.
`HubActionsScreen`'s dev "hub-auto" tile (`Editor/Wizard/Screens/HubActionsScreen.cs:29`)
navigates straight to `"progress"` with `_router.GoTo("progress")`, with no
`AutoInstaller.StartAll()` call first. If a one-shot flag were still set from a
*previous, unrelated* completed run (e.g. the user finished an install, went to Setup,
came back later and poked that dev tile), it would falsely show a stale "Installation
complete" panel once, then silently self-clear — masking whatever that tile was actually
meant to show. A durable `IsCompleted` flag doesn't have this failure mode: it's true
only for as long as the batch really is in the completed state, and only becomes false
again via `AutoInstaller.Clear()`, called from the completed screen's own `Cancel`/
`Continue` handlers (the only two ways to leave it) or from the top of a fresh
`StartAll()` (defensive, so a new run never inherits a stale phase from a previous one).

**What changed:**
- `AutoInstaller.MarkCompleted()` / `IsCompleted` (new, `SessionState`-backed).
- `Drive()`'s `next < 0` branch now calls `MarkCompleted()` instead of `Clear()`, then
  `ShowComplete()`. `Clear()` (which resets `Completed` too) moved to `OnCancel` and
  `OnContinue`.
- `ProgressScreen.OnEnter` now checks `AutoInstaller.IsCompleted` (and the failure flag,
  see #2) **before** `IsActive`, and calls a new `EnterCompletedMode()` which rebuilds
  the step list from live component status (all resolved, since the run finished) and
  redraws the completion panel — no polling needed, matching what `Drive()` already knew
  when it first showed the panel.
- `AutoInstaller.StartAll()` now calls `Clear()` at the top of its "committed to
  install" path, before setting `Active = true`, so a fresh run can never start with a
  leftover `Completed`/`Failed`/`IssuedIndex` from a previous batch.

**Reload-path reasoning:** completion happens → `MarkCompleted()` (persisted) →
`ShowComplete()` (drawn in the live instance) → poll stopped, `Active` still `true`. If a
reload now recreates the window: `OnEnter` → `IsCompleted` true → `EnterCompletedMode()`
→ same panel redrawn, no dependency on the poll or on `IsActive`. If the user instead
clicks Continue/Cancel first (no reload): `AutoInstaller.Clear()` runs, `IsCompleted`
becomes false, and the *next* `OnEnter` for this screen (a genuinely new run via
`StartAll`, or an idle dev-tile poke) sees a clean slate — no possibility of the stale
panel reappearing.

### 2. Failure panel + reload can hang silently

**Root cause confirmed:** `AutoInstaller.StepDeadline` is only armed *after* a
successful issue (`ProgressScreen.cs`, in the success path of `Drive()`, right before
`FailStep` would otherwise be reached on a *different* code path). When a step fails
synchronously — `InstallOne` itself fails, or the manifest doesn't gain the dependency —
`FailStep` runs and the deadline was **never armed** for that step (it's still whatever
it was left at by the *previous* step, or `0`). `AutoInstallProgress.IsStepOverdue`
treats `deadline <= 0` as never-overdue by design (so a freshly-entered screen can't
false-trip the watchdog). If a reload then hides the fail panel (old bug), `EnterAutoMode`
would resume polling and the watchdog would never fire for this step — the run hangs
with zero visible signal to the user.

**Fix:** rather than trying to make the watchdog deadline reload-safe (fragile — it
would need to distinguish "never armed because nothing was issued yet" from "never
armed because the issued step failed before reaching UPM," which isn't recoverable from
`StepDeadline`/`IssuedIndex` alone), the failure itself is now persisted directly:
- `AutoInstaller.MarkFailed(id, detail)` / `TryGetFailure(out id, out detail)` (new,
  `SessionState`-backed strings) — `FailStep` calls `MarkFailed` right alongside the
  existing `ShowFailure(id, detail)`.
- `ProgressScreen.OnEnter` checks `AutoInstaller.TryGetFailure(...)` **first**, before
  `IsCompleted`/`IsActive`, and calls a new `EnterFailedMode(id, detail)` which rebuilds
  the step list (steps before the failed one assumed resolved, the failed one shown
  "active" — matching what the live in-session render already looked like at the moment
  of failure) and redraws the fail panel from the persisted id/detail — no reliance on
  the watchdog at all.
- `OnRetryStep` now also calls `AutoInstaller.ClearFailure()` (alongside rolling
  `IssuedIndex` back), so a reload that happens *while* the retried step is resolving
  doesn't re-show the old (now-stale) failure.

**Reload-path reasoning:** step fails → `MarkFailed` (persisted) + `ShowFailure` (drawn
live) → poll stopped. Any later reload: `OnEnter` → `TryGetFailure` true →
`EnterFailedMode` redraws the exact same panel from the persisted strings, independent
of `StepDeadline`. The only ways `TryGetFailure` becomes false again are `ClearFailure()`
(Retry) or `Clear()` (Cancel, or the top of the next `StartAll()`) — both are the
intended, user-initiated exits from the failed state.

### 3. `"ready"` missing from `ScreenOrder`

Confirmed: `ResolveStartScreen()` only restores a `SessionState`-persisted screen id
when `Array.IndexOf(ScreenOrder, saved) >= 0`; `"ready"` (added in Task 3, and now the
`Cancel` target from this task) wasn't in that array, so a reload while sitting on Ready
would bounce to `"components"` instead of restoring Ready. One-line fix in
`Editor/Wizard/InstallerWizardWindow.cs:24-25`:

```csharp
private static readonly string[] ScreenOrder =
    { "welcome", "integration", "ready", "components", "hub", "progress", "done", "settings", "setup", "about" };
```

Inserted right after `"integration"`, mirroring `RegisterScreens`' registration order
(`WelcomeScreen`, `IntegrationModeScreen`, `ReadyScreen`, `ComponentsScreen`, ...) even
though `ScreenOrder`'s only consumer (`ResolveStartScreen`'s `IndexOf` membership check)
doesn't care about array order. As the coordinator noted, `ScreenOrder` gets rewritten
in Task 5 — this is just the minimal fix to keep the intermediate state coherent.

### Minor: `OnRetryStep` guard ordering

Reordered so the null/empty guard (`_failedId`/`_targetIds` missing — shouldn't happen,
but defensive) runs **before** hiding `_failPanel`, not after. Previously, hitting that
guard would have already hidden the panel and then bailed without restarting the poll —
stranding the user on a blank screen with no visible state and no running poll. Now, an
unrecoverable click leaves the fail panel (and its Copy log / the footer Cancel) visible
instead.

## Note on this file

This report replaces an unrelated stale `task-4-report.md` (SetupScreen platform-scope
change) that was left over from an earlier/different task-numbering pass in this SDD
ledger — that content did not match the Task 4 brief given for this session
(`ProgressScreen` retry/copy/complete) and has been overwritten.

## Commit

`feat(installer): progress screen failure panel (retry step, copy log) + inline completion state`
