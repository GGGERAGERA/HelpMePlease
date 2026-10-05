# Production Orbital Relay Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans for inline execution or superpowers:subagent-driven-development if the user explicitly chooses delegation. Steps use checkbox (`- [ ]`) syntax for tracking. Do not implement before the user approves this plan and its spec.

**Goal:** Заменить CaptureZone одним production Orbital Relay, подключить существующие rewards/pressure и использовать его же в tutorial и WorldSystemsLab.

**Architecture:** Чистая state machine + небольшие event/Node/presentation adapters; authored prefab и immutable config. WorldEventSpawner остаётся owner completion/rewards/pressure, UpgradeManager — owner queue/UI; lab остаётся только bootstrap. Удаление старой системы происходит после перевода прямых ссылок.

**Tech Stack:** Unity 6000.3.13f1, C# 9, MonoBehaviour/ScriptableObject, TMP/UGUI, Animator, CircleCollider2D, существующие Unity Test Runner и dotnet csproj сборки.

**Spec:** [2026-10-06-orbital-relay-production-design.md](../specs/2026-10-06-orbital-relay-production-design.md).

## Global Constraints

- Один production runtime и один production prefab используются в игре, tutorial и WorldSystemsLab.
- State flow: `Inactive → Stabilization → Transition → Bonus → Completed`; таймаут Stabilization ведёт в `Failed`.
- Defaults: requiredActivations=3, stabilizationDuration=20, stabilizationContactTime=0.6, transitionDuration=0.75, bonusDuration=15, goldPerActivation=10, comboWindow=2.5, bonusEnemyPressureMultiplier=1.6.
- Gold только за BonusActivations; AddGoldExact; ровно одна Upgrade selection через существующий UpgradeManager.
- Runtime composition и уникальная FX-геометрия authored в prefab. Production никогда не зависит от Dev/Labs asset или editor/development-only API.
- Другие Events сохраняют нынешний generic reward container и standard pressure. Bunker, mission и threat systems не переписываются.
- Нет Golden Path, Batch Runner, полного Core или длинного tutorial integration run. Только targeted critical-contract checks, compilation, reference audit и короткий local flow.
- Текущие незакоммиченные три lab-файла — исходный проверенный прототип пользователя. Не discard/reset; удалять gameplay/prefab только после миграции.
- Пока пользователь рассматривает документы, никаких code/asset/scene изменений.

## Review Focus

1. Отмена/disable/reset/unload между Stabilized и Bonus: qualification не превращается в fail игрового результата, teardown не открывает reward UI; leases не остаются. Tests tasks 1/3/4/7.
2. Старый lease освобождён после старта нового event: он не сбрасывает новый pressure. Test task 3.
3. Normal anomaly site и tutorial: Upgrade queue вызывается ровно один раз, generic container не появляется; существующий special-site Ring сохраняется. Tests tasks 4/6.
4. Contact в release, rotated/nonuniform body, preview/drag/halo и malformed Node config: никаких ложных contacts, exception или silently running invalid event. Tests tasks 1/2/5.
5. Timeout на границе frame, pause и повторная completion notification: score точен, Gold не overflow и не удваивается. Tests tasks 1/4.

## File map

**Create production:**

- `Assets/_Project/scripts/World/Events/OrbitalRelayConfig.cs`
- `Assets/_Project/scripts/World/Events/OrbitalRelayState.cs`
- `Assets/_Project/scripts/World/Events/OrbitalRelayResult.cs`
- `Assets/_Project/scripts/World/Events/OrbitalRelayNode.cs`
- `Assets/_Project/scripts/World/Events/OrbitalRelayEvent.cs`
- `Assets/_Project/scripts/World/Events/OrbitalRelayPresentation.cs`
- `Assets/_Project/scripts/World/Events/WorldEventPressureModifier.cs`
- `Assets/_Project/scripts/Combat/OrbitalStation/OrbitalBodyContactDetector.cs`
- `Assets/_Project/Data/World/Events/OrbitalRelayConfig.asset`
- `Assets/_Project/prefabs/Environment/WorldEvents/OrbitalRelayEvent.prefab`
- `Assets/_Project/art/FX/OrbitalRelay/RelayGlow.mat`, `NodePulse.anim`, `NodeActivation.anim`, `StabilizedTransition.anim`, `ComboPunch.anim`, `UrgentTimer.anim` (with needed authored AnimatorControllers and .meta).

