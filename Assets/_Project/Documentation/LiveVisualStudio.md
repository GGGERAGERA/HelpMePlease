# Live Visual Studio — implementation and operation

## Intent and constraints

Build a live, unpaused visual workbench for the production MVP using existing uGUI/TMP. Preserve the current CombatHierarchy/Astra work and existing labs/debug APIs. No gameplay tuning, art direction, GDD edits, commits, staging or branch changes.

Original is an explicit immutable Git revision, Current is the captured working tree at session initialization, Custom is sparse user overrides. Original never means “whatever the prefab currently contains”. Code/geometry changes which cannot be represented by a visual adapter must be reported, not simulated by a misleading slider.

## Architecture decision

Extend the existing scene composition and visual preset, with a runtime profile/registry shared by release presentation and the development workbench. Component adapters expose only visual properties. Pooled creation and enemy registration supply stable type identities; scene targets are enumerated on scene load, not every frame. Runtime material clones/property blocks and cloned config protect authored assets. Group → Type → Instance precedence; instance edits never enter production JSON.

Keep the existing lab implementations behind new navigation. A separate uGUI side panel owns pointer gestures, remains independent of F1 and never owns game time. Existing input owners must consult its UI gate.

## Implementation plan

- [x] Capture starting changes and resolve the pre-CombatHierarchy Git revision using evidence/history. Generate baseline values and provenance without checking out that revision.
- [x] Implement sparse profile model, validation, precedence, comparison modes and bounded undo/redo. Add tests for per-type independence, resets, serialization and non-finite input rejection.
- [x] Implement target registration, component adapters and runtime application. Add checks for pooling and material ownership; runtime validation remains a separate acceptance step.
- [x] Add explicit ORBITAL/config/FlashLink family adapters. Preserve current visuals by default; add sibling-family/gameplay isolation tests.
- [x] Replace the F1 entry UI with a compact Russian command hub, retaining labs/APIs, search and favorites.
- [x] Implement a separate live side panel with picking, scopes, searchable property groups, direct numeric/color input, reset/history, profile import/export and explicit production save.
- [ ] Compile, test profile behavior, exercise the production scenario in the user's Editor and inspect actual Game View PNGs. Report unsupported coverage and unverified input honestly.

## Review focus

Current values survive Original/Custom switching; newborn pooled instances inherit type overrides; renderer changes do not touch colliders/shared materials; animation cannot silently overwrite tuned presentation; UI capture does not leak RMB or a drag into gameplay; imported profiles cannot create NaN/unbounded allocations; release applies approved profile without debug UI.

## Verification record

### Material provenance copy isolation — 28.10.2026

IssueKey: `live-visual-profile-material-source-copy-isolation`. Основание — Architecture decision (независимые профили и защита authored assets), Implementation plan (serialization, undo/redo). Дефект воспроизведён исполнением текущего VisualProfile.cs без Unity: Copy/ForProduction разделяют изменяемые VisualMaterialSource; изменение Target/AssetPath/ShaderName копий изменяет источник. Ремонт: независимые записи с прежними полями и порядком; null-список по-прежнему становится пустым, null-элементы сохраняются. Игровой дизайн и художественные значения не меняются.

Критерий: существующий ProductionSerializationExcludesInstanceOverridesAndRoundTripsTypeDiffs сохраняет прежние assertions Instance/round-trip, проверяет точный JSON до мутаций, Notes/Values и независимость источника, Copy и ForProduction в обе стороны. Формат JSON, адаптеры, утверждённый профиль и authored assets не меняются. Unity/XML проверяет хост; статическое воспроизведение не является PASS этого теста. Визуальных изменений нет.

Отдельное исполнение исправленного VisualProfile.cs через Add-Type подтвердило независимость в обе стороны и между копиями, порядок/null, Values/Notes и исключение Instance. Свидетельство: `Artifacts/GeneratedQA/live-visual-profile-material-source-copy-isolation-20261028.txt`. Точный JSON через JsonUtility ещё требует адресного Unity-теста.

Уточнение проверки после cycle-70 (тот же issueKey): полный `cycle-70/unity/results.xml` содержит 1 выполненный тест, 1 FAIL — ожидался null, получен VisualMaterialSource (`LiveVisualProfileTests.cs:471`). До assertion тест уже вызывал JsonUtility; это не доказательство потери null непосредственно в Copy. Проверки мутационной независимости после места отказа не были выполнены. Прямое управляемое копирование и inline-сериализация Unity имеют разные контракты представления null.

