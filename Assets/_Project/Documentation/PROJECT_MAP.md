# Current Architecture

Актуальный срез проверен по Unity `6000.3.13f1`, C#-коду, сериализованным ссылкам сцен/prefab и ScriptableObject. В `Assets/_Project/scripts` находится 238 runtime-скриптов. В Build Settings включены только `MainMenu.unity` и `MVP.unity`. Метки ниже: **CURRENT** — есть доказанный runtime-вход; **LEGACY** — заменённая или отсоединённая ветка; **PROTOTYPE** — тестовая/dev-функция; **UNKNOWN** — файл или компонент существует, но полезный runtime-вызов не доказан.

| Система | Entry / ключевые классы | Зависимости, выходы и состояние |
|---|---|---|
| Bunker / запуск | `BunkerContext`, `BunkerCursorInteractor`, `BunkerPanelManager`; запуск — `BunkerRunStarter` | Читает выбор из DDOL `RunSelectionManager`, потребляет `AnomalyStabilizerData`, создаёт run через `RunStateManager.BeginNewRun`, загружает `MVP`. |
| Run flow | `RunStateManager`, `RunFlowController`, `RunTimer`, `LevelChoiceManager`, `RunEndService` | Зависит от `EnemySpawner`, boss death, UI выбора и SceneManager. Состояние сектора, HP/XP, upgrades, статистика и итог run хранится в DDOL `RunStateManager`. |
| Level setup | `LevelModifiersApplier`, `RunSector`, `StageProfileData` | После загрузки `MVP` применяет spawn profile/скейлинг, один `WorldRuleData` и один корневой `LocalAnomalyData`; dev fallback — Sector 1 + None + Berserk. |
| Player | `CharacterSpawner`, `CharacterMovement2D`, `PlayerHealth`, `ExperienceManager` | Создаёт выбранный `CharacterData` prefab и одно выбранное оружие, восстанавливает run upgrades/HP/XP; смерть открывает `GameOverManager`. |
| Weapons / damage | `BaseWeapon`, `ProjectileWeapon`, `LaserWeapon`, `Bullet`, `RocketProjectile`, `BeamFireBehaviour`, `EnemyHealth` | Данные — `WeaponData`; runtime-статы — `WeaponRuntimeStats`; hit завершается `EnemyHealth.TakeDamage`, смерть публикует `OnDied` и для boss вызывает run flow. |
| Enemies / spawn | `EnemySpawner`, `EnemySpawnProfile`, `EnemyHealth`, реализации `EnemyMovement` | Профиль задаёт временные фазы, веса, type caps, общий cap, HP/speed; внешние множители приходят от sector, World Rule, Event и acceleration prototype. |
| Run progression | `UpgradeManager`, `UpgradePanelView`, `RunItemSlots`, `UpgradeApplier`, `PlayerCombatModifiers` | Level-up/chest ставят игру на паузу, дают `UpgradeData`, занимают 6 runtime-слотов (до Lv.3), применяются к player/оружию и записываются в `RunStateManager`. |
| Anomalies / Rules / Events | `LevelAnomalyController`, `WorldRuleController`, `WorldEventSpawner` | Все три запускаются из `MVP`, но это независимые системы. Anomaly даёт зоны среды; Rule — глобальный модификатор; Event — случайная интерактивная задача с chest reward. |
| Bunker progression / unlocks | `BunkerStationProgressionService`, `MetaProgressionManager`, `UnlockProgressService`, `BunkerShopService`, `CurrencyManager` | Состояние распределено по `PlayerPrefs`; конфиги — `Resources/BunkerProgression`, `AnomalyStabilizers` и content/unlock ScriptableObjects. Единого save-объекта нет. |
| UI / HUD / audio | `HUDManager`, `RunMessageService`, panels/views, `AudioService`, `AudioSceneDirector` | Подписываются на runtime-сервисы и показывают timer, XP, items, mechanics, markers, result. Не определяют core flow. |
| Minigame | `BunkerMinigame` → `FootballMinigame`, `FootballStartZone`, `FootballScoreZone`, `BallRollVisual` | Встроен в `MainMenu`; 60-секундный score-mode, best score хранится в `PlayerPrefs`. На run progression не влияет. |

