# Development labs

Open an individual Lab scene directly and press Play. World systems are in
`WorldSystemsLab/WorldSystemsLab.unity`; ORBITAL labs remain available from the existing menu.
These are isolated Editor/development scenes, intentionally absent from production Build Settings.

**WASD:** move. **Mouse wheel:** production camera zoom. **F1:** hide/show the panel. **Esc:** cancel reward placement back to its cards.
Panels use tabs and scroll on smaller Game Views; scrolling the panel does not zoom the camera.

## OrbitalRewardLab

- Starts from production `OrbitalRunState.CreateDefault`: Core 0, one ring, one built mount (capacity 3), Pistol.
- **OPEN NORMAL REWARD:** real `UpgradeManager` choices, production cards, ring/mount selection and attachment.
- **OPEN RANDOM REWARD:** one randomly selected eligible production reward, still presented as a real card.
- **GIVE 3 / 10 REWARDS:** instant eligible grants, without cards; the status reports the number applied.
- **Direct Give:** generated from `OrbitalRewardProvider` definitions: Pistol, Laser Sword, Impulse Gun, Arc Emitter, Link Pair, Ring Speed/Power/Capacity, Add Mount, Core Upgrade, Link Matrix, Max Health, Move Speed, Module Damage, New Ring.
- The selected R1/R2/etc. is the direct ring-upgrade target. Modules use a free mount on that ring first, then other rings. Link Pair installs two nodes atomically.
- **Build / Orbit:** Clear Modules, Add Ring, Add Mount, Core +1, Remove Last Module, Remove Last Ring, Many Rings, Compress/Release, Reverse, Pause/Resume Rotation.
- **Live state:** core, ring/mount/module counts, modules per ring, reward/placement mode and Link Pair state.
- **RESET BUILD:** cancels cards/flights, removes the old player, station and loose runtime effects, clears body upgrades/history, and recreates the baseline without leaving Play Mode. Production caps/target validation remain in effect; rejected grants show a status message.

## EnemyOrbitalLab

- Starts empty, with the same production player/ORBITAL baseline, a neutral 60×60 arena and production camera.
- **Spawn:** select a real prefab, Spawn 1/5/10/25, Spawn All Types, Clear Enemies, Reset Enemies; Around Player, Line, Cluster, Random Arena layouts.
- **RESET ENEMIES:** recreates the accumulated spawn layout at its original positions and full HP, keeping current AI/speed/invincibility settings. Clear Enemies also forgets that layout.
- **AI / Health:** Freeze/Resume AI, speed ×0.5/×1/×2, player/enemy invincibility, Heal All Enemies, Kill All Enemies.
- **Build / Orbit:** Starter, Pistol, Laser Sword, Impulse, Arc, Mixed, Many Rings, plus the shared rotation/compression controls. Presets reset the player/build and clear enemies; spawn targets afterwards. Many Rings uses the production cap of 8 rings with 24 modules.
- **RESET LAB:** clears enemies, projectiles/effects and the saved spawn layout; restores the player/build; resets speed to ×1, resumes AI and disables both invincibility flags. Reset remains accessible after player death.
- Enemy invincibility preserves damage numbers/hit FX. Bomber still explodes but survives its own explosion while invincible. Kill All explicitly overrides enemy invincibility for the current targets.

## Production assets and implementation

`OrbitalLabsAuthoring.FindProductionEnemies` discovers enabled non-boss enemies in MVP's asset dependencies. The serialized list can be updated with **Tools → Subject42 → Dev → OrbitalLab → Refresh production enemy list**; unused gallery variants are not enumerated separately.

Current references, all under `Assets/_Project/prefabs/Enemies/`:

- `p_Enemy_classic.prefab` (Basic)
- `p_Enemy_default.prefab` (Elite)
- `p_Enemy_Shooter.prefab`
- `p_Enemy_Bomber.prefab`
- `p_EnemyEye1 Variant.prefab`
- `p_EnemyTurret1.prefab`

Added scripts: `OrbitalLabSession`, `OrbitalRewardLabController`, `EnemyOrbitalLabController` in `Dev/Debug/OrbitalLab`, and `Dev/Editor/OrbitalLab/OrbitalLabsAuthoring.cs`.

Reuses production player/presentation prefabs, `PlayerLoadoutFactory`, `RunStateManager`, `OrbitalStationRuntime`, `OrbitalRewardProvider`, `UpgradeManager`, `UpgradePanelView`, `CameraFollow`, enemy prefabs/AI/health, `EnemyDebugAiFreeze`, and `CombatFeelTestDummy` for invulnerability/reward suppression. The legacy spawner embedded in the production player is removed only from the Lab's runtime instance.

Small shared fixes: development controls feed existing ORBITAL compression/direction logic; cancelling idle reward queues clears stale displayed choices; Bomber self-destruction respects the existing development invulnerability marker. No separate weapons, enemy variants, run/progression framework, bot or batch runner is added.

## Verification (Unity 6000.3.13f1)

Both scenes were opened in the running Editor and exercised in actual Play Mode. Game View captures were visually inspected. Most actions were invoked individually through controller/production debug APIs using a temporary Editor inspection helper, removed before delivery; this was not an exhaustive physical click-through of every button. Esc and F1 were sent as real keyboard input.

