# Production Hold Zone / Orbital Relay — спецификация миграции

Дата: 2026-10-06. Статус: проект для совместного подтверждения со следующим implementation plan; реализация ещё не разрешена.

## 1. Цель и границы

Заменить production Capture Zone проверенной двухфазной ORBITAL-механикой. Один production runtime и один production prefab используются в игре, tutorial и WorldSystemsLab. Отдельной lab-копии gameplay, legacy CaptureZone и compatibility layer после миграции нет.

State flow: `Inactive → Stabilization → Transition → Bonus → Completed`; таймаут Stabilization ведёт в `Failed`. Runtime composition и уникальная FX-геометрия authored в prefab. Награды используют существующие CurrencyManager/UpgradeManager; враги — существующий EnemySpawner через WorldEventSpawner. Другие Events, Bunker, economy multipliers, mission progression и общая threat-архитектура не переписываются.

Реализация затрагивает MVP только заменой event catalog reference; другие production-scene настройки не меняются. Перевод tutorial, bot и tests — прямые зависимости замены, а не отдельный рефакторинг.

## 2. Подтверждённый аудит текущего проекта

| Область | Найдено | Решение |
|---|---|---|
| Старый runtime | `Assets/_Project/scripts/World/Events/CaptureZoneEvent.cs` | Полностью заменить и удалить |
| Старый visual | `CaptureZoneVisual.cs`, внутри также `CaptureZoneCompletionVisual` | Удалить после переноса presentation; не переносить создание Mesh/GameObject |
| Старый prefab | `Assets/_Project/prefabs/Environment/WorldEvents/CaptureZoneEvent.prefab` | Новый OrbitalRelay prefab; перевести ссылки и удалить старый |
| Старый exclusive art | `Assets/_Project/art/mat/WorldRules/M_CaptureZone.mat`, `Assets/_Project/Shaders/CaptureZone.shader` | Удалить вместе с .meta после финальной проверки GUID usages |
| Registry | `WorldEventSpawner.eventPrefabs`, `Scenes/MainBuild/MVP.unity`; веса в `Data/SurfaceMap/Sector_D1.asset`, `Sector_D2.asset` | Заменить записи, сохранить числовые веса |
| Config старого event | `requiredHoldTime`, `captureRadius`, `resetProgressOnExit`, `timerText` в prefab/script | Удалить; отдельного exclusive CaptureZone ScriptableObject не найдено |
| Tutorial | `TutorialController`, `TutorialOverlay`, `ProductionExplorationSectorController.InitializeTutorialSector` | Новая Node-цель, тот же event, retry после fail |
| Bot/tests | `Dev/Debug/GoldenPathLab/BotController.cs`, `Dev/Tests/Core/Editor/Subject42TutorialTests.cs` | Movement/contact с новым event; старое ожидание в центре удалить |
| Event reward | `WorldEventSpawner.EventCompleted → SpawnRewardContainer → WorldBreakable → UpgradeManager` | Сохранить для других Events; Relay выдаёт typed result через этот же completion dispatcher и общую queue, без контейнера |
| Site reward | `ProductionAnomalySite.HandleEventCompleted` уже запрашивает normal Upgrade; special site отдельно выдаёт Ring после двух Events | Для Relay убрать дублирующий normal Upgrade; special-site Ring остаётся отдельным существующим site reward |
| Currency | `CurrencyManager.AddGold` применяет income multiplier; `AddGoldExact` — фиксированную выплату | Relay вызывает AddGoldExact для точного совпадения HUD и выплаты |
| Pressure | `WorldEventSpawner` выставляет `standardEventPressure=1.15`; `EnemySpawner` уже учитывает event pressure | Scoped modifier, без второго spawner; первая фаза Relay имеет обычный multiplier 1 |
| Contact | `OrbitalModuleRuntime.TouchesCircle` и `OrbitalModuleVisual.TouchesCircle` под editor/development define | Runtime-safe API после извлечения геометрического detector; guards mount/drag/preview сохраняются |
| Lab | `OrbitalHoldZoneEvent`, lab prefab, controller debug HUD, local event spawner, authoring substitution | Удалить gameplay/prefab; оставить bootstrap adapter и selector production prefab |
| Localization | `event.capture`, `event.capture.name`, `event.capture.description`, `event.enter` | Новые `event.relay.*`; удалять старые только по exact usages, включая localized components |

