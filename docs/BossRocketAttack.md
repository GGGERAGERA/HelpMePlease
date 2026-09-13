# p_Boss1 rocket attack

The boss prefab is `Assets/_Project/prefabs/Enemies/p_Boss1.prefab`.
`EnemyChaseMovement`, `EnemyCollisionHandler`, `EnemyHealth` and the existing
`Boss2_0.controller` remain in use. The original prefab had animation assets
but no shooting component or animation events.

`BossRocketAttack` on the prefab root owns this small cycle:
Chasing → PreparingAttack → Firing → BetweenShots → PreparingAttack (repeat),
then Recovering → Chasing after the last shot animation exits.
Chase stays enabled and owns a bounded stationary pause throughout the burst.
`EnemyChaseMovement` publishes its MovePosition command's speed to Animator `Speed`;
the ability never controls Walk/Idle or writes Rigidbody velocity. Contact damage
is unchanged. Recovery and chase allow movement even with rockets still pending.

## Inspector defaults

| Field | Default | Meaning |
| --- | ---: | --- |
| AttackCooldown | 6 s | Initial delay and cooldown after recovery |
| ShotsPerBurst | 3 | 1–5 animation cycles; each fires exactly two rockets, LH + RH |
| DelayBetweenShots | 0.275 s | Stationary gap after one Attack exits, before the next preparation |
| StopDuration | 0.15 s | Stationary pause before preparation |
| PreAttackDuration | 0.75 s | Minimum preparation time; also waits for the authored prepare-idle state |
| RocketFallDelayMin / Max | 0.35 / 1.8 s | Independently sampled once per rocket at launch |
| RocketFallDuration | 0.4 s | Vertical descent duration |
| RecoveryDuration | 0.55 s | Moving recovery after the last Attack exits |
| RocketSpawnHeight | 14 | Minimum height above target; raised above orthographic camera if necessary |
| ExplosionRadius | 2 | World-space damage radius |
| ExplosionDamage | 25 | Damage per rocket, subject to normal player invulnerability |
| TargetPrediction | 0 s | Locked target = player position + Rigidbody2D velocity × prediction |
| MinAttackDistance | 0 | Minimum distance to begin |
| MaxAttackDistance | 30 | Maximum distance to begin |
| TargetSpreadRadius | 3.25 | Independent offsets around predicted player position, with separation of each pair |

## Animation and reused visuals

The existing `PAttack` and `Attack` triggers drive the `Hands1` layer.
`animBossShoot1.anim` calls `FireRocket` at its second sprite key, 1/12 s.
`BossRocketAnimationEvents` on `graphic` forwards it to the root ability.
Only one event is accepted per attack and only in the shoot state (or its incoming
transition). That event creates both hand launches. Prepare/shoot clips do not loop;
the shoot exit waits for its full duration. Existing locomotion transitions now use
Speed alone: the previous IsRunning condition described aggro distance, not motion.
Other controller users without a relay may ignore the event.

The existing nested `p_fxRocketMuzzleFire` systems play on both `LH1Muzzle` and
`RH1Muzzle`. `p_fxRocket1` is reused for ascent and descent without adding physics
or a projectile script to the asset. `BossRocketAttack` moves those runtime
instances; no new falling-rocket prefab is needed. Its particle body is emitted
once in local simulation space so it stays attached to the moving transform.

`fx_BossTaget1` appears at the shot event, is scaled to the danger diameter and
kept alive through delay/descent, then removed at impact. Its position never
tracks the player after spawning. Each rocket owns a marker, fixed target, elapsed
time, sampled delay and fall duration; rockets continue updating during every burst
state, recovery and chase. `fx_BomberExplosion` provides the impact FX.

`EnemyExplosion.Detonate` shares only overlap/damage/audio/impact FX with Bomber.
It resolves player health on parent objects and deduplicates compound colliders.
Enemy explosions retain their existing player-only damage policy. Bomber keeps
its own warning, delay, shockwave and owner-destruction logic. Boss damage occurs
on reaching the target, independently of particle lifetime.

Death, disabling the ability/boss, player loss/death and stopped/finished run flow
clear pending warnings, ascending/falling rockets and owned impact FX. No future
damage remains scheduled. Cancel/disable releases the pause, while a per-stage
watchdog also releases it if animation completion or an event never arrives.

## Quick manual check

For a playable dodge arena, open `Assets/_Project/Scenes/Dev/BossPractice.unity`
or use **Tools → Subject42 → Open Boss Practice**, then enter Play Mode.
Click Game View: WASD/arrows move, Space dashes, R restarts with full health.
The arena uses the production player movement/health and current boss prefab,
with no waves, orbital weapons, progression or production scene routing.
Boss settings are inherited from the prefab. The camera follows the player;
the overlay shows HP, attack state and pending rockets. The scene is kept out
of Build Settings. `BossPracticeTests` checks the authored tuning and playable setup.

Select a `p_Boss1` instance and set AttackCooldown to 0.5 before entering Play Mode
(or disable/re-enable the ability after changing the value during Play Mode).
Keep MinAttackDistance = 0, MaxAttackDistance = 30; try ShotsPerBurst = 1, 3, 5. The only
periodic ability is Rocket Attack, so no probability override is needed.

Move out of the marker for one shot, stay inside for the next, then kill the boss
during preparation and during descent. Check that chase resumes after the final Attack,
the target stays fixed, and canceled shots leave no markers or rockets.
Restore the prefab cooldown to 6 after testing.

The focused Unity test is `BossRocketAttackTests` (EditMode runner, entering real
Play Mode). Captures and its latest check report are under `Artifacts/BossRocketSmoke`.
`BossBurstTests` covers dual launch, actual walk sprites, 1/3/5 bursts, independent
delays/targets, continuous Idle during the burst and death/disable/missing-event cleanup.
Baseline failures and current burst measurements are under `Artifacts/BossBurst`.

Latest Play Mode run: all seven selected tests passed (`Artifacts/BossBurst/results.xml`).
ShotsPerBurst 1/3/5 produced exactly 1/3/5 Attack cycles, 2/6/10 upward launches and
2/6/10 actual falling instances. The five-shot burst sampled delays from 0.650 to
1.666 seconds within the configured 0.4–2.0 range. Idle/stationary between shots,
Walk sprites during chase, release before the last impact, both muzzle origins,
fixed markers, cleanup, damage and final-sector victory → Bunker all passed.
