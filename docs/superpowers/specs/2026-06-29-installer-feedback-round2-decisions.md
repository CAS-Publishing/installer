# Installer — Cas PUB SDK feedback (round 2): item-by-item decisions

> **Status:** Decision record. Each feedback item reviewed and approved by the owner
> (2026-06-29) via item-by-item brainstorm. Source: `dev/docs/Копия Фидбек по Cas PUB SDK.pdf`.
> Branch context: `feat/installer-wizard-ui`. Next step: writing-plans per work-stream.

## How to read this

The PDF is the owner's **second-round** field test. Its «Итого» marks 1/3/4/5 done,
the rest not — but much of the "not done" list is **already implemented on-branch and
unreleased** (the owner tested an older preview). Each row below states the real code
state and the approved decision. "Release+retest" = no new code, ship the branch and
re-verify in Unity.

## Decisions

### 1 — Install via git URL in UPM
- **Problem:** clients need a git-URL install route, not "normal vs legacy".
- **State:** done on-branch — `InstallMethod` enum (Upm/Git) + Welcome radio + `InstallMethodState`.
- **Decision:** ✅ **Close as done.** Per-SDK route nuance / native `.unitypackage` route NOT pursued.

### 2 — Welcome: single field, lock Next, no return to install screens
- **Problem:** two platform fields confuse single-platform clients; field must be empty; Next locked; never return to the install picker.
- **State:** single field + Android/iOS segments, lock Next (`CanProceed`), Welcome/Integration not tabs — all on-branch. BUT the field still auto-fills.
- **Decision:** ✅ **Field empty by default; prefill ONLY a real existing CAS managerId.**
  Concrete change: in `WelcomeScreen.Seed`, drop the `InstallerKeyStore.Get` re-seed; keep
  only `CasIdApplier.ReadExisting(platform) ?? ""`. The persisted value is still written on
  `Next` (for apply), it just no longer repopulates the field on reopen. (Reconciles #2 + #2.2.)
  Rest of #2 = release+retest.

### 2.1 — CAS id doesn't reach CASSettings without manual reopen
- **(a) Timing:** `OnNext` now calls `CasIdApplier.ApplyPending()` eagerly; post-install
  re-apply on Components rebuild. **Fixed on-branch → release+retest.** Residual edge
  (CAS not yet installed at Next) verified in Unity.
- **(b) UX gap (round-2 #3 "тут же нельзя его вписать"):** Configuration tab is read-only
  (cell click only pings the asset). **Decision:** ✅ **Add an editable inline CAS-id field
  in the Configuration tab** — typing writes to CASSettings immediately via `ApplyPending`,
  no Inspector / no reopen.

### 2.2 — Prefill existing CAS id when CAS pre-installed
- **State:** done (`Seed` → `ReadExisting`).
- **Decision:** ✅ **Close as part of #2** (same mechanism). Release+retest.

### 3 — Remove/uninstall button in Packages tab
- **State:** `WizardActions.Remove` + ghost button per installed component on-branch.
- **Decision:** ✅ **Close as done.** Release+retest. (Remove covers UPM/manifest; Assets
  installs go through Migrate.)

### 4 — unitypackage-installed SDKs invisible → duplicates (CRITICAL)
- **Detection:** reflection over loaded namespaces + catalog `assetMarkers`; classified
  `InstalledOutsideUpm`; Migrate-or-Skip. Done on-branch, owner agrees (#4 marked done).
- **Open issue (round-2 stor.11 "не работает"):** `MigrateExternal` deletes the manual copy
  FIRST under git-guard; freshly-imported (untracked) files make `BackupAndDeletePath`
  refuse → migration aborts. Safe-by-design but reads as broken.
- **Decision:** ✅ **Add a "Delete anyway" fallback.** When git can't recover the files, show
  an explicit second confirm ("these will be permanently deleted, not recoverable") and let
  the user proceed. Unblocks the fresh-import case.

### 4.1 — git-installed package: visible but Fix doesn't work
- **State:** `Classify` maps git-spec → `UpmCurrent`; exact broken-Fix state not reproducible
  from the old screenshot (needs Unity repro).
- **Decision:** ✅ **Show explicit "Installed (git)" state, no misleading Fix; add an optional
  explicit "Switch to UPM" action** (git→registry). Repro the original Fix bug in Unity.

### 4.2 — updating outside the hub flips visibility to "local"
- **State:** cosmetic — `FriendlyVersion` shows "local" for any non-semver spec. Owner "под вопросом".
- **Decision:** ✅ **Fold into 4.1** — show accurate source (registry / git / embedded) instead
  of a blanket "local". Low priority.

### 4.3 — CAS stable ≠ latest (update-nag conflict)
- **Problem:** hub pins a CAS version; CAS's own updater then nags → the pin feels pointless.
- **Decision:** ✅ **Keep the catalog pin but at the current stable CAS = 4.7.4 (bump now).**
  Policy: keep the pin in sync with CAS stable (manual catalog bump; auto-tracking CAS API
  deferred). Bonus: 4.7.4 also addresses the CAS half of #5.

### 4.5 — auto-config CAS itself (networks + ad types) on automatic setup
- **State:** only managerIds written; ad networks/types not configured.
- **Decision:** ✅ **Separate brainstorm later** (minimal-set vs 1C-sync undecided). No direction fixed now.

### 5 — "Missing signature" warning (critical)
- **Problem:** Unity 6.3 flags unsigned packages.
- **Decision:** ✅ **Bump CAS→4.7.4 first and verify the warning is gone** (owner suspects it
  came from CAS 4.7.0 in the installer). Signing the rest of our hosted packages
  (Firebase, Tenjin, installer, metadata, analytics) via the `unity-package-signing` process
  is a **separate later** ops task.

### 6 — EDM4U resolves aar but doesn't enable gradle/manifest → won't build
- **State:** installer does NOT touch Android build settings (confirmed).
- **Decision:** ✅ **Auto-enable the required gradle/manifest templates during auto-integration**
  (Custom Main Manifest + main/launcher/base gradle + gradle properties + settings templates),
  PLUS a Configuration-tab check with a one-click "Enable Android build settings" Fix.

### 7 — auto-open the hub after install (WS-2)
- **(a) Reliability:** fix the verified root causes — `IntroDone` permanently gating
  `ShowIfReportChanged`, post-install routing to `done→components` never `hub`, fresh-project
  `Bootstrap.RunOnce` returning without UI.
- **(b) Landing screen:** **Decision:** ✅ **Open the Components tab after install** (all-real,
  lowest-risk; "main menu" = the tabbed hub). Hub tiles / `HubActionsScreen` stay out of scope.
- Supersedes the open decision in `2026-06-25-installer-auto-open-hub-design.md`.

### new #8 — Migrate confirm dialog and the shared Assets/Plugins folder
- **State:** signature-matched scattered files already auto-deleted; marker-matched files in
  the shared `Assets/Plugins` listed as "NOT deleted — remove by hand" (the screenshot dialog).
- **Decision:** ✅ **Auto-delete the precisely-matched Plugins files too.** Upgrade Plugins-file
  matching to signature/known-filename (e.g. `libFirebaseCpp*.a`) and auto-delete under the same
  git-guard + "Delete anyway" fallback (#4), so the user never prunes `Assets/Plugins` by hand.

## Work-stream mapping & order

- **WS-0 — Release + retest (no new code):** #1, #2 (existing), #2.2, #3, #2.1(a), #4 detection.
  Plus the small #2 `Seed` fix. Ship the branch, owner re-verifies in Unity.
- **WS-A — Catalog/version:** #4.3 bump CAS pin → 4.7.4; #5 verify signature warning clears.
- **WS-1 — CAS-id edit:** #2.1(b) editable CAS-id field in Configuration.
- **WS-3 — git source clarity:** #4.1 "Installed (git)" + "Switch to UPM"; #4.2 accurate source label.
- **WS-8 — Migrate safety:** #4 "Delete anyway" fallback; new#8 auto-delete matched Plugins files.
- **WS-2 — Auto-open:** #7 reliability + land on Components.
- **WS-4 — Android build:** #6 auto-enable gradle/manifest + Configuration check/Fix.
- **WS-5 — CAS auto-config:** #4.5 — own brainstorm, not scheduled here.
- **WS-6 — Signing rollout:** #5 rest-of-packages — own ops task, not scheduled here.

Suggested order: WS-0 → WS-A → (WS-1, WS-3, WS-8, WS-2, WS-4 batched) → later WS-5, WS-6.
