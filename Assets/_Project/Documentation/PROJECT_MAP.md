# Subject#42 — current project map candidate

Updated for Cleanup Step 1 + Step 2 (2026-09-16). This is the current navigation map; older QA reports describe historical checks, not the current production contract.

## Production

Enabled build scenes, in order:

1. `Assets/_Project/Scenes/MainBuild/StartScreen.unity`
2. `Assets/_Project/Scenes/MainBuild/MainMenu.unity`
3. `Assets/_Project/Scenes/MainBuild/MVP.unity`

`RunRoute` defines three exploration sectors. The final sector transitions to the final boss phase through `RunFlowController`; it is not a separate fifth sector. `RunStateManager` owns state across scene reloads, and `RunEndService` closes death/victory runs.

CharacterData selects each production prefab: Gera — Circle, Di-mag — FigureEight, Vika — Custom. ORBITAL runtime and rewards are the current character loadout path; legacy weapon assets remain pending a separate dependency review.

Runtime owners remain under `scripts/`: Combat/OrbitalStation, Progression, Run, World, Bunker and UI. Production prefabs, Scriptable Objects, Resources/loaders and initialization are unchanged by this cleanup.

## Development

All development domains live under `Assets/_Project/Dev/`:

| Entry | Scene/tools | Purpose |
|---|---|---|
| F1 Inspector | `Labs/F1`; F1 in an Editor/development run | Gameplay controls, combat/visual tuning, Boss practice, Enemy gallery, surface comparisons |
| GoldenPathLab | `Labs/GoldenPathLab/GoldenPathLab.unity` | Bot batches, seeds, replay and history |
| OrbitalLab | `Labs/OrbitalLab` | Reward, Enemy and Custom Orbit modes in three existing scenes |
| WorldSystemsLab | `Labs/WorldSystemsLab/WorldSystemsLab.unity` | World Rules, anomalies, events and portals |

Editor commands are grouped under `Tools > Subject42 > Dev`. `Dev/Editor` contains authoring and diagnostics; `Dev/Debug` contains Editor/development runtime tools. The useful SurfaceVisualLab prototype is retained under `Dev/Labs/F1`.

Two release-runtime types remain in `scripts/Debug`: `ProductionVisualTuningController` and `VisualTuningPreset`. They have real production callers/serialized data and are not cleanup candidates based on their folder name.

## Tests and output

`Dev/Tests/Core/Editor` contains Core scenarios. Run the NUnit `Core` category for bounded validation. `Dev/Tests/Extended/Editor` retains distinct technical checks, including malformed-state and lab/authoring diagnostics. Both remain in the existing Editor assembly; no runtime test dependency or new test framework is introduced. Mixed fixtures share helpers through partial classes.

`Artifacts/GeneratedQA` is ignored, reproducible output, not project documentation. Bot latest/history/seeds required for replay remain there. Historical QA captures do not replace a current test run.

## Boundaries

`art/` belongs to the artist and is untouched. AudioEffects is not cleaned by reference count. Production prefab/SO ownership and Resources removal are separate later steps. No production gameplay or balance change is part of this cleanup.
