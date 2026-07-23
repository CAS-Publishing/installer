# CAS.AI Publishing Hub — редизайн інсталера (за PDF-бордом)

**Дата:** 2026-07-16
**Джерело дизайну:** `doc/CAS Publishing Installer.pdf` (борд з 11 мокапами)
**Статус:** затверджено власником 2026-07-16 (CAS-конфіг код — видалити повністю, без feature-флагів)

## Суть

Інсталер стає «чистим інсталером»: жодної CAS-конфігурації всередині (ні CAS ID на
встановленні, ні вибору аудиторії/форматів). Уся конфігурація живе в нативному вікні
CAS.AI Settings; інсталер лише **детектить** стан і веде користувача кнопками.
Брендинг — CAS.AI Publishing Hub.

## Зафіксовані рішення власника

1. **Tenjin-секція в CAS Settings** (поле Tenjin SDK Key, чекбокси Auto Init / Send
   Attribution Info) та runtime-автовідправка AttributionInfo — **реалізує команда CAS,
   не ми**. Інсталер тільки читає стан і показує «Open CAS Settings».
2. **Брендинг — повний CAS.AI:** вікно «CAS.AI Publishing Hub», меню
   `Assets → CleverAdsSolutions → Hub`, PSV зникає з UI повністю.
3. **Структура — одне вікно, вкладкова модель:** степпер «1 Install — 2 Configure —
   3 Done» лише на час інтро; після — вкладки (Components / Configuration / About).
4. **Main Components — без ADAPTER-колонки** в цій ітерації: VERSION / STATUS /
   ACTION / REMOVE + секція Additional components із каталогу.
5. **CAS-конфіг код видаляється повністю** (не ховається за флагом).

## Підхід

**A. Інкрементальний рефакторинг поточного візарда.** `WizardRouter`,
`InstallerWizardWindow` і весь бекенд (`ComponentStatusProvider`, `WizardActions`,
`AutoInstaller`, scanner / migrator / catalog) лишаються; переписується склад екранів
та їхні UXML. Відхилені альтернативи: паралельна нова реалізація зі swap (подвійна
підтримка без вигоди), мінімальний рескін (не виконує тезу PDF).

## 1. Навігація і склад екранів

- **Видалити екрани:** `welcome` (введення CAS ID), `integration` (Auto/Manual),
  стаби `hub` і `settings`.
- **Інтро-флоу (степпер):** `ready` (новий) → `progress` (перероблений) →
  `configure` (перероблений `setup`) → `done` (перероблений).
- **Вкладки після інтро:** `components` (Main Components), `configure`
  (Configuration), `about`. `IntroDone`-гейт лишається; `ResolveStartScreen`:
  незавершений setup → `ready`.

## 2. Крок 1 — Install

- **Ready to install:** список 4 дефолтних компонентів (CAS SDK, Tenjin SDK,
  Firebase Analytics, EDM4U) зі статусами з `ComponentStatusProvider`: ціль-версія
  для невстановлених, `✓ Already installed (x.y.z)` для наявних. Підпис Tenjin:
  «Attribution — handled on our end, nothing for you to configure». Примітка: «The
  installer will modify Packages/manifest.json and import required plugins. No scene
  files will be changed.» Кнопки: **Advanced integration** (→ вкладка `components`,
  знімає інтро-гейт) і **Install**; якщо все вже встановлено — primary стає
  **Continue** (одразу на `configure`).
- **Installing components…** (поточний `ProgressScreen` + нове з PDF): панель
  помилки збійного кроку з **Retry step** (ретрай лише цього кроку) і **Copy log**
  (лог кроку в буфер обміну), кнопка **Cancel**. Тексти про рекомпіляцію/resume
  відповідають наявному SessionState-механізму.
- **Installation complete** — стан того ж progress-екрана (усі ✓, зелена панель
  «All components installed», **Continue** → `configure`), не окремий клас.

## 3. Крок 2 — Configure (найбільша зміна)

- **Прибрати з інсталера всю CAS-конфігурацію:** радіо Optimal/Children Ads,
  тумблери ad-форматів, запис `audienceTagged`/`allowedAdFlags`. Інсталер більше
  **не пише** CAS-налаштування — тільки читає.
- Екран = таблиця **Component × Android × iOS** на базі `SetupModel`/`SetupChecker`:
  - **CAS SDK:** `CAS ID configured / not configured` — читання `managerIds` із
    `CASSettings<Platform>.asset`;
  - **Tenjin SDK:** `Key configured / not configured` — **feature-detect** поля
    Tenjin Key у CAS-налаштуваннях рефлексією; поки CAS не випустив секцію — рядок
    інформаційний («handled on our end») і **не блокує** Continue;
  - **Firebase Analytics:** `File detected / missing` (`google-services.json` /
    `GoogleService-Info.plist`, проби вже є в `SetupChecker`).
