# World hazards

`WorldHazardDirector`, authored on GameManager in MVP, owns phase eligibility, timers,
safe target selection and cancellation. `StageProfileData.WorldHazards` selects a preset.
`WorldHazardDefinition` creates an `IWorldHazardAttack`; new hazard types need no director changes.
The first definition, `RocketHazardDefinition`, uses the same `RocketAttackRunner` as the boss.

The runner owns fixed targets, optional ascent, warning, descent, impact and cleanup.
It pools the existing `p_fxRocket1`, `fx_BossTaget1`, `fx_BomberExplosion` assets through
`SimplePrefabPool`. Warning setup is shared with `ExplosionWarningVisual`; damage, audio and
impact remain in `EnemyExplosion`. Boss animation events, dual muzzles, target/delay sampling,
burst cycle, movement pauses, damage and serialized prefab settings remain unchanged.

The actual route has three sectors. Presets in `Assets/_Project/Data/WorldHazards`:

| Sector | First warning | Interval after impact |
| --- | --- | --- |
| 1 / Early | 45 s | 45–65 s |
| 2 / Middle | 30 s | 30–45 s |
| 3 / Late (before boss) | 20 s | 20–30 s |

Rocket hazard defaults: radius 1.5, damage 10, warning 1.75 s, descent .5 s.
Tune the definition for intensity and sector presets for timing/allowed attacks.
Unused profiles 4–10 remain unchanged. A missing preset disables hazards.

Safety: one pending attack; no catch-up volleys; fixed target outside the player's collider
bounds plus clearance; full warning visible on camera, within the playable area and clear of
solid obstacles. No safe target means skip. Runtime floors: warning 1.5 s, descent .3 s,
interval/grace 5 s. Hazard damage leaves at least 1 HP accounting for damage multipliers;
this cap does not affect boss/Bomber attacks.

Tutorial/scene transitions/non-normal phases suppress attacks. Pause/debug AI freeze suspend
progression. StopRunGameplay, disable, player loss/death and phase changes cancel pending work.
Reinitialization disposes the previous runtime; re-enable restarts grace. Particle state is
cleared on release and direction, body, smoke, marker scale/timing reset on reuse.

Verification: **Tools → Subject42 → Verify World Hazards** runs EditMode tests entering real
Play Mode. Covers production sector, repeated strikes, cancel/reinitialize/disable/re-enable,
damage and compound colliders, low-HP safety, instance-ID reuse of all three assets, and boss
animation bursts 1/3/5, dual launch and movement recovery. Results: `Artifacts/WorldHazards/results.xml`.

Latest verification (2026-09-24): all five tests passed in Unity 6000.3.13f1. Three consecutive
production-sector strikes finished without damaging the stationary player. Stop/reinitialize and
disable/re-enable restarted cleanly. The shared runner reused the same marker, rocket and impact
instance IDs through three cycles, applied one 25-damage hit per cycle despite compound colliders,
and left 1 HP with a 3x incoming-damage multiplier. Real boss bursts 1/3/5 produced two ascent
rockets per firing cycle; movement resumed while the last rockets were pending. Boss prefab and
animation assets have no diff.

Changed code: `RocketAttackRunner`, `BossRocketAttack`, `EnemyExplosion`, `ExplosiveZone`
(warning configuration extraction), `WorldHazardDefinition`, `RocketHazardDefinition`,
`WorldHazardPreset`, `WorldHazardDirector`, `StageProfileData`, `RunFlowController`.
Authored data: `MVP.unity`, `StageProfile_01/02/03`, four assets under `Data/WorldHazards`.
Verification: `WorldHazardTests`, `WorldHazardPlayTests`, `WorldHazardVerificationRunner` and results.
