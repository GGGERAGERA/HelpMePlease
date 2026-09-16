# Orbital authored ownership migration plan

> Execute inline in the existing Unity project; the user requests a bounded migration, compile/reference verification, and then stop. No branch integration or full regression suite.

**Goal:** Remove the private Resources prefab hierarchy without altering prefab contents or runtime behavior.

**Architecture:** Use Unity AssetDatabase.MoveAsset for the existing assets, preserving GUIDs and local file IDs. CharacterData remains the production character selector. Orbital module compositions retain their existing nested references to authoritative miniWeapons.

**Tech Stack:** Unity 6000.3.13f1, serialized YAML, C# editor API.

**Spec:** User attachment `b81b4bf6-6429-4a09-bdbb-2d3d2339529a/pasted-text.txt`.

## Constraints

- No prefab regeneration, duplication, visual/gameplay changes, transforms, icon edits, or miniWeapons edits.
- Delete only verified duplicates; no equivalent production replacements were found, so preserve all 19 assets.
- Existing base player prefabs lack the production station composition. Four module compositions are nested-prefab consumers, not copies of miniWeapons; LinkNode has a distinct circle-based view.
- Do not run the full regression suite.

## Tasks

- [x] Audit all 19 assets, incoming GUID references, component classes, scripts, sprites and nested sources; compare existing players/miniWeapons and search for equivalent Orbital/FX assets.
- [x] Move three character compositions to `Assets/_Project/prefabs/players/Production/`; five module compositions to `Assets/_Project/prefabs/Orbital/Modules/`; Ring, Mount, CustomOrbitDrawing to `prefabs/Orbital/Rings/`; Station to `prefabs/Orbital/Core/`; StationEnergyLine to `prefabs/Orbital/FX/`; two materials to `_Project/Materials/Orbital/`; four sprites to `_Project/art/Orbital/`. Assert file hashes, GUIDs and dependency integrity after every move.
- [x] Remove obsolete serialized PlayerVariants from OrbitalPresentationConfig (the field does not exist in C#). Change WorldSystemsLabAuthoring to load Vika CharacterData and use ProductionPrefab. Update two authoring output paths and two character path assertions.
- [x] Verify compilation, config validation, all three CharacterData references, affected prefab/scene dependency closure, missing GUIDs/scripts, all preserved file hashes and unchanged miniWeapons. Run only focused existing identity/prefab tests.
- [x] Write per-asset audit and final results to `docs/orbital-source-of-truth-migration.md`; stop.
