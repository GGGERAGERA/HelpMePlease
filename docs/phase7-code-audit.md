# Phase 7 code cleanup evidence

Scope: dead character-selection bundle, obsolete migration paths/helpers, completed serialized-name migrations, and unused localization entries. No gameplay behavior redesign.

## Removed

- `CharacterSelectionUI`, `CharacterStationEmbeddedView`, `CharacterCardView`, `CharacterClickHandler`, with their script metadata. First two had zero serialized GUID consumers across Assets. Card/click components existed only in the unused `UI/about/AboutPanel.prefab`; the asset pass audited its zero incoming GUID/path consumers and removed it together. C# consumers were confined to the bundle and the F1 debug menu. No tests, reflection names, or other dynamic type consumers were found. Production selection uses `BunkerSelectionWindow`/`BunkerSelectionSourceHub`.
- F1 `debugCharacters`, `AddCharacterDebugSelection`, `DebugRefreshCharacterUi`, `DebugSelectCharacter`, and their stale controls. Current character/station diagnostics remain.
- `WorldLootChest.HandleRewardClaimed`: no code call, callback assignment, UnityEvent `m_MethodName`, or reflection-name consumer; production uses `HandleUpgradeRewardAccepted`.
- `VisualTuningPresetStorage.LegacyAssetPath` and old-path `MoveAsset` branch. Old `Assets/_Project/VisualTuningSavedValues.asset` is absent; current preset lives in `Data/World/Presentation`. Normal save/create and schema normalization remain.
- `BunkerNavigationAuthoring` plus metadata (`4df1e35d70b2ee440aa63dac0495455c`): no serialized GUID or C# consumers. Its manual menu action requires already-absent `art/BunkerNavigation/FloorLight.mat` and cannot author the current composition. `BunkerNavigationView` remains because OLD/SIMPLE BunkerSimple scenes serialize it.
- Completed `FormerlySerializedAs` aliases: `stationName`, `nextUnlockText`, `investButton`, `investButtonText`, `slowField`, `interactionsRoot`. All Assets scene/prefab/config YAML searches returned zero old-name fields and property overrides. Current fields and metadata remain.

Deleted bundle fields (30):

- CharacterSelectionUI: cards, characterInfoRoot, emptyStateText, portraitImage, characterNameText, combatTypeText, featureText, statsText, descriptionText, selectButton, backButton, stationView, panelManager.
- CharacterStationEmbeddedView: panelRect, titleText, levelText, progressRoot, progressSegments, progressFills, goldProgressText, availableGoldText, upgradeButton, upgradeButtonText.
- CharacterCardView: character, backgroundImage, characterImage, characterSprite, nameText, button.
- CharacterClickHandler: selectionUI.

## Preserved compatibility

- `CharacterMovement2D` keeps `FormerlySerializedAs("speed")`: production `p_Capsule1.prefab` still overrides `speed` on Gera component fileID `7398020079416971295`, GUID `8522b3d6a57df2648903eb77fc00ecb6`; that component resolves to the movement script GUID `879dc705f86e1f84c91ba97bfffc9dfa`.
- All 16 unread serialized-field declarations outside the deleted bundle still occur in asset data, so they do not meet the requested zero-serialized-usage condition: XP pickupSound/pickupVolume; HUD healthText/experienceText/killsText; LevelAnomalyView.cardRect; TacticalMapHUD NormalSiteFill/NormalSiteBorder/SpecialSiteFill/SpecialSiteBorder/BossFill/BossBorder/anomalySize; UICrosshairFollowMouse.cursorCanvas; WorldLootChest.useNormalUpgradePool; WorldRuleController.logGoldenEnemyAssignments.
- BunkerRoomAccess.legacyStationsRoot and its runtime cache fallback remain: current-name serialization exists in the door prefab and old lab overrides.
- Live WeaponData.weaponPrefab links stay: PlayerLoadoutFactory instantiates them, and WeaponUpgradeCapability inspects their combat components. Legacy-labelled enum numeric IDs and persistent-save schema migration remain.
- Public menu authoring utilities and Corridor migration test helpers remain where code/test consumers or manual rebuild workflows exist. No dead commented implementation or completed TODO was found outside the deleted bundle.

