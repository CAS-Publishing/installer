# PSV Game Studio Installer — как это работает

> Внутренний документ для презентации и утверждения функционала перед публикацией.
> Версия инсталлера: `com.psvgamestudio.installer` **0.0.1-preview.39**, каталог метаданных
> `com.psvgamestudio.installer.metadata` **0.0.2-preview.28**. Только Editor, Unity 2022.3+.

---

## TL;DR (одним абзацем)

Инсталлер — это Editor-инструмент, который ставит и поддерживает в актуальном состоянии
издательский стек PSV/CAS (CAS Mediation, Tenjin, Firebase) в проекте клиента **без ручного
редактирования `Packages/manifest.json`**. Он сам прописывает нужные scoped-реестры, ставит
пакеты в правильных версиях, переносит «старые» (legacy / .unitypackage / git) установки на UPM,
находит дубликаты и показывает готовность конфигурации по платформам, ведя клиента прямо в нужное
нативное окно (например, CAS.AI Settings для CAS App ID). Список пакетов и правила миграции
живут в **отдельном data-пакете** (`...installer.metadata` → `catalog.json`), поэтому новые пакеты
и правила доезжают до клиентов **без выпуска новой версии инсталлера**.

---

# Часть 1. Обзор функционала

## 1.1 Какую проблему решает

Клиент получает Unity-проект, в который нужно завести рекламу/аналитику/атрибуцию. Сделать это
вручную — значит: добавить scoped-реестры, подобрать совместимые версии CAS/Tenjin/Firebase,
не словить дубликаты от ранее импортированных `.unitypackage`, прописать CAS App ID в настройки,
настроить EDM4U. Это долго и ошибкоопасно. Инсталлер сводит всё это к одной кнопке.

## 1.2 Как ставится сам инсталлер (3 способа)

| Способ | Кому | Что делает |
|---|---|---|
| **A. UPM scoped registry** | Проекты уже на UPM | Регистрируется реестр `https://npm.psvgamestudio.com/` (scope `com.psvgamestudio`), добавляется пакет инсталлера. |
| **B. Git URL** | Быстрая проба / CI | `https://github.com/CAS-Publishing/installer.git` (опц. `#версия`). Реестр не нужен. |
| **C. Bootstrap `.unitypackage`** | Legacy-клиенты, «ноль настроек» | Крошечный `.unitypackage` сам прописывает реестр + зависимость, дёргает resolve, ставит метаданные, открывает визард и **самоудаляется**. |

> Клиент вручную регистрирует только scope `com.psvgamestudio` (способ A) — чтобы получить сам
> инсталлер. Все остальные реестры (CAS `com.cleversolutions`, Tenjin `com.tenjin`,
> Firebase `com.google`) инсталлер добавляет **сам**, когда ставит соответствующий компонент.

## 1.3 Сценарий клиента end-to-end (первый запуск)

1. Любым из 3 способов в проект попадает пакет инсталлера.
2. При загрузке Editor инсталлер видит, что метаданных нет → **сам ставит** data-пакет каталога.
3. После переподгрузки доменов каталог читается → проект сканируется → **открывается окно
   CAS.AI Publishing Hub**.
4. **Ready:** список основных компонентов (CAS SDK, Tenjin SDK, Firebase Analytics, EDM4U) с
   текущим статусом каждого. Кнопка **Install** ставит всё, чего не хватает; если всё уже стоит —
   сразу переходит на **Configuration**. Ссылка **Advanced** пропускает авто-установку и ведёт
   прямо на вкладку **Components** для точечного выбора.
5. **Progress:** компоненты ставятся **по одному**, переживая перезагрузки доменов между шагами;
   прогресс-бар и список шагов показывают статус.
6. **Configuration:** готовность по платформам (Android/iOS) для того, что уже установлено.
   **Continue** разблокируется, как только хотя бы одна платформа полностью настроена.
