#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public enum Subject42ValidationSeverity
{
    Info,
    Warning,
    Error
}

public sealed class Subject42ValidationIssue
{
    public Subject42ValidationSeverity Severity { get; }
    public string Code { get; }
    public string Message { get; }
    public UnityEngine.Object Context { get; }

    public Subject42ValidationIssue(
        Subject42ValidationSeverity severity,
        string code,
        string message,
        UnityEngine.Object context = null)
    {
        Severity = severity;
        Code = code;
        Message = message;
        Context = context;
    }

    public override string ToString()
    {
        return $"[{Severity}] {Code}: {Message}";
    }
}

public sealed class Subject42ValidationReport
{
    private readonly List<Subject42ValidationIssue> issues = new();

    public IReadOnlyList<Subject42ValidationIssue> Issues => issues;
    public int ErrorCount { get; private set; }
    public int WarningCount { get; private set; }
    public int InfoCount { get; private set; }

    public void Add(
        Subject42ValidationSeverity severity,
        string code,
        string message,
        UnityEngine.Object context = null)
    {
        issues.Add(new Subject42ValidationIssue(
            severity,
            code,
            message,
            context));

        switch (severity)
        {
            case Subject42ValidationSeverity.Error:
                ErrorCount++;
                break;
            case Subject42ValidationSeverity.Warning:
                WarningCount++;
                break;
            default:
                InfoCount++;
                break;
        }
    }

    public string FormatErrors()
    {
        StringBuilder builder = new();

        for (int i = 0; i < issues.Count; i++)
        {
            Subject42ValidationIssue issue = issues[i];
            if (issue.Severity == Subject42ValidationSeverity.Error)
                builder.AppendLine(issue.ToString());
        }

        return builder.ToString();
    }
}

public static class Subject42ProjectValidator
{
    private const string ProjectRoot = "Assets/_Project";
    private const string MainMenuScene =
        "Assets/_Project/Scenes/MainMenu.unity";
    private const string GameplayScene =
        "Assets/_Project/Scenes/MVP.unity";

    private static readonly Regex MissingScriptRegex = new(
        @"m_Script:\s*\{fileID:\s*0(?:\s*,|\s*\})",
        RegexOptions.Compiled);
    private static readonly Regex ScriptGuidRegex = new(
        @"m_Script:\s*\{fileID:\s*11500000,\s*guid:\s*([0-9a-fA-F]{32})",
        RegexOptions.Compiled);
    private static readonly Regex AssetGuidRegex = new(
        @"guid:\s*([0-9a-fA-F]{32})",
        RegexOptions.Compiled);

    private static readonly HashSet<string> SerializedExtensions = new(
        StringComparer.OrdinalIgnoreCase)
    {
        ".unity",
        ".prefab",
        ".asset",
        ".mat",
        ".anim",
        ".controller",
        ".overrideController",
        ".playable"
    };

    [MenuItem("Tools/Subject42/Validate Project")]
    public static void ValidateProjectFromMenu()
    {
        Subject42ValidationReport report = ValidateProject();
        LogReport(report);
    }

    public static void ValidateFromBatchMode()
    {
        Subject42ValidationReport report = ValidateProject();
        LogReport(report);

        if (report.ErrorCount > 0)
        {
            throw new BuildFailedException(
                $"Subject42 validation failed with " +
                $"{report.ErrorCount} error(s).\n" +
                report.FormatErrors());
        }
    }

    public static Subject42ValidationReport ValidateProject()
    {
        Subject42ValidationReport report = new();
        ValidateBuildScenes(report);
        ValidateSerializedReferences(report);
        ValidateMainMenuScene(report);
        ValidateGameplayScene(report);
        ValidateDataAssets(report);
        ValidateProductionDependencies(report);

        report.Add(
            Subject42ValidationSeverity.Info,
            "VALIDATION_COMPLETE",
            "Read-only validation completed; no assets were modified.");
        return report;
    }