Главная граница архитектуры: `MainMenu` одновременно является main menu и Bunker, а `MVP` — переиспользуемой gameplay-сценой для всех секторов. Переход между секторами сейчас выполняется полной перезагрузкой `MVP`; долгоживущее состояние переносит `RunStateManager`.

# Current Core Loop

**CURRENT:**

`MainMenu/Bunker` → выбор Character + Weapon (+ необязательный Stabilizer) → `BunkerRunStarter` создаёт Sector 1 → загрузка `MVP` → `CharacterSpawner` → `LevelModifiersApplier` → враги/XP/upgrades/случайные Events → `RunTimer` истекает → появляется boss → смерть boss → три варианта следующего World Rule → сохранение HP/XP/stats → перезагрузка `MVP` для следующего сектора.

`LevelChoiceManager` меняет только World Rule; stage profile берётся по номеру, а локальная anomaly остаётся фиксированной `LocalAnomaly_Berserk`. Все Stage Profiles 1–10 указывают на один `p_Boss1`. В Sector 10 смерть boss вызывает `RunEndService.CompleteRunVictory`, затем возврат в `MainMenu`; смерть игрока ведёт в result UI и далее в bunker/restart.

Параллельно во время сектора `WorldEventSpawner` создаёт Hold Zone, False Signal, Corridor или Carrier Hunt, предлагает standard/risk через `DoubleOrLeave` и за успех выдаёт chest с обычным upgrade. Однако `RunFlowController.ApplyLevelMechanics()` явно отключает Hold Zone. Нынешний loop не имеет исследования, найденных/завершённых anomaly sites, Core-наград, физического Exit или выбора «уйти/остаться».

# Target Core Loop

**PLANNED — этого flow в проекте сейчас нет.**

`Bunker` → loadout/scanner info → большой исследуемый Sector → ручной поиск скрытых `AnomalySite` → внутри site: anomaly environment + один или несколько существующих Events → завершение site → Core для текущего оружия → давление растёт от Threat I к IV → физический Exit остаётся целью на карте → игрок решает выйти либо рисковать ради следующих sites → следующий Sector.

- Sectors 1–4 используют exploration/anomaly/Threat/Exit loop без обязательного boss по таймеру.
- Sector 5 — boss, использующий anomaly zones и одну простую вторую атаку; текущий boss этого не умеет.
- Победа возвращает в Bunker и открывает новый контент. Sectors 6–10 — последующее расширение.
- Первые Cores: Gravity (hit слегка притягивает enemies), Stasis (hits накапливают slow), Glitch (периодические displacement/отталкивание на hit).
- Hold Zone, Corridor и False Signal становятся этапами site; сложный site может последовательно потребовать несколько Events.
- World Rules позже могут комбинироваться или меняться внутри сложного sector. Текущий controller поддерживает только одно правило, поэтому это отдельный будущий этап.

# Progression

**CURRENT:** `UpgradeData` смешивает `Numeric` и `Behavior` в одном выборе. Реально применяются Damage, Fire Rate, Max HP, Move Speed, Crit Chance/Damage, XP Pickup Radius, Extra Shot, Knockback и behavior-эффекты (explosions, stationary ramp, low HP, random shots, circular burst, nuke). `EveryFifthAttackExtraShot` есть в enum, но case в `UpgradeApplier` и asset не найдены. При заполненных шести слотах `RunItemSlots` возвращает `RequiresReplacement`, но UI замены не реализован.

**PLANNED model:**

- **Stat Cards** остаются числовым run-layer и идут по существующему пути `UpgradeManager → RunItemSlots → UpgradeApplier`: Damage, Fire Rate, HP, Move Speed, Crit, Range, Projectile Count и аналоги. Projectile Count уже близок к `ExtraShot`; Range отсутствует в `UpgradeType`/assets, хотя `BaseWeapon` имеет runtime range API.
- **Anomaly Cores** — отдельный behavioral layer, получаемый только из `AnomalySite`, хранящийся на weapon/run, а не в общем level-up pool и не обязательно в шести item slots. Существующие behavior upgrades не следует автоматически переименовывать в Cores: их судьбу надо решить отдельно после прототипа.
- **Characters CURRENT** различаются prefab, HP и Move Speed; у всех `specialDescription: No`. **PLANNED later:** character задаёт стиль владения оружием — одно оружие, два стартовых, inventory нескольких оружий, swap. Это не блокирует первый Core prototype.

