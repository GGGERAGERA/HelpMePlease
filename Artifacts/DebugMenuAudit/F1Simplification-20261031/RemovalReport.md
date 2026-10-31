# F1 — результат упрощения

В production MVP F1 открывает ПЕСОЧНИЦУ. Основная навигация: **ПЕСОЧНИЦА | ВИЗУАЛ | QA / ЛАБОРАТОРИИ**. Поиск, избранное, возврат и внешнее владение вводом сохранены. Старые ID избранного отфильтровываются по оставшимся командам.

Физически удалены файлы (вместе с `.meta`):

- `Assets/_Project/Dev/Debug/F1/Subject42DebugMenu.Pickups.cs` — исключительно UI удалённой страницы мира, её testPickups/pathHits и helpers.
- `Assets/_Project/Dev/Tests/Core/Editor/F1PickupDebugTests.cs` — единственная проверка этой удалённой UI-панели.
- `Assets/_Project/Dev/Editor/OrbitalLab/MvpF1Verification.Play.cs` — верификатор удалённых индексированных страниц. Независимый Audit производства в `MvpF1Verification.cs` сохранён.

Физически удалённые методы и состояние:

- Каталоги/страницы: `BuildMvpTab`, `BuildMvpOrbits`, `BuildMvpCatalogue`, `BuildMvpCardPreview`, `AddMvpBuildReward`, `BeginMvpReward`, `BuildMvpEnemies`, `BuildMvpPlayer`, `BuildMvpWorldEvents`, `AddOrbitalProductionSection`, `AddPlayerBuildSection`, `AddWorldEventsSection`, `AddEnemiesSection`.
- Вся F9-панель: `OpenMvpOrbitSurface`, `ToggleMvpOrbitSurface`, `BuildMvpOrbitSurface`, `BuildMvpOrbitSaveFooter`, `SaveMvpOrbitSurface`, `MvpOrbitSlider`, `ConfigureMvpOrbitSurfaceDrag`, `ConstrainMvpOrbitSurfacePosition`, `ReturnFromMvpOrbitSurface`, `ApplyMvpViewportLayout`, `AddSliderRow`, hotkey/backup/footer/drag/live-simulation state. Неиспользуемый `OrbitalPresentationConfig.SaveRingAppearance` и его private authored reference удалены; старый reader PlayerPrefs оставлен для совместимости development player.
- Исключительные handlers/helpers: `CompleteLevel`, menu-local `FindAliveBoss`, `AddCaptureEncounter`, `AddCaptureSpawnControls`, `AddCaptureToggle`, `CaptureWorldRuleLabel`, `ResolveEnemyPrefab`, `AddManualEnemyRows`, `SpawnDebugEnemies`, `ToggleEnemyAutoSpawn`, `ToggleEnemyAiFreeze`, `SetOpen`, pickup helpers `SetTestHealthToHalf`, `SpawnTestRarePickup`, `TryGetTestPickupPosition`, `PruneTestPickups`, `ReleaseTestPickups`, `BuildMvpPickupTools`.
- Навигация: `HubOpenDetails`, enum `PlayerBuild/WorldEvents/OrbitalProduction`, MVP indexed-tab labels, catalogue filters/preview/status state, legacy/utility routing flag и поле Legacy в истории. Старый visual-only back builder заменён общим `BuildBackButton`.
- Live Visual Studio: 53 отдельных ring UI properties, категории RingSurface/Tier I–IV и helpers `AddTier`/`ChangeTier`. Невидимых VisualProperty/builders для них нет. Поля конфигурации и разбор исторических profile IDs нужны сохранённым профилям, поэтому их применение сохранено без UI.

Сохранённые зависимости:

- `OrbitalStationRuntime`, `OrbitalRunState`, `UpgradeManager`, `EnemySpawner`, `WorldEventSpawner`, `OrbitalPresentationConfig`, ring views/model/materials и остальные игровые системы. Награды, баланс, сохранения и Segmented geometry не изменялись.
- Песочница: единый `GrantMvpBuildReward`, штатный constructor/input owner; общие spawn/freeze через `TrailerCaptureSession`; отдельные очистки обычных и вручную созданных врагов, бессмертие/HP и оба runtime-селектора.
- Live Visual Studio, `LiveVisualRuntime`, weapon/projectile/FlashLink subset `OrbitalVisualAdapter`, обычные scene adapters, сохранённые production profiles. Исторические ring values применяются приватным binder в существующем адаптере с прежним precedence/bounds/restoration; новых менеджеров нет.
- Боты, Golden Path, диагностические snapshots/logs, восемь экспериментов врагов и их ручная очистка, XP visual lab, бункер MainMenu и публичные input-контракты `Subject42DebugMenu`.
- Все шесть пользовательских visual assets совпадают с исходными SHA-256 ([проверка](user-visual-assets-after.txt)).

Проверка: [Editor compile](editor-compilation.txt) без ошибок C#, затем **один** `F1SandboxTests.SimplifiedF1ProductionSmoke`: PASS, 1/1, 14,128 с ([XML](results.xml), [пункты проверки](result.txt)). Реальные UI listeners выдали три оружия/четыре модуля в запас, открыли штатный Tab, включили/выключили AI Freeze, вызвали spawn и безопасную очистку. Обычный пистолет продолжил стрелять, реальные ring renderers содержат геометрию, Live Visual Studio открылась без ring controls; QA/search/back прошли. Это сценарий Editor с вызовом UI listeners, без синтетического физического ввода.

Кадры: [Песочница](sandbox.png), [Tab](constructor.png), [Segmented/стрельба](segmented-fire.png), [Live Visual Studio](live-visual-studio.png). MVP оставлена открытой в Editor. Player builds, массовые тесты, git add/commit и смена ветки не выполнялись. Новые точечные проверки совместимости профилей скомпилированы, отдельно не запускались.

Подробные проверки consumers: [UI/runtime/test references](consumer-audit.txt), [visual/profile boundary](visual-removal.txt).

Независимая проверка diff: конкретных регрессий не найдено. Проверены navigation/enum/history, cursor/timeScale, выдача, retained lab cleanup и lifecycle/precedence/restoration сохранённых ring values. Проверяющий не менял файлы и не запускал дополнительные тесты.
