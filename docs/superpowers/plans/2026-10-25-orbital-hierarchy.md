# Orbital visual hierarchy implementation plan

> **For agentic workers:** Use superpowers:executing-plans for inline execution and one final review.

**Goal:** Keep round colored cannons and combat threats above passive territory decoration in the visual hierarchy.
**Architecture:** Reuse territory meshes/boundary renderers and the cannon's existing non-contact halo. No new effects or combat geometry.
**Tech stack:** Unity 6000.3.13f1, existing production scene composition and QA runner.
**Spec:** User-approved gameplay strategy, stage 3; continuation requested 2026-10-25.

## Constraints and design
- Keep weapon colors, damage, cadence, hitboxes, movement, anomaly effects, saves and Deploy unchanged.
- Reduce territory tint alpha from .20 to .07; reduce background boundary alpha to 25% and focused boundary to 60% of its original color, retaining the existing focus blend and shared-segment ownership.
- Retain the existing subtle selection halo and stronger colored shot pulse (.62). Keep selection, preview and drag states authoritative.
- Reduce the authored focus overlay's outside darkness from 1 to .28, preserving the zone geometry and transition.
- Preserve weapon hue during shot whitening; use 25% white in the weapon accent instead of pure white. Keep enemy/player feedback unchanged.
- Keep the already-open checkout and preserve existing door/MainMenu changes. No new approval needed for the approved stage.

## Review focus
- Shared borders and focus transitions remain visible with multiple neighboring territories.
- Multiple cannon colors remain distinct while charging/firing.
- Selection/drag/reward preview and depth opacity remain correct.
- Halo changes never participate in contact geometry or enlarge framing.
- Normal launches do not activate QA; Play Mode and non-Development player produce real nonblank Game View captures.

## Tasks
- [x] Add a production-scene hierarchy scenario, capture pre-change evidence, observe focused assertions fail.
- [x] Adjust passive territory presentation and preserve shot color on existing sprites.
- [x] Run focused and previous combat regression checks; inspect Game View at 1600x900 with multiple cannons and an enemy group.
- [x] Build/run an ordinary visible Windows player, inspect captures and verify unchanged gameplay/settings hashes.
- [x] Obtain final code review, record results and limitations.

## Ledger
- Inspection: production projectile modules preserve authored body colors during charge. Their short shot flash currently lerps all sprites toward pure white by .72; change only weapon shot feedback, not the idle body art.
- Existing anomaly shutdown coroutine errors remain outside this visual change.
- Ruling: all three current projectile profiles have ChargeSeconds=0. Remove the proposed charge indicator and its QA assertions; idle/selection/firing are the real current states. No artificial charge behavior is introduced.
- Evidence: pre-change production captures show territory alpha .20, focused boundary .902 and a fully black adjacent zone. The focused test fails for muted boundaries/fill and weapon hue loss; captures retained in Before/.
- Review: no production defect found. Address the QA gap by placing an actual production enemy outside the focused collider, verifying its viewport position and the active overlay's property block before capture.
- Preliminary regression verification: 11/11 passed. After the QA-only coverage improvement, repeat the focused Game View scenario and ordinary build verification.
- Final evidence: focused repeat PASS; actual production MVP Play Mode and visible ordinary player both PASS. Eight nonblank 1600x900 captures inspected/validated, including a real enemy outside the active focus area. None build succeeded, 0 errors, 10 warnings. Detailed evidence: Artifacts/GeneratedQA/OrbitalHierarchy/verification.json.
- Preservation: all 12 gameplay/settings hashes match; MainMenu equals the preserved saved scene. User door edits retained. No commit or Deploy operation.
- Limitation: direct shutdown still logs the known inactive Normal Anomaly Site 3/6 coroutine errors and a ComputeBuffer disposal warning. Controlled QA positions and presentation probes do not claim physical input testing.
- Review: final reviewer confirmed the outside-threat QA gap resolved, with no further correctness finding.
