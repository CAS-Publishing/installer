# CAS.AI Publishing Hub Redesign — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Перетворити інсталер на «чистий інсталер» за PDF-бордом: 3-кроковий інтро-флоу (Install → Configure → Done) без жодної CAS-конфігурації всередині, вкладки Components/Configuration/About, брендинг CAS.AI Publishing Hub.

**Architecture:** Інкрементальний рефакторинг наявного UI-Toolkit візарда: `WizardRouter`/`InstallerWizardWindow` і весь бекенд (`ComponentStatusProvider`, `WizardActions`, `AutoInstaller`, scanner/migrator/catalog) лишаються; змінюється склад екранів, їхні UXML та детекція конфігурації (read-only). CAS-write код видаляється повністю.

**Tech Stack:** Unity 2022.3.62f3, C# (Editor-only), UI Toolkit (UXML/USS), Unity Test Framework (EditMode). Тести запускаються в Unity: `Window → General → Test Runner → EditMode → PSV.Installer.Editor.Tests`. CLI-ранера немає.

**Spec:** `docs/superpowers/specs/2026-07-16-cas-publishing-hub-redesign-design.md`

## Global Constraints

- Package root: `E:\workspace\casai\dev\Packages\com.psvgamestudio.installer`; гілка `feat/installer-wizard-ui`; Conventional Commits зі scope `(installer)`.
- Брендинг (точні рядки): вікно/шапка/діалоги — `CAS.AI Publishing Hub`; меню — `Assets/CleverAdsSolutions/Hub`; лог-префікс — `[CAS Hub]`.
- Інсталер **ніколи не пише** CAS-налаштування (managerIds, audienceTagged, allowedAdFlags, mediation solutions) — тільки читає.
- Кожен новий `.cs`/`.uxml`/`.md` файл — з `.meta` (fileFormatVersion: 2, guid = 32 hex, `TextScriptImporter` для .md, `MonoImporter`-стиль генерує Unity; якщо Unity закритий — створити .meta вручну як у сусідніх файлах). Кожен видалений файл — видаляти разом із `.meta`.
- Screen ids після рефакторингу: `ready`, `progress`, `configure`, `done`, `components`, `about`. Вкладки: `components`, `configure`, `about`.
- EDM4U id: `com.google.external-dependency-manager`.
- Реліз (preview.37, підпис, publish) — ПОЗА цим планом; після ручної верифікації власником.

---

### Task 1: Брендинг CAS.AI Publishing Hub

**Files:**
- Modify: `Editor/Wizard/InstallerWizardWindow.cs:61-91` (MenuItem + title)
- Modify: `Editor/Wizard/WizardActions.cs`, `Editor/Wizard/AutoInstaller.cs`, `Editor/Wizard/Screens/ProgressScreen.cs`, `Editor/Wizard/MigrateConfirmWindow.cs` (усі `EditorUtility.DisplayDialog("PSV Installer", …)` → `"CAS.AI Publishing Hub"`)
- Modify: `Editor/Wizard/Uxml/WizardShell.uxml` (заголовок шапки → `CAS.AI Publishing Hub`)
- Modify: усі файли з лог-префіксом `[PSV Installer]` → `[CAS Hub]` (grep-заміна по `Editor/`)

**Interfaces:**
- Consumes: —
- Produces: MenuItem `Assets/CleverAdsSolutions/Hub` → `InstallerWizardWindow.Open()`; MenuItem `Assets/CleverAdsSolutions/Hub (Restart Intro)` → `OpenFirstRun()`.

- [ ] **Step 1:** У `InstallerWizardWindow.cs` замінити атрибути й title:

```csharp
[MenuItem("Assets/CleverAdsSolutions/Hub")]
public static void Open()
{
    var window = GetWindow<InstallerWizardWindow>(true, "CAS.AI Publishing Hub", true);
    ...
}

[MenuItem("Assets/CleverAdsSolutions/Hub (Restart Intro)")]
public static void OpenFirstRun() { ... }
```

- [ ] **Step 2:** `Grep '"PSV Installer"' Editor/` → замінити всі заголовки діалогів на `"CAS.AI Publishing Hub"`. `Grep '\[PSV Installer\]' Editor/` → `[CAS Hub]`.
- [ ] **Step 3:** У `WizardShell.uxml` замінити текст заголовка шапки на `CAS.AI Publishing Hub`.
- [ ] **Step 4:** Переконатися, що `Tools/PSV Installer/Run Scan (Debug)` (`Editor/Scanner/_DebugMenu.cs`) або видалено, або перейменовано на `Assets/CleverAdsSolutions/Hub Debug/Run Scan`.
- [ ] **Step 5:** Компіляція в Unity без помилок; меню видно під Assets → CleverAdsSolutions.
- [ ] **Step 6:** Commit: `git commit -m "feat(installer): rebrand UI to CAS.AI Publishing Hub, menu under Assets/CleverAdsSolutions"`

### Task 2: EDM4U у дефолтному наборі компонентів

**Files:**
- Modify: `Editor/Wizard/ComponentStatusProvider.cs:48-53` (Defaults)
- Test: `Editor/Tests/ComponentStatusProviderDefaultsTests.cs` (новий)

