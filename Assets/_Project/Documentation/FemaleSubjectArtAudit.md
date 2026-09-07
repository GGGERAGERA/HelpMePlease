# Female Subject — art pipeline audit

Date: 2026-09-07. Status: audit complete; production character NOT delivered. Both generated candidates failed acceptance. No prefab, animation assets or gameplay integration were created.

## Existing production path

- `Scriptable Objects/Characters/01_Gera.asset` references `prefabs/players/p_Player3.prefab` (GUID `1868b54bca0976a44b3b858e4529d00d`).
- `CharacterSpawner` resolves that source through `Resources/OrbitalStation/OrbitalPresentationConfig.asset`, then instantiates `Resources/OrbitalStation/Authored/Player_0_p_Player3.prefab`. An unmapped character cannot spawn in this flow.
- Active art: `Graphic/Gera4(2)`, `art/Sprites/Aseprite/newCharacter4/Gera4.png` (GUID `8c6d4adfa9a298d42a0aca3f3de80a59`). Several alternative historical visuals remain inactive in the existing prefab; they must not be treated as current art.
- Graphic Animator: `art/animations/Player/Player1/Player1.controller`. `CharacterMovement2D` sets float `Speed` and reflects `visualRoot.localScale.x`; positive X movement uses negative scale. Author the sheet facing left.
- No distinct north/south animation parameter exists. Movement in every direction uses the same movement cycle, retaining the last horizontal facing.

## Technical requirements

| Property | Existing value |
|---|---|
| Texture | 400 × 50 RGBA, eight 50 × 50 cells |
| Active idle frames | 0–3; sprite keys at 0, 0.1125, 0.2375, 0.35, repeat first at 0.475 seconds |
| Active movement frames | 4–7; keys at 0, 1/3, 2/3, 1 second |
| Movement state speed | 2 |
| Animator transitions | Speed > 0.1 / Speed < 0.1, no exit-time requirement, zero transition duration |
| PPU | 32 |
| Slice pivot | Bottom centre: (0.5, 0), alignment 7 |
| Active visual scale | (1.5, 1.5, 1.5) |
| Import | Sprite Multiple, Point, Clamp, no mipmaps, NPOT scaling disabled |
| Compression | Default platform uncompressed; platform overrides disabled |
| Material | Existing SpritePlayer material GUID `a97c105638bdf8b4a8650670310a4cd3` |
| MVP camera | Orthographic, serialized size 7 |

Measured source palette: 15 distinct visible RGBA values, including 10 pixels with partial alpha. New art should satisfy the requested stricter binary alpha requirement instead of reproducing those partial-alpha pixels.

The current clips animate multiple historical sprite paths and include transform scale curves. New character clips should bind only the new renderer's sprite; do not copy unrelated historical bindings or scale breathing into the new character. Keep the established Speed parameter and horizontal reflection contract.

## One design direction

Adult woman, escaped medical test subject. Three readable anchors: asymmetric dark auburn bob, dirty ivory medical wrap with a diagonal graphite restraint, enlarged coral/amber abnormal forearm retained by dark cuffs. Charcoal trousers and practical boots. Compact proportions consistent with Gera, exposed face, human silhouette. No weapon or equipment added to the sprite; ORBITAL supplies equipment independently.

Requested generator specification: native 50 × 50 cells; 8 idle plus 8 walk frames in a 400 × 100 RGBA sheet; character approximately 30–34 pixels tall; fixed feet baseline; left-facing three-quarter view; maximum 16 solid colours; binary alpha; no antialiasing, gradients, external glow, downsampling, or background.

## Acceptance and integration

Before producing imported assets, verify dimensions, colour count, binary alpha, frame bounds, consistent body volume, foot baseline and native-scale readability. A large generated illustration is not a production sprite and must not be downsampled to bypass this gate.

Once accepted: author sprite-only Idle/Move clips, a separate character prefab inheriting the working gameplay composition, CharacterData with unchanged baseline stats, and an explicit ORBITAL production mapping. Keep current characters and scene defaults intact. Use a separate test scene copied from current MVP on disk, with this CharacterData as its default; verify actual spawned production prefab, both horizontal facings, vertical movement, stopping, dash, restart and ORBITAL ownership. Do not automatically open or save over dirty scenes.

Required visual QA remains a neutral-background image and a real gameplay capture with enemies and ORBITAL rings. The camera's serialized size alone does not establish final on-screen pixel size: inspect actual Game view resolution and camera behaviour.

## Tool limitations observed

The first built-in image generation returned 1536 × 1024 art with a painted background and excessive detail despite the native-grid request. It was rejected for production use. A targeted second generation used the actual Gera sheet as a technical reference. Pixel inspection of the second output found 1774 × 887 pixels, 103,553 distinct opaque colours, zero transparent pixels and zero partial-alpha pixels. Its checkerboard is painted into the image. This output is preserved as `FemaleSubject_ConceptOnly.png`, solely as a design reference, not a sprite sheet. Neither candidate was downsampled or connected to gameplay.

Built-in tool used: `image_gen.imagegen`, no CLI/API fallback. Final revision prompt:

> Edit target image 1 to a REAL native low resolution transparent GAME sprite sheet. Image 2 is the TECHNICAL/style reference, its actual native sprites 50x50 pixels, figure about 30 pixels tall. MUST MATCH image 2 small pixel count and chibi head/body proportions. Keep woman design identity from image 1 (auburn bob, white medical wrap charcoal restraint, coral changed forearm) but RE-DRAW with large SIMPLE clusters from scratch, remove all HD detail and background. Output EXACTLY 400 pixels wide by 100 pixels tall, with 8 columns x 2 rows each 50x50. Top row idle 8, bottom row walk left 8. 16 flat opaque palette colors MAXIMUM; all other pixels fully transparent alpha zero. NO gradients, NO glows outside silhouette, NO semi-transparent pixels, NO antialiasing, NO lighting backdrop. Character 30-34 pixels tall in each cell same scale as technical reference, head 11x11 pixels, feet baseline y47. This is pixel data for Unity, not a picture of pixel art, do not enlarge. Critical correction: previous image was 1536x1024 detailed art with painted background and is unusable. Need true native 400x100 RGBA.

Unity window capture through computer-use failed twice with `SetIsBorderRequired failed: Интерфейс не поддерживается (0x80004002)`. No gameplay screenshot or Play Mode result is claimed.

Existing unrelated uncommitted gameplay, scene, test and progression work was present before this task. It must remain intact.
