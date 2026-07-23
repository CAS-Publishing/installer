# Firebase legacy migration + package registry fix — design

**Date:** 2026-07-23
**Source:** field-test feedback on `0.0.1-preview.37` (Slack, 4 testers; triage 2026-07-23).
**Scope:** two approved fixes — (1) compound migration `com.psv.firebase.base` → native Firebase + adapters, (2) "Package cannot be found" for catalog packages. Plus two metadata-only riders approved in the same triage: EDM legacy detection (`com.psv.unity.edm`) and the CAS 4.6.6 rollback.

## Verified root causes

1. **"Package cannot be found" (`com.psvgamestudio.analytics@0.0.1-preview.3`, `com.psvgamestudio.remoteconfig@0.0.1-preview.2`).** Both versions exist on Verdaccio and are anonymously readable (verified). Three stacked causes:
   - `MigrationPlanner.PlanForPackage` never emits registry actions (only `PlanForExternal` does), so when the installer itself arrived via git URL (install method B) the manifest has no PSV scoped registry and the added dependency cannot resolve. Unity then fails the whole resolve, cascading into unrelated errors (e.g. `CS0234 Firebase.Crashlytics`).
   - Package classification has no "dependency present but scope missing" state, so already-broken manifests show no recovery action.
   - Both adapters declare `com.psv.core: *`, and `com.psv.core` is NOT published to Verdaccio (404, verified) — it exists only as a git dependency in PSV projects. Adapters are therefore uninstallable in projects without core.
2. **Firebase migration produces duplicates.**
   - *Additional components path:* both adapters share `legacyNpmIds: ["com.psv.firebase.base"]`, forming a split group. The wizard applies with `SingleSelection`, so the planner's partial-split backstop (`MigrationPlanner.Plan`, removes.RemoveAll) silently drops `Remove(com.psv.firebase.base)` on every Apply — adapter lands next to the legacy package → duplicate asmdef `PSV.FirebaseServices`. "Fix" replans identically, so it never converges.
   - *Main components path:* the Firebase external record has no legacy linkage to `com.psv.firebase.base`, so Install adds native modules alongside the legacy package's vendored Firebase libs.
   - *PSV EDM:* the catalog `uninstall` rule for `com.psv.unity.edm` is only consumed by the legacy IMGUI report view; the wizard's `SingleSelection` never selects uninstall entries, so PSV EDM is never removed.

## Design

### Part 1 — registry fix for catalog packages

1. **`PlanForPackage` ensures the registry.** For every state that emits `AddPackage`/`UpdatePackageVersion` (`NotInstalled`, `LegacyUpm`, `LegacyAssets`, `Conflict`, `UpmOutdated`, `UpmBelowMin`), first emit `AddScopedRegistry(registryName, url, scope)`. URL resolves via the existing `catalog.registries[record.Registry]` lookup. Scope comes from a new optional `scopes` field on `packages[]` records; when absent, default to `record.Id` (an exact package-name scope is always correct and never over-captures). `ManifestWriter` already merges scopes into an existing registry block by URL, so no duplicates for either install method.
2. **New package-side "scope missing" handling** (parity with `ExternalState.ScopeMissing`): dependency present in manifest, no covering scope registered → status "Needs registry", action **Fix** emitting only the registry action. This is the self-heal path for the three broken tester projects.
3. **`com.psv.core` gate.** Adapters are offered in Additional components only when core is present — in manifest dependencies OR as an embedded package folder (`Packages/com.psv.core`). Without core the row is non-actionable with status "Requires PSV Core". Driven by a new optional `requires` field on `packages[]` records (`["com.psv.core"]`); records without `requires` behave as today.

   `com.psv.core` itself is a legacy git-distributed package that is being gradually decomposed; it is NOT planned for the npm registry. The installer therefore NEVER installs, resolves, or registers a scope for it — `requires` is a pure presence check, nothing more. No scoped-registry entry for `com.psv.*` is ever written.

Edge cases covered by tests: registry already present under a different name but same URL (merge, not duplicate); git-installed adapter left untouched; catalog record with unknown/missing registry key (warning, no crash); core embedded without a manifest entry.

### Part 2 — compound Firebase migration

New pure plan builder `FirebaseMigrationPlan` (no I/O, unit-testable; sibling of `MigrationPlanner`, modeled on the dedicated-path precedent of `WizardActions.MigrateExternal`), executed by `WizardActions.MigrateFirebaseLegacy()`:

1. **Trigger.** Scan detects `com.psv.firebase.base` in manifest → both entry points show **Migrate**: the "Firebase Analytics" row in Main components and the adapter rows in Additional components. Both invoke the same compound flow; one confirm window lists the full action set.
2. **Plan contents, in order (removes → registry → adds):**
   - `Remove(com.psv.firebase.base)`;
   - `Remove(com.psv.unity.edm)` when present in manifest — sourced from the catalog `uninstall` rules, not hardcoded;
   - `AddScopedRegistry` for the psv registry (Part 1 mechanics);
   - native Firebase modules by detection — reuse `ResolveInstallSet` (loaded-type markers `Firebase.Analytics` / `Firebase.RemoteConfig` / `Firebase.Installations`); the base chain always installs so EDM arrives transitively;
   - adapters by rule: `analytics` when core is present; `remoteconfig` when core is present AND RemoteConfig is detected. Without core: native-only migration with a warning in the confirm window.
3. **Split-backstop bypass.** The compound plan is built directly and applied via `MigrationRunner`, not through the generic planner selection, so the partial-split backstop cannot drop the legacy removal. The backstop itself stays untouched for other scenarios.
4. **Failure safety.** All actions are manifest-level and applied in one `ManifestWriter` pass, as today — no partial asset deletion is involved in this flow.
5. **Conflict recovery.** The adapter `Conflict` state (legacy + canonical simultaneously — the state the testers are already in) routes to the same compound migration, so "Fix" converges instead of replanning the dropped remove forever.

### Part 3 — metadata changes (ships as `0.0.2-preview.27`)

- `packages[]`: explicit `scopes: ["com.psvgamestudio"]` on both adapters; `requires: ["com.psv.core"]` on both adapters. Old installers ignore unknown fields — no compat break.
- EDM external record: `legacyManifestIds: ["com.psv.unity.edm"]` so custom-EDM projects show "Installed (legacy)" instead of "not used" (approved triage item 4).
- Same release, separate commit (approved triage item 5, outside this design's core): CAS `recommendedVersion` 4.7.4 → 4.6.6 and matching `git.packages[].tag` 4.6.6.

## Testing

Unity Test Framework, `Editor/Tests/`:

- Planner registry emission for every package state; scope default = record id; scope merge into an existing registry block; git-installed adapter untouched.
- "Needs registry" classification + Fix plan against manifest fixtures reproducing the three broken tester projects (dependency present, no registry).
- `FirebaseMigrationPlan` detection matrix → exact plan contents for each combination: Analytics-only / Analytics+RC / no core / `com.psv.unity.edm` absent from manifest.
- Regression: the partial-split backstop still holds for other split groups via the generic planner.

## Rollout

Installer `0.0.1-preview.38` (signed → Verdaccio) + metadata `0.0.2-preview.27`. Recovery for broken projects: Hub pulls the latest catalog, shows "Needs registry"/Fix and Firebase "Migrate" — self-heal without hand-editing manifests. Release only after owner confirmation.

## Out of scope

Remaining approved triage items (Android Settings menu names, auto-refresh after Install, Hub auto-open without restart, top-level "CAS Hub" menu) are separate fixes with their own plans. YangoAds in `com.psv.adsmanager` is handed off to that package's owners.
