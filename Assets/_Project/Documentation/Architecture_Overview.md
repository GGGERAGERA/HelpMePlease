# Архитектура Vertical Slice

Документ описывает фактический код после раскладки папок. Все runtime-классы остаются без namespaces. Ссылки Inspector отмечены отдельно; их наличие в YAML не заменяет Play Mode проверку.

## Главные потоки

```text
MainMenu
  -> RunSelectionManager (character + weapon)
  -> BunkerRunStarter / MainMenuController
  -> RunStateManager.BeginNewRun
  -> load MVP

Boss death
  -> EnemyHealth
  -> RunFlowController
  -> register modifier + completed level
  -> stop spawning + clear enemies
  -> 5 real seconds
  -> LevelChoiceManager / LevelChoicePanelView
  -> save HP + XP + scene stats
  -> select node + advance level
  -> reload MVP

Pause or GameOver
  -> RunEndService
  -> RunStateManager.EndRun (idempotent)
  -> RunRewardCalculator
  -> CurrencyManager.AddGold
  -> load MainMenu
  -> BunkerRunSummaryPresenter consumes summary once

Enemy death
  -> KillManager -> RunStatsManager
  -> EnemyIdentity -> UnlockProgressService

Completed weather modifier
  -> RunFlowController
  -> UnlockProgressService
```

## 1. Bootstrap и постоянные сервисы

| Модуль | Главный класс | Принимает / изменяет | Зависимости и caller | Между сценами / Inspector / Console |
|---|---|---|---|---|
| Выбор до run | `RunSelectionManager` | Принимает `CharacterData`, `WeaponData`; меняет текущий выбор | Selection UI, starter/spawner | DDOL; без полей; искать duplicate singleton |
| Состояние run | `RunStateManager` | Принимает выбор, player, upgrades, stats; меняет snapshots/итоги/номер уровня | starter, character spawner, level choice, end service | DDOL, создаётся `EnsureExists`; искать `Save skipped`, missing stats |
| Unlock progress | `UnlockProgressService` | condition + target ID; меняет `PlayerPrefs` progress/unlock | enemy death, boss flow, cards/debug | DDOL; `UnlockRegistry` обязателен; искать `Registry is missing` |
| Валюта | `CurrencyManager` | gold delta/percent; меняет `TOTAL_GOLD` | run state, shop, debug, meta | DDOL; есть bootstrap в обеих сценах; искать duplicate/add logs |
| Meta levels | `MetaProgressionManager` | upgrade type; меняет PlayerPrefs уровней | shop, `MetaUpgradeApplier` | DDOL/EnsureExists; без Inspector; искать missing currency |

## 2. Bunker interaction

- Ответственность: mouse hover/click по станциям, цели бункера, физический мяч и контекст сервисов.
- Главные классы: `BunkerContext`, `BunkerCursorInteractor`, `BunkerStation`, `BunkerInteractableCollider`, `BunkerEventManager`.
- Данные: `BunkerContentData`, station/content enums, collider/hover refs.
- Изменения: открытие панелей и локальные bunker events; глобальные награды не начисляет.
- Зависит от: `BunkerPanelManager`, shop, notification, run starter.
- Caller: Unity input/collider и scene events.
- Жизнь: только MainMenu; `BunkerContext` не DDOL.
- Inspector: все backing fields `BunkerContext`, camera/layer/collider refs.
- Console: missing context/panel/service, null content data.

## 3. Bunker UI и панели

- Ответственность: открыть ровно одну панель, показать shop/selection/summary/notifications.
- Главные классы: `BunkerPanelManager`, `SelectionPanelController`, `BunkerNotificationManager`, `BunkerRunSummaryPresenter`.
- Принимает: команды станций, `RunSummary`, UI clicks.
- Изменяет: active state и view text; summary только consume, награду не начисляет.
- Зависит от: `BunkerContext`, `RunStateManager`, конкретные view.
- Caller: station/UnityEvent.
- Жизнь: MainMenu.
- Inspector: root panels, CanvasGroup, texts, service refs.
- Console: missing notifications, missing panel manager.

## 4–5. Выбор персонажа и оружия

| Модуль | Главные классы | Данные | Изменяет | Зависимости / Inspector | Console |
|---|---|---|---|---|---|
| Character | `CharacterSelectionUI`, `CharacterCardView` | `CharacterData`, `UnlockableContentData` | только `RunSelectionManager.SelectedCharacter` через метод | cards[], details text/image, buttons, panel manager; unlock service | missing RunSelectionManager |
| Weapon | `WeaponSelectionUI`, `WeaponCardView` | `WeaponData`, unlock data | только selected weapon | cards[], icon/stats/buttons/panel manager | missing selection/panel manager |