- Жовті панелі дій (за PDF):
  - «CAS plugin configuration required» → **Android settings** / **iOS settings**
    (відкривають нативне вікно CAS.AI на платформі);
  - «Tenjin SDK Key required» → **Open CAS Settings** (тільки коли поле
    задетектовано);
  - «Firebase Analytics — configuration file missing» → **Open Firebase Console** /
    **Locate file…** (копіює вибраний файл в Assets).
- **Гейтинг Continue:** заблоковано, доки для **хоча б однієї платформи** не
  виконані всі активні вимоги (правило «one platform is enough», мокап №8).
  Кнопки **↻ Refresh** / **Back**.
- **Configuration complete** — стан цього ж екрана (обидві платформи ✓, або одна ✓
  + друга «Not used»).

## 4. Крок 3 — Done

Зелена галочка + чеклист фактичних результатів: `CAS SDK — mediation ready
(<bundle id>)`, `Tenjin — attribution key configured`, `Firebase — analytics
connected`. Блок Next steps: «Add ad placements to your game», «Open CAS dashboard»
(лінк), «Re-open this installer anytime: Assets → CleverAdsSolutions → Hub».
Кнопки **Components** (→ вкладка) і **Close**.

## 5. Main Components (вкладка `components`)

- Таблиця **SDK / VERSION / STATUS / ACTION / REMOVE**. Мапінг статусів:
  `Installed → Up to date`, `Update available → Update required` (+ підпис
  «to vX.Y.Z» під кнопкою), `Installed (manual/legacy/git) → Manual install`,
  `Not installed`.
- Дії: **Install**, **Update**, **Connect to Hub** (= наявні Migrate/SwitchToUpm,
  перейменовані; логіка `WizardActions` без змін), **Remove SDK** (наявний Remove;
  червоне обрамлення + confirm-діалог).
- Дві секції: **Main** (дефолтний набір) та **Additional components** — решта
  записів каталогу (Crashlytics вже є; Gadsme/Xsolla з'являться через
  metadata-пакет без релізу інсталера).
- Зелена пояснювальна панель про Connect to Hub (текст із PDF). Кнопка **Refresh**
  (інвалідовує session-кеш статусів).

## 6. Брендинг

Меню `Assets/CleverAdsSolutions/Hub` (пункти `PSV Game Studio/*` видалити), назва
вікна «CAS.AI Publishing Hub», заголовки діалогів і шапка UXML — «CAS.AI Publishing
Hub», лог-префікс `[CAS Hub]`. Ім'я scoped registry в manifest не чіпаємо (не UI).

## 7. Що видаляється в коді

- `WelcomeScreen` + запис-логіка `CasIdApplier` / `InstallerKeyStore` /
  `CasIdValidation`;
- `IntegrationModeScreen`, `HubActionsScreen`, `SettingsRedirectScreen`;
- CAS-write-логіка: запис у `CasSettingsWriter`, активація в `CasMediation`,
  `CasAudience`, `AdFlagsBits`;
- відповідні UXML (`Welcome`, `IntegrationMode`, `HubActions`, `SettingsRedirect`)
  та їхні .meta;
- тести: `WelcomeScreenTests`, `CasIdApplierTests`, `CasIdApplierWriteTests`,
  `CasAudienceTests`, `AdFlagsBitsTests` — видалити/замінити тестами детекторів.
- Read-only частини (`CasIdApplier.ReadExisting` → перенести в детектор,
  `CasPresence`) лишаються для Configure-детекції.

## 8. Тести й верифікація

Нові unit-тести: мапінг статусів Main Components; гейтинг Continue
(one-platform-rule); feature-detect Tenjin-поля; retry-політика кроку install.
Ручна верифікація в Unity: (а) чистий проєкт — флоу новачка; (б) проєкт з уже
встановленими SDK — Continue-варіант; (в) проєкт із ручним Tenjin —
Connect to Hub.

## 9. Поза скоупом

ADAPTER-колонка; Tenjin-секція в CAS Settings і runtime-автовідправка
AttributionInfo (команда CAS); записи Gadsme/Xsolla в каталозі (окрема задача на
metadata-пакет); реліз preview.37 — після затвердження і ручної верифікації.

## Відомий ризик

Видаляємо CAS-radio та введення CAS ID, релізнуті в preview.36 за фідбеком
власника. Це усвідомлений розворот за PDF; рішення власника — видаляти повністю.