**Modify production:**

- `scripts/World/Events/WorldEvent.cs`, `WorldEventSpawner.cs`
- `scripts/Combat/OrbitalStation/OrbitalModuleRuntime.cs`, `OrbitalModuleVisual.cs`
- `scripts/Run/Exploration/ProductionAnomalySite.cs`, `ProductionExplorationSectorController.cs`
- `scripts/Run/Tutorial/TutorialController.cs`, `TutorialOverlay.cs`
- `Scenes/MainBuild/MVP.unity` (event catalog reference only)
- `Data/SurfaceMap/Sector_D1.asset`, `Sector_D2.asset` (event reference; preserve weight)
- `Data/Localization/LocalizationTable.asset`

**Create/modify Dev:**

- Create `Assets/_Project/Dev/Debug/WorldSystemsLab/WorldSystemsLabRelayAdapter.cs`
- Create `Assets/_Project/Dev/Debug/GoldenPathLab/OrbitalRelayBotSteering.cs`
- Create `Assets/_Project/Dev/Editor/WorldSystemsLab/OrbitalRelayAuthoring.cs` (asset authoring/validation only, no runtime dependency)
- Modify WorldSystemsLabController.cs, WorldSystemsLabAuthoring.cs, WorldSystemsLab.unity, GoldenPathLab/BotController.cs.
- Create targeted fixtures in `Assets/_Project/Dev/Tests/Core/Editor/`: OrbitalRelayStateTests.cs, OrbitalRelayContactTests.cs, OrbitalRelayIntegrationTests.cs, OrbitalRelayAuthoringTests.cs.
- Modify Subject42TutorialTests.cs; update Dev/Tests/README.md inventory only if new fixtures change it.
- Update current `docs/tutorial-first-run.md` and `Assets/_Project/Documentation/PROJECT_MAP.md` links/names around migrated event.

**Delete after migration:** files and keys listed in spec §12; preserve all shared loot assets. Every created/moved/deleted Unity asset includes its .meta. New script/config/prefab GUIDs are assigned once and reused consistently; old GUIDs are scanned, not reused as a compatibility facade.

## Task 1 — State, config и result contracts

**Files:** create OrbitalRelayConfig.cs, OrbitalRelayState.cs, OrbitalRelayResult.cs; create OrbitalRelayStateTests.cs.

**Interfaces produced:**

- `OrbitalRelayPhase { Inactive, Stabilization, Transition, Bonus, Completed, Failed }`.
- `OrbitalRelayConfig.TryGetSettings(out OrbitalRelaySettings settings, out string error): bool`; readonly settings contains eight parameters from spec §5.
- `OrbitalRelayState(OrbitalRelaySettings settings, int nodeCount, Func<int,int> selectNextNode)`; selector takes previous index (-1 initially), returns valid different index.
- `Start(): void`, `Tick(float deltaTime, bool hasContact): void`, `CancelGameplay(): void`, `DisposeWithoutRewards(): void`; terminal operations idempotent. Tick does not allocate or invoke Unity services.
- `Snapshot: OrbitalRelaySnapshot` with Phase, ActiveNodeIndex, RemainingTime, ContactProgress, StabilizationActivations, BonusActivations, Combo, ComboRemaining; `Result: OrbitalRelayResult?` only for terminal gameplay state; null after administrative disposal.
- `OrbitalRelayResult.Calculate(bool stabilized, int bonusActivations, int goldPerActivation)` produces Success, BonusActivations, Gold, UpgradeSelections. `WorldEventRewardResult` readonly value contains Gold and UpgradeSelections; completion payload uses nullable value.

