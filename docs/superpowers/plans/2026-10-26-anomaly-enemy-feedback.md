# Anomaly lifecycle and enemy feedback implementation plan

> **For agentic workers:** Use superpowers:executing-plans, one final fresh-context review.

**Goal:** Remove anomaly coroutine errors at shutdown, then improve existing enemy hit/death presentation without balance changes.
**Architecture:** Fix the lifecycle owner at its actual callback boundary. Reuse EnemyHealth feedback events, EnemyHealthPresentation and existing flash/blood/death assets and pools; keep health, physics, rewards and clocks authoritative.
**Tech stack:** Unity 6000.3.13f1; production MVP Play Mode, ordinary Windows BuildOptions.None player and isolated QA save identity.
**Spec:** User request 2026-10-26: fix anomaly coroutine errors first, then enemies/deaths with existing FX, preserve balance, verify Play Mode and ordinary build.

## Constraints
- Preserve all prior readability/environment work and user MainMenu/bunker-door edits.
- No damage/health/knockback/cadence/AI/collider/stat, economy, loot, save or Deploy changes.
- Shutdown must not initiate a respawn; ordinary failed-event retry and temporary disable/resume must still work.
- No new visual assets, full-screen effects, hit-stop or physical enemy squash/recoil.
- Inspect actual prefab/pool behavior before selecting enemy presentation adjustments.

## Review focus
- Quit/destruction callback order and late notifications from children; pause/temporary disable must retain normal behavior.
- Hit FX must remain bounded in a crowd and reset correctly on pool reuse.
- Death presentation must outlive the destroyed actor, terminate, and return to its existing pool.
- Preserve enemy silhouettes and warning/attack colors while player hit feedback remains distinct.
- Actual production Game View and ordinary executable captures, including complete shutdown log audit.

## Tasks
- [x] Reproduce lifecycle failure with full ordinary-player stack/state audit and focused Play Mode checks; retain failing evidence.
- [x] Fix the proven lifecycle cause, verify normal retry/disable/resume/terminal behavior and actual ordinary-player shutdown before enemy work.
- [x] Inspect existing hit/death assets and pools, document narrow visual adjustments and add failing checks.
- [x] Implement existing enemy presentation adjustments and verify controlled crowd/hit/death/reuse in real Game View.
- [x] Run relevant regressions, ordinary build and visual/log checks; final review and preservation audit.

## Ledger
- Existing release logs consistently show inactive Normal Anomaly Site 3/6 coroutine errors after the scenario finishes. Existing HandleEventFailed checks isActiveAndEnabled but has no application-shutdown state.
- Work in the open checkout and isolated QA product identity; no new approval is needed for the explicitly approved continuation.
- Root cause proven in ordinary player: WorldEvent destruction notifies EventFailed after OnApplicationQuit, before site OnDisable unsubscribes. At the failing call sites 3/6, isActiveAndEnabled=True and activeSelf=True but activeInHierarchy=False. The existing enabled check admitted an inactive coroutine host. Full stacks/state retained in LifecycleBeforeRelease/player.log; focused terminal retry check also fails as intended.
- Fix: reject late notifications when disposed, application-closing or inactive in hierarchy; suppress re-enable scheduling/late timer retries after terminal closure. Temporary disable/resume remains supported. Diagnostic production logging removed.
- Lifecycle verification: focused Play Mode PASS and ordinary LifecycleRelease PASS, including actual process termination with no coroutine errors/exceptions. A pre-existing ComputeBuffer disposal warning remains, not a coroutine failure.

## Enemy presentation design
- Keep EnemyHealth, AI, transforms, colliders, loot and rewards unchanged.
- Extend the existing SpriteWhiteFlash shader with blend default 1 (legacy/player behavior unchanged). EnemyWhiteFlash sets .55 through one cached MaterialPropertyBlock, merging existing properties; restore blend 1 and current base material on completion/disable. Retain shared material identity and pool reuse. Use cached realtime wait/reset so an open paused UI does not retain a white silhouette.
- Six authored non-boss .2s flash overrides become .1s; base .08s and boss data stay unchanged.
- EnemyHealthPresentation-owned instances of existing p_FxAttacked2: lifetime .4 -> .14, size .6 -> .45, duration/pool return 1 -> .2. Critical size emphasis and authored sprite animation remain. The shared prefab asset stays unchanged for props/arcades.
- EnemyHealthPresentation-owned common p_FxDeath1 instances: lifetime .4 -> .28, size 1.2 -> 1.4, duration/pool return 1 -> .35. Boss death uses its separate authored prefab/settings unchanged. No extra particles, renderers or effects per hit/death; shared source FX assets are unchanged.
- Verify actual enemy damage/death, crowd silhouettes, realtime recovery, property-block preservation, FX return/reuse and unchanged damage using production actors/pools, with real Game View captures in both runtimes.
- Initial enemy verification: 10 relevant combat/lifecycle/pool checks PASS. Review identified missing standard sprite material/renderer tint inputs. A rendered calibration probe confirms vertex tint works on the current path; explicit material/non-batched inputs were absent. Extend the existing shader using the installed UnitySprites.cginc contract (vertex * _Color * _RendererColor), with default white values and exact pixel checks to reject double tinting. Full/player blend remains 1.
- QA captures now clear already-fired hostile projectiles and let prior player-hit presentation expire, isolating enemy presentation without changing production behavior.
- Scope correction: p_FxAttacked2 is shared by Case1 and arcade Ball1. Restore both original shared FX prefabs; apply compact duration/lifetime/size only to EnemyHealthPresentation-owned instances after stopping playOnAwake. The existing enemy-only pool resets size on Get; absolute duration/lifetime are reapplied. Boss death stays authored. QA asserts source prefabs remain unchanged.
- Render-probe correction: SpriteRenderer owns _RendererColor and overwrites an external MPB value. Keep its real color probe; use a temporary plain quad for the explicit shader per-draw input test. The initial unsupported injection failed only the QA setup and is retained separately.
- Final focused Play Mode checks: 4/4 PASS, including exact rendered sprite/material/renderer RGB and warm player RGB, common enemy damage/death/paused recovery, pool reuse and source-FX preservation. Prior 10-case regression run remains green for unchanged behaviors.
- Final reviewer confirmed lifecycle, merged property blocks, stopped instance-only particle tuning, critical reset and corrected rendered tint probe; no remaining code findings.
- Final ordinary player PASS at 1600x900, Editor=False/Development=False: rendered tint probes, actual enemy hit/death/recovery/pool checks, normal anomaly failure retry, temporary disable/resume and terminal closure all pass. The process has exited; no errors.log, coroutine errors or exceptions in the full shutdown log.
- Final evidence: 11 distinct checks pass using their latest real result (original XMLs retained), 12 nonblank 1600x900 Game View captures across Play Mode/release validated and representative images inspected. BuildOptions.None, 0 build errors, 10 warnings. See Artifacts/GeneratedQA/EnemyFeedback/verification.json.
- Preservation: all 24 gameplay/settings hashes match prior baselines; both shared FX prefabs, boss death FX, Case1 and Ball1 match Git HEAD; six enemy prefab diffs contain only visual flash-duration changes; MainMenu equals the preserved saved scene. No user save/Deploy/commit operation.
- Remaining limitation: the previous ComputeBuffer disposal warning still appears at process shutdown; it is separate from the two fixed coroutine errors. QA uses controlled actor positions/damage and rendering probes, with no claim of physical keyboard/mouse testing.
