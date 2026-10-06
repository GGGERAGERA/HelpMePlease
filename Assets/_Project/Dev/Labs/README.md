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

### Corridor V2 prefab prototype

All Corridor V2 changes stay in WorldSystemsLab / Dev. Production Corridor, MVP,
the World Event catalog and shared rocket assets are unchanged.
Open Tools > Subject42 > Dev > WorldSystemsLab > Open, then enter Play Mode.

- F5 starts/restarts, heals/revives and teleports to START (explicit dev command).
- F6 cycles Straight / L / Zigzag / Random for the next run. Default is Random.
- F7 rotates the next route by 90 degrees; F8 toggles the ordinary enemy crowd.
- F9 only spawns an independent Stasis territory at world origin; it never starts an event.
  Then separately choose Straight with F6 and start Corridor with F5 for guaranteed overlap.
- WASD/arrows and Space use normal movement/dash; F1 toggles the left Lab panel.

Before this structural pass, presentation constructed floor/wall objects and edge
geometry, line visuals, gates and TMP labels, Exit, collapse/trail and IMGUI gameplay
HUD at runtime. The event template was also constructed at runtime. Those paths
have been replaced; the small IMGUI Lab controls remain separate from gameplay HUD.

Authored assets are in WorldSystemsLab/CorridorKit:
PF_CorridorSegment_Straight, PF_CorridorSegment_Corner, PF_CorridorSegment_Cap,
PF_CorridorGate, PF_CorridorExit, PF_CorridorCollapseFront, PF_CorridorReclaimed,
PF_CorridorHUD and PF_CorridorV2Event. CorridorV2Kit.asset links them. Gates have
inactive/active/completed visual groups; Exit has locked/final/open groups.
CorridorV2NodeView only switches and pulses those authored groups. CorridorV2HudView
fills the two authored TMP fields; it never constructs a layout. Redraw the prefabs
without changing route or event logic. Explicit authoring menus rebuild initial
assets and should only be used intentionally because they overwrite visual edits.

CorridorRoutes contains editable CorridorV2Straight/L/Zigzag route definitions.
Each defines START, checkpoint segment endpoints and EXIT in local coordinates.
The default has three 35-unit checkpoint segments and a 24-unit final segment,
129 units total. F7 rotates the resulting route. Random chooses fresh cardinal
turns on each launch, rejects U-turns and overlapping nonadjacent segments, and
uses isolated System.Random. Set routeSeed to a nonzero value for reproducible runs.
Authored presets remain available for comparisons. The scene authoring tool loads
these same definitions and kit references.

Runtime still computes route sampling/projection, ordered physical gate crossing,
collapse progress/damage, final-push state, strike scheduling and prefab placement,
scaling/activation. It creates prefab instances rather than their visual hierarchy.
Solid BoxCollider2D walls are authored inside modules; instance layer overrides block
only the player's solid collider layers. Enemies pass both ways and the global
physics matrix stays unchanged. Gates are two units before bends and must be
crossed forwards through the opening. Exit requires all gates, the final delay
and an actual forward crossing of its plane. Time alone never completes the event.

After checkpoint one, the authored collapse front continuously follows the route.
Authored red trail modules stretch behind it. Reclaimed space applies periodic
small damage/knockback; camping beyond a completed checkpoint is unsafe too.
FINAL PUSH accelerates collapse and strike spacing for at least 3.5 seconds.
The compact pixel sci-fi HUD shows CORRIDOR / CHECKPOINT n / 3, then
FINAL PUSH / REACH EXIT. Exit changes red to yellow to green and pulses on crossing.

SINGLE, SIDE GAP, CROSS BLOCK and CHASE retain the existing RocketAttackRunner,
shared authored warning prefab, pooled rocket, explosion and damage. Shared rocket
FX internals remain unchanged. There are no separate Corridor debug circles.