Текущий ремонт проверки: отдельный профиль с двумя настоящими источниками и null между ними проверяется до любого JsonUtility — исходный null, независимость списков и записей обеих копий, порядок, все три поля и сохранность null. Основной блок точного JSON, мутаций в обе стороны/между копиями, Notes/Values, исключения Instance и round-trip использует два настоящих источника без null. Runtime-копирование повторно не меняется; null assertion не заменяется допуском объекта. Успех — компиляция и единственный хостовый запуск `LiveVisualProfileTests.ProductionSerializationExcludesInstanceOverridesAndRoundTripsTypeDiffs`; новый XML/PASS пока отсутствует. При прямом FAIL сохранить диагностику, не ремонтировать runtime предположительно. Игровой дизайн и визуальные значения не меняются.

Код внесён. Проверка runtime и фактического изображения продолжается; наличие кода или компиляции не означает принятие визуального результата. Временные доказательства находятся только в существующем ignored `Artifacts/GeneratedQA`.

### Адресная диагностика authored pool source — 28.10.2026

Контракт: Architecture decision / Review focus выше — регистрация pooled-компонента по identity, конфигурация владельца, наследование Type overrides до первого Emit и изоляция authored assets. Это исправление QA-выбора источника, без нового игрового или визуального решения.

Первое воспроизведённое нарушение: `cycle-7/unity/results.xml` в сессии `2026-10-27T22-51-43-105Z-night` содержит один Failed, `LiveVisualProductionTests.cs:152`: повторный корневой `GetComponent<ParticleSystem>() != null` дал False. Строка совпадает с текущим исходником до этой правки; создание `SimplePrefabPool` расположено ниже. Полный editor.log заканчивает тесты кодом 2, сообщений `Pool prefab` / `Configure pool root` нет. Этот запуск не проверил pool/reuse/round-trip и не доказывает дефект пула.

Установленная ошибка предиката: `is { }` проверяет managed-null и не гарантирует Unity-non-null. Остаётся проверить, был ли именно такой wrapper причиной данного запуска, либо каталог содержит другую ссылку. Fixture теперь материализует AssetPath, GameObject и единственный полученный корневой ParticleSystem, проверяет Unity-null до чтения модулей и сохраняет диагностику всех кандидатов в уникальный `pool-source.txt`: оба вида null, instance ID, asset path, persistence, scene и корневые компоненты. Прежние критерии stopAction=None, одномерный постоянный size и отсутствие корневых MonoBehaviour сохранены.

Выбранный источник сверяется с `AssetDatabase.LoadAssetAtPath<GameObject>` по тому же каталожному пути. Только доказанное несовпадение identity/persistence/path переключает QA на явно загруженный authored prefab; тот обязан удовлетворять всем прежним критериям. Отсутствие пригодного кандидата или несовпадение пригодности с authored asset завершает сценарий точным диагностическим FAIL, без синтетического эффекта или runtime-ремонта.

Успех проверки: хост компилирует и выполняет `LiveVisualProductionTests.ProductionTypesNewSpawnsParticlesLightsAndProfileRoundTrip`; диагностика устанавливает источник, затем проходят прежние identity/owner/reuse Emit/JSON/asset-byte assertions и настоящие Game View captures. Новый XML ещё не запущен, PASS не заявлен. Production/runtime, каталог и профили не изменены. Кнопочные Save/Load, повторное открытие панели и оценка PNG — **MANUAL_PLAYTEST_REQUIRED**.

## Управление

