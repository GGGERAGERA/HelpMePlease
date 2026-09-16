# Orbital SOURCE-OF-TRUTH migration — 2026-09-16

## Scope and ownership decision

19 assets (13 prefabs, four PNG sprites, two materials) were inventoried in `Assets/_Project/Resources/OrbitalStation/Authored/`, plus 19 importer `.meta` files and the folder `.meta`. Every asset was moved with `AssetDatabase.MoveAsset`; the empty source folder was removed with `AssetDatabase.DeleteAsset`. No production asset was deleted or regenerated.

The three production characters are **not equivalent** to `prefabs/players/p_Player*.prefab`: they contain an authored Station instance and production composition, while sharing the same nested character Visuals assets. Replacing them with the base prefabs would remove that composition. Their existing GUIDs and CharacterData references are retained under `prefabs/players/Production/`.

Pistol, LaserSword, ImpulseGun and ArcEmitter are **composition prefabs**, not copies of the miniWeapons. Each contains a nested reference to the original miniWeapon, an OrbitalModuleView, a selection halo and existing component-removal overrides. Raw miniWeapons do not contain OrbitalModuleView and include colliders; directly assigning them to the config would fail ValidateRequiredReferences and change behavior. The compositions move to `prefabs/Orbital/Modules/`; their underlying art remains authoritative in `prefabs/miniWeapons/`. LinkNode is a distinct circle-based composition, not a Core duplicate. Station already nests the original miniWeaponCore1.

All original prefabs, transforms, sprites, icons, materials, importer settings, GUIDs and local file IDs are unchanged. No new runtime mapping table was introduced.

## Per-asset audit (before migration)

Incoming references below are serialized GUID matches; editor/test path consumers are also listed where present. Every path is project-relative. Categories CONFIG and OTHER contain no standalone assets in the original folder (the catalog was already its parent).

### ArcEmitterVisual.prefab

- **PATH:** `Assets/_Project/Resources/OrbitalStation/Authored/ArcEmitterVisual.prefab`
- **TYPE / CATEGORY:** `.prefab` / MODULE
- **GUID:** `38c8927e5d970394aa4e821a63f73ac9`
- **REFERENCED BY:**
  - `Assets/_Project/Resources/OrbitalStation/OrbitalPresentationConfig.asset`
- **DUPLICATE OF:** No byte-identical asset in Assets; no equivalent production replacement confirmed.
- **CURRENT PRODUCTION USAGE:** OrbitalPresentationConfig.GetPrefab → OrbitalModuleView; production and Labs share the same composition. Uses original nested miniWeapon listed below.
- **TARGET AUTHORITATIVE ASSET:** `Assets/_Project/prefabs/Orbital/Modules/ArcEmitterVisual.prefab`
- **COMPONENT CLASS IDS:** `{"1": 2, "1001": 1, "114": 1, "198": 2, "212": 4, "4": 3}`
- **SCRIPTS:** `Assets/_Project/scripts/Combat/OrbitalStation/OrbitalModuleView.cs`
- **NESTED SOURCES:** `Assets/_Project/prefabs/miniWeapons/p_miniWeaponLaser1.prefab`
- **DIRECT SPRITE SOURCES:** `Assets/_Project/Resources/OrbitalStation/Authored/Circle.png`

### Circle.png

- **PATH:** `Assets/_Project/Resources/OrbitalStation/Authored/Circle.png`
- **TYPE / CATEGORY:** `.png` / FX
- **GUID:** `ceb8225d9461f5a4bbe86004909390d0`
- **REFERENCED BY:**
  - `Assets/_Project/Resources/OrbitalStation/OrbitalPresentationConfig.asset`
  - `Assets/_Project/Resources/OrbitalStation/Authored/ArcEmitterVisual.prefab`
  - `Assets/_Project/Resources/OrbitalStation/Authored/ImpulseGunVisual.prefab`
  - `Assets/_Project/Resources/OrbitalStation/Authored/LaserSwordVisual.prefab`
  - `Assets/_Project/Resources/OrbitalStation/Authored/LinkNodeVisual.prefab`
  - `Assets/_Project/Resources/OrbitalStation/Authored/Mount.prefab`
  - `Assets/_Project/Resources/OrbitalStation/Authored/PistolVisual.prefab`
  - `Assets/_Project/Resources/OrbitalStation/Authored/Station.prefab`
  - `Assets/_Project/Scenes/Dev/Labs/CustomOrbitLab.unity`