# Weapons

В production-выборе три `WeaponData`: Pistol (`ProjectileWeapon` + `Bullet`), Rocket (`ProjectileWeapon` + `RocketProjectile`), Laser Cannon (`LaserWeapon` + raycast `BeamFireBehaviour`). `CharacterSpawner` создаёт ровно один выбранный prefab под `WeaponPoint`.

**CURRENT hit pipeline:**

`BaseWeapon.BuildFireContext` → `WeaponFireContext` → либо `ProjectileShotPattern/ProjectileFireBehaviour` → projectile → trigger/overlap, либо `BeamFireBehaviour` → raycast → `EnemyHealth.TakeDamage(damage, hitPoint, crit)` → `EnemyHealth.OnDied` → kill/loot/unlock/boss flow.

Knockback реализован через `EnemyMovement.ApplyKnockback`; Bullet и Rocket вызывают его, Beam — нет. Экологические Gravity/Stasis уже имеют `AnomalyExternalVelocityStack`, `EnemyAnomalyEffects` и speed multiplier primitives, но weapon status-duration/stack system отсутствует. `EnemyHealth.TakeDamage` не знает source weapon и также вызывается explosions/nuke, поэтому встраивать Cores прямо в этот старый метод без контекста опасно.

**Рекомендуемая точка интеграции:** один общий `WeaponHitContext`/dispatcher на границе weapon hit → enemy damage. Его данные должны формироваться в `BaseWeapon.BuildFireContext`, переноситься projectile-путём через расширенный `ProjectileCombatContext`, а beam-путём — непосредственно из `WeaponFireContext`. Bullet, Rocket и Beam передают hit в один resolver, который вызывает damage и затем Core effects. Так Core-код един, сохраняет source/weapon/core/impact данные и не срабатывает на неоружейные explosions. Это предложение, не реализованная система.

# Anomalies

**CURRENT:** `LevelModifiersApplier` передаёт один `LocalAnomalyData` в `LevelAnomalyController.Apply`. Controller очищает старое состояние и размещает 5 прямоугольных зон с целевым покрытием 70% gameplay area. Стартовая/межсекторная конфигурация — Berserk с additional Stasis + Explosive Zone; поэтому production route циклически создаёт эти типы. Gravity и Glitch assets/prefabs существуют и доступны debug-контролю, но не выбираются обычным текущим flow.

Реализованы:

- Berserk — ускорение enemies внутри зоны;
- Stasis — slow player/enemies/projectiles/pickups;
- Explosive Zone — delayed AoE при смерти enemy внутри;
- Gravity — external velocity к центру для поддерживаемых объектов;
- Glitch — периодические безопасные teleport-смещения.

Lifecycle: `Apply` → spawn zones → zone enter/exit вызывает `NotifyLocalZoneEntered/Exited` для presentation → `Clear` уничтожает зоны и эффекты. Controller отдаёт геометрию через `CollectActiveLocalZones`, чем уже пользуется Event spawning. Публичных callbacks «site found/completed» нет; состояние открытия, последовательности Events и reward не хранится. Значит zone prefabs, geometry и movement primitives переиспользуемы, но нынешняя «anomaly» — environmental region, не site и не weapon Core.

# Events

**CURRENT:** `WorldEventSpawner` по таймеру создаёт максимум один `WorldEvent`; в `MVP` first delay = 2 s, interval = 5 s, шанс позиции внутри anomaly = 35%. `WorldEvent` наследует gameplay `Interactable`: `PlayerInteractor` начинает выбор standard/risk, затем `StartSelectedEvent`; завершение вызывает `NotifyEventCompleted`, failure — `NotifyEventFailed`. Spawner публикует `EventCompleted`/`EventFailed`, управляет enemy pressure и автоматически создаёт upgrade chest на completion.

