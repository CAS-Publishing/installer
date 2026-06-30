# Task 6: Catalog `adFormats` flag + version bumps — Completion Report

**Status:** DONE

**Date:** 2026-06-29

---

## Metadata (Repo A)

### Branch
`chore/cas-pin-4.7.4`

### Changes

#### catalog.json Diff
**CAS config rows — both Android and iOS:**

**Android row (added `"adFormats": true`):**
```json
{ "platform": "Android", "kind": "settingsAssetField", "assetPath": "Assets/CleverAdsSolutions/Resources/CASSettingsAndroid.asset", "field": "managerIds", "placeholder": "demo", "label": "CAS ID", "openMenu": "Assets/CleverAdsSolutions/Settings", "regex": "^[a-zA-Z][a-zA-Z0-9_]*(\\.[a-zA-Z][a-zA-Z0-9_]*)+$", "hint": "com.company.gamename", "editable": true, "adFormats": true }
```

**iOS row (added `"adFormats": true`):**
```json
{ "platform": "iOS", "kind": "settingsAssetField", "assetPath": "Assets/CleverAdsSolutions/Resources/CASSettingsiOS.asset", "field": "managerIds", "placeholder": "demo", "label": "CAS ID", "openMenu": "Assets/CleverAdsSolutions/Settings", "regex": "^[0-9]+$", "hint": "1234567890", "editable": true, "adFormats": true }
```

#### Validation Output
```
Android True
iOS True
```

#### Version Bump
- **File:** `package.json`
- **Before:** `0.0.2-preview.21`
- **After:** `0.0.2-preview.22`

#### CHANGELOG Entry
**File:** `CHANGELOG.md`
```markdown
## [0.0.2-preview.22] - 2026-06-29

- Mark CAS config `adFormats` so the installer offers ad-format + audience/network controls (#4.5).
```

#### Commit
```
Commit Hash: 0f3c949
Message: chore(metadata): mark CAS config adFormats (#4.5)
Branch: chore/cas-pin-4.7.4
```

---

## Installer (Repo B)

### Branch
`feat/installer-wizard-ui`

### Changes

#### Version Bump
- **File:** `package.json`
- **Before:** `0.0.1-preview.29`
- **After:** `0.0.1-preview.30`

#### CHANGELOG Entry
**File:** `CHANGELOG.md`
```markdown
## [0.0.1-preview.30] - 2026-06-29

- Configuration: CAS ad-format toggles + audience/network (Optimal/Families) set, with auto settings-asset create (#4.5).
```

#### Commit
```
Commit Hash: 3929e89
Message: chore(installer): release notes for CAS auto-config (preview.30)
Branch: feat/installer-wizard-ui
```

---

## Summary

✓ Both CAS config rows in metadata `catalog.json` marked with `"adFormats": true`
✓ JSON validation confirmed: `Android True`, `iOS True`
✓ Metadata version bumped: `0.0.2-preview.21` → `0.0.2-preview.22`
✓ Metadata CHANGELOG updated
✓ Metadata committed: `0f3c949`
✓ Installer version bumped: `0.0.1-preview.29` → `0.0.1-preview.30`
✓ Installer CHANGELOG updated
✓ Installer committed: `3929e89`

All task steps completed successfully.
