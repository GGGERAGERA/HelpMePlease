# p_Boss1 rocket attack

The boss prefab is `Assets/_Project/prefabs/Enemies/p_Boss1.prefab`.
`EnemyChaseMovement`, `EnemyCollisionHandler`, `EnemyHealth` and the existing
`Boss2_0.controller` remain in use. The original prefab had animation assets
but no shooting component or animation events.

`BossRocketAttack` on the prefab root owns this small cycle:
Chasing → PreparingAttack → WaitingForImpact → Recovering → Chasing.
Chase is temporarily disabled until impact and recovery finish. Contact damage
is unchanged. The visible graphic Animator receives `Speed` from this component;
the chase component no longer references the inactive duplicate Animator with
the nonexistent `IsRunning` parameter.

## Inspector defaults

| Field | Default | Meaning |
| --- | ---: | --- |
| AttackCooldown | 6 s | Initial delay and cooldown after recovery |
| StopDuration | 0.15 s | Stationary pause before preparation |
| PreAttackDuration | 0.75 s | Minimum preparation time; also waits for the authored prepare-idle state |
| RocketFallDelay | 1.2 s | Time from actual shot to falling rocket spawn |
| RocketFallDuration | 0.4 s | Vertical descent duration |
| RecoveryDuration | 0.55 s | Stationary recovery after impact |
| RocketSpawnHeight | 14 | Minimum height above target; raised above orthographic camera if necessary |
| ExplosionRadius | 2 | World-space damage radius |
| ExplosionDamage | 25 | Damage per rocket, subject to normal player invulnerability |
| TargetPrediction | 0 s | Locked target = player position + Rigidbody2D velocity × prediction |
| MinAttackDistance | 0 | Minimum distance to begin |
| MaxAttackDistance | 30 | Maximum distance to begin |
| RocketCount | 1 | Rockets per event; larger salvos spread targets around the predicted center |

## Animation and reused visuals

The existing `PAttack` and `Attack` triggers drive the `Hands1` layer.
`animBossShoot1.anim` calls `FireRocket` at its second sprite key, 1/12 s.
`BossRocketAnimationEvents` on `graphic` forwards it to the root ability.
Only one event is accepted per attack, including when the clip loops or blends.
Other controller users without a relay may ignore the event.

The existing nested `p_fxRocketMuzzleFire` systems play on both `LH1Muzzle` and
`RH1Muzzle`. `p_fxRocket1` is reused for ascent and descent without adding physics
or a projectile script to the asset. `BossRocketAttack` moves those runtime
instances; no new falling-rocket prefab is needed. Its particle body is emitted
once in local simulation space so it stays attached to the moving transform.

`fx_BossTaget1` appears at the shot event, is scaled to the danger diameter and
kept alive through delay/descent, then removed at impact. Its position never
tracks the player after spawning. `fx_BomberExplosion` provides the impact FX.

`EnemyExplosion.Detonate` shares only overlap/damage/audio/impact FX with Bomber.
It resolves player health on parent objects and deduplicates compound colliders.
Enemy explosions retain their existing player-only damage policy. Bomber keeps
its own warning, delay, shockwave and owner-destruction logic. Boss damage occurs
on reaching the target, independently of particle lifetime.

Death, disabling the ability/boss, player loss/death and stopped/finished run flow
clear pending warnings, ascending/falling rockets and owned impact FX. No future
damage remains scheduled. Disabling the ability restores the previous chase
enabled state for a living boss.

## Quick manual check

Select a `p_Boss1` instance and set AttackCooldown to 0.5 before entering Play Mode
(or disable/re-enable the ability after changing the value during Play Mode).
Keep MinAttackDistance = 0, MaxAttackDistance = 30, RocketCount = 1. The only
periodic ability is Rocket Attack, so no probability override is needed.

Move out of the marker for one shot, stay inside for the next, then kill the boss
during preparation and during descent. Check that chase resumes after recovery,
the target stays fixed, and canceled shots leave no markers or rockets.
Restore the prefab cooldown to 6 after testing.

The focused Unity test is `BossRocketAttackTests` (EditMode runner, entering real
Play Mode). Captures and its latest check report are under `Artifacts/BossRocketSmoke`.
