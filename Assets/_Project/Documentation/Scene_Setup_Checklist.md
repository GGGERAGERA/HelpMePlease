# Scene & Inspector Checklist

Легенда: **YAML** — ссылка/компонент найден в сохранённой сцене; **ручная проверка** — Unity Inspector/Play Mode необходим, даже если fileID не нулевой.

## MainMenu

### Обязательные root GameObject

- [x] `RunSelectionManager` — root, active, `RunSelectionManager` (**YAML**), DDOL.
- [x] `UnlockProgressService` — root, active, service + registry (**YAML**), DDOL.
- [x] `CurrencyManager` — root, active (**YAML**), DDOL.
- [x] `MetaProgressionManager` — root, active (**YAML**), DDOL.
- [x] `BunkerContext` — root, active, вместе с `BunkerContentRegistry` (**YAML**), только MainMenu.
- [x] `BunkerUIManager` — root, active, `BunkerPanelManager` + `BunkerRunStarter` (**YAML**).
- [x] `BunkerInteractionSystem`, `BunkerEventManager`, `BunkerShopService` — active (**YAML**).
- [x] `BunkerRunSummaryPresenter` — root, active (**YAML**).
- [x] `NotificationRoot` — active при старте, view скрывается кодом/CanvasGroup (**YAML**, ручная визуальная проверка).
- [x] EventSystem и main camera присутствуют (**YAML**).

### Стартовое состояние UI

- [ ] Главный bunker HUD доступен; modal selection/shop/meta panels не блокируют raycasts при старте.
- [x] `SelectionPanel` GameObject, на котором стоит `SelectionPanelController`, сохранён inactive (**YAML**).
- [ ] Character/weapon/shop/meta panels закрыты до interaction.
- [ ] Notification root визуально скрыт после `Awake`, несмотря на active GameObject.
- [ ] Debug panels скрыты или недоступны в release build.

### Persistent services

Только эти сервисы должны переживать MainMenu -> MVP:

- `RunSelectionManager`
- `RunStateManager` (создаётся программно при старте run)
- `UnlockProgressService`
- `CurrencyManager`
- `MetaProgressionManager`

`BunkerContext`, panel managers, notifications и shop service **не** должны быть DDOL. В MVP нельзя вручную добавлять `UnlockProgressService`, `RunSelectionManager` или `MetaProgressionManager`; `CurrencyManager` в MVP оставлен как fallback прямого запуска и должен уничтожаться singleton guard при обычном пути.

## MVP

### Обязательные root GameObject

- [x] `GameManager` active: `CharacterSpawner`, `ExperienceManager`, `UpgradeManager`, `RunStatsManager`, `KillManager`, `MetaUpgradeApplier`, `RunTimer`, `WorldEventSpawner`, `RunLevelManager`, `UpgradeApplier`, `RunFlowController` (**YAML**).
- [x] `RunEndService`, `RunCompletionCleaner`, `LevelChoiceManager`, `LevelModifiersApplier`, `GameOverManager` — отдельные active roots (**YAML**).
- [x] `HUD`, `PauseMenuManager`, `RunMessageService`, `RunMessagePanel`, camera rig/main camera — active (**YAML**).
- [x] `CurrencyManager` fallback root — active (**YAML**); при обычном path остаётся ровно один persistent instance.
- [x] Level choice и upgrade views приходят из prefab instances (**YAML**).
- [ ] EnemySpawner prefab instance активен и ссылка `LevelModifiersApplier.enemySpawner` указывает на живой scene instance, не на asset.

### Стартовое состояние UI/effects

- [ ] Pause panel inactive.
- [ ] GameOver/run result panel inactive.
- [ ] Upgrade choice panel inactive.
- [ ] Level choice panel inactive.
- [ ] LowHPVignette inactive (**YAML**).
- [ ] Rain/snow inactive после первого кадра; normal global light восстановлен.
- [x] `WorldEventMarker` сохранён inactive (**YAML**); это не влияет на обязательный цикл.

## Inspector: обязательные компоненты

### RunSelectionManager

- MainMenu root, active; полей нет.
- Не дублировать вручную в MVP.
- Проверка: после scene load объект имеет `DontDestroyOnLoad` scene и один instance.

### RunStateManager

- Не назначается в Inspector; создаётся `RunStateManager.EnsureExists()`.
- Проверка: до загрузки MVP после bunker start существует ровно один instance; direct MVP может начать без него.

### UnlockProgressService

- `registry` -> `Scriptable Objects/Unlock/UnlockRegistry.asset` (**YAML, non-null**).
- Root MainMenu, active, DDOL.
- Проверить registry: все 6 character/weapon unlock assets без дубликатов ID.

### CurrencyManager

- Полей Inspector нет.
- Root в MainMenu и fallback root в MVP (**YAML**).
- Проверить один instance после MainMenu -> MVP -> MainMenu и сохранение `TOTAL_GOLD`.

### BunkerContext

- `Panels` -> BunkerPanelManager; `Notifications` -> notification manager; `Events` -> event manager; `Shop` -> shop service; `RunStarter` -> starter; `ContentRegistry` -> registry (**YAML, все non-null**).
- Root, active, не DDOL.

### BunkerPanelManager

- `selectionPanelController` non-null (**YAML**).
- `shopUI`, `upgradePanel`, `runStarter`, `metaUpgradeShopUI` non-null (**YAML**).
- `mapPanel` сейчас null (**YAML**) — допустимо для удалённого pre-run level selection; убедиться, что никакая активная кнопка не вызывает старый map flow.

### SelectionPanelController

- `root`, player/weapon/shop/upgrade panels non-null (**YAML**).
- `sceneSelectPanel` сейчас null (**YAML**) — старый flow не подключать.
- GameObject inactive при старте.

