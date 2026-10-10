# Development labs

Lab scenes are isolated Editor/development tools, absent from production Build Settings. Open one scene and press Play. Runtime controls live in `Dev/Debug`; authoring lives in `Dev/Editor`. Menus are under `Tools > Subject42 > Dev`.

## ORBITAL labs

`OrbitalLab/OrbitalRewardLab.unity` uses the production baseline, reward provider, real cards/placement and station state. Controls expose ordinary/random rewards, eligible direct grants, ring/mount/module operations, rotation/Bullet Time and reset. Reset cancels owned reward/presentation state and recreates the baseline.

`OrbitalLab/EnemyOrbitalLab.unity` uses production enemy prefabs/AI/health and player/ORBITAL systems. It provides spawn layouts, presets, AI freeze, speed/invulnerability, clear/reset and build controls. The serialized production enemy list can be refreshed from the OrbitalLab menu.

`OrbitalLab/CustomOrbitLab.unity` exercises custom orbit authoring/interaction with the shared runtime. These scenes have no alternate production weapon/reward framework.

## WorldSystemsLab

`WorldSystemsLab/WorldSystemsLab.unity` reuses production world rules, anomaly sites, events, portals, props, camera, player and tactical map. Its adapter owns temporary Lab support; gameplay phases, contacts, pressure and reward delivery stay in production. The Lab normally runs without the complete combat run. Orbital Relay prepares a production station and enemy pipeline; rewards are optional through the adapter. Carrier Hunt is a preview without a spawned carrier.

- WASD moves; mouse wheel zooms; F1 toggles the world-system panel.
- The panel applies/clears rules, creates/clears territories/events/portals, centers the player, toggles the map and resets the Lab.
- Corridor controls: F5 starts/restarts; F6 cycles route; F7 rotates the next route; F8 toggles crowd; F9 creates an independent Stasis territory.
- Corridor uses `Data/WorldEvents/Corridor` and `prefabs/Environment/WorldEvents/Corridor/PF_CorridorEvent.prefab` under `_Project`. Runtime and hazards are shared with production; Lab controls snapshot settings for the local run.
- Orbital Relay uses the same `prefabs/Environment/WorldEvents/OrbitalRelayEvent.prefab` as production/tutorial. `RelaySupport.prefab` contains shared notification/reward presentation. Event clear releases the adapter's event support and pressure; independent anomaly sites have their own cleanup.

`WorldSystemsLabAuthoring` retains the production tactical marker components when building the scene. Scene refs are authored; missing required map refs disable the map with a diagnostic.

## F1 and other retained tools

`Labs/F1` holds inspector, practice and surface comparison assets. Enemy gallery visual data and the older test scene are under this Dev domain. BunkerSimple variants are manual source scenes used by existing authoring tools; their old navigation runtime remains Dev-only. Source art and unrelated manual tools are retained where current use or abandonment cannot be established.

`GoldenPathLab/GoldenPathLab.unity` owns opt-in bot/batch/replay history. Running a narrow QA filter does not launch it. Generated results go to ignored `Artifacts/GeneratedQA`.

## Narrow acceptance

`Phase7SmokeTests.StartScreenBunkerEventRewardAndReturn` checks one production route with a real completed event and clicked reward. `Phase7SmokeTests.WorldSystemsLabStartsSharedRelayAndReleasesItsOwners` checks shared Relay startup, map integrity, event release and independent territory ownership. Use exact test names; `Core` includes longer scenarios and is not the limited cleanup acceptance filter.