Вторичный reference sweep проводится по всему `Assets`, C# и GUID, перед любым удалением. Старые исторические audit/design документы не являются runtime reference; ссылку на актуальную миграцию добавляем в текущую документацию вместо переписывания исторических выводов.

## 3. Итоговая структура файлов и ответственности

Production scripts размещаются рядом с другими Events, без новых глубоких папок:

| Новый файл | Ответственность |
|---|---|
| `scripts/World/Events/OrbitalRelayEvent.cs` | `WorldEvent` adapter: валидация, player/station binding, contact sampling в LateUpdate, lifecycle, markers, result snapshot |
| `scripts/World/Events/OrbitalRelayConfig.cs` | Immutable ScriptableObject настройки, validation; экземпляр `Data/World/Events/OrbitalRelayConfig.asset` |
| `scripts/World/Events/OrbitalRelayState.cs` | Чистая C# state machine: enum, remaining timer, continuous contact, стабилизация, bonus score, combo; никаких MonoBehaviour/UI/spawn/reward side effects |
| `scripts/World/Events/OrbitalRelayNode.cs` | Ссылки на authored CircleCollider2D, core/glow/progress/activation FX; доступ к contact circle и обновление локального progress |
| `scripts/World/Events/OrbitalRelayPresentation.cs` | Typed snapshot → authored TMP/UGUI, Animator, Node visuals, transition shockwave, optional existing CameraShake |
| `scripts/World/Events/OrbitalRelayResult.cs` | Неизменяемый итог и чистый расчёт Gold/Upgrade; также общий nullable `WorldEventRewardResult` value для dispatcher |
| `scripts/World/Events/WorldEventPressureModifier.cs` | Optional компонент: ровно один lease pressure, освобождение в terminal/disable/destroy; никаких собственных enemy instances |
| `scripts/Combat/OrbitalStation/OrbitalBodyContactDetector.cs` | Общая runtime-safe геометрия пересечения authored sprite-body с кругом |
| `Dev/Debug/WorldSystemsLab/WorldSystemsLabRelayAdapter.cs` | Только lab bootstrap: временный run/station, scene-level debug EnemySpawner, запуск того же production prefab; никаких timers/score/phases/reward formulas |

`OrbitalRelayState.cs` также объявляет `OrbitalRelayPhase` и immutable `OrbitalRelaySnapshot`. В `OrbitalRelayResult.cs` фиксируем один Upgrade, не вводим tiers, configurable reward counts или вторую reward queue. Старый lab `guaranteedUpgradeCount` удаляется: production contract — ровно одна selection.

Минимальные изменения общих owners:

- `WorldEvent.cs`: virtual configuration validation; virtual `UsesStandardSpawnPressure` (default true); virtual nullable `CompletionReward` (default null); virtual `Cancel()` с текущим fail-default для других Events.
- `WorldEventSpawner.cs`: validate до Instantiate/register, completion dispatcher, scoped pressure lease API и явная policy preview/production для custom result. Отсутствие custom result сохраняет нынешний generic container path.
- `EnemySpawner.cs`: без Relay-special-case. Уже существующая setter/formula используется; нужны только доказательства очистки и совместимости с reset/disabled.
- `ProductionAnomalySite.cs`: нормальный site не выдаёт второй Upgrade за event с custom completion reward; completion site привязывается к idle общей reward queue. Другие Events остаются на нынешнем path.

## 4. State machine и точная игровая семантика

| Состояние | Таймер и контакты | Выход |
|---|---|---|
| Inactive | Таймер не идёт, все Node неактивны | Валидный запуск → Stabilization |
| Stabilization | 20 секунд; один активный Node; continuous contact 0.6 секунды; X/3 | Порог достигнут → Transition; таймаут → Failed |
| Transition | 0.75 секунды; contact sampling/активации остановлены; timers Stabilization/Bonus и combo не идут | По отдельному transition timer → Bonus |
| Bonus | Новый полный таймер 15 секунд; score начинается с 0; Node переключаются бесконечно | Таймер истёк → Completed независимо от score |
| Completed / Failed | Терминальные состояния; никаких новых contacts/score/pressure/reward side effects | Snapshot и cleanup один раз |

