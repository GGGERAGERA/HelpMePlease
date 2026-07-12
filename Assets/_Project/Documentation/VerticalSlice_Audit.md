# Vertical Slice — технический аудит

Дата аудита: 2026-07-12. Область: `Assets/_Project`, 151 C#-файл, сцены `MainMenu` и `MVP`, связанные prefab и ScriptableObject YAML. Отчёт создан до реорганизации; пути ниже обновлены до итоговой структуры. Статусы: **проверено по коду**, **проверено по YAML**, **требуется ручной Play Mode тест**.

## Вывод

Текущий цикл реализован и архитектурно прослеживается: `MainMenu -> MVP -> boss death -> level choice -> MVP`, а выход через Pause/GameOver централизован в `RunEndService`. Награда начисляется один раз через идемпотентный `RunStateManager.EndRun`. До безопасных исправлений проект имеет два CRITICAL-дефекта сохранения runtime-состояния и несколько IMPORTANT-рисков конфигурации. Статус до исправлений: **Playable**, но не Vertical Slice Candidate.

## BLOCKER

### Сохранённое HP может быть потеряно при загрузке следующего уровня

- Файл: `scripts/Combat/Player/PlayerHealth.cs`; класс `PlayerHealth`; метод `Start`.
- Связанный код: `scripts/Combat/Player/CharacterSpawner.cs`, `Start`; `scripts/Run/State/RunStateManager.cs`, `ApplyToSpawnedPlayer`.
- Проблема: `CharacterSpawner.Start` создаёт игрока и сразу восстанавливает snapshot, но `PlayerHealth.Start` безусловно выполняет `currentHealth = maxHealth`. Для компонента созданного в ходе другого `Start` порядок вызова `Start` допускает последующий сброс восстановленного HP.
- Проявление: после выбора следующего уровня повреждённый игрок может загрузиться с полным HP.
- Безопасное решение: инициализировать HP до восстановления либо в `Start` присваивать максимум только при неинициализированном (`<= 0`) значении.
- Inspector/Scene/Prefab: не требуется; требуется ручной тест перехода с неполным HP.

## CRITICAL

### Бонус золота метапрогрессии накапливается между уровнями

- Файлы: `scripts/Progression/MetaUpgrades/MetaUpgradeApplier.cs`, `ApplyGoldGain`; `scripts/Progression/MetaUpgrades/CurrencyManager.cs`, `AddGoldGainPercent`.
- Проблема: `CurrencyManager` использует `DontDestroyOnLoad`, а `MetaUpgradeApplier.ApplyTo` вызывается новым `CharacterSpawner` на каждом MVP. Метод умножает уже изменённый `goldGainMultiplier`, поэтому одинаковый meta-бонус применяется повторно.
- Проявление: при трёх последовательных уровнях награда выше ожидаемой и зависит от числа reload сцены.
- Безопасное решение: сделать установку множителя идемпотентной для единственного существующего источника meta-бонуса.
- Inspector/Scene/Prefab: не требуется; требуется тест награды после 1 и 3 уровней.

### Enemy unlock зависит от строковых ID prefab-вариантов

- Файлы: `scripts/Combat/Enemies/EnemyHealth.cs`, `Die`; `scripts/Combat/Enemies/EnemyIdentity.cs`, `EnemyId`; `scripts/Progression/Unlocks/UnlockProgressService.cs`, `AddProgressByCondition`.
- Данные: `unlock_weapon_rocket_bomber` ожидает `Bomber`, `unlock_weapon_laser_tupik` — `Tupik`. Базовый `p_Enemy.prefab` содержит пустой `enemyId`; overrides найдены у Bomber/default/Shooter.
- Проблема: сравнение строгое и регистрозависимое; пустой/иной ID бесшумно не продвигает нужный unlock (с warning для пустого значения).
- Проявление: закрытые карточки не откроются после требуемых убийств.
- Безопасное решение: вручную проверить итоговые значения `EnemyIdentity.enemyId` на всех реально спавнящихся prefab и соответствие `targetId`; код не нормализовать без решения о канонических ID.
- Inspector/Prefab: **требуется проверка prefab**.

## IMPORTANT

### Старые порталы содержат ссылки на удалённые скрипты

- Prefab: `prefabs/PortalToNextLevel.prefab` (`NextLevelChoicePortal`, GUID `59bd...`); `prefabs/PortalToMenu.prefab` (`MenuPortal`, GUID `d909...`).
- Проблема: исходных C#-скриптов в текущем проекте нет. Активные `MainMenu`/`MVP` на эти prefab не ссылаются; новый flow использует `LevelChoiceManager` и `RunEndService`.
- Проявление: если prefab вернуть в сцену, появится Missing Script и старый flow будет обходить централизованные сервисы.
- Безопасное решение: не использовать; удалить только после ручного подтверждения отсутствия ссылок во всех нужных сценах/Addressables.
- Scene/Prefab: **проверено по YAML активных сцен**, требуется ручное подтверждение перед удалением.

