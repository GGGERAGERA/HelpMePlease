# Player damage and orbital impact implementation plan

> **For agentic workers:** Execute inline using superpowers:executing-plans. Use one final review.

**Goal:** Make accepted player damage and round orbital cannon fire/hits readable in production Play Mode and a non-Development Windows player, using the existing presentation.

**Architecture:** Extend PlayerWhiteFlash/HUDManager and the existing OrbitalModuleVisual/pooled FlashLink effects. Keep feedback independent from damage resolution, colliders, orbital geometry and time control. Do not promote the development-only combat laboratory into production.

**Tech Stack:** Unity 6000.3.13f1, C#, URP 2D, existing sprite flash shader, existing HUD vignette and orbital line/halo prefabs.

**Spec:** User-approved first two items of the gameplay review: readable damage received and impact of round orbital cannons; reuse effects; preserve balance; verify Play Mode and an ordinary build.

## Constraints and review focus
- Preserve damage, cadence, spread, range, projectile collision geometry, knockback, invulnerability, enemies, waves, rewards, saves and Deploy.
- Warm short player flash and a transient pulse of the existing damage vignette; enemy flashes and orbital gun colors remain distinct.
- Emphasize shot/impact through existing non-colliding halo and pooled line effects. Leave physical body recoil and contact checks unchanged.
- Rejected damage must produce no new feedback; interrupted flashes must restore original materials; healing must not pulse as damage.
- Preserve low-HP vignette state after the transient pulse and clear it on death/UI teardown.
- Shot feedback must coexist with selection highlighting and must never affect contact bounds.
- Build/run verification uses enabled production scenes, BuildOptions.None and a separate QA product/save namespace. Restore project build settings after building.

## Execution ledger
- Ruling: Work in the already-open Unity checkout; snapshot and save the dirty scene before scene-changing tests. Preserve the three pre-existing bunker-door prefab edits. A separate checkout would not test the user's open editor and would discard its unsaved context.
- Ruling: User approved scope and approach in the preceding message; no additional design approval is needed.
- Evidence: all four focused checks failed for the intended pre-fix behavior (red-results.xml). Initial nested Play Mode command setup was corrected before collecting this baseline.
- Evidence: LowHPVignette was inactive in the authored GameplayHUD; alpha updates alone could never render it.
- Ruling: QA uses ordinary GameplayRandom.Begin(1709) for repeatable production layout; an unrelated randomly rejected required-site layout was observed, and no layout/generation code is changed here.
- Ruling: Low-health expectations use the authored HUD threshold (0.4), not the script's 0.3 default. Preserve that value.
- Ruling: frame the QA camera by moving its follow rig, not its shake child; leave the original production camera shake unchanged.
- Review: fix halo tint composition after the regular sprite pass, and preserve snapped/invalid reward preview state across ticks. Existing physical body recoil is unchanged.
- Evidence: fast-projectile QA exposed birth-frame lifetime subtraction (Sniper impact already absent at age 0.0919 s despite a 0.15 s duration). Pooled lines now expire at their absolute scaled creation time plus lifetime; pause and bullet-time behavior are preserved.
- Final verification: 9/9 focused/existing tests PASS; three-color real production Game View scenario PASS; BuildOptions.None Windows player built with 0 errors and 8 existing obsolete-API/unused-field warnings.
- Final visible player scenario PASS at 1600x900 (Editor=False, Development=False), with ten nonblank screenshots. Hidden-window captures were black and are explicitly not visual evidence; use ReleaseVisible/ only.
- Limitation: direct application shutdown emits coroutine errors for Normal Anomaly Site 3 and 6, plus a ComputeBuffer disposal warning. These occur after the feedback scenario; raw logs and the issues are retained in verification.json. Anomaly lifecycle code is outside this change and was not modified.
- Preservation: gameplay/settings SHA-256 baselines all match; MainMenu remains equal to the saved unsaved-scene snapshot; pre-existing door edits retained.

## Task 1: Baseline and failing behavior checks
- [x] Snapshot/save the open scene and record baseline project settings and gameplay parameters.
- [x] Add Play Mode checks for distinct/recoverable player flash, damage-only vignette, non-colliding shot halo and longer visible orbital impact with unchanged actual damage.
- [x] Run the tests against existing presentation and retain the expected failures.

## Task 2: Existing production presentation
- [x] Update PlayerWhiteFlash to cache a warm flash material once, restore on interruption and expire in unscaled presentation time.
- [x] Pulse the existing HUD vignette for accepted damage, keeping its low-health baseline and modal/death cleanup.
- [x] Reuse the selection halo for a brief colored shot pulse, preserving its idle/selected appearance and body collision geometry.
- [x] Expand the existing short muzzle/impact FlashLink pattern and lifetime; configure visual-only values in OrbitalPresentationConfig.
- [x] Run focused checks plus existing enemy pool/flash and orbital state-operation checks; retain real Game View captures. Flash pause/reuse is covered by the focused lifecycle test.

## Task 3: Ordinary executable verification
- [x] Add a command-line-only QA scenario using the production startup, actors and damage/projectile paths; no development-only API dependencies.
- [x] Build enabled production scenes without Development or ScriptDebugging into a dedicated QA output; restore PlayerSettings.
- [x] Run the visible executable at 1600x900 and save screenshots, actual damage/feedback/recovery checks and player log.
- [x] Inspect Play Mode and executable screenshots; verify feedback checks, unchanged gameplay/settings, and review final diff. Record unrelated shutdown errors separately rather than claim a globally clean log.
