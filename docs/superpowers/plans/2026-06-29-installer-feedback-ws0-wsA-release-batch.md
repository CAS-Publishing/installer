# Installer feedback — WS-0 + WS-A release batch — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Produce one corrected installer preview build the owner can re-test, by fixing the Welcome CAS-ID auto-fill (#2) and bumping the CAS pin to the current stable 4.7.4 (#4.3, which also clears the CAS "Missing signature" warning #5).

**Architecture:** Two tiny, independent changes in two separate git repos — a pure-function refactor of the Welcome seed policy in `com.psvgamestudio.installer`, and a version-data bump in `com.psvgamestudio.installer.metadata`'s `catalog.json`. Both ship in one preview release; the owner then re-verifies the already-on-branch items in Unity.

**Tech Stack:** Unity 2022.3.62f3 Editor, C# (UPM editor package), NUnit (Unity Test Framework, EditMode), JSON catalog, Verdaccio registry (`https://npm.psvgamestudio.com/`).

## Global Constraints

- Registry for every publish: `https://npm.psvgamestudio.com/` — never npmjs.org.
- Conventional Commits with package scope: `fix(installer):`, `chore(metadata):`, etc.
- Commit all `.meta` files before any `npm publish` (Verdaccio ships tarballs without them; Unity then warns on import).
- Unity 6.3 consumers flag UNSIGNED tarballs — sign every published tarball via the `unity-package-signing` skill before publishing.
- **No CLI test runner exists.** EditMode tests run via Unity: `Window → General → Test Runner → EditMode`. "Run the test" steps below mean running that test in the Test Runner.
- **Subagents cannot run Unity.** Every Unity-side run/verify step is OWNER-RUN; a subagent stops at the boundary and hands back.
- The **dev embedded** `catalog.json` takes effect immediately in `dev/`; CLIENT projects only see catalog changes after the metadata package is **republished**.
- `com.psvgamestudio.installer` git repo branch: `feat/installer-wizard-ui`. `com.psvgamestudio.installer.metadata` git repo branch: `main` (branch before committing).
- Absolute repo roots:
  - Installer: `E:/workspace/casai/dev/Packages/com.psvgamestudio.installer`
  - Metadata: `E:/workspace/casai/dev/Packages/com.psvgamestudio.installer.metadata`

---

### Task 1: Welcome CAS-ID field starts empty (#2)

