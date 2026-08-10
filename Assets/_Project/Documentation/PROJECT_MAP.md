# Subject#42 — Production Project Map

## Product flow

```text
MainMenu / Bunker
  → loadout and optional stabilizer
  → Sector 1–4: Exploration
      → 3 Normal regions + 1 Special region
      → optional anomaly-site objectives and rewards
      → physical Exit available from sector start
      → session-wide Threat continues across sectors
  → Sector 5: Boss
  → victory or defeat
  → Bunker
```

The shipped route is defined by `RunRoute`: four exploration sectors and one
final boss sector. The same `MVP` scene is reloaded between sectors; persistent
run state lives in `RunStateManager`.

## Scenes and entry points

Build Settings contain exactly:

1. `Assets/_Project/Scenes/MainMenu.unity`
2. `Assets/_Project/Scenes/MVP.unity`

`MainMenu` also hosts the Bunker. `BunkerRunStarter` creates a new run from
the selected character, weapon and optional anomaly stabilizer, then loads
`MVP`.

`LevelModifiersApplier` is the gameplay-scene composition root. It applies
the current `RunSector`, World Rule and stage data, then starts either:

- `ProductionExplorationSectorController` for Sectors 1–4;
- the existing boss lifecycle for Sector 5.

## Exploration sectors

`ProductionExplorationSectorController` is the only exploration
implementation.

- It creates four large asymmetric regions covering 85–95% of playable bounds
  (target/fallback around 89%).
- Three regions are Normal; one distant region is Special.
- The path toward the Special region is deliberately allowed to cross Normal
  territory.
- Player start, region geometry and Exit are randomized with validation and a
  deterministic fallback.
- Exit placement is independent from region membership and is available from
  the beginning; site completion is optional.

`ExplorationSectorConfig` owns the anomaly pools, event prefabs, Special power
pool, damage tuning, exit radius and Threat config.

### Normal sites

`ProductionAnomalySite.InitializeNormal` combines an existing production
local anomaly (Stasis, Berserk or Glitch) with a production event selected from
Hold/Capture, Evacuation Corridor and False Signal. Completing the objective
collapses the site and produces the standard numeric-upgrade chest flow.

### Special site

Exactly one Special site is created from the available production pool:

- Gravity → `GravityZone` + `GravityTrajectoryService` integration;
- Electric → `ProductionElectricSiteHazard`;
- Beam → `ProductionBeamSiteHazard`.

Completion grants the corresponding session power through
`RunStateManager.TryAddAnomalyPower` and
`AnomalyPowerRuntime.EnsurePower`: Gravity Orb, Arc Node or Red Beam.
The runtime power layer is independent from ordinary upgrade slots.

### Exit

`ProductionSectorExit` is a physical interaction target. It saves the current
scene/run state, advances to the next sector and reloads `MVP`. The Sector 4
exit leads to Sector 5. It does not require all anomaly sites to be completed.

## Threat and enemies

`RunThreatController` reads `RunThreatConfig`, advances the session value in
`RunStateManager` and applies the active preset to `EnemySpawner`.
Threat elapsed time and value persist between exploration-sector reloads;
transitioning sectors does not reset them or add a fixed transition bonus.
Presets control phase/composition, spawn interval, batch and live-enemy cap.

Enemy damage still terminates in `EnemyHealth.TakeDamage`; deaths feed the
normal kill, XP, loot, unlock and boss callbacks.

## Run state and progression

`RunStateManager` is the DDOL owner of:

- current `RunSector` and completed-sector guard;
- player HP, XP and level;
- ordinary run upgrades and item slots;
- up to three unique `AnomalyPowerType` values;
- Threat value and elapsed time;
- run statistics used by result/victory flows.

`RunFlowController` owns sector completion and the Sector 5 boss-defeat
lifecycle. `RunEndService` and `GameOverManager` close the run and return to
the Bunker.

## World systems

- `WorldRuleController` applies one global `WorldRuleData`.
- `LevelAnomalyController` owns production local anomaly implementations and
  their geometry.
- `WorldEventSpawner` owns production event lifecycle and markers.
- Current site-compatible events are `CaptureZoneEvent`,
  `EvacuationCorridorEvent` and `FalseSignalEvent`.
- `RescueCapsuleEvent` remains a separate production/legacy candidate and is
  not part of this cleanup.

World Rules, local anomalies and World Events are separate systems even when a
site composes them into one encounter.

## Player, combat and rewards

`CharacterSpawner` creates the selected character and weapon and restores
run state. Weapons use `WeaponData`, `BaseWeapon` and their projectile/beam
fire behaviours. Ordinary upgrades continue through
`UpgradeManager → RunItemSlots → UpgradeApplier`.

Anomaly powers are implemented only by the production
`AnomalyPowerRuntime`; there is no parallel Sandbox power implementation.

## HUD and navigation

`HUDManager` is the production HUD root. Exploration presentation includes:

- route progress for Sectors 1–5;
- compact Threat status;
- site/event objective state;
- physical Exit guidance;
- tactical map generated from production site and exit geometry.

Runtime menu canvases use overlay sorting so world labels and diagnostics do
not cover core combat HUD.

## Production debug tooling

`Subject42DebugMenu` is the F1 production menu in Editor/Development builds.
Its production tabs retain:

- run, Bunker, World Rule, anomaly, enemy and event diagnostics/actions;
- weapon, upgrade and `WeaponCoreDebugSelector` controls;
- `TelekinesisDebugPrototype` controls;
- `ProductionSectorDebugController` controls for invulnerability,
  readability presets, decor brightness, anomaly emphasis, enemy readability,
  Gravity/Electric/Beam Special override, sector rebuild and diagnostics.

`MvpCameraComparisonDebugController`, `UnlockDebugHotKeys` and
`DebugGoldCheat` remain separate useful debug utilities.

## Intentional boundaries

The project has one production implementation for exploration, anomaly sites,
Special hazards and anomaly powers. There is no GameplaySandbox scene or
Sandbox controller package.

The following remain intentionally pending a separate risk decision:

- `TelekinesisDebugPrototype`, `WeaponCoreDebugSelector`;
- `NoDamageChallenge`, `WorldAccelerationRule`;
- `MainMenuController`, `DebugSaveResetButton`, `UISoundPlayer`;
- football authoring/test scripts;
- `RescueCapsuleEvent`, `DoorInteractable`, `RunLevelManager`.

They are not described here as core owners unless listed in the production
sections above.
