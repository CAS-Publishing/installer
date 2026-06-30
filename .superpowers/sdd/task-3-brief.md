### Task 3: `Setup.uxml` — single platform header label

Replace the two fixed `Android` / `iOS` header labels with one named label the screen fills per active platform.

**Files:**
- Modify: `Editor/Wizard/Setup.uxml`

**Interfaces:**
- Produces: a `Label` named `setup-th-plat` (classes `cas-th cas-setup-col-plat`) inside `cas-setup-head`.

- [ ] **Step 1: Edit the header row**

In `Editor/Wizard/Setup.uxml`, replace this block:

```xml
            <ui:VisualElement class="cas-setup-head">
                <ui:Label text="Component" class="cas-th cas-setup-col-comp" />
                <ui:Label text="Android" class="cas-th cas-setup-col-plat" />
                <ui:Label text="iOS" class="cas-th cas-setup-col-plat" />
            </ui:VisualElement>
```

with:

```xml
            <ui:VisualElement class="cas-setup-head">
                <ui:Label text="Component" class="cas-th cas-setup-col-comp" />
                <ui:Label name="setup-th-plat" class="cas-th cas-setup-col-plat" />
            </ui:VisualElement>
```

- [ ] **Step 2: Verify in Unity**

Open `PSV → Installer…`, go to the Configuration tab. Expected: the header shows `Component` and one (currently blank — filled in Task 4) platform column; no compile/UXML errors in the Console.

- [ ] **Step 3: Commit**

```bash
git add Editor/Wizard/Setup.uxml
git commit -m "feat(installer): single platform header label in Configuration"
```

---

