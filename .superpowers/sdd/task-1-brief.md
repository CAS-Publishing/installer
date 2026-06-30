### Task 1: `PickForPlatform` pure helper + unit test

A pure helper that selects one of two per-platform values from the platform string. Used by Task 4 to pick the active platform's status cells; isolated here so it is unit-testable without constructing UI.

**Files:**
- Modify: `Editor/Wizard/Screens/SetupScreen.cs` (add an `internal static` helper)
- Create: `Editor/Tests/SetupScreenPlatformTests.cs`
- Create: `Editor/Tests/SetupScreenPlatformTests.cs.meta`

**Interfaces:**
- Produces: `internal static T SetupScreen.PickForPlatform<T>(T android, T ios, string platform)` — returns `ios` when `platform == "iOS"`, otherwise `android`.

- [ ] **Step 1: Write the failing test**

Create `Editor/Tests/SetupScreenPlatformTests.cs`:

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

Create `Editor/Tests/SetupScreenPlatformTests.cs.meta`:

```
fileFormatVersion: 2
guid: 4f8a2c1d9e6b47338a05c2e1d7b9043f
MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData:
  assetBundleName:
  assetBundleVariant:
```

- [ ] **Step 2: Add a compiling stub so the test fails (not errors)**

In `Editor/Wizard/Screens/SetupScreen.cs`, add near the other private static helpers (e.g. just above `SetSegActive`):

```csharp
internal static T PickForPlatform<T>(T android, T ios, string platform) => default;
```

- [ ] **Step 3: Run the test — verify it FAILS**

In Unity: `Window → General → Test Runner → EditMode → Run All` (or run the `SetupScreenPlatformTests` class).
Expected: the three tests FAIL (stub returns `default`, i.e. `null`, not `"A"`/`"I"`).

- [ ] **Step 4: Implement the helper**

Replace the stub with:

```csharp
internal static T PickForPlatform<T>(T android, T ios, string platform) =>
    platform == "iOS" ? ios : android;
```

- [ ] **Step 5: Run the test — verify it PASSES**

In Unity Test Runner, re-run `SetupScreenPlatformTests`.
Expected: all three PASS.

- [ ] **Step 6: Commit**

```bash
git add Editor/Wizard/Screens/SetupScreen.cs Editor/Tests/SetupScreenPlatformTests.cs Editor/Tests/SetupScreenPlatformTests.cs.meta
git commit -m "test(installer): add SetupScreen.PickForPlatform helper + tests"
```

---

