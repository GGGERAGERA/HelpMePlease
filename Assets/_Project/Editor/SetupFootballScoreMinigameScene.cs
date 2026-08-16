using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class SetupFootballScoreMinigameScene
{
    private const string ScenePath = "Assets/_Project/Scenes/MainMenu.unity";
    private const string AreaName = "FootballMinigame_Area";
    private const string BallPrefabPath = "Assets/_Project/art/test/prefabs/Ball1.prefab";
    private const string SessionKey = "Bunker.Football.ScoreMinigameSetup.2";

    private static void QueueAutomaticSetup()
    {
        EditorApplication.delayCall += TryAutomaticSetup;
    }

    [MenuItem("Tools/Bunker/Setup Football Score Minigame")]
    public static void SetupFromMenu()
    {
        SetupBunkerFootballV1Scene.SetupFromMenu();
    }

    private static void TryAutomaticSetup()
    {
        if (SessionState.GetBool(SessionKey, false))
            return;

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            if (EditorApplication.isPlaying)
                EditorApplication.ExitPlaymode();
            return;
        }

        SessionState.SetBool(SessionKey, true);
        SetupAndSave();
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredEditMode)
            return;

        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.delayCall += TryAutomaticSetup;
    }

    private static void SetupAndSave()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            Debug.LogError($"[FootballScoreSetup] Expected active scene '{ScenePath}'.");
            return;
        }

        GameObject area = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(item => item.name == AreaName)
            ?.gameObject;
        FootballMinigame minigame = area != null
            ? area.GetComponentInChildren<FootballMinigame>(true)
            : null;
        GameObject ballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BallPrefabPath);

        if (area == null || minigame == null || ballPrefab == null)
        {
            Debug.LogError("[FootballScoreSetup] Existing area, FootballMinigame, or Ball1 prefab is missing.");
            return;
        }

        Vector3 center = minigame.transform.position;
        Transform player = FindPlayer();

        FootballStartZone startZone = ConfigureStartZone(area.transform, minigame);
        BoxCollider2D playArea = ConfigurePlayArea(area.transform, center);
        FootballScoreZone scoreZone = ConfigureScoreZone(area.transform, center, minigame);
        BallRollVisual[] balls = ConfigureBalls(area.transform, center, ballPrefab, scene);
        FootballMinigameHUD hud = ConfigureHud(area.transform, center);

        ConfigureMinigame(minigame, balls, playArea, scoreZone, startZone, hud, player);

        foreach (FootballGoal oldGoal in area.GetComponentsInChildren<FootballGoal>(true))
            Undo.DestroyObjectImmediate(oldGoal);

        Transform legacySpawn = area.transform.Find("BallSpawnPoint");
        if (legacySpawn != null)
            Undo.DestroyObjectImmediate(legacySpawn.gameObject);

        if (!Validate(area, minigame, startZone, scoreZone, playArea, balls, hud))
        {
            Debug.LogError("[FootballScoreSetup] Validation failed; scene was not saved.");
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        Debug.Log(
            $"[FootballScoreSetup] SUCCESS balls={balls.Length}; duration=60; " +
            $"radii=1.6/1.1/0.65; weights=50/35/15; sceneSaved={scene.path}.");
    }

    private static FootballStartZone ConfigureStartZone(Transform area, FootballMinigame minigame)
    {
        Transform startTransform = area.Find("StartZone") ?? area.Find("FootballChallengeTerminal");
        if (startTransform == null)
        {
            Debug.LogError("[FootballScoreSetup] Existing football terminal was not found.");
            return null;
        }

        startTransform.name = "StartZone";
        BunkerStation station = startTransform.GetComponent<BunkerStation>();
        BunkerMinigameTerminal terminal = startTransform.GetComponent<BunkerMinigameTerminal>();
        FootballStartZone startZone = startTransform.GetComponent<FootballStartZone>();
        if (startZone == null)
            startZone = Undo.AddComponent<FootballStartZone>(startTransform.gameObject);

        Transform labelTransform = startTransform.Find("StartLabel");
        TextMeshPro label;
        if (labelTransform == null)
        {
            GameObject labelObject = new("StartLabel", typeof(RectTransform), typeof(TextMeshPro));
            Undo.RegisterCreatedObjectUndo(labelObject, "Create Football Start Label");
            labelObject.transform.SetParent(startTransform, false);
            labelTransform = labelObject.transform;
            label = labelObject.GetComponent<TextMeshPro>();
        }
        else
        {
            label = labelTransform.GetComponent<TextMeshPro>();
        }

        RectTransform labelRect = (RectTransform)labelTransform;
        labelRect.localPosition = new Vector3(0f, 3.1f, 0f);
        labelRect.localScale = Vector3.one * 0.12f;
        labelRect.sizeDelta = new Vector2(20f, 5f);
        label.text = "START";
        label.fontSize = 24f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(0.2f, 1f, 0.45f, 1f);
        label.fontStyle = FontStyles.Bold;
        label.sortingOrder = 1100;

        SpriteRenderer[] renderers = startTransform
            .GetComponentsInChildren<SpriteRenderer>(true)
            .Where(renderer => renderer.transform != startTransform)
            .ToArray();

        SerializedObject startData = new(startZone);
        startData.FindProperty("minigame").objectReferenceValue = minigame;
        startData.FindProperty("terminal").objectReferenceValue = terminal;
        SerializedProperty rendererList = startData.FindProperty("visualRenderers");
        rendererList.arraySize = renderers.Length;
        for (int i = 0; i < renderers.Length; i++)
            rendererList.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
        startData.FindProperty("startText").objectReferenceValue = label;
        startData.ApplyModifiedPropertiesWithoutUndo();

        if (station != null)
        {
            SerializedObject stationData = new(station);
            stationData.FindProperty("stationType").enumValueIndex = (int)BunkerStationType.CustomEvent;
            stationData.FindProperty("interactionText").stringValue = "START FOOTBALL";
            stationData.FindProperty("progressionEnabled").boolValue = false;
            stationData.ApplyModifiedPropertiesWithoutUndo();

            FieldInfo eventField = typeof(BunkerStation).GetField(
                "onInteract", BindingFlags.Instance | BindingFlags.NonPublic);
            UnityEvent onInteract = eventField?.GetValue(station) as UnityEvent;
            if (onInteract != null)
            {
                while (onInteract.GetPersistentEventCount() > 0)
                    UnityEventTools.RemovePersistentListener(onInteract, 0);
                UnityEventTools.AddVoidPersistentListener(onInteract, startZone.Interact);
                EditorUtility.SetDirty(station);
            }
        }

        return startZone;
    }

    private static BoxCollider2D ConfigurePlayArea(Transform area, Vector3 center)
    {
        Transform existing = area.Find("PlayAreaBounds");
        GameObject boundsObject = existing != null ? existing.gameObject : new GameObject("PlayAreaBounds");
        if (existing == null)
        {
            Undo.RegisterCreatedObjectUndo(boundsObject, "Create Football Play Area");
            boundsObject.transform.SetParent(area, true);
        }

        boundsObject.transform.position = center;
        BoxCollider2D collider = boundsObject.GetComponent<BoxCollider2D>();
        if (collider == null)
            collider = Undo.AddComponent<BoxCollider2D>(boundsObject);
        collider.isTrigger = true;
        collider.size = new Vector2(18f, 12f);
        return collider;
    }

    private static FootballScoreZone ConfigureScoreZone(
        Transform area,
        Vector3 center,
        FootballMinigame minigame)
    {
        Transform existing = area.Find("ScoreZone");
        GameObject zoneObject = existing != null ? existing.gameObject : new GameObject("ScoreZone");
        if (existing == null)
        {
            Undo.RegisterCreatedObjectUndo(zoneObject, "Create Football Score Zone");
            zoneObject.transform.SetParent(area, true);
        }

        zoneObject.transform.position = center;
        CircleCollider2D collider = zoneObject.GetComponent<CircleCollider2D>();
        if (collider == null)
            collider = Undo.AddComponent<CircleCollider2D>(zoneObject);
        collider.isTrigger = true;

        SpriteRenderer renderer = zoneObject.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = Undo.AddComponent<SpriteRenderer>(zoneObject);
        renderer.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");
        renderer.drawMode = SpriteDrawMode.Sliced;
        renderer.size = Vector2.one * 3.2f;
        renderer.color = new Color(0.12f, 1f, 0.3f, 0.78f);
        renderer.sortingOrder = 1005;

        FootballScoreZone zone = zoneObject.GetComponent<FootballScoreZone>();
        if (zone == null)
            zone = Undo.AddComponent<FootballScoreZone>(zoneObject);
        SerializedObject zoneData = new(zone);
        zoneData.FindProperty("minigame").objectReferenceValue = minigame;
        zoneData.ApplyModifiedPropertiesWithoutUndo();
        renderer.enabled = false;
        collider.enabled = false;
        return zone;
    }

    private static BallRollVisual[] ConfigureBalls(
        Transform area,
        Vector3 center,
        GameObject ballPrefab,
        Scene scene)
    {
        Transform ballsRoot = area.Find("Balls");
        if (ballsRoot == null)
        {
            GameObject root = new("Balls");
            Undo.RegisterCreatedObjectUndo(root, "Create Football Balls Root");
            root.transform.SetParent(area, false);
            ballsRoot = root.transform;
        }

        BallRollVisual[] existingBalls = area.GetComponentsInChildren<BallRollVisual>(true);
        foreach (BallRollVisual existingBall in existingBalls)
            Undo.SetTransformParent(existingBall.transform, ballsRoot, "Parent Football Ball");

        while (ballsRoot.GetComponentsInChildren<BallRollVisual>(true).Length < 4)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(ballPrefab, scene);
            Undo.RegisterCreatedObjectUndo(instance, "Create Football Ball");
            Undo.SetTransformParent(instance.transform, ballsRoot, "Parent Football Ball");
        }

        BallRollVisual[] balls = ballsRoot.GetComponentsInChildren<BallRollVisual>(true)
            .Take(4)
            .ToArray();
        Vector2[] offsets =
        {
            new(-4f, -2.4f),
            new(4f, -2.4f),
            new(-4f, 2.4f),
            new(4f, 2.4f)
        };

        for (int i = 0; i < balls.Length; i++)
        {
            balls[i].name = $"Ball{i + 1}";
            balls[i].transform.position = center + (Vector3)offsets[i];
        }

        return balls;
    }

    private static FootballMinigameHUD ConfigureHud(Transform area, Vector3 center)
    {
        Transform existing = area.Find("HUD");
        GameObject hudObject;
        if (existing == null)
        {
            hudObject = new GameObject("HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(Image));
            Undo.RegisterCreatedObjectUndo(hudObject, "Create Football HUD");
            hudObject.transform.SetParent(area, true);
        }
        else
        {
            hudObject = existing.gameObject;
        }

        RectTransform rect = hudObject.GetComponent<RectTransform>();
        rect.position = center + Vector3.up * 7.2f;
        rect.localScale = Vector3.one * 0.012f;
        rect.sizeDelta = new Vector2(800f, 180f);

        Canvas canvas = hudObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 1200;

        Image background = hudObject.GetComponent<Image>();
        background.color = new Color(0.025f, 0.04f, 0.07f, 0.88f);
        background.raycastTarget = false;

        FootballMinigameHUD hud = hudObject.GetComponent<FootballMinigameHUD>();
        if (hud == null)
            hud = Undo.AddComponent<FootballMinigameHUD>(hudObject);

        TextMeshProUGUI time = CreateHudText(rect, "Time", new Vector2(-260f, 30f), new Vector2(250f, 70f));
        TextMeshProUGUI score = CreateHudText(rect, "Score", new Vector2(0f, 30f), new Vector2(250f, 70f));
        TextMeshProUGUI best = CreateHudText(rect, "Best", new Vector2(260f, 30f), new Vector2(250f, 70f));
        TextMeshProUGUI result = CreateHudText(rect, "Result", new Vector2(0f, -48f), new Vector2(700f, 60f));
        result.color = new Color(1f, 0.85f, 0.2f, 1f);

        SerializedObject hudData = new(hud);
        hudData.FindProperty("timeText").objectReferenceValue = time;
        hudData.FindProperty("scoreText").objectReferenceValue = score;
        hudData.FindProperty("bestScoreText").objectReferenceValue = best;
        hudData.FindProperty("resultText").objectReferenceValue = result;
        hudData.ApplyModifiedPropertiesWithoutUndo();
        return hud;
    }

    private static TextMeshProUGUI CreateHudText(
        RectTransform parent,
        string name,
        Vector2 position,
        Vector2 size)
    {
        Transform existing = parent.Find(name);
        TextMeshProUGUI text;
        if (existing == null)
        {
            GameObject textObject = new(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(textObject, $"Create Football HUD {name}");
            textObject.transform.SetParent(parent, false);
            text = textObject.GetComponent<TextMeshProUGUI>();
        }
        else
        {
            text = existing.GetComponent<TextMeshProUGUI>();
        }

        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        text.text = name.ToUpperInvariant();
        text.fontSize = 34f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private static void ConfigureMinigame(
        FootballMinigame minigame,
        BallRollVisual[] balls,
        Collider2D playArea,
        FootballScoreZone scoreZone,
        FootballStartZone startZone,
        FootballMinigameHUD hud,
        Transform player)
    {
        SerializedObject data = new(minigame);
        SerializedProperty ballList = data.FindProperty("balls");
        ballList.arraySize = balls.Length;
        for (int i = 0; i < balls.Length; i++)
            ballList.GetArrayElementAtIndex(i).objectReferenceValue = balls[i];
        data.FindProperty("playAreaBounds").objectReferenceValue = playArea;
        data.FindProperty("scoreZone").objectReferenceValue = scoreZone;
        data.FindProperty("startZone").objectReferenceValue = startZone;
        data.FindProperty("hud").objectReferenceValue = hud;
        data.FindProperty("player").objectReferenceValue = player;
        data.FindProperty("roundDuration").floatValue = 60f;
        data.FindProperty("zoneRespawnDelay").floatValue = 0.3f;
        data.FindProperty("greenRadius").floatValue = 1.6f;
        data.FindProperty("blueRadius").floatValue = 1.1f;
        data.FindProperty("redRadius").floatValue = 0.65f;
        data.FindProperty("greenPoints").intValue = 3;
        data.FindProperty("bluePoints").intValue = 6;
        data.FindProperty("redPoints").intValue = 10;
        data.FindProperty("greenWeight").floatValue = 50f;
        data.FindProperty("blueWeight").floatValue = 35f;
        data.FindProperty("redWeight").floatValue = 15f;
        data.FindProperty("spawnMargin").floatValue = 1.5f;
        data.FindProperty("minDistanceFromPlayer").floatValue = 3f;
        data.FindProperty("minDistanceFromBalls").floatValue = 2.5f;
        data.FindProperty("spawnAttempts").intValue = 20;
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform FindPlayer()
    {
        CharacterMovement2D movement = Object.FindObjectsByType<CharacterMovement2D>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(candidate => candidate.gameObject.activeInHierarchy);
        return movement != null ? movement.transform : null;
    }

    private static bool Validate(
        GameObject area,
        FootballMinigame minigame,
        FootballStartZone startZone,
        FootballScoreZone scoreZone,
        BoxCollider2D playArea,
        BallRollVisual[] balls,
        FootballMinigameHUD hud)
    {
        return area != null &&
               minigame != null &&
               startZone != null &&
               scoreZone != null &&
               playArea != null && playArea.isTrigger &&
               balls.Length == 4 && balls.All(ball => ball != null) &&
               hud != null &&
               area.GetComponentsInChildren<FootballGoal>(true).Length == 0 &&
               area.transform.Find("StartZone") != null &&
               area.transform.Find("ScoreZone") != null &&
               area.transform.Find("PlayAreaBounds") != null &&
               area.transform.Find("Balls") != null &&
               area.transform.Find("HUD") != null;
    }
}
