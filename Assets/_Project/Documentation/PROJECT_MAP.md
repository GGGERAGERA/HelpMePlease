# Subject#42 — project map

Current production and development navigation. Historical QA reports describe their own snapshots.

## Production flow

Enabled build scenes, in order:

1. `Scenes/MainBuild/StartScreen.unity` — entry and settings.
2. `Scenes/MainBuild/MainMenu.unity` — Bunker selection, progression, missions and minigames.
3. `Scenes/MainBuild/MVP.unity` — exploration, combat, world events and rewards.

Paths here are relative to `Assets/_Project`. `RunRoute` defines three exploration sectors; the last sector enters the final boss phase through `RunFlowController`. `RunStateManager` owns cross-scene state and run identity. `RunEndService` handles return, death and confirmed victory.

`ProductionSceneComposition` assigns typed services/configs and scene bindings before consumers run. Shared authored service prefabs are in `prefabs/Bootstrap`; readiness controls scene reveal. CharacterData selects `prefabs/Characters/Gera.prefab`, `DiMag.prefab` or `Vika.prefab` (Circle, FigureEight or Custom). `PlayerLoadoutFactory` builds the ORBITAL loadout. WeaponData and Pistol/Laser prefabs still have live consumers; they are retained.

## Runtime ownership

| Domain | Sources and assets |
| --- | --- |
| Run/lifecycle | `scripts/Run`, `Data/Stages`, `Data/SurfaceMap` |
| ORBITAL/combat | `scripts/Combat`, `prefabs/Orbital`, authoritative nested `prefabs/miniWeapons` |
| Bunker | `scripts/Bunker`, `prefabs/Bunker`, `Data/Meta` |
| World | `scripts/World`, `Data/World`, `Data/WorldEvents`, `prefabs/Environment` |
| UI/progression | `scripts/UI`, `scripts/Progression`, `prefabs/UI`, `Data/UI`, `Data/Upgrades` |

Production Corridor uses `Data/WorldEvents/Corridor` and `prefabs/Environment/WorldEvents/Corridor/PF_CorridorEvent.prefab`. Orbital Relay uses `prefabs/Environment/WorldEvents/OrbitalRelayEvent.prefab` and `Data/World/Events/OrbitalRelayConfig.asset`. Production, tutorial and WorldSystemsLab share these runtimes; Labs provide adapters and controls.

## Development

All development tools/scenes/tests live under `Dev` and are excluded from the production scene graph. Runtime tools are guarded for Editor/development builds.

| Entry | Location | Purpose |
| --- | --- | --- |
| F1 Inspector | `Dev/Labs/F1`, F1 in Editor/development runs | Gameplay controls, combat/visual tuning and practice |
| GoldenPathLab | `Dev/Labs/GoldenPathLab` | Opt-in bot batches, seeds, replay/history |
| OrbitalLab | `Dev/Labs/OrbitalLab` | Reward, enemy and custom orbit scenes |
| WorldSystemsLab | `Dev/Labs/WorldSystemsLab` | Shared world systems and event adapters |

Authoring/diagnostics are in `Dev/Editor` under `Tools > Subject42 > Dev`. See [Labs](../Dev/Labs/README.md) and [tests](../Dev/Tests/README.md). `Artifacts/GeneratedQA` is ignored reproducible output.

## Integrity boundary

`ProductionBoundaryAuthoringTests` checks saved production scenes/prefabs/configs and their serialized dependency closure for Dev dependencies, broken references and missing scripts. Explicit optional nulls are accepted. `Tools/QA/Test-ProductionScriptBoundary.ps1` compiles actual Unity response files with every project Dev source removed. The project retains Unity's predefined assemblies; the final asmdef decision and verification are in `docs/phase7-cleanup.md` at repository root.

Artist libraries, audio, gameplay balance and namespaces were not broadly reorganized. Dead assets were removed only after GUID and source-consumer audit; live import roles and moved identities are preserved.
