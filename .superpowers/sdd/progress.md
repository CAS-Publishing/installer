# SDD progress — WS-4 EDM4U/Android build templates (#6)

Plan: docs/superpowers/plans/2026-06-29-ws4-edm4u-android-build-templates.md
BASE: 1644a16
(Prior this session: Plans 1+2, WS-1, WS-5, WS-3, WS-8 complete on-branch. WS-4 = LAST in batch.)

- Task 1: complete (commit 1df2252, review clean+safe; overwrite:false, graceful; GradleTemplates subpath owner-verify)
- Task 2: complete (commit 5423128, controller-verified; banner renders only when missing>0, USS present)
- Task 3: complete (commit d36b8f3, verified; Ensure first in StartAll)
- Task 4: complete (commit c70a99b preview.33, verified)

## WS-4 COMPLETE (2026-06-29) — BATCH WS-3/WS-8/WS-4 ALL DONE
WS-4: AndroidBuildTemplates(pure+tests)+AndroidBuildFix(copy Editor defaults, overwrite:false, graceful) + Configuration banner + AutoInstaller.StartAll hook. installer preview.33. Task1 copy-safety reviewed clean; banner+hook self-verified.
Session batch all on-branch unreleased. OWNER-RUN: Unity compile+EditMode(all new test files)+visual+behaviour; confirm GradleTemplates Editor subpath (degrades gracefully); then sign+publish installer preview.33 + metadata preview.23.

# SDD progress — Configuration active-platform + network checkboxes
Plan: docs/superpowers/plans/2026-06-29-configuration-active-platform-and-network-checkboxes.md
BASE: 591fbb7
NOTE: No CLI test runner — Unity EditMode tests + compile are OWNER-RUN. Implementers transcribe code+.meta+commit; do not launch Unity.
Task 1: complete (commit 7f28b6e, review clean; ⚠️ internals-visible resolved via Wizard AssemblyInfo InternalsVisibleTo + Tests asmdef ref)
Task 2: complete (commit 5c62b62, review clean; Approved). Minor (spec-inherited, no fix): double GetField; null member→GetValue (safe, caught); IsSolutionInstalled reads audience=0 — verified non-issue (solutions array fixed, installedVersion from XML presence, audience-independent; matches old IsFamiliesActive). Old SelectSolution/IsFamiliesActive untouched (removed in Task 5).
Task 3: complete (commit f338b07, review clean; Approved). Minor: real path is Editor/Wizard/Uxml/Setup.uxml (plan said Editor/Wizard/Setup.uxml) — correct file edited, no action.
Task 4: complete (commit ded5c60, review clean; Approved). Minor (pre-existing, not introduced): double comment block above BuildCasConfig — cosmetic, final-review triage.
Task 5: complete (commit e82d3c2, review clean; Approved). Grep-verified zero orphaned refs to SelectSolution/IsFamiliesActive/SetupScreen.SetSegActive; WelcomeScreen.SetSegActive untouched. Net -120 lines.

## ALL 5 TASKS COMPLETE (2026-06-29). Feature commits: b9c6b66(RefreshInspector fix) + 7f28b6e,5c62b62,f338b07,ded5c60,e82d3c2. Minor roll-up for final review: (T2) double GetField / null-member-to-GetValue / IsSolutionInstalled audience=0 — all spec-inherited, verified non-issues; (T4) pre-existing double-comment above BuildCasConfig. OWNER-RUN: Unity compile + EditMode tests (SetupScreenPlatformTests) + visual/behaviour verify (single platform, independent checkboxes, XML+CAS checkbox live).

## AUDIT preview.25→.33 + FIXES (2026-06-29). 4 parallel area audits (A cas-core, B config-ui, C welcome/autoopen, D migrate/android/git) vs published .25 baseline (34bf7be). 1 Critical + 6 Important + perf/edge. Fixes committed 3c574fc(SwitchToUpm UpdatePackageVersion + GitRefusalMarker const + BackupAndDelete doc), a7cbd76(Android banner platform-gate), c8b8c44(CAS write warnings), 9f765ba(git-pkg muted Installed(git)), 4fbe4dc(OpenAtWelcome guards + BuildTargetWatcher coalesce + WelcomeScreen valCache). DEFERRED: #8 ProjectScanner-per-switch detection change (registered!=installed risk; mitigated by #11 coalesce). Final opus verify of e82d3c2..4fbe4dc: all 7 resolved clean, Ready to publish Yes. Audit non-issue: IsSolutionInstalled audience=0 (read-path audience-independent, confirmed). Minor cosmetic NOT fixed (dead WS-1 USS classes, double-comment, etc).