Starting Corridor preserves existing anomaly sites, props, portals and world rules.
Route placement does not inspect anomaly sites or move to their centers. Authored presets
and Random can naturally intersect territories already present in the world.
F9 adds a normal Stasis site through the existing anomaly initializer, independently
of Corridor ownership. Its existing visuals, collider and effect remain active.
Corridor cleanup destroys only its event/modules/HUD, pending strikes and its own
enemy crowd. It does not clear anomaly sites or their effects/lifecycle. Global Lab
reset/clear-anomalies controls still have their explicit original responsibilities.

Normal success, death and cancel do not write the player's transform or body position.
The player stays where gameplay ended and control continues. The enlarged playable
Lab area remains after a normal result so the Exit is still navigable; explicit Lab
reset or the next F5 restores the previous extent before starting. F5 may teleport
and revive because it is a dev restart command.

Corridor Tuning on WorldSystemsLabController exposes route preset/seed/definitions,
checkpoint count, segment and exit length, width, collapse speed/damage/interval,
strike interval/telegraph/fall/damage/radius, chase spacing, final push duration and
multipliers, plus SFX hooks. Values are snapshotted at F5. Scene Inspector edits
persist; Play Mode edits apply to the next run.

Targeted verification uses CorridorV2RouteAuthoringTests, CorridorV2StructuralTests
and CorridorV2Tests, with evidence under Artifacts/GeneratedQA/CorridorV2. It covers
presets/random seeds/rotation, real player/dash blocking and normal enemy chase,
physical gates/Exit, pressure, four strikes, full traversal, cleanup/restart and
actual Stasis effect during/after overlap. Traversal steering uses extra health
because it does not dodge; assess combat feel manually. Golden Path, Batch Runner
and standalone build are not run.


Presentation refresh:
- Gate/Exit now use armored pixel pylons, induction forks and a continuous energy
  membrane. Cyan Active has a restrained energy-only pulse and a forward flow shape;
  Inactive is dim and Completed uses quiet stable green. Pylons never pulse/stretch.
- PF_CorridorGate separates Gameplay/Trigger + crossing plane/forward anchor from
  Presentation/LeftPylon/RightPylon/EnergyField with three authored FX states.
  Existing CorridorV2Route validates order and forward crossing; the trigger volume
  records the same plane/opening without adding a second event or changing gameplay.
  Editing art under Presentation leaves crossing data and physical walls intact.
- Removed old square pedestals, dotted guides/barrier cells, procedural texture
  samplers and loose pixel-shards. Deleted unused CorridorPixel.png/CollapsePixels.png.
  Art/*.svg are static layered pixel artwork sources; Art/*.png are point-filtered
  sprite exports, editable/replacable independently of gameplay. No image-model art
  or runtime sprite construction is used. The shared Orbital Pixel is retained for
  the functional reclaimed-area tint; no shared project artwork was deleted.
- Walls use an authored tiled armor rail while retaining their original physics.
  The dark corridor floor overlay was removed: props, zones and the existing world
  remain visible under the event. No separate arena or anomaly content is spawned
  by Corridor; only Lab's existing playable extent can expand for long routes.
- F9 is a standalone Lab world-territory command. It uses the normal Stasis/site
  initializer and creates a scene-root site with no Corridor parent or owner.
  F5 does not query anomaly types, effects or positions. Corridor cleanup only
  removes its own objects/crowd/strikes; independent territories keep their lifecycle.
Presentation refresh verified 2026-10-07: 6 route + 5 structural + 2 ownership regression + 8 gameplay tests passed.
Visual screenshots cover Active/Completed in all four presets and all four quarter-turns.
Default traversal remains 21.7 seconds; Stasis effect remains 0.65 after cancellation.

F5 uses the existing concurrent debug spawn API and preserves foreign Lab events.
If independent territory bootstrap initialization is rejected, only that newly created
site and its zone are removed via RemoveForLayout; no orphan zone is left behind.

## Corridor production runtime

F5/F6/F7/F8/F9 use `PF_CorridorEvent` and `Data/WorldEvents/Corridor/CorridorConfig.asset`. Lab owns only bootstrap, route input overrides and controls; route/gates/collapse/strikes/views live in production. F9 anomaly territory is independent and survives Corridor cleanup.
