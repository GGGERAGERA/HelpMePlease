# Phase 7 asset audit

Audit scope: known legacy/migration candidates, all Assets GUID consumers, enabled production scenes and Dev/editor consumers. No asset was deleted because of its name or visual similarity. Retained GUIDs and import settings remain unchanged; minimal category moves are recorded below.

## Evidence and removal rules

`Artifacts/GeneratedQA/Phase7/asset-index.json` records GUIDs, incoming references and the production closure. The scan reads Unity serialized scenes, prefabs, configs, materials, animations/controllers and metadata across all Assets; root scenes are StartScreen, MainMenu and MVP from EditorBuildSettings. Candidate GUID searches also covered ProjectSettings and Packages. Source searches covered runtime, Dev, tests and authoring tools for paths, types, Resources/Addressables/AssetBundle loads and shader names. Project runtime has no Resources.Load/Addressables/AssetBundle loading of these assets; the Gravity fallback uses Unity's built-in Quad mesh.

Before removal, every asset below had **zero incoming GUID references** anywhere in Assets and no code path/dynamic-name consumer. All were outside the enabled production dependency closure. Files and paired metadata were removed using exact absolute, workspace-contained paths. Shared outgoing dependencies were retained.

## Deleted assets

Paths below are relative to repository root. Each listed asset's `.meta` was also removed.

| Asset | GUID | Evidence / bundle |
| --- | --- | --- |
| `Assets/_Project/prefabs/Pickups/crystal.prefab` | `af8c07181226c72428723251e92170da` | Unreferenced pickup; m_Script points to absent b1ebd0ae0fb625a4e98b9c808a775eab. Production crystal variants and their art remain. |
| `Assets/_Project/prefabs/Environment/WorldAnomalies/GravityZone.prefab` | `d17a42a1000000000000000000000005` | Old standalone zone with no authored VisualRoot; production consumes GravityZone_World, not this prefab. |
| `Assets/_Project/prefabs/UI/about/AboutPanel.prefab` | `c4796c1c151359c49a0c2faab870ed07` | Only serialized CharacterCardView/CharacterClickHandler consumers; no prefab/scene/authoring consumer. Matching obsolete code removed in coordinated code audit. |
| `Assets/_Project/Scenes/MainBuild/MainMenu_old.unity` | `15424f36f13d77044aeac6458f9dd925` | Unreferenced scene copy, absent from build settings and editor/code scene paths; current MainMenu remains. |
| `Assets/_Project/Shaders/UI_CardScanOverlay.shader` | `7c2271909ec32d54aacab272fcb51d00` | No material/config GUID use and no Shader.Find/path/name use in runtime or editor code. |
| `Assets/_Project/Shaders/UI_EmissivePulse.shader` | `8f400316b3cd430479f66bf7fff2fb14` | No material/config GUID use and no Shader.Find/path/name use in runtime or editor code. |
| `Assets/_Project/Shaders/UI_NeuralBackground.shader` | `4c0e23524b485384f99a2e110538871b` | No material/config GUID use and no Shader.Find/path/name use in runtime or editor code. |
| `Assets/_Project/Shaders/UI_VignetteOverlay.shader` | `99289feab45a36244abd4d1028df2111` | No material/config GUID use and no Shader.Find/path/name use in runtime or editor code. |
| `Assets/_Project/Shaders/Debug_EnvironmentReadability.shader` | `726c5cb8469e43648c8d604eff6dc401` | No material/config GUID use and no Shader.Find/path/name use in runtime or editor code. |
| `Assets/_Project/art/Shaders/BunkerFloorNavigation.shader` | `fb68be5bfb9b73448ad956d5d395ec29` | No material/config GUID use and no Shader.Find/path/name use in runtime or editor code. |
| `Assets/_Project/Materials/cfxr flare add 1.mat` | `2a1f30c43cdac0d409feb9fe37f7a85c` | Unreferenced legacy FX material; no renderer/config or code/path consumer. Shared FX textures and shaders remain. |
| `Assets/_Project/art/Sprites/weaponFx/Materials/cfxr flare add 1.mat` | `23dd8fc3b66210544a1ffd3335d684dc` | Unreferenced legacy FX material; no renderer/config or code/path consumer. Shared FX textures and shaders remain. |
| `Assets/_Project/art/Sprites/weaponFx/Materials/fx1.mat` | `c06f6fc85257a4e4b9950a9ecc665b9a` | Unreferenced legacy FX material; no renderer/config or code/path consumer. Shared FX textures and shaders remain. |
| `Assets/_Project/art/Sprites/weaponFx/Materials/fxtrail2_02.mat` | `abff8d6882f6bbc4aab783338756a428` | Unreferenced legacy FX material; no renderer/config or code/path consumer. Shared FX textures and shaders remain. |
| `Assets/_Project/art/Sprites/weaponFx/Materials/fxtrail5.mat` | `e8ccd3e31406d9c468949cca3b76d810` | Unreferenced legacy FX material; no renderer/config or code/path consumer. Shared FX textures and shaders remain. |
| `Assets/_Project/prefabs/fx/Sprite-Lit-Default.mat` | `7b9e7b917421f4742949c8c1709d727c` | Unreferenced legacy FX material; no renderer/config or code/path consumer. Shared FX textures and shaders remain. |

