# Phase 7 — final cleanup

Scope: dead bundles/assets, obsolete code and serialized compatibility, reference integrity, current-state docs. No gameplay or visual redesign.

1. Audit each candidate against GUID, code/path/name consumers and serialized data; remove only proven dead bundles. Keep live identities and import settings.
2. Validate all production scenes, prefabs and configs plus dependency closure. Distinguish unresolved object references from intentional null; detect missing scripts and Dev dependencies without modifying assets.
3. Repair the WorldSystemsLab's stale authored tactical-map references, retaining production marker components in its builder.
4. Add two bounded smoke scenarios: StartScreen → Bunker → Run → real World Event completion → clicked reward → Bunker; WorldSystemsLab production Relay start and owner cleanup.
5. Update project/architecture/folder/Lab/test navigation docs. Reassess asmdefs against concrete visibility and authoring filters; preserve existing compiler boundary enforcement if splitting brings routing/API churn.
6. Verify Editor, release and development compilation, no-Dev production compile, GUID/orphan audit and selected contracts plus exactly the two smoke scenarios. No Golden Path, Batch or full suite.
7. Review diff, report remaining risks and stop after Phase 7.
