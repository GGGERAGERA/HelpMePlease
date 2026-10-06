# Corridor V2 Production Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Заменить legacy production Corridor проверенной механикой V2 с независимыми anomaly territories и единственным runtime для production и Lab.

**Architecture:** Декомпозиция WorldEvent adapter, immutable route, state/crossing/collapse/strikes и prefab presentation. Placement допускается до предложения события; site objective completion отделяется от retirement среды. GUID production event prefab и перенесённых serialized assets сохраняются.

**Tech Stack:** Unity/C#, ScriptableObject, Physics2D collider overrides, существующие WorldEvent/reward queues/RocketAttackRunner, Unity Test Framework.

**Spec:** [2026-10-07-corridor-production-migration.md](../specs/2026-10-07-corridor-production-migration.md). Перед исполнением прочитать оба документа.

## Global Constraints

**Execution override, approved by user 2026-10-07:** spec and plan are approved for implementation. Preserve architecture and task order, implement first, then five compact fixtures: route/placement; progression; collision/hazards; anomaly/site lifecycle; production assets/dependencies/import/player compilation. Reuse existing tests. One short production smoke and one WorldSystemsLab smoke replace the matrices below. Per-task RED/GREEN, Golden Path, Batch Runner, full tutorial and full preset/rotation/outcome matrices are not run. This override supersedes the original verification steps; it does not change gameplay contracts.

- Этот документ — план, сейчас не исполнять. Начать только после подтверждения spec + plan пользователем.
- Не запускать Golden Path / Batch Runner. Проверки — отдельные named tests и короткие manual scenarios.
- Сохранять существующие незакоммиченные Lab изменения; не выполнять reset/clean и не переносить их в чужую задачу.
- Production не ссылается на Dev; Dev содержит adapters/controls/tests/Editor authoring.
- Три gates, width `8`, segment `35`, exit `24`, cardinal rotations; Straight/L/Zigzag и deterministic Random сохранены.
- Anomaly mechanics/visuals/collider сохраняются после Corridor outcome; owner reset среды остаётся независимым.
- Нет player teleport на production completion, новых rewards, global physics matrix edits, procedural visual/UI construction и финального legacy fallback.
- Strikes `nonLethal: true`; collapse damage lethal. Finishing `.45s`, смерть до success — Failed.
- Assets переносить с `.meta`; legacy production prefab GUID `e47c3f95a8a34c18a619b70ec785f142` сохранить.

## Review Focus

- Route не помещается или перекрыт prop: offer отвергается до публикации, игрок/props/arena не перемещаются — task 3.
- Corridor первый из двух special-site objectives: preserve latch переживает вторую цель и reward callback — task 4.
- Death/disable/reset при timeScale=0 и pending rockets: cleanup ровно один, без позднего reward/retry — tasks 6–7.
- Player на Default layer и conflicting actor overrides: walk/dash block, enemy pass — task 5.
- Bot достигает gate center или пересекает Exit раньше открытия: progression не подменяется, требуется forward crossing — tasks 2, 9.

## Файловая карта и способ проверок

Обозначения в задачах разворачиваются в точные корни:

| Обозначение | Корень |
|---|---|
| R | `Assets/_Project/scripts/World/Events/Corridor/` |
| W | `Assets/_Project/scripts/World/Events/` |
| E | `Assets/_Project/scripts/Run/Exploration/` |
| P | `Assets/_Project/prefabs/Environment/WorldEvents/Corridor/` |
| D | `Assets/_Project/Data/WorldEvents/Corridor/` |
| L | `Assets/_Project/Dev/Debug/WorldSystemsLab/` |
| T | `Assets/_Project/Dev/Tests/Core/Editor/` |

Все Create/Move/Delete включают соответствующие `.meta`. PlayMode cases добавляются в существующее test assembly с режимом выполнения, поддерживающим UnityTest; при отсутствии такого assembly выделить маленькое `Assets/_Project/Dev/Tests/Corridor/PlayMode/` assembly, Editor-only tests туда не переносить.

Named checks ниже запускать через Unity Test Runner только с указанным class/method filter. Red перед реализацией: ожидаемое assertion/отсутствующий контракт; Green после: все cases указанного filter PASS. Compile/import errors не считать успешным red proof. Никаких длинных runners. При тестах ресурсов использовать serialized production assets, не test-only geometry, повторяющую implementation.