- [ ] Write failing tests with named assertions: `StartsInactiveThenStabilization` (20s, zero scores); `ThresholdStartsTransitionNotCompletion` (3 contacts of .6s → Transition, 0 Bonus score); `TransitionFreezesContactAndStartsFullBonus` (.75s → Bonus with 15s, x0); `BonusCountsOnlyBonusActivations` (4 bonus contacts → 40 Gold and 1 Upgrade); `TimeoutFailsBeforeThreshold` (2 contacts then timeout → Failed, Gold=0, Upgrade=0).
- [ ] Add `ContactBreakResetsLocalProgress` (.3 contact, break, .3 contact is no activation); `ComboExpiresWithoutScoreLoss` (x2 then 2.5s gap → x0, bonus score intact); `TwoNodesAlternate`; `BoundaryContactWinsBeforeTimeout`; `ZeroDeltaDoesNotAdvance`; `CancelAfterQualificationIsSuccess`; `AdministrativeDisposeDoesNotYieldReward`; `InvalidSettingsRejected`; `GoldCalculationSaturatesWithoutOverflow`.
- [ ] Run only OrbitalRelayStateTests via Unity Test Runner exact class filter; verify failure caused by missing/new contract, not setup.
- [ ] Implement the pure model and validated settings using the exact defaults/phase policy in spec. No bool lattice replicating state; pending local contact/combo counters are data, qualification follows Phase.
- [ ] Re-run exact fixture; all named cases pass. Review snapshots at each transition for off-by-one/frame overshoot.
- [ ] Commit only task files and .meta after green checks. Do not stage user lab changes as unrelated files.

## Task 2 — Release-safe mounted-body detector

**Files:** create OrbitalBodyContactDetector.cs, modify OrbitalModuleRuntime.cs and OrbitalModuleVisual.cs; create OrbitalRelayContactTests.cs.

**Interfaces:**

- `OrbitalBodyContactDetector.IntersectsCircle(SpriteRenderer body, Vector2 center, float radius): bool` (internal geometry utility).
- `OrbitalModuleRuntime.HasBodyContact(Vector2 center, float radius): bool` public runtime API; internal `OrbitalModuleVisual.HasBodyContact` handles authored sprites and exclusions.
- During intermediate commit old lab caller can be changed to new API for compilation; delete TouchesCircle immediately after usages move, no retained alias at final state.

- [ ] Tests: `RotatedThinBodyDoesNotUseWorldAabb`; `CornerAndInsideContacts`; `NonuniformSpriteTransformSupported`; `NegativeOrNonfiniteRadiusRejected`; `DetachedOrHiddenBodyRejected`; `DragPreviewAndHaloDoNotActivateNode` using authored module fixtures/current public attach/drag presentation API, not private flags hacked by event.
- [ ] Run only OrbitalRelayContactTests; confirm failures before detector implementation.
- [ ] Extract transform/closest-edge geometric core, retaining mount parent/active checks and preview/drag/halo exclusions. No mount-center HitTest fallback, new collider on weapons or prefab mutation.
- [ ] Remove the two conditional TouchesCircle blocks after caller migration; inspect production method's transitive dependencies for UnityEditor/Dev references.
- [ ] Re-run contact fixture; compile release runtime. Regenerate Unity project files when new source files appear. `Assembly-CSharp.Player.csproj` currently has neither UNITY_EDITOR nor DEVELOPMENT_BUILD; inspect constants again before `dotnet build Assembly-CSharp.Player.csproj --no-restore --verbosity quiet`. Expected zero errors; report independently from Editor build.
- [ ] Commit detector/API/test changes only.

## Task 3 — Event-scoped production pressure

**Files:** modify WorldEvent.cs, WorldEventSpawner.cs; create WorldEventPressureModifier.cs; pressure cases in OrbitalRelayIntegrationTests.cs. EnemySpawner.cs remains unmodified unless an identified reset defect is necessary to fix the scoped contract.

**Interfaces produced:**

- `WorldEvent.UsesStandardSpawnPressure: bool` virtual default true; Relay false.
- `WorldEventSpawner.AcquireSpawnPressure(WorldEvent source, float multiplier): IDisposable`; validates ownership/finite>=1; null/no-op on absent pipeline with diagnostic, never creates a spawner.
- `WorldEventPressureModifier.SetBonusActive(WorldEventSpawner spawner, WorldEvent source, bool active, float multiplier): void`; disposing/terminal safe.

