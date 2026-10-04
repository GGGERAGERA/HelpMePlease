# Surface Map / production bunker

Production entry: `StartScreen` -> `Bunker_OneRoom_Prototype` -> `MVP` -> `Bunker_OneRoom_Prototype`. Enabled build order is StartScreen, new bunker, MVP. The old `Scenes/MainBuild/MainMenu.unity` remains unchanged and disabled as a reference/fallback; enable it explicitly for fallback builds. Runtime routing already pointed at the new bunker before this change; this integration completes the missing scene dependencies.

## Data and flow
`Data/SurfaceMap/SurfaceMap_MVP.asset` defines BUNKER-A1-A2, BUNKER-C1-C2 and BUNKER-D1-D2. Starting IDs are A1, C1, D1. Completed nodes are replayable. All IDs are stable and independent of asset names.

`SurfaceMapService` validates the graph, owns selection/progression, and snapshots the selected sector into `RunConfig`. `BunkerRunStarter.StartSurfaceRun` uses the existing character validation and transition. `RunStateManager.CurrentConfig` survives internal stages/restarts. Gameplay reads config parameters; no spawner, event, threat or reward system references the map definition.

`RunSector` combines stage scaling with configured enemy health, pressure, XP and initial location world rule. Threat uses configured initial pressure, growth and optional threat profile. Enemy weighted selection multiplies the existing prefab weights. Exploration chooses configured layout and weighted site-event/anomaly pools; timed events use weights/frequency. Gold multiplies the existing final reward calculation. The map world rule applies only to the first location (and tutorial entry). Subsequent locations use the ordinary random world-rule choices.

Only `RunStateManager` confirmed production victory publishes an eligible completion. MetaProgressionManager forwards it to the persistent map service, which unlocks neighbours and writes JSON using PlayerPrefs key `META_SURFACE_MAP_<mapId>`. Death, abort, unconfirmed Victory and dev runs do not advance the map. Saves store unlocked/completed IDs; new links from completed nodes reconcile on reload.

## Add a sector
Create a SurfaceSectorDefinition via Subject42/Surface/Sector. Give it a unique stable ID, display name/position, run parameters and neighbour IDs. Add it to the map sectors array and add reciprocal links on its neighbours. Add its ID to startingSectors only if it should be available initially. Graph validation rejects duplicate/missing IDs, invalid/nonreciprocal links and unreachable nodes. No runtime/UI code changes are needed.

Optional layout/spawn/threat assets override existing defaults. Weights reference existing prefabs/data directly, never enemy names. A uses 0.75 threat growth / 0.8 pressure / 1.35 XP; C uses 1.3 growth / 1.25 pressure / 2.5 bomber weight / 1.5 Gold; D biases False Signal/Corridor events and anomaly sites. Six sectors reuse three layout profiles and existing art.

## Future biome/theme
BiomeId/ThemeId already travel from sector data to the immutable RunConfig. A future resolver inserted before the snapshot/start boundary can combine those IDs with sector overrides to select layout pools, enemy pools, rules and presentation. Progression/UI remain independent. No biome visuals are implemented now.

## Production bunker authoring
The one-time editor command Tools/Subject42/Surface Map/Author MVP and Production Bunker wires existing station prefabs, a shared BunkerSelectionCatalog, Surface Map, notifications, events, summary templates, slot UI and existing football camera/door references. It does not copy a scene at runtime or create a second bootstrap. Re-running preserves existing map/prefab assets and designer settings.

The new bunker remains visually provisional; station placement is functional, rooms/art need polish, and football mechanics/visuals are not redesigned. Its existing arena/prefab and saves remain in use.

## Targeted verification
Tools are request-driven: write `Artifacts/SurfaceMap/run-tests.request` to run only SurfaceMapTests, SurfaceMapProgressionTests, SurfaceRunIntegrationTests and ProductionBunkerFlowTests. The latter exercises startup, stations, map, actual run startup/restart/abort/death and final-boss victory (route travel is skipped, boss spawn/death confirmation is real). Test storage avoids modifying the user's map save; existing core helpers preserve progression preferences.

Prior production/map target run: 11/11 passed. Marker extension target run: 4/4 passed; see `Artifacts/SurfaceMap/markers-results.xml` and `marker-map.png`.

## Markers and sector content
`SurfaceMarker` assets define extensible typeId/icon/color/pulse/concealment. MVP includes Unknown, Mission, Danger and Reward assets. UI has no type switch. `SurfaceSectorContent` contains a stable ID, marker, objective event/tag and run multipliers. Sector content arrays are immutable authored data; global contentCatalog also contains assignable mission templates.

`SurfaceContentService` owns separate dynamic mission records. `SurfaceMapService` only delegates config resolution and forwards content changes; frontier progression is unchanged. `RunContentResolver` combines active content with the base RunConfig snapshot. `RunConfig.Content` contains read-only guaranteed prefab/objective lists, without map/UI references. Multipliers combine with base XP/Gold/threat growth/spawn pressure. Missions currently follow their authored content template for requirements/modifiers; saved IDs bind that template.

Exploration reserves required prefabs in the largest normal sites before weighted content; unsupported prefabs/excess requirements are configuration errors, never silently dropped. Existing prefab source identity and optional event tags travel through generic completion reporting. Development runs and debug-created events cannot satisfy content objectives. Event failure is not completion. Pending objectives are scoped to RunId and discarded on death/abort/restart. Confirmed production victory commits marker Revealed/Completed using existing ISurfaceMapStorage/PlayerPrefs under `META_SURFACE_MAP_<mapId>:content`. Definitions are never modified by runtime progression.

Demo: D1 has UnknownSignal content (`?`, UNKNOWN ACTIVITY / Signal detected), guarantees existing False Signal, and reveals after event completion plus confirmed victory. Replays no longer guarantee completed content. Future NPC usage: register a mission content template in contentCatalog, then call `mapService.Content.AssignMission("npc-signal", "D2", template)`. This persists assignment and renders `!` immediately, including on a locked sector; assignment does not bypass frontier unlocking. No NPC/dialogue system is implemented.

Map navigation: drag the background or a node to pan; wheel zooms toward the pointer (0.65–2.2x), clipped to the map viewport. Drag cancels node clicks. Marker pulsing uses unscaled time. Targeted marker tests: write `markers` to `Artifacts/SurfaceMap/run-tests.request`.

Sector pacing: exit unlocks at ExitActivationTime, without assault completion/recovery gating. Automatic formations target 10–15 seconds before that deadline; missed automatic preparation windows are cancelled. Site-triggered assaults keep their event-driven behavior. Focused transition smoke passed 1/1: initial Golden, immediate timed exit during recovery, three random cards, non-Golden choice and actual next-scene application.
