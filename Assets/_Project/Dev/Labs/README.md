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

Open `Tools > Subject42 > Dev > WorldSystemsLab > Open`, then enter Play Mode.
The checked-in scene already contains its rocket/enemy asset references. If using
an old local scene copy, use `Rebuild Scene` outside Play Mode. Production scenes,
Corridor prefabs and rocket assets are unchanged.

- **F5** or the top-right Start button: start/restart, teleport to START and heal
  (also revive after a lab death). Start is immediate; no interaction or holding E.
- **F6**: select straight / L-shaped for the next run.
- **F7**: select 0 / 90 / 180 / 270 degrees for the next run; press F5 to apply.
- **F8**: toggle ordinary enemies immediately. OFF clears the lab crowd.
- **WASD / arrows**: move; **Space**: existing player dash.
- **F1**: hide/show the original left lab panel. The V2 timer stays visible.
- **Stop / clear**: cancel the event/rockets, clear enemies, restore arena size
  and center the player. The original lab Reset also restores scattered props.

Both fixed routes are 120 units long (about 20 seconds at the lab's 6 units/s,
before dodging/dashing). The deadline is 25 seconds; reaching EXIT sooner ends
successfully. Checkpoints at path distances 30, 60 and 90 are grey (inactive),
cyan (next) or green (completed). Run within 3.5 units of each node in order.
EXIT turns red to green only after all three. The panel shows time, HP, boundary
hits and enemy count; the result records completion time, checkpoints and rockets.

Pink anomalous boundaries have **no solid colliders**: normal enemies can enter
anywhere. Leaving costs 12 HP with existing hit knockback, then up to 12 HP/s
outside, subject to normal player invulnerability. Returning does not reset nodes
or grant skipped ones. A swept check catches brief corner excursions/dashes.
This is a penalty, not a guarantee against all shortcuts between ordered nodes.

V2 reuses `WorldEvent`/`WorldEventSpawner` for lifecycle and reward suppression,
`RocketForeshadow`/`RocketHazardDefinition.CreateAttack` (`IWorldHazardAttack`,
`RocketAttackRunner`, existing warning/pool/explosion) for centre, two-side gap and
left-to-right patterns, and `EnemySpawner.ConfigureDebugExplorationPressure` with
`p_Enemy_default`/its normal chase AI. The run-only `WorldHazardDirector` is not
started. The old moving `EvacuationCorridorEvent` remains available unchanged.
Only while testing V2, lab arena/bounds are scaled to 160 units and unrelated
anomalies/portals/props are cleared. Existing rewards/weapon systems are not added.

Manual checks: try both layouts/rotations; run straight to EXIT before nodes;
cut the L corner; leave and return before/after a node; dodge the centre/pair/sweep;
watch enemies cross the pink border; restart during a warning; let time expire;
restart after death. Assess time pressure and rocket difficulty in Play Mode.
No automated combat/bot batches are needed for this prototype.
