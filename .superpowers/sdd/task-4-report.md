# Task 4 Report — SetupScreen: scope Configuration to active build target platform

## Steps applied

**Step 1 — Cache the header label**
- Added `private readonly Label _thPlat;` field after `_summary` (line 23).
- Added `_thPlat = Root.Q<Label>("setup-th-plat");` in constructor after the `_summary` query (line 31).
- "Before" block matched exactly.

**Step 2 — Resolve platform + fill header at top of Rebuild()**
- Inserted `var platform = PlatformDetect.ActivePlatform();` and `if (_thPlat != null) _thPlat.text = platform;` immediately after `_rowsHost.Clear();` (lines 54–55).
- "Before" block matched exactly.

**Step 3 — Pass platform into row loop and attention count**
- Changed `BuildRow(row, alt: installed % 2 == 1)` → `BuildRow(row, alt: installed % 2 == 1, platform)`.
- Changed `attention += CountAttention(row.Android) + CountAttention(row.IOS);` → `attention += CountAttention(PickForPlatform(row.Android, row.IOS, platform));`.
- "Before" block matched exactly.

**Step 4 — Make BuildRow render one platform column**
- Changed signature from `BuildRow(SetupModel.Row row, bool alt)` → `BuildRow(SetupModel.Row row, bool alt, string platform)`.
- Replaced two-column grid (Component | Android | iOS) with two-column grid (Component | active platform): `BuildPlatformColumn(PickForPlatform(row.Android, row.IOS, platform))`.
- Changed `el.Add(BuildCasConfig())` → `el.Add(BuildCasConfig(platform))`.
- Updated comment from "3-column grid" to "2-column grid (Component | active platform)".
- "Before" blocks matched exactly.

**Step 5 — Make BuildCasConfig render one platform panel**
- Changed signature from `BuildCasConfig()` → `BuildCasConfig(string platform)`.
- Replaced `plats.Add(PlatformConfig("Android")); plats.Add(PlatformConfig("iOS"));` with `plats.Add(PlatformConfig(platform));`.
- Updated comment accordingly.
- "Before" block matched exactly.

## Call site audit

### BuildRow call sites
- Line 88 (in `Rebuild()` foreach loop): `BuildRow(row, alt: installed % 2 == 1, platform)` — UPDATED, passes `platform`.
- No other call sites exist in the file.

### BuildCasConfig call sites
- Line 172 (in `BuildRow`): `BuildCasConfig(platform)` — UPDATED, passes `platform`.
- No other call sites exist in the file.

## Leftover dual-platform renders

No remaining references that render both `row.Android` and `row.IOS` as separate side-by-side columns. All references to `row.Android`/`row.IOS` within BuildRow now go through `PickForPlatform`.

## Mutex segment verification (PlatformConfig)

The segment inside `PlatformConfig` (lines 331–361 in the final file) is completely untouched:
- `var bt = platform == "iOS" ? UnityEditor.BuildTarget.iOS : UnityEditor.BuildTarget.Android;`
- `var families = CasMediation.IsFamiliesActive(bt);`
- `optBtn`, `famBtn`, `Highlight`, `SetSegActive`
- `CasMediation.SelectSolution(bt, false/true)`
- `Highlight(families)`

`PlatformConfig` signature remains `PlatformConfig(string platform)` — unchanged.

## Self-review

- All signatures consistent: `BuildRow(Row, bool, string)`, `BuildCasConfig(string)`, `PlatformConfig(string)` (unchanged).
- No YAGNI additions — no extra methods, fields, or guard logic beyond what the brief specified.
- No edits to the mutex segment.
- `PickForPlatform` used for both the status column (Step 4) and the attention count (Step 3).
- `_thPlat` declared, assigned in ctor, used in `Rebuild()` — all three present.
- The file compiles: `PlatformDetect.ActivePlatform()` was already available on this branch (Task 1/3 dependency).

## Commit

`ded5c60 feat(installer): scope Configuration to the active build target platform`
(`Editor/Wizard/Screens/SetupScreen.cs` — 14 insertions, 11 deletions)