- **DUPLICATE OF:** No byte-identical asset in Assets; no equivalent production replacement confirmed.
- **CURRENT PRODUCTION USAGE:** Serialized sprite/material dependency of the catalog, production prefabs and/or scenes listed above; no direct Resources.Load of this asset found.
- **TARGET AUTHORITATIVE ASSET:** `Assets/_Project/art/Orbital/Circle.png`

### CustomOrbitDrawing.prefab

- **PATH:** `Assets/_Project/Resources/OrbitalStation/Authored/CustomOrbitDrawing.prefab`
- **TYPE / CATEGORY:** `.prefab` / RING
- **GUID:** `5d10b4fa34f16ad4e952e471b74e669a`
- **REFERENCED BY:**
  - `Assets/_Project/Resources/OrbitalStation/OrbitalPresentationConfig.asset`
  - `Assets/_Project/Editor/CustomOrbitLabAuthoring.cs (output path)`
- **DUPLICATE OF:** No byte-identical asset in Assets; no equivalent production replacement confirmed.
- **CURRENT PRODUCTION USAGE:** Production presentation through OrbitalPresentationConfig; Labs consume the same catalog.
- **TARGET AUTHORITATIVE ASSET:** `Assets/_Project/prefabs/Orbital/Rings/CustomOrbitDrawing.prefab`
- **COMPONENT CLASS IDS:** `{"1": 4, "114": 1, "120": 3, "4": 4}`
- **SCRIPTS:** `Assets/_Project/scripts/Combat/OrbitalStation/CustomOrbitDrawing.cs`
- **NESTED SOURCES:** none
- **DIRECT SPRITE SOURCES:** none; inherited nested sprites checked in Unity

### IconRingSpeed.png

- **PATH:** `Assets/_Project/Resources/OrbitalStation/Authored/IconRingSpeed.png`
- **TYPE / CATEGORY:** `.png` / RING
- **GUID:** `ef3b39e2cb6eab245aee02cf778f58eb`
- **REFERENCED BY:**
  - `Assets/_Project/Resources/OrbitalStation/OrbitalPresentationConfig.asset`
- **DUPLICATE OF:** No byte-identical asset in Assets; no equivalent production replacement confirmed.
- **CURRENT PRODUCTION USAGE:** Serialized sprite/material dependency of the catalog, production prefabs and/or scenes listed above; no direct Resources.Load of this asset found.
- **TARGET AUTHORITATIVE ASSET:** `Assets/_Project/art/Orbital/IconRingSpeed.png`

### ImpulseGunVisual.prefab

- **PATH:** `Assets/_Project/Resources/OrbitalStation/Authored/ImpulseGunVisual.prefab`
- **TYPE / CATEGORY:** `.prefab` / MODULE
- **GUID:** `e0b02d3b73e5c424d958aba343c83a95`
- **REFERENCED BY:**
  - `Assets/_Project/Resources/OrbitalStation/OrbitalPresentationConfig.asset`
- **DUPLICATE OF:** No byte-identical asset in Assets; no equivalent production replacement confirmed.
- **CURRENT PRODUCTION USAGE:** OrbitalPresentationConfig.GetPrefab → OrbitalModuleView; production and Labs share the same composition. Uses original nested miniWeapon listed below.
- **TARGET AUTHORITATIVE ASSET:** `Assets/_Project/prefabs/Orbital/Modules/ImpulseGunVisual.prefab`
- **COMPONENT CLASS IDS:** `{"1": 2, "1001": 1, "114": 1, "198": 2, "212": 3, "4": 3, "95": 1}`
- **SCRIPTS:** `Assets/_Project/scripts/Combat/OrbitalStation/OrbitalModuleView.cs`
- **NESTED SOURCES:** `Assets/_Project/prefabs/miniWeapons/p_miniWeaponImpulseGun1.prefab`
- **DIRECT SPRITE SOURCES:** `Assets/_Project/Resources/OrbitalStation/Authored/Circle.png`

### LaserSwordVisual.prefab

- **PATH:** `Assets/_Project/Resources/OrbitalStation/Authored/LaserSwordVisual.prefab`
- **TYPE / CATEGORY:** `.prefab` / MODULE
- **GUID:** `354ad5f6587b07b4ba24642a0aa52a35`
- **REFERENCED BY:**
  - `Assets/_Project/Resources/OrbitalStation/OrbitalPresentationConfig.asset`