**Interfaces:**
- Consumes: `ComponentStatusProvider.Defaults`, `DefaultIds`, `TryGetDefaultDisplay`.
- Produces: `DefaultIds` повертає 4 ids у порядку: CAS, Tenjin, Firebase Analytics, EDM4U (`com.google.external-dependency-manager`, DisplayName `External Dependency Manager (EDM4U)`, Sub `Android/iOS dependency resolver`, Logo `edm`).

- [ ] **Step 1:** Тест (новий файл):

```csharp
using NUnit.Framework;
using PSV.Installer.Wizard;

public class ComponentStatusProviderDefaultsTests
{
    [Test]
    public void DefaultIds_ContainsEdm4uLast()
    {
        var ids = ComponentStatusProvider.DefaultIds;
        Assert.AreEqual(4, ids.Count);
        Assert.AreEqual("com.google.external-dependency-manager", ids[3]);
    }

    [Test]
    public void Edm4uDisplay_IsMapped()
    {
        Assert.IsTrue(ComponentStatusProvider.TryGetDefaultDisplay(
            "com.google.external-dependency-manager", out var name, out _));
        Assert.AreEqual("External Dependency Manager (EDM4U)", name);
    }
}
```

- [ ] **Step 2:** Запустити в Test Runner — FAIL (3 ids).
- [ ] **Step 3:** Додати запис у `Defaults`:

```csharp
("com.google.external-dependency-manager", "External Dependency Manager (EDM4U)",
 "Android/iOS dependency resolver", "edm"),
```

- [ ] **Step 4:** Test Runner — PASS. Якщо запису немає в каталозі — рядок покаже `Not in catalog` (очікувано; додавання в metadata-пакет — окрема задача, зафіксувати TODO в каталог-репі).
- [ ] **Step 5:** Commit: `feat(installer): add EDM4U to default component set`

### Task 3: ReadyScreen (крок 1 «Ready to install»)

**Files:**
- Create: `Editor/Wizard/Screens/ReadyScreen.cs`, `Editor/Wizard/Uxml/Ready.uxml` (+ .meta)
- Create: `Editor/Wizard/ReadyModel.cs`
- Test: `Editor/Tests/ReadyModelTests.cs`

**Interfaces:**
- Consumes: `ComponentStatusProvider.TryGetStatuses(out List<ComponentStatus>, out string)`; `AutoInstaller.StartAll(WizardRouter)`; `WizardRouter.GoTo(string)`; `IWizardScreen { string Id; VisualElement Root; void OnEnter(WizardRouter); }`.
- Produces: screen id `"ready"`. `ReadyModel.Build(List<ComponentStatus>) → ReadyModel` з полями `List<ReadyRow> Rows` (`Name`, `Sub`, `RightText`, `AlreadyInstalled`), `bool AllInstalled`, `string PrimaryButtonText` (`"Install"` | `"Continue"`).

- [ ] **Step 1:** Тест `ReadyModelTests.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using PSV.Installer.Wizard;

public class ReadyModelTests
{
    private static ComponentStatus S(string name, bool installed, string version) =>
        new ComponentStatus { Id = name, DisplayName = name, Installed = installed, Version = version };

    [Test]
    public void MixedInstall_PrimaryIsInstall_RowsShowState()
    {
        var m = ReadyModel.Build(new List<ComponentStatus>
            { S("CAS SDK", false, null), S("Tenjin SDK", true, "1.19.3") });
        Assert.AreEqual("Install", m.PrimaryButtonText);
        Assert.IsFalse(m.AllInstalled);
        Assert.IsFalse(m.Rows[0].AlreadyInstalled);
        Assert.AreEqual("✓ Already installed (1.19.3)", m.Rows[1].RightText);
    }

    [Test]
    public void AllInstalled_PrimaryIsContinue()
    {
        var m = ReadyModel.Build(new List<ComponentStatus>
            { S("CAS SDK", true, "4.0.0"), S("Tenjin SDK", true, "1.19.3") });
        Assert.AreEqual("Continue", m.PrimaryButtonText);
        Assert.IsTrue(m.AllInstalled);
    }
}
```

- [ ] **Step 2:** Test Runner — FAIL (ReadyModel відсутній).
- [ ] **Step 3:** `ReadyModel.cs` — pure view-model:

```csharp
using System.Collections.Generic;

namespace PSV.Installer.Wizard
{
    internal sealed class ReadyRow
    {
        public string Name;
        public string Sub;
        public string RightText;      // "4.x.x" (ціль) або "✓ Already installed (v)"
        public bool   AlreadyInstalled;
    }

    internal sealed class ReadyModel
    {
        public readonly List<ReadyRow> Rows = new List<ReadyRow>();
        public bool AllInstalled = true;
        public string PrimaryButtonText => AllInstalled ? "Continue" : "Install";

        public static ReadyModel Build(List<ComponentStatus> statuses)
        {
            var m = new ReadyModel();
            foreach (var s in statuses)
            {
                var installed = s.Installed;
                m.Rows.Add(new ReadyRow
                {
                    Name = s.DisplayName,
                    Sub = s.Sub,
                    AlreadyInstalled = installed,
                    RightText = installed
                        ? $"✓ Already installed ({s.Version ?? "?"})"
                        : (s.Version ?? "latest"),
                });
                if (!installed) m.AllInstalled = false;
            }
            return m;
        }
    }
}
```

