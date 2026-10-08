# Phase 7 — final cleanup and refactor summary

The final pass keeps gameplay/balance/visual behavior intact. Each deletion is backed by serialized GUID, source/path/type and dynamic-consumer audit. Detailed evidence: [assets](phase7-asset-audit.md), [code](phase7-code-audit.md).

## Cleanup

- Removed 16 dead assets (including crystal with missing script, old standalone GravityZone, AboutPanel and MainMenu_old), their metadata and empty folders. Live weapons, production Gravity, shared miniWeapons and useful Labs remain.
- Removed the four obsolete character UI classes (30 serialized fields), unusable navigation authoring, two dead helpers/migration branches, six completed serialization aliases and 54 unused localization keys. Kept 16 unread fields with actual serialized data, the live speed alias and save/enum compatibility.
- Removed 95 inert prefab overrides, 14 absent-target removed-component rows, five orphan component-list entries, and four inert scene overrides. Existing object headers and asset metadata are preserved.
- Moved shared HoldInvestmentInput into UI/Common; moved Dev navigation, gallery data, old test scene and test projectile into Dev. Existing GUIDs are preserved; the three Dev asset moves retain exact bytes.
- Repaired WorldSystemsLab's three required map marker references and stopped its builder stripping TacticalMapMarker. Longer existing Lab tests no longer expect the obsolete missing-map error.
- Updated PROJECT_MAP, architecture/folder/navigation notes and Dev/Labs/test/SurfaceVisualLab navigation to current ownership and paths.

## Integrity enforcement

The reusable validator inspects saved .unity/.prefab/.asset files under `_Project/Scenes`, `_Project/prefabs`, `_Project/Data`, plus enabled build scenes, including unreferenced authoring assets. It recursively follows real production dependencies into other folders/packages and checks scene/prefab inactive components, ScriptableObjects/subassets, renderer material/sprite and MonoBehaviour refs, missing scripts, external GUID/subasset IDs and local file IDs. Preview scenes preserve open/dirty editor state. Seven fixture cases cover the real production graph, intentional nulls, deleted sprites, bad subasset IDs, missing scripts/local IDs, scene preservation and physical package paths.

Explicit `fileID: 0` is accepted as an optional reference; `m_Script: 0` is an error. Required-reference semantics still belong to component authoring/runtime validation. Imported model source IDs and native importer metadata are handled explicitly. Dynamic string lookups and unsaved edits are outside this saved-asset scan. Source artist/demo content outside the production graph is audited separately, not silently represented as production-clean.

Verification outputs are ignored under `Artifacts/GeneratedQA/Phase7`. The existing compiler tool and no-Dev response-file check are reused.

## Assembly decision

No asmdefs introduced. Runtime→Dev source dependencies are eliminated and guarded Dev types are absent from release. A four-assembly split still changes concrete visibility/routing contracts: WorldSystemsLab uses internal `ProductionSectorProps`; F1 calls internal `PlayerLoadoutFactory.ApplyDebugMoveSpeed`; WorldSystemsLab authoring has two assembly-identity component filters; Editor/tests live in nested Dev trees and require explicit assembly routing/references. Introducing that split adds churn without another gameplay benefit to this cleanup.

Keep `Tools/QA/Test-ProductionScriptBoundary.ps1` as enforcement: it recompiles Unity's actual Editor/release/development response files with every project Dev source removed. A future asmdef pass should design narrow friend-assembly/API access, update filter semantics and route Editor/tests explicitly; no mass public conversion or namespace change is needed now.

## Whole refactor result

| Area | Current ownership/contract |
| --- | --- |
| Ownership | Run identity rejects stale callbacks; owners release their own roots, subscriptions and scoped effects; terminal reward/cleanup paths are idempotent. |
| Scene composition | Typed authored ProductionSceneComposition binds services/local consumers before use; readiness gates transition reveal. |
| World systems | Sector sites own their environment; event admission/reward registry does not steal that ownership. Corridor and Orbital Relay share production runtimes/assets with tutorial/Lab adapters. |
| Bunker / ORBITAL | Current Bunker contexts/panels/progression select production CharacterData/loadouts. ORBITAL state, combat, interaction and queued rewards have explicit owners. |
| UI | Authored HUD/cards/windows/markers and shared input/transition rules replace stale legacy bundles and runtime fallbacks where migrated. |
| Tests / Dev | Behavioral/regression/authoring checks use narrow filters; Dev bootstrap/tools are guarded and the no-Dev production compile proves the source boundary. |
| Legacy / assets | Proven dead bundles and stale overrides removed; live GUID/import identities and real compatibility consumers preserved. |

## Deliberate limits and known risks

No new gameplay/visual/balance pass, broad artist/audio library pruning, live weapon-path replacement, persistent-save schema redesign, mass namespace/folder changes, Golden Path/Batch/full-suite run or asmdef split was performed.

Production and Dev script audit resolves all MonoBehaviour scripts. Retained Cainos artist demos have 11 missing-script components in 11 source/demo files outside production; these have demo consumers and were preserved. Gallery data retains two unresolved source textures; the old Dev test scenes and projectile retain missing old assets/sprite/controller. No exact safe replacements were established and no arbitrary nulls hide those data issues. Exact inventories are in the asset audit and `Artifacts/GeneratedQA/Phase7/missing-script-inventory.txt`.

## Final verification

| Check | Fresh result / evidence under `Artifacts/GeneratedQA/Phase7` |
| --- | --- |
| Editor compile | PASS; final smoke fixture compiled and ran after the final Editor-only edit. |
| Non-development / development player compile | Both PASS; actual Unity player compilation, `Compilation/player-results.txt`. Runtime sources were unchanged after this compile. |
| No-Dev source boundary | PASS in Editor, release and development compiler configurations; 413 runtime sources, 38 Dev sources excluded in each, `Compilation/no-dev-results.txt`. |
| Release type boundary | Zero matching Dev declarations in release; guarded F1/navigation/Lab types present in development, `Compilation/type-boundary.txt`. |
| Production asset integrity | PASS: 506 roots, 1059 inspected assets; no Dev dependencies, broken serialized refs or missing scripts, `ProductionBoundary/validation.txt`. |
| Metadata / GUID integrity | Zero orphan metadata and zero retained references to deleted GUIDs; moved production/Dev GUIDs preserved, exact bytes preserved where no source edit was required. Asset/code audits contain the manifests. |
| Selected high-value tests | All 62 distinct selected cases pass after a targeted rerun. Initial aggregate: 61 passed, one smoke fixture setup failure; original XML is retained as `Verification/selected-tests.xml`. |
| Production smoke | PASS: StartScreen → Bunker → Run → production Orbital Relay → actual reward selection/grant → Bunker; `Verification/production-smoke.xml`. |
| WorldSystemsLab smoke | PASS in the selected run; shared Relay admission/ownership, independent territory and cleanup checked. |

The production fixture's two failed setup attempts exposed an EditMode test iterator closure lost across EnterPlayMode domain reload. The fixture now constructs its route iterator after reload; the targeted case passes. This changed test setup only. The final per-case results and original failure evidence are recorded in `Verification/verification-summary.txt`; no aggregate XML was rewritten to conceal the earlier failures.

Golden Path, Batch Runner and the full test suite were not run. Acceptance is the selected set and the two requested bounded smokes, not a full-suite claim.

Phase 7 is the stopping point; no follow-up gameplay/visual pass is started automatically.
