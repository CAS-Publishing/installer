### Task 7: Version bump + changelog

**Files:**
- Modify: `package.json`
- Modify: `CHANGELOG.md`

- [ ] **Step 1: Bump and document**

In `package.json`: `"version": "0.0.1-preview.27",` → `"version": "0.0.1-preview.28",`

In `CHANGELOG.md`, add above `## [0.0.1-preview.27]`:

```markdown
## [0.0.1-preview.28] - 2026-06-29

- **Auto-open engine (#7 / WS-2):** after first-run, the installer reopens automatically following an
  installer-driven install (landing on Components) via a one-shot reload signal — and no longer
  re-pops on unrelated manual UPM changes.
- **Build-target switch (#7):** switching the active build target to Android or iOS, when CAS is
  installed but that platform's CAS id is unconfigured, auto-opens the wizard at Welcome with the new
  platform preselected. Other targets are ignored.
```

- [ ] **Step 2: Commit**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer"
git add package.json CHANGELOG.md
git commit -m "chore(installer): release notes for auto-open engine + target-switch (preview.28)"
```

---

## Self-Review

- **Spec coverage (Part 2):** unified engine / two triggers → Tasks 1-6; Trigger 1 reliability + IntroDone fix + installer-driven-only → Tasks 1-3; lands on Components → existing `ResolveStartScreen` (Task 3 note); Trigger 2 condition (Android/iOS + CAS installed + unconfigured) → Tasks 4+6; preselect new platform incl. already-open window → Task 5 hint; `OpenAtWelcome` doesn't clear `IntroDone` → Task 5; pure `ShouldOpenOnSwitch` unit-tested → Task 4; `IntroDone` gate is the named prerequisite → Task 3.
- **Placeholder scan:** none — every step has complete code/commands. SessionState-backed helpers are explicitly owner-verified (Unity-only API), with the one pure decision unit-tested.
- **Type consistency:** `InstallReloadSignal.MarkPending`/`ConsumePending` (Tasks 1→2,3); `BuildSwitchPolicy.ShouldOpenOnSwitch(bool, string)` (Tasks 4→6 + tests); `CasPresence.IsInstalled()` (Tasks 4→6); `InstallerWizardWindow.OpenAtWelcome(string)`/`ConsumeRequestedPlatform()` (Tasks 5→6 + WelcomeScreen); `PlatformDetect.FromBuildTarget` (from Part 1) — all consistent. `CasIdApplier.ReadExisting(platform)` returns null/empty for unconfigured, matching `ShouldOpenOnSwitch`'s `string.IsNullOrEmpty` check.