- [ ] Tests: `OtherEventsKeepStandardPressure`; `RelayBaselineIsNormal`; `BonusLeaseAppliesOnePointSix`; `RepeatedEnterDoesNotStack`; `DisposeSuccessFailDisableResetUnloadRestoresBaseline`; `StaleLeaseCannotResetNewEvent`; `RuleMultiplierIsPreserved`; `StoppedSpawnerStaysStopped`.
- [ ] Run exact pressure test selection; confirm failures.
- [ ] Implement token-scoped lease ownership and recomputation inside WorldEventSpawner, applying only existing EnemySpawner setter. Clear leases in completion/failure/debug clear/ReleaseRunScene/OnDisable, including before stale event notifications. No global multiplier assignment in Relay cleanup.
- [ ] Implement component that owns one IDisposable handle and releases it in all lifetime hooks. No coroutine spawner or event-type branch in EnemySpawner.
- [ ] Run selected tests, inspect `WorldEventSpawnPressureMultiplier` values before/after all cleanup paths; existing tutorial spawn safety remains stopped.
- [ ] Commit changes only after confirming next generic event still gets its ordinary pressure.

## Task 4 — Completion reward dispatch without duplicates

**Files:** WorldEvent.cs, WorldEventSpawner.cs, ProductionAnomalySite.cs; reward cases in OrbitalRelayIntegrationTests.cs. Consume task 1 result value and existing CurrencyManager/UpgradeManager APIs; do not create a reward service singleton.

**Interfaces produced:**

- `WorldEvent.CompletionReward: WorldEventRewardResult?` virtual default null.
- `WorldEvent.Cancel(): void` virtual gameplay cancellation (default old fail path).
- `WorldEvent.TryValidateConfiguration(out string error): bool` virtual default true; `WorldEvent.DisposeForOwnerReset(): void` internal administrative cleanup shares current ClearForDebug idempotency but is available in release. ClearForDebug forwards to it; no notifications/rewards during administrative release.
- `WorldEventSpawner.IsRewardDeliverySuppressed(WorldEvent source): bool` reports debug preview suppression, not site standard-container suppression; event uses it only for production reward-owner preflight. `ConfigureDebugEnemySpawner(EnemySpawner spawner): void` is editor/development-only bootstrap binding for lab, not a bonus control API.
- WorldEventSpawner completion handler snapshots custom result and dispatches exactly once before existing EventCompleted; null result retains old container route. Existing debug suppression affects entire custom delivery; site suppression affects generic container only.

- [ ] Tests: `FailedRelayHasNoReward`; `SuccessDispatchesExactGoldAndOneSelection`; `MetaGoldMultiplierDoesNotAlterRelayPayout`; `ReentrantCompletionDoesNotPayTwice`; `GenericEventStillSpawnsOneContainer`; `NormalSiteDoesNotGrantSecondUpgrade`; `SpecialSiteRingStillUsesExistingQueue`; `DebugPreviewAndDevRunDoNotPersistGold`; `ResetAndUnloadDoNotOpenRewardUi`.
- [ ] Run only these cases; preserve/restore currency/progression PlayerPrefs in existing CoreTestSupport cleanup. No Bunker scene flow or boss simulation.
- [ ] Add pre-spawn validation to every Instantiate/register path (normal/site/debug/concurrent debug), before bookkeeping. Runtime missing reward dependencies rejected before production start, preview mode exempt.
- [ ] Replace private SpawnRewardContainer handler with shared completion dispatcher: AddGoldExact once, ShowChestRewardChoices(3,false,onClosed) once for successful Relay result; never create container as well. Respect debug/dev currency protections and existing queue barrier.
- [ ] Modify only custom-reward branch of normal anomaly-site completion to use RunWhenRewardQueueIsIdle(CompleteSite). Generic normal reward path and special two-event/Ring path stay intact.
- [ ] Establish explicit gameplay Cancel versus administrative reset contract in base owner hooks, avoiding reward issuance from CleanupEvent/OnDestroy. Don't move generic reward owners into Relay.
- [ ] Re-run reward/pressure subsets; commit. No implementation of durable death compensation or unrelated currency persistence changes.

## Task 5 — Authored Node, FX, production UI и event adapter

**Files:** create Node/Event/Presentation production scripts, config asset, prefab, FX clips/controllers/material; create OrbitalRelayAuthoring.cs editor utility and OrbitalRelayAuthoringTests.cs.

**Interfaces:**

