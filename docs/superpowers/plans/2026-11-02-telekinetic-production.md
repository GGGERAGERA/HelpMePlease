# Telekinetic Rig production integration

**Goal / spec:** implement the user's 02.11.2026 production-transfer request in this working copy. No commits, staging, branch changes or push. Execute inline; keep the original orbital implementation available to labs.

**GDD:** [§4.3 Control of space](../../../Assets/_Project/Documentation/Subject42_Game_Design_Bible.md#43-контроль-пространства), [§4.5 Growth](../../../Assets/_Project/Documentation/Subject42_Game_Design_Bible.md#45-рост-силы), [§6.5 Anomalies](../../../Assets/_Project/Documentation/Subject42_Game_Design_Bible.md#65-текущая-реализация-и-границы), [§7.3 Rewards](../../../Assets/_Project/Documentation/Subject42_Game_Design_Bible.md#73-награда).

**Before → after:** production uses segmented O/M orbit slots → one connected footprint grid, whole-item placement/rotation/swap/Merge, orthogonal module effects, one common phase and centre. Player chooses packing, adjacency and fire direction. Ring/Mount rewards → items and grid-space expansion; the existing reward queue owns pause/resume. Five existing anomaly zones affect this rig without changing their identity or event spawning.

**Basis:** direct user request, including slow automatic rotation and validated Glitch. This explicitly revises the previous MVP-only segmented geometry and prohibition of automatic rotation. Slow automatic rotation (12°/s, toggle in constructor; Q/E overrides it) and numerical module tuning remain feel/balance experiments.

**Architecture:** promote the lab's placement model and chassis to shared production code; retain the lab controller. A production rig reuses the combat adapter and existing station input/time/reward ownership, bypassing old ring combat/presentation. Authored prefab references carry icons and art outside Dev. Run-owned grid and reserve survive scene transitions; old run data and persistent profile keys are retained. Initial available radius 2, expand to 3 then 4; the full 9×9 grid remains attainable. Items that cannot fit remain in reserve, never disappear.

**Alternatives:** adapting the new grid into old circular slots introduces two placement authorities; running the lab controller in MVP also introduces a second player, spawner and time owner. Both contradict the requested replacement. Reusing model/view and the production lifecycle is the chosen implementation approach.

- [x] Shared model/view: promoted with original MonoScript GUIDs and lab imports; atomic compatible swaps, bounds/connectivity checks, serialized item identity/reserve and expansion. Invalid swaps leave all state intact; no valid pair is a no-op.
- [x] Production combat/control/UI: one shared combat adapter, fixed weapon direction, Q/E and slow auto button, existing RMB/Shift, original constructor gate, reserve-only UI and module suppression.
- [x] Reward/anomaly/event integration: original queue/modal ownership, warned/validated whole-item Glitch, temporary interference, Slow/Red/Suppression. Relay and the retained FalseSignal contact contracts consume actual authored weapon-body renderers; event rules/spawning are unchanged.
- [x] Authored prefab/icons, synchronized Bible/Core, Release/Development compilation and no-Dev boundary; bounded real-Unity production smoke completed through bunker return. No full game build or broad suite.

**Success:** production starts only the new rig, reserves/placed items are conserved, every committed layout is valid, pause ownership and return work, target smoke has actual executed assertions. **Reject experiment:** auto rotation worsens aiming/readability or adjacency does not produce understandable choices; player observation is required and technical smoke cannot decide enjoyment.

## Evidence / decisions

- Initial working tree clean. Current lab types and extended-effect callbacks are development guarded; release must receive real shared callbacks, not a dependency on Dev.
- Existing Tab permits a paused constructor while the reward queue is idle; retain that exact gate rather than allowing live editing.
- Persistent profile storage must continue through the project's PlayerPrefs boundary. Smoke uses its isolated sandbox.
- Migration maps previously opened Segmented levels to available grid radii 2/3/4; imported weapons retain the old linear damage-upgrade multiplier, and original orbital data remains available as a rollback source. Sniper is retained as production compatibility content; the lab's visible catalogue stays unchanged.

## Verification completed — 02.11.2026, Unity 6000.3.13f1

- `TelekineticProductionTests`: **3 passed / 0 failed**, completed at 22:27 Moscow. Two model checks cover whole-footprint invalid swap rollback, repeated valid Glitch/no-pair no-op, reserve ID/level conservation, Merge, JSON restore, opened-space migration and absence of Dev dependencies in the rig prefab. The Play Mode scenario took 23.77 seconds (test-case duration).
- After making the auto toggle state explicit in the UI, repeated only `BunkerRewardControlsAnomaliesAndReturn`: **1 passed / 0 failed**, completed at 22:28:57 Moscow, test-case duration **23.89 seconds**. The route uses StartScreen → bunker → ordinary run, selects two displayed reward requests through the production selection handler, places items, checks Q/E/auto/zero energy, module adjacency/pulses, centre/time owners, all five zone effects, expansion, FR/cooldown preservation on station recreation, and ReturnToBunker cleanup. Legacy live ring/module lists remain empty.
- Player script compilation: **Release PASS, Development PASS**. The final candidate also compiles with **all 90 Dev/Lab sources excluded**: 460 runtime sources, exit 0 in both configurations, via `Tools/QA/Test-ProductionScriptBoundary.ps1` and fresh `19a000fP.dag` / `19a000fPDevDbg.dag` response files.
- Inspected actual 1600×900 Game View PNGs of constructor, combat and suppression. The constructor has earned reserve only, cyan weapon mounts, gray module footprint, side panels and an explicit auto ON/OFF button. Combat keeps the segmented chassis, original HUD and suppression crosses. Fixed a HUD overlap and obsolete ring-reward notification found in the captures.
- Independent read-only feature review found missing persistent weapon damage, changed migrated damage formula, missing camera envelope, FR timestamps surviving a reset clock, module charge reset on view creation and mismatched saw reach. Addressed each; the smoke checks meta damage, framing, restoration and base shooting during suppression.

Temporary results/captures are in gitignored `Artifacts/GeneratedQA/TelekineticProduction`: `results.xml` (three-case result), `ui-smoke-results.xml` (final one-case result), `constructor.png`, `combat.png`, `suppression.png`, compilation and boundary reports.

**Evidence limits:** this is an Editor Play Mode smoke using runtime handlers, not a standalone build, full manual A–Z or observation of players. The automation environment did not reliably provide OS focus/window capture: the smoke verifies rejection of unfocused live pointer input and the existing centre driver; Bullet Time uses its production time owner. Physical mouse/keyboard operation and subjective aiming/readability remain unconfirmed. An ordinary interference zone was admitted through `SpawnSiteZone` when the scene supplied only special Gravity; the special event was preserved. A completed Relay/FalseSignal event was not replayed in this smoke. Numerical module balance and the 12°/s auto feel remain experimental.

All changes are left uncommitted; no staging, branch switching or push was performed.