Drop the "restore the previously-typed value" re-seed so the field is empty on (re)open unless a **real** existing CAS managerId is present (#2 + #2.2). Encode the policy in one pure, tested function.

**Files:**
- Modify: `Editor/Wizard/Screens/WelcomeScreen.cs` (the `Seed` method, lines 58-64)
- Modify: `Editor/Tests/WelcomeScreenTests.cs` (add 3 tests)
- Modify: `package.json` (version bump)
- Modify: `CHANGELOG.md` (new entry)

**Interfaces:**
- Produces: `internal static string WelcomeScreen.ResolveSeed(string storedValue, string existingCasId)` — returns `existingCasId ?? ""`; `storedValue` is deliberately ignored (regression guard against re-adding the re-seed).
- Consumes: existing `CasIdApplier.ReadExisting(string platform)` (returns the real CAS managerId or null) and `InstallerKeyStore.Get(CasId, platform)`.

- [ ] **Step 1: Write the failing tests**

Add to `Editor/Tests/WelcomeScreenTests.cs`, inside the `WelcomeScreenTests` class:

```csharp
[Test]
public void ResolveSeed_ignores_stored_value()
{
    // A previously-typed-but-unapplied id must NOT repopulate the field (feedback #2).
    Assert.AreEqual("", WelcomeScreen.ResolveSeed("previously-typed", null));
}

[Test]
public void ResolveSeed_uses_existing_cas_id()
{
    // A real, already-configured CAS managerId IS prefilled (feedback #2.2).
    Assert.AreEqual("real-cas-id", WelcomeScreen.ResolveSeed("anything", "real-cas-id"));
}

[Test]
public void ResolveSeed_empty_when_no_existing()
{
    Assert.AreEqual("", WelcomeScreen.ResolveSeed(null, null));
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run in Unity: `Window → General → Test Runner → EditMode → run WelcomeScreenTests`.
Expected: the 3 new tests FAIL to compile / are missing — `WelcomeScreen` has no `ResolveSeed` member.

- [ ] **Step 3: Add the pure policy function and rewire `Seed`**

In `Editor/Wizard/Screens/WelcomeScreen.cs`, replace the current `Seed` method (lines 58-64):

```csharp
        // Stored key first; then any real managerId already in CAS settings; else empty.
        private static string Seed(string platform)
        {
            var stored = InstallerKeyStore.Get(CasId, platform);
            if (!string.IsNullOrEmpty(stored)) return stored;
            return CasIdApplier.ReadExisting(platform) ?? "";
        }
```

with:

```csharp
        // The field starts EMPTY on (re)open: a previously-typed-but-unapplied id is NOT restored
        // (feedback #2). Only a REAL existing CAS managerId prefills it (feedback #2.2). The stored
        // value is still WRITTEN on Next (so CasIdApplier can apply it) — it just no longer seeds.
        private static string Seed(string platform) =>
            ResolveSeed(InstallerKeyStore.Get(CasId, platform), CasIdApplier.ReadExisting(platform));

        /// <summary>
        /// Seed policy for the CAS-ID field: the real existing CAS managerId only. The stored value
        /// is intentionally ignored — passed in solely to document (and lock via test) that it is
        /// dropped, not forgotten. Pure/testable.
        /// </summary>
        internal static string ResolveSeed(string storedValue, string existingCasId) =>
            existingCasId ?? "";
```

- [ ] **Step 4: Run the tests to verify they pass**

Run in Unity: `Window → General → Test Runner → EditMode → run WelcomeScreenTests`.
Expected: all tests PASS (the 3 new `ResolveSeed_*` plus the existing `CanProceed_*`).

- [ ] **Step 5: Bump the installer version and changelog**

In `package.json` line 3: `"version": "0.0.1-preview.25",` → `"version": "0.0.1-preview.26",`

In `CHANGELOG.md`, insert directly under the `# Changelog` / preamble block, above the `## [0.0.1-preview.25]` entry:

```markdown
## [0.0.1-preview.26] - 2026-06-29

- **Fix (Welcome #2):** the CAS-ID field now starts empty on (re)open; only a real, already-configured
  CAS managerId prefills it (#2.2). A previously-typed-but-unapplied value no longer repopulates the
  field. Policy extracted to `WelcomeScreen.ResolveSeed` and unit-tested.
```

- [ ] **Step 6: Commit (installer repo)**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer"
git add Editor/Wizard/Screens/WelcomeScreen.cs Editor/Tests/WelcomeScreenTests.cs package.json CHANGELOG.md
git commit -m "fix(installer): Welcome CAS-ID field starts empty unless CAS configured (#2)"
```

---

### Task 2: Bump CAS pin to stable 4.7.4 (#4.3, #5)

Move the catalog's CAS `recommendedVersion` and git tag from `4.7.0` to the current stable `4.7.4`. Bump the metadata package version so clients receive it on republish.

**Files:**
- Modify: `E:/workspace/casai/dev/Packages/com.psvgamestudio.installer.metadata/catalog.json` (lines 46 and 49)
- Modify: `E:/workspace/casai/dev/Packages/com.psvgamestudio.installer.metadata/package.json` (version)
- Modify: `E:/workspace/casai/dev/Packages/com.psvgamestudio.installer.metadata/CHANGELOG.md`

**Interfaces:**
- Produces: catalog CAS record with `recommendedVersion = "4.7.4"` and git tag `"4.7.4"`. Consumed by the installer's `MigrateExternal` / install flow at runtime — no code change needed.

- [ ] **Step 1: Create a working branch in the metadata repo**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer.metadata"
git checkout -b chore/cas-pin-4.7.4
```

- [ ] **Step 2: Edit the CAS version fields in `catalog.json`**

Line 46: `      "recommendedVersion": "4.7.0",` → `      "recommendedVersion": "4.7.4",`

Line 49 (inside the `git.packages` entry): change `"tag": "4.7.0"` → `"tag": "4.7.4"`. The full line becomes:

```json
          { "id": "com.cleversolutions.ads.unity", "url": "https://github.com/cleveradssolutions/CAS-Unity.git", "tag": "4.7.4" }
```

Leave `"minVersion": "4.5.4"` unchanged (the floor stays).

- [ ] **Step 3: Verify the JSON is valid and shows 4.7.4**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer.metadata"
python -c "import json;d=json.load(open('catalog.json'));c=[e for e in d['external'] if e['id']=='com.cleversolutions.ads.unity'][0];print('rec',c['recommendedVersion']);print('tag',c['git']['packages'][0]['tag'])"
```

Expected output:
```
rec 4.7.4
tag 4.7.4
```

- [ ] **Step 4: Bump the metadata version and changelog**

In `package.json` line 3: `"version": "0.0.2-preview.18",` → `"version": "0.0.2-preview.19",`

In `CHANGELOG.md`, add a new top entry:

```markdown
## [0.0.2-preview.19] - 2026-06-29

- **CAS pin → 4.7.4:** bump CAS `recommendedVersion` and git tag from 4.7.0 to the current stable 4.7.4.
  Aligns the hub install with CAS's own stable channel (no more pointless in-CAS update nag, #4.3) and
  picks up CAS's signature fix, clearing the "Missing signature" warning that came from 4.7.0 (#5).
```

- [ ] **Step 5: Commit (metadata repo)**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer.metadata"
git add catalog.json package.json CHANGELOG.md
git commit -m "chore(metadata): bump CAS pin to stable 4.7.4 (#4.3, #5)"
```

---

### Task 3: Publish, reinstall, and owner re-verification (WS-0 + WS-A) — OWNER-RUN

Ship both packages and re-test the items the round-2 feedback marked "not done" that are in fact already on-branch. This task has no automated tests — its deliverable is a verified release. Every step here is owner-run (publishing + Unity).

**Files:** none (release + manual verification).

- [ ] **Step 1: Sign and publish the metadata package**

Use the `unity-package-signing` skill to sign, then publish from the metadata repo to `https://npm.psvgamestudio.com/`. Confirm `.meta` files are committed first. Verify the published version is `0.0.2-preview.19`.

- [ ] **Step 2: Sign and publish the installer package**

Use the `unity-package-signing` skill to sign, then publish `com.psvgamestudio.installer@0.0.1-preview.26`. Confirm `.meta` files are committed first.

- [ ] **Step 3: Reinstall in a clean verification project**

In a clean Unity project (or `consumer/`), install the installer via the bootstrap / Package Manager, let it pull `metadata@0.0.2-preview.19`, and open `PSV → Installer…`.

- [ ] **Step 4: Run the EditMode tests in `dev/`**

Open `dev/` in Unity → `Window → General → Test Runner → EditMode` → run `PSV.Installer.Editor.Tests`. Expected: all green (including the new `ResolveSeed_*`).

- [ ] **Step 5: Visual verification matrix**

Confirm each in Unity and tick:

  - [ ] **#1** Welcome shows the UPM / Git URL install-method radio.
  - [ ] **#2** On a fresh open with no CAS installed, the CAS-ID field is **empty** (no bundle id, no previously-typed value); Next is locked until an id is entered.
  - [ ] **#2.2** With CAS already configured (real managerId), the field prefills that id.
  - [ ] **#2.1(a)** Enter a CAS id → Next → CASSettings receives it WITHOUT manually reopening the wizard.
  - [ ] **#3** Each installed component row shows a working Remove button.
  - [ ] **#4 detection** A unitypackage-installed SDK (asmdef/DLL/raw .cs in `Assets/`) shows as "Installed (manual)" with Migrate, not an enabled Install (no duplicate).
  - [ ] **#4.3 / #5** Installing CAS pulls **4.7.4**; the "Missing signature" dialog no longer appears for CAS.

- [ ] **Step 6: Record the result**

If all pass: note the verified versions in `installer-wizard-client-feedback` memory and proceed to the next plan (WS-1/WS-3/WS-8/WS-2/WS-4). If anything fails, capture the exact symptom and open a follow-up before moving on.

---

## Self-Review

- **Spec coverage (this batch):** #2 → Task 1; #2.2 → Task 1 (`ResolveSeed` existing-id path) + Task 3 verify; #4.3 → Task 2; #5 (CAS half) → Task 2 + Task 3 verify; #1/#2.1(a)/#3/#4-detection → already on-branch, Task 3 verify only. Items NOT in this batch (own later plans): #2.1(b) editable Configuration field, #4 Delete-anyway, #4.1/#4.2 git source, #6 gradle, #7 auto-open, new#8 Plugins auto-delete, #4.5 brainstorm, #5 rest-of-packages signing.
- **Placeholder scan:** none — every step has exact code/commands.
- **Type consistency:** `ResolveSeed(string storedValue, string existingCasId)` defined in Task 1 and used only there; `CasIdApplier.ReadExisting(string)` and `InstallerKeyStore.Get(string, string)` are existing signatures.
