# Task 1 Report: PickForPlatform Pure Helper + Unit Test

## Status: DONE

### Implementation Summary

Added a pure generic helper method `PickForPlatform<T>` to `SetupScreen.cs` (namespace `PSV.Installer.Wizard`) that selects one of two platform-specific values based on a platform string. The helper is `internal static` so it is visible to tests in `PSV.Installer.Tests`.

### Files Changed

1. **Modified:** `Editor/Wizard/Screens/SetupScreen.cs`
   - Added helper at line 370-371 (before existing `SetSegActive` method)
   - Signature: `internal static T PickForPlatform<T>(T android, T ios, string platform) => platform == "iOS" ? ios : android;`

2. **Created:** `Editor/Tests/SetupScreenPlatformTests.cs`
   - 3 test cases covering iOS, Android, and unknown platform behavior

3. **Created:** `Editor/Tests/SetupScreenPlatformTests.cs.meta`
   - Standard Unity MonoImporter metadata with guid: `4f8a2c1d9e6b47338a05c2e1d7b9043f`

### Test Code

```csharp
using NUnit.Framework;
using PSV.Installer.Wizard;

namespace PSV.Installer.Tests
{
    public class SetupScreenPlatformTests
    {
        [Test] public void iOS_picks_ios_value()
            => Assert.AreEqual("I", SetupScreen.PickForPlatform("A", "I", "iOS"));

        [Test] public void Android_picks_android_value()
            => Assert.AreEqual("A", SetupScreen.PickForPlatform("A", "I", "Android"));

        [Test] public void Unknown_platform_defaults_to_android_value()
            => Assert.AreEqual("A", SetupScreen.PickForPlatform("A", "I", "Whatever"));
    }
}
```

### Implementation Details

**Helper location:** Line 370-371 of `SetupScreen.cs`, positioned immediately before the existing `SetSegActive` method (line 373).

**Generic design:** The helper uses a generic type parameter `<T>` to work with any value type (string, int, bool, enum, etc.), making it reusable across different platform-dependent configuration values.

**Platform matching:** Returns `ios` parameter when platform string equals `"iOS"` (exact match); all other platform strings (including `"Android"`, unknown values, null) default to the `android` parameter.

### Why RED/GREEN Testing is Owner-Run

This project has **no CLI test runner** — Unity Editor is the only compiler and test executor. The brief's RED/GREEN cycle (Steps 2-5) cannot be performed headless:
- Step 2: Adding a compiling stub requires Unity to verify syntax
- Step 3: RED test run requires `Window → General → Test Runner` in the Editor
- Step 5: GREEN test run requires the same Test Runner to re-run after implementation

Therefore, the final implementation (Step 4 code) was written directly without the intermediate stub, since we cannot observe the RED state without launching Unity. The owner will run the Test Runner to verify all three tests pass.

### Self-Review Findings

✓ **Correctness:** The helper logic is correct for all three test cases:
  - `"iOS"` returns `ios` value ✓
  - `"Android"` returns `android` value (default case) ✓
  - Any other platform string returns `android` value (default case) ✓

✓ **Namespace:** `SetupScreen` is in `PSV.Installer.Wizard` namespace; tests are in `PSV.Installer.Tests` namespace. The `internal` access modifier allows test visibility per established pattern.

✓ **Generic design:** Properly generic with no constraints; works with any type `<T>`.

✓ **Naming:** `PickForPlatform` is clear and consistent with the domain (platform selection). Parameter names are explicit: `android`, `ios`, `platform`.

✓ **Placement:** Added immediately before `SetSegActive` method (line 370-371), grouped with other private/internal static helpers.

✓ **Test structure:** Matches existing test pattern from `PlatformDetectTests.cs` (same namespace, same import style, expression-bodied test assertions).

✓ **Meta file:** Standard Unity MonoImporter YAML format with required fields. Guid `4f8a2c1d9e6b47338a05c2e1d7b9043f` is the 32-hex value from the brief.

✓ **No YAGNI violations:** The helper is a pure function with no side effects, minimal scope, and will be used by Task 4 (SetupScreen status-cell selection logic).

### Commit

```
7f28b6e test(installer): add SetupScreen.PickForPlatform helper + tests
```

Commit message follows conventional commit format: `test(installer):` scope with package name.

### Next Steps

Owner will:
1. Open `dev/` project in Unity 2022.3.62f3
2. Navigate to `Window → General → Test Runner → EditMode`
3. Run the `SetupScreenPlatformTests` suite
4. Verify all three tests PASS (currently blocked on owner's Unity environment)
