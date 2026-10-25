# ComputeBuffer shutdown lifetime implementation plan

> **For agentic workers:** Use superpowers:executing-plans and one final fresh-context review.

**Goal:** Identify and release the leaking GPU resource at shutdown without changing gameplay or visuals.
**Architecture:** Audit the installed package's actual resource and owner state first; add a narrow lifetime integration only after reproducing the leak. Leave renderer/animation settings and package versions unchanged.
**Tech stack:** Unity 6000.3.13f1, pinned 2D Animation 13.0.5, existing production Play Mode and ordinary Windows QA.
**Spec:** User request 2026-10-26: fix ComputeBuffer warning at exit, find its cause, preserve balance/visuals, verify both runtimes.

## Constraints
- Preserve previous fixes, user scene/door edits, all balance, art, shader, material, prefab and animation settings.
- Do not hide warnings, force CPU deformation, disable rendering, upgrade packages or edit Library/PackageCache.
- Resource must stay valid through normal frames and scene changes, and release once before teardown/finalization.
- Preserve saves and Deploy. Use the existing isolated QA product identity for ordinary builds.

## Review focus
- Actual owner/creation path and missing cleanup evidence, rather than inferring from warning text.
- Correct shutdown/Play Mode stop ordering, idempotence and repeated sessions.
- Package-private compatibility boundary and stripping preservation, if needed.
- No normal-frame resource changes or rendering configuration changes.
- Actual player process exit log, not only a pre-exit PASS marker.

## Tasks
- [x] Add read-only owner/resource audit and failing exit checks; reproduce in production Play Mode and ordinary player.
- [x] Implement the smallest durable cleanup at the proven lifecycle boundary; preserve package cache and configuration.
- [x] Verify scene transitions, Play Mode stop/re-entry and ordinary process shutdown, plus real Game View captures.
- [x] Review the final change, check visual/gameplay source hashes and report evidence.

## Investigation ledger
- No ComputeBuffer/GraphicsBuffer allocation exists in Assets or Packages project source.
- Installed 2D Animation 13.0.5 GpuDeformationSystem.CreateFallbackBuffer runs unconditionally at RuntimeInitialize AfterSceneLoad. Its static fallback is only cleared by GPU deformation-system Cleanup, which requires a constructed owner. This is the leading hypothesis; runtime owner state and A/B shutdown evidence are still required.
- Work in the already-open checkout, preserving the existing dirty scene snapshot and prior changes.
- Runtime proof: ordinary BeforeRelease has 0 scene SpriteSkins, no DeformationManager and 0 GPU owners in MVP/Bunker/MVP, but one valid static fallback (count=64, stride=64). It remains valid at application quitting and produces the exact original ComputeBuffer finalizer warning. Editor differs: it constructs a GPU manager even with no skins, so a pre-teardown valid buffer there is not alone proof of the player leak.
- Narrow fix: invoke the pinned package's existing private idempotent ClearFallbackBuffer only from Application.quitting, registered once at SubsystemRegistration (also covers Play Mode). A small link.xml preserves that private method for stripping; no package cache/version/renderer setting change and no per-frame work.
- Editor UI automation was stopped by its URL-confidence policy. No further UI input was issued; the already-existing Unity authoring API stop command safely ended the current clean Play session and the file-driven verification continued.

## Verification result
- Red: original ordinary player emits the exact ComputeBuffer warning once; retained fallback remains valid at quit. Focused Play Mode exit assertion fails before the fix.
- Green: two Play Mode sessions pass, including stop/re-entry. The ordinary Windows player was built with BuildOptions.None, Development=False, zero build errors (9 existing unrelated warnings), then actually exited. Full player shutdown log contains zero ComputeBuffer warnings or exceptions.
- All three fixed runs keep the resource valid through MVP -> Bunker -> MVP and explicitly prove released=True, fallbackNull=True, retainedValid=False at quit. Six real 1600x900 game captures are nonblank; ordinary-player captures were visually inspected.
- SHA256 check: 3216 existing production files unchanged from the task baseline; the preserved user MainMenu snapshot remains byte-identical. One concurrent external change to RunRewardCalculator removes the death penalty, explicitly documented as a separate user-approved decision in docs/gdd-reconciliation-2026-10-26.md. This task did not edit or revert that file. Baseline matches its HEAD version with checkout line endings.
- Fresh-context reviewer review_compute_buffer found no actionable findings. Disabled domain reload and IL2CPP were reviewed but not execution-tested; claims are limited to the two standard Editor sessions and current ordinary Windows player.
- Evidence: Artifacts/GeneratedQA/ComputeBuffer/verification.json, green-first-session.xml, green-second-session.xml, ReleaseVisible/player.log and each run's shutdown-audit.txt. Ordinary executable: Builds/ComputeBufferRelease/Subject42.exe. No Deploy or user-save changes.