Каждая задача заканчивается review diff и записью фактической проверки. Коммиты здесь не предписаны: не включать предыдущие Lab изменения автоматически; согласованная стратегия commits нужна только при запросе интеграции.

При GUID-preserving rename сразу обновлять типовые references существующих Dev consumers: перенос serialized типа не должен ломать компиляцию Lab между задачами. Поля prefab/SO сохранять по имени либо явно мигрировать через FormerlySerializedAs. До task 10 старый Lab runtime временно остаётся исходным потребителем новых data/view типов; это staging, а не финальный compatibility fallback. Новый production prefab публикуется только в task 8.

### Task 1. Production route data и immutable geometry

**Files:** Create `R/CorridorConfig.cs`, `R/CorridorRoute.cs`, `R/CorridorRouteBuilder.cs`. Move `L/CorridorV2RouteDefinition.cs` → `R/CorridorRouteDefinition.cs`, сохранить meta, снять Dev guard. Move три assets из `Dev/Labs/WorldSystemsLab/CorridorRoutes/` → `D/Routes/CorridorStraight.asset`, `CorridorL.asset`, `CorridorZigzag.asset`. Create `D/CorridorConfig.asset`, `T/CorridorRouteTests.cs`. Modify `Dev/Editor/WorldSystemsLab/CorridorV2RouteAuthoring.cs`, `T/CorridorV2RouteAuthoringTests.cs`, `L/CorridorV2Settings.cs`, `L/CorridorV2Route.cs`, `Dev/Editor/WorldSystemsLab/WorldSystemsLabAuthoring.cs` только для moved definition type/enum bindings.

**Interfaces:** `CorridorRouteMode { Straight, L, Zigzag, Random }`; `CorridorRouteBuilder.TryBuild(config, mode, seed, quarterTurns, start, out route, out error)`; `route.Sample(float)`, `Project(Vector2)`, immutable Vertices/GatePlanes/CumulativeDistances/ExitPosition. `CorridorConfig.TryValidate(out string error)`.

- [ ] Добавить route tests: `SameSeedProducesSameVertices`; `CardinalRotationPreservesDistances`; `PresetHasThreeOrderedGatesAnd129Length`; `MissingDefinitionRejectsInsteadOfGeneratingPreset`; `RandomBudgetFallbackIsDeterministic`. Проверять один seed `42`, четыре rotations, start `(7,11)`, длину `3*35+24`, три gates, отсутствие self-overlap.
- [ ] Запустить filter `CorridorRouteTests`, зафиксировать ожидаемый red.
- [ ] Извлечь pure V2 builder/math; убрать mutable Completed из route. Сохранить budget `2048` и Random algorithm fallback; перенести Populate defaults в Editor authoring. Создать production config с tuning spec §5/7.
- [ ] Config на этом шаге содержит routes/tuning/shared rocket refs; поле нового CorridorKit добавить в task 5, когда его тип создан. Preset enum serialized numeric values сохранить; Dev settings явно адаптировать к moved definition type без runtime зависимости production от Dev enum.
- [ ] Перенести definitions/assets с meta, обновить authoring destinations, исключить runtime Editor API/Dev random.
- [ ] Запустить `CorridorRouteTests` + `CorridorV2RouteAuthoringTests`: PASS; импорт route assets без missing script. Проверить diff GUID и отсутствие изменения исходных anchors.

### Task 2. Ordered progression, Exit и explicit state

**Files:** Create `R/CorridorRuntimeState.cs`, `R/CorridorGate.cs`, `R/CorridorExit.cs`, `R/CorridorNavigationSnapshot.cs`, `T/CorridorProgressionTests.cs`.

**Interfaces:** `CorridorPhase { Inactive, Running, FinalPush, Finishing, Completed, Failed, Cancelled }`; state `Start()`, `TryAdvance(route, previous, current)`, `Tick(deltaTime)`, `TryBeginFinish(previous,current)`, `Terminate(CorridorPhase)`; gate/exit crossing signatures из spec. State хранит next ordinal и ExitOpen.

