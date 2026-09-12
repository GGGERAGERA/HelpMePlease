# Subject#42 Golden Path regression

## Developer lab

Open `Assets/_Project/Scenes/Dev/GoldenPathLab.unity` (or Unity menu
`Tools > Subject42 > Golden Path Lab > Open scene`), press Play and use the Game
view. The editor-only panel provides ×1/×10/×100, 5×/10× speed, an initial seed,
rerun of the last non-aborted failure at its original speed, Stop, refresh and
file-open buttons. It uses existing production bunker startup and BotBatchRunner,
persists as a dev panel during the run, and returns to the lab after the batch.
No production Build Settings entry or production menu integration is added.

Status and counters assess the latest batch: red for a recorded game regression,
yellow (BOT FAILS / ABORTED PRESENT) for combat/assertion/aborted/incomplete results, green for a complete clean
batch. History-wide unresolved issues remain in the existing summary. Up to 20
run rows are shown in a scrollable table. Latest batch, History and Failures tabs
let you find older failed attempts without opening files. Click a row for a
scrollable detail panel with the exact assertion, full reason, combat/cleanup
metrics and last 10 damage records. Row colors distinguish PASS (green), combat
failure/abort (yellow), game regression (red) and assertion failure (orange).
RERUN SELECTED SEED starts one run with that attempt's seed and original speed;
COPY SEED and COPY FAILURE copy the seed or full detail text. Selection tracks
the exact run and survives Refresh. Live mode
shows the current seed, sector, speed and completed/requested runs. File changes
and batch completion refresh the display automatically.

The lab UI integration check completed seed `48151623` at 5× with PASS and
verified the automatic scene return and new result row. Fixed screenshots are
in `Artifacts/BotBatches/golden_path_lab_running.png` and
`golden_path_lab_after.png`; these are UI checks, not another result store.

The existing `BotRunSession`, `BotController`, `BotTelemetry`, `BotRunSeed` and
`BotBatchRunner` own this mode. Ordinary Survivor batches keep their original API.
Runtime bot code remains restricted to Editor/development builds.

## Run

In the production gameplay F1 menu, QA / BOT LAB offers Golden Path ×1, ×10 and
×100. Choose the existing 1×, 5× or 10× simulation speed. AUTO walks successive
seeds beginning at the seed input; FIXED repeats that exact seed. STOP releases
input, simulation speed and seed ownership. First FAIL stops a Golden Path batch.

The same mode is available as
`BotBatchRunner.StartGoldenPathBatch(count, seedMode, firstSeed, simulationSpeed)`.
It can start in the bunker with a selected character, or return from gameplay to
the bunker before starting. Starts use `BunkerRunStarter.StartRun`. Movement goes
through `CharacterMovement2D.MovementIntent`; ORBITAL weapons do normal production
combat. Objectives use `WorldEvent.Interact`, position/hold triggers and combat.
Exits use their real colliders. Cards and arena selections use the existing
validated reward entry points, including module flight. Sector cards use the
same `LevelChoiceManager.SelectRule` path as player input. Boss death and victory
are never assigned by the bot.

The bot circles capture zones, avoids enemies and projectile trajectories using
16 fixed candidate headings with acceleration and external velocity observations, collects nearby XP, selects
weapons/rings/mounts from offered rewards, seeks the exit and circles the boss.
The route includes three sectors, victory, bunker cleanup, a second real start
with a fresh baseline, and a final return to the bunker.

## Results and failures

`Artifacts/BotBatches/latest_batch.json` and `.csv` remain the current batch
artifacts. Each existing BotRunResult contains a GoldenPathResult. Completed
Golden Path batches, including failed attempts, are also retained in the single
`Artifacts/BotBatches/golden_path_history.json`; no per-run files are created.
The JSON includes assertion name, sector, simulation time, observed state and
reason for every failure. PASS requires every route milestone, one boss spawn,
one victory, applied rewards, DirectMountSelection coverage and second-run cleanup.

Unity tests `Subject42GoldenPathTests.One`, `.Ten`, `.Hundred` use this same batch
mode, not a separate simulation. Run them in that order, stopping and replaying
the failing seed before continuing. Existing `Subject42CoreQARunner` request-file
execution is supported. Refresh imported scripts first using the existing
`Artifacts/BunkerNetwork/refresh.request` command; wait for compilation before
writing the desired fully qualified test name (without a trailing newline) into
`Artifacts/GeneratedQA/CorePulse/run.request`. Verify the test result count is
nonzero in its `results.xml`.

This suite checks gameplay/state/progression. It does not test rendering. Its
test host accepts only the known ParticleSystem duration assertion from
`WorldRuleVisual.EnsureWindResources`; unrelated errors still fail the test.
Seeded random streams isolate gameplay choices, but Unity frame/physics timing
can still affect combat outcomes. A seed identifies a reproducible scenario,
not a promise of bit-identical frame timings.

## Regressions found during implementation

- Seed `48151623` reached victory with sector 3 still active in the bunker:
  cleanup depended on a delayed `BunkerRunSummaryPresenter` coroutine. `EndRun`
  now clears gameplay state after creating the summary. The standalone
  `EndRunClearsGameplayStateWithoutWaitingForBunkerPresentation` test reproduces it.
