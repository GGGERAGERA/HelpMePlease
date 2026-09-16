# Character prefab consolidation implementation plan

Goal: one authoritative Gera/DiMag/Vika gameplay prefab, preserving production values and visuals.
Spec: user attachment 47dbd61e-d500-4be3-8fe0-bb7a1a05745d/pasted-text.txt.
Constraints: no tests, no art/gameplay/stat/facing/path changes; only Characters consolidation and required direct consumers.

- [x] Audit resolved Legacy/Production/Visuals components, nested references and direct scene/Lab/config callers. Record evidence in Artifacts/GeneratedQA/CharacterConsolidation.
- [x] Preserve existing production prefab GUIDs and contents; embed character-only nested visual objects, retaining Orbital Station dependency. Move with AssetDatabase to Characters/Gera.prefab, DiMag.prefab, Vika.prefab.
- [x] Rebind direct old prefab references, preserving scene/presentation overrides and internal references. Remove unused CharacterData.characterPrefab fallback and adapt its three code consumers to ProductionPrefab.
- [x] Remove Legacy and redundant character Visual prefab assets only after all their references are handled; preserve genuinely separate capsule presentation if required.
- [x] Compile and inspect serialized references; compare full character snapshots, facing, stats/path and protected art hashes. No tests/Play Mode. Remove temporary migration driver, report and stop.