Snapshot и `StrikeThreat` — immutable data contracts из spec §3: route/phase/next ordinal/objective/ExitOpen/front/readonly threat footprints и времена. Здесь threats могут быть пустыми; task 6 заполняет их, не меняя интерфейс presentation.

- [ ] Добавить `OnlyNextForwardGateAdvances`, `GateCenterDoesNotAdvance`, `ReverseAndSkippedGateDoNotAdvance`, `LongInvalidSweepDoesNotAdvance`, `ExitRequiresThreeGatesAndFinalPush`, `DeathDuringFinishFails`. До `3.5s` Exit false; после true; completion после `.45s`, смерть перед completion → Failed.
- [ ] Запустить filter `CorridorProgressionTests`, red.
- [ ] Перенести swept crossing/route containment `.25`/max move `12`, один следующий gate за tick. Убрать progress из view и route; оформить transitions spec §4.
- [ ] Запустить тот же filter: PASS. Review не должен обнаружить setters progression для bot или completion от elapsed survival timer.

### Task 3. Whole-route admission до spawn offer

**Files:** Create `W/WorldEventPlacementContext.cs`, `R/CorridorPlacement.cs`, `T/CorridorPlacementTests.cs`. Modify `W/WorldEvent.cs`, `W/WorldEventSpawner.cs`, `E/ProductionExplorationSectorController.cs`, `E/ProductionAnomalySite.cs`.

**Interfaces:** `WorldEvent.TryPreparePlacement(context,out error)` default true. Context содержит Origin/PlayableArea/optional SiteStartBounds/Seed и `IsStaticFootprintClear(Rect)` callback. `CorridorPlacement.TryPrepare(config,context,mode,seed,quarterTurns,bool explicitSelection,out route,out error)`; результат frozen до Interact. Spawner возвращает false без started/completed notifications при отказе preparation.

- [ ] Добавить `Straight129Rejects100Square`, `RotatedWholeFootprintFits`, `SiteConstrainsStartOnly`, `AnomalyTriggerDoesNotBlock`, `StaticPropRejectsWithoutDeletion`, `ExplicitSeedIsNotSubstituted`, `DefaultRandomAdmissionIsBoundedAndRepeatable`, `DefaultHookLeavesOtherEventsUnchanged`.
- [ ] Запустить `CorridorPlacementTests`, red.
- [ ] Вызвать preparation после assignment site context, до successful registration/marker. Проверять segment/corner/cap/wall rectangles целиком; использовать existing static-clearance/sector-exit rules, а не origin-circle как route footprint. Ordinary Random максимум 16 candidates; explicit selection reject без замены.
- [ ] Предусмотреть start revalidation для новых blockers: withdraw offer/no reward, owner retry; никакого relocation после Interact. Site retries и guaranteed selections не должны зависать на недоступном Corridor: bounded placement failure с диагностикой и preflight guaranteed config.
- [ ] Запустить filter: PASS. Review логов: mode/effective seed/rotation/start/reason; нет пропуска guaranteed objectives, увеличения PlayableArea или удаления props.

### Task 4. Независимое environment lifetime при site completion

**Files:** Create `W/SiteEnvironmentCompletionPolicy.cs`, `T/CorridorSiteLifecycleTests.cs`. Modify `W/WorldEvent.cs`, `E/ProductionAnomalySite.cs`; при необходимости consumers environment predicates в `E/ProductionExplorationSectorController.cs` и `scripts/UI/HUD/TacticalMapHUD.cs`.

**Interfaces:** `WorldEvent.EnvironmentCompletionPolicy` default CollapseWithObjective. Site ObjectiveCompleted отдельно от EnvironmentAlive; `CompleteSite(SiteEnvironmentCompletionPolicy)` private, policy snapshot/latch до asynchronous callbacks. Public existing site completion semantics сохраняются для sector progress.

- [ ] Добавить `DefaultSiteStillCollapses`, `PreservedSiteKeepsZoneEffectsAndBoundary`, `NormalRewardCallbackCompletesObjectiveOnce`, `SpecialFirstObjectivePreserveSurvivesSecondSuccess`, `NeighborAnomalyNeverReceivesCleanup`, `OwnerResetRetiresPreservedEnvironment`. Проверять active-zone registration, collider/visual enabled, completion counts и отсутствие второго reward request.
- [ ] Запустить filter `CorridorSiteLifecycleTests`, red.
- [ ] Разделить completion и CollapseEnvironment; сохранить normal choices/special ring quantities/owners и queue barriers. Preserve latch действует на всю special chain; objective complete запрещает новые цели/assault, environment update/map/focus продолжаются.
- [ ] Запустить filter: PASS; дополнительно targeted существующие site/reward tests, найденные по consumers изменённых predicates. Не добавлять Corridor-type special-case в anomaly runtime.