    private static void LogReport(Subject42ValidationReport report)
    {
        for (int i = 0; i < report.Issues.Count; i++)
        {
            Subject42ValidationIssue issue = report.Issues[i];
            string message = $"[Subject42 Validator] {issue}";

            switch (issue.Severity)
            {
                case Subject42ValidationSeverity.Error:
                    Debug.LogError(message, issue.Context);
                    break;
                case Subject42ValidationSeverity.Warning:
                    Debug.LogWarning(message, issue.Context);
                    break;
                default:
                    Debug.Log(message, issue.Context);
                    break;
            }
        }

        Debug.Log(
            $"[Subject42 Validator] Summary: " +
            $"errors={report.ErrorCount}, " +
            $"warnings={report.WarningCount}, " +
            $"info={report.InfoCount}.");
    }

    private static void ValidateBuildScenes(Subject42ValidationReport report)
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        ValidateRequiredBuildScene(scenes, MainMenuScene, 0, report);
        ValidateRequiredBuildScene(scenes, GameplayScene, 1, report);

        for (int i = 0; i < scenes.Length; i++)
        {
            EditorBuildSettingsScene scene = scenes[i];
            if (!scene.enabled)
                continue;

            if (IsTestOrLegacyPath(scene.path))
            {
                report.Add(
                    Subject42ValidationSeverity.Error,
                    "TEST_SCENE_IN_BUILD",
                    $"Test/legacy scene is enabled in Build Settings: " +
                    $"'{scene.path}'.");
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scene.path) == null)
            {
                report.Add(
                    Subject42ValidationSeverity.Error,
                    "BUILD_SCENE_MISSING",
                    $"Enabled build scene does not exist: '{scene.path}'.");
            }
        }
    }

    private static void ValidateRequiredBuildScene(
        EditorBuildSettingsScene[] scenes,
        string requiredPath,
        int expectedEnabledIndex,
        Subject42ValidationReport report)
    {
        int enabledIndex = 0;

        for (int i = 0; i < scenes.Length; i++)
        {
            if (!scenes[i].enabled)
                continue;

            if (string.Equals(
                    scenes[i].path,
                    requiredPath,
                    StringComparison.Ordinal))
            {
                if (enabledIndex != expectedEnabledIndex)
                {
                    report.Add(
                        Subject42ValidationSeverity.Error,
                        "BUILD_SCENE_ORDER",
                        $"'{requiredPath}' must be enabled build scene " +
                        $"#{expectedEnabledIndex}, found #{enabledIndex}.");
                }

                return;
            }

            enabledIndex++;
        }

        report.Add(
            Subject42ValidationSeverity.Error,
            "REQUIRED_BUILD_SCENE",
            $"Required build scene is not enabled: '{requiredPath}'.");
    }

    private static void ValidateSerializedReferences(
        Subject42ValidationReport report)
    {
        HashSet<string> productionPaths = GetProductionAssetPaths();
        HashSet<string> reportedReferences = new(
            StringComparer.OrdinalIgnoreCase);

        foreach (string path in productionPaths)
        {
            if (!SerializedExtensions.Contains(Path.GetExtension(path)))
                continue;

            string fullPath = Path.GetFullPath(path);
            if (!File.Exists(fullPath))
                continue;

            string text;

            try
            {
                text = File.ReadAllText(fullPath);
            }
            catch (Exception exception)
            {
                report.Add(
                    Subject42ValidationSeverity.Warning,
                    "ASSET_READ_FAILED",
                    $"Could not inspect '{path}': {exception.Message}");
                continue;
            }

            if (MissingScriptRegex.IsMatch(text))
            {
                report.Add(
                    Subject42ValidationSeverity.Error,
                    "MISSING_SCRIPT",
                    $"Missing MonoBehaviour script in '{path}'.",
                    AssetDatabase.LoadMainAssetAtPath(path));
            }

            MatchCollection scriptMatches = ScriptGuidRegex.Matches(text);
            HashSet<string> scriptGuids = new(
                StringComparer.OrdinalIgnoreCase);
            for (int matchIndex = 0;
                 matchIndex < scriptMatches.Count;
                 matchIndex++)
            {
                string guid = scriptMatches[matchIndex].Groups[1].Value;
                scriptGuids.Add(guid);

                // Unity serializes editor-only MaterialVersion metadata inside
                // materials. It is irrelevant to a player build and can point
                // at a package version that is not installed.
                if (string.Equals(
                        Path.GetExtension(path),
                        ".mat",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string scriptPath = AssetDatabase.GUIDToAssetPath(guid);

                if (!string.IsNullOrEmpty(scriptPath) &&
                    AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath) != null)
                {
                    continue;
                }

                string key = $"script|{path}|{guid}";
                if (!reportedReferences.Add(key))
                    continue;

                report.Add(
                    Subject42ValidationSeverity.Error,
                    "BROKEN_SCRIPT_GUID",
                    $"'{path}' references missing script GUID {guid}.",
                    AssetDatabase.LoadMainAssetAtPath(path));
            }

            MatchCollection assetMatches = AssetGuidRegex.Matches(text);
            for (int matchIndex = 0;
                 matchIndex < assetMatches.Count;
                 matchIndex++)
            {
                string guid = assetMatches[matchIndex].Groups[1].Value;
                if (IsZeroGuid(guid) ||
                    scriptGuids.Contains(guid) ||
                    !string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)))
                {
                    continue;
                }

                string key = $"asset|{path}|{guid}";
                if (!reportedReferences.Add(key))
                    continue;

                report.Add(
                    Subject42ValidationSeverity.Warning,
                    "BROKEN_ASSET_GUID",
                    $"'{path}' references missing asset GUID {guid}.",
                    AssetDatabase.LoadMainAssetAtPath(path));
            }
        }
    }

    private static HashSet<string> GetProductionAssetPaths()
    {
        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

        for (int i = 0; i < scenes.Length; i++)
        {
            if (!scenes[i].enabled)
                continue;

            string[] dependencies = AssetDatabase.GetDependencies(
                scenes[i].path,
                true);
            for (int dependencyIndex = 0;
                 dependencyIndex < dependencies.Length;
                 dependencyIndex++)
            {
                paths.Add(dependencies[dependencyIndex]);
            }
        }

        string resourcesRoot = $"{ProjectRoot}/Resources";
        string[] resourceGuids = AssetDatabase.FindAssets(
            string.Empty,
            new[] { resourcesRoot });
        for (int i = 0; i < resourceGuids.Length; i++)
            paths.Add(AssetDatabase.GUIDToAssetPath(resourceGuids[i]));

        return paths;
    }

    private static void ValidateMainMenuScene(
        Subject42ValidationReport report)
    {
        ValidatePreviewScene(MainMenuScene, scene =>
        {
            BunkerContext context = RequireSingle<BunkerContext>(
                scene, report);
            RequireSingle<RunSelectionManager>(scene, report);
            BunkerRunStarter starter = RequireSingle<BunkerRunStarter>(
                scene, report);
            RequireSingle<CurrencyManager>(scene, report);
            RequireSingle<UnlockProgressService>(scene, report);
            RequireSingle<MetaProgressionManager>(scene, report);
            RequireSingle<AudioService>(scene, report);

            if (context != null)
            {
                RequireReference(context.Panels, context, "Panels", report);
                RequireReference(
                    context.Notifications,
                    context,
                    "Notifications",
                    report);
                RequireReference(context.Events, context, "Events", report);
                RequireReference(context.Shop, context, "Shop", report);
                RequireReference(
                    context.RunStarter,
                    context,
                    "RunStarter",
                    report);
                RequireReference(
                    context.ContentRegistry,
                    context,
                    "ContentRegistry",
                    report);
            }

            if (starter != null)
            {
                RequireSerializedObject(starter, "startingStageProfile", report);
                RequireSerializedObject(starter, "startingWorldRule", report);
                RequireSerializedObject(starter, "startingLocalAnomaly", report);
                RequireSerializedObject(starter, "transitionCamera", report);
                RequireSerializedObject(starter, "cameraRig", report);
                RequireSerializedObject(starter, "cameraFollow", report);
                RequireSerializedObject(starter, "playerMovement", report);
                RequireSerializedObject(starter, "bunkerCursor", report);
                RequireSerializedString(starter, "gameplaySceneName", report);
            }

            FootballMinigame football = FindFirst<FootballMinigame>(scene);
            if (football != null)
                ValidateFootball(football, report);
        }, report);
    }

    private static void ValidateGameplayScene(
        Subject42ValidationReport report)
    {
        ValidatePreviewScene(GameplayScene, scene =>
        {
            CharacterSpawner characterSpawner =
                RequireSingle<CharacterSpawner>(scene, report);
            UpgradeManager upgradeManager =
                RequireSingle<UpgradeManager>(scene, report);
            RequireSingle<RunStatsManager>(scene, report);
            RequireSingle<KillManager>(scene, report);
            RunTimer timer = RequireSingle<RunTimer>(scene, report);
            RequireSingle<RunFlowController>(scene, report);
            RunEndService endService =
                RequireSingle<RunEndService>(scene, report);
            RequireSingle<HUDManager>(scene, report);
            LevelModifiersApplier modifiers =
                RequireSingle<LevelModifiersApplier>(scene, report);
            LevelChoiceManager choice =
                RequireSingle<LevelChoiceManager>(scene, report);
            GameOverManager gameOver =
                RequireSingle<GameOverManager>(scene, report);
            RequireSingle<CurrencyManager>(scene, report);
            LevelAnomalyController anomaly =
                RequireSingle<LevelAnomalyController>(scene, report);
            WorldRuleController worldRule =
                RequireSingle<WorldRuleController>(scene, report);
            GameplayAreaService area =
                RequireSingle<GameplayAreaService>(scene, report);

            if (characterSpawner != null)
            {
                RequireSerializedObject(
                    characterSpawner, "defaultCharacter", report);
                RequireSerializedObject(
                    characterSpawner, "defaultWeapon", report);
                RequireSerializedObject(
                    characterSpawner, "metaUpgradeApplier", report);
                RequireSerializedObject(
                    characterSpawner, "upgradeApplier", report);
            }

            if (upgradeManager != null)
            {
                RequireSerializedObject(
                    upgradeManager, "upgradePanelView", report);
                RequireSerializedObject(
                    upgradeManager, "upgradeApplier", report);
                RequireSerializedArray(
                    upgradeManager, "allUpgrades", 1, report);
            }

            if (timer != null)
                RequireSerializedObject(timer, "gameplayArea", report);

            if (endService != null)
                RequireSerializedString(endService, "bunkerSceneName", report);

            if (modifiers != null)
            {
                RequireSerializedObject(modifiers, "enemySpawner", report);
                RequireSerializedObject(modifiers, "runFlowController", report);
                RequireSerializedObject(modifiers, "anomalyController", report);
                RequireSerializedObject(modifiers, "worldRuleController", report);

                ExplorationSectorConfig fallback =
                    Resources.Load<ExplorationSectorConfig>(
                        "ProductionRun/ExplorationSectorConfig");
                if (fallback == null)
                {
                    report.Add(
                        Subject42ValidationSeverity.Error,
                        "EXPLORATION_FALLBACK",
                        "Resources fallback ExplorationSectorConfig is missing.",
                        modifiers);
                }
                else
                {
                    report.Add(
                        Subject42ValidationSeverity.Info,
                        "EXPLORATION_FALLBACK",
                        "ExplorationSectorConfig Resources fallback is present.",
                        fallback);
                }
            }

            if (choice != null)
            {
                RequireSerializedObject(choice, "panelView", report);
                RequireSerializedArray(
                    choice, "availableWorldRules", 3, report);
                RequireSerializedObject(
                    choice, "defaultLocalAnomaly", report);
                RequireSerializedArray(choice, "stageProfiles", 4, report);
                RequireSerializedString(choice, "gameplaySceneName", report);
            }

            if (gameOver != null)
                RequireSerializedObject(gameOver, "runResultView", report);

            if (area != null)
            {
                RequireReference(
                    area.PlayableArea,
                    area,
                    "playableArea",
                    report);
                RequireReference(
                    area.SpawnArea,
                    area,
                    "spawnArea",
                    report);
            }

            if (anomaly != null)
            {
                RequireSerializedObject(anomaly, "visual", report);
                RequireSerializedObject(anomaly, "gameplayArea", report);
            }

            if (worldRule != null)
                RequireSerializedObject(worldRule, "worldRuleVisual", report);

        }, report);
    }

    private static void ValidateFootball(
        FootballMinigame football,
        Subject42ValidationReport report)
    {
        string[] requiredReferences =
        {
            "arenaBounds",
            "ballSpawnZone",
            "anomalySpawnZone",
            "targetSpawnZone",
            "playerBoundary",
            "startZone",
            "hud",
            "cameraFollow",
            "ballsRuntime",
            "anomaliesRuntime",
            "targetsRuntime",
            "ballPrefab",
            "gravityAnomalyPrefab",
            "gravityAnomalyData",
            "targetTemplate"
        };

        for (int i = 0; i < requiredReferences.Length; i++)
        {
            RequireSerializedObject(
                football,
                requiredReferences[i],
                report);
        }
    }

    private static void ValidateDataAssets(
        Subject42ValidationReport report)
    {
        ValidateUniqueIds<WorldRuleData>(
            data => data.Id,
            true,
            report);
        ValidateUniqueIds<LocalAnomalyData>(
            data => data.Id,
            true,
            report);
        ValidateUniqueIds<AnomalyItemData>(
            data => data.Id,
            true,
            report);
        ValidateUniqueIds<EvolutionDefinition>(
            data => data.Id,
            true,
            report);
        ValidateUniqueIds<AnomalyStabilizerData>(
            data => data.Id,
            true,
            report);
        ValidateUniqueIds<BunkerContentData>(
            data => data.Id,
            true,
            report);
        ValidateUniqueIds<UnlockableContentData>(
            data => data.id,
            true,
            report);
        ValidateUniqueIds<WorldLootRewardDefinition>(
            data => data.RewardId,
            true,
            report);

        CharacterData[] characters = FindAssetsOfType<CharacterData>();
        if (characters.Length == 0)
        {
            report.Add(
                Subject42ValidationSeverity.Error,
                "CHARACTER_DATA",
                "No CharacterData assets were found.");
        }

        for (int i = 0; i < characters.Length; i++)
            ValidateCharacter(characters[i], report);

        WeaponData[] weapons = FindAssetsOfType<WeaponData>();
        if (weapons.Length == 0)
        {
            report.Add(
                Subject42ValidationSeverity.Error,
                "WEAPON_DATA",
                "No WeaponData assets were found.");
        }

        for (int i = 0; i < weapons.Length; i++)
            ValidateWeapon(weapons[i], report);

        StageProfileData[] stages = FindAssetsOfType<StageProfileData>();
        for (int i = 0; i < stages.Length; i++)
        {
            StageProfileData stage = stages[i];
            if (stage.SpawnProfile == null)
            {
                report.Add(
                    Subject42ValidationSeverity.Error,
                    "STAGE_SPAWN_PROFILE",
                    $"'{stage.name}' has no EnemySpawnProfile.",
                    stage);
            }

            if (stage.BossPrefab == null)
            {
                report.Add(
                    Subject42ValidationSeverity.Error,
                    "STAGE_BOSS_PREFAB",
                    $"'{stage.name}' has no boss prefab.",
                    stage);
            }
        }

        EnemySpawnProfile[] spawnProfiles =
            FindAssetsOfType<EnemySpawnProfile>();
        HashSet<GameObject> validatedEnemies = new();

        for (int i = 0; i < spawnProfiles.Length; i++)
        {
            ValidateSpawnProfile(
                spawnProfiles[i],
                validatedEnemies,
                report);
        }
    }

    private static void ValidateCharacter(
        CharacterData data,
        Subject42ValidationReport report)
    {
        if (string.IsNullOrWhiteSpace(data.characterName))
        {
            report.Add(
                Subject42ValidationSeverity.Error,
                "CHARACTER_NAME",
                $"'{data.name}' has no characterName.",
                data);
        }

        if (data.characterPrefab == null)
        {
            report.Add(
                Subject42ValidationSeverity.Error,
                "CHARACTER_PREFAB",
                $"'{data.name}' has no character prefab.",
                data);
            return;
        }

        GameObject prefab = data.characterPrefab;
        RequirePrefabComponent<PlayerHealth>(prefab, data, report);
        RequirePrefabComponent<CharacterMovement2D>(prefab, data, report);
        RequirePrefabComponent<Rigidbody2D>(prefab, data, report);
        RequirePrefabComponent<Collider2D>(prefab, data, report);
        RequirePrefabComponent<PlayerPickupRadius>(prefab, data, report);
        RequirePrefabComponent<EnemySpawner>(prefab, data, report);

        if (data.maxHealth <= 0 || data.moveSpeed <= 0f)
        {
            report.Add(
                Subject42ValidationSeverity.Error,
                "CHARACTER_STATS",
                $"'{data.name}' has non-positive base health or speed.",
                data);
        }
    }

    private static void ValidateWeapon(
        WeaponData data,
        Subject42ValidationReport report)
    {
        if (string.IsNullOrWhiteSpace(data.weaponName))
        {
            report.Add(
                Subject42ValidationSeverity.Error,
                "WEAPON_NAME",
                $"'{data.name}' has no weaponName.",
                data);
        }

        if (data.weaponPrefab == null)
        {
            report.Add(
                Subject42ValidationSeverity.Error,
                "WEAPON_PREFAB",
                $"'{data.name}' has no weapon prefab.",
                data);
            return;
        }

        if (data.weaponPrefab.GetComponent<BaseWeapon>() == null)
        {
            report.Add(
                Subject42ValidationSeverity.Error,
                "WEAPON_COMPONENT",
                $"Weapon prefab '{data.weaponPrefab.name}' used by " +
                $"'{data.name}' has no root BaseWeapon.",
                data.weaponPrefab);
        }
    }

    private static void ValidateSpawnProfile(
        EnemySpawnProfile profile,
        HashSet<GameObject> validatedEnemies,
        Subject42ValidationReport report)
    {
        EnemySpawnPhase[] phases = profile.Phases;
        if (phases == null || phases.Length == 0)
        {
            report.Add(
                Subject42ValidationSeverity.Warning,
                "EMPTY_SPAWN_PROFILE",
                $"'{profile.name}' contains no spawn phases.",
                profile);
            return;
        }

        for (int phaseIndex = 0; phaseIndex < phases.Length; phaseIndex++)
        {
            EnemySpawnPhase phase = phases[phaseIndex];
            if (phase == null || phase.enemies == null)
                continue;

            for (int entryIndex = 0;
                 entryIndex < phase.enemies.Length;
                 entryIndex++)
            {
                EnemySpawnEntry entry = phase.enemies[entryIndex];
                if (entry == null || entry.weight <= 0f)
                    continue;

                if (entry.enemyPrefab == null)
                {
                    report.Add(
                        Subject42ValidationSeverity.Error,
                        "ENEMY_PREFAB_REFERENCE",
                        $"'{profile.name}' phase {phaseIndex} has a " +
                        "positive-weight entry without a prefab.",
                        profile);
                    continue;
                }

                if (!validatedEnemies.Add(entry.enemyPrefab))
                    continue;

                GameObject prefab = entry.enemyPrefab;
                EnemyHealth health = prefab.GetComponent<EnemyHealth>();
                EnemyIdentity identity = prefab.GetComponent<EnemyIdentity>();

                if (health == null)
                {
                    report.Add(
                        Subject42ValidationSeverity.Error,
                        "ENEMY_HEALTH",
                        $"Spawned enemy '{prefab.name}' has no root EnemyHealth.",
                        prefab);
                }

                if (identity == null ||
                    string.IsNullOrWhiteSpace(identity.EnemyId))
                {
                    report.Add(
                        Subject42ValidationSeverity.Error,
                        "ENEMY_IDENTITY",
                        $"Spawned enemy '{prefab.name}' has no usable EnemyIdentity.",
                        prefab);
                }

                if (prefab.GetComponentInChildren<Collider2D>(true) == null)
                {
                    report.Add(
                        Subject42ValidationSeverity.Error,
                        "ENEMY_COLLIDER",
                        $"Spawned enemy '{prefab.name}' has no Collider2D.",
                        prefab);
                }

                if (!prefab.CompareTag("Enemy"))
                {
                    report.Add(
                        Subject42ValidationSeverity.Error,
                        "ENEMY_TAG",
                        $"Spawned enemy '{prefab.name}' is not tagged Enemy.",
                        prefab);
                }
            }
        }
    }

    private static void ValidateProductionDependencies(
        Subject42ValidationReport report)
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;

        for (int i = 0; i < scenes.Length; i++)
        {
            if (!scenes[i].enabled)
                continue;

            string[] dependencies = AssetDatabase.GetDependencies(
                scenes[i].path,
                true);

            for (int dependencyIndex = 0;
                 dependencyIndex < dependencies.Length;
                 dependencyIndex++)
            {
                string dependency = dependencies[dependencyIndex];
                if (!dependency.StartsWith(
                        ProjectRoot,
                        StringComparison.Ordinal) ||
                    !IsTestOrLegacyPath(dependency))
                {
                    continue;
                }

                report.Add(
                    Subject42ValidationSeverity.Error,
                    "TEST_CONTENT_DEPENDENCY",
                    $"Build scene '{scenes[i].path}' depends on " +
                    $"test/legacy content '{dependency}'.",
                    AssetDatabase.LoadMainAssetAtPath(dependency));
            }
        }
    }

    private static void ValidatePreviewScene(
        string path,
        Action<Scene> validate,
        Subject42ValidationReport report)
    {
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            return;

        Scene scene = default;

        try
        {
            scene = EditorSceneManager.OpenPreviewScene(path);
            validate(scene);
        }
        catch (Exception exception)
        {
            report.Add(
                Subject42ValidationSeverity.Error,
                "SCENE_INSPECTION",
                $"Could not inspect '{path}': {exception.Message}");
        }
        finally
        {
            if (scene.IsValid())
                EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static T RequireSingle<T>(
        Scene scene,
        Subject42ValidationReport report)
        where T : Component
    {
        List<T> components = FindAll<T>(scene);

        if (components.Count == 1)
            return components[0];

        report.Add(
            Subject42ValidationSeverity.Error,
            components.Count == 0
                ? "REQUIRED_COMPONENT"
                : "DUPLICATE_MANAGER",
            components.Count == 0
                ? $"Scene '{scene.path}' has no {typeof(T).Name}."
                : $"Scene '{scene.path}' has {components.Count} " +
                  $"instances of {typeof(T).Name}.",
            components.Count > 0 ? components[0] : null);
        return components.Count > 0 ? components[0] : null;
    }

    private static T FindFirst<T>(Scene scene) where T : Component
    {
        List<T> components = FindAll<T>(scene);
        return components.Count > 0 ? components[0] : null;
    }

    private static List<T> FindAll<T>(Scene scene) where T : Component
    {
        List<T> result = new();
        GameObject[] roots = scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            result.AddRange(roots[i].GetComponentsInChildren<T>(true));
        }

        return result;
    }

    private static void RequireSerializedObject(
        UnityEngine.Object owner,
        string propertyName,
        Subject42ValidationReport report)
    {
        SerializedProperty property = FindProperty(owner, propertyName, report);
        if (property == null)
            return;

        if (property.propertyType != SerializedPropertyType.ObjectReference ||
            property.objectReferenceValue == null)
        {
            report.Add(
                Subject42ValidationSeverity.Error,
                "REQUIRED_REFERENCE",
                $"{owner.GetType().Name}.{propertyName} is not assigned.",
                owner);
        }
    }

    private static void RequireSerializedArray(
        UnityEngine.Object owner,
        string propertyName,
        int minimumCount,
        Subject42ValidationReport report)
    {
        SerializedProperty property = FindProperty(owner, propertyName, report);
        if (property == null)
            return;

        if (!property.isArray || property.arraySize < minimumCount)
        {
            report.Add(
                Subject42ValidationSeverity.Error,
                "REQUIRED_ARRAY",
                $"{owner.GetType().Name}.{propertyName} requires at least " +
                $"{minimumCount} entries.",
                owner);
            return;
        }

        for (int i = 0; i < property.arraySize; i++)
        {
            SerializedProperty element = property.GetArrayElementAtIndex(i);
            if (element.propertyType == SerializedPropertyType.ObjectReference &&
                element.objectReferenceValue == null)
            {
                report.Add(
                    Subject42ValidationSeverity.Error,
                    "NULL_ARRAY_ENTRY",
                    $"{owner.GetType().Name}.{propertyName}[{i}] is null.",
                    owner);
            }
        }
    }

    private static void RequireSerializedString(
        UnityEngine.Object owner,
        string propertyName,
        Subject42ValidationReport report)
    {
        SerializedProperty property = FindProperty(owner, propertyName, report);
        if (property == null)
            return;

        if (property.propertyType != SerializedPropertyType.String ||
            string.IsNullOrWhiteSpace(property.stringValue))
        {
            report.Add(
                Subject42ValidationSeverity.Error,
                "REQUIRED_STRING",
                $"{owner.GetType().Name}.{propertyName} is empty.",
                owner);
        }
    }

    private static SerializedProperty FindProperty(
        UnityEngine.Object owner,
        string propertyName,
        Subject42ValidationReport report)
    {
        SerializedObject serializedObject = new(owner);
        SerializedProperty property = serializedObject.FindProperty(propertyName);

        if (property == null)
        {
            report.Add(
                Subject42ValidationSeverity.Error,
                "VALIDATOR_SCHEMA",
                $"Validator could not find serialized property " +
                $"{owner.GetType().Name}.{propertyName}.",
                owner);
        }

        return property;
    }

    private static void RequireReference(
        UnityEngine.Object value,
        UnityEngine.Object owner,
        string fieldName,
        Subject42ValidationReport report)
    {
        if (value != null)
            return;

        report.Add(
            Subject42ValidationSeverity.Error,
            "REQUIRED_REFERENCE",
            $"{owner.GetType().Name}.{fieldName} is not assigned.",
            owner);
    }

    private static void RequirePrefabComponent<T>(
        GameObject prefab,
        UnityEngine.Object owner,
        Subject42ValidationReport report)
        where T : Component
    {
        if (prefab.GetComponentInChildren<T>(true) != null)
            return;

        report.Add(
            Subject42ValidationSeverity.Error,
            "PREFAB_COMPONENT",
            $"Prefab '{prefab.name}' used by '{owner.name}' has no " +
            $"{typeof(T).Name}.",
            prefab);
    }

    private static void ValidateUniqueIds<T>(
        Func<T, string> getId,
        bool requireValue,
        Subject42ValidationReport report)
        where T : ScriptableObject
    {
        T[] assets = FindAssetsOfType<T>();
        Dictionary<string, T> byId = new(
            StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < assets.Length; i++)
        {
            T asset = assets[i];
            string id = getId(asset)?.Trim();

            if (string.IsNullOrWhiteSpace(id))
            {
                if (requireValue)
                {
                    report.Add(
                        Subject42ValidationSeverity.Error,
                        "EMPTY_STABLE_ID",
                        $"'{asset.name}' ({typeof(T).Name}) has no stable ID.",
                        asset);
                }

                continue;
            }

            if (byId.TryGetValue(id, out T existing))
            {
                report.Add(
                    Subject42ValidationSeverity.Error,
                    "DUPLICATE_STABLE_ID",
                    $"{typeof(T).Name} ID '{id}' is used by " +
                    $"'{existing.name}' and '{asset.name}'.",
                    asset);
                continue;
            }

            byId.Add(id, asset);
        }
    }

    private static T[] FindAssetsOfType<T>() where T : UnityEngine.Object
    {
        string[] guids = AssetDatabase.FindAssets(
            $"t:{typeof(T).Name}",
            new[] { ProjectRoot });
        List<T> assets = new(guids.Length);

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                assets.Add(asset);
        }

        return assets.ToArray();
    }

    private static bool IsZeroGuid(string guid)
    {
        for (int i = 0; i < guid.Length; i++)
        {
            if (guid[i] != '0')
                return false;
        }

        return true;
    }

    private static bool IsTestOrLegacyPath(string path)
    {
        string normalized = path.Replace('\\', '/').ToLowerInvariant();
        return normalized.Contains("/testscene/") ||
            normalized.Contains("/tests/") ||
            normalized.Contains("_old.") ||
            normalized.Contains("_test.") ||
            normalized.Contains("shader_test");
    }
}
#endif