- **DUPLICATE OF:** No byte-identical asset in Assets; no equivalent production replacement confirmed.
- **CURRENT PRODUCTION USAGE:** OrbitalPresentationConfig.GetPrefab → OrbitalModuleView; production and Labs share the same composition. Uses original nested miniWeapon listed below.
- **TARGET AUTHORITATIVE ASSET:** `Assets/_Project/prefabs/Orbital/Modules/LaserSwordVisual.prefab`
- **COMPONENT CLASS IDS:** `{"1": 2, "1001": 1, "114": 1, "212": 3, "4": 3}`
- **SCRIPTS:** `Assets/_Project/scripts/Combat/OrbitalStation/OrbitalModuleView.cs`
- **NESTED SOURCES:** `Assets/_Project/prefabs/miniWeapons/p_miniWeaponLaserSward1.prefab`
- **DIRECT SPRITE SOURCES:** `Assets/_Project/Resources/OrbitalStation/Authored/Circle.png`

### LinkNodeVisual.prefab

- **PATH:** `Assets/_Project/Resources/OrbitalStation/Authored/LinkNodeVisual.prefab`
- **TYPE / CATEGORY:** `.prefab` / MODULE
- **GUID:** `a64bd75b4f740ba48be9197c1d6ae0d6`
- **REFERENCED BY:**
  - `Assets/_Project/Resources/OrbitalStation/OrbitalPresentationConfig.asset`
- **DUPLICATE OF:** No byte-identical asset in Assets; no equivalent production replacement confirmed.
- **CURRENT PRODUCTION USAGE:** OrbitalPresentationConfig.GetPrefab → OrbitalModuleView; production and Labs share the same composition. LinkNode uses its own circle sprites; no corresponding miniWeapon asset exists.
- **TARGET AUTHORITATIVE ASSET:** `Assets/_Project/prefabs/Orbital/Modules/LinkNodeVisual.prefab`
- **COMPONENT CLASS IDS:** `{"1": 4, "114": 1, "212": 3, "4": 4}`
- **SCRIPTS:** `Assets/_Project/scripts/Combat/OrbitalStation/OrbitalModuleView.cs`
- **NESTED SOURCES:** none
- **DIRECT SPRITE SOURCES:** `Assets/_Project/Resources/OrbitalStation/Authored/Circle.png`

### Mount.prefab

- **PATH:** `Assets/_Project/Resources/OrbitalStation/Authored/Mount.prefab`
- **TYPE / CATEGORY:** `.prefab` / RING
- **GUID:** `e7526e87822eb134f89574d1a62def78`
- **REFERENCED BY:**
  - `Assets/_Project/Resources/OrbitalStation/OrbitalPresentationConfig.asset`
- **DUPLICATE OF:** No byte-identical asset in Assets; no equivalent production replacement confirmed.
- **CURRENT PRODUCTION USAGE:** Production presentation through OrbitalPresentationConfig; Labs consume the same catalog.
- **TARGET AUTHORITATIVE ASSET:** `Assets/_Project/prefabs/Orbital/Rings/Mount.prefab`
- **COMPONENT CLASS IDS:** `{"1": 3, "114": 1, "210": 1, "212": 2, "4": 3}`
- **SCRIPTS:** `Assets/_Project/scripts/Combat/OrbitalStation/OrbitalMountView.cs`
- **NESTED SOURCES:** none
- **DIRECT SPRITE SOURCES:** `Assets/_Project/Resources/OrbitalStation/Authored/Circle.png`

### PistolCardIcon.png

- **PATH:** `Assets/_Project/Resources/OrbitalStation/Authored/PistolCardIcon.png`
- **TYPE / CATEGORY:** `.png` / MODULE
- **GUID:** `dff40f399f0f8634882e794a928aa39f`
- **REFERENCED BY:**
  - `Assets/_Project/Resources/OrbitalStation/Authored/PistolVisual.prefab`
  - `Assets/_Project/Scenes/MainBuild/MainMenu.unity`
- **DUPLICATE OF:** No byte-identical asset in Assets; no equivalent production replacement confirmed.
- **CURRENT PRODUCTION USAGE:** Serialized sprite/material dependency of the catalog, production prefabs and/or scenes listed above; no direct Resources.Load of this asset found.
- **TARGET AUTHORITATIVE ASSET:** `Assets/_Project/art/Orbital/PistolCardIcon.png`

### PistolVisual.prefab