### Task 5. Production Kit, authored views и wall overrides

**Files:** Move `L/CorridorV2Kit.cs`, `CorridorV2NodeView.cs`, `CorridorV2HudView.cs` → `R/CorridorKit.cs`, `CorridorNodeView.cs`, `CorridorHudView.cs`. Create `R/CorridorPresentation.cs`, `T/CorridorPresentationTests.cs`, `T/CorridorCollisionTests.cs`. Move восемь module/HUD prefabs из `Dev/Labs/WorldSystemsLab/CorridorKit/` в `P/` с теми же именами; Kit SO → `D/CorridorKit.asset`; `Art/*` → `Assets/_Project/art/WorldEvents/Corridor/`. Modify `L/Editor/CorridorV2KitAuthoring.cs`, `T/CorridorV2StructuralTests.cs`.

Также Modify `R/CorridorConfig.cs`, `D/CorridorConfig.asset`, `L/CorridorV2Presentation.cs`, `L/CorridorV2Lab.cs`, `L/WorldSystemsLabController.cs`, `Dev/Editor/WorldSystemsLab/WorldSystemsLabAuthoring.cs`, `Dev/Labs/WorldSystemsLab/WorldSystemsLab.unity` для moved view/Kit types. Удалить `eventPrefab` из production Kit сразу: это presentation data, root принадлежит catalog/adapter. До task 10 existing Dev consumer хранит прямую serialized reference на свой текущий Lab event prefab; production assets не ссылаются на него.

**Interfaces:** `Presentation.Bind(route,kit,playerColliderMask)`, `Render(snapshot)`, `Dispose()`; views `SetPhase(...)` без progression, HUD `SetData(...)` без runtime layout. Kit содержит только production assets.

- [ ] Добавить asset tests `KitHasAuthoredGameplayAndPresentationChildren`, `NoWorldTmpCheckpointLabels`, `NoProductionAssetDependsOnDev`, `DisposeDisablesOwnedCollidersAndKeepsForeignZone`.
- [ ] Добавить короткий collision UnityTest `DefaultLayerPlayerWalkAndDashBlockedEnemyPasses` на реальных player/enemy prefab collider shapes, четыре rotations; `ActorMaskConflictRejectsBinding`.
- [ ] Запустить named filters, red. Перенести assets/types с meta; адаптировать bindings и authoring paths. Instantiate существующую hierarchy, сохранить tiled pixel density и phase membranes spec §5.
- [ ] Применить include player mask / exclude inverse / priority100; не менять Physics2D matrix. Проверить наличие enemy/player layer separation, не использовать sorting-layer имя.
- [ ] Запустить filters: PASS; коротко визуально проверить production Kit в Play Mode. Shared `M_LaserLine`/pixel assets сохраняются, Art SVG+PNG не теряют import settings.

### Task 6. Collapse и shared strikes

**Files:** Create `R/CorridorCollapse.cs`, `R/CorridorStrikes.cs`, `T/CorridorHazardTests.cs`. Consumes snapshot/StrikeThreat task 2 и existing `scripts/Combat/Enemies/RocketAttackRunner.cs`, `EnemyExplosion.cs`; их копии не создавать.

**Interfaces:** Collapse Tick/TryPressure из spec; Strikes Tick/Dispose, readonly `StrikeThreat` footprints/time; snapshot route/phase/target/front/ExitOpen/threats. Tuning читается только из CorridorConfig snapshot.