- [ ] **Step 4:** Test Runner — PASS.
- [ ] **Step 5:** `Ready.uxml` — за мокапом №1: заголовок `Ready to install`, підзаголовок `Prepares your game for publishing by setting up mediation, attribution, and analytics.`, контейнер-картка `#ready-list` (рядки додаються з C#), примітка `The installer will modify Packages/manifest.json and import required plugins. No scene files will be changed.`, кнопка `#btn-advanced` `Advanced integration` (клас `.cas-btn--muted`), футер із `#btn-primary` (клас `.cas-btn--primary`). Стилі — наявні класи `theme.uss` (карток/рядків з Components.uxml).
- [ ] **Step 6:** `ReadyScreen.cs` — `IWizardScreen` з `Id => "ready"`. `OnEnter`: `ComponentStatusProvider.TryGetStatuses` → `ReadyModel.Build` → рендер рядків (Label name+sub зліва, Label right, зелений клас для installed); помилка каталогу → показати error-панель + Retry (виклик `Bootstrap.EnsureMetadata()` + `InvalidateCache` + перерендер). Tenjin-рядок: `Sub` = `Attribution — handled on our end, nothing for you to configure` (задати в ReadyScreen поверх каталожного Sub). Firebase-рядок: `Analytics — events wired automatically via CAS SDK`. Кнопки:

```csharp
_advanced.clicked += () => { InstallerWizardWindow.IntroDone = true; router.GoTo("components"); };
_primary.clicked  += () =>
{
    if (_model.AllInstalled) router.GoTo("configure");
    else AutoInstaller.StartAll(router);   // веде на progress сам
};
```

- [ ] **Step 7:** Тимчасово зареєструвати екран у `RegisterScreens` (`router.Register(new ReadyScreen());`) — навігація перемкнеться в Task 5. Компіляція без помилок.
- [ ] **Step 8:** Commit: `feat(installer): ReadyScreen with install/continue model (step 1 of 3)`

### Task 4: ProgressScreen — Retry step / Copy log / Installation complete

**Files:**
- Modify: `Editor/Wizard/Screens/ProgressScreen.cs`, `Editor/Wizard/Uxml/Progress.uxml`
- Modify: `Editor/Wizard/AutoInstaller.cs` (лог кроку + retry одного кроку, якщо ще нема)
- Test: `Editor/Tests/ProgressFailurePanelTests.cs`

**Interfaces:**
- Consumes: наявний step-механізм ProgressScreen/AutoInstaller (SessionState-черга, watchdog), `InstallRetryPolicy`.
- Produces: `ProgressFailureModel { string StepName; string Message; string Log; }` + `ProgressScreen` states: працює / збій кроку (панель із `Retry step`, `Copy log`) / завершено (заголовок `Installation complete`, зелена панель `All components installed`, кнопка `Continue` → `configure`).

- [ ] **Step 1:** Тест на pure-модель панелі збою (мапінг помилки кроку в модель):

```csharp
using NUnit.Framework;
using PSV.Installer.Wizard;

public class ProgressFailurePanelTests
{
    [Test]
    public void FailureModel_CarriesStepAndHint()
    {
        var m = ProgressFailureModel.From("Firebase Analytics", "Could not resolve package.");
        Assert.AreEqual("Firebase Analytics — installation failed", m.Title);
        StringAssert.Contains("Check your internet connection", m.Message);
    }
}
```

- [ ] **Step 2:** FAIL → реалізувати `ProgressFailureModel` (у `ProgressScreen.cs` або окремим файлом):

```csharp
internal sealed class ProgressFailureModel
{
    public string Title;
    public string Message;
    public string Log;
    public static ProgressFailureModel From(string step, string error) => new ProgressFailureModel
    {
        Title = step + " — installation failed",
        Message = error + " Check your internet connection.",
        Log = error,
    };
}
```

- [ ] **Step 3:** PASS.
- [ ] **Step 4:** `Progress.uxml`: додати приховану панель `#fail-panel` (title, message, кнопки `#btn-retry-step` `Retry step`, `#btn-copy-log` `Copy log`), футер `#btn-cancel` `Cancel`; фінальний стан: заголовок міняється на `Installation complete`, зелена панель `#done-panel` (`✓ All components installed` + `The project is fully prepared. You can continue to configuration and finalize plugin settings.`), кнопка `#btn-continue` `Continue`.
- [ ] **Step 5:** Wiring: на збій кроку — показати `#fail-panel` (модель From(step, error)), `Retry step` → повторити ТІЛЬКИ збійний крок (не перезапускаючи чергу з нуля; використати наявну SessionState-чергу — не знімати крок при збої), `Copy log` → `EditorGUIUtility.systemCopyBuffer = model.Log;`, `Cancel` → `AutoInstaller.Clear()` + `router.GoTo("ready")`. На завершення всіх кроків — done-стан, `Continue` → `router.GoTo("configure")` (замість переходу на окремий done-екран).
- [ ] **Step 6:** Компіляція + Test Runner PASS.
- [ ] **Step 7:** Commit: `feat(installer): progress screen failure panel (retry step, copy log) + inline completion state`

### Task 5: Роутинг 3-крокового флоу + степпер

**Files:**
- Modify: `Editor/Wizard/InstallerWizardWindow.cs` (`ScreenOrder`, `TabScreens`, `ResolveStartScreen`, `RegisterScreens`, видалити `OpenAtWelcome`/`ConsumeRequestedPlatform`/`RequestPlatformKey`)
- Modify: `Editor/Wizard/Uxml/WizardShell.uxml` + `Editor/Wizard/Uss/theme.uss` (степпер `1 Install — 2 Configure — 3 Done` над контентом на флоу-екранах)
- Modify: build-target watcher (файл, що викликає `OpenAtWelcome`; знайдений через `Grep OpenAtWelcome Editor/`)
- Test: `Editor/Tests/StartScreenResolveTests.cs` (новий; логіку `ResolveStartScreen` винести в статичний pure-метод)

**Interfaces:**
- Consumes: screen ids з Tasks 3–4.
- Produces: `ScreenOrder = { "ready", "progress", "configure", "done", "components", "about" }`; `TabScreens = { "components", "configure", "about" }`; `internal static string ResolveStartScreenPure(string saved, bool introDone)`; степпер: `WizardStepper.StepFor(screenId) → int?` (1=ready/progress, 2=configure, 3=done, null=вкладки — степпер прихований).

- [ ] **Step 1:** Тест:

```csharp
using NUnit.Framework;
using PSV.Installer.Wizard;

public class StartScreenResolveTests
{
    [TestCase(null, false, ExpectedResult = "ready")]
    [TestCase(null, true,  ExpectedResult = "components")]
    [TestCase("configure", true, ExpectedResult = "configure")]
    [TestCase("welcome", true, ExpectedResult = "components")] // стейл-id зі старої версії
    [TestCase("progress", false, ExpectedResult = "progress")]
    public string Resolve(string saved, bool introDone) =>
        InstallerWizardWindow.ResolveStartScreenPure(saved, introDone);

    [TestCase("ready", ExpectedResult = 1)]
    [TestCase("progress", ExpectedResult = 1)]
    [TestCase("configure", ExpectedResult = 2)]
    [TestCase("done", ExpectedResult = 3)]
    [TestCase("components", ExpectedResult = null)]
    public int? Step(string id) => WizardStepper.StepFor(id);
}
```

- [ ] **Step 2:** FAIL → реалізація: `ResolveStartScreenPure(saved, introDone)`: saved валідний (є в ScreenOrder) → saved, крім випадку `introDone && saved=="ready"` → `"components"`; невалідний/порожній → `introDone ? "components" : "ready"`. `WizardStepper` — новий маленький static-клас поруч. PASS.
- [ ] **Step 3:** `RegisterScreens`: лишити `ReadyScreen, ComponentsScreen, ProgressScreen, DoneScreen, SetupScreen(→ConfigureScreen у Task 6), AboutScreen`. Видалити реєстрації Welcome/Integration/Hub/SettingsRedirect (класи видаляються в Task 9; тут — тільки реєстрації та `using`).
- [ ] **Step 4:** `OpenAtWelcome`/`RequestPlatformKey`/`ConsumeRequestedPlatform` видалити; виклик у build-target watcher замінити на no-op або `Open()` на `configure` (детекція сама покаже платформні статуси). `OpenFirstRun`: `SessionState.SetString(CurrentScreenKey, "ready")` + `GoTo("ready")`.
- [ ] **Step 5:** `WizardShell.uxml`: під шапкою — контейнер `#cas-stepper` з трьома елементами (`1 Install`, `2 Configure`, `3 Done`, розділювачі-лінії). `UpdateTabBar(id)` розширити: `var step = WizardStepper.StepFor(id);` — степпер видимий коли `step != null` і `!IntroDone`-флоу; активний пункт — клас `.cas-step--active`, пройдений — `.cas-step--done` (галочка `✓ Install` як на мокапах). Вкладки видимі коли `step == null`.
- [ ] **Step 6:** `IntroDone` виставляється: у ReadyScreen (Advanced integration) та у DoneScreen.OnEnter (Task 8). Компіляція + Test Runner PASS.
- [ ] **Step 7:** Commit: `feat(installer): 3-step intro flow routing (ready→progress→configure→done) + stepper header`

### Task 6: ConfigureScreen — детекція замість конфігурації

**Files:**
- Modify: `Editor/Wizard/Screens/SetupScreen.cs` → перейменувати клас/файл на `ConfigureScreen.cs`, `Id => "configure"`; видалити `BuildCasConfig`/`BuildSolutionChoice` і всі виклики CAS-write
- Modify: `Editor/Wizard/Uxml/Setup.uxml` → `Configure.uxml` (таблиця + панелі + футер `↻ Refresh / Back / Continue`)
- Create: `Editor/Wizard/ConfigureGate.cs`
- Create: `Editor/Wizard/CasNativeSettings.cs`
- Test: `Editor/Tests/ConfigureGateTests.cs`

**Interfaces:**
- Consumes: `SetupModel.TryBuild(out List<SetupModel.Row>, out string)`; `SetupChecker.Evaluate(ConfigRequirement) → ReqResult` (`ReqStatus Ok/Missing/...`, `Value`); `CasIdApplier.ReadExisting(platform)`.
- Produces: `ConfigureGate.CanContinue(IReadOnlyList<PlatformReadiness>) → bool` де `PlatformReadiness { string Platform; bool Used; bool AllOk; }` — true, якщо існує платформа `Used && AllOk` (one-platform-rule); `CasNativeSettings.Open(string platform)` — відкриває нативне вікно CAS.AI (спроба `EditorApplication.ExecuteMenuItem("Assets/CleverAdsSolutions/" + (platform=="iOS" ? "iOS Settings" : "Android Settings"))`, фолбек: `Selection.activeObject = AssetDatabase.LoadMainAssetAtPath("Assets/CleverAdsSolutions/Resources/CASSettings" + platform + ".asset")` + `EditorGUIUtility.PingObject`).

- [ ] **Step 1:** Тест:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using PSV.Installer.Wizard;

public class ConfigureGateTests
{
    private static PlatformReadiness P(string name, bool used, bool ok) =>
        new PlatformReadiness { Platform = name, Used = used, AllOk = ok };

    [Test] public void BothIncomplete_Blocked() =>
        Assert.IsFalse(ConfigureGate.CanContinue(new List<PlatformReadiness>
            { P("Android", true, false), P("iOS", true, false) }));

    [Test] public void OnePlatformReady_Enough() =>
        Assert.IsTrue(ConfigureGate.CanContinue(new List<PlatformReadiness>
            { P("Android", true, true), P("iOS", true, false) }));

    [Test] public void UnusedPlatformDoesNotCount() =>
        Assert.IsFalse(ConfigureGate.CanContinue(new List<PlatformReadiness>
            { P("Android", false, true), P("iOS", true, false) }));
}
```

- [ ] **Step 2:** FAIL → `ConfigureGate.cs`:

```csharp
using System.Collections.Generic;

namespace PSV.Installer.Wizard
{
    internal sealed class PlatformReadiness
    {
        public string Platform;
        public bool Used;   // платформа задіяна (є вимоги і компонент встановлений)
        public bool AllOk;  // всі активні вимоги виконані
    }

    internal static class ConfigureGate
    {
        // Правило «one platform is enough» (мокап Configuration complete №8).
        public static bool CanContinue(IReadOnlyList<PlatformReadiness> platforms)
        {
            foreach (var p in platforms)
                if (p != null && p.Used && p.AllOk) return true;
            return false;
        }
    }
}
```

- [ ] **Step 3:** PASS.
- [ ] **Step 4:** `Configure.uxml` за мокапами №5/6/8/9: заголовок `Configuration`, динамічний підзаголовок, таблиця `#config-table` (шапка `Component / Android / iOS`, рядки з C#), контейнер панелей `#action-panels`, панель-гейт `#gate-note` (`⚠ Continue is disabled until…`), футер: `#btn-refresh` `↻ Refresh`, `#btn-back` `Back`, `#btn-continue` `Continue`.
- [ ] **Step 5:** `ConfigureScreen.cs`: з `SetupModel.TryBuild` рендерити клітинки (`✓ <label>` зелена / `⚠ <label>` жовта / `Not used` сіра — платформа Unused, коли для неї немає жодної вимоги або компонент не встановлений). Панелі:
  - якщо CAS ID missing на будь-якій платформі → панель `⚠ CAS plugin configuration required` (текст із мокапа №5) + кнопки `Android settings`/`iOS settings` → `CasNativeSettings.Open(...)`;
  - якщо Tenjin key missing і поле задетектовано (Task 7) → панель `⚠ Tenjin SDK Key required` + `Open CAS Settings` → `CasNativeSettings.Open(активна платформа)`;
  - якщо Firebase file missing → панель `⚠ Firebase Analytics — configuration file missing` (текст із мокапа №5) + `Open Firebase Console` (`Application.OpenURL("https://console.firebase.google.com/")`) + `Locate file…` (Task 7 Step 6).
  - `Continue.SetEnabled(ConfigureGate.CanContinue(...))`; заголовок стає `Configuration complete`, коли гейт відкритий (підзаголовки з мокапів №8/9). `Refresh` → `ComponentStatusProvider.InvalidateCache()` + ререндер; `Back` → `router.Back()`; `Continue` → `router.GoTo("done")`.
- [ ] **Step 6:** Видалити з екрана всі CAS-write виклики (`CasSettingsWriter.Set*`, `CasMediation`, радіо/тумблери) — компілятор підкаже точки; сам write-код видаляється у Task 9.
- [ ] **Step 7:** Компіляція + Test Runner PASS (`SetupCheckerTests`, `SetupScreenPlatformTests` — поправити/перенести під нову назву класу; тести, що перевіряли радіо/ad-flags UI — видалити).
- [ ] **Step 8:** Commit: `feat(installer): Configure screen = read-only detection table with one-platform gate`

### Task 7: Детектори — Tenjin key (feature-detect) і google-services.json Locate

**Files:**
- Create: `Editor/Wizard/TenjinKeyDetect.cs`
- Create: `Editor/Wizard/FirebaseConfigFile.cs`
- Modify: `Editor/Wizard/Screens/ConfigureScreen.cs` (використання)
- Test: `Editor/Tests/TenjinKeyDetectTests.cs`, `Editor/Tests/FirebaseConfigFileTests.cs`

**Interfaces:**
- Consumes: `CASSettings<Platform>.asset` через `SerializedObject` (той самий шлях, що читає `CasIdApplier.ReadExisting`).
- Produces:
  - `TenjinKeyDetect.Probe(string platform) → TenjinKeyProbe { bool FieldSupported; string Key; }` — `FieldSupported=false`, коли у CAS-версії ще немає Tenjin-поля (тоді рядок Tenjin інформаційний і НЕ блокує Continue); кандидати імен поля: `tenjinKey`, `tenjinSdkKey`, `tenjinAppKey`.
  - `FirebaseConfigFile.ValidateAndCopy(string sourcePath, string projectAssets) → string` (null = ok, інакше текст помилки); приймає лише `google-services.json`/`GoogleService-Info.plist`, копіює в `Assets/`.

- [ ] **Step 1:** Тести:

```csharp
using NUnit.Framework;
using PSV.Installer.Wizard;

public class TenjinKeyDetectTests
{
    [Test]
    public void FieldNames_AreProbedInOrder()
    {
        CollectionAssert.AreEqual(
            new[] { "tenjinKey", "tenjinSdkKey", "tenjinAppKey" },
            TenjinKeyDetect.CandidateFieldNames);
    }

    [Test]
    public void MissingAsset_ReportsUnsupported()
    {
        var probe = TenjinKeyDetect.Probe("NoSuchPlatform");
        Assert.IsFalse(probe.FieldSupported);
        Assert.IsNull(probe.Key);
    }
}

public class FirebaseConfigFileTests
{
    [Test] public void WrongFileName_Rejected() =>
        Assert.IsNotNull(FirebaseConfigFile.Validate("C:/tmp/random.json"));

    [Test] public void GoogleServicesJson_Accepted() =>
        Assert.IsNull(FirebaseConfigFile.Validate("C:/tmp/google-services.json"));

    [Test] public void PlistAccepted() =>
        Assert.IsNull(FirebaseConfigFile.Validate("D:/x/GoogleService-Info.plist"));
}
```

- [ ] **Step 2:** FAIL → реалізація. `TenjinKeyDetect`: знайти settings-asset (той же lookup, що в `CasIdApplier.ReadExisting` — винести спільний приватний хелпер або продублювати шлях), `new SerializedObject(asset)`, `FindProperty` по `CandidateFieldNames`; property знайдено → `FieldSupported=true, Key=prop.stringValue`; asset або property немає → `FieldSupported=false`. `FirebaseConfigFile.Validate(path)` — pure перевірка імені файлу; `ValidateAndCopy` — `File.Copy` в `Assets/` + `AssetDatabase.ImportAsset`.
- [ ] **Step 3:** PASS.
- [ ] **Step 4:** У `ConfigureScreen`: Tenjin-клітинки — `Probe(platform)`: `FieldSupported && !string.IsNullOrEmpty(Key)` → `✓ Key configured`; `FieldSupported && empty` → `⚠ Key not configured` (вимога активна, блокує платформу, панель Open CAS Settings); `!FieldSupported` → текст `Handled on our end` (сірий, НЕ впливає на `PlatformReadiness.AllOk`).
- [ ] **Step 5:** `Locate file…` → `EditorUtility.OpenFilePanel("Select google-services.json", "", "json,plist")` → `ValidateAndCopy`; помилку показати `EditorUtility.DisplayDialog("CAS.AI Publishing Hub", err, "OK")`; успіх → Refresh.
- [ ] **Step 6:** Компіляція + Test Runner PASS.
- [ ] **Step 7:** Commit: `feat(installer): Tenjin key feature-detect + Firebase config locate/copy`

### Task 8: DoneScreen (крок 3)

**Files:**
- Modify: `Editor/Wizard/Screens/DoneScreen.cs`, `Editor/Wizard/Uxml/Done.uxml`

**Interfaces:**
- Consumes: `CasIdApplier.ReadExisting(platform)` (bundle id для чеклиста), `TenjinKeyDetect.Probe`, `SetupChecker` (Firebase file), `InstallerWizardWindow.CloseActive()`.
- Produces: screen id `"done"` (без змін), `IntroDone = true` в `OnEnter`.

- [ ] **Step 1:** `Done.uxml` за мокапом №10: зелене коло-галочка, `Setup complete`, `Everything required for the CAS publishing setup is now connected.`, картка-чеклист `#done-list` (3 рядки з C#), картка `Next steps` (`→ Add ad placements to your game`, `→ Open CAS dashboard`, `→ Re-open this installer anytime: Assets → CleverAdsSolutions → Hub`) + кнопка `#btn-components` `Components`, футер `#btn-close` `Close`.
- [ ] **Step 2:** `DoneScreen.OnEnter`: `InstallerWizardWindow.IntroDone = true;` чеклист:
  - `✓ CAS SDK — mediation ready (<CasIdApplier.ReadExisting(активна платформа)>)`;
  - `✓ Tenjin — attribution key configured` (або `Tenjin — handled on our end`, якщо `!FieldSupported`);
  - `✓ Firebase — analytics connected`.
  `Open CAS dashboard` → `Application.OpenURL("https://cas.ai")`; `Components` → `router.GoTo("components")`; `Close` → `InstallerWizardWindow.CloseActive()`.
- [ ] **Step 3:** Компіляція; ручна перевірка флоу ready→…→done у Unity.
- [ ] **Step 4:** Commit: `feat(installer): Done screen checklist + next steps per PDF`

### Task 9: Видалення CAS-конфіг коду та мертвих екранів

**Files:**
- Delete (з .meta): `Editor/Wizard/Screens/WelcomeScreen.cs`, `Editor/Wizard/Screens/IntegrationModeScreen.cs`, `Editor/Wizard/Screens/HubActionsScreen.cs`, `Editor/Wizard/Screens/SettingsRedirectScreen.cs`, `Editor/Wizard/Uxml/Welcome.uxml`, `IntegrationMode.uxml`, `HubActions.uxml`, `SettingsRedirect.uxml`, `Editor/Wizard/CasAudience.cs`, `Editor/Wizard/AdFlagsBits.cs`, `Editor/Wizard/CasMediation.cs`, `Editor/Wizard/InstallerKeyStore.cs`, `Editor/Wizard/CasIdValidation.cs`
- Delete tests (з .meta): `WelcomeScreenTests.cs`, `CasIdApplierTests.cs`, `CasIdApplierWriteTests.cs`, `CasAudienceTests.cs`, `AdFlagsBitsTests.cs`, `InstallerKeyStoreTests.cs`, `BuildSwitchPolicyTests.cs` (якщо watcher видалено в Task 5), `SetupScreenPlatformTests.cs` (замінений у Task 6)
- Modify: `Editor/Wizard/CasIdApplier.cs` — лишити ТІЛЬКИ `ReadExisting` (+ приватний asset-lookup); видалити `ApplyPending`/`SetManagerId`/`ShouldWrite`/`NormalizeManagerId` і виклик `ApplyPending` у `InstallerWizardWindow.CreateGUI`. Розглянути перейменування на `CasSettingsReader` (разом із read-методами з `CasSettingsWriter`)
- Modify: `Editor/Wizard/CasSettingsWriter.cs` — видалити `SetAdFlags`/`SetAudience`/`WriteInt`/створення asset із YAML-шаблону; read-методи (`ReadAdFlags`/`ReadAudience`/`ReadInt`) перенести в `CasSettingsReader.cs` (новий файл або перейменування), або видалити повністю, якщо після Task 6 їх ніхто не викликає (перевірити grep-ом)

**Interfaces:**
- Consumes: результати Tasks 5–8 (ніхто більше не посилається на видалене).
- Produces: компіляція без CAS-write коду; `CasSettingsReader.ReadExisting(platform)` — єдина точка читання CAS-asset для Configure/Done/TenjinKeyDetect.

- [ ] **Step 1:** `Grep` по кожному видалюваному класу — переконатися, що посилань нема (крім видалюваних файлів).
- [ ] **Step 2:** Видалити файли разом із `.meta`; підчистити `using`-и.
- [ ] **Step 3:** Компіляція в Unity без помилок; Test Runner — усі тести, що лишилися, PASS.
- [ ] **Step 4:** Commit: `refactor(installer)!: remove in-installer CAS configuration (welcome ID entry, audience/ad-format writes) — config lives in native CAS Settings

BREAKING CHANGE: installer no longer writes CAS settings; CAS ID and audience are configured in the CAS.AI Settings window.`

### Task 10: Main Components — таблиця з Remove/Connect to Hub + Additional components

**Files:**
- Modify: `Editor/Wizard/Screens/ComponentsScreen.cs`, `Editor/Wizard/Uxml/Components.uxml`, `Editor/Wizard/Uss/theme.uss`
- Create: `Editor/Wizard/ComponentsViewMap.cs`
- Modify: `Editor/Wizard/ComponentStatusProvider.cs` (метод для additional-набору)
- Test: `Editor/Tests/ComponentsViewMapTests.cs`

**Interfaces:**
- Consumes: `ComponentStatus` (поля як зараз), `WizardActions.Apply/SwitchToUpm/Remove/MigrateExternal(componentId, displayName)`, каталог (`CatalogLoader.Load().Catalog.Packages/External`).
- Produces:
  - `ComponentsViewMap.Map(ComponentStatus, string recommendedVersion) → ComponentRowVm { string StatusText; string ActionText; string ActionHint; RowAction Action; bool RemoveEnabled; }`, `enum RowAction { None, Install, Update, ConnectToHub, Fix }` — мапінг PDF: `Installed→Up to date (Action=None)`, `Update available/Too old→Update required (Action=Update, Hint="to v"+recommended)`, `Installed (manual)/Needs migration/Installed (git)→Manual install (Action=ConnectToHub)`, `Mixed install→Mixed install (Action=Fix)`, `Not Installed→Not installed (Action=Install)`; `RemoveEnabled = status.Installed`.
  - `ComponentStatusProvider.TryGetAdditionalStatuses(out List<ComponentStatus>, out string)` — записи каталогу, яких немає в `DefaultIds` (той самий scan/маппінг `FromPackage`/`FromExternal`).

- [ ] **Step 1:** Тест:

```csharp
using NUnit.Framework;
using PSV.Installer.Wizard;

public class ComponentsViewMapTests
{
    private static ComponentStatus S(string status, bool installed, bool outsideUpm = false, bool git = false) =>
        new ComponentStatus { StatusText = status, Installed = installed, OutsideUpm = outsideUpm, GitInstalled = git };

    [Test] public void Installed_MapsToUpToDate()
    {
        var vm = ComponentsViewMap.Map(S("Installed", true), "1.12.0");
        Assert.AreEqual("Up to date", vm.StatusText);
        Assert.AreEqual(RowAction.None, vm.Action);
        Assert.IsTrue(vm.RemoveEnabled);
    }

    [Test] public void UpdateAvailable_MapsToUpdateRequired_WithHint()
    {
        var vm = ComponentsViewMap.Map(S("Update available", true), "1.12.0");
        Assert.AreEqual("Update required", vm.StatusText);
        Assert.AreEqual(RowAction.Update, vm.Action);
        Assert.AreEqual("to v1.12.0", vm.ActionHint);
    }

    [Test] public void ManualInstall_MapsToConnectToHub()
    {
        var vm = ComponentsViewMap.Map(S("Installed (manual)", true, outsideUpm: true), null);
        Assert.AreEqual("Manual install", vm.StatusText);
        Assert.AreEqual(RowAction.ConnectToHub, vm.Action);
        Assert.AreEqual("Connect to Hub", vm.ActionText);
    }

    [Test] public void NotInstalled_MapsToInstall()
    {
        var vm = ComponentsViewMap.Map(S("Not Installed", false), null);
        Assert.AreEqual(RowAction.Install, vm.Action);
        Assert.IsFalse(vm.RemoveEnabled);
    }
}
```

- [ ] **Step 2:** FAIL → `ComponentsViewMap.cs` (pure switch по `StatusText`/прапорцях `OutsideUpm`/`GitInstalled` → PDF-термінологія; `ConnectToHub` виконує наявні `MigrateExternal` для manual і `SwitchToUpm` для git). PASS.
- [ ] **Step 3:** `TryGetAdditionalStatuses`: у `BuildStatuses`-стилі пройтися по всіх catalog-записах (`Packages` + `External`), відфільтрувати `DefaultIds` та metadata/installer own ids (`com.psvgamestudio.installer*`), змапити наявними `FromPackage`/`FromExternal`; display name — з каталогу.
- [ ] **Step 4:** `Components.uxml`: заголовок `Main Components`; таблиця-шапка `SDK / VERSION / STATUS / ACTION / REMOVE`; рядки з C#: name+sub, version (`—` коли null), status (тон-класи наявні), кнопка Action за `RowAction` (`ActionHint` — маленький Label під кнопкою), кнопка `Remove SDK` (клас `.cas-btn--danger`, червоне обрамлення; disabled коли `!RemoveEnabled`; клік → наявний confirm + `WizardActions.Remove(InstalledId, DisplayName)`). Нижче — зелена панель: `Connect to Hub switches a manually installed package to CAS's UPM registry. Once connected, Hub takes over version tracking and will prompt you to update whenever a newer stable release is available.` Далі секція `ADDITIONAL COMPONENTS` — та сама таблиця з `TryGetAdditionalStatuses`. Внизу `Refresh` (наявний: `InvalidateCache` + ререндер).
- [ ] **Step 5:** Компіляція + Test Runner PASS; ручна перевірка вкладки.
- [ ] **Step 6:** Commit: `feat(installer): Main Components table (PDF terminology, Remove column, additional components section)`

### Task 11: Фінальна верифікація

**Files:**
- Modify: `CHANGELOG.md` (запис під Unreleased), `package.json` НЕ бампати (реліз поза планом)

**Interfaces:** —

- [ ] **Step 1:** Test Runner: усі EditMode-тести PSV.Installer.Editor.Tests — PASS; зафіксувати кількість.
- [ ] **Step 2:** Ручна верифікація в Unity (три сценарії зі спеки): (а) чистий проєкт → ready показує 4 компоненти, Install → progress → configure (гейт працює) → done; (б) проєкт з усім встановленим → primary `Continue`, одразу configure; (в) ручний Tenjin → Components показує `Manual install` + `Connect to Hub`, міграція проходить. Перевірити: збій мережі на кроці → панель Retry step/Copy log; `Advanced integration` → вкладка Components; повторне відкриття вікна → вкладки (IntroDone).
- [ ] **Step 3:** `CHANGELOG.md`: розділ Unreleased — перелік змін (rebrand, 3-step flow, no in-installer CAS config, Main Components, EDM4U).
- [ ] **Step 4:** Commit: `docs(installer): changelog for CAS.AI Publishing Hub redesign`
- [ ] **Step 5:** Доповісти власнику: готово до його ручної перевірки; реліз preview.37 (unity-package-signing skill → Verdaccio) — окремим кроком після його підтвердження.
