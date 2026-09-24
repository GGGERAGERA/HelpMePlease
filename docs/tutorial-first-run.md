# Subject#42: обучение первого сектора

Обучение запускается в обычном MVP через `RunFlowController.InitializeSector`, только в первом секторе активного забега, пока `Subject42.Tutorial.Completed` в PlayerPrefs не равен 1. BotRunSession (enabled/starting/running) отключает его. Завершение сохраняется после успешного `HandleExitReached`, не при открытии выхода или награды. При смерти до выхода следующий новый забег начинает обучение с движения.

## Реализация

Новые runtime-файлы: `Assets/_Project/scripts/Run/Tutorial/TutorialController.cs` (последовательные TutorialStep) и `TutorialOverlay.cs` (один Canvas с затемнением, вырезами для настоящих объектов/UI, рамками, стрелкой и двумя строками). Overlay не перехватывает ввод. Покадрово обновляется только представление движущихся целей; переходы шагов используют callbacks.

| Шаг | Событие/точка интеграции |
|---|---|
| MOVEMENT | Новый `CharacterMovement2D.Travelled`: фактическое смещение Rigidbody после physics, только с movement intent; порог 2.5 единицы |
| FIRST ENEMIES | Существующий `EnemyHealth.OnDied` у трёх учебных противников |
| XP | Новые `ExperiencePickup.Spawned` и `Collected`; callback pickup до начисления XP |
| FIRST REWARD | Новые `UpgradeManager.RewardOpened` / `RewardChosen` после показа карточек / успешного начала существующего placement |
| ORBITAL PLACEMENT | `UpgradeManager.RewardCommitted`, вызываемый существующим callback завершения `OrbitalRewardFlowController`; отмена возвращает тот же шаг карточек |
| SECTOR GOAL | Указатель к назначенному Capture Zone; новый `CaptureZoneEvent.PlayerEntered` запускает обычное событие Standard |
| FIRST EVENT | Новый `WorldEventSpawner.EventStarted` и существующий `EventCompleted` только назначенной зоны |
| EXIT | Новые `RunFlowController.ExitUnlocked` / `ExitReached` после реальной разблокировки / успешного принятия выхода |

В tutorial первый обычный site, ближайший к старту, получает существующий CaptureZoneEvent. Другие события не начинают работу до окончания обучения. Выход этого сектора открывается после завершения назначенной зоны; вне tutorial сохраняется обычное условие таймер/штурм. Обычная награда за site сохраняется и проходит штатную очередь.

## Мягкие поблажки

- До движения нет обычного спавна; затем три обычных chase-врага с XP, 2 HP, скоростью ×0.35, на расстоянии 5–7 единиц.
- До установки оружия обычный спавн закрыт; затем интервал 6 секунд, максимум 4 обычных врага, один за цикл, без автоматического штурма.
- Входящий урон ×0.2 только пока активен tutorial.
- XP до учебного pickup накапливается без раннего level-up; избыток временно удерживается, затем выдаётся. Первый учебный pickup гарантирует порог level-up.
- Первая карточная награда состоит из доступных NEW WEAPON для одиночного placement, без Link Pair.
- Стартовая орбита production имеет единственный занятый mount. Только tutorial добавляет один свободный через существующий `OrbitalStationRuntime.AddMount`; стартовые конфиги и Direct Mount Placement не изменены.
- Обычный сундук временно недоступен до учебного размещения, чтобы его награда не занимала подготовленный mount раньше времени.

Авторский баланс последующих секторов не изменён. Полученные в первом секторе оружие и mount переносятся обычным состоянием забега.

## DEV и проверка

`RESET TUTORIAL`: F1 → раздел Run → **RESET TUTORIAL**, либо Unity **Tools → Subject42 → Dev → RESET TUTORIAL**. Кнопка сбрасывает только completion; затем нужно начать новый забег. Текущий забег не перезапускается скрыто.

Тест `Subject42TutorialTests.AllEightStepsPersistAcrossRestartAndReset` использует production-сцены, движение через physics, автоматическое убийство, настоящий pickup, callback клика существующей карточки, штатный выбор допустимого mount и commit, естественное заполнение Capture Zone и trigger выхода. Для проверки Hold Zone и выхода тест позиционирует Rigidbody в соответствующих областях; полный маршрут пешком этим тестом не проверяется. Проверяются ранний XP, отмена placement, второй сектор, новый запуск без обучения и reset с новым запуском. В каждом кадре захвата проверяется наличие геометрии overlay.

Существующий Golden Path запускается с незавершённым tutorial и дополнительно проверяет, что Bot context его отключает. CoreTestSupport сохраняет/восстанавливает completion вместе с остальными preferences, а прочие Core-сценарии получают уже завершённый tutorial.

Результаты и кадры: `Artifacts/GeneratedQA/Tutorial/`. Локальный DEV test runner принимает имя fixture либо `Core` в `run-tests.request`; результат записывается в `results.xml`.

Фактический прогон 24.09.2026, Unity 6000.3.13f1: **Core 19/19 PASS**, включая все 8 шагов, ранний XP и сундук, отмену placement, сектор 2, повторный забег, reset и возврат после смерти. Итоговый XML: `Artifacts/GeneratedQA/Tutorial/core-results.xml`. Golden Path: seed 48151623, скорость 5×, 1/1 PASS; три сектора, босс, чистый Бункер и второй забег. Отчёт: `Artifacts/GeneratedQA/BotBatches/golden_path_summary.md`. Девять кадров tutorial сохранены и проверены при 1920×1080. Standalone build и другие разрешения не проверялись.

## Изменённые существующие файлы

- `scripts/Combat/Player/CharacterMovement2D.cs`, `PlayerHealth.cs`
- `scripts/Combat/Enemies/EnemyHealth.cs` — существующий признак XP loot доступен в release
- `scripts/Progression/Experience/ExperiencePickup.cs`, `ExperienceManager.cs`
- `scripts/Progression/RunUpgrades/UpgradeManager.cs`
- `scripts/Run/Flow/RunFlowController.cs`
- `scripts/Run/Flow/SceneTransitionOverlay.cs` — явная Unity-null проверка уничтоженного игрока перед переходом; обнаружено существующим тестом смерти и повторного запуска
- `scripts/Run/Exploration/ProductionExplorationSectorController.cs`
- `scripts/World/Events/CaptureZoneEvent.cs`, `WorldEventSpawner.cs`
- `scripts/World/Spawning/EnemySpawner.cs`
- `scripts/World/Loot/WorldLootChest.cs` — запрет раннего открытия до первого placement
- `scripts/UI/Notifications/RunMessageService.cs` — старые параллельные hints скрываются во время tutorial
- `Dev/Debug/F1/Subject42DebugMenu.cs`
- `Dev/Tests/Core/Editor/CoreTestSupport.cs`, `Subject42GoldenPathTests.cs`

Все пути в этом списке относительно `Assets/_Project/`. Дополнительно добавлены `Dev/Editor/TutorialMenu.cs`, `Dev/Tests/Core/Editor/Subject42TutorialTests.cs` и `TutorialVerificationRunner.cs`, а также Unity meta для новых assets.

## Ограничения

Зависимости — существующий CaptureZoneEvent в production-пуле, обычный chase-враг с XP loot и стандартный стартовый ORBITAL state. Удаление этих authored ресурсов требует обновить tutorial; при отсутствующих зависимостях он сообщает ошибку вместо создания заменяющих gameplay-систем. Указатель показывает направление, но не строит путь вокруг препятствий. Визуальная проверка выполняется на кадрах тестового разрешения; остальные соотношения сторон требуют отдельного ручного smoke-test.