- **F1** — новый Debug Hub: «Визуал», «Забег», «Бой и события», «Диагностика». Поиск ищет команды всех разделов; звезда сохраняет избранное разработчика.
- **Визуал → Live Visual Studio** — независимая панель. **F8** скрывает/возвращает уже открытую панель. Заголовок перемещает её, нижний правый угол меняет ширину, «—» минимизирует. Положение, ширина и прозрачность сохраняются локально.
- **Пипетка** — следующий клик по изображению выбирает зарегистрированный Renderer; при перекрытии появляется список. Picking использует игровую MainCamera и bounds, поэтому прозрачные пиксели внутри sprite bounds тоже могут попасть в список. Рамка не доказывает отсутствие перекрытия: инспектор отдельно показывает active/enabled/нахождение в кадре.
- **Type** изменяет один компонент определённого prefab-типа и его новые экземпляры. **Group** изменяет соответствующий параметр всей категории. **Instance** имеет приоритет и действует только на выбранный runtime-компонент. Instance не экспортируется в переносимые/production-профили.
- У врагов раздельные Group для обычных, подрывников, стрелков, элиты, турелей, глаз и боссов. Явное изменение параметра в Group снимает его Type/Instance overrides внутри этой категории, чтобы ползунок сразу влиял на всю выбранную группу; изменение Type снимает Instance overrides этого параметра данного типа. Undo восстанавливает заменённые настройки. При загрузке готового профиля сохраняется приоритет Instance → Type → Group.
- Число справа от slider можно вводить напрямую через точку; **±** даёт точный шаг, **Shift** уменьшает шаг в десять раз. Для цвета доступны RGB/HEX SDR/HSV; независимая насыщенность пикселей находится в «Коррекция пикселей · Lit 2D», отдельно от Tint.
- **Original** — поддерживаемые значения Git `19b6c01af57f91a3cb0248e5767079e50a1f2859`; **Current** — захваченное состояние текущей рабочей копии при регистрации объекта; **Custom** — Current плюс пользовательские изменения. Отсутствующее Git-значение явно обозначается «нет → Current». Переключение не стирает Custom.
- Undo/Redo и сбросы изменяют временный профиль. После изменения параметров не требуется подтверждение. Геометрия опасностей и боевые таймеры не редактируются.
- **Save JSON / Load** работают с именованными различиями в `Assets/_Project/Data/World/Presentation/Profiles` (Development player: `persistentDataPath/VisualProfiles`). Clipboard export/import тоже использует JSON.
- Только явная кнопка **Применить к production** записывает утверждённый snapshot `Data/World/Presentation/LiveVisualProduction.json` и назначает его существующему `VisualTuningSavedValues.asset`. Именованные JSON в `Profiles` остаются черновиками: последующий Save JSON не изменяет уже утверждённый production snapshot. Обычная release-сборка применяет утверждённый профиль через штатную production composition; debug UI в ней отсутствует. Ползунки не сохраняют prefab/material/config assets.

## Подключённые контракты

SpriteRenderer: tint/alpha, sorting/flip, независимые hue/saturation/brightness/contrast/gamma для существующего Subject42 Lit shader и URP Sprite-Lit-Default (временный совместимый material clone). Поддержанные shader float/range/color/vector читаются из настоящего shader. MaterialPropertyBlock сохраняет свойства других владельцев; combat-flash временно сохраняет владение material.

Light2D/Light: RGB/intensity, поддерживаемые радиусы/falloff/shadow intensity локальных Light2D. Global Light не показывает неработающие радиусы/alpha/shadows. Camera: orthographic size. Существующие Volume: Bloom и ColorAdjustments; редактируется runtime profile, включая корректное восстановление overrideState.

Particles: size/speed/lifetime, rate over time при включённом модуле, ограниченный maxParticles, отдельный Tint/Alpha через colorOverLifetime. Это позволяет окрашивать и частицы с C# EmitParams, сохраняя исходный startColor. Размер/lifetime действуют на новые частицы; Tint/Alpha — на уже существующие. Настройки извлекаемых из SimplePrefabPool объектов применяются до активации/синхронного Emit. TrailRenderer/LineRenderer: width, цвета/alpha; TrailRenderer.time. Линии станции принадлежат специализированному ORBITAL-адаптеру.

ORBITAL: RingSurface, tier palette/emission/glow, ядро/halo/визуальные интервалы, цвет и следы трёх семейств снарядов. FlashLink имеет независимые семейства Other, MuzzlePistol/Shotgun/Sniper, ImpactPistol/Shotgun/Sniper, Impact, Arc, CoreRay, Relocation. У каждого есть tint/примесь цвета/HDR/alpha/множитель duration/width. Структурные настройки, размер swept hit shape, урон, cadence, радиусы поражения и progression исключены.

## Известные границы покрытия