Карточки сами проверяют unlock и сообщают click; они не меняют PlayerPrefs. Жизнь — MainMenu/prefab UI.

## 6. Запуск забега

- Главный класс: `BunkerRunStarter` (альтернативный UI entry — `MainMenuController`).
- Принимает: выбранные character/weapon.
- Изменяет: вызывает `RunStateManager.BeginNewRun`, затем загружает `MVP`.
- Зависит от: `RunSelectionManager`, notifications, scene name.
- Caller: bunker door/station или button UnityEvent.
- Жизнь: MainMenu.
- Inspector: `gameplaySceneName = MVP`.
- Console: selection missing; загрузочная ошибка сцены.

## 7. RunSelectionManager

Хранит только ссылки на выбранные data assets. DDOL. Очищается при старте MainMenu. Не хранит HP/XP/upgrades и не должен использоваться как run state.

## 8. RunStateManager

Хранит selected data, `CurrentLevel`, selected node, список upgrades, health/XP snapshots, накопленные stats и однократно потребляемый summary. Не хранит scene GameObject. Guards: `runEnded` и `lastCommittedStatsInstanceId`.

## 9. CharacterSpawner

- Принимает selection/run state/default assets.
- Создаёт player prefab и weapon, применяет character base stats, meta bonuses, затем run upgrades/snapshot.
- Зависит от `MetaUpgradeApplier`, `UpgradeApplier`, Player tag/components.
- Caller: Unity `Start` MVP.
- Inspector: defaults, spawnPoint, `weaponPointName`, two appliers.
- Console: no selected/default character/weapon, missing prefab/BaseWeapon/applier.

## 10. EnemySpawner

- Принимает prefab lists/stages и difficulty multipliers.
- Изменяет частоту spawn, лимит и runtime health/speed новых enemies.
- Зависит от Player tag и `EnemyHealth`/`EnemyMovement` на prefab.
- Caller: Unity `Update`, `LevelModifiersApplier`, boss flow stop.
- Жизнь: MVP scene/prefab instance.
- Inspector: base enemies, distances, stage arrays, limits.
- Console: difficulty logs; пустые lists не должны давать exception.

## 11. RunTimer и boss spawn

- Считает 300 секунд, обновляет HUD, один раз создаёт boss после warning delay.
- Зависит от Player tag, boss prefab, HUD/message/camera.
- Caller: Unity Update.
- Inspector: duration, boss prefab, distance, warning clip/volume/delay.
- Console: `bossPrefab или Player не найден` означает провал теста.

## 12. EnemyHealth и убийства

- Принимает damage; меняет HP/death state.
- На death: unlock progress по `EnemyIdentity`, kill stats, event callbacks, loot/fx; для boss вызывает `RunFlowController`.
- Зависит от HUD, unlock service, kill manager, prefab UnityEvents/FX.
- Inspector: `isBoss`, bossName, loot/fx/audio/events; `EnemyIdentity.enemyId`.
- Console: missing identity/unlock/run flow — ошибка конфигурации VS.

## 13. Опыт и level-up

- `ExperiencePickup` передаёт XP в `ExperienceManager`.
- Manager синхронно меняет XP/character level и передаёт level-up в очередь `UpgradeManager`.
- `RunStateManager` сохраняет/восстанавливает XP между MVP.
- Inspector: `LevelData` и events.
- Console: отсутствие `LevelData` блокирует XP без exception; это ошибка конфигурации.

## 14. Run-upgrades

- `UpgradeManager` последовательно обрабатывает очередь choices, ставит pause и вызывает view.
- `UpgradeApplier` находит player context и изменяет health/movement/weapons/modifiers.
- После успешного apply upgrade регистрируется в `RunStateManager`; при новом MVP применяется один раз.
- Inspector: panel, applier, allUpgrades, count.
- Console: missing panel/applier/player context, unimplemented upgrade type.

## 15–17. Смерть босса, RunFlowController, очистка

- `EnemyHealth` вызывает `HandleBossDefeated` один раз благодаря собственному `isDead` и `RunFlowController.levelCompleted`.
- Flow регистрирует пройденный modifier и completed level, останавливает spawner, вызывает `RunCompletionCleaner`, ждёт 5 секунд и открывает choice.
- Cleaner уничтожает объекты с tag `Enemy` без death rewards.
- Inspector: level choice manager, delay, stop flag, cleaner, enemy tag.
- Console: missing manager/cleaner/tag/unlock service — ошибка.

## 18–20. Выбор уровня и применение модификаторов

- `LevelChoiceManager` выбирает уникальные `LevelNodeData`, передаёт их panel/card views.
- При выборе сохраняет stats/XP/HP, повышает endless level, сохраняет node и reload target scene.
- `LevelModifiersApplier` после одного frame находит current spawner и применяет weather, event и endless scaling.
- Inspector: panel, availableNodes, gameplay scene, 3 card views; weather objects/light/event/spawner.
- Console: no nodes/panel; missing scene; spawner null означает отсутствие scaling.