- The same scenario exposed a destroyed projectile returned by `SimplePrefabPool`.
  A repeated `PooledGameObject.Release` returned false, causing projectile callers
  to destroy an object already returned to the pool. Release is now idempotent.
  `RepeatedDespawnDoesNotDestroyAnObjectAlreadyReturnedToPool` covers both object
  survival and absence of duplicate pool entries.
- The new runner's initial duplicate check reused an arena-selection token for
  body rewards. It now tracks actual reward-request identity and exact deltas.
- The initial navigation could enter an exit before finishing a capture objective;
  Golden Path now steers around exits until the objective completes.
- Sector card sampling now uses the existing RuleRandom stream during seeded bot
  sessions instead of sharing Unity's visual/gameplay random state.
- Accelerated runs exposed frame-based movement-intent sampling: several physics
  steps could use an obsolete decision. External intent is now sampled by the
  existing movement component every physics step; Golden Path computes its
  direction in FixedUpdate. Human keyboard input retains its original path.
- Seed `48151632` reproduced contact deaths because the steering forecast assumed
  immediate direction changes and ignored acceleration/external velocity and a
  pursuer turning towards the player. The observation-only movement forecast and
  clearance check now cover these cases.
- Seed `48151625` exposed incorrect boss spacing for inner-ring swords and the
  boss's offset capsule collider, followed by corner trapping. Boss movement now
  uses the installed ring radius, actual collider clearance and early retreat
  from the arena boundary. Offered ranged modules have explicit priority; low
  health makes an offered MAX HP card the first choice.
- Golden Path telemetry retains the last 20 actual damage sources, simulation
  times, positions and boss HP values in each existing run result.

## Campaign stopped by request — 2026-09-12

No further combat tuning or runs are planned. Hundred-run tests were not executed.
The latest 5× batch contains 9 complete PASS results, seeds 48151623–48151631,
with zero failed assertions. While the stop UI was being opened, the existing
batch automatically started seed 48151632. Play Mode was then stopped during
its second-run baseline stage, after boss victory. Its outcome is Aborted, not
a completed Golden Path. The current aggregator also counts this abort as one
GoldenPathFailed and sets batch status Failed; its sole assertion is
Runner.Completed with reason "Bot Lab disabled / Play Mode stopped". Raw results
are preserved without relabeling. A separate 5× replay of seed 48151625 passed.

The latest 10× batch stopped after 2 PASS and a player-death FAIL at seed
48151625. The preceding individual replay of that seed passed, so 10× combat
stability is not established. An earlier 10× attempt reached 9 PASS / 1 FAIL
on an older steering revision; this is not the final revision's result.

Remaining observed combat failures were navigation/build/survival limitations
of the bot (contact damage, projectile/site hazards, boss spacing and boundary
trapping), not evidence of broken production progression. The corrected run
cleanup and pool reuse defects did not recur in the latest completed runs.

Regression tests for immediate EndRun cleanup, repeated pooled despawn and
per-physics-step movement intent each failed before their fix and passed after.
Exact reward delta and leaked baseline upgrade tests also passed. Full ordinary
bot regression was not rerun after the final changes. Two presentation tests
in the broader pooling suite failed (FeelOffset_LeavesGameplayOriginAndResetsOnPooledReuse
and RootSprite_UsesPresentationProxyWithoutMovingGameplayRoot); their visual
implementations were not changed or investigated within this gameplay task.

## Changed files

Production fixes / optional input adapter:
- Assets/_Project/scripts/Run/State/RunStateManager.cs
- Assets/_Project/scripts/Combat/Effects/SimplePrefabPool.cs
- Assets/_Project/scripts/Combat/Player/CharacterMovement2D.cs

Existing bot integration and diagnostic hooks:
- Assets/_Project/scripts/Debug/BotBatchResult.cs
- Assets/_Project/scripts/Debug/BotBatchRunner.cs
- Assets/_Project/scripts/Debug/BotController.cs
- Assets/_Project/scripts/Debug/BotLabDebugUI.cs
- Assets/_Project/scripts/Debug/BotRunResult.cs
- Assets/_Project/scripts/Debug/BotRunSession.cs
- Assets/_Project/scripts/Debug/BotTelemetry.cs
- Assets/_Project/scripts/Combat/Enemies/EnemyProjectile.cs
- Assets/_Project/scripts/Progression/RunUpgrades/UpgradeManager.cs
- Assets/_Project/scripts/Run/Flow/RunFlowController.cs
- Assets/_Project/scripts/Selection/Levels/LevelChoiceManager.cs

New runner files (each with its Unity .meta):
- Assets/_Project/scripts/Debug/BotRunSession.GoldenPath.cs
- Assets/_Project/scripts/Debug/GoldenPathResult.cs
- Assets/_Project/Editor/Tests/Subject42GoldenPathTests.cs

Extended existing regression fixtures and documentation:
- Assets/_Project/Editor/Tests/Subject42BotLabTests.cs
- Assets/_Project/Editor/Tests/Subject42PoolingTests.cs
- Assets/_Project/Editor/Tests/Subject42RunStateTests.cs
- docs/golden-path.md

Two pre-existing dirty assets were preserved and also appear in git status;
they are not production fixes from this task:
- Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset
- Assets/_Project/art/mat/UI/M_UI_WorldRuleOverlay.mat

Generated evidence remains in Artifacts/BotBatches/latest_batch.json, its CSV,
golden_path_history.json and Artifacts/GeneratedQA/CorePulse/results.xml and
progress.txt. Changes remain in the working tree on TestByDantes (0ef71209).
