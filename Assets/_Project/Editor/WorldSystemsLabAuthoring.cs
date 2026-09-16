#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class WorldSystemsLabAuthoring
{
    private const string ProductionScene =
        "Assets/_Project/Scenes/MainBuild/MVP.unity";
    private const string ProductionCharacter =
        "Assets/_Project/Scriptable Objects/Characters/03_Vika.asset";
    private const string RuleFolder =
        "Assets/_Project/Scriptable Objects/WorldRules";
    private const string PropProfile =
        "Assets/_Project/Environment/Props/Resources/PropScatterProfile.asset";
    private const string ExplorationConfig =
        "Assets/_Project/Resources/ProductionRun/ExplorationSectorConfig.asset";


    [MenuItem("Tools/Subject42/World Systems Lab/Open")]
    public static void Open()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;
        if (!File.Exists(WorldSystemsLabController.ScenePath))
            Create();
        EditorSceneManager.OpenScene(WorldSystemsLabController.ScenePath);
    }

    [MenuItem("Tools/Subject42/World Systems Lab/Rebuild Scene")]
    public static void Create()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Exit Play Mode first.");

        Directory.CreateDirectory(Path.GetDirectoryName(
            WorldSystemsLabController.ScenePath
        ));
        Scene previous = SceneManager.GetActiveScene();
        Scene openLab = SceneManager.GetSceneByPath(
            WorldSystemsLabController.ScenePath
        );
        bool reopenLab = openLab.IsValid() && openLab.isLoaded;
        bool labWasActive = reopenLab && previous.handle == openLab.handle;
        bool openedFallback = false;

        if (reopenLab)
        {
            if (labWasActive)
            {
                previous = Enumerable.Range(0, SceneManager.sceneCount)
                    .Select(SceneManager.GetSceneAt)
                    .FirstOrDefault(candidate =>
                        candidate.IsValid() &&
                        candidate.isLoaded &&
                        candidate.handle != openLab.handle
                    );
            }

            EditorSceneManager.CloseScene(openLab, true);
        }

        if (!previous.IsValid() || string.IsNullOrEmpty(previous.path))
        {
            previous = EditorSceneManager.OpenScene(
                "Assets/_Project/Scenes/MainBuild/MainMenu.unity",
                OpenSceneMode.Single
            );
            openedFallback = true;
        }
        Light2D[] existingLights = UnityEngine.Object.FindObjectsByType<Light2D>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        ).Where(light => light.enabled &&
            light.lightType == Light2D.LightType.Global).ToArray();
        foreach (Light2D light in existingLights)
            light.enabled = false;
        Scene production = EditorSceneManager.OpenPreviewScene(ProductionScene);
        foreach (Light2D light in production.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Light2D>(true)))
        {
            if (light.lightType == Light2D.LightType.Global)
                light.enabled = false;
        }
        Scene scene = default;

        try
        {
            scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive
            );
            SceneManager.SetActiveScene(scene);

            GameplayAreaService area = CloneIntoScene(
                Find<GameplayAreaService>(production).gameObject,
                scene
            ).GetComponent<GameplayAreaService>();
            area.gameObject.name = "Gameplay Area (production bounds)";

            GameObject player = CreatePlayer(scene);
            CameraFollow cameraRig = CreateCamera(production, scene, player.transform);
            Light2D globalLight = CreateGlobalLight(scene);
            TacticalMapHUD map = CreatePresentationCanvas(production, scene);
            CreateArenaWalls(scene);

            GameObject systems = new("Production World Systems");
            SceneManager.MoveGameObjectToScene(systems, scene);

            LevelAnomalyController anomalyController =
                systems.AddComponent<LevelAnomalyController>();
            EditorUtility.CopySerialized(
                Find<LevelAnomalyController>(production),
                anomalyController
            );
            SetObject(anomalyController, "gameplayArea", area);
            SetObject(anomalyController, "visual", null);

            GameObject ruleHost = map.GetComponentInParent<Canvas>().gameObject;
            WorldRuleController ruleController =
                ruleHost.AddComponent<WorldRuleController>();
            EditorUtility.CopySerialized(
                Find<WorldRuleController>(production),
                ruleController
            );

            WorldRuleVisual ruleVisual = ruleHost.AddComponent<WorldRuleVisual>();
            WorldRuleVisual sourceRuleVisual = Find<WorldRuleVisual>(production);
            EditorUtility.CopySerialized(sourceRuleVisual, ruleVisual);
            ConfigureRuleVisual(
                production,
                scene,
                sourceRuleVisual,
                ruleVisual,
                map,
                cameraRig.ControlledCamera,
                globalLight
            );

            SetObject(ruleController, "worldRuleVisual", ruleVisual);
            SetObject(ruleController, "goldenDeathFxPrefab", null);
            SetObject(ruleController, "goldenCoinPrefab", null);

            GameObject eventObject = new("Production Events");
            SceneManager.MoveGameObjectToScene(eventObject, scene);
            WorldEventSpawner eventSpawner =
                eventObject.AddComponent<WorldEventSpawner>();
            WorldEventSpawner sourceSpawner = Find<WorldEventSpawner>(production);
            EditorUtility.CopySerialized(sourceSpawner, eventSpawner);
            SetObject(eventSpawner, "gameplayArea", area);
            SetObject(eventSpawner, "eventRewardContainerPrefab", null);
            SetObject(eventSpawner, "doubleOrLeave", null);

            SetObject(map, "gameplayArea", area);
            SetObject(map, "anomalyController", anomalyController);
            SetObject(map, "eventSpawner", eventSpawner);
            map.BindPlayer(player.transform);

            WorldSystemsLabController lab = new GameObject(
                "WorldSystemsLabController"
            ).AddComponent<WorldSystemsLabController>();
            SceneManager.MoveGameObjectToScene(lab.gameObject, scene);
            SetObject(lab, "player", player.transform);
            SetObject(lab, "cameraRig", cameraRig);
            SetObject(lab, "gameplayArea", area);
            SetObject(lab, "worldRules", ruleController);
            SetObject(lab, "anomalies", anomalyController);
            SetObject(lab, "events", eventSpawner);
            SetObject(lab, "tacticalMap", map);
            SetArray(lab, "worldRuleAssets", FindWorldRules());
            SetArray(lab, "normalAnomalyAssets",
                AssetDatabase.LoadAssetAtPath<ExplorationSectorConfig>(
                    ExplorationConfig
                ).NormalAnomalies);
            SetArray(lab, "eventPrefabs", sourceSpawner.EventPrefabs.ToArray());
            SetObject(lab, "explorationConfig",
                AssetDatabase.LoadAssetAtPath<ExplorationSectorConfig>(
                    ExplorationConfig
                ));
            SetObject(lab, "propScatterProfile",
                AssetDatabase.LoadAssetAtPath<PropScatterProfile>(PropProfile));

            EditorSceneManager.SaveScene(scene, WorldSystemsLabController.ScenePath);
        }
        finally
        {
            if (scene.IsValid() && scene.isLoaded)
                EditorSceneManager.CloseScene(scene, true);
            EditorSceneManager.ClosePreviewScene(production);
            foreach (Light2D light in existingLights)
            {
                if (light != null)
                    light.enabled = true;
            }
            if (previous.IsValid() && previous.isLoaded)
                SceneManager.SetActiveScene(previous);
        }

        AssetDatabase.Refresh();

        if (reopenLab)
        {
            Scene rebuiltLab = EditorSceneManager.OpenScene(
                WorldSystemsLabController.ScenePath,
                openedFallback
                    ? OpenSceneMode.Single
                    : OpenSceneMode.Additive
            );
            if (labWasActive)
                SceneManager.SetActiveScene(rebuiltLab);
        }
    }

    private static GameObject CreatePlayer(Scene scene)
    {
        CharacterData character = AssetDatabase.LoadAssetAtPath<CharacterData>(
            ProductionCharacter
        );
        GameObject prefab = character != null ? character.ProductionPrefab : null;
        if (prefab == null)
            throw new InvalidOperationException("Production player prefab missing.");

        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(
            prefab,
            scene
        );
        PrefabUtility.UnpackPrefabInstance(
            player,
            PrefabUnpackMode.Completely,
            InteractionMode.AutomatedAction
        );
        player.name = "Player";
        player.tag = "Player";
        player.transform.position = Vector3.zero;

        foreach (Subject42.Combat.OrbitalStation.OrbitalStationView view in
            player.GetComponentsInChildren<
                Subject42.Combat.OrbitalStation.OrbitalStationView>(true))
        {
            Transform authoredRoot = view.RingsRoot != null
                ? view.RingsRoot.parent
                : null;
            if (authoredRoot != null && authoredRoot != player.transform)
                UnityEngine.Object.DestroyImmediate(authoredRoot.gameObject);
        }

        foreach (EnemySpawner component in
            player.GetComponentsInChildren<EnemySpawner>(true))
        {
            UnityEngine.Object.DestroyImmediate(component);
        }
        foreach (BaseWeapon weapon in
            player.GetComponentsInChildren<BaseWeapon>(true))
        {
            UnityEngine.Object.DestroyImmediate(weapon.gameObject);
        }
        foreach (PlayerPickupRadius component in
            player.GetComponentsInChildren<PlayerPickupRadius>(true))
        {
            UnityEngine.Object.DestroyImmediate(component);
        }
        foreach (PlayerCombatModifiers component in
            player.GetComponentsInChildren<PlayerCombatModifiers>(true))
        {
            UnityEngine.Object.DestroyImmediate(component);
        }
        foreach (MonoBehaviour component in
            player.GetComponentsInChildren<MonoBehaviour>(true))
        {
            string componentNamespace = component.GetType().Namespace ?? string.Empty;
            if (componentNamespace.StartsWith(
                "Subject42.Combat.OrbitalStation",
                StringComparison.Ordinal))
            {
                UnityEngine.Object.DestroyImmediate(component);
            }
        }

        CharacterMovement2D movement = player.GetComponent<CharacterMovement2D>();
        SerializedObject movementData = new(movement);
        movementData.FindProperty("baseMoveSpeed").floatValue = 6f;
        movementData.ApplyModifiedPropertiesWithoutUndo();
        return player;
    }

    private static CameraFollow CreateCamera(
        Scene production,
        Scene scene,
        Transform player)
    {
        CameraFollow source = Find<CameraFollow>(production);
        GameObject root = new("Camera");
        SceneManager.MoveGameObjectToScene(root, scene);
        Camera camera = root.AddComponent<Camera>();
        EditorUtility.CopySerialized(source.ControlledCamera, camera);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.055f, 0.07f, 0.085f);
        camera.orthographicSize = 14f;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        root.AddComponent<AudioListener>();
        root.tag = "MainCamera";

        CameraFollow follow = root.AddComponent<CameraFollow>();
        EditorUtility.CopySerialized(source, follow);
        follow.target = player;
        follow.offset = new Vector3(0f, 0f, -10f);
        root.transform.position = follow.offset;
        SetObject(follow, "controlledCamera", camera);
        return follow;
    }

    private static Light2D CreateGlobalLight(Scene scene)
    {
        GameObject root = new("World Light");
        SceneManager.MoveGameObjectToScene(root, scene);
        Light2D light = root.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.intensity = 1f;
        return light;
    }

    private static TacticalMapHUD CreatePresentationCanvas(
        Scene production,
        Scene scene)
    {
        TacticalMapHUD source = Find<TacticalMapHUD>(production);
        Canvas sourceCanvas = source.GetComponentInParent<Canvas>();
        if (sourceCanvas == null)
            throw new InvalidOperationException("Production tactical map Canvas missing.");
        GameObject canvasRoot = CloneIntoScene(sourceCanvas.gameObject, scene);
        canvasRoot.name = "World Systems UI (map + rule overlays)";
        TacticalMapHUD map =
            canvasRoot.GetComponentInChildren<TacticalMapHUD>(true);
        Transform mapRoot = map.LayoutRoot;
        Transform overlay = canvasRoot.GetComponentsInChildren<Transform>(true)
            .First(item => item.name == "WorldRuleOverlay");
        Transform wind = canvasRoot.GetComponentInChildren<WindRuleIndicator>(true)
            .transform;
        Transform condensation = canvasRoot
            .GetComponentInChildren<CondensationFogOverlay>(true).transform;
        PruneToTargets(
            canvasRoot.transform,
            new HashSet<Transform> { mapRoot, overlay, wind, condensation }
        );
        foreach (Transform item in
            canvasRoot.GetComponentsInChildren<Transform>(true))
        {
            if (item.name == "HUD")
                item.name = "World Systems Presentation";
        }

        foreach (MonoBehaviour behaviour in
            canvasRoot.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (behaviour == null ||
                behaviour.GetType().Assembly != typeof(WorldRuleController).Assembly ||
                behaviour is TacticalMapHUD ||
                behaviour is WindRuleIndicator ||
                behaviour is CondensationFogOverlay)
            {
                continue;
            }

            UnityEngine.Object.DestroyImmediate(behaviour);
        }

        SerializedObject mapData = new(map);
        RectTransform bossLegend = mapData.FindProperty("bossLegendRow")
            .objectReferenceValue as RectTransform;
        mapData.FindProperty("bossLegendRow").objectReferenceValue = null;
        mapData.ApplyModifiedPropertiesWithoutUndo();
        if (bossLegend != null)
            UnityEngine.Object.DestroyImmediate(bossLegend.gameObject);

        Canvas canvas = canvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.worldCamera = null;
        return map;
    }

    private static void PruneToTargets(
        Transform root,
        HashSet<Transform> targets)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            Transform child = root.GetChild(i);
            if (targets.Contains(child))
                continue;

            bool containsTarget = targets.Any(target => target.IsChildOf(child));
            if (containsTarget)
                PruneToTargets(child, targets);
            else
                UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
    }

    private static void ConfigureRuleVisual(
        Scene production,
        Scene scene,
        WorldRuleVisual source,
        WorldRuleVisual target,
        TacticalMapHUD map,
        Camera camera,
        Light2D globalLight)
    {
        Transform canvas = map.GetComponentInParent<Canvas>().transform;
        Image overlay = canvas.GetComponentsInChildren<Image>(true)
            .First(image => image.name == "WorldRuleOverlay");
        WindRuleIndicator wind =
            canvas.GetComponentInChildren<WindRuleIndicator>(true);
        CondensationFogOverlay condensation =
            canvas.GetComponentInChildren<CondensationFogOverlay>(true);

        SetObject(target, "fullscreenImage", overlay);
        SetObject(target, "windIndicator", wind);
        SetObject(target, "condensationFogOverlay", condensation);
        SetObject(target, "globalLight", globalLight);
        SetObject(target, "targetCamera", camera);
    }

    private static void CreateArenaWalls(Scene scene)
    {
        GameObject root = new("Environment Bounds");
        SceneManager.MoveGameObjectToScene(root, scene);
        Vector4[] walls =
        {
            new(-50f, 0f, 1f, 101f),
            new(50f, 0f, 1f, 101f),
            new(0f, -50f, 101f, 1f),
            new(0f, 50f, 101f, 1f)
        };
        foreach (Vector4 wall in walls)
        {
            GameObject item = new("Boundary");
            item.transform.SetParent(root.transform, false);
            item.transform.position = new Vector3(wall.x, wall.y, 0f);
            item.AddComponent<BoxCollider2D>().size =
                new Vector2(wall.z, wall.w);
        }
    }

    private static WorldRuleData[] FindWorldRules()
    {
        return AssetDatabase.FindAssets("t:WorldRuleData", new[] { RuleFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<WorldRuleData>)
            .Where(rule => rule != null)
            .OrderBy(rule => (int)rule.RuleType)
            .ToArray();
    }

    private static T Find<T>(Scene scene) where T : Component
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<T>(true))
            .First();
    }

    private static GameObject CloneIntoScene(GameObject source, Scene scene)
    {
        GameObject clone = UnityEngine.Object.Instantiate(source);
        SceneManager.MoveGameObjectToScene(clone, scene);
        return clone;
    }

    private static void SetObject(
        UnityEngine.Object target,
        string property,
        UnityEngine.Object value)
    {
        SerializedObject data = new(target);
        SerializedProperty field = data.FindProperty(property);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, property);
        field.objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetArray<T>(
        UnityEngine.Object target,
        string property,
        T[] values) where T : UnityEngine.Object
    {
        SerializedObject data = new(target);
        SerializedProperty field = data.FindProperty(property);
        if (field == null)
            throw new MissingFieldException(target.GetType().Name, property);
        field.arraySize = values?.Length ?? 0;
        for (int i = 0; i < field.arraySize; i++)
            field.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        data.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
