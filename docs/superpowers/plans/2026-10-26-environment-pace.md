# Environment and presentation pace implementation plan

> **For agentic workers:** Use superpowers:executing-plans, with one final fresh-context review.

**Goal:** Make the existing cold industrial surface quieter and event beats more readable without changing combat balance.
**Architecture:** Tune existing environment materials/scatter opacity, gate the existing corridor invitation with its existing CanInteract state, and focus relay flashes on the actual active node. Reuse all sprites, shaders and feedback components.
**Tech stack:** Unity 6000.3.13f1; production MVP, existing Play Mode test and ordinary-player QA infrastructure.
**Spec:** User request, 2026-10-26: existing assets, less visual clutter, more atmosphere, no balance change, verify in Play Mode and ordinary build.

## Design and constraints
- Keep layout, scatter counts/seed/placement, colliders, waves, timing, speed, damage, reward and event clocks unchanged. Keep previous damage/orbital readability changes.
- Existing ColdAshLit material: use a restrained cool tint (.78,.9,1), amount .22, saturation .78, brightness 2.35 (authored 2.5). It is referenced only by the existing surface and world prop prefabs, never characters or bunker stations.
- Existing visual scatter gets authored opacity .68, multiplied into original sprite alpha once at creation. No new props, frame work or gameplay RNG calls.
- Glitch material's constant cyan scanline alpha .28 -> .08. Keep event pulse and anomaly physics unchanged.
- Corridor invitation: prompt and moving arrows visible only when the existing CanInteract permits starting. Far-away start circle/beacons remain quiet landmarks; preserve the existing .6s start fade and geometry.
- Relay: start/handoff flash only the active node; transition flash only the previous active node. Existing durations, nodes, pressure and progression unchanged. Soften decorative arena/spawn ranges (.38/.08 alpha).
- Do not add weather, screen fog, particles, prose or assets. Existing floor detail and light provide atmosphere.
- Work in the open checkout, preserve prior MainMenu/door edits, do not commit/publish or modify Deploy/saves.

## Review focus
- Corridor invitation disappears when another event blocks interaction, and stays hidden after start.
- Relay phase changes never point to both nodes and do not restart feedback every render frame.
- Environment grading affects no combat sprites and scatter opacity is applied once, including regeneration.
- Play Mode and ordinary player must use the same production materials, not development-only preset plumbing.
- Existing threats, colored cannons and world warning cues remain visible in actual 1600x900 captures.

## Tasks
- [x] Add runtime QA and focused Play Mode entry, capture baseline, observe intended presentation assertions fail.
- [x] Implement the narrow visual changes in PropScatterProfile/ProductionSectorProps, CorridorStartView/CorridorEvent and OrbitalRelayPresentation plus existing material/prefab data.
- [x] Verify the production MVP with existing assets, RU/EN corridor invitation and relay state/feedback transitions. Run relevant existing combat and event-state regressions.
- [x] Build/run a visible non-Development Windows player using isolated QA save identity. Inspect nonblank 1600x900 screenshots and audit log/checksums.
- [x] Final review, preservation checks and concise report.

## Ledger
- Ruling: user explicitly approved this strategy and asks to proceed; no additional design approval needed.
- Inspection: production ColdAshLit differs from the development lab preset, so change the actual shared world material rather than promote debug consumers into production.
- Known unrelated inactive-anomaly coroutine errors on application shutdown remain outside scope.
- Evidence: baseline fails for opaque scatter, unchanged warm/neutral material, distant corridor prompt/flow, simultaneous relay start flashes, bright decorative ranges and multi-node transition flashes. Stored in Before/ and red-results.xml.
- QA setup correction: disabling OrbitalRelayEvent invokes its owner-disposal OnDisable. Leave the unstarted production event enabled while probing its view; gameplay clock remains inactive. Initial setup failure retained separately.
- QA setup correction: the controller's serialized scatter profile can be null while its initialized scatter.Profile is valid. Read the actual producer profile for regeneration checks. Production behavior was correct.
- Review: no important production findings. Expanded the relay probe to traverse actual required activations, call transition feedback before Render exactly like production, then advance the original TransitionDuration into Bonus and verify counters/one-node feedback. Reviewer confirmed the gap resolved.
- Final verification: 18 distinct cases pass using the latest result for each (14 prior regressions plus the final focused environment/corridor run). Original failed setup and passing XML evidence retained; no synthetic test report. Full details in Artifacts/GeneratedQA/EnvironmentPace/verification.json.
- Visible ordinary player PASS (Editor=False, Development=False); None build succeeded with 0 errors and 10 warnings. Twenty nonblank 1600x900 Game View captures across Play Mode and release player validated, representative frames visually inspected.
- Preservation: 24 gameplay/settings checksums unchanged; 238 props remain deterministic across regeneration without repeated opacity multiplication; MainMenu equals its preserved saved scene. Previous door edits retained; no Deploy/commit operation.
- Limitation: controlled QA availability and presentation probes do not simulate physical mouse/keyboard input. The known inactive Normal Anomaly Site 3/6 coroutine errors and ComputeBuffer disposal warning remain at application shutdown.