- Original содержит 8 004 конечных значения, 453 идентичности материалов и происхождение из 156 Git blobs. Это Git-снимок, а не ранний QA-кадр «before», уже содержавший незакоммиченные изменения. Старые mine-map/bunker/lab сцены и два неразрешённых старых turret-location пути не входят в snapshot; подробности находятся в Notes JSON.
- Изменения C#-структуры, генерации геометрии, состава prefab и поведения AI не восстанавливаются numeric slider. Для точного сравнения всего старого поведения нужен отдельный checkout/build указанного commit с изолированным сохранением. Текущий Editor и рабочее дерево автоматически не откатываются.
- Смена texture, изменение shape/bursts/sub-emitters и редактирование Animator не подключены. Специализированные custom shaders с другим lighting/alpha-контрактом показывают доступные shader-поля и причину отсутствия независимой коррекции пикселей.
- Безопасный изолированный replay VFX не подключён: перезапуск gameplay-объекта может нанести урон/запустить callbacks. Соответствующей фиктивной кнопки нет.
- Масштаб доступен только отделённым visual leaf-компонентам без физики/скриптов; ORBITAL-модули исключены, поскольку sprite geometry участвует в попаданиях. Радиусы предупреждений, точки линий и root transforms не регулируются.
- Реестр обновляется при композиции сцены, спавне врагов, pool creation/reuse, создании предупреждений/взрывов/декора и ORBITAL-компонентов. Для прочих сторонних/procedural объектов есть явное «Обновить»; полного поиска сцены каждый кадр нет.
- Полное покрытие всех визуальных систем не заявляется. В частности, legacy FEEL/Visual Lab остаются отдельными инструментами; изменение одного и того же presentation-параметра одновременно из двух лабораторий не является поддерживаемым workflow.

### Именованный файловый Save/Load — адресный контракт, 28.10.2026

issueKey: `live-visual-studio-named-file-save-load-contract`. Контракт — раздел «Управление / Save JSON / Load» выше; нового игрового или художественного решения нет. Основание: прежний ProductionSerializationExcludesInstanceOverridesAndRoundTripsTypeDiffs проверяет только сериализацию в памяти. Добавлен один сценарий `LiveVisualProfileTests.StudioNamedFileSaveLoadPreservesDraftAndRejectsInvalidImport`, вызывающий настоящие приватные SaveProfile(false), LoadProfile и LoadJson через reflection, без Open/Show, PlayerPrefs, production save или регистрации production-сцены.

Сценарий использует настоящий временный LiveVisualRuntime, копию preset с собственным временным TextAsset валидного production-профиля, минимальные TMP-поля и временный SpriteRenderer с существующим адаптером alpha. Preset назначается только локальному runtime, без Initialize/Attach и замены глобального Instance. Проверяются Type/Group в реальном уникальном NightShift-<GUID>.json, исключение Instance из файла при неизменном живом Custom и режиме, импорт TextAsset, изменение alpha после сохранения и восстановление сохранённого значения через LoadProfile. Синтаксически неправильный LoadJson должен выбросить ArgumentException до изменения Custom, режима, Revision или alpha. После каждого файлового действия и отказа импорта проверяются жизнеспособность и идентичность назначенного локального TextAsset. Authored reference проверяется через Unity null semantics и serialized objectReferenceInstanceIDValue; байты каталожных prefab, исторических материалов, preset, baseline и выбранного authored production JSON сравниваются с исходными.

Файл резервируется FileMode.CreateNew, чтобы не перезаписать чужой профиль. Finally освобождает адаптеры и временные объекты, включая локальный TextAsset, удаляет только собственные JSON/meta. Если ни каталог Profiles, ни его meta не существовали до сценария, созданный каталог удаляется только пустым: через AssetDatabase.DeleteAsset с нерекурсивным filesystem fallback, затем синхронный Refresh. Проверяются отсутствие собственного файла, каталога и их регистрации в AssetDatabase. Существующие профили и каталоги не удаляются. Runtime/Dev-путь Save/Load не исправлялись: воспроизведённого дефекта нет.

Cycle-16: полный XML содержит 6 выполненных тестов, 5 Passed и 1 Error. Файловые assertions до строки 111 выполнены; затем SameAs для незаданной production-reference сравнил managed-обёртки, а NUnit при форматировании fake-null вызвал TextAsset.ToString и UnassignedReferenceException. В authored preset сериализовано LiveProductionProfile: {fileID: 0}. Это отказ проверки ссылки, а не доказанный дефект Save/Load. Editor.log также сообщил оставленную регистрацию нового Profiles, хотя физически каталог отсутствовал: прежняя очистка удаляла его напрямую без синхронизации AssetDatabase. Ремонт сценария устраняет оба недостатка; отсутствие предупреждения ещё должен подтвердить хост.

