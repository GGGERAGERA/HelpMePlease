# ORBITAL development labs

Open either scene directly and press Play, or use **Tools → Subject42 → Orbital Labs → Open OrbitalRewardLab / Open EnemyOrbitalLab**.
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

`OrbitalLabsAuthoring.FindProductionEnemies` discovers enabled non-boss enemies in MVP's asset dependencies. The serialized list can be updated with **Tools → Subject42 → Orbital Labs → Refresh production enemy list**; unused gallery variants are not enumerated separately.

Current references, all under `Assets/_Project/prefabs/Enemies/`:

- `p_Enemy_classic.prefab` (Basic)
- `p_Enemy_default.prefab` (Elite)
- `p_Enemy_Shooter.prefab`
- `p_Enemy_Bomber.prefab`
- `p_EnemyEye1 Variant.prefab`
- `p_EnemyTurret1.prefab`

Added scripts: `OrbitalLabSession`, `OrbitalRewardLabController`, `EnemyOrbitalLabController` in `scripts/Debug/Labs`, and `Editor/OrbitalLabsAuthoring.cs`.

Reuses production player/presentation prefabs, `PlayerLoadoutFactory`, `RunStateManager`, `OrbitalStationRuntime`, `OrbitalRewardProvider`, `UpgradeManager`, `UpgradePanelView`, `CameraFollow`, enemy prefabs/AI/health, `EnemyDebugAiFreeze`, and `CombatFeelTestDummy` for invulnerability/reward suppression. The legacy spawner embedded in the production player is removed only from the Lab's runtime instance.

Small shared fixes: development controls feed existing ORBITAL compression/direction logic; cancelling idle reward queues clears stale displayed choices; Bomber self-destruction respects the existing development invulnerability marker. No separate weapons, enemy variants, run/progression framework, bot or batch runner is added.

## Verification (Unity 6000.3.13f1)

Both scenes were opened in the running Editor and exercised in actual Play Mode. Game View captures were visually inspected. Most actions were invoked individually through controller/production debug APIs using a temporary Editor inspection helper, removed before delivery; this was not an exhaustive physical click-through of every button. Esc and F1 were sent as real keyboard input.

- Reward: baseline, normal cards → Add Mount ring selection, Laser Sword card → free mount/flight, rejection of an occupied mount without consuming the reward, Esc back to cards, direct Impulse/Add Mount/New Ring/Core/HP/speed, two-stage Link Pair, bulk 3/10, random reward, reset and 8-ring/24-module build.
- Reset: also exercised during Link Pair flight; afterwards one player/station, valid baseline state, idle reward queue, empty choices/history, 100 HP and no legacy spawner; new rewards can open again.
- Enemy: all six production prefabs/Spawn All, 5-enemy Line and 10-enemy Cluster, Freeze/Resume, speed settings, player/enemy invincibility (including repeated Bomber explosions), ARC/Sword/Impulse/Mixed presets, kill/clear, layout reset and full Lab reset.
- ORBITAL: compressed radius 2 → 0.6 → 2, frozen phases while rotation paused, and resumed movement with reversed direction. F1 hides the panel.
- Scripts compiled in Unity; no errors/exceptions observed in the final Play Mode passes. No new per-button automated tests were added.