## Localization

54 keys had zero literal consumers in Assets (C#, scenes, prefabs, configs, UI sources) after coordinated bundle deletion. The follow-up included Packages. The computed-key audit found `mechanic.challenge.` + state; all four Inactive/Active/Completed/Failed entries remain. Existing serialized data-driven keys remain. LocalizationTable now contains 565 unique keys; table metadata is unchanged.

Removed keys:

- `hud.exitPacing.assault`
- `hud.exitPacing.recovery`
- `hud.exitPacing.open`
- `settings.gameplay`
- `settings.automaticFire`
- `language.russian`
- `language.english`
- `menu.about`
- `result.victory`
- `result.defeat`
- `stats.survived`
- `stats.totalGold`
- `stats.runGold`
- `pause.esc`
- `hud.objective.explore`
- `hud.specialOpportunity`
- `hud.movement`
- `hud.target.exit`
- `hud.target.special`
- `hud.target.event`
- `hud.target.container`
- `hud.target.anomaly`
- `bunker.football_enter`
- `bunker.football_claimed`
- `bunker.football_reward`
- `bunker.upgrade.1.name`
- `bunker.upgrade.1.description`
- `bunker.upgrade.1.category`
- `character.details.feature`
- `character.details.description`
- `character.details.stats`
- `character.choose`
- `character.station.title`
- `character.station.level`
- `character.station.gold`
- `character.station.max`
- `character.station.investing`
- `character.station.upgrade`
- `hud.threat`
- `hud.boss`
- `hud.slowFieldEnergy`
- `site.reward`
- `slot.open`
- `football.reset`
- `result.analysis`
- `about.protocol`
- `about.experiment`
- `map.chest`
- `map.event`
- `map.boss`
- `map.special`
- `map.exit`
- `map.player`
- `map.anomaly`

## Verification

- `git diff --check` for owned code/localization: no whitespace errors.
- Remaining production-source/binary-serialization search: zero references to the deleted character types, F1 helper names, HandleRewardClaimed, or LegacyAssetPath.
- Localization post-write check: 565 unique keys; zero remaining literal consumers of removed keys in Assets/Packages; all four computed challenge-state keys present.
- No Unity invocation or test/smoke runner execution performed by this code-cleanup subtask; root coordinates compile and selected runtime acceptance.

## Minimal file moves

- Shared HoldInvestmentInput moved from Selection/Characters to scripts/UI/Common. Existing GUID `5ded1b49aad6dc949bc70f6973623f18` is unchanged; all three production UI prefab links remain. Empty Characters directory and its folder metadata removed.
- BunkerNavigationView moved from production Bunker/Interaction to Dev/Debug/BunkerSimple and guarded with UNITY_EDITOR || DEVELOPMENT_BUILD. Existing GUID `2ac6a41afb803564fa703574797824ba` is unchanged; its only serialized consumers are BunkerSimple OLD/SIMPLE scenes. No production code/type/GUID consumers were found.

## Prefab inert-reference cleanup

Actual target headers and recursively reconstructed nested prefab IDs were checked before removing 95 property override blocks and 14 removed-component rows targeting absent objects. This includes Bunker room/door/screen/capsule variants, the old enemy variant, mine environment, HUD, and settings panels. Five PF_MinigameSidePanel component-list rows pointed to nonexistent local component headers and were removed (IDs 4067050299282379298, 3501194172664000549, 9172995072856357948, 7304693143548281851, 7423211453559892038). No valid target override or live property was changed. Prefab metadata is unchanged. Detailed target IDs/counts are in Artifacts/GeneratedQA/Phase7/prefab-inert-reference-cleanup.json.

Robot1 FBX sourcePrefab fileID 100100000 remains: imported-model object IDs need native Unity evidence, so a direct text-header miss cannot justify deletion. The still-used F1 Emi projectile was relocated to Dev by the asset pass with GUID/bytes preserved; its missing sprite/controller remain an explicit Dev limitation. No arbitrary null repair was made.