- **PATH:** `Assets/_Project/Resources/OrbitalStation/Authored/PistolVisual.prefab`
- **TYPE / CATEGORY:** `.prefab` / MODULE
- **GUID:** `061397fe35d9a524f9ac8e1c58b25f4a`
- **REFERENCED BY:**
  - `Assets/_Project/Resources/OrbitalStation/OrbitalPresentationConfig.asset`
- **DUPLICATE OF:** No byte-identical asset in Assets; no equivalent production replacement confirmed.
- **CURRENT PRODUCTION USAGE:** OrbitalPresentationConfig.GetPrefab → OrbitalModuleView; production and Labs share the same composition. Uses original nested miniWeapon listed below.
- **TARGET AUTHORITATIVE ASSET:** `Assets/_Project/prefabs/Orbital/Modules/PistolVisual.prefab`
- **COMPONENT CLASS IDS:** `{"1": 2, "1001": 1, "114": 1, "198": 2, "212": 3, "4": 3}`
- **SCRIPTS:** `Assets/_Project/scripts/Combat/OrbitalStation/OrbitalModuleView.cs`
- **NESTED SOURCES:** `Assets/_Project/prefabs/miniWeapons/p_miniWeaponPistol1.prefab`
- **DIRECT SPRITE SOURCES:** `Assets/_Project/Resources/OrbitalStation/Authored/Circle.png`

### Pixel.png

- **PATH:** `Assets/_Project/Resources/OrbitalStation/Authored/Pixel.png`
- **TYPE / CATEGORY:** `.png` / FX
- **GUID:** `842791f4876552648b2118a0575f7a6d`
- **REFERENCED BY:**
  - `Assets/_Project/Resources/OrbitalStation/OrbitalPresentationConfig.asset`
  - `Assets/_Project/Scenes/MainBuild/MainMenu.unity`
- **DUPLICATE OF:** Byte-identical asset(s): `Assets/_Project/art/WorldRulePixels/GoldMote.png`, `Assets/_Project/art/WorldRulePixels/DarknessPixel.png`, `Assets/_Project/art/Sprites/UI/AnomalyFocusPixel.png`. Retained: importer/reference equivalence is not assumed.
- **CURRENT PRODUCTION USAGE:** Serialized sprite/material dependency of the catalog, production prefabs and/or scenes listed above; no direct Resources.Load of this asset found.
- **TARGET AUTHORITATIVE ASSET:** `Assets/_Project/art/Orbital/Pixel.png`

### Player_0_p_Player3.prefab

- **PATH:** `Assets/_Project/Resources/OrbitalStation/Authored/Player_0_p_Player3.prefab`
- **TYPE / CATEGORY:** `.prefab` / CHARACTER
- **GUID:** `50fd004411bc2bc40bb6a21478296cc8`
- **REFERENCED BY:**
  - `Assets/_Project/Scenes/EnemyGallery.unity`
  - `Assets/_Project/Prototype/SurfaceVisualLab/SurfaceVisualLab.unity`
  - `Assets/_Project/Resources/OrbitalStation/OrbitalPresentationConfig.asset`
  - `Assets/_Project/Scenes/Dev/Labs/BossPracticeLab.unity`
  - `Assets/_Project/Scriptable Objects/Characters/03_Vika.asset`
  - `Assets/_Project/Editor/WorldSystemsLabAuthoring.cs (hardcoded path before migration)`
  - `Assets/_Project/Editor/Tests/CharacterIdentityConfigurationTests.cs (path assertion)`
- **DUPLICATE OF:** No byte-identical asset in Assets; no equivalent production replacement confirmed.
- **CURRENT PRODUCTION USAGE:** Selected through CharacterData.productionPrefab; existing serialized Lab/gallery consumers retain the same GUID. Shared nested character Visuals and Station; not equivalent to the base character prefab.
- **TARGET AUTHORITATIVE ASSET:** `Assets/_Project/prefabs/players/Production/Player_0_p_Player3.prefab`
- **COMPONENT CLASS IDS:** `{"1": 5, "1001": 2, "114": 9, "198": 1, "199": 1, "210": 1, "212": 1, "4": 8, "50": 1, "58": 1, "70": 1, "82": 1}`
- **SCRIPTS:** `Unity URP Light2D (package GUID 073797afb82c5a1438f328866b10b3f0)`, `Assets/_Project/scripts/Combat/Player/PlayerHitSound.cs`, `Assets/_Project/scripts/Combat/Player/PlayerPickupRadius.cs`, `Assets/_Project/scripts/World/Spawning/EnemySpawner.cs`, `Assets/_Project/scripts/Bunker/Interaction/PlayerInteractor.cs`, `Assets/_Project/scripts/Combat/Player/CharacterMovement2D.cs`, `Assets/_Project/scripts/Combat/Player/PlayerHealth.cs`, `Assets/_Project/scripts/Combat/Player/PlayerCombatModifiers.cs`, `Assets/_Project/scripts/Combat/Player/PlayerWhiteFlash.cs`
- **NESTED SOURCES:** `Assets/_Project/prefabs/players/Visuals/p_Player3_Visual.prefab`, `Assets/_Project/Resources/OrbitalStation/Authored/Station.prefab`
- **DIRECT SPRITE SOURCES:** `Assets/_Project/art/Sprites/Environment/Test/Shadow1.png`