`StabilizationActivations` и `BonusActivations` — разные счётчики. HUD и итоговый Gold используют только BonusActivations. Combo сбрасывается при входе в Bonus: новая независимая серия. Активный Node остаётся единственным; следующий выбирается равномерно среди коллекции кроме предыдущего, для 2 Node происходит чередование. Перед Transition выбирается следующий, но включается он только после перехода.

Разрыв aggregate mounted-body contact сбрасывает local progress в 0. Несколько реально mounted modules могут непрерывно передавать контакт друг другу; drag/preview/halo/hidden/detached тела не считаются. Игрок должен находиться в authored arena bounds; выход останавливает contact, но не event timer. RMB compression и обычное orbital input ownership не изменяются.

Combo: первая активация даёт x1; последующая до истечения `comboWindow` увеличивает series; expiry сбрасывает x0; следующая начинает x1. Combo не изменяет Gold или Upgrade. Transition не съедает combo window, но Bonus начинает новую series.

Tick использует scaled gameplay time; pause не расходует timers и contact. Отрезок последнего кадра ограничен remaining phase time. Contact, завершённый точно на границе таймера, засчитывается до решения о timeout; contact после нуля не засчитывается. Overshoot кадра не сокращает полный следующий phase timer.

### Cancel, death, reset и unload

- Gameplay cancel до стабилизации → Failed без rewards.
- Gameplay cancel после стабилизации → Completed с уже earned Upgrade и Gold за завершённые bonus contacts; transition cancel имеет Gold=0. Подсистема reward shutdown сохраняет свою существующую политику доступности UI при смерти, не создаём новую death reward queue.
- Administrative reset/debug-clear/run-release (включая текущий death flow)/scene-unload — disposal, не gameplay completion: pressure/UI/subscriptions очищаются, никаких новых currency writes или открытия reward UI при teardown. Уже завершённые выплаты не повторяются. Snapshot может быть discarded, старую run persistence не меняем.
- Terminal путь идемпотентен. Runtime adapter не прячет базовый `WorldEvent.OnDestroy`; cleanup hooks вызывают собственный idempotent disposal. OnDisable не должен оставлять ActiveEvent/lease; explicit cancel и run-release различаются по контексту.

Это различие намеренное: earned qualification относится к игровому результату, а scene teardown не может безопасно открывать UI нового уровня. Смерть следует существующему run-release, а не новому самостоятельному payout path. Если нужна durable компенсация Upgrade после смерти/аварийного unload, это отдельная run-persistence задача вне этой миграции.

## 5. Config и безопасность

`OrbitalRelayConfig.asset` defaults:

| Поле | Значение |
|---|---:|
| requiredActivations | 3 |
| stabilizationDuration | 20 сек. |
| stabilizationContactTime | 0.6 сек.; также используется в Bonus |
| transitionDuration | 0.75 сек. |
| bonusDuration | 15 сек. |
| goldPerActivation | 10 |
| comboWindow | 2.5 сек. |
| bonusEnemyPressureMultiplier | 1.6 |

Runtime получает validated immutable copy, не мутирует shared config asset. Минимум 2 разных Node; нет null/duplicate entries; есть config, arena/contact colliders, core/progress/presentation references. Значения durations/contact/combo положительные finite, requiredActivations>=1, gold>=0, pressure>=1 finite. Invalid prefab отклоняется до spawn bookkeeping с одной понятной ошибкой; нет random exception, бесконечного ожидания или частичной награды. Missing optional CameraShake не блокирует event. Missing station/модули делает запуск недоступным с localized reason; отсутствующие обязательные production reward owners считаются configuration failure до начала, preview lab их не требует.

На Node разрешены authored circular areas с uniform world XY scale; nonuniform/degenerate scale circle отклоняется validation. Body detector отдельно проверяется для rotated/nonuniform sprite transforms. Amount multiplication вычисляется через long и ограничивается int.MaxValue; отрицательного Gold от overflow не бывает.

## 6. Prefab hierarchy и presentation

`Assets/_Project/prefabs/Environment/WorldEvents/OrbitalRelayEvent.prefab`:

```text
OrbitalRelayEvent
  OrbitalRelayEvent (config, node[], presentation, pressure references)
  OrbitalRelayPresentation
  WorldEventPressureModifier
  ArenaBounds (CircleCollider2D trigger; radius 9; interaction/placement footprint)
  EventMarkerAnchor
  RewardAnchor
  Nodes
    Node A / Node B / Node C (OrbitalRelayNode)
      ContactArea (CircleCollider2D trigger; world radius 0.35)
      Core (SpriteRenderer)
      ActiveGlow (SpriteRenderer + authored pulse animation)
      ContactProgress (authored fill SpriteRenderer)
      ActivationFX (authored ring/flash Animator)
  TransitionFX
    Shockwave (authored closed LineRenderer geometry + Animator)
    FlashOverlay (authored UGUI Image, raycastTarget=false)
  PresentationCanvas (authored Canvas/CanvasScaler; no EventSystem)
    RelayPanel
      PhaseLabel / TimeLabel / ActivationsLabel / GoldLabel / ComboLabel
    StabilizedBanner (TMP + authored animation)
```

Positions drawn from tested lab layout: A=(4.51,0), B≈(-3.08,5.335), C≈(-2.255,-3.906). Different radii preserve positioning demand. Collection size is authored; controller doesn't generate clones or reposition nodes. Footprint validated against site/playable area before placement.

Animations/materials for relay live in `Assets/_Project/art/FX/OrbitalRelay/`; serialized geometry, clips and materials exist before play. Runtime only enables, changes color/fill/scale, plays clips and updates text; no new GameObject/CreatePrimitive/Mesh construction, no event-specific per-launch LineRenderer vertices. Editor authoring tool may create prefab assets once, but is not a runtime dependency.

Reuse project TMP font/material, HUD panel palette, notification service and CameraShake; no OnGUI in production. UI is passive (no input stealing), anchored so it doesn't cover reward panel or tactical map. Stabilization: `STABILIZE`, X/required and time. Transition: large `STABILIZED`, flash/pulse, center shockwave, synchronized Node flashes; optional local camera shake. Bonus: `BONUS PHASE`, TIME, ACTIVATIONS, GOLD, COMBO; last 5 seconds urgent color/pulse. Final text uses existing notification/presentation, Upgrade selection uses existing reward panel only.

Localized keys: `event.relay.marker`, `.name`, `.description`, `.stabilize`, `.stabilized`, `.bonus`, `.time`, `.activations`, `.gold`, `.combo`, `.failed`, `.upgradeNotEarned`, `.upgradeEarned`, `.unavailable`, `.retry`; RU and EN with format parameters, no lab-debug strings. Tutorial event instruction becomes localized active-Node/orbital guidance; earlier tutorial instructions are not rewritten.

## 7. Runtime contact API

Keep mounted-body intent of the current helper, not `HitTest`'s padded mount-center fallback. Introduce `OrbitalModuleRuntime.HasBodyContact(Vector2 center, float radius)` and internal presentation method using `OrbitalBodyContactDetector.IntersectsCircle(SpriteRenderer body, Vector2 center, float radius)`.

Pure geometric core transforms authored sprite-local rectangle corners into world XY and measures circle distance to the transformed quadrilateral: containment or minimum point-to-edge distance. Handles rotation, nonuniform scale and parent transforms without world-AABB false positives. Reject invalid radius/body data; never allocates objects per query. The caller retains mounting, active body, drag/preview and halo exclusions. This works in normal player build and doesn't touch editor-only APIs.

Old `TouchesCircle` entrypoints are removed with the lab gameplay caller; no redundant detector/compat wrapper. Test rotated narrow body, circle offset/corners, nonuniform scale, disabled sprite, detached module, preview, drag and halo exclusion. Compile runtime with neither UNITY_EDITOR nor DEVELOPMENT_BUILD to prove API availability.

## 8. Временное давление врагов

Add `WorldEventSpawner.AcquireSpawnPressure(WorldEvent source, float multiplier): IDisposable`. Lease validates active event ownership; handles are scoped to event/token and idempotent. Effective event pressure is base event pressure multiplied by live lease multipliers; ordinary Events retain existing 1.15 base, Relay opts out of standard event boost (base=1).

