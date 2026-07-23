# Welcome single-platform pass + build-target-switch auto-open — Design

> **Status:** Owner-approved design (2026-06-29, via brainstorm). Next: writing-plans.
> Evolves the Welcome screen shipped in `2026-06-29-installer-feedback-ws0-wsA-release-batch`
> (Task 1) and **merges with WS-2** (auto-open, feedback #7), per owner decision.

## Problem

Two related changes to how the installer captures the CAS ID:

1. **The installer should configure ONE platform per pass.** Today the Welcome screen captures
   both Android and iOS ids at once (segmented field over one input, both buffered). The owner
   wants a single-platform pass: the wizard configures only the platform you're targeting now.
2. **The default platform should follow the project, and switching the build target should
   re-engage the wizard.** The selected platform must default to Unity's active build target
   (not a hard-coded Android). And when CAS is installed, Android is already configured, and the
   user switches the build target to iOS (still unconfigured), the installer should auto-open the
   wizard to configure iOS.

It also adds real CAS-ID format validation (the field is currently unvalidated), with
per-platform rules and a placeholder hint.

## Goals / non-goals

- **Goal:** single-platform Welcome pass, defaulting to the active build target, switchable, with
  strict per-platform validation and a placeholder hint.
- **Goal:** a build-target-change trigger that auto-opens the wizard for an unconfigured platform.
- **Goal:** unify both auto-open paths (post-install, target-switch) on one engine (WS-2).
- **Non-goal:** capturing both platforms in one pass (explicitly dropped — owner chose "A").
- **Non-goal:** making the hub main-menu tiles functional (still out of WS-2 scope).

## Part 1 — Welcome: one platform per pass

### Behaviour

- The platform selector keeps the existing **Android | iOS** segmented look, but:
  - **Default = the active build target.** `EditorUserBuildSettings.activeBuildTarget` →
    `Android` selects Android, `iOS` selects iOS. Any other target (e.g. `StandaloneWindows64`)
    **defaults to Android**, still switchable.
  - Switching changes **which single platform** this pass configures.
- **One CAS-ID field** for the selected platform. On `Next`, only the selected platform's id is
  persisted and applied (`InstallerKeyStore.Set` + `CasIdApplier.ApplyPending` for that platform
  only). The other platform is not entered this pass.
- **Placeholder hint:** a semi-transparent hint shown in the empty field (UI Toolkit TextField
  `placeholder`-style, via a USS class), indicating the expected format. It disappears on input.
- **Prefill (#2.2 preserved):** if CAS already has a real managerId for the selected platform,
  prefill it (the field is valid immediately). Empty otherwise (#2 preserved).

### Validation (strict; locks Next; `demo` is NOT accepted)

Per-platform, data-driven from the catalog (see Data model). Default patterns:

- **Android — bundle / package name (reverse-DNS):**
  `^[a-zA-Z][a-zA-Z0-9_]*(\.[a-zA-Z][a-zA-Z0-9_]*)+$`
  Hint: `com.company.gamename`
- **iOS — numeric App Store id:** `^[0-9]+$`
  Hint: `1234567890`

`Next` is enabled only when the current field matches the selected platform's regex. An invalid
value shows a red hint and keeps `Next` locked. The CAS test value `demo` does not match either
pattern, so it cannot proceed (owner decision).

### Code shape

- `WelcomeScreen` holds **one** active platform (replacing the `_android`/`_ios` dual buffers and
  the swap logic). The active platform initialises from a new helper
  `PlatformDetect.ActivePlatform()` → `"Android"` | `"iOS"` (active build target, else `"Android"`).
- Replace `CanProceed(android, ios)` with a pure validator
  `internal static bool IsValid(string value, string regex)` →
  `!string.IsNullOrEmpty(value?.Trim()) && Regex.IsMatch(value.Trim(), regex)`. The caller picks the
  regex by the selected platform (from the catalog). Unit-tested with the default patterns.
- `Seed` / `ResolveSeed` (from Task 1) stay: prefill only a real existing managerId, never the
  stored-but-unapplied value — applied now to the single selected platform.
- The UPM / Git install-method radio is unchanged.

## Part 2 — Build-target-switch auto-open (merged WS-2)

### One engine, two triggers

Unify auto-open behind a single entry point (the WS-2 work). The engine opens the installer
window and routes to a target screen; callers supply the reason:

- **Trigger 1 — post-install** (WS-2 / #7): after an install completes, open and land on the
  **Components** tab. (Decided in `2026-06-29-installer-feedback-round2-decisions.md`.)
- **Trigger 2 — build-target change** (this feature): open and land on **Welcome**, which
  naturally preselects the now-active platform (Part 1's default-from-active-target).

### Trigger 2 condition (owner chose "A")

On `IActiveBuildTargetChanged.OnActiveBuildTargetChanged(prev, next)` (a class implementing the
Unity interface, with a `callbackOrder`):

1. Map `next` → platform: `Android`→"Android", `iOS`→"iOS"; any other target → **do nothing**.
2. Open the wizard **only if** both hold:
   - CAS is **installed** (the catalog CAS external resolves to installed — reuse the scanner /
     `CasIdApplier` asset presence), AND
   - the new platform's CAS id is **not configured** — `CasIdApplier.ReadExisting(platform)`
     returns null (empty or still the `demo` placeholder).
3. Otherwise do nothing (no nag when the platform is already configured, or CAS isn't installed —
   the normal install flow covers a fresh project).

Because the build target has already changed when the callback fires, opening Welcome shows the
new platform by default (no extra platform-passing plumbing needed).

### Dependency: the `IntroDone` gate

`InstallerWizardWindow.ShowIfReportChanged` returns early when `IntroDone` is set
(`InstallerWizardWindow.cs:105`) — the verified root cause of #7's "won't auto-open after first
setup." This gate would also suppress Trigger 2. WS-2's fix (make auto-open reliable after
first-run without re-popping on unrelated manual UPM changes) is therefore a **prerequisite**;
this feature lands together with WS-2 so both triggers share the corrected engine.

## Data model (catalog)

Extend the CAS `config` entries in `catalog.json` (currently `platform`, `kind`, `assetPath`,
`field`, `placeholder`, `label`, `openMenu`) with two optional fields the wizard reads:

- `regex` — the per-platform validation pattern (Android bundle / iOS numeric above).
- `hint` — the placeholder hint text shown in the empty field.

`placeholder` keeps its existing meaning (the "not configured" sentinel `demo`, used by
`CasIdApplier`/`SetupChecker`), distinct from `hint` (UI affordance). The `ConfigRequirement`
model (`Catalog.cs`) gains `Regex` and `Hint` string fields. The wizard falls back to the
built-in default patterns if the catalog omits them (so a stale metadata package still validates).

## Edge cases

- **Active target neither Android nor iOS:** Welcome defaults to Android (switchable); Trigger 2
  ignores switches to such targets.
- **CAS not installed:** Trigger 2 does nothing; Welcome still works as the install entry point.
- **Switching platform inside Welcome:** re-evaluates validation/hint for the newly selected
  platform; the field reloads that platform's prefill (real existing id) or empties.
- **Stale metadata (no `regex`/`hint`):** wizard uses built-in default patterns/hints.
- **Rapid target flips:** the trigger is idempotent — `Open()` via `GetWindow` focuses the
  existing window rather than stacking.

## Testing

- `IsValid` pure unit tests (EditMode): Android bundle accept/reject (`com.a.b` ✓, `com` ✗,
  `demo` ✗, `1.2` ✗), iOS numeric accept/reject (`1234567890` ✓, `demo` ✗, `12a` ✗, empty ✗).
- `PlatformDetect.ActivePlatform` mapping: testable pure overload taking a `BuildTarget`
  (Android→"Android", iOS→"iOS", other→"Android").
- Trigger-2 decision as a pure function
  `ShouldOpenOnSwitch(bool casInstalled, string existingId)` → open iff `casInstalled && existingId
  is null`, unit-tested.
- Window open / build-target callback / actual Unity validation visuals: **owner-run in Unity**
  (no headless runner).

## Relationship to prior work

- **Supersedes** the dual-field capture from Task 1's `WelcomeScreen` (single active platform now);
  keeps Task 1's #2 empty-field and #2.2 prefill policies.
- **Supersedes** the landing-only `2026-06-25-installer-auto-open-hub-design.md` open decision and
  folds it into the unified engine (Trigger 1 → Components).
- Touches feedback items **#2 / #2.2** (Welcome) and **#7** (auto-open).
