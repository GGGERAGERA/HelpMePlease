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
    private const string SessionKey = "Bunker.Football.V1.SceneSetup.4";

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
                .FirstOrDefault(item => item.name == "PlayAreaBounds") : null;
        GravityZone gravityPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GravityPrefabPath)
            ?.GetComponent<GravityZone>();
        LocalAnomalyData gravityData = AssetDatabase.LoadAssetAtPath<LocalAnomalyData>(GravityDataPath);

        if (area == null || minigame == null || target == null || playArea == null ||
            gravityPrefab == null || gravityData == null)
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
            if (root != area.gameObject &&
                root.GetComponent<FootballMinigame>() != null)
            {
                Undo.DestroyObjectImmediate(root);
            }
        }

        Vector3 center = playArea.transform.position;
        playArea.size = new Vector2(24f, 28f);

        Transform zones = GetOrCreate(area, "Zones", center);
        BoxCollider2D ballsZone = ConfigureZone(zones, "Zone_Balls", center + Vector3.down * 9.5f, new Vector2(22f, 7f));
        BoxCollider2D anomaliesZone = ConfigureZone(zones, "Zone_Anomalies", center, new Vector2(22f, 7f));
        BoxCollider2D targetsZone = ConfigureZone(zones, "Zone_Targets", center + Vector3.up * 9.5f, new Vector2(22f, 7f));

        Transform ballSpawns = GetOrCreate(area, "BallSpawns", center);
        Transform[] ballPoints =
        {
            Marker(ballSpawns, "BallSpawn_01", center + new Vector3(-5f, -10f)),
            Marker(ballSpawns, "BallSpawn_02", center + new Vector3(-1.7f, -10f)),
            Marker(ballSpawns, "BallSpawn_03", center + new Vector3(1.7f, -10f)),
            Marker(ballSpawns, "BallSpawn_04", center + new Vector3(5f, -10f))
        };

        Transform anomalySpawns = GetOrCreate(area, "AnomalySpawns", center);
        Transform[] anomalyPoints =
        {
            Marker(anomalySpawns, "AnomalySpawn_Left", center + Vector3.left * 5f),
            Marker(anomalySpawns, "AnomalySpawn_Right", center + Vector3.right * 5f)
        };
        Transform anomalyLanes = GetOrCreate(area, "AnomalyLanes", center);
        Transform[][] anomalyLanePoints =
        {
            Lane(anomalyLanes, "Lane_Lower", center + Vector3.down * 1.7f, 8f),
            Lane(anomalyLanes, "Lane_Upper", center + Vector3.up * 1.7f, 8f)
        };

        Transform targetLanes = GetOrCreate(area, "TargetLanes", center);
        Transform[][] targetLanePoints =
        {
            Lane(targetLanes, "Lane_Left", center + new Vector3(0f, 7.6f), 8f),
            Lane(targetLanes, "Lane_Center", center + new Vector3(0f, 9.5f), 8f),
            Lane(targetLanes, "Lane_Right", center + new Vector3(0f, 11.4f), 8f)
        };

        Transform runtime = GetOrCreate(area, "Runtime", center);
        Transform ballsRuntime = area.Find("Balls") ?? GetOrCreate(runtime, "Balls", center);
        Transform anomaliesRuntime = GetOrCreate(runtime, "Anomalies", center);
        Transform targetsRuntime = GetOrCreate(runtime, "Targets", center);
        target.transform.SetParent(targetsRuntime, true);
        target.name = "Target_01";

        SerializedObject data = new(minigame);
        SetObject(data, "ballSpawnZone", ballsZone);
        SetObject(data, "anomalySpawnZone", anomaliesZone);
        SetObject(data, "targetSpawnZone", targetsZone);
        SetObject(data, "playAreaBounds", playArea);
        SetObject(data, "ballsRuntime", ballsRuntime);
        SetObject(data, "anomaliesRuntime", anomaliesRuntime);
        SetObject(data, "targetsRuntime", targetsRuntime);
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
        data.FindProperty("useRoundTimer").boolValue = false;
        data.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log("[FootballV1Setup] SUCCESS: manual zones, spawn markers, lanes and runtime roots wired.");
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
}
