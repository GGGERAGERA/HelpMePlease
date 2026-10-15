# Orbital production transfer

Goal: carry the supplied EnemyOrbitalLab brief into MainBuild/MVP and its existing reward/install flow.
Execution: inline, no agents, no batch runner, preserve the user's uncommitted prototype.

Design: retain Pistol=0, ArcEmitter=3 (p_miniWeaponLaser1), LinkNode=4. Retire module IDs 1/2 without reuse; allocate Sniper=5, Shotgun=6, Saw=7. Shared projectile, saw and shield implementations live under production scripts; lab controllers only choose profiles and fixtures. Store four explicit categories and run-wide multipliers in OrbitalRunState so later acquisitions inherit them. Retain the mount inventory, pair placement, red weapon preset, normal starter pistol, and run lifecycle.

Tasks:
- [x] Shared weapon profiles, swept projectiles, saw disk contact and shield geometry; lab uses the same implementations.
- [x] Rewards and category data/application/UI; migrate only retired saved modules, preserve unrelated progress.
- [x] Promote artist wrappers, bullet visual, shield prefabs and normal spawn profile entries; remove retired assets, catalogs and presets.
- [x] Shift slow time, RMB station steering plus WASD, release/focus/teardown cleanup. Physical manual input remains unverified after Computer Use timeouts.
- [x] Compile release scripts and editor, run one bounded production Play Mode smoke through real cards/placement, inspect references and final diff. Narrow follow-ups checked combat/sector lifecycle and spawner/pooling, without repeating the full card scenario.

Review focus: old enum values must never become new weapons; category upgrades before acquisition must persist over sector restoration; a shotgun volley consumes at most one shield point; pooled trails and shields reset; cancellation/death releases input/time state.

Verification: focused checks only. Record programmed versus manual input coverage separately. No commits or unrelated refactoring requested.

Rulings: reserve retired numeric IDs rather than reusing them; retain MainMenu's demonstration station and assert destruction of the run-owned station; late-bind the scene camera since CharacterSpawner publishes it after station creation; preserve independent existing working-copy changes. No agents, branch changes, commits, pushes or full batch runs. Final review: self-review, per the user's explicit prohibition on agents. Evidence: Artifacts/OrbitalProductionTransfer/validation.md.