- [ ] Добавить `FrontDoesNotResetAtGate`, `PressureIntervalsAndFinalPushSpeed`, `PatternsPreserveCenterGapAndOrder`, `StrikeDamageCannotKillButCollapseCan`, `DisposeReleasesDetachedWarningsRocketsAndImpacts`, `CancelDuringDamageCallbackIsSafe`.
- [ ] Запустить `CorridorHazardTests`, red.
- [ ] Перенести V2 math/scheduler и точные tuning spec §7: speed5, damage8/1s minimum.65, final×1.25; strikes3.2/1.4/.35/10/radius1.5/chase.4, final interval×.75. Передать runner nonLethal true явно, combatAllowed от run/death state.
- [ ] Dispose runner при любом terminal outcome, включая finishing entry cancellation, не ограничиться Destroy parent.
- [ ] Запустить filter: PASS; проверить первые тихие segment/pending-pattern gating и отсутствие enemy damage от shared explosion.

### Task 7. CorridorEvent adapter, death и reward integration

**Files:** Create `R/CorridorEvent.cs`, `T/CorridorLifecycleTests.cs`, `T/CorridorRewardTests.cs`. Modify `scripts/Combat/Player/PlayerHealth.cs`, `E/ProductionAnomalySite.cs`; existing WorldEvent hooks использовать, не переписывать общий reward system.

**Interfaces:** `PlayerHealth` public event `Action Died` после установки dead flag; CorridorEvent override placement/lifecycle/cancel/config validation/EnvironmentCompletionPolicy/RewardPosition; `CompletionReward` остаётся null. Prepared route передаётся task2/5/6. Один DisposeOwned path с terminal notification mode.

- [ ] Добавить `ExitKeepsPlayerPositionAndEndpointReward`, `CompleteNotifiesOnce`, `DeathAtZeroTimeScaleReleasesWallsAndShots`, `InactiveDeathWithdrawsOffer`, `FailCancelDestroyRootWithoutReward`, `ResetUnloadNoResultNoRetry`, `DisableCancelsOnce`, `DeathDuringFinishNoReward`.
- [ ] Добавить reward cases ordinary container endpoint / normal-site choices / special two objectives ring; counts совпадают со старой policy, explicit reward не появляется.
- [ ] Запустить named filters, red. Собрать тонкий adapter; записывать policy/endpoint до cleanup; подписка Died с immediate already-dead check. Site retry после death запретить при stopped run; пользовательский cancel живого run сохраняет existing failed-event retry behavior.
- [ ] Сохранить Finishing `.45s`, SFX pulse/cascade; production не вызывает Lab teleport/revive/arena expansion и не обращается к UpgradeManager напрямую.
- [ ] Запустить filters: PASS. Проверить complete/fail/cancel/reset/unload/reentrant Destroy порядок, отмену async hazard callbacks и unsubscribe death.

### Task 8. Production prefab / catalog cutover и localization

**Files:** Move `prefabs/Environment/WorldEvents/EvacuationCorridorEvent.prefab` → `P/PF_CorridorEvent.prefab` с прежним GUID; rewrite hierarchy/component на CorridorEvent/config. Modify `Dev/Editor/Authoring/SurfaceMapProductionAuthoring.cs`, `Data/Localization/LocalizationTable.asset`; проверить сохранённые refs `Scenes/MainBuild/MVP.unity`, `Data/SurfaceMap/Sector_D1.asset`, `Sector_D2.asset`. Create `T/CorridorCatalogTests.cs`.

**Interfaces:** Catalog получает CorridorEvent с AllowedInSite=true и production config. В authoring weight проверка использует новый CorridorEvent (либо уже существующий stable identity, если assigned); не оставлять старый type. Сохранить multiplier2 и eventFrequency1.5, не придумывать новый eventId, влияющий на RunConfig selection.

- [ ] Добавить `MvpAndSectorsResolveProductionPrefab`, `SectorWeightsRemainUnchanged`, `PrefabHasValidProductionConfig`, `LocalizedGuidanceContainsNoLegacySafeRectangleInstruction`.
- [ ] Запустить filter red; переместить prefab/meta, перебиндить m_Script/serialized config, убрать legacy fields. Update authoring type-check; не переписывать sector GUIDs на другой prefab.
- [ ] Локализовать HUD/progress/FinalPush/Exit; сохранить name/description keys и обновить описание. Старый marker key удалять только при zero usage.
- [ ] Запустить filter: PASS, Unity import без Missing MonoBehaviour. Короткий MainMenu → gameplay → offer/start сценарий: frozen prepared route, gates по порядку, Exit position, награда через existing owner.