`WorldEventPressureModifier` acquires exactly once on Bonus entry and disposes on Bonus exit/terminal/disable/destroy. Dispatcher additionally clears event leases at NotifyEventCompleted/Failed, cancel/debug-clear, ReleaseRunScene and OnDisable; stale handles cannot reset pressure owned by a newer event. EnemySpawner's existing SetWorldEventSpawnPressureMultiplier is the only application point; world-rule/threat acceleration factors remain independent.

Only spawn rate is modified in this migration; no new cap system is required. Default Bonus rate multiplier=1.6. If spawner is stopped for tutorial, reward pause or existing threat policy, modifier does not force it on. No local event spawner, Instantiate enemies or spawn profile mutation. Reset releases leases before any subsequent event; optional absence of enemy pipeline logs a diagnostic and doesn't break completion.

## 9. Rewards: одна выдача и общий UI

`WorldEvent.CompletionReward` returns null for current Events and a typed `WorldEventRewardResult(Gold, UpgradeSelections)` for successful Relay. Pure calculation: `Gold = bonusActivations * goldPerActivation`; `UpgradeSelections = stabilized ? 1 : 0`. Failed event emits failure notification, no completion reward. Qualification occurs at Stabilization but actual queue request waits until bonus completion (or live gameplay cancel after qualification).

`WorldEventSpawner` snapshots custom result before destruction, clears pressure, delivers result once, then publishes existing EventCompleted. Relay-specific completion does not instantiate EventRewardContainer; generic other Events keep current behavior.

- Gold uses `CurrencyManager.AddGoldExact` once, before queue/UI. Development runs retain existing persistent-currency suppression. Lab preview never changes saved gold.
- Upgrade uses `UpgradeManager.ShowChestRewardChoices(3, false, onClosed)` once: three offered cards mean one chosen Upgrade, not three grants. Existing queue, placement/cancel-to-cards and transition barrier own the rest. No new reward UI/queue.
- `suppressStandardReward` for site means suppress old container; Relay custom reward still applies. `ProductionAnomalySite` skips its additional normal selection for a custom-reward event and calls CompleteSite through existing RunWhenRewardQueueIsIdle. Special-site two-event progression and final Ring remain existing site rewards; Relay's per-event Upgrade does not replace the Ring. Other event site behavior remains unchanged.
- Tutorial now intentionally receives the same single completion Upgrade; the previous test expectation of zero additional event rewards is replaced. Exit is gated by event completion and existing reward queue barrier.
- Debug `suppressReward=true` suppresses all delivery, but computes and exposes the same result for presentation. False allows ordinary queue behavior, subject to development-run currency protection.

Global Mission/Run completed-event signal and SourcePrefab reporting remain the existing generic callback. Failure/cancel before qualification never reports a successful event. Currency unavailable or UpgradeManager unavailable blocks production start rather than silently paying half a reward. Dispatcher re-entry after notifications cannot double-pay.

## 10. Tutorial, bot и tests

Tutorial's earlier movement/kill/XP/first placement steps stay intact. Target type becomes OrbitalRelayEvent. Entry into ArenaBounds at SectorGoal may still auto-start via PlayerEntered; entering is just a trigger, not progress. Once started, FocusTarget tracks ActiveNode position; text tells player to touch the glowing core with mounted weapon, reposition and use RMB compression.

`ProductionExplorationSectorController` places the same prefab using its full collider footprint and remembers the tutorial placement. New `TryRespawnTutorialRelay(out OrbitalRelayEvent relay)` reuses that placement after failure with 2-second delay, while sector is active and no target is pending. Failed target subscriptions are detached, new target subscribed once; player does not repeat movement/XP/tutorial reward steps. Step returns to SectorGoal, then FirstEvent; no premature Exit. Bonus/transition are the same timers as production; no tutorial legacy mode, relaxed detector or instant stabilization.

After Completed and reward queue idle, GoalCompleted/exit flow proceeds normally. Current tutorial EnemySpawner safety stop is preserved: event modifier must not bypass TutorialController.IsTutorialSector spawn suppression. No unrelated tutorial combat changes.

