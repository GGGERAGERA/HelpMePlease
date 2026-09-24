# World hazards implementation plan

User approved shared runner, orchestration-only director, sector presets and existing-asset pooling; inline execution without intermediate approvals.

- [x] Add and run a failing integration contract test (baseline.xml: shared implementation missing).
- [x] Extract RocketAttackRunner; preserve boss cycle, random sampling, timings and damage. Reuse SimplePrefabPool and EnemyExplosion.
- [x] Add extensible hazard definition/runtime contract and rocket implementation; director handles phase, timer, safe target and cancellation only.
- [x] Author sector presets and wire the existing production flow. One concurrent fixed-target strike, warning floor, player/edge clearance, no lethal hazard damage, tutorial suppression.
- [x] Verify production sector, sequential impacts, cancel/restart, actual instance reuse, boss animation/burst and cleanup; document measured results (5/5 tests passed).

No procedural visual prefabs; existing boss assets only. Unrelated working-tree edits must remain untouched.

Rulings: the finite route is three sectors, so bind Early/Middle/Late to profiles 01/02/03.
Reuse the scene-owned SimplePrefabPool rather than introduce another pool. Keep boss tuning
serialized in place; the hazard definition owns separate safety tuning and calls the same runner.
Independent read-only review found no concrete defects. Play tests run through UnitySetUp because
the installed Unity Test Framework does not support nested EnterPlayMode yields; waits use actual
Time.time rather than WaitForSeconds in the EditMode runner.