### CharacterSelectionUI

- Компонент находится внутри prefab instance, поэтому scene grep не раскрывает все поля.
- `cards[]` — все отдельные `CharacterCardView`, без null/дубликатов.
- Все details text/image, select/back buttons, `panelManager` назначены.
- Select button изначально disabled; locked card не позволяет confirm.
- **Требуется проверка prefab**.

### WeaponSelectionUI

- Компонент найден как stripped prefab instance (**YAML**).
- `cards[]`, back/confirm, panel manager, icon/details/stats назначены.
- Confirm изначально disabled; locked card не подтверждается.
- **Требуется проверка prefab**.

### CharacterCardView / WeaponCardView

- На каждой карточке свой `CharacterData`/`WeaponData`, image/text/locked overlay.
- MainMenu содержит 3 character card components через prefab instances (**YAML**); weapon cards раскрываются внутри weapon selection prefab.
- Не смешивать типы карточек; `CharacterCardView` и `WeaponCardView` отдельны.
- Проверить Button на том же GameObject и отсутствие duplicate listeners.

### RunFlowController

- На `GameManager` (**YAML**).
- `levelChoiceManager` -> scene manager; `completionCleaner` -> cleaner; delay `5`; `stopEnemySpawnerAfterBoss = true`.
- Все ссылки должны быть scene objects, не project assets.

### RunCompletionCleaner

- Root active (**YAML**); `enemyTag = Enemy` (**YAML**).
- Tag `Enemy` существует; каждый обычный enemy root имеет этот tag.

### LevelChoiceManager

- Root active (**YAML**).
- `panelView` non-null prefab instance; `availableNodes` содержит 4 non-null assets; `gameplaySceneName = MVP`; `choicesCount = 3` (**YAML**).
- Каждый node `SceneName` пуст или существует в Build Settings.

### LevelChoicePanelView

- Prefab instance (**YAML**).
- `titleText`, `subtitleText`, ровно 3 `cardViews` non-null.
- Root скрыт при Awake; buttons не имеют старых persistent callbacks.
- **Требуется проверка prefab**.

### LevelModifiersApplier

- Root active (**YAML**).
- `enemySpawner`, rain, snow, global Light2D non-null (**YAML**); `holdZoneEventObject` сейчас null (**YAML**, допустимо если nodes не требуют event).
- Scene override growth: HP `0.12`, speed `0.025`, spawn `0.08`; это отличается от code defaults и является фактическим балансом (**YAML**).

### EnemySpawner

- Приходит из prefab; `LevelModifiersApplier` содержит ссылку на prefab scene instance (**YAML**).
- Base `enemyPrefabs` и каждый stage list non-empty; intervals/distances > 0; min <= max.
- Enemy prefab: `EnemyHealth`, `EnemyIdentity`, movement/collider; root tag `Enemy`.
- **Требуется проверка prefab**.

### RunTimer

- На GameManager (**YAML**).
- `runDuration = 300`, boss prefab non-null, spawn distance 8, delay 1; warning clip optional.
- Boss prefab: `EnemyHealth.isBoss = true`, tag `Enemy`, `EnemyIdentity`, boss UI name, no stale Victory/portal callback.
- **Требуется проверка prefab boss**.

### RunEndService

- Root active; `bunkerSceneName = MainMenu` (**YAML**).
- Pause/GameOver MainMenu actions должны ссылаться только на собственные controller methods, которые вызывают service.

### PauseMenuUI

- Root `PauseMenuManager`, active (**YAML**).
- pausePanel/stats/title non-null (**YAML**); panel inactive при старте.
- MainMenu button -> `PauseMenuUI.MainMenu`; Resume -> `Resume`; не использовать прямой `SceneManager.LoadScene` из Button.

### GameOverManager

- Root active; `runResultView` non-null prefab instance (**YAML**).
- Result panel inactive; MainMenu -> `GameOverManager.MainMenu`; Restart считается отдельным legacy/debug path.

### BunkerNotificationManager

- `root`, CanvasGroup, panel, background, message non-null (**YAML**); durations/colors заполнены.
- Не DDOL; `BunkerContext.Notifications` указывает на него.

### BunkerRunSummaryPresenter

- Root active (**YAML**); delay должен быть >= 0 (code default/scene value проверить Inspector).
- Один экземпляр; notification manager уже должен пройти Awake.

## Часто слетающие ссылки после UI-изменений

- Arrays `CharacterSelectionUI.cards`, `WeaponSelectionUI.cards`, `LevelChoicePanelView.cardViews`, `UpgradePanelView` cards.
- Button components на card views и select/confirm/back buttons.
- Prefab instance overrides `panelManager` в bunker selection prefab.
- `BunkerContext` backing fields после замены service GameObject.
- `RunFlowController` -> LevelChoiceManager/Cleaner.
- `LevelChoiceManager` -> prefab panel view и node assets.
- `GameOverManager` -> result view; `PauseMenuUI` -> panel/texts.
- `LevelModifiersApplier` -> scene Light2D/weather/spawner.
- Boss prefab `isBoss`, loot, enemy ID и tag.

## Build Settings и ручная проверка

- [ ] `MainMenu` и `MVP` добавлены и enabled; имена точно совпадают со строками.
- [ ] Обычный старт — `MainMenu`, не direct MVP.
- [ ] После folder import Console без compile/missing script errors.
- [ ] Active scenes не содержат Missing Script (статически `m_Script fileID: 0` не найден — **YAML**).
- [ ] Старые `PortalToNextLevel`/`PortalToMenu` не находятся в active hierarchy.
- [ ] UnityEvent listeners просмотрены вручную; C#-аудит не может подтвердить их корректность.