### Player_1_p_Player2 Variant.prefab

- **PATH:** `Assets/_Project/Resources/OrbitalStation/Authored/Player_1_p_Player2 Variant.prefab`
- **TYPE / CATEGORY:** `.prefab` / CHARACTER
- **GUID:** `586616dfa41374a43ac464cb7dcb2282`
- **REFERENCED BY:**
  - `Assets/_Project/Resources/OrbitalStation/OrbitalPresentationConfig.asset`
  - `Assets/_Project/Scriptable Objects/Characters/02_Di-mag.asset`
- **DUPLICATE OF:** No byte-identical asset in Assets; no equivalent production replacement confirmed.
- **CURRENT PRODUCTION USAGE:** Selected through CharacterData.productionPrefab; existing serialized Lab/gallery consumers retain the same GUID. Shared nested character Visuals and Station; not equivalent to the base character prefab.
- **TARGET AUTHORITATIVE ASSET:** `Assets/_Project/prefabs/players/Production/Player_1_p_Player2 Variant.prefab`
- **COMPONENT CLASS IDS:** `{"1": 5, "1001": 2, "114": 9, "198": 1, "199": 1, "210": 1, "212": 1, "4": 8, "50": 1, "58": 1, "70": 1, "82": 1}`
- **SCRIPTS:** `Unity URP Light2D (package GUID 073797afb82c5a1438f328866b10b3f0)`, `Assets/_Project/scripts/Combat/Player/PlayerHitSound.cs`, `Assets/_Project/scripts/Combat/Player/PlayerPickupRadius.cs`, `Assets/_Project/scripts/World/Spawning/EnemySpawner.cs`, `Assets/_Project/scripts/Bunker/Interaction/PlayerInteractor.cs`, `Assets/_Project/scripts/Combat/Player/CharacterMovement2D.cs`, `Assets/_Project/scripts/Combat/Player/PlayerHealth.cs`, `Assets/_Project/scripts/Combat/Player/PlayerCombatModifiers.cs`, `Assets/_Project/scripts/Combat/Player/PlayerWhiteFlash.cs`
- **NESTED SOURCES:** `Assets/_Project/prefabs/players/Visuals/p_Player2 Variant_Visual.prefab`, `Assets/_Project/Resources/OrbitalStation/Authored/Station.prefab`
- **DIRECT SPRITE SOURCES:** `Assets/_Project/art/Sprites/Environment/Test/Shadow1.png`

### Player_2_p_Player1 Variant.prefab

- **PATH:** `Assets/_Project/Resources/OrbitalStation/Authored/Player_2_p_Player1 Variant.prefab`
- **TYPE / CATEGORY:** `.prefab` / CHARACTER
- **GUID:** `8522b3d6a57df2648903eb77fc00ecb6`
- **REFERENCED BY:**
  - `Assets/_Project/Resources/OrbitalStation/OrbitalPresentationConfig.asset`
  - `Assets/_Project/Scriptable Objects/Characters/01_Gera.asset`
  - `Assets/_Project/Editor/Tests/CharacterIdentityConfigurationTests.cs (path assertion)`