### Дублирующий `CurrencyManager` находится и в MainMenu, и в MVP

- Файл: `scripts/Progression/MetaUpgrades/CurrencyManager.cs`, `Awake`; YAML обеих сцен.
- Проблема: обычный путь безопасен за счёт уничтожения дубля, но прямой запуск MVP создаёт постоянный экземпляр, который затем переживает возврат. Это допустимо, однако усложняет диагностику и зависит от singleton-guard.
- Проявление: временно существуют два объекта при загрузке второй сцены; неверные подписки на сценовый экземпляр могут потеряться.
- Решение: оставить для поддержки прямого запуска MVP; в Play Mode проверять ровно один живой экземпляр после каждой загрузки.
- Scene: **проверено по YAML**, изменение не требуется.

### `RunStateManager` создаётся программно и отсутствует в сценах

- Файл: `scripts/Run/State/RunStateManager.cs`, `EnsureExists`.
- Проблема: это штатная схема, но запуск MVP напрямую не вызывает `BeginNewRun`; используются default character/weapon, а состояние забега появляется позднее только при выборе уровня/выходе.
- Проявление: direct-MVP тест отличается от полного пути и не доказывает работу selection/persistence.
- Решение: тестировать основной путь только через MainMenu; direct MVP считать отладочным режимом.
- Scene: изменение не требуется.

### Старые entry points обходят текущий цикл, если будут подключены

- Файлы: `scripts/UI/Common/MainMenuController.cs` и `scripts/Bunker/MetaProgression/BunkerRunStarter.cs` оба могут начать забег; `scripts/UI/Common/RunResultButtons.cs` и `GameOverManager.RestartGame` перезагружают сцену без `EndRun`.
- Проблема: два стартовых контроллера допустимы только если UI использует один; Restart намеренно начинает текущую сцену заново, но не закрывает/не сбрасывает persistent run.
- Проявление: двойной клик/две кнопки могут дать разные paths; Restart после смерти сохраняет старый `RunStateManager`.
- Решение: проверить UnityEvent кнопок; для Vertical Slice использовать bunker door/`BunkerRunStarter` и MainMenu из GameOver через `RunEndService`. Не менять поведение Restart без продуктового решения.
- Scene/UI: **требуется проверка сцены и UnityEvent**.

### Level-choice UI останавливает время без аварийного восстановления

- Файл: `scripts/Selection/Levels/LevelChoiceManager.cs`, `ShowChoices`, `SelectNode`.
- Проблема: при пустом/сломавшемся card callback время останется `0`; runtime guard есть до паузы для пустого pool/panel.
- Проявление: soft-lock при неверных ссылках `LevelChoicePanelView.cardViews`.
- Решение: проверить три карточки и callbacks prefab; кодовый fallback не добавлять до воспроизведения.
- Prefab: **требуется проверка prefab и Play Mode**.

### Статистика HUD уровня не равна номеру endless-уровня

- Файлы: `PauseMenuUI.UpdateStats`, `RunResultView.Show` читают `ExperienceManager.currentLevel`, тогда как endless level хранится в `RunStateManager.CurrentLevel`.
- Проявление: поле `LEVEL` означает уровень персонажа, а не номер пройденной карты; может вводить в заблуждение.
- Решение: переименовать текст или явно показать оба значения после UI-решения. Не исправлено, чтобы не менять дизайн.
- Inspector: не требуется.

### Отсутствует защита от пустого `enemyPrefabs`

- Файл: `scripts/World/Spawning/EnemySpawner.cs`, `SpawnEnemy`.
- Проблема: используется `enemyPrefabs.Length` без null-check; Unity обычно сериализует пустой массив, но повреждённая/программная конфигурация даст NRE.
- Решение: добавить null-check; безопасное кодовое исправление.
- Inspector: проверить base list и каждый `spawnStages[].enemyPrefabs`.

### Scene-поля старого выбора намеренно пусты

- YAML MainMenu: `SelectionPanelController.sceneSelectPanel = null`, `BunkerPanelManager.mapPanel = null`.
- Это согласуется с текущим циклом (уровень выбирается после босса), но вызов `ShowScenes`/`OpenMap` ничего не откроет либо использует `BunkerRunStarter`.
- Решение: проверить, что старые кнопки не вызывают эти методы. Не назначать старые панели автоматически.

## CLEANUP

