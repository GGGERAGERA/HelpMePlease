# Expedition cores implementation plan

**Goal:** Complete the user's sector → optional task → expedition → immediate permanent cores → bunker loop in the existing production game.

**Architecture:** Extend CurrencyManager with a separate saved balance and idempotent receipts. A data-driven ExpeditionTaskRun belongs to RunStateManager, observes existing run statistics/completed-location notifications, and records payouts before UI notifications. Surface sector assets configure three repeatable tasks and victory/first-clear rewards. Existing surface map, HUD and result presenters display this state.

**Constraints:** Work directly in the saved project as requested. Preserve existing gold, story missions, map unlocks, tutorial and development-run boundaries. No new rooms, combat HUD, maps or spending. Existing completed-sector progress suppresses retroactive first-clear rewards. No user progress resets in tests or authoring.

- [x] Verify missing currency/data behavior with focused Editor tests.
- [x] Implement core balance, receipts, task data/state and run ownership; verify death, repeat runs, first clear and reload.
- [x] Integrate sector selection, task selection, compact HUD and existing results. Author production assets with pixel frames and a turquoise code-native pixel icon; localize RU/EN.
- [x] Run focused lifecycle/authoring and existing regression tests, production script compilation, and review the final diff. Record actual evidence and any unavailable visual checks.

**Interfaces:** Sector.BuildRunConfig snapshots core rewards. SurfaceMapService selects one valid sector task (or none). RunStateManager owns ExpeditionTaskRun; RunStatsManager's existing reward-relevant event and RegisterCompletedLevel refresh progress. Results consume committed payout fields only. Currency transactions stage dependent state before PlayerPrefs.Save and notify afterward.

**Review focus:** duplicate callbacks and reentrancy; old completed sectors; death before/after completion; scene transitions and retry/new run identity; authored prefab references and UI fit. The user's detailed request in this chat is the implementation specification and authorization to proceed directly.

**Evidence (2026-10-17):** Final Unity Test Runner run: 27/27 passed, including 10 focused core/data/UI cases, a real production Play Mode loop with task selection, immediate payout then actual death, repeated task plus real guardian victory, no-task victory and a fresh Play Mode session reloading the saved wallet/map. Existing bunker HUD, compact HUD, map progression/UX and production reference/dependency checks passed. Release and Development player scripts compiled; both also compiled with all 63 Dev/Lab sources removed (439 runtime sources). `git diff --check` passed. Captured picker, live progress, death result, victory result and reloaded bunker screenshots were visually inspected. No manual standalone-player walkthrough was performed.

**Review resolution:** A fresh reviewer identified nested currency notification payout loss and death-comment overlap; focused failing tests reproduced both, and the final suite verifies their fixes. The hidden-task camera reservation was corrected. Tutorial time/kills intentionally follow the existing complete-run statistics once normal gameplay starts; tutorial is not a completed location. Unity nested-prefab local IDs are preserved during authoring. Three pre-existing stale removed-component entries in StartScreen were removed after the production integrity test exposed them.