## Coordinated character code bundle

AboutPanel was the only serialized owner of CharacterCardView (`4f807e296a882ed419dc5b9e05a0a1ec`) and CharacterClickHandler (`efbf73e14ba5d3f4390859f0d3068d50`). CharacterSelectionUI (`4d612544436664d4c9efcc5fc6baf227`) and CharacterStationEmbeddedView (`6ec34735bf784c32bf08a88245d89f66`) had no serialized users. Remaining source consumers were confined to that bundle and obsolete F1 refresh helpers; those four scripts/metas and debug helpers were removed by the code audit. HoldInvestmentInput remains live in StationWindow, BunkerSelectionWindow and DeathResultWindow; its script/meta moved intact to `scripts/UI/Common/`, preserving GUID `5ded1b49aad6dc949bc70f6973623f18`, and the now-empty Characters source folder was removed.

## Empty folders / metadata

Removed only now-empty folders with no GUID consumers: `Assets/_Project/prefabs/UI/about` (`cdc22759844f24d43985ab9ef6cae4d3`), `Assets/Resources` (`b1d8dd30917a89d4e896c481a696601e`), and `Assets/Epic Toon FX/Upgrade` (`4d72081c398a58243be8a7305f3618e7`). The nonempty weaponFx/Materials and art/Shaders folders remain. No orphan `.meta` was found before or after. Untracked empty `.agents`/`.git` directories were left alone.

## Kept candidates and duplicates

- Pistol/Laser weapon prefabs and WeaponData are live: the production catalog/recipes reference both data assets, PlayerLoadoutFactory and WeaponUpgradeCapability read weaponPrefab, and F1/Test also references the prefabs. Removing this path would require gameplay migration, outside this cleanup.
- All miniWeapons remain authoritative nested sources for ORBITAL module compositions and Station. They are not duplicate legacy weapons.
- GravityZone_World, its material/shader and FootballGravityZone remain live. GravityZone's built-in visual fallback is still reachable for FootballGravityZone and is retained; no oldGravity asset/path remains.
- BunkerSimple/OLD/MainMenu is still explicitly opened by SurfaceMapProductionAuthoring. BunkerSimple/SIMPLE, BunkerRoomTest and other Labs have their own authoring/menu consumers; retained. Subject42UIPrototype has an isolated manual menu and README, so abandonment is uncertain and it remains.
- SHA-256 equality scan of project prefabs/materials/PNG assets found only one group: Football/Chalk.png, Orbital/Pixel.png, WorldRulePixels/DarknessPixel.png, WorldRulePixels/GoldMote.png and Sprites/UI/AnomalyFocusPixel.png. All five have live production references and distinct importer/GUID roles; no merge. No byte-identical project prefab or material pair was found.
- Broad test/source art and third-party libraries were not culled. Remaining zero-incoming test materials do not justify deleting their artist/importer source bundles without a dedicated audit.

## Dev ownership moves