- `OrbitalRelayNode.TryGetContactCircle(out Vector2 center, out float radius): bool`; `Apply(bool active, float progress): void`; `PlayActivation(): void`.
- `OrbitalRelayPresentation.Render(OrbitalRelaySnapshot snapshot): void`, `PlayTransition(): void`, `ShowResult(OrbitalRelayResult result): void`, `Clear(): void`; all refs serialized, no runtime hierarchy generation.
- `OrbitalRelayEvent : WorldEvent`: `Snapshot`, `ActiveNode: OrbitalRelayNode`, `ArenaRadius`, `IsPlayerInside`, `event Action PlayerEntered`, `event Action<OrbitalRelayPhase> PhaseChanged`, `event Action<OrbitalRelayResult> Finished`. Override CompletionReward, UsesStandardSpawnPressure, validation, start/cleanup/Cancel.
- `OrbitalRelayAuthoring.CreateOrUpdateProductionAssets(): void` and `ValidateProductionAssets(): void` (Editor only). Creation is opt-in, never InitializeOnLoad auto-rewrite of unrelated assets.

- [ ] Tests: `PrefabHasAuthoredNodesAndFx`, `NoMissingScriptOrRequiredReferences`, `ProductionPrefabHasNoDevDependencies`, `DuplicateOrOneNodeConfigurationRejected`, `NonuniformContactCircleRejected`, `MissingStationDoesNotStart`, `ContactApiDrivesActualNodeProgress`, `BonusStartsOnlyAfterTransition`, `RuntimeDoesNotCreateVisualObjects` (hierarchy/renderer count stable apart from existing common markers/reward UI).
- [ ] Author prefab according to spec hierarchy; link shared TMP font/palette, three explicit positions, radius9 bounds, radius.35 circles, serialized progress/FX and passive Canvas. Author ring vertices/animations in asset, not event LateUpdate. Config defaults copied exactly.
- [ ] Bind runtime to player/station through existing PlayerRuntimeReference/OrbitalStationRuntime; sample aggregate body contact in LateUpdate using task 2 API, feed model; select different Node from collection. No debug run creation in production adapter.
- [ ] Drive presentation by phase/snapshot, start pressure lease only on Bonus. Shared CameraShake optional; no new camera system. Final result and cleanup must snapshot before WorldEvent destroys instance.
- [ ] Add RU/EN `event.relay.*` keys listed in spec and localize presentation/reasons; preserve generic hud keys.
- [ ] Run authoring/contact flow subsets. Compile both Editor and Player projects after regeneration; verify zero errors before reference migration.
- [ ] Commit new production assets/scripts, editor authoring and tests. Old system remains only until next tasks replace all consumers; no final parallel registry entry.

## Task 6 — Registry, tutorial, bot и tests migration

**Files:** MVP.unity, Sector_D1.asset, Sector_D2.asset; ProductionExplorationSectorController.cs, TutorialController.cs, TutorialOverlay.cs; create OrbitalRelayBotSteering.cs; modify BotController.cs and Subject42TutorialTests.cs; retry tests in OrbitalRelayIntegrationTests.cs.

**Interfaces:**

- `ProductionExplorationSectorController.TryRespawnTutorialRelay(out OrbitalRelayEvent relay): bool` uses remembered placement and registered new prefab, no legacy variation.
- `TutorialController.ConfigureTarget(OrbitalRelayEvent target): void`, `TargetEvent: OrbitalRelayEvent`; subscribe/unsubscribe entry, phase and failure/complete once.
- `OrbitalRelayBotSteering.GetDesiredMovement(OrbitalRelayEvent relay, OrbitalStationRuntime station, Vector2 playerPosition): Vector2`; read-only real node/module geometry, bounded velocity intent, no writes to model.

- [ ] Replace old prefab GUID references in MVP catalog and D1/D2 weights, preserving weight/ordering. New prefab flags `allowedInSite` and `requiresHoldPointFeature` reflect old catalog contract.
- [ ] Tutorial sector chooses OrbitalRelayEvent, validates full placement footprint and exit clearance; uses same production prefab/settings. Entry auto-start remains, actual goal is orbital contacts.
- [ ] Replace tutorial capture instruction, focus active Node and phase; fail respawns after 2s without repeating earlier steps. Guard late callbacks against shutdown and old target; no retry coroutine after scene unload.
- [ ] Retry tests: `TutorialFailKeepsExitLockedAndRetriesSamePrefab`, `TutorialRetryDoesNotDuplicateSubscriptions`, `TutorialCompletesOnlyAfterBonusAndRewardBarrier`, `TutorialNoContactsNeverCompletes`. Run only these new targeted cases, not full tutorial fixture.
- [ ] Bot removes capture-radius circling and candidate scoring. New steering uses active Node/module offset through movement intent, retains obstacle/enemy/exit avoidance; Transition keeps safe movement, Bonus resumes contacts. Add short named steering/contact test; Golden Path itself not run.
- [ ] Update `Subject42TutorialTests.AllEightStepsPersistAcrossRestartAndReset` code: after entry drive ordinary intent via shared helper; assert observed phases/progress; complete one shared Upgrade choice/placement before exit; replace old idle-with-no-reward assertion. Compile this test, don't execute 240-second scenario.
- [ ] Compile Editor/Player; validate catalog and placement references; commit only direct migration files.

