using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class Subject42DebugMenu : MonoBehaviour
{
    [Header("Existing scene systems")]
    [SerializeField] private WorldRuleController worldRuleController;
    [SerializeField] private LevelAnomalyController anomalyController;
    [SerializeField] private WorldEventSpawner worldEventSpawner;
    [SerializeField] private EnemySpawner enemySpawner;

    [Header("Known project content")]
    [SerializeField] private WorldRuleData[] worldRules;
    [SerializeField] private LocalAnomalyData[] localAnomalies;
    [SerializeField] private WorldEvent[] worldEventPrefabs;
    [SerializeField] private GameObject turretEnemyPrefab;
    [SerializeField] private GameObject eyesEnemyPrefab;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private static readonly WorldRuleType[] DebugRuleTypes =
    {
        WorldRuleType.Snow,
        WorldRuleType.Rain,
        WorldRuleType.Darkness,
        WorldRuleType.Wind,
        WorldRuleType.Golden,
        WorldRuleType.Condensation
    };

    private static readonly LocalAnomalyType[] DebugAnomalyTypes =
    {
        LocalAnomalyType.Berserk,
        LocalAnomalyType.Stasis,
        LocalAnomalyType.ExplosiveZone,
        LocalAnomalyType.Gravity,
        LocalAnomalyType.Glitch
    };

    private GameObject menuRoot;
    private RectTransform contentRoot;
    private bool isOpen;
    private float previousTimeScale;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;
    private bool warnedRuleController;
    private bool warnedAnomalyController;
    private bool warnedEventSpawner;
    private readonly List<LevelAnomalyController.LocalAnomalyZoneGeometry>
        activeAnomalyZones = new();
    private readonly List<LocalAnomalyType> activeAnomalyTypes = new();
    private readonly List<int> activeAnomalyTypeCounts = new();
    private readonly StringBuilder activeAnomalySummary = new();
    private readonly List<WorldEvent> addedEventPrefabs = new();
    private readonly List<GameObject> debugEnemies = new();

    private readonly Color panelColor = new(0.035f, 0.045f, 0.06f, 0.97f);
    private readonly Color rowColor = new(0.09f, 0.11f, 0.145f, 0.95f);
    private readonly Color accentColor = new(0.13f, 0.58f, 0.72f, 1f);
    private readonly Color mutedColor = new(0.65f, 0.69f, 0.74f, 1f);
    private readonly Color successColor = new(0.36f, 0.82f, 0.48f, 1f);
    private readonly Color warningColor = new(1f, 0.69f, 0.25f, 1f);

    private void Awake()
    {
        ResolveSceneReferences();
        BuildMenu();
        RefreshData();
        menuRoot.SetActive(false);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
            SetOpen(!isOpen);
    }

    private void OnDisable()
    {
        if (isOpen)
            CloseMenu();
    }

    private void OnDestroy()
    {
        if (isOpen)
            RestoreGameState();

        if (menuRoot != null)
            Destroy(menuRoot);
    }

    private void SetOpen(bool open)
    {
        if (open)
            OpenMenu();
        else
            CloseMenu();
    }

    private void OpenMenu()
    {
        if (isOpen)
            return;

        ResolveSceneReferences();
        RefreshData();

        previousTimeScale = Time.timeScale;
        previousCursorVisible = Cursor.visible;
        previousCursorLockMode = Cursor.lockState;

        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        isOpen = true;
        menuRoot.SetActive(true);
    }

    private void CloseMenu()
    {
        if (!isOpen)
            return;

        menuRoot.SetActive(false);
        RestoreGameState();
        isOpen = false;
    }

    private void RestoreGameState()
    {
        Time.timeScale = previousTimeScale;
        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockMode;
    }

    private void ResolveSceneReferences()
    {
        if (worldRuleController == null)
            worldRuleController = FindFirstObjectByType<WorldRuleController>();

        if (anomalyController == null)
            anomalyController = FindFirstObjectByType<LevelAnomalyController>();

        if (worldEventSpawner == null)
            worldEventSpawner = FindFirstObjectByType<WorldEventSpawner>();

        if (enemySpawner == null)
            enemySpawner = FindFirstObjectByType<EnemySpawner>();

        WarnIfMissing(
            worldRuleController,
            ref warnedRuleController,
            "WorldRuleController"
        );
        WarnIfMissing(
            anomalyController,
            ref warnedAnomalyController,
            "LevelAnomalyController"
        );
        WarnIfMissing(
            worldEventSpawner,
            ref warnedEventSpawner,
            "WorldEventSpawner"
        );
    }

    private void WarnIfMissing(
        UnityEngine.Object target,
        ref bool wasWarned,
        string systemName)
    {
        if (target != null || wasWarned)
            return;

        wasWarned = true;
        Debug.LogWarning(
            $"[Subject42DebugMenu] {systemName} was not found. " +
            "The related section is diagnostics-only until it is available.",
            this
        );
    }

    private void BuildMenu()
    {
        menuRoot = new GameObject(
            "Subject42 Debug Menu (Runtime)",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );

        Canvas canvas = menuRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        CanvasScaler scaler = menuRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform blocker = CreateRect("Input Blocker", menuRoot.transform);
        Stretch(blocker);
        Image blockerImage = blocker.gameObject.AddComponent<Image>();
        blockerImage.color = new Color(0f, 0f, 0f, 0.68f);
        blockerImage.raycastTarget = true;

        RectTransform panel = CreateRect("Panel", blocker);
        panel.anchorMin = new Vector2(0.1f, 0.06f);
        panel.anchorMax = new Vector2(0.9f, 0.94f);
        panel.offsetMin = Vector2.zero;
        panel.offsetMax = Vector2.zero;
        panel.gameObject.AddComponent<Image>().color = panelColor;

        RectTransform header = CreateRect("Header", panel);
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = Vector2.one;
        header.pivot = new Vector2(0.5f, 1f);
        header.sizeDelta = new Vector2(0f, 72f);
        header.anchoredPosition = Vector2.zero;

        TextMeshProUGUI title = CreateText(
            "Title",
            header,
            "SUBJECT#42  —  DEBUG MENU",
            30f,
            TextAlignmentOptions.MidlineLeft,
            Color.white
        );
        RectTransform titleRect = title.rectTransform;
        titleRect.anchorMin = Vector2.zero;
        titleRect.anchorMax = Vector2.one;
        titleRect.offsetMin = new Vector2(24f, 0f);
        titleRect.offsetMax = new Vector2(-90f, 0f);

        Button closeButton = CreateButton(header, "X", CloseMenu, 54f);
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 0.5f);
        closeRect.pivot = new Vector2(1f, 0.5f);
        closeRect.anchoredPosition = new Vector2(-16f, 0f);
        closeRect.sizeDelta = new Vector2(54f, 44f);

        RectTransform scrollRectTransform = CreateRect("Scroll View", panel);
        scrollRectTransform.anchorMin = Vector2.zero;
        scrollRectTransform.anchorMax = Vector2.one;
        scrollRectTransform.offsetMin = new Vector2(18f, 18f);
        scrollRectTransform.offsetMax = new Vector2(-18f, -72f);

        ScrollRect scrollRect = scrollRectTransform.gameObject.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 34f;

        RectTransform viewport = CreateRect("Viewport", scrollRectTransform);
        Stretch(viewport);
        viewport.gameObject.AddComponent<Image>().color =
            new Color(0f, 0f, 0f, 0.12f);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        scrollRect.viewport = viewport;

        contentRoot = CreateRect("Content", viewport);
        contentRoot.anchorMin = new Vector2(0f, 1f);
        contentRoot.anchorMax = Vector2.one;
        contentRoot.pivot = new Vector2(0.5f, 1f);
        contentRoot.offsetMin = new Vector2(10f, 0f);
        contentRoot.offsetMax = new Vector2(-10f, 0f);

        VerticalLayoutGroup layout =
            contentRoot.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 16);
        layout.spacing = 7f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter =
            contentRoot.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scrollRect.content = contentRoot;
    }

    private void RefreshData()
    {
        if (contentRoot == null)
            return;

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        AddWorldRulesSection();
        AddLocalAnomaliesSection();
        AddWorldEventsSection();
        AddEnemiesSection();
        AddHint("F1 toggles this menu. Gameplay remains paused while it is open.");
    }

    private void AddWorldRulesSection()
    {
        AddSectionTitle("WORLD RULES", "Apply/Clear through WorldRuleController");

        AddRow(
            "None / Clear",
            worldRuleController == null
                ? "CONTROLLER NOT FOUND"
                : worldRuleController.ActiveRule == null
                    ? "ACTIVE"
                    : "AVAILABLE",
            worldRuleController == null ? warningColor : successColor,
            "CLEAR",
            worldRuleController != null,
            ClearWorldRule
        );

        for (int i = 0; i < DebugRuleTypes.Length; i++)
        {
            WorldRuleType type = DebugRuleTypes[i];
            WorldRuleData data = worldRules != null && i < worldRules.Length
                ? worldRules[i]
                : null;
            string status;
            Color statusColor;
            bool canApply = worldRuleController != null && data != null &&
                data.RuleType == type;

            if (data == null)
            {
                status = "MISSING";
                statusColor = warningColor;
            }
            else if (data.RuleType != type)
            {
                status = "NOT CONFIGURED";
                statusColor = warningColor;
            }
            else if (worldRuleController == null)
            {
                status = "CONTROLLER NOT FOUND";
                statusColor = warningColor;
            }
            else if (worldRuleController.ActiveRule != null &&
                worldRuleController.ActiveRule.RuleType == type)
            {
                status = "ACTIVE";
                statusColor = successColor;
            }
            else
            {
                status = "AVAILABLE";
                statusColor = mutedColor;
            }

            WorldRuleData capturedData = data;
            AddRow(
                GetWorldRuleName(type, data),
                status,
                statusColor,
                "APPLY",
                canApply,
                () => ApplyWorldRule(capturedData)
            );
        }
    }

    private void AddLocalAnomaliesSection()
    {
        AddSectionTitle(
            "LOCAL ANOMALIES",
            "Apply or clear through LevelAnomalyController"
        );

        if (localAnomalies != null)
        {
            for (int i = 0; i < localAnomalies.Length; i++)
            {
                LocalAnomalyData data = localAnomalies[i];

                if (data == null || WasAnomalyAlreadyAdded(data, i))
                    continue;

                AddLocalAnomalyRow(data);
            }
        }

        for (int i = 0; i < DebugAnomalyTypes.Length; i++)
        {
            LocalAnomalyType type = DebugAnomalyTypes[i];

            if (FindLocalAnomaly(type) != null)
                continue;

            AddRow(
                $"{GetAnomalyTypeName(type)}  ·  {type}",
                "NOT CONFIGURED",
                warningColor,
                "APPLY",
                false,
                null
            );
        }

        AddRow(
            "All local anomaly zones",
            anomalyController == null
                ? "CONTROLLER NOT FOUND"
                : anomalyController.ActiveAnomaly == null
                    ? "CLEAR"
                    : "ACTIVE",
            anomalyController != null &&
                anomalyController.ActiveAnomaly != null
                    ? successColor
                    : mutedColor,
            "CLEAR ANOMALIES",
            anomalyController != null,
            ClearLocalAnomalies
        );

        AddActiveAnomalySummary();
    }

    private void AddLocalAnomalyRow(LocalAnomalyData data)
    {
        bool canApply = anomalyController != null &&
            data != null && data.ZonePrefab != null;
        string status;
        Color statusColor;

        if (data.ZonePrefab == null)
        {
            status = "MISSING PREFAB";
            statusColor = warningColor;
        }
        else if (anomalyController == null)
        {
            status = "NOT CONFIGURED";
            statusColor = warningColor;
        }
        else if (anomalyController.ActiveAnomaly == data)
        {
            status = "ACTIVE";
            statusColor = successColor;
        }
        else
        {
            status = "AVAILABLE";
            statusColor = mutedColor;
        }

        string displayName = !string.IsNullOrWhiteSpace(
                data.Presentation.Title)
            ? data.Presentation.Title
            : data.name;
        LocalAnomalyData capturedData = data;

        AddRow(
            $"{displayName}  ·  {GetAnomalyTypeName(data.AnomalyType)}",
            status,
            statusColor,
            "APPLY",
            canApply,
            () => ApplyLocalAnomaly(capturedData)
        );
    }

    private void AddActiveAnomalySummary()
    {
        LocalAnomalyData active = anomalyController != null
            ? anomalyController.ActiveAnomaly
            : null;
        string profile = active != null
            ? GetAnomalyTypeName(active.AnomalyType)
            : "None";

        AddRow("Active profile", profile,
            active != null ? successColor : mutedColor, null, false, null);
        AddRow("Active zones", BuildActiveZoneSummary(),
            activeAnomalyZones.Count > 0 ? successColor : mutedColor,
            null, false, null);
    }

    private string BuildActiveZoneSummary()
    {
        activeAnomalyZones.Clear();
        activeAnomalyTypes.Clear();
        activeAnomalyTypeCounts.Clear();

        if (anomalyController == null)
            return "None";

        anomalyController.CollectActiveLocalZones(activeAnomalyZones);

        for (int i = 0; i < activeAnomalyZones.Count; i++)
        {
            LocalAnomalyType type = activeAnomalyZones[i].Type;
            int typeIndex = activeAnomalyTypes.IndexOf(type);

            if (typeIndex >= 0)
            {
                activeAnomalyTypeCounts[typeIndex]++;
                continue;
            }

            activeAnomalyTypes.Add(type);
            activeAnomalyTypeCounts.Add(1);
        }

        if (activeAnomalyTypes.Count == 0)
            return "None";

        activeAnomalySummary.Clear();

        for (int i = 0; i < activeAnomalyTypes.Count; i++)
        {
            if (i > 0)
                activeAnomalySummary.Append(", ");

            activeAnomalySummary.Append(
                GetAnomalyTypeName(activeAnomalyTypes[i])
            );
            activeAnomalySummary.Append(" × ");
            activeAnomalySummary.Append(activeAnomalyTypeCounts[i]);
        }

        return activeAnomalySummary.ToString();
    }

    private void AddWorldEventsSection()
    {
        AddSectionTitle(
            "WORLD EVENTS",
            "Spawn/Clear through WorldEventSpawner"
        );

        WorldEvent currentEvent = worldEventSpawner != null
            ? worldEventSpawner.CurrentEvent
            : null;

        AddRow(
            $"Active event: {GetEventDisplayName(currentEvent)}",
            currentEvent != null ? "ACTIVE" : "None",
            currentEvent != null ? successColor : mutedColor,
            "CLEAR EVENT",
            currentEvent != null,
            ClearWorldEvent
        );

        addedEventPrefabs.Clear();
        AddEventRow<CaptureZoneEvent>("Capture Zone");
        AddEventRow<FalseSignalEvent>("False Signal");
        AddEventRow<EvacuationCorridorEvent>("Evacuation Corridor");
        AddEventRow<RescueCapsuleEvent>("Rescue Capsule");
        AddEventRow<CarrierHuntEvent>("Carrier Hunt");

        if (worldEventSpawner == null)
            return;

        IReadOnlyList<WorldEvent> connectedPrefabs =
            worldEventSpawner.EventPrefabs;

        if (connectedPrefabs == null)
            return;

        for (int i = 0; i < connectedPrefabs.Count; i++)
        {
            WorldEvent prefab = connectedPrefabs[i];

            if (prefab == null || addedEventPrefabs.Contains(prefab))
                continue;

            AddEventRow(GetEventDisplayName(prefab), prefab);
        }
    }

    private void AddEnemiesSection()
    {
        RemoveDestroyedDebugEnemies();
        AddSectionTitle("ENEMIES", "Spawn existing prefabs near the player");

        AddDebugEnemyRow("Turret", turretEnemyPrefab, "SPAWN TURRET");
        AddDebugEnemyRow("Eyes", eyesEnemyPrefab, "SPAWN EYES");
        AddRow(
            "Debug-spawned enemies",
            debugEnemies.Count > 0 ? $"ACTIVE: {debugEnemies.Count}" : "CLEAR",
            debugEnemies.Count > 0 ? successColor : mutedColor,
            "CLEAR DEBUG ENEMIES",
            debugEnemies.Count > 0,
            ClearDebugEnemies
        );
    }

    private void AddDebugEnemyRow(
        string displayName,
        GameObject prefab,
        string buttonLabel)
    {
        bool available = enemySpawner != null && prefab != null;
        string status = prefab == null
            ? "PREFAB NOT ASSIGNED"
            : enemySpawner == null
                ? "SPAWNER NOT FOUND"
                : "AVAILABLE";

        AddRow(
            displayName,
            status,
            available ? successColor : warningColor,
            buttonLabel,
            available,
            () => SpawnDebugEnemy(prefab)
        );
    }

    private void SpawnDebugEnemy(GameObject prefab)
    {
        if (enemySpawner == null || prefab == null)
            return;

        Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (player == null)
            return;

        GameObject enemy = enemySpawner.SpawnSpecificEnemyAround(
            prefab,
            player.position,
            3f,
            6f,
            3f,
            false
        );

        if (enemy != null)
            debugEnemies.Add(enemy);

        RefreshData();
    }

    private void ClearDebugEnemies()
    {
        for (int i = debugEnemies.Count - 1; i >= 0; i--)
        {
            if (debugEnemies[i] != null)
                Destroy(debugEnemies[i]);
        }

        debugEnemies.Clear();
        RefreshData();
    }

    private void RemoveDestroyedDebugEnemies()
    {
        for (int i = debugEnemies.Count - 1; i >= 0; i--)
        {
            if (debugEnemies[i] == null)
                debugEnemies.RemoveAt(i);
        }
    }

    private void AddEventRow<T>(string displayName) where T : WorldEvent
    {
        WorldEvent prefab = FindEventPrefab<T>();
        AddEventRow(displayName, prefab);
    }

    private void AddEventRow(string displayName, WorldEvent prefab)
    {
        string status;
        Color statusColor;
        bool canSpawn = false;

        if (prefab != null && !addedEventPrefabs.Contains(prefab))
            addedEventPrefabs.Add(prefab);

        if (prefab == null)
        {
            status = "MISSING";
            statusColor = warningColor;
        }
        else if (worldEventSpawner == null)
        {
            status = "MISSING";
            statusColor = warningColor;
        }
        else if (!ContainsEventPrefab(worldEventSpawner.EventPrefabs, prefab))
        {
            status = "PREFAB NOT CONNECTED";
            statusColor = warningColor;
        }
        else if (!worldEventSpawner.IsEventPrefabEnabled(prefab))
        {
            status = "CONNECTED BUT DISABLED";
            statusColor = warningColor;
        }
        else
        {
            WorldEvent currentEvent = worldEventSpawner.CurrentEvent;
            status = currentEvent != null &&
                currentEvent.GetType() == prefab.GetType()
                    ? "ACTIVE"
                    : "AVAILABLE";
            statusColor = successColor;
            canSpawn = true;
        }

        AddRow(
            displayName,
            status,
            statusColor,
            "SPAWN",
            canSpawn,
            () => SpawnWorldEvent(prefab)
        );
    }

    private void SpawnWorldEvent(WorldEvent prefab)
    {
        if (worldEventSpawner == null || prefab == null)
            return;

        worldEventSpawner.SpawnDebugEvent(prefab);
        RefreshData();
    }

    private void ClearWorldEvent()
    {
        if (worldEventSpawner == null)
            return;

        worldEventSpawner.ClearDebugEvent();
        RefreshData();
    }

    private void ApplyWorldRule(WorldRuleData data)
    {
        if (worldRuleController == null || data == null)
            return;

        worldRuleController.Apply(data);
        RefreshData();
    }

    private void ClearWorldRule()
    {
        if (worldRuleController == null)
            return;

        worldRuleController.Clear();
        RefreshData();
    }

    private void ApplyLocalAnomaly(LocalAnomalyData data)
    {
        if (anomalyController == null || data == null ||
            data.ZonePrefab == null)
        {
            return;
        }

        anomalyController.Apply(data);
        RefreshData();
    }

    private void ClearLocalAnomalies()
    {
        if (anomalyController == null)
            return;

        anomalyController.Clear();
        RefreshData();
    }

    private LocalAnomalyData FindLocalAnomaly(LocalAnomalyType type)
    {
        if (localAnomalies == null)
            return null;

        for (int i = 0; i < localAnomalies.Length; i++)
        {
            if (localAnomalies[i] != null &&
                localAnomalies[i].AnomalyType == type)
            {
                return localAnomalies[i];
            }
        }

        return null;
    }

    private bool WasAnomalyAlreadyAdded(LocalAnomalyData data, int beforeIndex)
    {
        for (int i = 0; i < beforeIndex; i++)
        {
            if (localAnomalies[i] == data)
                return true;
        }

        return false;
    }

    private WorldEvent FindEventPrefab<T>() where T : WorldEvent
    {
        if (worldEventPrefabs == null)
            return null;

        for (int i = 0; i < worldEventPrefabs.Length; i++)
        {
            if (worldEventPrefabs[i] is T)
                return worldEventPrefabs[i];
        }

        return null;
    }

    private static string GetAnomalyTypeName(LocalAnomalyType type)
    {
        return type == LocalAnomalyType.ExplosiveZone
            ? "Explosive"
            : type.ToString();
    }

    private static string GetEventDisplayName(WorldEvent worldEvent)
    {
        if (worldEvent == null)
            return "None";

        if (worldEvent is CaptureZoneEvent)
            return "Capture Zone";
        if (worldEvent is FalseSignalEvent)
            return "False Signal";
        if (worldEvent is EvacuationCorridorEvent)
            return "Evacuation Corridor";
        if (worldEvent is RescueCapsuleEvent)
            return "Rescue Capsule";
        if (worldEvent is CarrierHuntEvent)
            return "Carrier Hunt";

        return string.IsNullOrWhiteSpace(worldEvent.EventDisplayName)
            ? worldEvent.name
            : worldEvent.EventDisplayName;
    }

    private static bool ContainsEventPrefab(
        System.Collections.Generic.IReadOnlyList<WorldEvent> prefabs,
        WorldEvent target)
    {
        if (prefabs == null)
            return false;

        for (int i = 0; i < prefabs.Count; i++)
        {
            if (prefabs[i] == target)
                return true;
        }

        return false;
    }

    private static string GetWorldRuleName(
        WorldRuleType type,
        WorldRuleData data)
    {
        if (data != null && !string.IsNullOrWhiteSpace(data.DisplayName))
            return data.DisplayName;

        return type.ToString();
    }

    private void AddSectionTitle(string title, string subtitle)
    {
        RectTransform section = CreateRect(title, contentRoot);
        LayoutElement layout = section.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 64f;
        section.gameObject.AddComponent<Image>().color =
            new Color(accentColor.r, accentColor.g, accentColor.b, 0.18f);

        TextMeshProUGUI text = CreateText(
            "Label",
            section,
            $"<b>{title}</b>\n<size=17><color=#A6AFBC>{subtitle}</color></size>",
            23f,
            TextAlignmentOptions.MidlineLeft,
            Color.white
        );
        Stretch(text.rectTransform, 16f, 12f, 8f, 8f);
    }

    private void AddRow(
        string label,
        string status,
        Color statusColor,
        string buttonLabel,
        bool buttonEnabled,
        UnityEngine.Events.UnityAction action)
    {
        RectTransform row = CreateRect(label, contentRoot);
        row.gameObject.AddComponent<Image>().color = rowColor;
        LayoutElement layout = row.gameObject.AddComponent<LayoutElement>();
        layout.preferredHeight = 54f;

        float buttonWidth = buttonLabel switch
        {
            "CLEAR DEBUG ENEMIES" => 218f,
            "CLEAR ANOMALIES" => 176f,
            "SPAWN TURRET" => 166f,
            "SPAWN EYES" => 150f,
            "CLEAR EVENT" => 150f,
            _ => 116f
        };
        float rightPadding = string.IsNullOrEmpty(buttonLabel)
            ? 18f
            : buttonWidth + 36f;
        TextMeshProUGUI labelText = CreateText(
            "Name",
            row,
            label,
            20f,
            TextAlignmentOptions.MidlineLeft,
            Color.white
        );
        labelText.rectTransform.anchorMin = Vector2.zero;
        labelText.rectTransform.anchorMax = new Vector2(0.52f, 1f);
        labelText.rectTransform.offsetMin = new Vector2(16f, 0f);
        labelText.rectTransform.offsetMax = Vector2.zero;

        TextMeshProUGUI statusText = CreateText(
            "Status",
            row,
            status,
            16f,
            TextAlignmentOptions.MidlineRight,
            statusColor
        );
        statusText.rectTransform.anchorMin = new Vector2(0.47f, 0f);
        statusText.rectTransform.anchorMax = Vector2.one;
        statusText.rectTransform.offsetMin = Vector2.zero;
        statusText.rectTransform.offsetMax = new Vector2(-rightPadding, 0f);

        if (string.IsNullOrEmpty(buttonLabel))
            return;

        Button button = CreateButton(
            row,
            buttonLabel,
            action,
            buttonWidth,
            buttonEnabled
        );
        RectTransform buttonRect = button.GetComponent<RectTransform>();
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(1f, 0.5f);
        buttonRect.pivot = new Vector2(1f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(-12f, 0f);
        buttonRect.sizeDelta = new Vector2(buttonWidth, 38f);
    }

    private void AddHint(string message)
    {
        TextMeshProUGUI hint = CreateText(
            "Hint",
            contentRoot,
            message,
            16f,
            TextAlignmentOptions.Center,
            mutedColor
        );
        hint.gameObject.AddComponent<LayoutElement>().preferredHeight = 42f;
    }

    private Button CreateButton(
        Transform parent,
        string label,
        UnityEngine.Events.UnityAction action,
        float width,
        bool interactable = true)
    {
        RectTransform rect = CreateRect(label + " Button", parent);
        rect.sizeDelta = new Vector2(width, 38f);
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = accentColor;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.2f, 0.73f, 0.88f, 1f);
        colors.pressedColor = new Color(0.08f, 0.4f, 0.53f, 1f);
        colors.disabledColor = new Color(0.22f, 0.24f, 0.27f, 0.7f);
        button.colors = colors;
        button.interactable = interactable;

        if (interactable && action != null)
            button.onClick.AddListener(action);

        TextMeshProUGUI text = CreateText(
            "Label",
            rect,
            label,
            17f,
            TextAlignmentOptions.Center,
            Color.white
        );
        Stretch(text.rectTransform);
        return button;
    }

    private static TextMeshProUGUI CreateText(
        string name,
        Transform parent,
        string value,
        float fontSize,
        TextAlignmentOptions alignment,
        Color color)
    {
        RectTransform rect = CreateRect(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject gameObject = new(name, typeof(RectTransform));
        RectTransform rect = gameObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private static void Stretch(
        RectTransform rect,
        float left = 0f,
        float right = 0f,
        float bottom = 0f,
        float top = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);
    }
#else
    private void Awake()
    {
        enabled = false;
    }
#endif
}