| Event | Фактическая логика | Статус для AnomalySite |
|---|---|---|
| Hold Zone / `CaptureZoneEvent` | После interact прогресс идёт, пока player внутри радиуса; difficulty увеличивает требуемое время; затем `CompleteEvent`. | Логика готова, но production flow сейчас отключает prefab через `SetHoldPointEnabled(false)`. |
| Corridor / `EvacuationCorridorEvent` | После старта строит путь и движущуюся безопасную область; вне corridor player периодически получает damage; конец пути завершает Event. | Можно переиспользовать без смены механики, site должен задавать placement/owner. |
| False Signal / `FalseSignalEvent` | Создаёт signal points; реальная точка завершает, ложные запускают ambush/turrets или blackout; timeout вызывает failure. | Готов как более сложный этап site; уже зависит от `EnemySpawner`, `GameplayArea` и rule visuals. |
| Carrier Hunt | Выбирает живого enemy-carrier, меняет movement, создаёт escort wave; kill = completion, escape/despawn = failure. | CURRENT в случайном pool, но не обязателен для первого site prototype. |

Минимальная адаптация: оставить конкретные Event-классы; дать `WorldEventSpawner` production API для targeted spawn/register (сейчас явный targeted путь debug-only), позволить владельцу отключить стандартный chest reward, а `AnomalySite` подписать на `EventCompleted/EventFailed` и запускать следующий этап либо Core reward. Нельзя просто Instantiate Event из site: `WorldEvent.CanInteract` требует owner/spawner и разрешение `CanStartEvent`.

# World Rules

**CURRENT:** `WorldRuleData` реализует None, Snow, Rain, Darkness, Wind, Golden, Condensation. `LevelChoiceManager` после boss предлагает 3 из 6 ненулевых правил. `WorldRuleController.Apply` сначала вызывает `Clear`, затем применяет movement, spawn pressure, visuals и type-specific runtime effects; `Clear` восстанавливает значения и отписывается. Для Darkness используются `BaseWeapon.ShotFired` и enemy lifecycle; Golden подписывается на spawn/death; Wind/Snow/visual systems имеют собственный cleanup.

Pipeline хорошо переиспользуем для одиночного правила. **PLANNED combinations** напрямую не поддерживаются: controller хранит один `activeRule`, а каждый `Apply` удаляет предыдущий. Отдельный `WorldAccelerationRule` присутствует в `MVP`, но production-вызов `StartRule` не найден; это не доказательство комбинируемых rules.

# Threat

**CURRENT:** `RunTimer` берёт `Duration` и `BossPrefab` из `RunSector`; при нуле предупреждает и создаёт boss. `EnemySpawner` уже умеет `EnemySpawnProfile` с временными phases, weighted prefabs, per-type caps, общий max alive, interval, batch growth, HP/speed multipliers. Сверху умножаются sector pressure, World Rule pressure, Event pressure и acceleration. Есть `StopSpawning`, `ResumeSpawning`, `SpawnAdditionalWave`, `SetLevelScaling` и pressure setters.

Stage 1 имеет duration 5 s, хотя его profile phases начинаются в 0/12/24/36/48/60 s; обычный запуск не достигает пяти поздних фаз. Sectors 2–10 длятся 72–90 s, pressure растёт 1.12→2.08. Это текущая конфигурация, не Threat design.

**Минимальный переход:** не писать новый spawner. Для exploration sector задать profile с четырьмя согласованными временными bands — Threat I, Threat II, Threat III, Threat IV; тонкий threat-state/UI слой должен читать тот же источник thresholds (лучше read-only active phase/event из `EnemySpawner`), а не вести второй несинхронный таймер. В Sectors 1–4 `RunTimer` перестаёт завершать sector/создавать boss: время только переводит spawner к следующей фазе. Завершение делает физический Exit. Sector 5 использует отдельный boss trigger/profile. Сначала pressure повышать interval/caps/weights существующими средствами; новые enemy AI не нужны для проверки loop.

# Bunker

**CURRENT proven stations:** Character Selection, Weapon Selection, Shop, Upgrade, Anomaly Stabilizer и Start Run; также Football terminal. `BunkerStationType.Map` и `OpenMap` существуют, но отдельная активная Map station сериализованными ссылками не доказана. Cursor interaction идёт через `IBunkerInteractable`, а panels централизованы в `BunkerPanelManager`.