- Reward: baseline, normal cards → Add Mount ring selection, Laser Sword card → free mount/flight, rejection of an occupied mount without consuming the reward, Esc back to cards, direct Impulse/Add Mount/New Ring/Core/HP/speed, two-stage Link Pair, bulk 3/10, random reward, reset and 8-ring/24-module build.
- Reset: also exercised during Link Pair flight; afterwards one player/station, valid baseline state, idle reward queue, empty choices/history, 100 HP and no legacy spawner; new rewards can open again.
- Enemy: all six production prefabs/Spawn All, 5-enemy Line and 10-enemy Cluster, Freeze/Resume, speed settings, player/enemy invincibility (including repeated Bomber explosions), ARC/Sword/Impulse/Mixed presets, kill/clear, layout reset and full Lab reset.
- ORBITAL: compressed radius 2 → 0.6 → 2, frozen phases while rotation paused, and resumed movement with reversed direction. F1 hides the panel.
- Scripts compiled in Unity; no errors/exceptions observed in the final Play Mode passes. No new per-button automated tests were added.

## WorldSystemsLab

This lightweight scene uses the production World Rule, anomaly territory,
World Event, portal, prop scatter, player, camera, and tactical-map code. It has
no EnemySpawner by default, Threat, XP/reward flow, boss, ORBITAL, or combat HUD. The panel
can switch every authored World Rule, spawn/clear normal and special anomaly
territories, spawn/clear each production Event, manage a production portal pair,
reset the Lab, center the player, and toggle the map. Carrier Hunt is explicitly
labelled `preview only`: it creates the real authored event and initial visual,
but never starts or creates an enemy carrier.

### Corridor V2 gameplay prototype

Only `WorldSystemsLab` is changed. Production Corridor, MVP, World Event catalog,
other events and shared rocket assets remain unchanged.

Open `Tools > Subject42 > Dev > WorldSystemsLab > Open`, then enter Play Mode.
The checked-in scene includes all references; `Rebuild Scene` preserves the same defaults.

- **F5**: start/restart, heal/revive and teleport to START.
- **F6**: next-run preset Straight / L / Zigzag.
- **F7**: rotate next run 0 / 90 / 180 / 270 degrees.
- **F8**: toggle ordinary map enemies; OFF clears the lab crowd.
- **WASD/arrows**, **Space**: normal movement and collision-aware dash.
- **F1**: hide/show the original left lab controls.
- Reset through the original lab panel; completion/death also clear temporary
  enemies, walls, visuals and pending strikes, restoring original area/bounds.

Before this pass V2 had 120-unit Straight/L routes, three fixed proximity nodes,
visual-only damaging boundaries, three rocket patterns, a 25-second survival
cutoff and a large debug panel. There was no collapsing rear front.

`CorridorV2Route` now builds cardinal polyline presets from tuning. Default is
five ordered gates, 21 units per checkpoint segment plus a 24-unit exit segment
(129 units total). Gates are two units before bends: physically cross the NEXT
cyan gate forwards through its central opening, with no E. Out-of-order crossings,
reverse crossings, teleports and chords outside the route grant nothing. DONE gates
turn green with a short pulse, camera punch and configurable SFX hook.

After the first checkpoint, a bright pulsing jagged magenta front continuously
advances along the route. A red trail marks reclaimed space. Camping just after a
checkpoint is unsafe too. Behind the front, periodic small damage and existing
knockback apply; there is no invisible instant death. After the final gate, FINAL
PUSH lasts at least 3.5 seconds, accelerating collapse and shortening strike spacing.
EXIT changes from locked red to yellow (final) to pulsing green. Success requires
all gates, the final phase, and physically reaching EXIT; elapsed time alone never
completes or fails the event. Completion has a brief exit pulse/SFX/punch.

Boundaries are the exposed union of segment capsules. Their EdgeCollider2D overrides
include only the player's solid-collider layers and exclude everything else, with
priority 100. Ordinary enemies retain their existing Enemy-layer colliders and chase
AI, passing both ways. No global physics matrix, enemy movement or difficulty edits.
The player's existing dash checks the same collision pair rules.

`CorridorV2Strikes` uses existing `RocketAttackRunner`, warning visual, pooled rocket,
explosion and damage. The first segment is quiet; subsequent deterministic patterns:

- SINGLE: one warning ahead on the route.
- SIDE GAP: two warnings with a safe middle passage.
- CROSS BLOCK: adjacent warnings on alternating sides, leaving the other side free.
- CHASE: three staggered warnings from behind towards the player's sampled position.

Every strike has a warning, delay, falling rocket and impact FX; no random spam.
Radius is limited relative to width so SIDE GAP remains passable when tuning narrows
the route. The short impact shake only fires on impact, not throughout the event.

Expand **Corridor Tuning** on `WorldSystemsLabController` in the scene Inspector.
Parameters are snapshotted at F5; scene Inspector edits persist, Play Mode edits are
for the next run. Core tuning: `routePreset`, `checkpointCount`, `corridorWidth`,
`segmentLength`, `collapseSpeed`, `collapseDamage`, `strikeInterval`,
`strikeTelegraphTime`, `finalPushDuration`, `finalCollapseMultiplier`. Also exposed:
`exitSegmentLength`, `collapseDamageInterval`, `strikeFallTime`, `strikeDamage`,
`strikeRadius`, `chaseSpacingTime`, `finalStrikeIntervalMultiplier`, font and SFX hooks.

Prototype responsibilities are separate: settings, route/order, event lifecycle and
pressure, presentation/walls, strike schedule, lab controls. Procedural floor/walls,
gates/TMP labels, collapse and compact IMGUI HUD remain lab-only; the transient event
template is registered by the existing spawner, not added to the production catalog.

Targeted verification: `CorridorV2Tests` only, results/screenshots under
`Artifacts/GeneratedQA/CorridorV2/`. Covers every preset, ordered physical gate
crossings, locked Exit, real player/dash blocking and normal enemy chase inward/outward,
pressure damage, all four strike telegraphs, one default-speed traversal with ordinary
enemies, Final Push, completion/reset/death cleanup and restart. The traversal smoke
uses additional health because deterministic steering does not dodge; assess combat
feel manually. Golden Path, Batch Runner and standalone build are not run.