### Task 9. Honest bot и generic tutorial guidance

**Files:** Create `R/ICorridorNavigation.cs`, `W/IWorldEventObjectiveProvider.cs`, `T/CorridorBotNavigationTests.cs`, `T/CorridorGuidanceTests.cs`. Modify `R/CorridorEvent.cs`, `Dev/Debug/GoldenPathLab/BotController.cs`, `scripts/Run/Tutorial/TutorialController.cs`; existing `T/Subject42TutorialTests.cs` сохранить.

**Interfaces:** `ICorridorNavigation.GetNavigationSnapshot()` read-only. `IWorldEventObjectiveProvider.GetObjectiveGuidance()` возвращает keys/target/progress. Event emits generic Target marker только для следующей цели; full map route renderer не добавлять.

- [ ] Добавить `BotFollowsCornerInsteadOfCuttingChord`, `BotCrossesGateBeyondPlane`, `BotWaitsEntrySideUntilExitOpen`, `BotAvoidsThreatAndFrontWithoutProgressWrites`, `RelayTutorialRemainsTargeted`, `CorridorGuidanceChangesWithObjective`.
- [ ] Запустить filters red; адаптировать bot через normal intents, gate target +1/tolerance.25, route sampling и threat-safe point. Не менять state напрямую, не teleport/heal.
- [ ] Подключить guidance capability без замены OrbitalRelay tutorial target/setup. HUD получает localized values presenter/event adapter; no concrete legacy dependency.
- [ ] Запустить named filters + `Subject42TutorialTests`: PASS. Короткие steering сценарии Straight/L/Zigzag/Random с seed42, без Golden Path/Batch Runner; фиксировать реальные crossing outcomes.

### Task 10. Lab становится production consumer

**Files:** Replace `L/CorridorV2Lab.cs` → `L/CorridorLab.cs`; Modify `L/WorldSystemsLabController.cs`, `Dev/Editor/WorldSystemsLab/WorldSystemsLabAuthoring.cs`, `Dev/Labs/WorldSystemsLab/WorldSystemsLab.unity`, `Dev/Labs/README.md`, `T/OrbitalRelayRuntimeTests.cs`. Adapt `T/CorridorV2Tests.cs` → production test fixtures; source-path structural checks task5 обновить до нового runtime.

Adapter переключает свою прямую event prefab reference на production root отдельно от presentation Kit. Production dependency check покрывает Kit рекурсивно, включая все nested serialized references.

**Interfaces:** Lab adapter запускает тот же PF_CorridorEvent через existing dev spawner hooks, config overrides только для admission. F5 start; F6 mode; F7 quarterTurns; F8 enemies; F9 independent anomaly. Production config/assets — единственные источники route/kit.

- [ ] Добавить `LabUsesProductionPrefabAndConfig`, `RelayAndCorridorHudAreExclusive`, `F9ZoneSurvivesCorridorCancel` (targeted asset/PlayMode cases).
- [ ] Запустить named filters red; заменить Lab gameplay wiring. Только adapter может teleport/heal/expand lab bounds; F9 не входит в owned Corridor tree.
- [ ] Удалить Lab root prefab `Dev/Labs/WorldSystemsLab/CorridorKit/PF_CorridorV2Event.prefab` после обновления Kit/scene refs. Editor builders не создают gameplay копию в Dev.
- [ ] Запустить named cases + `OrbitalRelayRuntimeTests`: PASS; короткая Lab F5–F9 проверка без batch. README описывает production runtime и dev controls.

### Task 11. Legacy deletion и финальная ограниченная verification

**Files:** Delete `W/EvacuationCorridorEvent.cs`; `art/mat/WorldRules/M_EvacuationCorridor.mat`; `Shaders/EvacuationCorridor.shader`; `L/CorridorV2Event.cs`, `CorridorV2Route.cs`, `CorridorV2Settings.cs`, `CorridorV2Strikes.cs`, `CorridorV2Presentation.cs` после замены consumers. Удалить пустые moved source asset folders/metas. Modify `scripts/UI/HUD/ITacticalMapMarkerProvider.cs` только для доказанно unused Corridor enum; localization только zero-usage legacy key. Сохранить все shared assets/runtime/tests.

