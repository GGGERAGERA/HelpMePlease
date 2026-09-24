# Subject#42 — Bunker Architecture Kit v1

32 px orthographic Tilemap kit. One shared atlas and palette, five reusable structural templates, five room prefab variants, five preview scenes. No runtime scripts, furniture, NPCs or props.

## Entry points

- Atlas: BunkerArchitecture_32.png — 192 x 128 px; Multiple; 32 x 32 sprite cells; 32 PPU; Point; no compression or mipmaps.
- Palette: BunkerArchitecture_Palette.prefab. Select it in Unity's Tile Palette window.
- Templates: Templates/*.prefab.
- Rooms: Rooms/*.prefab, each a prefab variant of the matching template.
- Scenes: Assets/_Project/Scenes/BunkerArchitecture/KitV1/*.unity.

| Template | Room | Floor / accent |
|---|---|---|
| Neutral_Box | Neutral_Room | Cold grey-blue / steel |
| Wide_With_Niches | Lounge_Room | Softer warm grey / muted amber |
| Inner_Partition | Lab_Room | Cool blue / cyan |
| Side_Pockets | Utility_Room | Dark technical grey / ochre |
| Back_Alcove | Secret_Room | Dark base, blue rear alcove / muted violet |

## Required tiles

Floor_Base_A, Floor_Base_B, Floor_Base_C, Floor_Lounge, Floor_Lab, Floor_Utility;
Wall_Straight_H, Wall_Straight_V, Wall_OuterCorner, Wall_InnerCorner, Wall_End,
Door, Wall_Projection, Wall_Partition, Threshold_Door, Inset_Niche, Small_Recess, Zone_Border.

Five existing refinements are retained in the same atlas/palette: Floor_Lounge_B/C, Floor_Lab_B/C and Wall_Panel. No duplicate atlas.

## Authoring

Each template has one unit-cell Grid and three Tilemaps:

1. Floor: quiet solid surfaces. A is clean; B contains subtle wear; C contains a soft panel seam. Use variants in sparse coherent patches.
2. Walls and entrance: dark full-cell boundaries with a narrow metal cap. H has the cap above; V has the cap on the right. Rotate in 90-degree increments for the opposite sides. OuterCorner is the top-left outer corner; InnerCorner joins inward steps. Partition is a two-sided divider. Projection caps a short intrusion. Inset_Niche and Small_Recess are solid wall-face architectural treatments, not traversable openings.
3. Architecture accents: transparent Threshold_Door and Zone_Border overlays. The room variant sets this Tilemap's restrained accent colour.

Edit geometry on the template to propagate it to its room variant. Edit floor theme/accents on the room variant. Place prefabs at integer world positions with scale (1,1,1). Each scene contains only the room and an orthographic preview camera.

Walls use Grid tile colliders and a static Rigidbody2D + polygon CompositeCollider2D. A 0.01-unit extrusion joins floating-point corner seams. Floor, Door and accent tiles have no collider. Door is an open architectural entrance, with no animated/interactive door logic.

Existing Wall_Straight and Wall_Corner assets were renamed to Wall_Straight_H and Wall_OuterCorner with their GUIDs preserved. Earlier rooms retain their asset references. Previous study scenes remain available outside KitV1.