Anomaly Table сейчас является не Scanner, а станцией одноразовых stabilizers. `BunkerAnomalyStabilizerPanel` загружает 5 assets из `Resources/AnomalyStabilizers`; уровень станции открывает 2/3 случайных варианта, выбранный effect потребляется при старте run и меняет размер зон, gold, Stasis effect на player или Gravity force. Station progression хранит Character/Weapon/Upgrades/Anomaly уровни 1–3 в `PlayerPrefs`.

**PLANNED Scanner placement:** использовать тот же station/panel/progression pattern рядом с Anomaly Table/Map, но завести отдельную логическую Scanner station, чтобы не смешать разведданные с одноразовым stabilizer. Scanner I показывает количество sites; Scanner II — часть типов; Scanner III — все типы; Scanner IV — приблизительные/точные позиции. Для этого текущую модель надо сознательно расширить: `BunkerStationId` содержит только 4 IDs, service ожидает ровно 4 configs, а `BunkerStationProgressionData` и stored-level clamp ограничены Lv.3. Scanner data должен читать sector layout/state, но не владеть генерацией sites.

# Scenes

| Scene | Статус | Крупные системы |
|---|---|---|
| `Scenes/MainMenu.unity` | CURRENT, build; MainMenu + Bunker | Bunker context/stations/panels, selection, shop/meta/station progression, unlocks, run start, football, audio/localization. |
| `Scenes/MVP.unity` | CURRENT, build; Gameplay | Run/level setup, player spawn, spawner, timer/boss, upgrades, rules, anomaly zones, events, HUD/results, debug menu. |
| `Scenes/GameplaySandbox.unity` | PROTOTYPE, not build | `GameplaySandboxBootstrap` собирает тестовую среду и debug menu. |
| `Scenes/MainMenu_old.unity` | LEGACY, not build | Старый `MainMenuController`/UI route. |
| `Scenes/Test.unity` | UNKNOWN/test, not build | Доказан только минимальный camera-follow setup; production entry нет. |
| `Scenes/TestScene/MainMenu_shader_test.unity` | PROTOTYPE/LEGACY, not build | Старые menu, bunker goal и shader/UI experiments. |
| `Scenes/TestScene/LoadingScene.unity`, `Cutscene1.unity` | UNKNOWN, not build | Production-переходы на них не найдены. |

Сцены из imported art demo не относятся к Subject#42 runtime и в build не включены.

# Legacy

| Class / system | Status | Почему |
|---|---|---|
| `BunkerRunStarter` + `RunStateManager` + `RunFlowController` | CURRENT | Доказанный путь `MainMenu → MVP → sectors/results`. |
| `RunLevelManager` | UNKNOWN | Компонент есть в `MVP`, но это тонкий facade к `RunStateManager`; потребителей его API не найдено. |
| `MainMenuController` | LEGACY | Сам компонент сериализован в `MainMenu_old`, не в текущем `MainMenu`; заменён bunker flow. |
| `PanelManager` (`PanelManger.cs`) | LEGACY | Сериализованных компонентов и code consumers не найдено; текущий владелец UI — `BunkerPanelManager`. |
| `BunkerCursorInteractor/IBunkerInteractable` | CURRENT | Реальный mouse interaction Bunker. |
| `PlayerInteractor/Interactable` | CURRENT | Отдельный keyboard interaction в `MVP`, необходим `WorldEvent`; не удалять как «старый bunker input». |
| `DoorInteractable` / `BunkerRoom1.prefab` | UNKNOWN | Class есть только в prefab, а сам prefab не найден среди scene/prefab consumers. |
| `FootballMinigame` + `FootballScoreZone` | CURRENT | Активный bunker score-mode. |
| `BallRollVisual` | CURRENT | Несмотря на старую папку `FootBall`, четыре активных мяча используют его; это physics/input layer новой мини-игры. |
| `FootballGoal` | LEGACY | Новая игра держит только пустой compatibility callback; сериализованных component refs не найдено. |
| `BunkerGoalTrigger` | PROTOTYPE | Сериализован только в `MainMenu_shader_test`. |
| `BunkerCallTimer` | UNKNOWN | Ни сериализованных ссылок, ни code consumers не найдено. |
| `GameplaySandboxBootstrap` | PROTOTYPE | Только non-build sandbox scene. |
| `Subject42DebugMenu` / `TelekinesisDebugPrototype` | PROTOTYPE | Menu подключён в `MVP` и добавляется Bunker context, но функциональность ограничена Editor/Development Build; telekinesis создаётся debug-путём. |
| `WorldAccelerationRule` / `NoDamageChallenge` | PROTOTYPE | Компоненты и HUD есть в `MVP`, но production callers `StartRule`/`StartChallenge` не найдены. |
| `RescueCapsuleEvent` | PROTOTYPE | Prefab доступен debug menu/sandbox, но отсутствует в production `eventPrefabs`. |
| `CarrierHuntEvent` | CURRENT | Prefab входит в production `WorldEventSpawner.eventPrefabs`. |
| `EnemyData` | UNKNOWN | ScriptableObject class есть, assets/consumers не найдены; текущий spawn настраивается prefab + `EnemySpawnProfile`. |
| `MatrixRainBackground` | CURRENT | Папка называется `Legacy`, но компонент идёт через SelectPanel variants в текущий `MainMenu`. Остальные `PortalLabel`, `MenuZombieWalker`, `MenuAnimatedTransformDetected` не имеют serialized refs и являются LEGACY. |