Cycle-17 подтверждён полным XML: LiveVisualProfileTests.StudioNamedFileSaveLoadPreservesDraftAndRejectsInvalidImport — Passed, total=1, failed=0. Editor.log содержит импорт собственного NightShift JSON; прежнее предупреждение об оставленном Profiles не найдено. Настоящие файловые Save/Load, отказ плохого импорта и очистка прошли адресную хостовую проверку. Дефект самого Save/Load не воспроизведён. Это проверка файлового контракта, не production startup, полный UI или визуальное принятие. **MANUAL_PLAYTEST_REQUIRED:** F1 → «Визуал», сохранить именованный черновик, изменить параметр, загрузить его; проверить сообщение и значение, затем скрыть/открыть панель. Художественные параметры проекта не менялись.

### Размерный контракт Save/Load — 28.10.2026

IssueKey: `live-visual-studio-save-load-size-asymmetry`. До исправления код SaveProfile записывал Export без проверки длины, затем проверял только структуру VisualProfile. LoadJson имел две проверки 8/4 × 1024 × 1024; фактический предел составлял **4 194 304 символа строки .NET**, не байты. VisualProfile.Validate не ограничивает Notes, Copy/ForProduction сохраняют их: валидный профиль с длинной заметкой может превышать этот предел. Это статически подтверждённая асимметрия; исполнение исходного SaveProfile с большим профилем в Unity в worker-фазе не проводилось, поэтому отдельного воспроизведённого FAIL/XML нет.

Исправление Dev-пути: одна ValidateProfileText применяется в LoadJson и к Export до создания каталога, записи и импорта. Предел сохранён. Runtime, authored assets, production-reference и художественные параметры не меняются; нового дизайнерского решения нет.

Расширен прежний StudioNamedFileSaveLoadPreservesDraftAndRejectsInvalidImport: локальная копия профиля получает валидную Notes-строку; настоящий Export должен оказаться выше предела менее чем на 4096 символов и пройти структурную Validate. Настоящие SaveProfile(false) и LoadJson обязаны отказать. Проверяются отсутствие нового JSON/meta и создания каталога, сохранение точных байтов собственного существующего JSON/meta, Custom (ссылка и JSON), Mode, Revision, alpha и production-reference. Обычный успешный файловый контракт сохранён; используются прежние уникальное имя, локальный preset и finally. Проверка отсутствующего файла выполняется до его эксклюзивного резервирования; finally владеет возможными следами неудачного действия только по заранее проверенному уникальному пути.

Статический просмотр и адресный git diff --check выполнены. Cycle-22 подтверждён XML: `LiveVisualProfileTests.StudioNamedFileSaveLoadPreservesDraftAndRejectsInvalidImport` — Passed, total=1, failed=0; завершающий участок editor.log подтверждает штатное завершение. Это проверка исправленного контракта, не фактическое воспроизведение старой реализации. **MANUAL_PLAYTEST_REQUIRED:** в F1 → «Визуал» сохранить и загрузить небольшой именованный черновик; для большого черновика проверить сообщение с лимитом в символах и сохранность предыдущего файла. Художественное принятие и полное восстановление панели этим тестом не подтверждаются.

### Ограниченное чтение именованного JSON — 28.10.2026

IssueKey: `live-visual-studio-bounded-profile-file-read`. Основание — Review focus: imported profiles cannot create NaN/unbounded allocations. LoadProfile прежде вызывал File.ReadAllText до проверки размера. Теперь StreamReader с прежним распознаванием BOM передаёт в LoadJson не более **4 194 304 декодированных символов плюс один** для обнаружения превышения. Файловое чтение и ValidateProfileText используют общий именованный предел; байтовый лимит не вводится. Это исправление Dev-импорта, без изменения игрового дизайна, runtime production или художественных параметров.

Существующий `LiveVisualProfileTests.StudioNamedFileSaveLoadPreservesDraftAndRejectsInvalidImport` расширен счётчиком TextReader с короткими чтениями: проверяются точный префикс и остановка на limit+1. На собственном уникальном пути проверяются UTF-16/BOM-файл ровно на символьном пределе (его размер в байтах выше 4 MiB) и отказ oversized-файла без изменения Custom, Mode, Revision, отображаемого alpha и production references. Прежние байты восстанавливаются во вложенном finally; исходные проверки Save/Load, meta и authored assets сохранены. Успех — адресная компиляция и этот тест у хоста; новый XML ещё не запущен, PASS не заявляется. Ручное художественное принятие для этой правки не требуется.

