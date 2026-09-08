# Special Site → ORBITAL: vertical-slice cleanup

## Runtime audit before changes

- `ProductionExplorationSectorController.Initialize` creates three normal Sites and one Special Site. `SelectSpecialPower` selects the existing environment identity (Gravity/Electric/Beam), not a reward pool. Generation, rarity, location, art and hazards are unchanged.
- `ProductionAnomalySite.InitializeSpecial` resolves its environment definition, creates the existing environment and calls `SpawnEvent(true)`. `WorldEventSpawner.SpawnSiteEventAt` records suppression of standard event loot for this event. Normal Sites use `SpawnEvent(false)` and retain their existing rewards.
- Each existing encounter calls `WorldEvent.CompleteEvent` on its own success condition. It marks itself complete, cleans up once, and notifies its owning spawner. `ProductionAnomalySite.HandleEventCompleted` accepts only its own active event and only once, marks the Site complete, clears the event and collapses the environment/boundary. Failure instead respawns the existing encounter after two seconds.
- Previously, Special completion called `GrantSpecialPower` → `AnomalyItemCatalog.Find(definition.PowerReward)` → `RunStateManager.TryGrantAnomalyItem` → `AnomalyInventory.TryGrant` → `AnomalyPowerRuntime.EnsurePower`. This bypassed the ORBITAL queue. Inventory encoded a single slot, levels I–III and replacement/maxed results. The Site displayed legacy acquisition/upgrade/occupied/maxed messages.
- `CharacterSpawner.Start` restored that inventory via `ApplyRunLoadout` and added `AnomalyCoreRuntime` and `EvolutionRuntimeController`. Gravity Orb uses `GravityConstruct`; old Arc Node uses `ArcNodeRuntime`; rotating beam uses `RedBeamRuntime`. These are distinct from ORBITAL `ArcEmitter` and `LinkNode`.
- Anomaly inventory/evolution are in-memory fields on persistent `RunStateManager`, cleared at new-run/end-run boundaries. They are not a serialized power save to migrate. Old definitions, runtime registry and debug commands still reference them.
- `AnomalySlotHUD` was a separate authored root in MVP with a HUDManager reference and safe-area reservation. It subscribed to inventory/evolution changes. No separate production upgrade/replace window exists; the Site displayed a replacement-unavailable message.
- Bunker Anomaly station selects/invests in `AnomalyStabilizerData`: zone size, gold inside anomalies, stasis effect and gravity force. `AnomalyRunModifiers` feeds existing zone behavior. It does not award the removed powers, so it and its saved meta progress remain unchanged. World Rules, normal Events and local anomalies retain their existing ownership.
- Production rewards already use one `OrbitalRewardProvider` owned by `UpgradeManager`. `ShowLevelUpChoices` and `ShowUpgradeChoices` enter the same request machinery. Provider builds the current eligible pool; `SelectOrbitalReward` uses `OrbitalRewardFlowController`. Mount/ring/module placement and module flight retain the request until commit; Link stages both targets before atomic install. Esc/RMB return to the same cards without consuming or rerolling the request.
- `UpgradeManager.IsRewardQueueIdle` includes active/pending/milestone requests. `LevelChoiceManager.ShowChoices` defers on it, `TransitionToSector` checks it again, and the current `RunFlowController` waits for rewards before transition/final boss. The existing three-exploration-sector boss changes were already uncommitted and are preserved.

## Changes

- Special completion now calls `UpgradeManager.Instance.ShowUpgradeChoices()` once, after the existing completion/collapse guard. It uses the same configured card count and eligible pool as normal progression. One choice means one selected reward, not one card. No new provider, queue, source enum or reward service.
- Removed `GrantSpecialPower`, its Roman-level messages, and unused power-reward/display-name fields from environment definitions. Environment type identifiers remain to avoid a generation/debug migration.
- Removed legacy loadout restoration and automatic anomaly-core/evolution components from player spawn. Deleted the now-unused `ApplyRunLoadout` method.
- Legacy grant/clear commands are explicitly named `DebugTryGrantAnomalyItem`/`DebugClearAnomalyItem` and compiled only for Editor/development builds. Existing debug callers are updated. These intentional sandbox commands remain outside normal run progression.
- Removed the anomaly-slot scene instance, HUDManager field/reservations, script/prefab and their metas. Updated authored-UI validator/tests while retaining pre-existing boss/UI changes.

## Deliberately retained legacy

`AnomalyInventory`, item assets/catalog, evolution state/recipes, power implementations and registries remain for existing debug/legacy code. There is no production grant or automatic restoration path. Shared `AnomalyPowerType` and `AnomalyPowerVisuals` still support Site environments and visuals, so this is not a broad deletion pass. ORBITAL modules, transactions, XP, progression balance and Bunker remain unchanged.

## Verification

The new scene test exercises the real bunker startup, all three Special environment definitions with a real Capture encounter and its hold-time completion, queue duplicate protection, module/Link cancellation and commit, transition gating, scene restoration, exit with a pending reward and a new run. It expands the test card count to cover the existing pool deterministically and shortens only the test encounter timer. Esc uses the real interaction handler; RMB cancellation calls its shared cancellation path (no physical mouse injection).

### Executed checks (Unity 6000.3.13f1)

- Tests run in a separate temporary project copy, including copied import cache. Company/product names were changed only in that copy to isolate PlayerPrefs. The open working Editor was not driven or closed.
- Broad selected suite: 94 cases, initially 91 passed. The new test fixture was corrected for domain reload, asynchronous level-up milestone presentation and typed SceneHandle equality; its full scenario subsequently passed. No production transaction changes were needed.
- Final current-source rerun: Special Site end-to-end test and authored UI validator both PASS (2/2), including a valid blocked 1 → 2 transition and an actual scene change after commit. Across the selected 94 cases, 92 are verified passing; the two baseline failures are detailed below.
- Existing passing coverage includes ORBITAL initial state, module/ring/core operations, staged Link transactions, cancellation/duplicate-flight protection, restore identity, actual sector transitions, run-state boundaries, authored references, and the three-sector final-boss/reward/death flows.
- Two existing cases are not green: `AuthoredUiSupportsLootPauseResultsAndSceneTransitions` reports a LocalizationService object created during Play Mode teardown; `ProductionScene_GrowthSwitchRestoreCombatAndScreenshots` encountered a dead test player and cleared station state during its unprotected wait.
- Baseline comparison restored the pre-cleanup paths in the test copy while retaining the user's pre-existing boss changes. Both old cases still fail: identical LocalizationService teardown error; growth reaches its framing assertion but measures 24.17 where its old threshold requires 55–65. This is not a clean visual-regression baseline. No camera, localization or growth code was changed in this cleanup.
- The Site scenario uses real Capture encounter start/hold completion for each existing Gravity/Electric/Beam environment. Other encounter implementations remain unchanged and were traced statically; this is not a manual playthrough of every encounter variant. Physical RMB input was not injected; the shared RMB cancellation handler was exercised directly.
- `git diff --check` passes. Serialized references to the removed HUD prefab/script and legacy power components were checked across production scenes/prefabs. Existing uncommitted boss/route changes are preserved.

Remaining debt is the deliberately isolated legacy power/evolution code plus the two pre-existing test failures above. No new mechanics or follow-up cleanup pass was started.
