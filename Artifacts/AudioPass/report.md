# Subject#42 — first production SFX pass

One GUID-preserving AudioCatalog now lives at Assets/_Project/Resources/Audio/VerticalSliceAudioCatalog.asset. The old location has been removed. Existing scene GUID references are preserved.

## Cue settings

| Event | Existing clip | Volume | Pitch | Cooldown (s) | Max voices | Priority |
|---|---|---:|---|---:|---:|---:|
| PistolShot | Pistol_shot_SFX.mp3 | 0.22 | 0.96–1.04 | 0.085 | 3 | 20 |
| EnemyHit | impactSoft_medium_000.mp3, impactSoft_medium_001.mp3, impactSoft_medium_002.mp3 | 0.09 | 0.92–1.08 | 0.065 | 2 | 0 |
| EnemyCritical | crit.mp3 | 0.1 | 0.98–1.04 | 0.12 | 1 | 5 |
| CommonEnemyDeath | impactPunch_heavy_000.mp3, impactPunch_heavy_001.mp3, impactPunch_heavy_002.mp3 | 0.16 | 0.92–1.08 | 0.1 | 2 | 10 |
| PlayerHurt | impactPunch_medium_001.mp3 | 0.45 | 0.97–1.03 | 0.18 | 1 | 100 |
| OrbitalSwordHit | Sword_Laser.mp3 | 0.12 | 0.95–1.05 | 0.12 | 2 | 20 |
| OrbitalImpulseFire | lowFrequency_explosion_001.mp3 | 0.28 | 0.93–1.03 | 0.15 | 2 | 25 |
| OrbitalArcFire | etfx_shoot_lightning2.wav | 0.2 | 0.96–1.04 | 0.15 | 2 | 25 |
| OrbitalCompress | engineCircular_000.mp3 | 0.1 × 0.55–1.0 intensity | 0.90–1.12 (compression intensity) | 0 | 1 | 30 |
| OrbitalRelease | laserLarge_003.mp3 | 0.34 | 0.97–1.03 | 0.12 | 1 | 60 |
| CorePulse | forceField_000.mp3 | 0.22 | 0.99–1.01 | 0.12 | 1 | 40 |
| CoreCascade | explosionCrunch_000.mp3 | 0.28 | 0.98–1.02 | 0.15 | 2 | 45 |
| ModuleInstall | confirmation_001.mp3 | 0.32 | 0.98–1.02 | 0.08 | 2 | 60 |
| XPPickup | pickupCoin.wav | 0.045 | 0.98–1.1 | 0.08 | 1 | 0 |
| LevelUp | Level_up_test.mp3 | 0.7 | 1–1 | 0.5 | 1 | 80 |
| RewardSelect | Button_confirm.wav | 0.38 | 0.98–1.02 | 0.08 | 1 | 80 |

## Existing-asset selection

Selection uses decoded Unity clip duration/peak/RMS (candidates.tsv), original pack purpose and role in the mix. No clips were generated, edited or deleted. No claim of a full subjective listening/mix session is made.

- Pistol_shot_SFX: existing requested recording, 1.056 s; 3 voices, 85 ms global cooldown. Each real module fire requests playback; caps intentionally merge dense simultaneous fire.
- EnemyHit: three soft medium impacts, 0.209/0.261/0.209 s, volume .09. Punch impacts are reserved for deaths/hurt.
- EnemyCritical: crit.mp3, .418 s, peak 1.0; low .10 volume, replaces ordinary hit rather than adding another layer. ORBITAL currently does not roll criticals; available damage paths with isCritical=true use the cue.
- EnemyDeath: three heavy punches .731/.601/.549 s. Rejected zombie scream (8.098 s) and Zombie_SFX (3.912 s) for mass deaths.
- PlayerHurt: medium punch .470 s, .45 volume, priority100. Rejected 1.071 s zombie-hurt clip: cap1 would suppress a valid second hurt after .6 s invulnerability, and a zombie vocal is a weak identity match for the player.
- Sword_Laser: .601 s, peak1.0/RMS.223, so conservative .12 volume. Plays only on a real target contact.
- Impulse: lowFrequency_explosion_001, 1.071 s, low-frequency thump; chosen over the 2.064 s variant.
- Arc: Epic Toon FX etfx_shoot_lightning2.wav, .667 s, shorter than .800/1.065 s lightning alternatives; once per chain activation.
- Compression: engineCircular_000, 5.068 s, lower RMS .122 than spaceEngine alternatives. One managed loop; source volume .055–.10 and pitch .90–1.12 follow compression. MP3 loop seam has not been polished; a purpose-built seamless tension loop remains desirable.
- Release: laserLarge_003, .810 s, energy accent .34, once at outward-motion start. No second sound on radius settling.
- CorePulse: forceField_000, 1.019 s. CoreCascade: explosionCrunch_000, .862 s; cap2 allows adjacent waves at normal timings without one voice per target.
- ModuleInstall: confirmation_001, .366 s, short electronic lock; emitted after successful FinishFlight attachment only.
- XP: pickupCoin.wav, .068 s, peak .977/RMS .278; very low .045 volume, cap1 and 80 ms cooldown.
- LevelUp: Level_up_test, 1.848 s, peak .341/RMS .032; .70 source volume; one announcement per choice request, not on cancel-to-cards refresh.
- RewardSelect: Button_confirm, .231 s, successful reward selection, separate from physical install. Existing hover untouched.

## Remaining useful original sounds

1. Short player-specific hurt grunt (under .5 s).
2. Organic enemy death set (2–3 short variants).
3. Seamless mechanical/energy compression loop.
4. Release burst designed for 1.15 overshoot.
5. Physical module latch with a small energy lock.
6. Distinct CorePulse and stronger Cascade accents.
7. Short sword contact set with less sustained energy.
8. Softer XP tick, less coin-like.

No requested cue was left empty: existing files provide functional first-pass substitutes. These are replacements to improve identity later, not a requirement to add more events now.

## Architecture limits

Audio clip selection and pitch variation use a separate System.Random, preserving the Unity gameplay random stream.

Existing pool remains 20 SFX/UI sources plus two music and one ambience source. Higher cue priority can reuse a lower-priority active slot; no new global manager. Unity source priority follows cue priority. EnemyHealth no longer allocates a per-enemy AudioSource or calls external one-shot for hit/critical. Managed handles stop on pause, inactive/destroyed follow target, service disable and scene load; station disable/teardown and StopRunGameplay also stop compression. Held-loop recovery is throttled to 200 ms and forbidden after run end/stopped/victory.

Existing PlayExternalOneShot remains for unrelated low-frequency legacy callers; new pass events do not use it. Ambience/music, gates, gold, crates, broad UI and gameplay balance were not changed.

## Validation evidence

See catalog-red.xml and priority-red.xml for reproduced infrastructure failures; infrastructure-green.xml for passing fixes. Runtime results and warnings are stored beside this report. Final validation summary is in validation.md.

Initial MainMenu.unity and TMP fallback changes predate this pass and are preserved. No commit or unrelated asset cleanup was performed.