### Уничтожение владельца курсора — подготовка воспроизведения, 28.10.2026

Уточнение после cycle-23: полный XML содержит 7 тестов, 6 Passed, 1 Failed. Сценарий остановился на подготовке `Cursor.lockState`: ожидался Locked, среда вернула None (строка 49), **до DestroyImmediate и до проверки видимости**. Завершение editor.log штатное, код 2. Это доказательство ограничения среды; невосстановление курсора открытым владельцем ещё не воспроизведено.

Следующая адресная проверка сначала независимо пробует `Cursor.visible=false/true` при `lockState=None`, до создания объектов. Если переключение невозможно, результат — Inconclusive с наблюдаемыми возможностями hide/show, без заявления PASS. Если возможно, реальные закрытый и открытый владельцы уничтожаются сначала в отдельном visibility-only случае с сохранённым None. Затем проверяется сохранённый Locked, только если среда действительно принимает его; ожидаемый Locked в этой ветке не заменяется None. Неподдерживаемый Locked выводится в диагностику до уничтожения; даже при успешной visibility-only части итог остаётся Inconclusive, поскольку захват не проверен. Наблюдаемый отказ восстановления видимости остаётся строгим FAIL. Исходный курсор, собственные объекты и static instance восстанавливаются в finally при любом исходе.

В этой worker-фазе изменены только сценарий и этот документ: OnDestroy/Hide оставлены прежними, поскольку фактического воспроизведения пока нет. Новый запуск, компиляция и XML ожидают хоста; результат переключения видимости заранее неизвестен. Контракт и адресный фильтр ниже сохранены. **MANUAL_PLAYTEST_REQUIRED:** восстановление Locked в интерактивной среде и передача курсора открытому F1.

IssueKey: `live-visual-studio-destroy-cursor-restoration`. Контракт — Architecture decision: независимая Dev-панель владеет указателем. Show сохраняет прежние Cursor.visible/lockState и захватывает курсор; Hide возвращает его, если F1 закрыт. OnDestroy сейчас только отписывается от runtime и очищает instance. Чтение кода показывает асимметрию, но ещё не доказывает наблюдаемое нарушение в Unity. Игровой дизайн, production и художественные параметры не меняются.

До ремонта требуется фактическое воспроизведение. Добавлен `LiveVisualProfileTests.StudioDestroyRestoresCursorOnlyForOpenOwner`: минимальный собственный root, uiRoot и reflection, без Open/Show/Build/Hide, PlayerPrefs и runtime. Сначала проверяется уничтожение закрытого экземпляра без изменения курсора, затем открытого — с восстановлением сохранённых значений. Компонент уничтожается настоящим DestroyImmediate при ещё существующем uiRoot. Root кратко активируется без передачи кадра и затем деактивируется: Unity вызывает OnDestroy только для ранее активного объекта. Тест требует отсутствия существующей static Studio и закрытого F1; проверяет, что среда действительно принимает обе комбинации состояния курсора, и что OnDestroy очищает instance. Finally уничтожает только собственные объекты, восстанавливает исходную static-ссылку и курсор.

Unity в worker-фазе не запускалась. OnDestroy намеренно оставлен прежним до результата хоста; новый PASS и воспроизведённый дефект пока не заявляются. Адресная проверка: `live-visual` / `LiveVisualProfileTests.StudioDestroyRestoresCursorOnlyForOpenOwner`. Если batchmode отвергает установку курсора, это ограничение среды, а не воспроизведение дефекта; assertions не ослаблять. После подтверждённого отказа восстановления — минимальный Dev-helper для Hide/OnDestroy с сохранением передачи курсора открытому F1, без вызова Hide из OnDestroy (Hide пишет PlayerPrefs). **MANUAL_PLAYTEST_REQUIRED:** в отдельной игровой сессии открыть Studio при закрытом F1, уничтожить её владельца при переходе сцены и проверить возврат управления; отдельно повторить при открытом F1 и проверить сохранение доступного курсора меню. Полное открытие и переход сцены этим изолированным тестом не покрыты.