- `CurrencyManager.AddGold` печатает полный stack trace при каждом начислении — шум и аллокации; убрать после подтверждения единственной точки награды.
- `ExperienceManager.cs` и несколько UI-строк содержат mojibake-комментарии/текст; это не ломает компиляцию, но видимые строки в `CharacterSelectionUI` требуют проверки локализации.
- `EnemyHealth.TakeDamage` вызывает `GetComponent<EnemyWhiteFlash>` на каждый hit. Кэшировать при профилировочно подтверждённой нагрузке; не блокер.
- `EnemyHealth.Death` выполняет `FindFirstObjectByType<PlayerCombatModifiers>` на каждую смерть. Это не `Update`, но на массовых убийствах может быть дорого.
- `EnemySpawner.Update` повторяет `FindGameObjectWithTag` только пока игрок не найден; допустимо на bootstrap, но создаёт работу каждый кадр при сломанном spawn.
- `RunTimer.StartSurvivalPhase` и старые survival методы не участвуют в заявленном flow и должны остаться до подтверждения prefab/UnityEvent.
- `PanelManger.cs` содержит класс `PanelManager` с опечаткой имени файла; Unity GUID сохраняет ссылку, переименование можно отложить.

## DEAD CODE CANDIDATE

Не удалять без ручного подтверждения:

- `PortalLabel.cs`, `PortalToNextLevel.prefab`, `PortalToMenu.prefab`: старый portal flow; в активных сценах ссылок нет.
- `RunTimer.StartSurvivalPhase`, `RestartBossTimer`, `RunResultButtons`, `RunResultView` victory branch: остатки survival/victory/restart flow.
- `WorldEvent/*`: отдельная система событий; `LevelModifiersApplier` сейчас управляет только `holdZoneEventObject`, а обязательный VS не требует world events.
- `BunkerCallTimer`, `MenuZombieWalker`, `MatrixRainBackground`, `MenuAnimatedTransformDetector`: presentation/menu helpers, использование может быть только из сцен.
- `prefabs/Envir/p_chest.prefab` и `LevelNodeData.hasExtraChest`: данные есть, gameplay-обработчика сундука в текущем коде нет; сохранить на будущее.
- Debug: `DebugGoldCheat`, `DebugSaveResetButton`, `UnlockDebugHotkeys`; нужны для тестов, но должны быть исключены/недоступны в release UI.

## Инвентаризация типов

### MonoBehaviour

**Bootstrap/persistent:** `RunSelectionManager`, `RunStateManager`, `UnlockProgressService`, `CurrencyManager`, `MetaProgressionManager`, `MusicPlayer` (singleton, но без DDOL в коде).

**Run/flow/stats:** `RunEndService`, `RunFlowController`, `RunCompletionCleaner`, `RunTimer`, `RunStatsManager`, `RunLevelManager`, `KillManager`, `GameOverManager`, `CharacterSpawner`, `LevelChoiceManager`, `LevelModifiersApplier`.

**Bunker/selection:** `BunkerContext`, `BunkerPanelManager`, `BunkerRunStarter`, `BunkerRunSummaryPresenter`, `BunkerNotificationManager`, `BunkerContent`, `BunkerContentRegistry`, `BunkerShopService`, `BunkerShopUI`, `BunkerShopItemView`, `BunkerStation`, `BunkerCursorInteractor`, `BunkerInteractableCollider`, `BunkerHoverOutline`, `BunkerKickableBall`, `BunkerEventManager`, `BunkerGoalTrigger`, `CharacterSelectionUI`, `WeaponSelectionUI`, `CharacterCardView`, `WeaponCardView`, `SelectionPanelController`.

**Combat/player/enemy/weapons:** `PlayerHealth`, `CharacterMovement2D`, `PlayerPickupRadius`, `PlayerWhiteFlash`, `PlayerHitSound`, `PlayerCombatModifiers`, `EnemyHealth`, `EnemyIdentity`, `EnemyCollisionHandler`, `EnemyWhiteFlash`, `EnemySpawner`, все классы движения enemy, `EnemyProjectile`, `ExperiencePickup`, `UpgradeManager`, `UpgradeApplier`, `WeaponRuntimeStats`, `BaseWeapon`, `LaserWeapon`, `ProjectileWeapon`, `Bullet`, projectile/fire/fx behaviours, `ProjectileCombatContext`, `CircularBurstRuntime`, `NukeEveryTenKillsRuntime`.

**World/UI/support:** `WorldEvent`, `WorldEventSpawner`, `WorldEventMarker`, `CaptureZoneEvent`, `RescueCapsuleEvent`, `Interactable`, `DoorInteractable`, `PlayerInteractor`, `InteractionPromptUI`, все классы `UI/*`, `PauseMenuUI`, audio/UI sound, camera/fx/menu helpers.

Полный перечень MonoBehaviour соответствует всем классам с наследованием `MonoBehaviour` в 151 просмотренном файле; интерфейсы и data-классы перечислены ниже.