- **DUPLICATE OF:** No byte-identical asset in Assets; no equivalent production replacement confirmed.
- **CURRENT PRODUCTION USAGE:** Selected through CharacterData.productionPrefab; existing serialized Lab/gallery consumers retain the same GUID. Shared nested character Visuals and Station; not equivalent to the base character prefab.
- **TARGET AUTHORITATIVE ASSET:** `Assets/_Project/prefabs/players/Production/Player_2_p_Player1 Variant.prefab`
- **COMPONENT CLASS IDS:** `{"1": 5, "1001": 2, "114": 9, "198": 1, "199": 1, "210": 1, "212": 1, "4": 8, "50": 1, "58": 1, "70": 1, "82": 1}`
- **SCRIPTS:** `Unity URP Light2D (package GUID 073797afb82c5a1438f328866b10b3f0)`, `Assets/_Project/scripts/Combat/Player/PlayerHitSound.cs`, `Assets/_Project/scripts/Combat/Player/PlayerPickupRadius.cs`, `Assets/_Project/scripts/World/Spawning/EnemySpawner.cs`, `Assets/_Project/scripts/Bunker/Interaction/PlayerInteractor.cs`, `Assets/_Project/scripts/Combat/Player/CharacterMovement2D.cs`, `Assets/_Project/scripts/Combat/Player/PlayerHealth.cs`, `Assets/_Project/scripts/Combat/Player/PlayerCombatModifiers.cs`, `Assets/_Project/scripts/Combat/Player/PlayerWhiteFlash.cs`
- **NESTED SOURCES:** `Assets/_Project/prefabs/players/Visuals/p_Player1 Variant_Visual.prefab`, `Assets/_Project/Resources/OrbitalStation/Authored/Station.prefab`
- **DIRECT SPRITE SOURCES:** `Assets/_Project/art/Sprites/Environment/Test/Shadow1.png`

### Ring.mat

- **PATH:** `Assets/_Project/Resources/OrbitalStation/Authored/Ring.mat`
- **TYPE / CATEGORY:** `.mat` / RING
- **GUID:** `fa406feb433e41078e768859e07982f2`
- **REFERENCED BY:**
  - `Assets/_Project/Resources/OrbitalStation/Authored/Ring.prefab`
- **DUPLICATE OF:** No byte-identical asset in Assets; no equivalent production replacement confirmed.
- **CURRENT PRODUCTION USAGE:** Serialized sprite/material dependency of the catalog, production prefabs and/or scenes listed above; no direct Resources.Load of this asset found.
- **TARGET AUTHORITATIVE ASSET:** `Assets/_Project/Materials/Orbital/Ring.mat`

### Ring.prefab

- **PATH:** `Assets/_Project/Resources/OrbitalStation/Authored/Ring.prefab`
- **TYPE / CATEGORY:** `.prefab` / RING
- **GUID:** `d6ae1d52a5c1f604f935151e75ce217d`
- **REFERENCED BY:**
  - `Assets/_Project/Resources/OrbitalStation/OrbitalPresentationConfig.asset`
- **DUPLICATE OF:** No byte-identical asset in Assets; no equivalent production replacement confirmed.
- **CURRENT PRODUCTION USAGE:** Production presentation through OrbitalPresentationConfig; Labs consume the same catalog.
- **TARGET AUTHORITATIVE ASSET:** `Assets/_Project/prefabs/Orbital/Rings/Ring.prefab`
- **COMPONENT CLASS IDS:** `{"1": 4, "114": 1, "120": 2, "4": 4}`
- **SCRIPTS:** `Assets/_Project/scripts/Combat/OrbitalStation/OrbitalRingView.cs`
- **NESTED SOURCES:** none
- **DIRECT SPRITE SOURCES:** none; inherited nested sprites checked in Unity

### Station.prefab

- **PATH:** `Assets/_Project/Resources/OrbitalStation/Authored/Station.prefab`
- **TYPE / CATEGORY:** `.prefab` / CORE
- **GUID:** `ff2e17705544a184c937303c9ea8dcc4`
- **REFERENCED BY:**
  - `Assets/_Project/Resources/OrbitalStation/OrbitalPresentationConfig.asset`
  - `Assets/_Project/Resources/OrbitalStation/Authored/Player_0_p_Player3.prefab`
  - `Assets/_Project/Resources/OrbitalStation/Authored/Player_1_p_Player2 Variant.prefab`
  - `Assets/_Project/Resources/OrbitalStation/Authored/Player_2_p_Player1 Variant.prefab`