7. **Done:** информационный чек-лист (CAS / Tenjin / Firebase) для активной платформы. Дальше
   клиент работает с вкладками (Components / Configuration / About) — к Ready/Progress/Done он
   больше не возвращается (кроме явного `Hub (Restart Intro)`).

## 1.4 Экраны визарда

| Экран | Назначение |
|---|---|
| **Ready** | Шаг 1: список основных компонентов с текущим статусом, кнопка Install (или переход на Configuration, если всё стоит), ссылка Advanced → Components. Только при первичной настройке. |
| **Progress** | Шаг 2: пошаговая установка (переживает reload’ы), инлайн-панели ошибки/завершения. |
| **Configuration** | Шаг 3 интро **и** отдельная вкладка после интро: готовность по платформам для установленных компонентов + точечные действия (клик по CAS/Tenjin открывает нативные настройки CAS.AI, клик по Firebase — пингует/ищет конфиг-файл). |
| **Done** | Шаг 4: итоговый чек-лист по активной платформе, ссылки на Components / cas.ai. Только при первичной настройке. |
| **Components** | Живой список пакетов (Main + Additional components) с состоянием каждой строки и действиями: Install / Update / Connect to Hub (Migrate) / Fix / Remove SDK. |
| **About** | Текущая версия инсталлера, проверка реестра на более новую и **самообновление**; красная точка = доступно обновление. |

