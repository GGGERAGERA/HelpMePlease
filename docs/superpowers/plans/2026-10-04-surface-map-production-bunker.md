# Surface Map and Production Bunker Implementation Plan

> For agentic workers: use superpowers:executing-plans inline. User explicitly authorized immediate execution without another review gate.

**Goal:** Launch production from the new bunker through a persistent surface frontier map.
**Architecture:** SO graph -> progression/selection service -> RunConfig snapshot -> existing run lifecycle/gameplay consumers. Editor authoring wires existing scene/prefab systems once.
**Tech Stack:** Unity 6, C#, ScriptableObject, uGUI/TMP, PlayerPrefs, Unity Test Runner.
**Spec:** ../specs/2026-10-04-surface-map-production-bunker-design.md

## Global Constraints
No new enemies/art/biomes, no football redesign, no unrelated refactors, no broad regression suites. Preserve existing player settings and old bunker.

## Review Focus
- Stale/duplicate/unknown sector IDs cannot launch a run or corrupt saves.
- Repeated completion is idempotent; only confirmed production victory advances the map.
- Config survives internal stage transitions/restart and cannot leak into legacy/dev starts.
- New bunker contains one context/controller set and shared persistent services are not duplicated.
- Events influence site-controlled exploration as well as timed events.

## Tasks
- [x] 1. Add targeted tests and a request-driven editor runner; observe missing surface contract/context failures.
- [x] 2. Add scripts/Run/SurfaceMap/{SurfaceMapDefinition,SurfaceSectorDefinition,SurfaceMapProgressionState,SurfaceMapService}. Add scripts/Run/State/RunConfig. Validate graph, save by stable IDs, expose TrySelect/TryBuildRunConfig/CompleteVictory; snapshot weighted prefab data.
- [x] 3. Extend BunkerRunStarter and RunStateManager: accept config, retain across stages/restart, clear on ordinary starts/end, complete only at confirmed production victory. Adapt RunSector multipliers, Threat, EnemySpawner, LevelModifiersApplier and event/site selection through RunConfig.
- [x] 4. Add SurfaceMapView and connect BunkerPanelManager Run Exit path, keeping escape protocol terminal intact. Author MVP map assets and prefab with existing UI primitives; no logic in UI beyond rendering/commands.
- [x] 5. Add editor authoring for production bunker: missing station prefabs, context, notifications/events/summary and football dependencies. Validate existing references instead of wholesale scene copying. Retain MainMenu disabled in build list; central production route remains new bunker.
- [x] 6. Run only SurfaceMap/ProductionBunker targeted tests, compile and runtime smoke, inspect diff/scene refs. Document concrete assets, authoring, future theme resolver and unfinished visuals/football.

Execution ledger: audit complete; existing route already correct; user ProjectSettings.asset modification must be preserved. Work in place on TestByDantes to use running Unity Editor and its authored scene dependencies.

Verification: Unity targeted run passed 11/11 tests on 2026-10-04 (41.8 seconds). Includes actual startup, station interactions, Surface Map button, run restart/abort/death, real final boss victory, map save/reload and production scene/build validators. No broad suite or player build performed.
Independent read-only review completed: no actionable correctness findings.
