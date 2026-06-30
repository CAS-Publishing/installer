# Task 7: Version Bump + Changelog — Report

## Changes Applied

### package.json
```diff
- "version": "0.0.1-preview.27",
+ "version": "0.0.1-preview.28",
```

### CHANGELOG.md
Inserted at top (above `## [0.0.1-preview.27]`):

```markdown
## [0.0.1-preview.28] - 2026-06-29

- **Auto-open engine (#7 / WS-2):** after first-run, the installer reopens automatically following an
  installer-driven install (landing on Components) via a one-shot reload signal — and no longer
  re-pops on unrelated manual UPM changes.
- **Build-target switch (#7):** switching the active build target to Android or iOS, when CAS is
  installed but that platform's CAS id is unconfigured, auto-opens the wizard at Welcome with the new
  platform preselected. Other targets are ignored.
```

## Commit

```
ca77eda chore(installer): release notes for auto-open engine + target-switch (preview.28)
```

Both files staged and committed successfully.
