# Enemy Gallery

Open `Assets/_Project/Scenes/EnemyGallery.unity` directly and press Play.
Move with WASD/arrows, zoom with the gameplay mouse wheel, toggle enemy labels with F1.
There is no run bootstrap, HUD, weapon loadout, rewards or progression.

## Current content audit (2026-09-10)

All prefab assets in Assets were inspected for EnemyHealth/CharacterMovement2D,
including inherited components. References were traced through current spawn profiles,
stage profiles, enabled production scenes and their prefab dependencies.

| Enemy prefab | Path relative to Assets/_Project | Production reference |
| --- | --- | --- |
| p_Boss1 | prefabs/Enemies/p_Boss1.prefab | VerticalSlice/StageProfiles/StageProfile_01–10 |
| p_EnemyEye1 Variant | prefabs/Enemies/p_EnemyEye1 Variant.prefab | ESP_Level_02–10, MVP |
| p_EnemyTurret1 | prefabs/Enemies/p_EnemyTurret1.prefab | ESP_Level_03–10, MVP, FalseSignalEvent |
| p_Enemy_Bomber | prefabs/Enemies/p_Enemy_Bomber.prefab | ESP_Level_01–10 |
| p_Enemy_Shooter | prefabs/Enemies/p_Enemy_Shooter.prefab | ESP_Level_01–10 |
| p_Enemy_classic | prefabs/Enemies/p_Enemy_classic.prefab | ESP_Level_01–10 |
| p_Enemy_default | prefabs/Enemies/p_Enemy_default.prefab | ESP_Level_06–10 |

Excluded: `p_Enemy` is an inheritance base with no movement/active animation;
`p_Enemy1 Variant Mos` has no AI or references; `EnemyDebugTest` is an unused group
of test shooters. The two `locationElements` turret variants have their health and
AI disabled and are bunker decorations. Robot prefabs have no enemy health/AI.
Prototype visual copies and capsule/player displays are not enemy archetypes.

## Isolation and presentation

Enemies remain linked production prefab instances. Scene-only removal overrides
remove EnemyMovement subclasses, TurretEnemyBehaviour, EnemyCollisionHandler and
EnemyHealth. Physics simulation and enemy colliders are disabled. Removing contact
handlers matters because Unity can deliver physics callbacks to disabled behaviours;
disabling a health behaviour also does not disable its public TakeDamage method.
No production combat code or prefab asset is modified.

Original visual hierarchies, sprites, materials, scales and controllers are retained.
The scene controller starts the existing animated idle state, or the boss's existing
walk state in place. Animator root motion and animation events are locally disabled.

**Turret exception:** its active production model has no Animator; its old animated
model is an inactive child. The old model stays inactive. With the user's approval,
Gallery slowly sweeps the actual production aimPivot by ±25 degrees, independent
of player position. Thus six enemies have changing production sprite frames and the
turret has procedural articulation. No artificial Animator or replacement art is added.

Player: `Resources/OrbitalStation/Authored/Player_0_p_Player3.prefab`, resolved through
the same OrbitalPresentationConfig mapping as CharacterSpawner for `01_Gera`.
The authored ORBITAL subtree is locally inactive. EnemySpawner, PlayerHealth,
PlayerPickupRadius, PlayerCombatModifiers, PlayerInteractor and PlayerHitSound are
removed only from this scene instance. Movement, animation and player visuals remain.
The camera copies current MVP Camera/CameraFollow settings with an explicit player
target. Its neutral clear colour provides the floor; simple colliders bound the room.

## Maintenance

`Tools > Subject42 > Refresh Enemy Gallery` re-reads existing EnemySpawnProfile and
StageProfileData assets plus enabled production scene dependencies. It keeps existing
instance positions/labels, adds missing instances and removes obsolete entries without
duplicates. It saves only the Gallery scene, never production prefab assets.
New unknown behaviour components cause an explicit authoring error for review instead
of silently being allowed to attack. Refresh requires Edit Mode. The current room is
authored for the current lineup; expand its boundaries when adding further rows.

`Tools > Subject42 > Validate Enemy Gallery (3 minute Play Mode)` verifies linked
prefabs in Edit Mode, then samples actual sprite frames, visual scales, root positions,
turret articulation and combat isolation for 180 seconds beside successive exhibits.
It writes reports and gameplay-camera PNGs under `Artifacts/EnemyGallery` and returns
to Edit Mode. Save any scene edits before running this opt-in check.

## Validation recorded

- Enemy prefabs found: 7; Enemy prefabs displayed: 7.
- 180.1 seconds in Play Mode beside the exhibits: all roots stationary, all sprites
  visible, no deaths/despawns/projectiles/weapons/spawners/run progression.
- Production root and every visual-child scale matched each source prefab.
- Actual sprite-frame signatures changed: Boss 4, Eye 3, Bomber/Shooter/classic/default
  4 each. Turret's approved production pivot sweep also changed during the run.
- Console during the soak: 0 errors, 0 warnings.
- Two successive Refresh commands retained the same instance identities and layout.
- A separate interactive Play Mode run received real keyboard input: player moved
  1.284 units, 9 distinct player sprite frames were observed, and F1 hid/restored names.
  Camera target remained the real player. The temporary observer was removed afterward.
- Reports and seven gameplay-camera captures: `Artifacts/EnemyGallery`.