- `art/EnemyGalleryVisuals.asset` moved with unchanged asset/meta bytes to `Dev/Labs/F1/EnemyGallery/EnemyGalleryVisuals.asset`. GUID `955d3c43b9542a34b8f709f2696ff5cf` remains unchanged. Its only serialized consumer is Dev `EnemyGallery.unity`; the authoring constant and its local README now use the new path. It is absent from enabled production dependencies.
- `Scenes/Test.unity` moved with unchanged scene/meta bytes to `Dev/Labs/F1/TestScene/Test.unity`. GUID `de51c58905ddf3045a8415039523f811` remains unchanged. No GUID, code path, scene-name loader or Build Settings consumer was found. It is an isolated old camera/tilemap/Bunker test scene. The existing distinct `Dev/Labs/F1/Test.unity` remains untouched.
- `prefabs/projectiles/p_EmiProjectile1.prefab` moved with unchanged prefab/meta bytes to `Dev/Labs/F1/Projectiles/p_EmiProjectile1.prefab`. GUID `8b0f54bba72d8e547b91c306a06062df` remains unchanged. Its only consumer is the serialized prefab instance in `Dev/Labs/F1/Test.unity`; no code/path consumers exist. SHA-256 checks pass; evidence is `Artifacts/GeneratedQA/Phase7/moved-dev-projectile.json`. Existing missing sprite `88e220b6679599c438f7e4fdda158a80` and controller `4a865c9349690924f9d0d4bd08bf0983` remain preserved as Dev limitations.
- SHA-256 comparisons before/after both moves pass for asset and meta bytes; evidence is `Artifacts/GeneratedQA/Phase7/moved-dev-assets.json`. New folder metadata uses fresh GUIDs.
- Existing gallery sprite texture references `1263aca9ee0523e44aba9eefeb78e989` and `ec11f879abb949444a434f83a61510dc` remain unresolved. The shader `650dd9526735d5b46b79224bc6e94025` resolves in URP PackageCache and is not broken. No exact replacement for the two textures was established, so their source data was preserved.
- Existing Test scene references `20e0c8d613ea07e458fb15d0342c62c7`, `52908db27a6a5334ca897cc136d1b14f`, `576f504eed187b84a8cdc7db3c8ff20e`, `67679d9a4e0100542a2c9a0484376338`, `b40f0911b5f956e4a80020a6ebc706b0`, and `e437d775072c219459c5d55a268ee9bb` remain unresolved in Assets/Packages/PackageCache. Built-in GUIDs and all six component GUIDs (one project, five package) resolve correctly. No arbitrary nulling/deletion was performed. The three moved assets remain Dev limitations, excluded from production asset integrity scope.

## Result / limits

Final read-only scan of **482 project scenes/prefabs / 2626 MonoBehaviour script fields** resolved project, PackageCache, DLL/package and built-in references. Production canonical roots/closure and Dev assets have **zero missing scripts** and there are **zero null m_Script fields**. Broad artist/demo source scope still contains **11 missing-script components in 11 files**, all under `art/test/CainosPackage/Pixel Art Top Down - Basic`: SC All Props, SC Demo, PF Player, Altar 01, Rune Pillar X2/X3 and five Stairs prefabs. Their five absent script GUIDs and exact paths/lines/incoming demo consumers are listed in `Artifacts/GeneratedQA/Phase7/missing-script-inventory.txt`. None enters production; retained demo scenes and SCENE.prefab consume the prop prefabs. This source bundle was preserved under the no-broad-art-culling constraint, so a repository-wide "zero missing scripts" claim would be incorrect.

- Asset pass removed **16 asset files + 16 metadata files**, and **3 empty folder metadata files** (35 tracked files). Coordinated character removal adds 4 script files + 4 metadata files.
- Full Assets inventory: 6906 tracked metadata files at HEAD. Initial combined-removal snapshot had 6900 on-disk GUID entries; the fresh scan after all three Dev moves and concurrent edits has 6904 entries, including untracked/import-generated metadata. Enabled scene closure remains **1092 nodes**; no removed GUID is referenced by a retained asset.
- Fresh complete GUID/consumer rescan: zero orphan meta and zero retained incoming references to deleted GUIDs. No Unity run was performed by the asset audit; Editor/player compile, integrity tests and requested smokes are owned by the main Phase 7 verification.
- Historical authoring notes still mention BunkerFloorNavigation. BunkerNavigationAuthoring depended on an already-absent FloorLight material and had no other code or serialized consumers; the coordinated code audit removed this unusable helper and meta (`4df1e35d70b2ee440aa63dac0495455c`). Main docs are updated separately.