Экраны Ready/Progress/Configuration/Done образуют первичный интро-флоу (степпер "1 Install →
2 Configure → 3 Done" в шапке окна); после него окно всегда открывается на вкладках
Components/Configuration/About (Configuration используется в обоих местах — тем же экраном).

## 1.5 Что инсталлер умеет (инвентарь функций)

- **Скан проекта** — определяет реальное состояние каждого пакета по `manifest.json`.
- **Установка / обновление** пакетов в версиях из каталога (min / recommended).
- **Миграция legacy → UPM** — переносит старые npm-id, `.unitypackage`-установки и git-установки
  на правильную UPM-установку из реестра.
- **Обнаружение установок «мимо UPM»** (через `.unitypackage` / руками в `Assets/`) — чтобы
  кнопка Install не плодила **дубликаты**, которые ломают проект.
- **Удаление пакета** (Remove) прямо из вкладки — без ручной возни в Package Manager; заодно путь
  отката кривой установки.
- **Управление реестрами** — сам добавляет нужные scoped-реестры в `manifest.json`.
- **Индикация готовности CAS/Tenjin/Firebase** — вкладка Configuration только **читает** и
  показывает статус; сам ввод CAS App ID, ad formats, сетей медиации и Tenjin key теперь целиком
  происходит в нативном окне настроек CAS.AI (инсталлер туда не пишет — только открывает нужное
  окно/платформу по клику).
- **Самообновление** инсталлера и **фоновое обновление каталога** метаданных.

## 1.6 Поддерживаемые компоненты (по умолчанию)

| Компонент | Package id | Источник | Версия (recommended) |
|---|---|---|---|
| **CAS Mediation** | `com.cleversolutions.ads.unity` | OpenUPM / git | 4.6.6 (min 4.5.4) |
| **Tenjin SDK** | `com.tenjin.sdk` | PSV Verdaccio / git | 1.15.14-psv.2 |
| **Firebase Analytics** | `com.google.firebase.analytics` (+ app, EDM транзитивно) | PSV Verdaccio / git | 13.1.0-psv.1 |
| **EDM4U** | `com.google.external-dependency-manager` | PSV Verdaccio / git | 1.2.186-psv.1 (min 1.2.161) |

Плюс **Additional components** на вкладке Components (не входят в набор по умолчанию Ready, но
доступны из каталога): `PSV Analytics (Firebase)` и `PSV Remote Config (Firebase)` (оба требуют
`com.psv.core` и показываются в списке только когда он уже есть в проекте), `PSV Tenjin
(Attribution)`.

---

# Часть 2. Как это работает (технически)

## 2.1 Архитектура: два пакета

- **`com.psvgamestudio.installer`** — вся логика и UI (Editor-only). Ничего не попадает в билд игрока.
- **`com.psvgamestudio.installer.metadata`** — данные: `catalog.json` (список пакетов, версии,
  правила миграции, реестры, конфиги). Инсталлер **не хардкодит** список пакетов — он читается из
  каталога. Поэтому новые пакеты/правила публикуются обновлением **только** data-пакета.

## 2.2 Автозапуск (bootstrap)

`Bootstrap` (`[InitializeOnLoad]`) на каждой загрузке доменов делает `delayCall → RunOnce`:

1. В batch-режиме (CI) — выходит, чтобы не трогать `manifest.json` и не висеть.
2. `CatalogLoader.Load()`:
   - **NotInstalled** → `MetadataAutoInstall.Run()` (прописывает scoped-реестр, спрашивает у
     Verdaccio последнюю версию, ставит data-пакет) и выходит — UI появится на следующем reload.
   - **Unreadable** → лог-ошибка, **без** переустановки (иначе цикл).
   - **Ok** → лог, фоновое обновление каталога, скан, открытие визарда.
3. Для **git-установленного** инсталлера метаданные тоже тянутся через git-зеркало (без Verdaccio и
   без scoped-реестра) — `InstallerSource.IsGit()`.

Открытие окна делает `InstallerWizardWindow.ShowIfReportChanged(report)`. Флаг `IntroDone`
(в `EditorPrefs`, на проект) различает «первый запуск» и «уже настроен».

## 2.3 Реестры

| Реестр | URL | Scopes |
|---|---|---|
| **PSV Verdaccio** | `https://npm.psvgamestudio.com/` | `com.psvgamestudio`, `com.google`, `com.tenjin` |
| **OpenUPM** | `https://package.openupm.com` | `com.cleversolutions` (CAS) |

Git-режим инсталлера обходится **без** scoped-реестров — всё тянется по git-URL из зеркал
`CAS-Publishing/*`.

## 2.4 Скан и классификация состояний

Источник истины об установке — `Packages/manifest.json`. `StateClassifier` (чистая логика, без I/O)
по записи в манифесте + данным проб выдаёт состояние строки:

| Состояние | Текст на вкладке Components | Действие |
|---|---|---|
| `UpmCurrent` | Up to date | — |
| `UpmOutdated` / `UpmBelowMin` | Update required | **Update** |
| `LegacyUpm` / `LegacyAssets` | Manual install | **Migrate** |
| `Conflict` | Mixed install | **Fix** |
| `ScopeMissing` (external) | Needs registry | **Fix** |
| `InstalledOutsideUpm` (external) | Manual install | **Connect to Hub** |
| `InstalledLegacy` (external) | Manual install (с подсказкой — обнаруженный legacy id) | — |
| `NotInstalled` | Not installed | **Install** |

(`ComponentsViewMap` — presentation-слой поверх этой классификации: переводит внутренние
состояния сканера в текст и подпись кнопки, показанные на вкладке Components; таблица выше уже
приводит итоговый текст, не внутренний `StatusText` сканера.)

### Обнаружение установок «мимо UPM»

`.unitypackage`/ручные установки в манифесте не видны, поэтому помимо манифеста работают 3 слоя
(`AssetInstallProbe`), любой срабатывает → `InstalledOutsideUpm`:

1. **Рефлексия** по загруженным типам (namespaces + имена) против `assetMarkers` каталога — ловит
   asmdef/DLL/сырые `.cs` одинаково (всё компилируется в типы).
2. **Наличие на диске** папок из `assetRoots`.
3. **Сигнатуры файлов** (`legacyAssetFiles`: имя + контент-маркеры) — для «разбросанных» legacy-файлов.

Это и есть защита от дубликатов: пока SDK замечен «мимо UPM», кнопка Install заменяется на
**Migrate to UPM**, а авто-установка такой компонент пропускает (иначе опрос завис бы навсегда).

## 2.5 Миграция

- **legacy npm-id / legacy assets → UPM:** убрать старое, добавить канонический пакет,
  зарегистрировать scope.
- **«мимо UPM» (.unitypackage) → UPM:** строго **в два шага** — сначала удалить ручную копию,
  только при успехе зарегистрировать scope + добавить пакет (одношаговый план дал бы дубликат, если
  удаление заблокировано git-гардом).
- **Удаляются ТОЛЬКО объявленные в каталоге `assetRoots`** — никакого «обхода `Assets/`». Папку
  нельзя вывести из случайного пользовательского скрипта, поэтому пользовательские папки
  (`Assets/Scripts`, …) под удаление попасть не могут.
- **Общие папки** (`Assets/Plugins` и т.п.) **не удаляются автоматически** — окно подтверждения
  (`MigrateConfirmWindow`) показывает их **отдельным предупреждением** «удалите вручную», а в
  списке на удаление перечисляет **конкретные** папки SDK (Firebase, CleverAdsSolutions, Tenjin…).
- **`GitGuard` + `PathSafety`:** удаление отказывает на untracked/dirty файлах («сначала закоммить»)
  — безопасно по умолчанию, отката не теряем (`git restore .`).

`assetRoots` по компонентам: CAS → `CleverAdsSolutions`; Tenjin → `Tenjin`; Firebase →
`Firebase`, `ExternalDependencyManager`, `PlayServicesResolver`, `Editor Default Resources/Firebase`.

## 2.6 Пошаговая авто-установка

`AutoInstaller.StartAll` (запускается кнопкой **Install** на экране Ready) строит план, показывает
диалог подтверждения и уводит на `Progress` (`IntroDone` выставляется позже, на входе в `Done`, а не
здесь — иначе степпер шапки пропал бы раньше времени). `ProgressScreen` ведёт установку **по одному
компоненту**: опрашивает реальный список UPM (`Client.List`), ставит следующий, переживает
перезагрузку доменов между шагами (состояние в `SessionState`: `Active` / `IssuedIndex`), и по
завершении показывает инлайн-панель «Installation complete» с кнопкой **Continue** → `Configure`.
Никакой конфигурации (CAS App ID и т.п.) на этом шаге больше не применяется — этим занимается
исключительно нативное окно CAS.AI (см. 2.7).

## 2.7 Конфигурация компонентов

Экран **Configuration** ничего не пишет в проект — это read-only индикатор готовности, а точечные
клики ведут пользователя в нужное место:

- **CAS / Tenjin:** клик по ячейке открывает нативное окно настроек **CAS.AI** для выбранной
  платформы (`CasNativeSettings.Open`) — именно там теперь вводятся CAS App ID, ad formats, сети
  медиации и Tenjin key. Готовность CAS для чек-листа Done читается **только на чтение**
  (`CasSettingsReader.ReadExisting`) из `config.kind = settingsAssetField`
  (`Assets/CleverAdsSolutions/Resources/CASSettings{Android,iOS}.asset`, поле `managerIds`);
  плейсхолдер `demo` считается «пусто». Инсталлер этот asset больше не редактирует.
- **Firebase:** `config.kind = assetFile` — проверка наличия `google-services.json` /
  `GoogleService-Info.plist`; если файл найден, клик подсвечивает его в Project-окне, если нет —
  открывает диалог **Locate file…** (с подсказкой-ссылкой на консоль Firebase).

## 2.8 Модель данных каталога (`catalog.json`)

- `registries` — алиасы реестров (`psv`, `openupm`).
- `packages[]` — PSV-пакеты (id, реестр, `legacyNpmIds`, `minVersion`, `recommendedVersion`).
- `external[]` — сторонние SDK (CAS/Tenjin/Firebase): `scopes`, `assetMarkers`, `assetRoots`,
  `legacyManifestIds`, `legacyAssetFiles`, `config[]`, `git.packages[]`, `modules[]` (для Firebase —
  analytics / remote-config / installations), `versionType/versionField` (чтение версии из типа).
- `uninstall[]` — что снять как лишнее (напр. legacy `com.psv.unity.edm`: EDM приходит транзитивно с
  `com.google.firebase.app`).

Git-зеркала (для способа B и git-установки компонентов): CAS — `cleveradssolutions/CAS-Unity`;
Tenjin — `CAS-Publishing/tenjin-sdk`; Firebase — `CAS-Publishing/firebase-analytics` + `firebase-app`
+ `external-dependency-manager` (EDM 1.2.186).

## 2.9 Обновления

- **Инсталлер:** вкладка About проверяет Verdaccio на более новую версию и обновляет «на месте»
  (только UPM-путь; git-установка обновляется сменой `#тега`).
- **Каталог метаданных:** обновляется **тихо в фоне** при загрузке Editor — новые пакеты и правила
  миграции появляются без действий пользователя.

---

# Часть 3. Текущий статус и известные ограничения

> Раздел для утверждения: что готово, что в работе. Базируется на полевом фидбеке клиента (2 раунда)
> и текущем коде ветки `feat/installer-wizard-ui`.

## 3.1 Реализовано

- 3 способа установки инсталлера (UPM / git / bootstrap), self-update, фоновое обновление каталога.
- Скан + классификация состояний, установка/обновление/удаление.
- Окно **CAS.AI Publishing Hub**: интро-флоу Ready → Progress → Configuration → Done (Welcome с
  вводом CAS App ID и выбор Express/Manual убраны — конфигурация CAS теперь целиком в нативном
  окне CAS.AI); Ready показывает 4 компонента по умолчанию с уже-установленным детектом.
- Вкладка Components: Main components + Additional components (адаптеры `PSV Analytics (Firebase)`,
  `PSV Remote Config (Firebase)`, `PSV Tenjin (Attribution)`), компаунд-миграция легаси
  `com.psv.firebase.base` в нативный Firebase + адаптеры.
- Обнаружение установок «мимо UPM» (рефлексия + диск + сигнатуры) и защита от дубликатов.
- Миграция с удалением **только** объявленных `assetRoots`; безопасное окно подтверждения; git-гард.
- Пошаговая установка, переживающая reload’ы; авто-открытие Hub после первой установки (включая
  git-установку) работает без ручного рестарта редактора.
- Самовосстановление отсутствующего scoped-реестра («Needs Registry» + **Fix**) без рестарта.

## 3.2 В работе / запланировано

| Тема | Статус | Примечание |
|---|---|---|
| **EDM4U: gradle-шаблоны / манифест** | частично решено | Banner «Enable Android build settings» на вкладке Configuration чинит недостающие gradle/manifest-шаблоны одним кликом (`AndroidBuildFix`), но это по-прежнему ручное действие, не полностью автоматическое. |
| **«Fix» для git-установленного пакета** | уточняется | Точный сценарий воспроизведения уточняется у клиента (обычный git → UPM переход через **Connect to Hub** уже работает). |
| **Подписи пакетов (Missing signature)** | в работе | Firebase уже публикуется подписанным (`13.1.0-psv.1`); остальные пакеты — по мере перевыпуска. |

## 3.3 Гарантии безопасности

- **Editor-only** — ничего не попадает в билд игрока.
- **Никакого ручного `manifest.json`** — все правки делает инсталлер идемпотентно.
- **Удаление только своих папок SDK** — пользовательские папки под удаление не попадают;
  общие папки (`Assets/Plugins`) только предупреждение.
- **Git-safe** — удаление отказывает на незакоммиченных файлах; откат через `git restore .`.

---

*Документ описывает поведение на момент инсталлера 0.0.1-preview.39 / каталога 0.0.2-preview.28.*
