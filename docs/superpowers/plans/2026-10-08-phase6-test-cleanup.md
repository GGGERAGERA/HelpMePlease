# Phase 6 — Test cleanup and Dev boundary

**Goal:** Execute the user-supplied Phase 6 specification; stop before Phase 7.
**Authority:** Pasted Phase 6 request, 2026-10-08. Existing checkout `TestByDantes`; work in place to preserve the active Unity session.
**Architecture:** Preserve gameplay behavior and GUIDs. Runtime owns seeded gameplay input and passive debug hooks; Dev supplies input and creates its own menu. Consolidate useful authoring validation and delete tests whose only assertion is source spelling or existence.

- [x] Audit and reduce existing tests; map every removed contract to retained behavioral coverage. Keep lifecycle, deduplication, save/load, atomic placement, session tokens, cleanup, deterministic logic and real regressions.
- [x] Separate targeted runner ownership and route diagnostics into ignored `Artifacts/GeneratedQA`. Retain Golden Path/Batch; do not execute them.
- [x] Remove runtime references to Bot/Dev/Lab types; move shared feedback contracts/runtime with their metadata only where production already consumes them.
- [x] Remove Dev component from production HUD; Dev bootstrap binds production services in editor/development contexts. Eliminate release stub.
- [x] Decide on asmdefs only after dependency cleanup; prefer deferral over expanding scope into third-party/predefined assembly restructuring.
- [x] Verify Editor compile, release Player scripts, development Player scripts, selected valuable fixtures, authoring graph and runner outputs. No Golden Path, Batch, or full gameplay matrix.
- [x] Record actual before/after counts, categories, changes, evidence and remaining Phase 7 work.

Review focus: seeded streams must preserve draw order and Unity fallback; session teardown must clear hooks; additive scene loads must not duplicate debug menus; prefab references must not become missing; global TestRunner callbacks must not overwrite another owner's XML.

## Progress

Initial audit: clean checkout. Baseline: 44 fixture files / 147 declared cases (includes TestCase expansion and combined attributes, confirmed by Unity discovery). Unity was already open on this project. Phase 0 standalone audit was not found; the explicit Phase 6 candidate list and current coverage are the audit basis.

Final inventory: 46 files / 138 cases. Fifteen redundant cases removed; six boundary/input regression cases added. Selected verification: 56/56, zero skipped. Editor domain loaded, release and development scripts compiled; independent compilation of all 418 runtime source files with all 37 Dev sources excluded passed in all three configurations.

Ruling: retain useful regressions in migration-named files; remove historical/source/GUID assertions rather than deleting lifecycle coverage. Cost: filenames retain history.
Ruling: authoring Dev dependency checks cover every production prefab/config plus enabled build scenes; missing-reference checks cover referenced prefab MonoBehaviours. Unused crystal missing script is outside this active-reference validation and is deferred to asset cleanup. Cost: this check is not a full scene/renderer missing-reference validator.
Ruling: clear two unresolved optional laser material references to explicit null, preserving their existing effective null value; do not substitute art or alter rendering logic.
Ruling: defer asmdefs. Dev tuning still calls an internal runtime move-speed API; authoring helpers use assembly identity filters. A complete split needs visibility/filter decisions and Editor/test assembly routing, beyond merely adding four files. Production without Dev is proven by a reusable compiler boundary check now; it is not claimed to be permanently enforced by asmdefs. Cost: future runs must keep this check until assembly boundaries are introduced.
Review: fresh read-only reviewer found additive debug-menu ownership and missing release guards. Both fixed. Additive owner regression failed 2 versus 1 before fix, then passed. Guards verified against actual release assembly (zero qualified project Dev type definitions). Retained tutorial regression moved into isolated PlayMode; its localization service is configured while inactive before Awake.
Verification outputs are ignored under Artifacts/GeneratedQA/Phase6. Source docs hashes were unchanged during test runs. Initial old callback changes to tracked XML were restored; final tracked XML diff is empty. Phase 7 was not started.
