using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public static class SetupFootballMinigameScene
{
    private const string ScenePath = "Assets/_Project/Scenes/MainMenu.unity";
    private const string AreaName = "FootballMinigame_Area";
    private const string BallPrefabPath = "Assets/_Project/art/test/prefabs/Ball1.prefab";
    private const string GoalPrefabPath = "Assets/_Project/art/test/prefabs/FootBallGates1.prefab";
    private const string SessionAttemptKey = "Bunker.FootballMinigame.SetupAttempted";

    [InitializeOnLoadMethod]
    private static void QueueSingleSetupAttempt()
    {
        if (SessionState.GetBool(SessionAttemptKey, false))
            return;

        SessionState.SetBool(SessionAttemptKey, true);
        EditorApplication.delayCall += RunAutomaticSetup;
    }

    [MenuItem("Tools/Bunker/Setup Football Minigame")]
    public static void SetupFromMenu()
    {
        SetupAndSave();
    }

    private static void RunAutomaticSetup()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[FootballMinigameSetup] Setup cancelled: Editor is entering Play Mode.");
            return;
        }

        SetupAndSave();
    }

    private static void SetupAndSave()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            Debug.LogError(
                $"[FootballMinigameSetup] Expected active scene '{ScenePath}', " +
                $"but found '{scene.path}'. No changes were made.");
            return;
        }

        GameObject existingArea = FindSceneObject(scene, AreaName);
        if (existingArea != null)
        {
            ValidateSetup(existingArea, null, null);
            Debug.Log("[FootballMinigameSetup] Area already exists; scene was not modified.");
            return;
        }

        Transform player = FindPlayerTransform();
        if (player == null)
        {
            Debug.LogError("[FootballMinigameSetup] Player was not found by CharacterMovement2D, tag, or PlayerInteractor.");
            return;
        }

        BunkerStation referenceStation = FindReferenceStation(player.position);
        if (referenceStation == null)
        {
            Debug.LogError("[FootballMinigameSetup] No working BunkerStation with interaction collider was found.");
            return;
        }

        BunkerInteractableCollider referenceInteraction =
            referenceStation.GetComponentInChildren<BunkerInteractableCollider>(true);
        Collider2D referenceCollider = referenceInteraction != null
            ? referenceInteraction.GetComponent<Collider2D>()
            : null;

        if (referenceInteraction == null || referenceCollider == null)
        {
            Debug.LogError("[FootballMinigameSetup] Reference station interaction setup is incomplete.");
            return;
        }

        GameObject ballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BallPrefabPath);
        GameObject goalPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GoalPrefabPath);
        if (ballPrefab == null || goalPrefab == null)
        {
            Debug.LogError("[FootballMinigameSetup] Ball1 or FootBallGates1 prefab is missing.");
            return;
        }

        BallRollVisual ball = FindReusableBall(player.position);
        Vector3 areaCenter = ball != null
            ? ball.transform.position
            : player.position + Vector3.left * 10f;

        if (ball == null)
        {
            GameObject ballObject = (GameObject)PrefabUtility.InstantiatePrefab(ballPrefab, scene);
            ballObject.name = "Ball1";
            ballObject.transform.position = areaCenter;
            ball = ballObject.GetComponent<BallRollVisual>();
        }

        if (ball == null)
        {
            Debug.LogError("[FootballMinigameSetup] Selected Ball1 has no BallRollVisual.");
            return;
        }

        GameObject area = new(AreaName);
        Undo.RegisterCreatedObjectUndo(area, "Create Football Minigame Area");

        GameObject minigameObject = CreateChild(area.transform, "FootballMinigame", areaCenter);
        FootballMinigame minigame = minigameObject.AddComponent<FootballMinigame>();

        Transform spawnPoint = CreateChild(area.transform, "BallSpawnPoint", areaCenter).transform;
        Undo.SetTransformParent(ball.transform, area.transform, "Parent Football Ball");
        ball.transform.position = areaCenter;

        ConfigureMinigame(minigame, ball, spawnPoint);

        List<GameObject> goals = GetOrCreateGoals(scene, goalPrefab, areaCenter, area.transform);
        ConfigureGoals(goals, minigame);

        GameObject terminal = CreateTerminal(
            area.transform,
            areaCenter,
            player.position,
            minigame,
            referenceStation,
            referenceInteraction,
            referenceCollider);

        if (!ValidateSetup(area, referenceInteraction, referenceCollider))
        {
            Debug.LogError("[FootballMinigameSetup] Validation failed. Scene was not saved.");
            Undo.DestroyObjectImmediate(area);
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene))
        {
            Debug.LogError("[FootballMinigameSetup] MainMenu scene could not be saved.");
            return;
        }

        AssetDatabase.SaveAssets();
        Debug.Log(
            $"[FootballMinigameSetup] SETUP_SUCCESS scene={scene.path}; " +
            $"player={player.name}@{player.position}; areaCenter={areaCenter}; " +
            $"ball={ball.name}; goalPrefab={GoalPrefabPath}; " +
            $"referenceStation={referenceStation.name}; terminal={terminal.name}.");
    }

    private static Transform FindPlayerTransform()
    {
        CharacterMovement2D[] movementComponents =
            UnityEngine.Object.FindObjectsByType<CharacterMovement2D>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

        CharacterMovement2D activeMovement = movementComponents.FirstOrDefault(
            movement => movement != null && movement.gameObject.activeInHierarchy);
        if (activeMovement != null)
            return activeMovement.transform;

        try
        {
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null)
                return taggedPlayer.transform;
        }
        catch (UnityException)
        {
            // The fallback below still uses the existing player interaction component.
        }

        PlayerInteractor playerInteractor =
            UnityEngine.Object.FindFirstObjectByType<PlayerInteractor>(FindObjectsInactive.Include);
        return playerInteractor != null ? playerInteractor.transform : null;
    }

    private static BunkerStation FindReferenceStation(Vector3 playerPosition)
    {
        return UnityEngine.Object.FindObjectsByType<BunkerStation>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(station =>
                station != null &&
                station.gameObject.scene == SceneManager.GetActiveScene() &&
                station.GetComponentInChildren<BunkerInteractableCollider>(true) != null)
            .OrderBy(station => Vector3.Distance(playerPosition, station.transform.position))
            .FirstOrDefault();
    }

    private static BallRollVisual FindReusableBall(Vector3 playerPosition)
    {
        return UnityEngine.Object.FindObjectsByType<BallRollVisual>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(candidate =>
            {
                float distance = Vector3.Distance(playerPosition, candidate.transform.position);
                return candidate.gameObject.scene == SceneManager.GetActiveScene() &&
                       distance >= 6f && distance <= 14f;
            })
            .OrderBy(candidate => candidate.name == "Ball1" ? 0 : 1)
            .ThenBy(candidate => Vector3.Distance(playerPosition, candidate.transform.position))
            .FirstOrDefault();
    }

    private static List<GameObject> GetOrCreateGoals(
        Scene scene,
        GameObject goalPrefab,
        Vector3 center,
        Transform parent)
    {
        List<GameObject> nearbyGoals = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Select(transform => PrefabUtility.GetOutermostPrefabInstanceRoot(transform.gameObject))
            .Where(root => root != null && root.name.StartsWith("FootBallGates1", StringComparison.Ordinal))
            .Distinct()
            .Where(root =>
            {
                float distance = Vector3.Distance(center, root.transform.position);
                return distance >= 4f && distance <= 12f;
            })
            .OrderBy(root => Vector3.Distance(center, root.transform.position))
            .Take(3)
            .ToList();

        Vector3[] fallbackPositions =
        {
            center + Vector3.left * 7f,
            center + Vector3.right * 7f,
            center + Vector3.down * 7f
        };

        while (nearbyGoals.Count < 3)
        {
            int index = nearbyGoals.Count;
            GameObject goal = (GameObject)PrefabUtility.InstantiatePrefab(goalPrefab, scene);
            goal.transform.position = fallbackPositions[index];
            nearbyGoals.Add(goal);
        }

        GameObject bottom = nearbyGoals.OrderBy(goal => goal.transform.position.y).First();
        List<GameObject> sides = nearbyGoals.Where(goal => goal != bottom)
            .OrderBy(goal => goal.transform.position.x)
            .ToList();

        List<GameObject> ordered = new() { sides[0], sides[1], bottom };
        string[] names = { "GoalLeft", "GoalRight", "GoalBottom" };
        for (int i = 0; i < ordered.Count; i++)
        {
            Undo.SetTransformParent(ordered[i].transform, parent, "Parent Football Goal");
            ordered[i].name = names[i];
        }

        return ordered;
    }

    private static void ConfigureGoals(List<GameObject> goals, FootballMinigame minigame)
    {
        foreach (GameObject goalRoot in goals)
        {
            Collider2D trigger = goalRoot.GetComponentsInChildren<Collider2D>(true)
                .FirstOrDefault(collider => collider.isTrigger);

            if (trigger == null)
            {
                GameObject triggerObject = CreateChild(goalRoot.transform, "GoalTrigger", goalRoot.transform.position);
                BoxCollider2D box = triggerObject.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = new Vector2(1.7f, 0.5f);
                trigger = box;
            }

            FootballGoal footballGoal = trigger.GetComponent<FootballGoal>();
            if (footballGoal == null)
                footballGoal = trigger.gameObject.AddComponent<FootballGoal>();

            SerializedObject goalData = new(footballGoal);
            goalData.FindProperty("minigame").objectReferenceValue = minigame;
            goalData.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static GameObject CreateTerminal(
        Transform parent,
        Vector3 center,
        Vector3 playerPosition,
        FootballMinigame minigame,
        BunkerStation referenceStation,
        BunkerInteractableCollider referenceInteraction,
        Collider2D referenceCollider)
    {
        Vector3 directionToPlayer = (playerPosition - center).normalized;
        if (directionToPlayer.sqrMagnitude < 0.1f)
            directionToPlayer = Vector3.down;

        Vector3 perpendicular = new(-directionToPlayer.y, directionToPlayer.x, 0f);
        Vector3 terminalPosition = center + directionToPlayer * 4f + perpendicular * 2f;

        GameObject terminalObject = CreateChild(parent, "FootballChallengeTerminal", terminalPosition);
        terminalObject.layer = referenceStation.gameObject.layer;

        SpriteRenderer visual = terminalObject.AddComponent<SpriteRenderer>();
        visual.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        visual.drawMode = SpriteDrawMode.Sliced;
        visual.size = new Vector2(2f, 1.5f);
        visual.color = new Color(0.08f, 0.75f, 0.9f, 0.95f);
        visual.sortingOrder = 995;

        BunkerStation station = terminalObject.AddComponent<BunkerStation>();
        EditorUtility.CopySerialized(referenceStation, station);

        SerializedObject stationData = new(station);
        stationData.FindProperty("stationType").enumValueIndex = (int)BunkerStationType.CustomEvent;
        stationData.FindProperty("interactionText").stringValue = "Start Football Minigame";
        stationData.ApplyModifiedPropertiesWithoutUndo();

        BunkerMinigameTerminal terminal = terminalObject.AddComponent<BunkerMinigameTerminal>();
        SerializedObject terminalData = new(terminal);
        terminalData.FindProperty("minigame").objectReferenceValue = minigame;
        terminalData.ApplyModifiedPropertiesWithoutUndo();

        FieldInfo eventField = typeof(BunkerStation).GetField(
            "onInteract", BindingFlags.Instance | BindingFlags.NonPublic);
        UnityEvent onInteract = new();
        eventField?.SetValue(station, onInteract);
        UnityEventTools.AddPersistentListener(onInteract, terminal.Interact);
        onInteract.SetPersistentListenerState(0, UnityEventCallState.RuntimeOnly);
        EditorUtility.SetDirty(station);

        terminalObject.AddComponent<BunkerHoverOutline>();

        GameObject interactionObject = CreateChild(
            terminalObject.transform,
            referenceInteraction.gameObject.name,
            terminalObject.transform.position);
        interactionObject.transform.localPosition = referenceInteraction.transform.localPosition;
        interactionObject.transform.localRotation = referenceInteraction.transform.localRotation;
        interactionObject.transform.localScale = referenceInteraction.transform.localScale;
        interactionObject.layer = referenceInteraction.gameObject.layer;

        Collider2D terminalCollider =
            (Collider2D)interactionObject.AddComponent(referenceCollider.GetType());
        EditorUtility.CopySerialized(referenceCollider, terminalCollider);

        BunkerInteractableCollider interaction =
            interactionObject.AddComponent<BunkerInteractableCollider>();
        SerializedObject interactionData = new(interaction);
        interactionData.FindProperty("sourceRoot").objectReferenceValue = terminalObject;
        interactionData.ApplyModifiedPropertiesWithoutUndo();

        return terminalObject;
    }

    private static void ConfigureMinigame(
        FootballMinigame minigame,
        BallRollVisual ball,
        Transform spawnPoint)
    {
        SerializedObject minigameData = new(minigame);
        minigameData.FindProperty("ball").objectReferenceValue = ball;
        minigameData.FindProperty("ballSpawnPoint").objectReferenceValue = spawnPoint;
        minigameData.FindProperty("goalsToComplete").intValue = 3;
        minigameData.ApplyModifiedPropertiesWithoutUndo();
    }

    private static bool ValidateSetup(
        GameObject area,
        BunkerInteractableCollider referenceInteraction,
        Collider2D referenceCollider)
    {
        if (area == null || area.scene.path != ScenePath)
            return false;

        FootballMinigame minigame = area.GetComponentInChildren<FootballMinigame>(true);
        if (minigame == null || minigame.Ball == null || minigame.GoalsToComplete != 3)
            return false;

        SerializedObject minigameData = new(minigame);
        if (minigameData.FindProperty("ballSpawnPoint").objectReferenceValue == null)
            return false;

        FootballGoal[] goals = area.GetComponentsInChildren<FootballGoal>(true);
        if (goals.Length != 3)
            return false;

        foreach (FootballGoal goal in goals)
        {
            SerializedObject goalData = new(goal);
            if (goalData.FindProperty("minigame").objectReferenceValue != minigame)
                return false;
        }

        Transform spawnPoint = area.transform.Find("BallSpawnPoint");
        if (spawnPoint == null || Vector3.Distance(minigame.Ball.transform.position, spawnPoint.position) > 0.01f)
            return false;

        Transform terminalTransform = area.transform.Find("FootballChallengeTerminal");
        if (terminalTransform == null)
            return false;

        BunkerStation station = terminalTransform.GetComponent<BunkerStation>();
        BunkerMinigameTerminal terminal = terminalTransform.GetComponent<BunkerMinigameTerminal>();
        BunkerInteractableCollider interaction =
            terminalTransform.GetComponentInChildren<BunkerInteractableCollider>(true);
        Collider2D collider = interaction != null ? interaction.GetComponent<Collider2D>() : null;

        if (station == null || terminal == null || interaction == null || collider == null)
            return false;

        SerializedObject stationData = new(station);
        if (stationData.FindProperty("stationType").enumValueIndex != (int)BunkerStationType.CustomEvent)
            return false;

        FieldInfo eventField = typeof(BunkerStation).GetField(
            "onInteract", BindingFlags.Instance | BindingFlags.NonPublic);
        UnityEvent onInteract = eventField?.GetValue(station) as UnityEvent;
        if (onInteract == null || onInteract.GetPersistentEventCount() != 1 ||
            onInteract.GetPersistentTarget(0) != terminal ||
            onInteract.GetPersistentMethodName(0) != nameof(BunkerMinigameTerminal.Interact))
        {
            return false;
        }

        SerializedObject terminalData = new(terminal);
        if (terminalData.FindProperty("minigame").objectReferenceValue != minigame)
            return false;

        SerializedObject interactionData = new(interaction);
        if (interactionData.FindProperty("sourceRoot").objectReferenceValue != terminalTransform.gameObject)
            return false;

        if (referenceInteraction != null && referenceCollider != null)
        {
            if (interaction.gameObject.layer != referenceInteraction.gameObject.layer ||
                collider.GetType() != referenceCollider.GetType() ||
                collider.isTrigger != referenceCollider.isTrigger)
            {
                return false;
            }
        }

        if (area.GetComponentInChildren<BunkerGoalTrigger>(true) != null ||
            area.GetComponentInChildren<BunkerKickableBall>(true) != null)
        {
            return false;
        }

        Debug.Log("[FootballMinigameSetup] Validation passed: all required references are configured.");
        return true;
    }

    private static GameObject CreateChild(Transform parent, string name, Vector3 worldPosition)
    {
        GameObject child = new(name);
        Undo.RegisterCreatedObjectUndo(child, $"Create {name}");
        child.transform.SetParent(parent, true);
        child.transform.position = worldPosition;
        return child;
    }

    private static GameObject FindSceneObject(Scene scene, string objectName)
    {
        return scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .FirstOrDefault(transform => transform.name == objectName)
            ?.gameObject;
    }
}