- **DUPLICATE OF:** No byte-identical asset in Assets; no equivalent production replacement confirmed.
- **CURRENT PRODUCTION USAGE:** Production Station and character nested Station; includes original miniWeaponCore1 plus orbital controllers and presentation.
- **TARGET AUTHORITATIVE ASSET:** `Assets/_Project/prefabs/Orbital/Core/Station.prefab`
- **COMPONENT CLASS IDS:** `{"1": 6, "1001": 1, "114": 7, "198": 1, "199": 1, "212": 2, "4": 7}`
- **SCRIPTS:** `Assets/_Project/scripts/Combat/OrbitalStation/OrbitalStationRuntime.cs`, `Assets/_Project/scripts/Combat/OrbitalStation/OrbitalStationView.cs`, `Assets/_Project/scripts/Combat/OrbitalStation/OrbitalInteractionController.cs`, `Assets/_Project/scripts/Combat/OrbitalStation/OrbitalInteractionPresentation.cs`, `Assets/_Project/scripts/Combat/OrbitalStation/OrbitalRelocationController.cs`, `Assets/_Project/scripts/Combat/OrbitalStation/OrbitalWorldTelekinesisController.cs`, `Assets/_Project/scripts/Combat/OrbitalStation/OrbitalRewardFlowController.cs`
- **NESTED SOURCES:** `Assets/_Project/prefabs/miniWeapons/p_miniWeaponCore1.prefab`
- **DIRECT SPRITE SOURCES:** `Assets/_Project/Resources/OrbitalStation/Authored/Circle.png`

### StationEnergyLine.prefab

- **PATH:** `Assets/_Project/Resources/OrbitalStation/Authored/StationEnergyLine.prefab`
- **TYPE / CATEGORY:** `.prefab` / FX
- **GUID:** `db8249cd301190e488b9d6ff70d5a1e7`
- **REFERENCED BY:**
  - `Assets/_Project/Resources/OrbitalStation/OrbitalPresentationConfig.asset`
  - `Assets/_Project/Editor/Subject42CoreAuthoring.cs (output path)`
- **DUPLICATE OF:** No byte-identical asset in Assets; no equivalent production replacement confirmed.
- **CURRENT PRODUCTION USAGE:** Production presentation through OrbitalPresentationConfig; Labs consume the same catalog.
- **TARGET AUTHORITATIVE ASSET:** `Assets/_Project/prefabs/Orbital/FX/StationEnergyLine.prefab`
- **COMPONENT CLASS IDS:** `{"1": 1, "120": 1, "4": 1}`
- **SCRIPTS:** 
- **NESTED SOURCES:** none
- **DIRECT SPRITE SOURCES:** none; inherited nested sprites checked in Unity

### Visual.mat

- **PATH:** `Assets/_Project/Resources/OrbitalStation/Authored/Visual.mat`
- **TYPE / CATEGORY:** `.mat` / FX
- **GUID:** `1800b25f5e1f7754b8d5b7d7595c0afd`
- **REFERENCED BY:**
  - `Assets/_Project/Resources/OrbitalStation/OrbitalPresentationConfig.asset`
  - `Assets/_Project/Resources/OrbitalStation/Authored/CustomOrbitDrawing.prefab`
  - `Assets/_Project/Resources/OrbitalStation/Authored/Station.prefab`
  - `Assets/_Project/Resources/OrbitalStation/Authored/StationEnergyLine.prefab`
  - `Assets/_Project/Scenes/Dev/Labs/CustomOrbitLab.unity`
- **DUPLICATE OF:** No byte-identical asset in Assets; no equivalent production replacement confirmed.
- **CURRENT PRODUCTION USAGE:** Serialized sprite/material dependency of the catalog, production prefabs and/or scenes listed above; no direct Resources.Load of this asset found.
- **TARGET AUTHORITATIVE ASSET:** `Assets/_Project/Materials/Orbital/Visual.mat`

## Reference changes

