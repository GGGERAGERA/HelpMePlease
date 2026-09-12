# First production SFX implementation plan

Goal: connect the user-approved first-pass events to current ORBITAL and Golden Path without changing gameplay or visuals.
Architecture: one GUID-preserving Resources catalog move; existing fixed AudioService pool with a small cue priority field and lower-priority eviction; one managed compression loop owned by station lifecycle. No new manager, audio generation, or unused-file deletion.
Spec: user attachment 02f47e1e-78fb-48ae-b990-939b50a14c89/pasted-text.txt.
Execution: inline in the shared working project so existing Unity runner can verify it. Preserve existing MainMenu/TMP changes.

- [x] Verify a failing Resources catalog test; inspect existing candidate clips using Unity decoded duration/peak/RMS.
- [x] Move catalog and meta to Resources/Audio; retain ID values and add production cue IDs. Select short existing clips and conservative limits.
- [x] Extend AudioCueDefinition with priority; AudioService reuses lower-priority slots for important cues. Stop managed loops on pause, disabled/destroyed owner and scene load. Verify saturation and lifecycle with actual playback tests.
- [x] Wire successful ORBITAL pistol/sword/impulse/arc activations, EnemyHealth hit, core wave, compression/release, FinishFlight install, successful reward selection. Reuse existing death/player/XP/level hooks.
- [x] Run focused tests and one existing Golden Path at 5x; then real production-scene 1x smoke with multiple weapons/enemies/XP/core/hurt/reward flight, loop lifecycle and voice counts.
- [x] Inspect code diff and compilation logs; report exact cue table, candidate choices/rejections, test results, warnings, remaining desired assets and changed production files.

Excluded: ambience, boss music/attacks, gates, bunker windows, sector transitions, gold, crates, rotation/reverse loops, elite audio, progression/balance/visual changes.