- [ ] Перед каждым delete выполнить `rg -n` по symbol и GUID в Assets/ProjectSettings; записать ожидаемые оставшиеся owners либо zero refs. Проверить old prefab/script/material/shader GUIDs из spec §1; prefab GUID должен остаться в новом path и MVP/D1/D2, а не стать zero.
- [ ] Удалить legacy runtime, procedural geometry и Dev duplicate implementation; helpers/materials без доказанного exclusive ownership не удалять. Финал не содержит old compatibility paths.
- [ ] Повторить `rg -n 'EvacuationCorridorEvent|CorridorV2Event|CorridorV2Route|CorridorV2Settings|CorridorV2Strikes|CorridorV2Presentation' Assets/_Project`: zero runtime/serialized usages; допустимы только историческая документация, не live tests/authoring. Production dependency tests через AssetDatabase и исходные imports не находят Dev paths/types.
- [ ] Запустить только созданные named Corridor filters и затронутые existing Relay/site tests; Unity import/compile clean. Один non-development player compilation check подтверждает отсутствие Dev dependencies; это compilation, не Golden Path/Batch Runner.
- [ ] Короткая production Play Mode matrix: all four rotations, ordered gates, locked/open Exit, physical wall walk/dash/enemy, patterns/front, complete/fail/cancel/death/reset. Сравнить конкретную anomaly zone до и после outcome: same instance/active collider/visual/effect; проверять также site-owned Corridor и special chain. Player world position после success остаётся у Exit.
- [ ] Итоговый review: сравнить каждый критерий spec §10 с записанным доказательством; проверить reward counts, preserved GUIDs, zero Dev dependencies, owned pool/module leaks. Если критерий не подтверждён — описать блокер, не объявлять migration готовой. Остановиться без merge/deploy, если они отдельно не запрошены.

## Проверка полноты плана

### Execution evidence — 2026-10-07

Tasks 1–11 implemented in the existing checkout, preserving serialized GUIDs and the uncommitted Lab baseline. Verification follows the approved economy override above.

- Five-fixture run: **5 passed / 0 failed** (`Artifacts/GeneratedQA/CorridorMigration/five-groups-results.xml`, 01:19 Moscow). Covers route/placement, progression, collision/hazards, independent anomaly outcomes and actual normal-site reward commit/lifecycle, production catalog/dependencies/legacy references.
- Final corner-target correction: existing progression fixture extended with input-only L steering; targeted rerun **1 passed / 0 failed** (`progression-steering-results.xml`). No Golden Path or Batch Runner.
- Non-development Windows player script compilation: **PASS**, including the final navigation correction (`compile.result`, 01:21 Moscow). Unity import and `git diff --check` completed without errors.
- Short Play Mode smoke scenarios exercised Bunker → MVP and WorldSystemsLab using the production runtime. Screenshots `production-smoke.png`, `production-exit-locked.png`, `production-exit-open.png`, `lab-smoke.png` were visually inspected. Fixtures use physical movement for collision and bounded physical samples for ordered gate/Exit crossing; they do not set progression state.
- Known limits: Straight's 129-unit route is correctly rejected by the authored 100×100 MVP bounds; no world resizing. The existing Lab TacticalMapHUD missing-reference error remains explicitly expected by its fixture. Broad bot/tutorial/rotation/outcome matrices were deliberately omitted per user instruction.

All evidence paths above are relative to `Artifacts/GeneratedQA/CorridorMigration/` unless fully specified. No commit, merge or deploy performed.

| Требования spec | Задачи |
|---|---|
| Route data/seed/rotation/presets | 1, 3 |
| Gates/Exit/state/finish | 2, 7 |
| Anomaly overlap/lifecycle | 3, 4, 7, 11 |
| Prefab assets/UI/layers | 5, 8 |
| Strikes/collapse/cleanup/death | 6, 7 |
| Reward policy / existing catalog | 4, 7, 8 |
| Tutorial/bot | 9 |
| Lab без gameplay duplication | 10, 11 |
| Legacy deletion / no Dev dependency | 5, 11 |

На этапе подготовки документов ни один checkbox исполнения не отмечен: gameplay, сцены и assets остаются в исходном состоянии. Следующий шаг — подтверждение пользователем spec + plan.
