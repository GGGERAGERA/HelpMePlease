using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class SetupBunkerFootballV1Scene
{
    private const string ScenePath = "Assets/_Project/Scenes/MainMenu.unity";
    private const string AreaName = "FootballMinigame_Area";
    private const string GravityPrefabPath = "Assets/_Project/prefabs/WorldAnomalies/GravityZone.prefab";
    private const string GravityDataPath = "Assets/_Project/Scriptable Objects/LocalAnomalies/LocalAnomaly_Gravity.asset";
    private const string BallPrefabPath = "Assets/_Project/prefabs/Ball1.prefab";
    private const string GatePrefabPath = "Assets/_Project/prefabs/FootBallGates1 Variant.prefab";
    private const string ZoneSpritePath =
        "Assets/_Project/art/Sprites/Environment/Boundary/BoundarySolid.png";
    private const string SessionKey = "Bunker.Football.V1.2.RuntimeWiring.2";

    [InitializeOnLoadMethod]
    private static void QueueSetup()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, true);
        EditorApplication.delayCall += TrySetupActiveScene;
    }

    [MenuItem("Tools/Bunker/Setup Football Minigame V1")]
    public static void SetupFromMenu() => Setup(SceneManager.GetActiveScene(), true);

    private static void TrySetupActiveScene()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
            Setup(SceneManager.GetActiveScene(), false);
    }

    private static void Setup(Scene scene, bool reportWrongScene)
    {
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            if (reportWrongScene)
                Debug.LogError($"[FootballV1Setup] Open '{ScenePath}' first.");
            return;
        }

        Transform area = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(item => item.name == AreaName);
        FootballMinigame minigame = area != null
            ? area.GetComponentInChildren<FootballMinigame>(true) : null;
        FootballScoreZone target = area != null
            ? area.GetComponentInChildren<FootballScoreZone>(true) : null;
        BoxCollider2D playArea = area != null
            ? area.GetComponentsInChildren<BoxCollider2D>(true)
                .FirstOrDefault(item => item.name == "ArenaBounds" ||
                    item.name == "PlayAreaBounds") : null;
        CameraFollow cameraFollow = Object.FindFirstObjectByType<CameraFollow>(
            FindObjectsInactive.Include);
        GravityZone gravityPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GravityPrefabPath)
            ?.GetComponent<GravityZone>();
        LocalAnomalyData gravityData = AssetDatabase.LoadAssetAtPath<LocalAnomalyData>(GravityDataPath);
        BallRollVisual ballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BallPrefabPath)
            ?.GetComponent<BallRollVisual>();
        GameObject gatePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GatePrefabPath);
        Sprite zoneSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ZoneSpritePath);

        if (area == null || minigame == null || target == null || playArea == null ||
            gravityPrefab == null || gravityData == null || ballPrefab == null ||
            gatePrefab == null || zoneSprite == null)
        {
            Debug.LogError("[FootballV1Setup] Existing football scene objects or gravity assets are missing.");
            return;
        }

        FootballMinigame[] controllers = Object.FindObjectsByType<FootballMinigame>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (FootballMinigame controller in controllers)
        {
            if (controller != null && controller != minigame &&
                controller.gameObject.scene == scene)
            {
                Undo.DestroyObjectImmediate(controller.gameObject);
            }
        }
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name == "FootballArena" && root != area.gameObject)
                root.SetActive(false);

            if (root != area.gameObject &&
                root.GetComponent<FootballMinigame>() != null)
            {
                Undo.DestroyObjectImmediate(root);
            }
        }

        Vector3 center = playArea.transform.position;
        playArea.name = "ArenaBounds";
        playArea.size = new Vector2(24f, 28f);

        Transform zones = GetOrCreate(area, "Zones", center);
        BoxCollider2D ballsZone = ConfigureZone(
            zones, "Zone_Balls", center, Vector2.one);
        BoxCollider2D anomaliesZone = ConfigureZone(
            zones, "Zone_Anomalies", center, Vector2.one);
        BoxCollider2D targetsZone = ConfigureZone(
            zones, "Zone_Targets", center, Vector2.one);
        SpriteRenderer ballsVisual = ConfigureZoneVisual(
            ballsZone, zoneSprite, new Color(0.08f, 0.65f, 0.78f, 0.55f));
        SpriteRenderer anomaliesVisual = ConfigureZoneVisual(
            anomaliesZone, zoneSprite, new Color(0.035f, 0.02f, 0.055f, 0.92f));
        SpriteRenderer targetsVisual = ConfigureZoneVisual(
            targetsZone, zoneSprite, new Color(0.18f, 0.42f, 0.22f, 0.65f));

        Transform boundaryRoot = GetOrCreate(area, "Player Boundary", center);
        BoxCollider2D boundaryCollider = boundaryRoot.GetComponent<BoxCollider2D>();
        if (boundaryCollider == null)
            boundaryCollider = Undo.AddComponent<BoxCollider2D>(boundaryRoot.gameObject);
        FootballPlayerBoundary playerBoundary = boundaryRoot.GetComponent<FootballPlayerBoundary>();
        if (playerBoundary == null)
            playerBoundary = Undo.AddComponent<FootballPlayerBoundary>(boundaryRoot.gameObject);
        playerBoundary.Configure(center, 1f, 0.2f);
        Transform boundaryVisualRoot = GetOrCreate(boundaryRoot, "BoundaryVisual", boundaryRoot.position);
        boundaryVisualRoot.localPosition = Vector3.zero;
        SpriteRenderer boundaryVisual = boundaryVisualRoot.GetComponent<SpriteRenderer>();
        if (boundaryVisual == null)
            boundaryVisual = Undo.AddComponent<SpriteRenderer>(boundaryVisualRoot.gameObject);
        boundaryVisual.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        playerBoundary.ConfigureVisual(boundaryVisual);

        Transform ballSpawns = GetOrCreate(area, "BallSpawns", center);
        Transform[] ballPoints =
        {
            Marker(ballSpawns, "BallSpawn_01", center),
            Marker(ballSpawns, "BallSpawn_02", center),
            Marker(ballSpawns, "BallSpawn_03", center),
            Marker(ballSpawns, "BallSpawn_04", center)
        };

        Transform anomalySpawns = GetOrCreate(area, "AnomalySpawns", center);
        Transform[] anomalyPoints =
        {
            Marker(anomalySpawns, "AnomalySpawn_Left", center),
            Marker(anomalySpawns, "AnomalySpawn_Right", center)
        };
        Transform anomalyLanes = GetOrCreate(area, "AnomalyLanes", center);
        Transform[][] anomalyLanePoints =
        {
            Lane(anomalyLanes, "Lane_Lower", center, 0f),
            Lane(anomalyLanes, "Lane_Upper", center, 0f)
        };

        Transform targetLanes = GetOrCreate(area, "TargetLanes", center);
        Transform[][] targetLanePoints =
        {
            Lane(targetLanes, "Lane_Left", center, 0f),
            Lane(targetLanes, "Lane_Center", center, 0f),
            Lane(targetLanes, "Lane_Right", center, 0f)
        };

        Transform runtime = GetOrCreate(area, "Runtime", center);
        Transform ballsRuntime = area.Find("Balls") ?? GetOrCreate(runtime, "Balls", center);
        Transform anomaliesRuntime = GetOrCreate(runtime, "Anomalies", center);
        Transform targetsRuntime = GetOrCreate(runtime, "Targets", center);
        Transform gatesRuntime = GetOrCreate(area, "Gates", center);
        target.transform.SetParent(targetsRuntime, true);
        target.name = "Target_01";

        BallRollVisual[] sceneBalls = Object.FindObjectsByType<BallRollVisual>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(item => item != null && item.gameObject.scene == scene)
            .Take(4)
            .ToArray();
        while (sceneBalls.Length < 4)
        {
            GameObject created = (GameObject)PrefabUtility.InstantiatePrefab(
                ballPrefab.gameObject, scene);
            created.transform.SetParent(ballsRuntime, true);
            created.SetActive(false);
            sceneBalls = sceneBalls.Append(created.GetComponent<BallRollVisual>()).ToArray();
        }
        for (int i = 0; i < sceneBalls.Length; i++)
        {
            sceneBalls[i].name = $"FootballBall_{i + 1:00}";
            sceneBalls[i].transform.SetParent(ballsRuntime, true);
        }

        FootballScoreZone[] targetPool = targetsRuntime
            .GetComponentsInChildren<FootballScoreZone>(true);
        while (targetPool.Length < 3)
        {
            FootballScoreZone created = Object.Instantiate(target, targetsRuntime);
            created.gameObject.SetActive(false);
            targetPool = targetPool.Append(created).ToArray();
        }
        for (int i = 0; i < targetPool.Length; i++)
            targetPool[i].name = $"Target_{i + 1:00}";

        FootballGateScoreZone[] gates = EnsureGates(
            scene, gatesRuntime, gatePrefab, minigame);

        FootballArenaLayout layout = area.GetComponent<FootballArenaLayout>();
        if (layout == null)
            layout = Undo.AddComponent<FootballArenaLayout>(area.gameObject);
        SerializedObject layoutData = new(layout);
        SetObject(layoutData, "arenaBounds", playArea);
        SetObject(layoutData, "ballsZone", ballsZone);
        SetObject(layoutData, "anomalyZone", anomaliesZone);
        SetObject(layoutData, "targetsZone", targetsZone);
        SetObject(layoutData, "ballsVisual", ballsVisual);
        SetObject(layoutData, "anomalyVisual", anomaliesVisual);
        SetObject(layoutData, "targetsVisual", targetsVisual);
        SetObject(layoutData, "playerTopBoundary", boundaryCollider);
        SetArray(layoutData, "ballSpawnPoints", ballPoints.Cast<Object>().ToArray());
        SetObject(layoutData, "startZone", area.Find("StartZone"));
        SetLanes(layoutData.FindProperty("anomalyLanes"), anomalyLanePoints, 1.1f);
        SetArray(layoutData, "anomalySpawnPoints", anomalyPoints.Cast<Object>().ToArray());
        SetLanes(layoutData.FindProperty("targetLanes"), targetLanePoints, 1.35f);
        SetObject(layoutData, "targetTemplate", target.transform);
        SetArray(layoutData, "gateRoots", gates
            .Take(2)
            .Select(item => (Object)(item.transform.parent != null
                ? item.transform.parent
                : item.transform))
            .ToArray());
        layoutData.FindProperty("ballsRatio").floatValue = 0.2f;
        layoutData.FindProperty("anomalyRatio").floatValue = 0.4f;
        layoutData.FindProperty("targetsRatio").floatValue = 0.4f;
        layoutData.FindProperty("playerBoundaryThickness").floatValue = 0.2f;
        layoutData.FindProperty("ballSpawnNormalizedY").floatValue = 0.5f;
        layoutData.FindProperty("horizontalPadding").floatValue = 0.5f;
        layoutData.FindProperty("anomalyVisualSize").vector2Value = new Vector2(4.5f, 3.2f);
        layoutData.FindProperty("targetMaximumRadius").floatValue = 1.08f;
        layoutData.FindProperty("targetGateReservedHeight").floatValue = 3.2f;
        layoutData.FindProperty("gateHorizontalOffset").floatValue = 6f;
        layoutData.FindProperty("gateVerticalInset").floatValue = 1.4f;
        layoutData.FindProperty("showGizmos").boolValue = false;
        layoutData.FindProperty("showLaneGizmos").boolValue = true;
        layoutData.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject data = new(minigame);
        SetObject(data, "arenaLayout", layout);
        SetObject(data, "ballSpawnZone", ballsZone);
        SetObject(data, "anomalySpawnZone", anomaliesZone);
        SetObject(data, "targetSpawnZone", targetsZone);
        SetObject(data, "arenaBounds", playArea);
        SetObject(data, "playerBoundary", playerBoundary);
        SetObject(data, "ballPrefab", ballPrefab);
        SetArray(data, "balls", sceneBalls.Cast<Object>().ToArray());
        SetObject(data, "ballsRuntime", ballsRuntime);
        SetObject(data, "anomaliesRuntime", anomaliesRuntime);
        SetObject(data, "targetsRuntime", targetsRuntime);
        SetObject(data, "gatePrefab", gatePrefab);
        SetObject(data, "gatesRuntime", gatesRuntime);
        SetObject(data, "cameraFollow", cameraFollow);
        SetArray(data, "ballSpawnPoints", ballPoints);
        SetObject(data, "gravityAnomalyPrefab", gravityPrefab);
        SetObject(data, "gravityAnomalyData", gravityData);
        SetArray(data, "anomalySpawnPoints", anomalyPoints);
        SetLanes(data.FindProperty("anomalyLanes"), anomalyLanePoints, 1.1f);
        SetObject(data, "targetTemplate", target);
        SetLanes(data.FindProperty("targetLanes"), targetLanePoints, 1.35f);
        data.FindProperty("initialBallCount").intValue = 4;
        data.FindProperty("activeAnomalyCount").intValue = 2;
        data.FindProperty("activeTargetCount").intValue = 3;
        data.FindProperty("anomalyForce").floatValue = 3.2f;
        data.FindProperty("anomalyFieldSize").vector2Value = new Vector2(4.5f, 3.2f);
        data.FindProperty("useRoundTimer").boolValue = true;
        data.FindProperty("roundDuration").floatValue = 60f;
        data.FindProperty("cameraPadding").floatValue = 1f;
        data.FindProperty("showDebugZones").boolValue = false;
        data.FindProperty("showLaneDebug").boolValue = false;
        data.FindProperty("topOutOfBoundsMargin").floatValue = 3f;
        data.FindProperty("targetBaseRadius").floatValue = 0.8f;
        data.FindProperty("gateVisualScale").floatValue = 0.55f;
        data.FindProperty("gateTriggerSize").vector2Value = new Vector2(3.6f, 1.8f);
        data.FindProperty("gateScore").intValue = 20;
        SetTargetSettings(data.FindProperty("greenTarget"),
            FootballScoreZoneType.Green, new Color(0.15f, 0.9f, 0.25f, 0.9f), 1.35f, 1.5f, 2);
        SetTargetSettings(data.FindProperty("yellowTarget"),
            FootballScoreZoneType.Yellow, new Color(1f, 0.82f, 0.08f, 0.92f), 1f, 3f, 5);
        SetTargetSettings(data.FindProperty("redTarget"),
            FootballScoreZoneType.Red, new Color(1f, 0.12f, 0.08f, 0.92f), 0.65f, 5.5f, 10);
        data.ApplyModifiedPropertiesWithoutUndo();
        layout.RefreshLayout();
        minigame.SynchronizeArenaGeometry();
        EditorUtility.SetDirty(layout);
        EditorUtility.SetDirty(minigame);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log($"[FootballV1.2Setup] SUCCESS: 20/40/40, {sceneBalls.Length} balls, " +
            $"{targetPool.Length} pooled targets and {gates.Length} gates wired.");
    }

    private static Transform GetOrCreate(Transform parent, string name, Vector3 worldPosition)
    {
        Transform result = parent.Find(name);
        if (result != null) return result;
        GameObject created = new(name);
        created.transform.SetParent(parent, true);
        created.transform.position = worldPosition;
        return created.transform;
    }

    private static FootballGateScoreZone[] EnsureGates(
        Scene scene,
        Transform parent,
        GameObject prefab,
        FootballMinigame minigame)
    {
        FootballGateScoreZone[] result = parent
            .GetComponentsInChildren<FootballGateScoreZone>(true);
        while (result.Length < 2)
        {
            int index = result.Length;
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            visual.transform.SetParent(parent, true);
            visual.name = index == 0 ? "Gate_Left" : "Gate_Right";
            Vector3 scale = visual.transform.localScale;
            visual.transform.localScale = new Vector3(scale.x * 0.55f, scale.y * 0.55f, scale.z);
            StabilizeGate(visual);

            GameObject trigger = new("ScoreTrigger");
            trigger.transform.SetParent(visual.transform, false);
            Undo.RegisterCreatedObjectUndo(trigger, "Create football gate trigger");
            trigger.AddComponent<BoxCollider2D>();
            FootballGateScoreZone scoreZone = trigger.AddComponent<FootballGateScoreZone>();
            result = result.Append(scoreZone).ToArray();
        }

        for (int i = 0; i < result.Length; i++)
        {
            Transform root = result[i].transform.parent;
            StabilizeGate(root.gameObject);
            result[i].Configure(minigame, 20, new Vector2(3.6f, 1.8f));
        }
        return result;
    }

    private static void StabilizeGate(GameObject gateRoot)
    {
        foreach (Rigidbody2D body in gateRoot.GetComponentsInChildren<Rigidbody2D>(true))
        {
            body.bodyType = RigidbodyType2D.Kinematic;
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
        }
    }

    private static BoxCollider2D ConfigureZone(Transform parent, string name, Vector3 position, Vector2 size)
    {
        Transform marker = GetOrCreate(parent, name, position);
        marker.position = position;
        BoxCollider2D collider = marker.GetComponent<BoxCollider2D>();
        if (collider == null)
            collider = Undo.AddComponent<BoxCollider2D>(marker.gameObject);
        collider.isTrigger = true;
        collider.size = size;
        return collider;
    }

    private static SpriteRenderer ConfigureZoneVisual(
        BoxCollider2D zone,
        Sprite sprite,
        Color color)
    {
        Transform root = GetOrCreate(zone.transform, "Visual", zone.transform.position);
        root.localPosition = Vector3.zero;
        root.localRotation = Quaternion.identity;
        root.localScale = Vector3.one;
        SpriteRenderer visual = root.GetComponent<SpriteRenderer>();
        if (visual == null)
            visual = Undo.AddComponent<SpriteRenderer>(root.gameObject);
        visual.sprite = sprite;
        visual.color = color;
        visual.drawMode = SpriteDrawMode.Sliced;
        visual.size = zone.size;
        visual.sortingOrder = -10;
        return visual;
    }

    private static Transform Marker(Transform parent, string name, Vector3 position)
    {
        Transform marker = GetOrCreate(parent, name, position);
        marker.position = position;
        return marker;
    }

    private static Transform[] Lane(Transform parent, string name, Vector3 center, float halfWidth)
    {
        Transform root = GetOrCreate(parent, name, center);
        return new[]
        {
            Marker(root, "Left", center + Vector3.left * halfWidth),
            Marker(root, "Right", center + Vector3.right * halfWidth)
        };
    }

    private static void SetObject(SerializedObject data, string name, Object value) =>
        data.FindProperty(name).objectReferenceValue = value;

    private static void SetArray(SerializedObject data, string name, Object[] values)
    {
        SerializedProperty property = data.FindProperty(name);
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    private static void SetLanes(SerializedProperty property, Transform[][] points, float speed)
    {
        property.arraySize = points.Length;
        for (int i = 0; i < points.Length; i++)
        {
            SerializedProperty lane = property.GetArrayElementAtIndex(i);
            lane.FindPropertyRelative("leftAnchor").objectReferenceValue = points[i][0];
            lane.FindPropertyRelative("rightAnchor").objectReferenceValue = points[i][1];
            lane.FindPropertyRelative("speed").floatValue = speed + i * 0.12f;
        }
    }

    private static void SetTargetSettings(
        SerializedProperty property,
        FootballScoreZoneType type,
        Color color,
        float scale,
        float speed,
        int score)
    {
        property.FindPropertyRelative("type").enumValueIndex = (int)type;
        property.FindPropertyRelative("color").colorValue = color;
        property.FindPropertyRelative("sizeScale").floatValue = scale;
        property.FindPropertyRelative("moveSpeed").floatValue = speed;
        property.FindPropertyRelative("score").intValue = score;
    }
}
