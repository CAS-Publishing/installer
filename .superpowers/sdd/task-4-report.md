# Task 4 Completion Report

## Status
✓ COMPLETE

## Commit
- **SHA:** ca12be26b34167ea2471a16426ea0a508a49ea14
- **Branch:** feat/installer-wizard-ui
- **Message:** `feat(installer): top-level CAS Hub menu; changelog for UX fixes`

## Changes Made

### 1. InstallerWizardWindow.cs
- Added `OpenFromMainMenu()` method at line 94 (after `OpenFirstRun()`)
- New menu item: `[MenuItem("CAS Hub/Open Hub")]`
- Method delegates to existing `Open()` call — same window, top-level menu access

### 2. CHANGELOG.md
Extended the `0.0.1-preview.38` entry with 4 UX-fix bullets:
1. **CAS native settings** — menu items work on both pre-4.7 ("Android Settings") and 4.7+ ("Android Settings...") CAS versions
2. **Immediate component refresh** — rows update instantly after Install/Update/Fix/Migrate/Remove (no manual Refresh needed)
3. **Transient metadata retries** — catalog install failures auto-retry in-session, Hub auto-opens on first install without editor restart
4. **Top-level menu** — new "CAS Hub → Open Hub" entry in main menu bar; old Assets/CleverAdsSolutions items remain

## No Concerns
All changes align with the task brief verbatim. Commit trailer included as specified.