## 21–22. Погода и сложность врагов

`LevelNodeData` задаёт weather и node multipliers. `LevelModifiersApplier` сначала выключает все эффекты, затем включает выбранный и умножает параметры на рост от `CurrentLevel`. `EnemySpawner.SetLevelScaling` ограничивает interval/health/speed/enemy count. Жизнь только MVP; настройки — scene + ScriptableObject.

## 23–24. Unlock service, registry и условия

- Registry перечисляет unlock content assets.
- Conditions поддерживают kill enemy type и completion level modifier.
- Ключи: `Unlock_<content.id>` и `UnlockProgress_<content.id>`.
- Сравнение `targetId` строгое.
- MainMenu создаёт service с registry, DDOL переносит его в MVP.
- Console: registry missing/empty ID/missing enemy identity.

## 25–26. CurrencyManager и MetaUpgradeShop

- Currency загружает/сохраняет total gold; единственная gameplay-награда приходит из `RunStateManager.EndRun`.
- `MetaProgressionManager.BuyUpgrade` проверяет cost, списывает через Currency и сохраняет уровень.
- `MetaUpgradeShopUI`/cards только отображают и вызывают manager.
- Inspector: shop cards/views; managers — без обязательных data refs.
- Console: missing Currency/Meta managers.

## 27–29. Stats, reward, end service

- `RunStatsManager`: kills/time текущей MVP.
- `RunStateManager.CommitCurrentSceneStats`: добавляет их один раз.
- `RunRewardCalculator`: pure formula kills + minutes + completed levels, death multiplier.
- `RunStateManager.EndRun`: идемпотентно считает и начисляет один раз.
- `RunEndService`: единая точка выхода и загрузка MainMenu.
- Inspector: bunker scene name.
- Console: missing RunStats предупреждает о неполной summary; missing Currency сейчас не блокирует scene load, но теряет reward.

## 30–31. Возврат и summary в бункере

`RunEndService` возвращает в MainMenu. Persistent `RunStateManager` несёт `lastRunSummary`. `BunkerRunSummaryPresenter` ждёт UI bootstrap, consume summary один раз и показывает notification. Inspector: delay. Console: missing notification даёт warning и fallback log.

## 32. GameOver

`PlayerHealth.Die` выключает движение/renderers и открывает `GameOverManager`. Manager ставит timeScale 0 и показывает result view. MainMenu button обязан вызвать `RunEndService.EndRunAfterDeath`. Restart — отдельный legacy/debug path и не считается корректным завершением run.

## 33. Pause

`PauseMenuUI` переключает panel/timeScale. Resume восстанавливает 1. MainMenu вызывает `RunEndService.ReturnToBunker`; UI награду не считает. Inspector: panel и texts. Console: missing end service.

## 34. Debug-инструменты

- `DebugGoldCheat`: добавляет gold.
- `DebugSaveResetButton`: `PlayerPrefs.DeleteAll` и reload — затрагивает все сохранения.
- `UnlockDebugHotkeys`: editor-only методы unlock service.
- Должны использоваться только при тестировании; scene `MainMenu` сейчас содержит debug objects, поэтому release visibility нужно проверить вручную.

## ScriptableObject

| Тип | Назначение | Главные потребители |
|---|---|---|
| `CharacterData` | prefab/stats/portrait/unlock | cards, selection, spawner |
| `WeaponData` | prefab/stats/icon/unlock | cards, spawner, weapons |
| `UpgradeData` | type/value/rarity/availability | roller, manager, applier |
| `LevelData` | XP curve | ExperienceManager |
| `EnemyData` | enemy configuration (использование ограничено) | требует prefab verification |
| `LevelNodeData` | next scene/weather/difficulty/event | choice, modifier applier |
| `UnlockRegistry` | полный список unlock content | UnlockProgressService |
| `UnlockableContentData` | id/default/condition | registry/cards/service |
| `RunMessageData` | message presentation | RunMessageService/View |
| `BunkerContentData` | bunker content/shop | bunker registry/shop/content |

## Ошибки, которые искать в Console

- `No selected/default character`, `Weapon prefab is missing`, `spawned weapon has no BaseWeapon`.
- `RunCompletionCleaner is not assigned`, `LevelChoiceManager not found`, `No level nodes available`.
- `UnlockProgressService/Registry/EnemyIdentity is missing`.
- `RunStatsManager is missing`, `CurrencyManager is missing`, `RunEndService is missing`.
- Любой `NullReferenceException`, missing script, scene not in Build Settings, Animator parameter warning.
