# Subject#42: Tutorial Level 0

При создании нового забега `RunStateManager` выбирает Tutorial Sector 0, пока `Subject42.Tutorial.Completed` в PlayerPrefs не равен 1. Он использует сцену MVP и существующий `TutorialController` с восемью шагами. BotRunSession (enabled/starting/running) по-прежнему пропускает обучение. При сохранённом completion новый забег начинается сразу с Sector 1.

Маршрут: `Tutorial 0 → Sector 1 → Sector 2 → Sector 3 → Boss`. Level 0 не увеличивает `CompletedLevels`, не начисляет награду за завершение основного сектора и не продвигает World Rule unlock. После успешного выхода сохраняется completion, на 1,5 секунды показывается `ОБУЧЕНИЕ ЗАВЕРШЕНО`, затем обычный `SceneTransitionOverlay` переносит игрока в исходную конфигурацию Sector 1 без выбора случайного правила. XP, установленное оружие и mount сохраняются штатным переходом. При смерти до выхода следующий новый забег снова начинается с обучения.

## Реализация

Новые runtime-файлы: `Assets/_Project/scripts/Run/Tutorial/TutorialController.cs` (последовательные TutorialStep) и `TutorialOverlay.cs` (один Canvas с затемнением, вырезами для настоящих объектов/UI, рамками, стрелкой и двумя строками). Overlay не перехватывает ввод. Покадрово обновляется только представление движущихся целей; переходы шагов используют callbacks.

| Шаг | Событие/точка интеграции |
|---|---|
| MOVEMENT | Новый `CharacterMovement2D.Travelled`: фактическое смещение Rigidbody после physics, только с movement intent; порог 2.5 единицы |
| FIRST ENEMIES | Существующий `EnemyHealth.OnDied` у трёх учебных противников |
| XP | Новые `ExperiencePickup.Spawned` и `Collected`; callback pickup до начисления XP |
| FIRST REWARD | Новые `UpgradeManager.RewardOpened` / `RewardChosen` после показа карточек / успешного начала существующего placement |
| ORBITAL PLACEMENT | `UpgradeManager.RewardCommitted`, вызываемый существующим callback завершения `OrbitalRewardFlowController`; отмена возвращает тот же шаг карточек |
| SECTOR GOAL | Указатель к назначенному Orbital Relay; новый `OrbitalRelayEvent.PlayerEntered` запускает обычное событие Standard |
| FIRST EVENT | Новый `WorldEventSpawner.EventStarted` и существующий `EventCompleted` только назначенной зоны |
| EXIT | Новые `RunFlowController.ExitUnlocked` / `ExitReached` после реальной разблокировки / успешного принятия выхода |

Level 0 создаёт одну существующую OrbitalRelayEvent и штатный выход в свободных точках по фиксированному порядку направлений от старта. Обычные sites, локальные аномалии, special site, ресурсы, разрушаемые объекты, случайный сундук и порталы не создаются. World Rule и hazards не применяются; обычный спавн, случайные события и рост threat заблокированы по номеру сектора, включая паузу после completion. Relay выдаёт одну Upgrade selection через общую queue; Gold начисляется только за Bonus activations. При fail через 2 секунды запускается тот же prefab без повторения ранних шагов. Выход открывается после Bonus и разрешения награды; основные сектора сохраняют условие таймер/штурм.

## Мягкие поблажки

- До движения нет обычного спавна; затем три обычных chase-врага с XP, 2 HP, скоростью ×0.35, на расстоянии 5–7 единиц.
- Обычный спавн и автоматический штурм закрыты на всём Level 0. При временно занятой позиции учебный спавн повторяется вместо отключения контроллера.
- Входящий урон ×0.2 только пока активен tutorial.
- XP до учебного pickup ограничен порогом level-up минус 1. Первый pickup гарантирует ровно один level-up; избыток и последующий XP в Level 0 отбрасываются, чтобы не создавать очередь лишних наград.
- Первая карточная награда состоит из доступных NEW WEAPON для одиночного placement, без Link Pair.
- Стартовая орбита production имеет единственный занятый mount. Только tutorial добавляет один свободный через существующий `OrbitalStationRuntime.AddMount`; стартовые конфиги и Direct Mount Placement не изменены.
- Обычные сундуки недоступны на всём Level 0.

Авторский баланс основных секторов не изменён. HUD и окно паузы показывают `TUTORIAL 0` отдельно от прогресса трёх секторов. Старые onboarding-подсказки после завершённого обучения не повторяются в Sector 1.

## DEV и проверка

`RESET TUTORIAL`: F1 → раздел Run → **RESET TUTORIAL**, либо Unity **Tools → Subject42 → Dev → RESET TUTORIAL**. Кнопка сбрасывает только completion; затем нужно начать новый забег. Текущий забег не перезапускается скрыто.

Тест `Subject42TutorialTests.AllEightStepsPersistAcrossRestartAndReset` использует production-сцены, движение через physics, автоматическое убийство, настоящий pickup, callback клика существующей карточки, штатный выбор допустимого mount и commit, контакты орбитального оружия с активными Node, Transition и Bonus и trigger выхода. Для входа в relay и выхода тест позиционирует Rigidbody в соответствующих областях; внутри события действует movement intent через OrbitalRelayBotSteering и разрешается общая Upgrade selection; полный маршрут пешком этим тестом не проверяется. Проверяются номер 0, отсутствие побочных sites и роста threat, одна XP-награда, отмена placement, автоматический переход в Sector 1 без зачёта завершённого сектора, новый запуск без обучения и reset с новым запуском. В каждом кадре захвата проверяется наличие геометрии overlay.

Проверка изменения Level 0 (26.09.2026): компиляция `Assembly-CSharp-Editor.csproj` вместе с runtime и изменённым тестом — 0 ошибок. Runtime/Play Mode и дополнительные batch-тесты не запускались по ограничению задачи. Приведённые ниже результаты от 24.09 относятся к прежнему обучению внутри Sector 1 и не подтверждают новый flow.

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
- `scripts/World/Events/OrbitalRelayEvent.cs`, `WorldEventSpawner.cs`
- `scripts/World/Spawning/EnemySpawner.cs`
- `scripts/World/Loot/WorldLootChest.cs` — запрет раннего открытия до первого placement
- `scripts/UI/Notifications/RunMessageService.cs` — старые параллельные hints скрываются во время tutorial
- `Dev/Debug/F1/Subject42DebugMenu.cs`
- `Dev/Tests/Core/Editor/CoreTestSupport.cs`, `Subject42GoldenPathTests.cs`

Все пути в этом списке относительно `Assets/_Project/`. Дополнительно добавлены `Dev/Editor/TutorialMenu.cs`, `Dev/Tests/Core/Editor/Subject42TutorialTests.cs` и `TutorialVerificationRunner.cs`, а также Unity meta для новых assets.

## Ограничения

Зависимости — существующий OrbitalRelayEvent в production-пуле, обычный chase-враг с XP loot и стандартный стартовый ORBITAL state. Удаление этих authored ресурсов требует обновить tutorial; при отсутствующих зависимостях он сообщает ошибку вместо создания заменяющих gameplay-систем. Указатель показывает направление, но не строит путь вокруг препятствий. Визуальная проверка выполняется на кадрах тестового разрешения; остальные соотношения сторон требуют отдельного ручного smoke-test.
