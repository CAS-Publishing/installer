# CAS Hub Installer Wizard — morning review checklist

Built overnight on branch **`feat/installer-wizard-ui`** (4 commits). This is **iteration 1**:
the full visual design + screen navigation + **stub data only**. No Scanner/Migrator logic is
wired yet — every button is a stub (`Debug.Log`) or a screen transition.

**I could not run Unity**, so nothing here is visually verified by me. The icons (the one thing
I *could* check) rasterized correctly. Everything else needs your eyes.

---

## 0. First: let Unity import + compile

1. Focus the Unity Editor. It will import the new assets (UXML/USS/PNG/TTF) and **generate `.meta`
   files** for them (these are new/untracked — that's expected; we commit them after you confirm).
2. Watch the **Console** for compile errors. The wizard lives in its **own assembly**
   (`PSV.Installer.Wizard.Editor`), so even if it failed to compile, the **old IMGUI window is safe**.
3. Open it: **menu `PSV Game Studio → Installer Wizard (Preview)`**. A fixed **480×560** window titled
   "CAS Hub Installer" should appear.

**Sanity check the old window still works:** `PSV Game Studio → Installer` (the original IMGUI one)
should open and behave exactly as before. I did not touch any file under `Editor/Ui/`.

---

## 1. Per-screen checklist

Use the **DEV dropdown** at the top of the window ("jump to screen") to inspect each screen, and the
in-screen buttons to test navigation.

| Screen (dropdown id) | What to verify | Buttons → |
|---|---|---|
| **welcome** | CAS logo lockup, title, 3 radio method rows (Git selected), Git URL field, Load Information | Next → integration; clicking a method row moves the radio dot |
| **integration** | Green "auto" card (robot icon) selected, blue-on-select "manual" card (gear), amber warning box | clicking cards toggles selection; Cancel → welcome; Continue → progress (auto) / components (manual) |
| **components** | 7-row table, status dots (green/yellow/red), vendor logos, action buttons (Install/Update/Up to date), Auto-Init checkboxes, zebra rows, legend | Refresh (logs); **Continue** (I added this — design had none) → progress |
| **hub** | 4 tiles, first blue with wand icon + white chevron, others grey icon boxes | auto → progress; Open CAS Settings → settings; Check for Updates → update; rest log |
| **progress** | 9 steps (green ✓ done, **spinning** ring on active, hollow dots waiting), blue progress bar at **56%** | Cancel → hub; "Simulate finish →" → done |
| **done** | Big green check, "All Done!", 3 stacked buttons | Open CAS Settings → settings; Close → closes window |
| **update** | Current 1.2.3 / Latest 1.2.5 (green), download icon, "What's new" bullets, info box | Later → hub; Update Now (logs) |
| **settings** | Breadcrumb, "Open CAS Settings Window" button (gear + external icon), config bullets, blue "Tip:" box, white CAS logo card | breadcrumb "CAS Settings" → hub; Open button logs |

**The spinner on the Progress screen** is the only animation — it should rotate continuously.

---

## 2. Known risks & how to fix (in likely-to-hit order)

1. **A screen looks unstyled / wrong colors.** Means `theme.uss` didn't apply. Check the Console for a
   USS import error and which line. All colors are literal hex (no variables), so a single bad property
   is just warned-and-ignored — tell me the warning and I'll fix that line.

2. **An icon is blank.** The PNG path in `theme.uss` (`url("../Icons/<name>.png")`) didn't resolve, or
   the PNG didn't import. Confirm the file exists under `Editor/Wizard/Icons/`. (I verified all 19
   rasterized correctly.) White-stroke icons are invisible on white but show on the dark/colored boxes.

3. **Font looks off (too thin/bold/plain).** Inter is a **variable** TTF; Unity may import a default
   instance oddly. The window falls back to the editor font automatically if the asset is missing, so
   text is always readable. If the weight looks wrong, say so — I can swap to static Inter weights.

4. **Chevrons (›) look like small square corners.** The `rotate` USS property may behave differently in
   your exact 2022.3 patch. Cosmetic only — easy to swap for a "›" text glyph.

5. **The Git URL field has a double background / odd inner box.** The inner-input USS selector
   (`.unity-base-text-field__input`) class name can vary by version. Cosmetic; tell me and I'll adjust.

6. **A screen's content is clipped at the bottom.** The window is locked to 480×560. If any screen is
   taller, I'll either relax `maxSize` or trim spacing. (I estimated all fit, but couldn't measure.)

7. **Compile error in the wizard assembly.** The old window still works. Paste the error(s) and I'll fix
   — the wizard code is isolated under `Editor/Wizard/`.

---

## 3. What's intentionally NOT here (iteration 2)

- No real logic: install method, auto/manual behavior, real component scan, Auto-Init meaning, live
  progress, Reset, Tenjin/CAS redirects, real version/changelog, self-update — all stubs.
- The navigation graph is my design (the mockup had no transitions). I **added a "Continue" button** to
  the Components screen and a "Simulate finish" button to Progress purely to make the flow walkable.
- The fade-between-screens transition was deliberately removed (it risked leaving a screen invisible if
  the animation didn't tick before first layout). Switches are instant; we can add a safe fade later.

---

## 4. How we proceed

Tell me, per screen, **what's wrong or what to refine** (spacing, colors, copy, layout). Iteration 2 is
where we wire each screen to the real Scanner/Migrator/Catalog/CatalogUpdater and resolve the
design↔capability gaps (Auto-Init, per-row vs batch actions, real progress, etc.).
