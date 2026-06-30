### Task 6: Catalog `adFormats` flag + version bumps

**Files:**
- Modify: metadata `catalog.json` (CAS config rows), `package.json`, `CHANGELOG.md`
- Modify: installer `package.json`, `CHANGELOG.md`

- [ ] **Step 1: Add `"adFormats": true` to both CAS config rows**

In `catalog.json`, on each CAS `config` row (which already carry `regex`/`hint`/`editable`), add `"adFormats": true` after `"editable": true`.

- [ ] **Step 2: Verify JSON**

```bash
cd "E:/workspace/casai/dev/Packages/com.psvgamestudio.installer.metadata"
python -c "import json;d=json.load(open('catalog.json'));cfg=[e for e in d['external'] if e['id']=='com.cleversolutions.ads.unity'][0]['config'];[print(c['platform'],c.get('adFormats')) for c in cfg]"
```
Expected: `Android True` / `iOS True`.

- [ ] **Step 3: Bump metadata (`0.0.2-preview.21` → `0.0.2-preview.22`) + changelog**, commit `chore(metadata): mark CAS config adFormats (#4.5)` (on branch `chore/cas-pin-4.7.4`).

- [ ] **Step 4: Bump installer (`0.0.1-preview.29` → `0.0.1-preview.30`) + changelog** ("Configuration: CAS ad-format toggles + audience/network set with auto asset-create (#4.5)"), commit `chore(installer): release notes for CAS auto-config (preview.30)`.

---

## Self-Review

- **Spec coverage:** formats → Task 1 (`AdFlagsBits`) + Task 3 (`SetAdFlags`) + Task 5 (toggles); audience/network → Task 2 (`CasAudience`) + Task 3 (`SetAudience`) + Task 4 (`CasMediation`) + Task 5 (control); asset-create → Task 3 (`EnsureAsset`); data-driven gating → Task 5/6 (`adFormats`); graceful reflection fallback → Task 4.
- **Placeholder scan:** none — pure helpers carry full code+tests; Unity-only writers/reflection are explicitly owner-run with documented contracts.
- **Type consistency:** `AdFlagsBits.{Banner,Interstitial,Rewarded,AppOpen,HasFlag,WithFlag}`, `CasAudience.{Mixed,Children,NotChildren,ForFamilies,IsFamilies}`, `CasSettingsWriter.{ReadAdFlags,ReadAudience,SetAdFlags,SetAudience,EnsureAsset}`, `CasMediation.SelectSolution(BuildTarget,bool)`, `ConfigRequirement.AdFormats`, `SetupModel.Row.{Id,AdFormats}` — consistent across tasks.
