# Mission Flow Implementation Plan
> Implement inline with superpowers:executing-plans; implementation inline; independent read-only review.

**Goal:** Complete one production mission with persistent one-time turn-in reward.
**Architecture:** Definitions + Meta-owned MissionService; priority content source reuses RunContentResolver. Separate versioned PlayerPrefs mission storage, currency receipt transaction.
**Tech Stack:** Unity 6, C#, ScriptableObject, current bunker UI/interactions.
**Spec:** ../specs/2026-10-05-mission-flow.md

- [x] Add MissionDefinition/Catalog, objective/reward definitions, MissionState/Service/Storage. Check provisional vs committed progress and receipt idempotence.
- [x] Extend SurfaceContentService sources/marker resolution; route existing callbacks through MetaProgressionManager. Configure catalog in ProductionSceneComposition prefab. Check event deduplication and underlying unknown preservation.
- [x] Add MissionProvider, panel/prefabs and one authored Operator in MainMenu. Extend current panel manager and marker panel; no scene-specific service duplication.
- [x] Run only MissionServiceTests and MissionProductionFlowTests; use real False Signal callback and final boss victory, reload storage and reject repeat payout. Check death/abort and wrong run/sector/event/unconfirmed victory. Preserve player preferences and report results.

Verification: targeted MissionServiceTests + MissionProductionFlowTests: 8/8 passed, 19.57 seconds. Fresh-context review: no actionable findings. Runtime screenshots and result XML: Artifacts/Missions/.
