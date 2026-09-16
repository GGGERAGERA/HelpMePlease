# Subject#42 — current project map candidate

Updated for Cleanup Step 4 (2026-09-16). This is the current navigation map; older QA reports describe historical checks, not the current production contract.

## Production

Enabled build scenes, in order:

1. `Assets/_Project/Scenes/MainBuild/StartScreen.unity`
2. `Assets/_Project/Scenes/MainBuild/MainMenu.unity`
3. `Assets/_Project/Scenes/MainBuild/MVP.unity`

`RunRoute` defines three exploration sectors. The final sector transitions to the final boss phase through `RunFlowController`; it is not a separate fifth sector. `RunStateManager` owns state across scene reloads, and `RunEndService` closes death/victory runs.

CharacterData selects each production prefab: Gera — Circle, Di-mag — FigureEight, Vika — Custom. ORBITAL runtime and rewards are the current character loadout path; legacy weapon assets remain pending a separate dependency review.

Runtime owners remain under `scripts/`: Combat/OrbitalStation, Progression, Run, World, Bunker and UI. Data assets live in `Data/`: Characters, Weapons, Upgrades, Stages, World, Anomalies, Meta and UI. The three authoritative gameplay prefabs are `prefabs/Characters/Gera.prefab`, `DiMag.prefab` and `Vika.prefab`. CharacterData has one productionPrefab reference; character visuals are embedded, with no Legacy/Production pair. Bunker composition and minigames live in `prefabs/Bunker`; world props/events/anomalies in `prefabs/Environment`. Moved assets retain their GUIDs. ProductionSceneComposition assigns typed service/config dependencies before scene consumers run; shared service prefabs live in prefabs/Bootstrap.

## Development

All development domains live under `Assets/_Project/Dev/`:

| Entry | Scene/tools | Purpose |
|---|---|---|
| F1 Inspector | `Labs/F1`; F1 in an Editor/development run | Gameplay controls, combat/visual tuning, Boss practice, Enemy gallery, surface comparisons |
| GoldenPathLab | `Labs/GoldenPathLab/GoldenPathLab.unity` | Bot batches, seeds, replay and history |
| OrbitalLab | `Labs/OrbitalLab` | Reward, Enemy and Custom Orbit modes in three existing scenes |
| WorldSystemsLab | `Labs/WorldSystemsLab/WorldSystemsLab.unity` | World Rules, anomalies, events and portals |

Editor commands are grouped under `Tools > Subject42 > Dev`. `Dev/Editor` contains authoring and diagnostics; `Dev/Debug` contains Editor/development runtime tools. The useful SurfaceVisualLab prototype is retained under `Dev/Labs/F1`.

Two release-runtime presentation types, `ProductionVisualTuningController` and `VisualTuningPreset`, now live in `scripts/World/Presentation`. Prop scripts are in `scripts/World/Props`; `BallRollVisual` joins `scripts/Bunker/Minigames`.

## Tests and output

`Dev/Tests/Core/Editor` contains 16 critical scenarios in 7 test files and one shared setup helper. Run the NUnit `Core` category. Extended has been removed (0 scenarios). There are no partial fixtures. Tests stay in the existing Editor assembly; GoldenPathLab/Bot and manual labs remain development tools.

`Artifacts/GeneratedQA` is ignored, reproducible output, not project documentation. Bot latest/history/seeds required for replay remain there. Historical QA captures do not replace a current test run.

## Boundaries

`art/` belongs to the artist and is untouched. AudioEffects is not cleaned by reference count. Own Resources loaders and folders have been removed. Their assets live in Data, Materials and prefabs; the props guide is Documentation/ProductionSectorProps.md. `prefabs/miniWeapons` remains the authoritative miniWeapon source; `prefabs/Orbital` is unchanged. The two loose root assets moved to `Materials` and `Materials/RenderTextures`. No production gameplay or balance change is part of this cleanup.
