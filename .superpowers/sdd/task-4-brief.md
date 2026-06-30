### Task 4: `SetupScreen` — scope the whole screen to the active platform

Resolve the active platform once per rebuild; fill the header; render only that platform's status column and CAS panel; count attention for that platform only.

**Files:**
- Modify: `Editor/Wizard/Screens/SetupScreen.cs`

**Interfaces:**
- Consumes: `PlatformDetect.ActivePlatform()`, `SetupScreen.PickForPlatform<T>` (Task 1), the `setup-th-plat` label (Task 3).
- Produces: `BuildCasConfig(string platform)` and `BuildRow(SetupModel.Row, bool, string platform)` now take the active platform.

- [ ] **Step 1: Cache the header label**

Add a field next to `_summary` and query it in the constructor.

Field (near `private readonly Label _summary;`):

```csharp
        private readonly Label _thPlat;
```

In the constructor, after `_summary = Root.Q<Label>("setup-summary");`:

```csharp
            _thPlat   = Root.Q<Label>("setup-th-plat");
```

- [ ] **Step 2: Resolve platform + fill header at the top of `Rebuild()`**

In `Rebuild()`, immediately after `_rowsHost.Clear();`:

```csharp
            var platform = PlatformDetect.ActivePlatform();
            if (_thPlat != null) _thPlat.text = platform;
```

- [ ] **Step 3: Pass the platform into the row loop and attention count**

Replace the row loop body in `Rebuild()`:

```csharp
            foreach (var row in rows)
            {
                if (!row.Installed) continue; // Configuration lists only installed components
                _rowsHost.Add(BuildRow(row, alt: installed % 2 == 1));
                installed++;
                attention += CountAttention(row.Android) + CountAttention(row.IOS);
            }
```

with:

```csharp
            foreach (var row in rows)
            {
                if (!row.Installed) continue; // Configuration lists only installed components
                _rowsHost.Add(BuildRow(row, alt: installed % 2 == 1, platform));
                installed++;
                attention += CountAttention(PickForPlatform(row.Android, row.IOS, platform));
            }
```

- [ ] **Step 4: Make `BuildRow` render one platform column**

Change the signature and the grid assembly. Replace:

```csharp
        private static VisualElement BuildRow(SetupModel.Row row, bool alt)
        {
```

with:

```csharp
        private static VisualElement BuildRow(SetupModel.Row row, bool alt, string platform)
        {
```

Then, inside `BuildRow`, replace the grid block:

```csharp
            // Status line: the 3-column grid (Component | Android | iOS), read-only.
            var grid = new VisualElement();
            grid.AddToClassList("cas-setup-row__grid");
            grid.Add(comp);
            grid.Add(BuildPlatformColumn(row.Android));
            grid.Add(BuildPlatformColumn(row.IOS));
            el.Add(grid);

            // CAS gets a dedicated, labelled config card BELOW the status grid (formats + audience),
            // instead of crowding the status columns.
            if (row.AdFormats)
                el.Add(BuildCasConfig());
            return el;
```

with:

```csharp
            // Status line: 2-column grid (Component | active platform), read-only.
            var grid = new VisualElement();
            grid.AddToClassList("cas-setup-row__grid");
            grid.Add(comp);
            grid.Add(BuildPlatformColumn(PickForPlatform(row.Android, row.IOS, platform)));
            el.Add(grid);

            // CAS gets a dedicated, labelled config card BELOW the status grid (formats + audience),
            // for the active platform only.
            if (row.AdFormats)
                el.Add(BuildCasConfig(platform));
            return el;
```

- [ ] **Step 5: Make `BuildCasConfig` render one platform panel**

Replace:

```csharp
        private static VisualElement BuildCasConfig()
        {
            var card = new VisualElement();
            card.AddToClassList("cas-cfg");

            var title = new Label("CAS CONFIGURATION");
            title.AddToClassList("cas-cfg__title");
            card.Add(title);

            var plats = new VisualElement();
            plats.AddToClassList("cas-cfg__plats");
            plats.Add(PlatformConfig("Android"));
            plats.Add(PlatformConfig("iOS"));
            card.Add(plats);
            return card;
        }
```

with:

```csharp
        private static VisualElement BuildCasConfig(string platform)
        {
            var card = new VisualElement();
            card.AddToClassList("cas-cfg");

            var title = new Label("CAS CONFIGURATION");
            title.AddToClassList("cas-cfg__title");
            card.Add(title);

            var plats = new VisualElement();
            plats.AddToClassList("cas-cfg__plats");
            plats.Add(PlatformConfig(platform));
            card.Add(plats);
            return card;
        }
```

- [ ] **Step 6: Verify in Unity**

Open Configuration with **Android** active: header reads `Android`, each installed component shows a single status column, and the CAS card shows one `Android` panel. Switch the build target to **iOS** (File → Build Settings) and reopen/Refresh: everything reflects iOS. No Console errors.

- [ ] **Step 7: Commit**

```bash
git add Editor/Wizard/Screens/SetupScreen.cs
git commit -m "feat(installer): scope Configuration to the active build target platform"
```

---