## Task 7 — Lab uses production, then remove legacy assets

**Files:** WorldSystemsLabRelayAdapter.cs, WorldSystemsLabController.cs, WorldSystemsLabAuthoring.cs, WorldSystemsLab.unity; delete spec §12 list/.meta; localization cleanup and current docs/README inventory updates.

**Interfaces:**

- `WorldSystemsLabRelayAdapter.Prepare(Transform player, CharacterData character, WorldEventSpawner events, GameObject debugEnemyPrefab, bool enableEnemyPressure): bool` — only bootstrap temporary dev run, station and one scene-level production EnemySpawner context.
- `Clear(): void` — release adapter-owned temporary objects/context; pressure phase changes are never coded here.
- `WorldSystemsLabController.SpawnEvent(WorldEvent prefab)` keeps selector behavior; production OrbitalRelayEvent uses this adapter then existing SpawnDebugEventAt. Existing ClearEvents handles every lab cleanup.

- [ ] Lab authoring loads production OrbitalRelay prefab directly and copies production registry without CaptureZone substitution; patch existing lab scene serialized catalog references without rebuilding unrelated scene content.
- [ ] Remove relay OnGUI HUD/result/formula and gameplay Configure call from lab controller. Subscribe only to typed production notifications if needed for notice; production presentation works with F1 panel hidden.
- [ ] Adapter baseline scene EnemySpawner binds to existing WorldEventSpawner; no dedicated event spawner or bonus multiplier in adapter. Clear it when switching to another Event or reset; CorridorV2 control untouched.
- [ ] Add optional reward-flow bootstrap using existing authored Upgrade UI/manager dependencies in dev run. Preview default; real queue check optional. Don't imply real reward verification when suppressReward=true or dependency bootstrap unavailable.
- [ ] Run scoped scan across Assets for old classes, old script/prefab/material/shader GUIDs and localization keys. Fix all live consumers first, then delete old production files and lab gameplay/prefab with .meta. No old alias left in bot/tests or generated authoring.
- [ ] Remove exclusive dead localization/fields, lab-specific local spawner/result code, historical active navigation references. Keep loot container, WorldEventMarker and site shared config.
- [ ] Authoring tests: `LabCatalogUsesSameProductionPrefab`, `NoLegacyRuntimeReferences`, `OtherLabEventReferencesUnchanged`, `ResetClearsOnlyAdapterOwnedEnemies`, `RewardPreviewHasNoPersistence`.
- [ ] Recompile Editor/Player and run exact authoring checks; commit deletion/bootstrap migration.

## Task 8 — Bounded acceptance and report

**Files:** no new runtime work planned; fixes restricted to preceding tasks' contracts. Record validation in plan checkboxes/report; update docs only if final implementation differs.