### ScriptableObject

`CharacterData`, `WeaponData`, `UpgradeData`, `LevelData`, `EnemyData`, `LevelNodeData`, `UnlockRegistry`, `UnlockableContentData`, `RunMessageData`, `BunkerContentData`.

### Singleton и DontDestroyOnLoad

| Класс | Singleton | DDOL | Область жизни |
|---|---:|---:|---|
| RunSelectionManager | да | да | полный путь MainMenu -> run |
| RunStateManager | да | да | создаётся программно, весь run и summary |
| UnlockProgressService | да | да | создаётся в MainMenu |
| CurrencyManager | да | да | обе сцены содержат bootstrap-копию |
| MetaProgressionManager | да | да | создаётся/находится по требованию |
| ExperienceManager, UpgradeManager, KillManager, RunStatsManager | да | нет | одна MVP сцена |
| RunFlowController, RunEndService, RunLevelManager | да | нет | одна MVP сцена |
| BunkerContext, BunkerNotificationManager | да | нет | MainMenu |
| HUDManager, CameraShake, RunMessageService, UISoundPlayer, GameOverManager | да | нет | сценовые UI/support |

### Data и сервисы без MonoBehaviour

- Data: `RunSummary`, `EnemySpawnStage`, `UnlockConditionData`, enums run/unlock/level/bunker/message/upgrade.
- Runtime helpers: `RunRewardCalculator`, `UpgradeRoller`, `CombatExplosionService`, `AICommentGenerator`, `WeaponFireContext`, `ProjectileCombatContext` (MonoBehaviour), interfaces fire/projectile/bunker interaction.
- Сохранение: `CurrencyManager`, `MetaProgressionManager`, `UnlockProgressService`, `BunkerShopService` используют `PlayerPrefs`; runtime run-state хранится только в `RunStateManager` в памяти.

## Проверка заданных рисков

| Риск | Результат |
|---|---|
| Двойное начисление золота | `EndRun` идемпотентен; второй прямой reward path не найден. Повторно применяется **множитель**, это CRITICAL выше. |
| Двойная статистика | `lastCommittedStatsInstanceId` защищает повторный commit одной сцены. Требуется 3-level Play Mode. |
| Потеря HP/XP/upgrades | XP и upgrades сохраняются; HP имеет BLOCKER порядка `Start`. |
| Unlock ID | Weather совпадает (`Darkness`, `Rain`, `Snow`); enemy ID требует prefab-проверки. |
| Victory/portals | вызовов Victory нет; старые portal prefab содержат отсутствующие скрипты, в активных сценах не используются. |
| Сундуки | gameplay кода нет; только `hasExtraChest`/UI marker и prefab. |
| Find в hot path | `EnemyHealth.TakeDamage` GetComponent; `EnemySpawner.Update` tag lookup лишь при null player. |
| Корутины disabled object | level-up использует realtime и объект остаётся активен; notification отменяет старую coroutine; явный дефект не доказан. |
| Animator params | вызовы параметров требуют ручной проверки controllers; массового warning path по коду не доказано. |
| UnityEvent | `EnemyHealth` и `ExperienceManager` имеют сериализуемые события; корректность listeners требует сцены/prefab. |
| Scene missing scripts | в `MainMenu` и `MVP` записей `m_Script: {fileID: 0}` нет. |

## План безопасных изменений

Перед каждым изменяемым файлом:

1. `PlayerHealth.cs`: условная инициализация HP; причина — не перетирать snapshot; риск — prefab с намеренно нулевым HP; проверка — новый run и переход с неполным HP.
2. `CurrencyManager.cs`: идемпотентная установка meta multiplier и удаление stack trace; причина — повторный бонус/спам; риск — будущие независимые источники бонуса; проверка — одинаковая награда после 1 и 3 reload при одинаковой базе.
3. `EnemySpawner.cs`: null-check массива; причина — явный NRE; риск минимальный; проверка — пустая конфигурация выдаёт отсутствие spawn без exception.
4. Singleton cleanup (`OnDestroy`) — только где безопасно и нужно; риск порядка уничтожения; проверка повторными загрузками.

## Что проверено и что нет

- **Проверено по коду:** ownership награды, state snapshots, level transition, boss death, unlock progression.
- **Проверено по YAML:** наличие основных компонентов и заполненность видимых ссылок в MainMenu/MVP; отсутствие missing scripts в этих сценах; старые portal prefab.
- **Требуется проверка prefab:** enemy IDs, card arrays/listeners, boss `isBoss`, Player tag/components, LevelChoice cards.
- **Требуется ручной Play Mode тест:** весь план из `VerticalSlice_TestPlan.md`; статический анализ не подтверждает корректность UnityEvent, execution order во всех prefab и Console без ошибок.