`BotController` replaces CaptureZone-specific center-circling with a small read-only movement steering helper `Dev/Debug/GoldenPathLab/OrbitalRelayBotSteering.cs`. It consumes active Node and actual mounted module world position to propose player movement that keeps that module body at the Node; existing obstacle/enemy/exit safety scoring remains. Transition waits/evades; Bonus resumes Node chasing; failed event is reacquired through current selector. No Score setters, forced contact, timer skips, direct health mutation or bypassed reward selection. Tutorial tests can use this same intent helper; they verify actual contact via runtime detector, phases, retry and real shared Upgrade selection.

Critical automated checks are small named fixtures, not whole Core/Golden Path. Existing full tutorial test is migrated but not executed as part of the cheap validation. A short local relay-only smoke can use production WorldSystemsLab with small temporary debug config asset; shipped defaults and original asset remain untouched.

## 11. WorldSystemsLab

Scene event list and authoring both reference `prefabs/Environment/WorldEvents/OrbitalRelayEvent.prefab`; remove CaptureZone→lab prefab substitution. WorldSystemsLabController retains other Events and only delegates ORBITAL bootstrap/spawn to WorldSystemsLabRelayAdapter. Production presentation renders phases/score/result; old relay OnGUI HUD and result-formula duplication are deleted.

Adapter ensures temporary RunState/ORBITAL player setup using existing lab character. If manual enemy pressure testing is enabled, it bootstraps one scene-level EnemySpawner with ordinary debug fixed pressure and binds that instance to existing WorldEventSpawner pipeline; it does not change rate at phase transitions. Phase changes come only from production pressure leases. Adapter owns cleanup of its temporary enemies/run, and disables this bootstrap for other Events. Existing CorridorV2 spawner/control remains untouched and stopped through current ClearEvents path.

Preview rewards are default; optional lab reward-flow check bootstraps/binds existing authored UpgradePanelView and UpgradeManager UI dependencies, not a custom panel. It uses a temporary development run with no persistent currency writes. Missing bootstrap dependency produces a clear lab diagnostic rather than a false claim of reward verification.

## 12. Deletions и границы cleanup

Delete with corresponding .meta after reference migration:

1. CaptureZoneEvent.cs.
2. CaptureZoneVisual.cs (including CaptureZoneCompletionVisual).
3. CaptureZoneEvent.prefab.
4. M_CaptureZone.mat and CaptureZone.shader, after exhaustive GUID check.
5. Dev/Debug/WorldSystemsLab/OrbitalHoldZoneEvent.cs.
6. Dev/Labs/WorldSystemsLab/OrbitalHoldZone.prefab.
7. Old localization entries only if exact key no longer referenced.

Remove old hold fields, bot radius behavior, lab renderer construction, lab reward/HUD state and authoring replacement mapping. Preserve common WorldEventMarker, loot container/profile, shared orbital visuals, pressure/threat owners, site ring reward and unrelated Events. Replace historical current-navigation entries where applicable; do not delete shared configs merely because their names mention Events.

## 13. Приёмка и проверки

- Editor/runtime compile: zero errors; release-runtime compile explicitly without editor/development defines. Ordinary generated `.csproj` is not hand-edited or committed.
- Targeted model/contact/lease/reward tests for the contracts described above. No Golden Path, Batch Runner, full Core or long tutorial integration run.
- Unity prefab import inspection: no MissingScript/invalid serialized references, collection>=2, all required authored visuals/colliders/config linked, no Dev asset dependency in production relay.
- Registry/GUID scan: MVP/D1/D2/lab point only at new prefab; no old scripts/classes/runtime localization/asset GUID usages.
- Short local production flow: Stabilization → Transition → Bonus → queue Upgrade + exact Gold policy → clean pressure, plus fail/retry and explicit reset check. Lab preview and delivery checks clearly distinguished.
- Other Events' standard reward path and standard pressure tested narrowly; no changes in their prefab/config.
- Final report lists architecture, modified/new/deleted files, production/lab start, pressure/rewards, authored replacement and consciously preserved adjacent systems.

## 14. Decisions for review

These are intentional concrete choices, not implementation placeholders: first-phase timeout stays 20 seconds; gold uses exact payout; combo restarts in Bonus; normal-site duplicated Upgrade is removed for custom result only; special-site Ring stays; tutorial receives one normal completion Upgrade; administrative teardown does not open rewards; pressure changes rate only, not cap. The plan implements these choices unless the reviewer changes them.