- [ ] Regenerate Unity csproj files; inspect inclusion of new scripts and exclusion of removed ones. Do not hand-edit/commit generated project files.
- [ ] Run `dotnet build Assembly-CSharp-Editor.csproj --no-restore --verbosity quiet`; expected zero errors. Warnings summarized, not mislabeled zero warnings.
- [ ] Inspect Player DefineConstants: neither UNITY_EDITOR nor DEVELOPMENT_BUILD; run `dotnet build Assembly-CSharp.Player.csproj --no-restore --verbosity quiet`; expected zero errors. If player project absent, regenerate through Unity IDE integration; never claim release coverage from Editor-only compile.
- [ ] Run only the new named critical fixtures through Unity Test Runner class/name filter. No category Core, no TutorialVerificationRunner default, no Golden Path/Batch Runner. Capture actual pass/fail output.
- [ ] `git diff --check` clean; exact `rg` scan for `CaptureZoneEvent|CaptureZoneVisual|CaptureZoneCompletionVisual|TouchesCircle|OrbitalHoldZoneEvent` has zero live code usages; any documentation historical mentions reported separately. Scan old GUIDs in serialized assets and new production prefab AssetDatabase.GetDependencies for Dev paths.
- [ ] Short relay-only WorldSystemsLab play: movement/body contact → 3 stabilize → visible .75s transition → 15s bonus → correct computed result; spawner pressure 1→1.6→1. Repeat fail/no-contact and reset mid-bonus. Temporary reduced-duration config for quick retry is restored and not shipped.
- [ ] Optional production queue smoke in same lab bootstrap: one Upgrade selection through common UI, development-run gold suppression confirmed; test harness currency assertions prove exact production dispatch. If unavailable, state limitation and use a short direct production scene event-only check, not full run.
- [ ] Inspect prefab/script/component references in Unity after deletions, registry weights and tutorial target type; no MissingScript, orphan GUID or duplicate production Hold Zone.
- [ ] Final report: architecture; new/modified/deleted files; production start; same-prefab lab start; pressure cleanup; Gold/Upgrade integration; authored composition; removed dead code; deliberately preserved site Ring/tutorial safety/other Events and any actual verification limits.

## Self-review completed before handoff

- Spec requirements mapped to tasks: state/config (1), contact/release (2), pressure cleanup (3), rewards/sites (4), prefab/UI/FX (5), registry/tutorial/bot (6), lab/deletion (7), validation/report (8).
- Interfaces named consistently: HasBodyContact replaces TouchesCircle; Snapshot separates counters; nullable CompletionReward customizes existing owner; reward selection count=1 is distinct from offered card count=3.
- Five Review Focus cases each have named checks. No expensive suite in any command/filter.
- Candidate shared assets not deleted just by name; old material/shader deletion is conditional on GUID-exclusive usage.
- Native execution is recommended: most tasks share owner interfaces and serialized assets. No subagent delegation before user explicitly selects it.

## Approval / execution handoff

Both spec and plan are drafts for the user's requested joint review. Implementation starts only after approval. Recommended method: inline/native execution with `superpowers:executing-plans`; it avoids parallel edits to WorldEventSpawner, prefab references and tutorial owners. If the user explicitly selects subagents, use the corresponding skill while preserving the same staged dependency order.

## Execution record — 2026-10-06

User approved the spec and all eight tasks; implemented sequentially on `codex/orbital-relay-production`.

- [x] Task 1: validated pure state/settings/result; State fixture 9/9.
- [x] Task 2: mounted runtime body contact, no debug-only dependency; Contact fixture 2/2 and Player compile.
- [x] Task 3: scoped pressure leases, lifecycle/stale ownership; Integration fixture.
- [x] Task 4: existing currency/Upgrade queue/custom site path; Integration and bounded Runtime fixtures.
- [x] Task 5: authored production prefab/config/FX; Authoring fixture 8/8.
- [x] Task 6: registry/tutorial/retry/barrier/bot; Integration fixture 10/10, long tutorial compile-only.
- [x] Task 7: same production prefab in lab; bootstrap only, legacy deletion/reference audit.
- [x] Task 8: 30/30 targeted Unity tests; default-duration contact flow, normal Interactor entry, queue, exact Gold, normal/special-site reward contracts, fail/cancel/reset/unload; Editor and release-runtime compilation zero errors. Detailed report: [orbital-relay-production-migration.md](../../orbital-relay-production-migration.md).

Named cases in the task briefs were consolidated into five exact-class fixtures rather than invoking large existing runners. State/contact use real geometry and pure timing; authoring checks assets; integration checks owners and barriers; the ~30s Runtime fixture exercises real movement/body contact and production reward owners in lab. Full tutorial/Golden Path/Batch Runner were not run. Known baseline TacticalMapHUD missing map-shell diagnostic is explicitly expected; relay migration does not alter map assets.

Acceptance found and fixed: old Corridor HUD overlap in lab, destroyed center-shift access on unload, transparent result notification ancestor, and production arena collider on a child that existing PlayerInteractor could not discover. Each has a regression check; Bunker/interactor code remains unchanged. Final whole-branch review and its conclusions are recorded in the report.
