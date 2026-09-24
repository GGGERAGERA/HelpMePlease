# Subject#42 — Modular Bunker Architecture 32

The approved material-study language translated to native Unity Grid/Tilemap assets. This kit contains one empty test room, not a room generator. No runtime scripts, custom managers or procedural repair are required.

## Open in Unity

- `Subject42_Bunker_Palette.prefab`: select **Subject42_Bunker_Palette** in Window → 2D → Tile Palette. Native GridPalette settings are embedded in the prefab.
- `Subject42_Empty_10x8.unity`: the single visual test scene.
- `Subject42_Empty_10x8.prefab`: the same room as a reusable Grid prefab; the preview camera belongs to the scene, not the prefab.
- `Subject42_Bunker_32.png`: one atlas, 288×216 px. Each sprite is **32×32**, with two pixels of extruded padding per side (36 px atlas pitch). Use the imported named sprites; do not re-slice on a 32 px atlas grid.
- `Tiles/`: 47 native Tile assets. The 22 requested modules are included; the remainder are explicit directional variants, seam junctions and a coherent 2×2 wear patch.
- `Bunker_Unlit.mat`: URP 2D Sprite Unlit material. The kit owns its material; it does not depend on the previous architecture kit's assets.

Import: 32 PPU, Point, Full Rect, no compression, no mipmaps, no NPOT scaling. Grid cell size is 1×1. Keep parent transforms at unit scale; place room prefabs on integer positions. For pixel-exact screenshots, use integer pixel magnification.

## Material rules

Floor_Fill_Clean is the primary surface, without a cell border. Floor_Fill_Worn and Floor_LargePanel_Variant are restrained alternatives, not alternating checkerboard entries. In the example, 80 interior cells are grouped into five visible metal-sheet fields using only a few seam runs.

Floor_Seam_H/V/Corner are transparent weld overlays. Their junction is near the middle of a cell; connect matching ends using Floor_Seam_T, its directional variants, and Floor_Seam_End. A visible sheet should span several cells. Never outline every fill tile.

Floor_WornPatch_NW/NE/SW/SE form one 2×2 scuff cluster. Place the four fragments together; do not scatter fragments randomly. The test uses three clusters and otherwise quiet fill.

Wall_Top_H/V supply the heavy cross-section and pale structural bevel. Wall_Face_H/V supply the separate facade/return. Do not replace the facade with a contour stroke. Use the explicit right/south variants and named corner variants instead of rotating light-bearing sprites indiscriminately. OuterCorner has a convex external chamfer; InnerCorner is the concave junction treatment. Tile names NE/SW/SE describe their orientation relative to the default NW form.

Wall_End exposes a cut wall end. Wall_Pillar interrupts a long facade at a structural joint; avoid a pillar at every cell. Inset_Panel and Wall_To_Door_Transition are detail overlays. Corner_Bevel is a local accent, not a continuous bright border.

Door_Left/Right are jamb details over the supporting wall cells. The test leaves a **two-cell opening** between them. Door_Threshold sits over continuing floor. Door_Header is an optional overhead lintel: its renderer is deliberately disabled in the top-down cutaway so that the entrance remains visible. It has no collider. Enable it only when the chosen camera/cutaway presentation needs an overhead lintel.

Base_Shadow_Strip attaches to the upper edge of a floor cell. Base_Shadow_Left/Right/Bottom are explicit orientations. These are contact shadows, not a replacement for a wall facade or global illumination.

## Example ownership and layers

1. Floor fill: x=0…9, y=0…7 is the clear 10×8 interior; four extra floor cells continue through the entrance at x=4…5, y=-2…-1.
2. Large sheet seams: only material boundaries; no colliders.
3. Wall facades: upper/southern vertical faces; TilemapCollider2D.
4. Wall cross sections: perimeter masses; TilemapCollider2D.
5. Structural details: pillars, inset panels, jamb graphics; no duplicate colliders.
6. Contact shadows: shallow transparent attachment shadows.
7. Open doorway threshold: two repeated threshold modules over floor.
8. Optional overhead header: saved native Tilemap, renderer off for this cutaway.

The collision-bearing layers alone define solid walls. The entrance contains no tiles in either collision-bearing layer. Faces/tops remain separate for authoring clarity. All geometry and references are serialized; there is no Awake/Start assembly step.

## Scope

The kit preserves the reference's large quiet sheets, gunmetal wall masses, pale bevels and two rare cyan entry indicators at a native 32 px module scale. It is a pixel-art interpretation, not a downscaled single-image room. No furniture, gameplay scripts, navigation data or additional rooms are supplied.

Previous BunkerArchitecture assets are not migrated or deleted: they are uncommitted and referenced by existing scenes. This directory is self-contained so that replacement can be decided independently of those scenes.

Temporary authoring scripts, generated comparison sheets, Unity logs and verification reports are kept outside Assets in the project's ignored Artifacts/GeneratedQA/BunkerArchitectureV2 directory.