- `WorldSystemsLabAuthoring`: loads `03_Vika.asset` and reads `CharacterData.ProductionPrefab`, preserving its former Vika selection.
- `Subject42CoreAuthoring` and `CustomOrbitLabAuthoring`: output paths updated to authoritative Orbital folders.
- `CharacterIdentityConfigurationTests`: Gera/Vika expected locations updated.
- `OrbitalPresentationConfig.asset`: removed obsolete serialized `PlayerVariants` block (no corresponding C# field exists). Character identity remains solely in CharacterData.
- No GUID replacement was necessary. Config, CharacterData, nested prefab, production scene and Lab serialized references followed the moved assets unchanged.

## Remaining in Resources

Only `Assets/_Project/Resources/OrbitalStation/OrbitalPresentationConfig.asset` and its `.meta` remain. `OrbitalPresentationConfig.Active` / `TryGetRequired` load this data asset using `Resources.Load`. Its prefab/material/sprite dependencies now resolve outside Resources. No direct load of an Authored prefab or any resource-path fragment referring to it was found in project C# code. The user explicitly chose to retain this catalog in Resources temporarily and defer replacing Resources-based loading to a separate pass.

## Authoritative prefab locations

| Kind | Location |
|---|---|
| Characters | `Assets/_Project/prefabs/players/Production/` via CharacterData.productionPrefab; shared art in `players/Visuals/` |
| MiniWeapons, including Core and projectile | `Assets/_Project/prefabs/miniWeapons/` (unchanged) |
| Orbital module compositions | `Assets/_Project/prefabs/Orbital/Modules/` (nested original miniWeapons) |
| Rings, Mount, CustomOrbitDrawing | `Assets/_Project/prefabs/Orbital/Rings/` |
| Station/Core presentation | `Assets/_Project/prefabs/Orbital/Core/` |
| Orbital FX | `Assets/_Project/prefabs/Orbital/FX/`; existing shared FX remain in `prefabs/fx/` |
| Materials / sprites | `Assets/_Project/Materials/Orbital/` / `Assets/_Project/art/Orbital/` |

## Deleted duplicates

None. All 19 assets were preserved by move; only the empty Authored folder and its folder metadata were deleted.

## Verification

See final verification results below. No full regression suite was run.


### Final results

- **Compile: PASS** — Unity 6000.3.13f1 compiled editor changes and recompiled after removing the temporary runner (`Tundra build success`). No compile errors; existing Shapes2D obsolete-API warnings remain.
- **Focused tests: 18/18 PASS**, 0 failed, 0 skipped. Includes three CharacterOwnsItsProductionPath cases, canonical character visuals, production IDs/facing, five pure module prefabs, four source-art preservation cases, Core/projectile ownership, Station/Ring composition and production-vs-legacy character composition.
- **Migrated scope: missing GUIDs = 0; missing scripts = 0; broken object references = 0** across the moved assets, CharacterData, config and 178 dependencies, checked through Unity AssetDatabase and SerializedObject.
- **10 scenes scanned**: all six Dev/Labs scenes, EnemyGallery, SurfaceVisualLab, MainMenu and MVP, plus their dependencies. All six Dev/Labs and both gallery/visual Lab scenes had no broken references or missing GameObject scripts.
- **Broader dependency scan: four pre-existing missing GUIDs**, with two corresponding unresolved scene object references. No new broken references. Every affected file is unchanged from HEAD, and each missing GUID was verified in HEAD. These are outside the bounded migration and were not repaired:

| File | Missing GUID | Meaning |
|---|---|---|
| `Assets/_Project/Scenes/MainBuild/MVP.unity` | `45477ec4cb3beb745aa2cda24aaf5e33` | MusicPlayer tracks[0] audio reference |
| `Assets/_Project/Scenes/MainBuild/MainMenu.unity` | `c304c3a61e99d8e4095a4f8bfff11cca` | FullScreenImage sprite reference |
| `Assets/_Project/Resources/AnomalyFocus.mat` | `da692e001514ec24dbc4cca1949ff7e8` | URP editor MaterialVersion metadata script; pre-existing missing script reference outside migrated scope |
| `Assets/_Project/art/test/TestAnimations1/char2Animations/char2_Clip.anim` | `f9df7fa9cd9bda24d9bb4fc1ea8c98d6` | Animation sprite keyframe references |

- All 19 moved assets have their original SHA-256 content hash. Their `.meta` contents match the original git versions (line-ending normalized), and each move separately checked exact `.meta` hashes inside Unity before/after the operation.
- Original miniWeapons, runtime scripts, CharacterData files and all scenes remain unchanged. Only four editor/test source files and the obsolete catalog block changed, in addition to moves and new folder metadata.
- No project C# consumer still addresses `OrbitalStation/Authored`. Temporary runner removed through AssetDatabase.DeleteAsset and recompiled; no migration request or runtime mapping table remains.
- `git diff --check`: PASS. No full regression suite was run.

Local verification evidence is in `Artifacts/GeneratedQA/OrbitalOwnership/`: `before.json`, `migration.result` (move results and first discovery of the legacy audio GUID), `validation.result` (complete follow-up scan), `hash-verification.json`, `results.xml`, `progress.txt`, `cleanup.result`, and `final-verification.txt`. Generated QA artifacts are git-ignored; this report records the durable results.