# Risks

1. Current completion жёстко связан с `RunTimer → boss → LevelChoice`; target Exit/Threat требует разъединить завершение сектора и рост сложности.
2. Stage 1 длится 5 s при phase thresholds до 60 s: текущая стартовая конфигурация фактически не проверяет profile progression.
3. Все 10 stages используют один простой chase boss, и boss появляется в каждом sector; planned Sector 5 topology/second attack отсутствуют.
4. Weapon hits расходятся по Bullet, Rocket AoE и Beam; общего source-aware hit endpoint нет. Прямая вставка Cores в каждый класс создаст три реализации и разные результаты.
5. `WorldEvent` требует `WorldEventSpawner` owner, а spawner автоматически выдаёт chest: без маленького ownership/reward seam `AnomalySite` будет либо дублировать spawner, либо выдавать две награды.
6. Hold Zone настроен в active event array, но каждый level отключается run flow; event prototype нельзя считать проверенным production loop.
7. `WorldRuleController` допускает только одно active rule и очищает предыдущее; planned combinations нельзя выразить текущими данными.
8. Термины Gravity/Stasis/Glitch уже означают environmental `LocalAnomalyType`, а stabilizers тоже называются anomaly effects; Core data нельзя смешивать с этими двумя слоями.
9. Upgrade layer смешивает numeric/behavior, не имеет Range case, содержит неиспользованный Every Fifth type и не завершает replacement при шести слотах.
10. Scanner I–IV не помещается в текущий bunker progression (фиксированные 4 station configs, station max Lv.3); молчаливое переиспользование Anomaly station сломает смысл stabilizers.

# Prototype Plan

1. **Core hit slice:** создать единый source-aware hit context/dispatcher и вручную проверить Gravity, Stasis, Glitch на Pistol, Rocket и Laser в текущем `MVP`; пока без reward/save/UI.
2. **Core ownership:** добавить отдельное runtime-хранилище Cores на выбранном weapon/run и debug-выбор для повторяемого теста; не переносить туда обычные `UpgradeData`.
3. **One AnomalySite:** обернуть одну существующую zone + Hold Zone; targeted spawn через `WorldEventSpawner`, отключённый chest, completion → один Core. Затем тем же контрактом подключить Corridor и False Signal/sequence.
4. **Exploration arena:** увеличить тестовый layout, разместить 3 скрытых sites и физический Exit; хранить discovered/completed state в текущем run state. Scanner пока заменить минимальным debug/status display.
5. **Threat I–IV:** собрать четырёхфазный `EnemySpawnProfile`, вывести active threat из spawner thresholds и отключить timer boss/completion для exploration sector.
6. **Sector 1 vertical slice:** Bunker → исследование → 0–3 Cores → решение Exit/continue → сохранённый переход в Sector 2. Проверить death/restart/return paths.
7. **Sectors 1–4:** только после подтверждения fun/readability варьировать layout, sites, event sequences и одиночные World Rules; затем добавить настоящую Scanner progression.
8. **Sector 5 boss:** отдельный boss sector, anomaly zones плюс одна читаемая вторая атака; victory → Bunker/unlocks. Sectors 6–10 и комбинируемые World Rules оставить за пределами первого prototype.
