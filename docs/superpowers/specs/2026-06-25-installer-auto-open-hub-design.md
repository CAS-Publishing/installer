# Installer auto-open after install — Problem & Decision Record

> **Status:** Problem fixed in scope; **one open decision pending reviewer agreement** (what screen to open). Not yet a complete design — do NOT start a plan until the open decision is resolved.

> Work-stream **WS-2** of the 2026-06 client-feedback backlog. Solves feedback item **#7** only.

## The problem (verbatim feedback)

**Round 1, item 7:**
> «При установке хаба автоматически открыть главное меню, Wizard уже для последующих настроек если он нужен. Сейчас хаб не открывается вовсе или открывается после перезапуска unity»

**Round 2, item 1 (same issue, re-reported on the newer build):**
> «Не открылось окно установщика после установки компонентов через UPM (пункт 7 который был описан раньше)»

In one sentence: **after an install the installer window does not open by itself (or only after a Unity restart),** and when it does the client wants it to land on a *main menu*, with the first-run wizard (Welcome → Integration) reserved for initial setup only.

This is **only** about the window opening reliably to the right screen. The *contents/behaviour of individual main-menu tiles* (Reset All, Tenjin Dashboard, Docs, Support) are **out of scope** — no feedback item mentions them.

## Root causes (verified against current code, branch `feat/installer-wizard-ui`)

1. **The "main menu" screen is dead code.** `HubActionsScreen` (id `"hub"`) is registered (`Editor/Wizard/InstallerWizardWindow.cs:180`) and listed in `ScreenOrder` (`:25`), but **no navigation ever routes to it**. The screen the client pictures as the "main menu" exists yet is unreachable.

2. **Post-install navigation never reaches the hub.** When an auto-install finishes, `ProgressScreen` routes to `"done"` (`Editor/Wizard/Screens/ProgressScreen.cs:156`), and `DoneScreen`'s "Finish" routes to `"components"` (`Editor/Wizard/Screens/DoneScreen.cs:35`). Never to `"hub"`.

3. **`IntroDone` permanently suppresses auto-open.** `InstallerWizardWindow.ShowIfReportChanged` returns early when `IntroDone` is set (`:105`). Once a project has passed the first-run intro, the window **never auto-opens again on a domain reload** — so an installer-driven UPM install that triggers a reload brings no window back up. This is the direct cause of "didn't open after installing components via UPM." `IntroDone` is set as soon as the user commits the install (`Editor/Wizard/AutoInstaller.cs:83`, `Editor/Wizard/Screens/IntegrationModeScreen.cs:56`).

4. **Fresh project needs an extra reload before any UI.** On a project without metadata, `Bootstrap.RunOnce` installs the metadata package and returns **without opening UI** (`Editor/Bootstrap.cs:40-41`); the window can only appear on the *next* reload, once the catalog loads. Partly mitigated already (the report hash is now stored *after* `Open()`, see the comment at `InstallerWizardWindow.cs:108-111`), but the metadata-install reload still adds a perceived delay.

## Scope of WS-2

In scope:
- Make the chosen landing screen **reachable** after install (fix root causes 2 + 1).
- Make auto-open **reliable** after an installer-driven install, without re-popping the window on unrelated/manual UPM changes (the original reason `IntroDone` gated it).
- Keep the first-run wizard (Welcome → Integration) for initial setup only.

Out of scope (separate work-streams / not requested by any feedback item):
- Making individual main-menu tiles functional (Reset All, Tenjin Dashboard, Docs, Support).
- Any redesign of the hub layout.

## OPEN DECISION — to agree with reviewers

**What screen should the installer open after install ("the main menu")?**

The choice changes scope because the candidate screens differ in how finished they are:

- **Option A — `HubActionsScreen` (tiles).** Matches the client's wording ("главное меню"). But several tiles are currently stubs (Settings → a screen whose main button only logs; Reset All / Tenjin / Docs / Support → log-only). Landing here surfaces non-functional buttons unless they are hidden or wired — which is itself out of WS-2 scope. *(Client's tentative pick, pending reviewers.)*
- **Option B — `Components` tab.** Everything on it is real (per-package status + actions). Smaller, lower-risk change; the "main menu" is the tabbed hub. Does not surface any stub.

Reviewers to confirm: which landing, and — if Option A — whether the stub tiles are hidden for now or promoted to their own work-stream.

## Next step

Once the landing decision is signed off, finish this into a full design (auto-open trigger/guard semantics + navigation wiring), then proceed to a writing-plans implementation plan. Until then this record stands as the agreed problem statement.
