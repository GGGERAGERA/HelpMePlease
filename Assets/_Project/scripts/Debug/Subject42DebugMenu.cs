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
    [SerializeField] private CharacterSpawner characterSpawner;
    [SerializeField] private UpgradeManager upgradeManager;
    [SerializeField] private RunTimer runTimer;
    [SerializeField] private RunFlowController runFlowController;
    [SerializeField] private LevelChoiceManager levelChoiceManager;

    [Header("Known project content")]
    [SerializeField] private WorldRuleData[] worldRules;
    [SerializeField] private LocalAnomalyData[] localAnomalies;
    [SerializeField] private WorldEvent[] worldEventPrefabs;
    [SerializeField] private GameObject turretEnemyPrefab;
    [SerializeField] private GameObject eyesEnemyPrefab;
    [SerializeField] private WeaponData[] debugWeapons;
    [SerializeField] private UpgradeData[] additionalDebugUpgrades;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private enum DebugTab
    {
        Run,
        Bunker,
        World,
        Enemies,
        Events,
        WeaponsAndUpgrades,
        Telekinesis
    }

    private enum UpgradeFilter
    {
        All,
        Numeric,
        Behavior,
        OutOfPool
    }

    private enum SandboxTab
    {
        Exploration,
        SectorVisual,
        VisualReadability,
        WorldRule,
        Anomaly,
        WeaponsPowers
    }

    private static readonly string[] TabLabels =
    {
        "RUN",
        "BUNKER",
        "WORLD",
        "ENEMIES",
        "EVENTS",
        "WEAPONS & UPGRADES",
        "TELEKINESIS"
    };

    private static readonly string[] SandboxTabLabels =
    {
        "EXPLORATION",
        "SECTOR VISUAL",
        "VISUAL READABILITY",
        "WORLD RULE",
        "ANOMALY",
        "WEAPONS & POWERS"
    };

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
    private readonly GameObject[] tabRoots = new GameObject[TabLabels.Length];
    private readonly Image[] tabButtonImages = new Image[TabLabels.Length];
    private DebugTab activeTab = DebugTab.Run;
    private SandboxTab activeSandboxTab = SandboxTab.Exploration;
    private UpgradeFilter upgradeFilter = UpgradeFilter.All;
    private bool sandboxLabMode;
    private ExplorationSectorController explorationSector;
    private SectorVisualDebugController sectorVisualController;
    private AnomalyPowerDebugController anomalyPowerController;
    private GravityTrajectoryPreview trajectoryPreview;
    private WorldEventDebugStatusOverlay eventStatusOverlay;
    private EnvironmentReadabilityDebugController readabilityController;
    private bool isOpen;
    private bool waitingForF1Release;
    private float previousTimeScale;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;
    private bool warnedRuleController;
    private bool warnedAnomalyController;
    private bool warnedEventSpawner;
    private string lastUpgradeResult;

    private readonly List<LevelAnomalyController.LocalAnomalyZoneGeometry>
        activeAnomalyZones = new();
    private readonly List<LocalAnomalyType> activeAnomalyTypes = new();
    private readonly List<int> activeAnomalyTypeCounts = new();
    private readonly StringBuilder activeAnomalySummary = new();
    private readonly List<WorldEvent> addedEventPrefabs = new();
    private readonly List<GameObject> debugEnemies = new();
    private readonly List<UpgradeData> visibleUpgrades = new();
    private TelekinesisDebugPrototype telekinesisPrototype;

    private readonly Color panelColor = new(0.035f, 0.045f, 0.06f, 0.97f);
    private readonly Color rowColor = new(0.09f, 0.11f, 0.145f, 0.95f);
    private readonly Color accentColor = new(0.13f, 0.58f, 0.72f, 1f);
    private readonly Color mutedColor = new(0.65f, 0.69f, 0.74f, 1f);
    private readonly Color successColor = new(0.36f, 0.82f, 0.48f, 1f);
    private readonly Color warningColor = new(1f, 0.69f, 0.25f, 1f);

    private void Awake()
    {
        ResolveSceneReferences();
    }

    private void Start()
    {
        BuildMenu();
        RefreshAllTabs();
        if (sandboxLabMode)
            SelectSandboxTab(activeSandboxTab, false);
        else
            SelectTab(activeTab, false);
        menuRoot.SetActive(false);
    }

    public void ConfigureSandbox(
        WorldRuleController rulesController,
        LevelAnomalyController anomaliesController,
        WorldEventSpawner eventsSpawner,
        EnemySpawner enemiesSpawner,
        CharacterSpawner playerSpawner,
        UpgradeManager upgradesManager,
        WorldRuleData[] rules,
        LocalAnomalyData[] anomalies,
        WorldEvent[] events,
        GameObject turretPrefab,
        GameObject eyesPrefab,
        WeaponData[] weapons,
        UpgradeData[] upgrades)
    {
        worldRuleController = rulesController;
        anomalyController = anomaliesController;
        worldEventSpawner = eventsSpawner;
        enemySpawner = enemiesSpawner;
        characterSpawner = playerSpawner;
        upgradeManager = upgradesManager;
        worldRules = rules;
        localAnomalies = anomalies;
        worldEventPrefabs = events;
        turretEnemyPrefab = turretPrefab;
        eyesEnemyPrefab = eyesPrefab;
        debugWeapons = weapons;
        additionalDebugUpgrades = upgrades;
        if (menuRoot != null)
        {
            RefreshAllTabs();
            SelectTab(activeTab, false);
        }
    }

    public void ConfigureSandboxLab(
        ExplorationSectorController exploration,
        SectorVisualDebugController sectorVisual,
        AnomalyPowerDebugController powers,
        GravityTrajectoryPreview trajectory,
        WorldEventDebugStatusOverlay eventOverlay,
        EnvironmentReadabilityDebugController readability)
    {
        sandboxLabMode = true;
        explorationSector = exploration;
        sectorVisualController = sectorVisual;
        anomalyPowerController = powers;
        trajectoryPreview = trajectory;
        eventStatusOverlay = eventOverlay;
        readabilityController = readability;
    }

    private void Update()
    {
        if (waitingForF1Release)
        {
            if (!Input.GetKey(KeyCode.F1))
                waitingForF1Release = false;

            return;
        }

        if (!Input.GetKeyDown(KeyCode.F1))
            return;

        waitingForF1Release = true;
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
        if (sandboxLabMode)
            RefreshSandboxTab(activeSandboxTab);
        else
            RefreshTab(activeTab);

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
        bool productionChoiceIsOpen =
            (levelChoiceManager != null && levelChoiceManager.IsChoosing) ||
            (upgradeManager != null && upgradeManager.IsChoosingUpgrade);

        if (productionChoiceIsOpen)
        {
            Time.timeScale = 0f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            return;
        }

        Time.timeScale = previousTimeScale;
        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockMode;
    }

    private void ResolveSceneReferences()
    {
        worldRuleController ??= FindFirstObjectByType<WorldRuleController>();
        anomalyController ??= FindFirstObjectByType<LevelAnomalyController>();
        worldEventSpawner ??= FindFirstObjectByType<WorldEventSpawner>();
        enemySpawner ??= FindFirstObjectByType<EnemySpawner>();
        characterSpawner ??= FindFirstObjectByType<CharacterSpawner>();
        upgradeManager ??= UpgradeManager.Instance != null
            ? UpgradeManager.Instance
            : FindFirstObjectByType<UpgradeManager>();
        runTimer ??= FindFirstObjectByType<RunTimer>();
        runFlowController ??= RunFlowController.Instance != null
            ? RunFlowController.Instance
            : FindFirstObjectByType<RunFlowController>();
        levelChoiceManager ??= FindFirstObjectByType<LevelChoiceManager>();

        WarnIfMissing(worldRuleController, ref warnedRuleController,
            "WorldRuleController");
        WarnIfMissing(anomalyController, ref warnedAnomalyController,
            "LevelAnomalyController");
        WarnIfMissing(worldEventSpawner, ref warnedEventSpawner,
            "WorldEventSpawner");
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
            "The related tab is diagnostics-only until it is available.",
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
        panel.anchorMin = new Vector2(0.07f, 0.05f);
        panel.anchorMax = new Vector2(0.93f, 0.95f);
        panel.offsetMin = Vector2.zero;
        panel.offsetMax = Vector2.zero;
        panel.gameObject.AddComponent<Image>().color = panelColor;

        RectTransform header = CreateRect("Header", panel);
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = Vector2.one;
        header.pivot = new Vector2(0.5f, 1f);
        header.sizeDelta = new Vector2(0f, 62f);
        header.anchoredPosition = Vector2.zero;

        TextMeshProUGUI title = CreateText(
            "Title", header, "SUBJECT#42 - DEBUG MENU", 28f,
            TextAlignmentOptions.MidlineLeft, Color.white
        );
        Stretch(title.rectTransform, 22f, 90f);

        Button closeButton = CreateButton(header, "X", CloseMenu, 52f);
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 0.5f);
        closeRect.pivot = new Vector2(1f, 0.5f);
        closeRect.anchoredPosition = new Vector2(-14f, 0f);
        closeRect.sizeDelta = new Vector2(52f, 40f);

        RectTransform tabBar = CreateRect("Tabs", panel);
        tabBar.anchorMin = new Vector2(0f, 1f);
        tabBar.anchorMax = Vector2.one;
        tabBar.pivot = new Vector2(0.5f, 1f);
        tabBar.anchoredPosition = new Vector2(0f, -62f);
        tabBar.sizeDelta = new Vector2(0f, 54f);

        string[] labels = sandboxLabMode ? SandboxTabLabels : TabLabels;
        for (int i = 0; i < labels.Length; i++)
        {
            int captured = i;
            RectTransform slot = CreateRect(labels[i] + " Slot", tabBar);
            slot.anchorMin = new Vector2((float)i / labels.Length, 0f);
            slot.anchorMax = new Vector2((float)(i + 1) / labels.Length, 1f);
            slot.offsetMin = new Vector2(3f, 3f);
            slot.offsetMax = new Vector2(-3f, -3f);
            Button button = CreateButton(
                slot,
                labels[i],
                sandboxLabMode
                    ? () => SelectSandboxTab((SandboxTab)captured)
                    : () => SelectTab((DebugTab)captured),
                100f
            );
            Stretch(button.GetComponent<RectTransform>());
            tabButtonImages[i] = button.targetGraphic as Image;
        }

        RectTransform pages = CreateRect("Tab Pages", panel);
        pages.anchorMin = Vector2.zero;
        pages.anchorMax = Vector2.one;
        pages.offsetMin = new Vector2(18f, 18f);
        pages.offsetMax = new Vector2(-18f, -120f);

        for (int i = 0; i < labels.Length; i++)
            tabRoots[i] = CreateTabPage(labels[i], pages, out _);
    }

    private GameObject CreateTabPage(
        string tabName,
        Transform parent,
        out RectTransform pageContent)
    {
        RectTransform page = CreateRect(tabName + " Page", parent);
        Stretch(page);

        ScrollRect scroll = page.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 34f;

        RectTransform viewport = CreateRect("Viewport", page);
        Stretch(viewport);
        viewport.gameObject.AddComponent<Image>().color =
            new Color(0f, 0f, 0f, 0.12f);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        scroll.viewport = viewport;

        pageContent = CreateRect("Content", viewport);
        pageContent.anchorMin = new Vector2(0f, 1f);
        pageContent.anchorMax = Vector2.one;
        pageContent.pivot = new Vector2(0.5f, 1f);
        pageContent.offsetMin = new Vector2(10f, 0f);
        pageContent.offsetMax = new Vector2(-10f, 0f);

        VerticalLayoutGroup layout =
            pageContent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(10, 10, 10, 16);
        layout.spacing = 7f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter =
            pageContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = pageContent;
        return page.gameObject;
    }

    private void SelectTab(DebugTab tab, bool refresh = true)
    {
        activeTab = tab;

        for (int i = 0; i < tabRoots.Length; i++)
        {
            bool selected = i == (int)tab;
            tabRoots[i].SetActive(selected);

            if (tabButtonImages[i] != null)
            {
                tabButtonImages[i].color = selected
                    ? new Color(0.2f, 0.73f, 0.88f, 1f)
                    : accentColor;
            }
        }

        if (refresh)
            RefreshTab(tab);
    }

    private void SelectSandboxTab(SandboxTab tab, bool refresh = true)
    {
        activeSandboxTab = tab;
        for (int i = 0; i < tabRoots.Length; i++)
        {
            if (tabRoots[i] == null)
                continue;
            bool selected = i == (int)tab;
            tabRoots[i].SetActive(selected);
            if (tabButtonImages[i] != null)
            {
                tabButtonImages[i].color = selected
                    ? new Color(0.2f, 0.73f, 0.88f, 1f)
                    : accentColor;
            }
        }
        if (refresh)
            RefreshSandboxTab(tab);
    }

    private void RefreshAllTabs()
    {
        if (sandboxLabMode)
        {
            for (int i = 0; i < SandboxTabLabels.Length; i++)
                RefreshSandboxTab((SandboxTab)i);
            return;
        }
        for (int i = 0; i < tabRoots.Length; i++)
            RefreshTab((DebugTab)i);
    }

    private void RefreshCurrentTab()
    {
        if (sandboxLabMode)
            RefreshSandboxTab(activeSandboxTab);
        else
            RefreshTab(activeTab);
    }

    private void RefreshSandboxTab(SandboxTab tab)
    {
        if (tabRoots[(int)tab] == null)
            return;

        contentRoot = tabRoots[(int)tab].transform
            .Find("Viewport/Content") as RectTransform;
        if (contentRoot == null)
            return;
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        AddTabHeading(SandboxTabLabels[(int)tab]);
        switch (tab)
        {
            case SandboxTab.Exploration:
                AddSandboxExplorationSection();
                break;
            case SandboxTab.SectorVisual:
                AddSandboxSectorVisualSection();
                break;
            case SandboxTab.VisualReadability:
                AddSandboxVisualReadabilitySection();
                break;
            case SandboxTab.WorldRule:
                AddSandboxWorldRuleSection();
                break;
            case SandboxTab.Anomaly:
                AddSandboxAnomalySection();
                break;
            case SandboxTab.WeaponsPowers:
                AddSandboxWeaponsPowersSection();
                break;
        }
        AddHint("F1 toggles this menu. Gameplay remains paused while it is open.");
    }

    private void RefreshTab(DebugTab tab)
    {
        if (tabRoots[(int)tab] == null)
            return;

        contentRoot = tabRoots[(int)tab].transform
            .Find("Viewport/Content") as RectTransform;

        if (contentRoot == null)
            return;

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        AddTabHeading(TabLabels[(int)tab]);

        switch (tab)
        {
            case DebugTab.Run:
                AddRunSection();
                break;
            case DebugTab.Bunker:
                AddBunkerSection();
                break;
            case DebugTab.World:
                AddWorldRulesSection();
                AddLocalAnomaliesSection();
                break;
            case DebugTab.Enemies:
                AddEnemiesSection();
                break;
            case DebugTab.Events:
                AddWorldEventsSection();
                break;
            case DebugTab.WeaponsAndUpgrades:
                AddWeaponsSection();
                AddUpgradesSection();
                break;
            case DebugTab.Telekinesis:
                AddTelekinesisSection();
                break;
        }

        AddHint("F1 toggles this menu. Gameplay remains paused while it is open.");
    }

    private void AddSandboxExplorationSection()
    {
        bool available = explorationSector != null;
        AddSectionTitle("SECTOR SESSION", "Runtime-only Exploration controls");
        AddRow("New Layout", available ? "GENERATE + RESET" : "NOT FOUND",
            available ? mutedColor : warningColor,
            "NEW", available, () =>
            {
                explorationSector.NewLayout();
                RefreshCurrentTab();
            });
        AddRow("Reset Sector", available ? "KEEP LAYOUT" : "NOT FOUND",
            available ? mutedColor : warningColor,
            "RESET", available, () =>
            {
                explorationSector.ResetSector();
                RefreshCurrentTab();
            });
        AddToggleRow("Invulnerability",
            available && explorationSector.InvulnerabilityEnabled,
            available,
            () => explorationSector.SetInvulnerability(
                !explorationSector.InvulnerabilityEnabled));
        AddToggleRow("Exploration HUD",
            available && explorationSector.HudVisible,
            available,
            () => explorationSector.SetHudVisible(!explorationSector.HudVisible));
        AddToggleRow("Debug Map",
            available && explorationSector.MapVisible,
            available,
            () => explorationSector.SetMapVisible(!explorationSector.MapVisible));

        AddSectionTitle("ENEMY CAP", "Comparison scale; default balance remains 100%");
        AddOptionRow("50%", available && Mathf.Approximately(
            explorationSector.EnemyCapScale, 0.5f), available,
            () => explorationSector.SetEnemyCapScale(0.5f));
        AddOptionRow("75%", available && Mathf.Approximately(
            explorationSector.EnemyCapScale, 0.75f), available,
            () => explorationSector.SetEnemyCapScale(0.75f));
        AddOptionRow("100%", available && Mathf.Approximately(
            explorationSector.EnemyCapScale, 1f), available,
            () => explorationSector.SetEnemyCapScale(1f));
        AddRow("Kill All Enemies", $"ALIVE: {EnemyHealth.ActiveInstances.Count}",
            mutedColor, "KILL ALL", anomalyPowerController != null,
            () => anomalyPowerController.KillAllEnemiesDebug());

        if (!available)
            return;
        AddSectionTitle("LIVE STATUS", "Current Exploration state");
        AddRow("Threat", explorationSector.ThreatLevel.ToString(), mutedColor,
            null, false, null);
        AddRow("Elapsed", FormatDebugElapsed(explorationSector.Elapsed), mutedColor,
            null, false, null);
        AddRow("Enemies Alive", explorationSector.EnemiesAlive.ToString(), mutedColor,
            null, false, null);
        AddRow("Sites completed", $"{explorationSector.SitesCompleted}/4", mutedColor,
            null, false, null);
    }

    private void AddSandboxSectorVisualSection()
    {
        bool available = sectorVisualController != null;
        string active = available ? sectorVisualController.CurrentPresetName : "NOT FOUND";
        AddSectionTitle("VISUAL PRESET", $"Active: {active}");
        AddSectorPresetRow(SectorVisualDebugController.SectorPreset.Calibration);
        AddSectorPresetRow(SectorVisualDebugController.SectorPreset.CorruptedTest);
        AddSectorPresetRow(SectorVisualDebugController.SectorPreset.Containment);
        AddSectorPresetRow(SectorVisualDebugController.SectorPreset.SystemFailure);
        AddSectorPresetRow(SectorVisualDebugController.SectorPreset.CoreFinalTest);

        AddSectionTitle("LAYERS", "Visual only; no colliders or gameplay state");
        AddToggleRow("Grid", available && sectorVisualController.GridVisible,
            available, () => sectorVisualController.SetGridVisible(
                !sectorVisualController.GridVisible));
        AddToggleRow("Sector Lines",
            available && sectorVisualController.SectorLinesVisible,
            available, () => sectorVisualController.SetSectorLinesVisible(
                !sectorVisualController.SectorLinesVisible));
        AddToggleRow("Debug Boundaries",
            available && sectorVisualController.BoundariesVisible,
            available, () => sectorVisualController.SetBoundariesVisible(
                !sectorVisualController.BoundariesVisible));
        AddToggleRow("Exploration HUD",
            explorationSector != null && explorationSector.HudVisible,
            explorationSector != null,
            () => explorationSector.SetHudVisible(!explorationSector.HudVisible));
    }

    private void AddSectorPresetRow(SectorVisualDebugController.SectorPreset preset)
    {
        bool available = sectorVisualController != null;
        bool selected = available && sectorVisualController.CurrentPreset == preset;
        AddRow(SectorVisualDebugController.GetPresetName(preset),
            selected ? "SELECTED" : "AVAILABLE",
            selected ? successColor : mutedColor,
            ((int)preset).ToString(), available, () =>
            {
                sectorVisualController.ApplyPreset(preset);
                RefreshCurrentTab();
            });
    }

    private void AddSandboxVisualReadabilitySection()
    {
        bool controllerAvailable = readabilityController != null;
        bool canEnable = controllerAvailable && readabilityController.CanEnable;
        bool testEnabled = controllerAvailable && readabilityController.TestEnabled;
        AddSectionTitle("ENVIRONMENT READABILITY TEST",
            "Opt-in Sandbox visual layer; OFF preserves the original scene");
        AddRow("ENABLE ENVIRONMENT READABILITY TEST",
            testEnabled ? "ON" : "OFF",
            testEnabled ? successColor : mutedColor,
            "TOGGLE",
            canEnable,
            () =>
            {
                readabilityController.SetTestEnabled(!readabilityController.TestEnabled);
                RefreshCurrentTab();
            });

        string active = testEnabled
            ? EnvironmentReadabilityDebugController.GetPresetName(
                readabilityController.Preset)
            : "DISABLED";
        AddSectionTitle(
            "READABILITY PRESET",
            $"Active: {active} | Environment renderers: " +
            $"{(testEnabled ? readabilityController.EnvironmentRendererCount : 0)}"
        );
        AddReadabilityPresetRow(
            EnvironmentReadabilityDebugController.ReadabilityPreset.Original);
        AddReadabilityPresetRow(
            EnvironmentReadabilityDebugController.ReadabilityPreset.MutedWorld);
        AddReadabilityPresetRow(
            EnvironmentReadabilityDebugController.ReadabilityPreset.HighGameplayContrast);
        AddReadabilityPresetRow(
            EnvironmentReadabilityDebugController.ReadabilityPreset.DarkWorld);

        AddSectionTitle("ENVIRONMENT PROPS INTENSITY",
            "Trees, plants, decorative props and their shadows");
        AddReadabilityValueRow("Props 100%", 1f,
            testEnabled ? readabilityController.PropsIntensity : 1f,
            testEnabled, value => readabilityController.SetPropsIntensity(value));
        AddReadabilityValueRow("Props 75%", 0.75f,
            testEnabled ? readabilityController.PropsIntensity : 1f,
            testEnabled, value => readabilityController.SetPropsIntensity(value));
        AddReadabilityValueRow("Props 50%", 0.5f,
            testEnabled ? readabilityController.PropsIntensity : 1f,
            testEnabled, value => readabilityController.SetPropsIntensity(value));
        AddReadabilityValueRow("Props 25%", 0.25f,
            testEnabled ? readabilityController.PropsIntensity : 1f,
            testEnabled, value => readabilityController.SetPropsIntensity(value));

        AddSectionTitle("ANOMALY EMPHASIS",
            "Visual brightness/alpha and line width only; radius unchanged");
        AddReadabilityValueRow("Anomaly 100%", 1f,
            testEnabled ? readabilityController.AnomalyEmphasis : 1f,
            testEnabled, value => readabilityController.SetAnomalyEmphasis(value));
        AddReadabilityValueRow("Anomaly 125%", 1.25f,
            testEnabled ? readabilityController.AnomalyEmphasis : 1f,
            testEnabled, value => readabilityController.SetAnomalyEmphasis(value));
        AddReadabilityValueRow("Anomaly 150%", 1.5f,
            testEnabled ? readabilityController.AnomalyEmphasis : 1f,
            testEnabled, value => readabilityController.SetAnomalyEmphasis(value));

        AddSectionTitle("ENEMY READABILITY", "Optional subtle tint separation");
        AddOptionRow("Enemy Highlight OFF",
            testEnabled && !readabilityController.EnemyHighlight,
            testEnabled,
            () => readabilityController.SetEnemyHighlight(false));
        AddOptionRow("Enemy Highlight SUBTLE",
            testEnabled && readabilityController.EnemyHighlight,
            testEnabled,
            () => readabilityController.SetEnemyHighlight(true));

        AddRow("Reset Visual", testEnabled ? "RESTORE ORIGINAL" : "DISABLED",
            testEnabled ? warningColor : mutedColor,
            "RESET VISUAL", testEnabled, () =>
            {
                readabilityController.ResetVisual();
                RefreshCurrentTab();
            });
    }

    private void AddReadabilityPresetRow(
        EnvironmentReadabilityDebugController.ReadabilityPreset value)
    {
        bool available = readabilityController != null && readabilityController.TestEnabled;
        bool selected = available && readabilityController.Preset == value;
        AddRow(EnvironmentReadabilityDebugController.GetPresetName(value),
            selected ? "SELECTED" : "AVAILABLE",
            selected ? successColor : mutedColor,
            "SELECT", available, () =>
            {
                readabilityController.SetPreset(value);
                RefreshCurrentTab();
            });
    }

    private void AddReadabilityValueRow(
        string label,
        float value,
        float current,
        bool available,
        System.Action<float> setter)
    {
        bool selected = available && Mathf.Approximately(value, current);
        AddRow(label, selected ? "SELECTED" : "AVAILABLE",
            selected ? successColor : mutedColor,
            "SELECT", available, () =>
            {
                setter?.Invoke(value);
                RefreshCurrentTab();
            });
    }

    private void AddSandboxWorldRuleSection()
    {
        WorldRuleData active = worldRuleController != null
            ? worldRuleController.ActiveRule : null;
        AddSectionTitle("WORLD RULE",
            $"Active Rule: {(active != null ? GetWorldRuleName(active.RuleType, active) : "None")}");
        AddRow("None", active == null ? "SELECTED" : "AVAILABLE",
            active == null ? successColor : mutedColor,
            "APPLY", worldRuleController != null, ClearWorldRule);

        WorldRuleType[] order =
        {
            WorldRuleType.Snow,
            WorldRuleType.Rain,
            WorldRuleType.Wind,
            WorldRuleType.Darkness,
            WorldRuleType.Condensation,
            WorldRuleType.Golden
        };
        for (int i = 0; i < order.Length; i++)
        {
            WorldRuleData data = FindWorldRule(order[i]);
            bool selected = data != null && active == data;
            WorldRuleData captured = data;
            AddRow(order[i].ToString(), data == null ? "ASSET MISSING" :
                selected ? "SELECTED" : "AVAILABLE",
                data == null ? warningColor : selected ? successColor : mutedColor,
                "APPLY", worldRuleController != null && data != null,
                () => ApplyWorldRule(captured));
        }
        AddRow("Clear Rule", active != null ? "ACTIVE" : "CLEAR",
            active != null ? warningColor : mutedColor,
            "CLEAR", worldRuleController != null, ClearWorldRule);
    }

    private void AddSandboxAnomalySection()
    {
        bool trajectoryAvailable = trajectoryPreview != null;
        AddSectionTitle("GRAVITY TRAJECTORY", "Existing orbital prediction");
        AddToggleRow("Gravity Trajectory",
            trajectoryAvailable && trajectoryPreview.PreviewEnabled,
            trajectoryAvailable,
            () => trajectoryPreview.SetPreviewEnabled(!trajectoryPreview.PreviewEnabled));
        float[] times = { 0.75f, 1.25f, 1.5f, 2f };
        for (int i = 0; i < times.Length; i++)
        {
            float captured = times[i];
            AddOptionRow($"Prediction {captured:0.00} sec",
                trajectoryAvailable && Mathf.Approximately(
                    trajectoryPreview.PredictionTime, captured),
                trajectoryAvailable,
                () => trajectoryPreview.SetPredictionTime(captured));
        }
        AddSectionTitle("EVENT DIAGNOSTICS", "Existing interaction status overlay");
        AddToggleRow("Show Event Debug",
            eventStatusOverlay != null && eventStatusOverlay.OverlayVisible,
            eventStatusOverlay != null,
            () => eventStatusOverlay.SetOverlayVisible(
                !eventStatusOverlay.OverlayVisible));
    }

    private void AddSandboxWeaponsPowersSection()
    {
        AddWeaponsSection();
        AddSectionTitle("WEAPON CORE", "Existing debug selector");
        AddOptionRow("None", WeaponCoreDebugSelector.ActiveCore == WeaponCoreType.None,
            true, () => WeaponCoreDebugSelector.Select(WeaponCoreType.None));
        AddOptionRow("Chain", WeaponCoreDebugSelector.ActiveCore == WeaponCoreType.Chain,
            true, () => WeaponCoreDebugSelector.Select(WeaponCoreType.Chain));

        AddSectionTitle("ANOMALY POWERS", "Existing runtime power components");
        bool available = anomalyPowerController != null;
        AddPowerToggle("Gravity Orb",
            available && anomalyPowerController.GravityOrbEnabled,
            available && !anomalyPowerController.GravityOrbSiteLocked,
            () => anomalyPowerController.SetGravityOrbEnabled(
                !anomalyPowerController.GravityOrbEnabled));
        AddPowerToggle("Arc Node",
            available && anomalyPowerController.ArcNodeEnabled,
            available && !anomalyPowerController.ArcNodeSiteLocked,
            () => anomalyPowerController.SetArcNodeEnabled(
                !anomalyPowerController.ArcNodeEnabled));
        AddPowerToggle("Red Beam",
            available && anomalyPowerController.RedBeamEnabled,
            available && !anomalyPowerController.RedBeamSiteLocked,
            () => anomalyPowerController.SetRedBeamEnabled(
                !anomalyPowerController.RedBeamEnabled));
    }

    private void AddPowerToggle(string label, bool enabled, bool available,
        UnityEngine.Events.UnityAction action)
    {
        AddRow(label, available ? (enabled ? "ON" : "OFF") : "LOCKED",
            enabled ? successColor : available ? mutedColor : warningColor,
            enabled ? "TURN OFF" : "TURN ON", available, () =>
            {
                action?.Invoke();
                RefreshCurrentTab();
            });
    }

    private void AddToggleRow(string label, bool enabled, bool available,
        UnityEngine.Events.UnityAction action)
    {
        AddRow(label, enabled ? "ON" : "OFF",
            enabled ? successColor : mutedColor,
            enabled ? "TURN OFF" : "TURN ON", available, () =>
            {
                action?.Invoke();
                RefreshCurrentTab();
            });
    }

    private void AddOptionRow(string label, bool selected, bool available,
        UnityEngine.Events.UnityAction action)
    {
        AddRow(label, selected ? "SELECTED" : "AVAILABLE",
            selected ? successColor : mutedColor,
            "SELECT", available, () =>
            {
                action?.Invoke();
                RefreshCurrentTab();
            });
    }

    private WorldRuleData FindWorldRule(WorldRuleType type)
    {
        if (worldRules == null)
            return null;
        for (int i = 0; i < worldRules.Length; i++)
        {
            if (worldRules[i] != null && worldRules[i].RuleType == type)
                return worldRules[i];
        }
        return null;
    }

    private static string FormatDebugElapsed(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return $"{total / 60:00}:{total % 60:00}";
    }

    private void AddRunSection()
    {
        ResolveSceneReferences();
        RunStateManager runState = RunStateManager.Instance;
        RunSector sector = runState != null ? runState.CurrentSector : null;
        WorldEvent currentEvent = worldEventSpawner != null
            ? worldEventSpawner.CurrentEvent
            : null;
        EnemyHealth boss = FindAliveBoss();
        bool choiceOpen = levelChoiceManager != null &&
            levelChoiceManager.IsChoosing;
        bool completed = runFlowController != null &&
            runFlowController.IsLevelCompleted;

        AddSectionTitle("CURRENT RUN", "Read-only production state");
        AddRow("Current level",
            sector != null ? sector.SectorNumber.ToString() : "NOT AVAILABLE",
            sector != null ? successColor : warningColor,
            null, false, null);
        AddRow("Current World Rule",
            sector != null && sector.WorldRule != null
                ? GetWorldRuleName(sector.WorldRule.RuleType, sector.WorldRule)
                : "None",
            mutedColor, null, false, null);
        AddRow("Current Local Anomaly",
            sector != null && sector.LocalAnomaly != null
                ? sector.LocalAnomaly.name
                : "None",
            mutedColor, null, false, null);
        AddRow("ANOMALY STABILIZER",
            runState != null && runState.CurrentAnomalyStabilizer != null
                ? runState.CurrentAnomalyStabilizer.DisplayName
                : "NONE",
            runState != null && runState.CurrentAnomalyStabilizer != null
                ? successColor
                : mutedColor,
            null, false, null);
        AddRow("Current Event",
            currentEvent == null
                ? "None"
                : $"{GetEventDisplayName(currentEvent)} - " +
                  $"{(currentEvent.IsStarted ? "ACTIVE" : "WAITING")}",
            currentEvent != null ? successColor : mutedColor,
            null, false, null);
        AddRow("Boss alive", boss != null ? "YES" : "NO",
            boss != null ? successColor : mutedColor,
            null, false, null);
        AddRow("Level choice open", choiceOpen ? "YES" : "NO",
            choiceOpen ? successColor : mutedColor,
            null, false, null);

        AddSectionTitle("LEVEL FLOW", "Production boss-defeat lifecycle");
        bool canSpawnBoss = runTimer != null &&
            runTimer.CanDebugSpawnBoss &&
            boss == null && !completed && !choiceOpen;
        AddRow("Spawn the configured current-sector boss",
            canSpawnBoss ? "READY" : "UNAVAILABLE IN CURRENT STATE",
            canSpawnBoss ? mutedColor : warningColor,
            "SPAWN BOSS", canSpawnBoss, SpawnBoss);

        bool canKillBoss = boss != null && !completed && !choiceOpen;
        AddRow("Defeat the live boss through EnemyHealth",
            canKillBoss ? "READY" : "NO LIVE BOSS",
            canKillBoss ? mutedColor : warningColor,
            "KILL BOSS", canKillBoss, KillBoss);

        bool canComplete = runFlowController != null &&
            runFlowController.CanDebugCompleteCurrentLevel;
        AddRow("Complete current level through RunFlowController",
            canComplete ? "READY" : completed ? "ALREADY COMPLETED" : "UNAVAILABLE",
            canComplete ? successColor : warningColor,
            "COMPLETE LEVEL", canComplete, CompleteLevel);

        bool canOpenCards = runFlowController != null &&
            runFlowController.CanDebugOpenLevelChoice;
        AddRow("Skip only the post-boss presentation delay",
            choiceOpen ? "ALREADY OPEN" : canOpenCards ? "READY" :
                "COMPLETE LEVEL FIRST",
            canOpenCards ? mutedColor : warningColor,
            "OPEN LEVEL CARDS", canOpenCards, OpenLevelCards);

        bool canClearEvent = worldEventSpawner != null && currentEvent != null;
        AddRow("Current World Event",
            canClearEvent ? GetEventDisplayName(currentEvent) : "None",
            canClearEvent ? mutedColor : warningColor,
            "CLEAR CURRENT EVENT", canClearEvent, ClearWorldEvent);

        AddHint(
            "NEXT LEVEL remains the production card-confirmation action; " +
            "there is no safe no-choice transition API."
        );
        AddHint(
            "REROLL LEVEL CARDS is not exposed because LevelChoiceManager " +
            "has no safe production reroll lifecycle."
        );

        AddSectionTitle("HUD", "Runtime-only comparison setting");
        HUDManager hud = HUDManager.Instance;
        bool mapVisible = hud != null && hud.IsTacticalMapVisible;
        AddRow("TACTICAL MAP", hud == null ? "HUD NOT FOUND" :
                mapVisible ? "ON" : "OFF",
            mapVisible ? successColor : hud != null ? mutedColor : warningColor,
            mapVisible ? "TURN OFF" : "TURN ON", hud != null,
            ToggleTacticalMap);
    }

    private void AddBunkerSection()
    {
        BunkerStationProgressionService service = BunkerStationProgressionService.Instance;
        AddSectionTitle("CHARACTER STATION", "Persistent station investment");
        if (service == null ||
            !service.TryGetData(BunkerStationId.Character, out BunkerStationProgressionData data))
        {
            AddRow("Progression service", "NOT AVAILABLE", warningColor,
                null, false, null);
            return;
        }

        int level = service.GetLevel(BunkerStationId.Character);
        int cost = service.GetUpgradeCost(BunkerStationId.Character);
        int invested = service.GetInvestedGold(BunkerStationId.Character);
        int gold = CurrencyManager.Instance != null ? CurrencyManager.Instance.TotalGold : 0;
        AddRow("Current state",
            level >= data.MaxLevel ? $"LV{level} - MAX" : $"LV{level} - {invested}/{cost} INVESTED",
            successColor, null, false, null);
        AddRow("Available Gold", gold.ToString(), gold > 0 ? successColor : warningColor,
            "+1000 GOLD", CurrencyManager.Instance != null, DebugAddStationGold);
        AddRow("Set Character Station level", "LV1 / INVESTED 0", mutedColor,
            "SET LV1", true, () => DebugSetCharacterLevel(1));
        AddRow("Set Character Station level", "LV2 / INVESTED 0", mutedColor,
            "SET LV2", true, () => DebugSetCharacterLevel(2));
        AddRow("Set Character Station level", "LV3 / INVESTED 0", mutedColor,
            "SET LV3", true, () => DebugSetCharacterLevel(3));
        AddRow("Set partial investment", "0", mutedColor,
            "SET INVESTED 0", level < data.MaxLevel,
            () => DebugSetCharacterInvestment(0));
        AddRow("Set partial investment", "50% OF CURRENT COST", mutedColor,
            "SET INVESTED 50%", level < data.MaxLevel,
            DebugSetCharacterInvestmentHalf);
        AddRow("Reset Character Station", "LV1 / INVESTED 0", warningColor,
            "RESET", true, DebugResetCharacterStation);

        AddSectionTitle("CHARACTER UI", "Selection and locked-state testing");
        CharacterSelectionUI characterUi = FindFirstObjectByType<CharacterSelectionUI>();
        bool uiAvailable = characterUi != null;
        AddRow("Character Selection", uiAvailable ? "OPEN" : "NOT OPEN",
            uiAvailable ? successColor : warningColor,
            "REFRESH", uiAvailable, () => DebugRefreshCharacterUi(characterUi));
        AddCharacterDebugSelection(characterUi, "Gera");
        AddCharacterDebugSelection(characterUi, "Di-mag");
        AddCharacterDebugSelection(characterUi, "Vika");
    }

    private void AddCharacterDebugSelection(
        CharacterSelectionUI characterUi,
        string characterName)
    {
        bool canSelect = characterUi != null &&
            characterUi.CanDebugSelectCharacter(characterName);
        AddRow($"Select {characterName}", canSelect ? "AVAILABLE" : "LOCKED",
            canSelect ? mutedColor : warningColor,
            $"SELECT {characterName.ToUpperInvariant()}", canSelect,
            () => DebugSelectCharacter(characterUi, characterName));
    }

    private void DebugRefreshCharacterUi(CharacterSelectionUI characterUi)
    {
        characterUi?.DebugRefresh();
        RefreshCurrentTab();
    }

    private void DebugSelectCharacter(
        CharacterSelectionUI characterUi,
        string characterName)
    {
        characterUi?.DebugSelectCharacter(characterName);
        RefreshCurrentTab();
    }

    private void DebugAddStationGold()
    {
        BunkerStationProgressionService.Instance?.DebugAddGold();
        RefreshCurrentTab();
    }

    private void DebugSetCharacterLevel(int level)
    {
        BunkerStationProgressionService.Instance?.DebugSetStationLevel(
            BunkerStationId.Character, level);
        RefreshCurrentTab();
    }

    private void DebugSetCharacterInvestment(int amount)
    {
        BunkerStationProgressionService.Instance?.DebugSetStationInvestment(
            BunkerStationId.Character, amount);
        RefreshCurrentTab();
    }

    private void DebugSetCharacterInvestmentHalf()
    {
        BunkerStationProgressionService service = BunkerStationProgressionService.Instance;
        if (service != null)
            DebugSetCharacterInvestment(Mathf.RoundToInt(
                service.GetUpgradeCost(BunkerStationId.Character) * 0.5f));
    }

    private void DebugResetCharacterStation()
    {
        BunkerStationProgressionService.Instance?.DebugResetStation(
            BunkerStationId.Character);
        RefreshCurrentTab();
    }

    private void SpawnBoss()
    {
        runTimer?.TryDebugSpawnBoss();
        RefreshCurrentTab();
    }

    private void KillBoss()
    {
        EnemyHealth boss = FindAliveBoss();
        boss?.TakeDamage(float.MaxValue, boss.transform.position);
        RefreshCurrentTab();
    }

    private void CompleteLevel()
    {
        EnemyHealth boss = FindAliveBoss();

        if (boss != null)
            boss.TakeDamage(float.MaxValue, boss.transform.position);
        else
            runFlowController?.TryDebugCompleteCurrentLevel();

        if (runFlowController != null &&
            runFlowController.TryDebugOpenLevelChoice())
        {
            CloseMenu();
            return;
        }

        RefreshCurrentTab();
    }

    private void OpenLevelCards()
    {
        if (runFlowController != null &&
            runFlowController.TryDebugOpenLevelChoice())
        {
            CloseMenu();
            return;
        }

        RefreshCurrentTab();
    }

    private void ToggleTacticalMap()
    {
        HUDManager hud = HUDManager.Instance;

        if (hud != null)
            hud.SetTacticalMapVisible(!hud.IsTacticalMapVisible);

        RefreshCurrentTab();
    }

    private static EnemyHealth FindAliveBoss()
    {
        foreach (EnemyHealth enemy in EnemyHealth.ActiveInstances)
        {
            if (enemy != null && enemy.IsBoss && enemy.isActiveAndEnabled &&
                !enemy.IsDead)
            {
                return enemy;
            }
        }

        return null;
    }

    private void AddTabHeading(string title)
    {
        TextMeshProUGUI heading = CreateText(
            "Active Tab", contentRoot, title, 26f,
            TextAlignmentOptions.MidlineLeft, Color.white
        );
        heading.gameObject.AddComponent<LayoutElement>().preferredHeight = 42f;
    }

    private void AddWorldRulesSection()
    {
        AddSectionTitle("WORLD RULES", "Apply/Clear through WorldRuleController");
        AddRow(
            "None / Clear",
            worldRuleController == null ? "CONTROLLER NOT FOUND" :
                worldRuleController.ActiveRule == null ? "ACTIVE" : "AVAILABLE",
            worldRuleController == null ? warningColor : successColor,
            "CLEAR", worldRuleController != null, ClearWorldRule
        );

        for (int i = 0; i < DebugRuleTypes.Length; i++)
        {
            WorldRuleType type = DebugRuleTypes[i];
            WorldRuleData data = worldRules != null && i < worldRules.Length
                ? worldRules[i]
                : null;
            bool valid = worldRuleController != null && data != null &&
                data.RuleType == type;
            bool active = valid && worldRuleController.ActiveRule != null &&
                worldRuleController.ActiveRule.RuleType == type;
            string status = data == null ? "MISSING" :
                data.RuleType != type ? "NOT CONFIGURED" :
                worldRuleController == null ? "CONTROLLER NOT FOUND" :
                active ? "ACTIVE" : "AVAILABLE";
            WorldRuleData captured = data;
            AddRow(GetWorldRuleName(type, data), status,
                active ? successColor : valid ? mutedColor : warningColor,
                "APPLY", valid, () => ApplyWorldRule(captured));
        }
    }

    private void AddLocalAnomaliesSection()
    {
        AddSectionTitle("LOCAL ANOMALIES",
            "Apply or clear through LevelAnomalyController");

        if (localAnomalies != null)
        {
            for (int i = 0; i < localAnomalies.Length; i++)
            {
                LocalAnomalyData data = localAnomalies[i];

                if (data != null && !WasAnomalyAlreadyAdded(data, i))
                    AddLocalAnomalyRow(data);
            }
        }

        for (int i = 0; i < DebugAnomalyTypes.Length; i++)
        {
            LocalAnomalyType type = DebugAnomalyTypes[i];

            if (FindLocalAnomaly(type) == null)
            {
                AddRow($"{GetAnomalyTypeName(type)} - {type}",
                    "NOT CONFIGURED", warningColor, "APPLY", false, null);
            }
        }

        bool hasActive = anomalyController != null &&
            anomalyController.ActiveAnomaly != null;
        AddRow("All local anomaly zones",
            anomalyController == null ? "CONTROLLER NOT FOUND" :
                hasActive ? "ACTIVE" : "CLEAR",
            hasActive ? successColor : mutedColor,
            "CLEAR ANOMALIES", anomalyController != null,
            ClearLocalAnomalies);
        AddActiveAnomalySummary();
    }

    private void AddLocalAnomalyRow(LocalAnomalyData data)
    {
        bool valid = anomalyController != null && data.ZonePrefab != null;
        bool active = valid && anomalyController.ActiveAnomaly == data;
        string status = data.ZonePrefab == null ? "MISSING PREFAB" :
            anomalyController == null ? "CONTROLLER NOT FOUND" :
            active ? "ACTIVE" : "AVAILABLE";
        string displayName = !string.IsNullOrWhiteSpace(data.Presentation.Title)
            ? data.Presentation.Title
            : data.name;
        LocalAnomalyData captured = data;
        AddRow($"{displayName} - {GetAnomalyTypeName(data.AnomalyType)}",
            status, active ? successColor : valid ? mutedColor : warningColor,
            "APPLY", valid, () => ApplyLocalAnomaly(captured));
    }

    private void AddActiveAnomalySummary()
    {
        LocalAnomalyData active = anomalyController != null
            ? anomalyController.ActiveAnomaly
            : null;
        AddRow("Active profile",
            active != null ? GetAnomalyTypeName(active.AnomalyType) : "None",
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
            int index = activeAnomalyTypes.IndexOf(type);

            if (index >= 0)
                activeAnomalyTypeCounts[index]++;
            else
            {
                activeAnomalyTypes.Add(type);
                activeAnomalyTypeCounts.Add(1);
            }
        }

        if (activeAnomalyTypes.Count == 0)
            return "None";

        activeAnomalySummary.Clear();

        for (int i = 0; i < activeAnomalyTypes.Count; i++)
        {
            if (i > 0)
                activeAnomalySummary.Append(", ");

            activeAnomalySummary.Append(GetAnomalyTypeName(activeAnomalyTypes[i]));
            activeAnomalySummary.Append(" x");
            activeAnomalySummary.Append(activeAnomalyTypeCounts[i]);
        }

        return activeAnomalySummary.ToString();
    }

    private void AddEnemiesSection()
    {
        RemoveDestroyedDebugEnemies();
        AddSectionTitle("DEBUG SPAWN", "Existing EnemySpawner debug route");
        AddDebugEnemyRow("Turret", turretEnemyPrefab, "SPAWN TURRET");
        AddDebugEnemyRow("Eyes", eyesEnemyPrefab, "SPAWN EYES");
        AddRow("Debug-spawned enemies",
            debugEnemies.Count > 0 ? $"ACTIVE: {debugEnemies.Count}" : "CLEAR",
            debugEnemies.Count > 0 ? successColor : mutedColor,
            "CLEAR DEBUG ENEMIES", debugEnemies.Count > 0,
            ClearDebugEnemies);
    }

    private void AddWorldEventsSection()
    {
        AddSectionTitle("WORLD EVENTS", "Spawn/Clear through WorldEventSpawner");
        WorldEvent current = worldEventSpawner != null
            ? worldEventSpawner.CurrentEvent
            : null;
        AddRow($"Active event: {GetEventDisplayName(current)}",
            current != null ? "ACTIVE" : "None",
            current != null ? successColor : mutedColor,
            "CLEAR EVENT", current != null, ClearWorldEvent);

        addedEventPrefabs.Clear();
        AddEventRow<CaptureZoneEvent>("Capture Zone");
        AddEventRow<FalseSignalEvent>("False Signal");
        AddEventRow<EvacuationCorridorEvent>("Evacuation Corridor");
        AddEventRow<RescueCapsuleEvent>("Rescue Capsule");
        AddEventRow<CarrierHuntEvent>("Carrier Hunt");

        IReadOnlyList<WorldEvent> connected = worldEventSpawner != null
            ? worldEventSpawner.EventPrefabs
            : null;

        if (connected == null)
            return;

        for (int i = 0; i < connected.Count; i++)
        {
            WorldEvent prefab = connected[i];

            if (prefab != null && !addedEventPrefabs.Contains(prefab))
                AddEventRow(GetEventDisplayName(prefab), prefab);
        }
    }

    private void AddWeaponsSection()
    {
        AddSectionTitle("ACTIVE WEAPON",
            "Session-only replacement through CharacterSpawner");
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        BaseWeapon current = FindPrimaryWeapon(player);
        AddRow("Current Weapon",
            current != null ? GetWeaponName(current.weaponData) : "NOT FOUND",
            current != null ? successColor : warningColor,
            null, false, null);

        if (debugWeapons == null || debugWeapons.Length == 0)
        {
            AddHint("No WeaponData assets are assigned to the debug menu.");
            return;
        }

        for (int i = 0; i < debugWeapons.Length; i++)
        {
            WeaponData data = debugWeapons[i];

            if (data == null || WasWeaponAlreadyAdded(data, i))
                continue;

            bool active = current != null && current.weaponData == data;
            bool available = player != null && characterSpawner != null &&
                data.weaponPrefab != null;
            WeaponData captured = data;
            AddRow(GetWeaponName(data),
                active ? "ACTIVE" : data.weaponPrefab == null
                    ? "PREFAB MISSING"
                    : available ? "AVAILABLE" : "PLAYER/SPAWNER NOT FOUND",
                active ? successColor : available ? mutedColor : warningColor,
                "USE", available, () => UseWeapon(captured));
        }
    }

    private void AddUpgradesSection()
    {
        AddSectionTitle("RUN UPGRADES",
            "Production UpgradeManager apply; runtime rarity is not defined");
        AddUpgradeFilterRow();

        if (!string.IsNullOrWhiteSpace(lastUpgradeResult))
            AddHint(lastUpgradeResult);

        BuildUpgradeList();

        if (visibleUpgrades.Count == 0)
        {
            AddHint("No UpgradeData assets are available for this filter.");
            return;
        }

        RunStateManager runState = RunStateManager.Instance;

        for (int i = 0; i < visibleUpgrades.Count; i++)
        {
            UpgradeData data = visibleUpgrades[i];
            bool inPool = IsUpgradeInCurrentPool(data);

            if (!MatchesUpgradeFilter(data, inPool))
                continue;

            int stack = runState != null
                ? runState.ItemSlots.GetLevel(data)
                : 0;
            string displayName = string.IsNullOrWhiteSpace(data.upgradeName)
                ? data.name
                : data.upgradeName;
            string status = $"{data.category} - {data.upgradeType} - x{stack}";
            bool isUnlocked = UnlockProgressService.IsUnlockedNow(data.unlockData);

            if (!inPool)
                status += " - OUT OF CURRENT POOL";
            if (!isUnlocked &&
                data.unlockData != null &&
                data.unlockData.condition != null &&
                data.unlockData.condition.type == UnlockConditionType.StationLevelRequirement)
            {
                status += $" - LOCKED BY {data.unlockData.condition.stationId.ToString().ToUpperInvariant()} " +
                    $"STATION LV{Mathf.Max(1, data.unlockData.condition.requiredAmount)}";
            }

            bool canApply = upgradeManager != null &&
                GameObject.FindGameObjectWithTag("Player") != null &&
                stack < RunItemSlots.MaxItemLevel;
            UpgradeData captured = data;
            AddRow(displayName, status,
                stack > 0 ? successColor : inPool && isUnlocked ? mutedColor : warningColor,
                stack >= RunItemSlots.MaxItemLevel ? "MAX" : "APPLY",
                canApply, () => ApplyUpgrade(captured));
        }

        AddHint(
            "GRAY/BLUE/PURPLE/LEGENDARY filters are intentionally absent: " +
            "UpgradeData has no rarity enum or serialized rarity field."
        );
        AddHint(
            "RESET RUN UPGRADES is unavailable: the production systems do not " +
            "provide a safe combat-state rebuild/reset lifecycle."
        );
    }

    private void AddUpgradeFilterRow()
    {
        RectTransform row = CreateRect("Upgrade Filters", contentRoot);
        row.gameObject.AddComponent<Image>().color = rowColor;
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 50f;
        string[] labels = { "ALL", "NUMERIC", "BEHAVIOR", "OUT OF POOL" };

        for (int i = 0; i < labels.Length; i++)
        {
            int captured = i;
            RectTransform slot = CreateRect(labels[i] + " Slot", row);
            slot.anchorMin = new Vector2((float)i / labels.Length, 0f);
            slot.anchorMax = new Vector2((float)(i + 1) / labels.Length, 1f);
            slot.offsetMin = new Vector2(5f, 6f);
            slot.offsetMax = new Vector2(-5f, -6f);
            Button button = CreateButton(slot, labels[i], () =>
            {
                upgradeFilter = (UpgradeFilter)captured;
                RefreshTab(DebugTab.WeaponsAndUpgrades);
            }, 100f);
            Stretch(button.GetComponent<RectTransform>());
            Image image = button.targetGraphic as Image;

            if (image != null && captured == (int)upgradeFilter)
                image.color = new Color(0.2f, 0.73f, 0.88f, 1f);
        }
    }

    private void BuildUpgradeList()
    {
        visibleUpgrades.Clear();
        IReadOnlyList<UpgradeData> pool = upgradeManager != null
            ? upgradeManager.AllUpgrades
            : null;

        AddUniqueUpgrades(pool);
        AddUniqueUpgrades(additionalDebugUpgrades);
    }

    private void AddUniqueUpgrades(IReadOnlyList<UpgradeData> upgrades)
    {
        if (upgrades == null)
            return;

        for (int i = 0; i < upgrades.Count; i++)
        {
            UpgradeData data = upgrades[i];

            if (data != null && !visibleUpgrades.Contains(data))
                visibleUpgrades.Add(data);
        }
    }

    private bool MatchesUpgradeFilter(UpgradeData data, bool inPool)
    {
        return upgradeFilter switch
        {
            UpgradeFilter.Numeric => data.category == UpgradeCategory.Numeric,
            UpgradeFilter.Behavior => data.category == UpgradeCategory.Behavior,
            UpgradeFilter.OutOfPool => !inPool,
            _ => true
        };
    }

    private bool IsUpgradeInCurrentPool(UpgradeData target)
    {
        IReadOnlyList<UpgradeData> pool = upgradeManager != null
            ? upgradeManager.AllUpgrades
            : null;

        if (pool == null)
            return false;

        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i] == target)
                return true;
        }

        return false;
    }

    private void ApplyUpgrade(UpgradeData data)
    {
        if (upgradeManager == null || data == null)
            return;

        bool applied = upgradeManager.TryApplyDebugUpgrade(
            data,
            out ItemGrantResult result
        );
        lastUpgradeResult = applied
            ? $"Applied {GetUpgradeName(data)} through UpgradeManager ({result})."
            : $"Could not apply {GetUpgradeName(data)} ({result}).";
        RefreshCurrentTab();
    }

    private void UseWeapon(WeaponData data)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null || characterSpawner == null || data == null)
            return;

        characterSpawner.TryReplaceDebugPrimaryWeapon(
            player,
            data,
            out _
        );
        telekinesisPrototype = player.GetComponent<TelekinesisDebugPrototype>();
        RefreshTab(DebugTab.WeaponsAndUpgrades);
        RefreshTab(DebugTab.Telekinesis);
    }

    private void AddTelekinesisSection()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        PlayerHealth health = player != null
            ? player.GetComponent<PlayerHealth>()
            : null;
        BaseWeapon primary = FindPrimaryWeapon(player);
        bool available = player != null && primary != null &&
            (health == null || !health.IsDead);

        if (telekinesisPrototype == null && player != null)
            telekinesisPrototype = player.GetComponent<TelekinesisDebugPrototype>();

        TelekinesisDebugMode currentMode = telekinesisPrototype != null
            ? telekinesisPrototype.CurrentMode
            : TelekinesisDebugMode.Base;

        AddSectionTitle("SECONDARY DEBUG WEAPON",
            "Used by DUAL CONTROL / DUAL SWITCH / command modes");
        AddRow("Same as primary",
            telekinesisPrototype == null ||
                telekinesisPrototype.SecondaryDebugWeaponData == null
                    ? "SELECTED" : "AVAILABLE",
            telekinesisPrototype == null ||
                telekinesisPrototype.SecondaryDebugWeaponData == null
                    ? successColor : mutedColor,
            "USE", available, () => SelectSecondaryWeapon(null));

        if (debugWeapons != null)
        {
            for (int i = 0; i < debugWeapons.Length; i++)
            {
                WeaponData data = debugWeapons[i];

                if (data == null || WasWeaponAlreadyAdded(data, i))
                    continue;

                bool selected = telekinesisPrototype != null &&
                    telekinesisPrototype.SecondaryDebugWeaponData == data;
                WeaponData captured = data;
                AddRow(GetWeaponName(data), selected ? "SELECTED" : "AVAILABLE",
                    selected ? successColor : mutedColor,
                    "USE", available && data.weaponPrefab != null,
                    () => SelectSecondaryWeapon(captured));
            }
        }

        AddSectionTitle("TELEKINESIS DEBUG PROTOTYPE",
            $"Current: {GetTelekinesisModeName(currentMode)}");
        AddTelekinesisModeRow("Current gameplay control", "BASE",
            TelekinesisDebugMode.Base, currentMode, available);
        AddTelekinesisModeRow("Mouse position / auto target", "MANUAL POSITION",
            TelekinesisDebugMode.ManualPosition, currentMode, available);
        AddTelekinesisModeRow("Mouse aim and position / LMB fire", "MANUAL FIRE",
            TelekinesisDebugMode.ManualFire, currentMode, available);
        AddTelekinesisModeRow("Manual primary + auto secondary", "DUAL CONTROL",
            TelekinesisDebugMode.DualControl, currentMode, available);
        AddTelekinesisModeRow("TAB switches the manual weapon", "DUAL SWITCH",
            TelekinesisDebugMode.DualSwitch, currentMode, available);
        AddTelekinesisModeRow("RMB moves two auto weapons", "COMMAND POINT",
            TelekinesisDebugMode.CommandPoint, currentMode, available);
        AddTelekinesisModeRow("LMB selects priority enemy", "FOCUS TARGET",
            TelekinesisDebugMode.FocusTarget, currentMode, available);
        AddTelekinesisModeRow("RMB throws the auto weapon", "WEAPON THROW",
            TelekinesisDebugMode.WeaponThrow, currentMode, available);
        AddTelekinesisModeRow("RMB position / LMB priority", "FULL AUTO COMMAND",
            TelekinesisDebugMode.FullAutoCommand, currentMode, available);
        AddRow("Return to current gameplay",
            available ? "READY" : "PLAYER/WEAPON NOT FOUND",
            available ? mutedColor : warningColor,
            "RESET", available, ResetTelekinesisPrototype);
    }

    private void SelectSecondaryWeapon(WeaponData data)
    {
        if (!ResolveTelekinesisPrototype())
            return;

        telekinesisPrototype.SetSecondaryDebugWeapon(data);
        RefreshCurrentTab();
    }

    private void AddTelekinesisModeRow(
        string description,
        string buttonLabel,
        TelekinesisDebugMode mode,
        TelekinesisDebugMode currentMode,
        bool available)
    {
        bool active = currentMode == mode;
        AddRow(description,
            active ? "ACTIVE" : available ? "AVAILABLE" : "UNAVAILABLE",
            active ? successColor : available ? mutedColor : warningColor,
            buttonLabel, available, () => ApplyTelekinesisMode(mode));
    }

    private void ApplyTelekinesisMode(TelekinesisDebugMode mode)
    {
        if (!ResolveTelekinesisPrototype())
            return;

        telekinesisPrototype.ApplyMode(mode);
        RefreshCurrentTab();
    }

    private void ResetTelekinesisPrototype()
    {
        telekinesisPrototype?.ResetPrototype();
        RefreshCurrentTab();
    }

    private bool ResolveTelekinesisPrototype()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
            return false;

        if (telekinesisPrototype == null ||
            telekinesisPrototype.gameObject != player)
        {
            telekinesisPrototype = player.GetComponent<TelekinesisDebugPrototype>();
            telekinesisPrototype ??= player.AddComponent<TelekinesisDebugPrototype>();
        }

        telekinesisPrototype.Configure(characterSpawner);
        return telekinesisPrototype.IsAvailable;
    }

    private static BaseWeapon FindPrimaryWeapon(GameObject player)
    {
        if (player == null)
            return null;

        BaseWeapon[] weapons = player.GetComponentsInChildren<BaseWeapon>(true);

        for (int i = 0; i < weapons.Length; i++)
        {
            BaseWeapon weapon = weapons[i];

            if (weapon != null && !weapon.IsTelekinesisDebugSecondary)
                return weapon;
        }

        return null;
    }

    private void AddDebugEnemyRow(
        string displayName,
        GameObject prefab,
        string buttonLabel)
    {
        bool available = enemySpawner != null && prefab != null;
        AddRow(displayName,
            prefab == null ? "PREFAB NOT ASSIGNED" :
                enemySpawner == null ? "SPAWNER NOT FOUND" : "AVAILABLE",
            available ? successColor : warningColor,
            buttonLabel, available, () => SpawnDebugEnemy(prefab));
    }

    private void SpawnDebugEnemy(GameObject prefab)
    {
        Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (enemySpawner == null || prefab == null || player == null)
            return;

        GameObject enemy = enemySpawner.SpawnSpecificEnemyAround(
            prefab, player.position, 3f, 6f, 3f, false
        );

        if (enemy != null)
            debugEnemies.Add(enemy);

        RefreshCurrentTab();
    }

    private void ClearDebugEnemies()
    {
        for (int i = debugEnemies.Count - 1; i >= 0; i--)
        {
            if (debugEnemies[i] != null)
                Destroy(debugEnemies[i]);
        }

        debugEnemies.Clear();
        RefreshCurrentTab();
    }

    private void RemoveDestroyedDebugEnemies()
    {
        for (int i = debugEnemies.Count - 1; i >= 0; i--)
        {
            if (debugEnemies[i] == null)
                debugEnemies.RemoveAt(i);
        }
    }

    private void AddEventRow<T>(string displayName) where T : WorldEvent =>
        AddEventRow(displayName, FindEventPrefab<T>());

    private void AddEventRow(string displayName, WorldEvent prefab)
    {
        if (prefab != null && !addedEventPrefabs.Contains(prefab))
            addedEventPrefabs.Add(prefab);

        bool connected = prefab != null && worldEventSpawner != null &&
            ContainsEventPrefab(worldEventSpawner.EventPrefabs, prefab);
        bool enabledPrefab = connected &&
            worldEventSpawner.IsEventPrefabEnabled(prefab);
        bool active = enabledPrefab && worldEventSpawner.CurrentEvent != null &&
            worldEventSpawner.CurrentEvent.GetType() == prefab.GetType();
        string status = prefab == null ? "MISSING" :
            worldEventSpawner == null ? "SPAWNER NOT FOUND" :
            !connected ? "PREFAB NOT CONNECTED" :
            !enabledPrefab ? "CONNECTED BUT DISABLED" :
            active ? "ACTIVE" : "AVAILABLE";
        AddRow(displayName, status,
            active ? successColor : enabledPrefab ? mutedColor : warningColor,
            "SPAWN", enabledPrefab, () => SpawnWorldEvent(prefab));
    }

    private void SpawnWorldEvent(WorldEvent prefab)
    {
        if (worldEventSpawner == null || prefab == null)
            return;

        worldEventSpawner.SpawnDebugEvent(prefab);
        RefreshCurrentTab();
    }

    private void ClearWorldEvent()
    {
        worldEventSpawner?.ClearDebugEvent();
        RefreshCurrentTab();
    }

    private void ApplyWorldRule(WorldRuleData data)
    {
        if (worldRuleController == null || data == null)
            return;

        worldRuleController.Apply(data);
        RefreshCurrentTab();
    }

    private void ClearWorldRule()
    {
        worldRuleController?.Clear();
        RefreshCurrentTab();
    }

    private void ApplyLocalAnomaly(LocalAnomalyData data)
    {
        if (anomalyController == null || data == null || data.ZonePrefab == null)
            return;

        anomalyController.Apply(data);
        RefreshCurrentTab();
    }

    private void ClearLocalAnomalies()
    {
        anomalyController?.Clear();
        RefreshCurrentTab();
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

    private bool WasWeaponAlreadyAdded(WeaponData data, int beforeIndex)
    {
        for (int i = 0; i < beforeIndex; i++)
        {
            if (debugWeapons[i] == data)
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

    private static string GetTelekinesisModeName(TelekinesisDebugMode mode) =>
        mode switch
        {
            TelekinesisDebugMode.ManualPosition => "MANUAL POSITION",
            TelekinesisDebugMode.ManualFire => "MANUAL FIRE",
            TelekinesisDebugMode.DualControl => "DUAL CONTROL",
            TelekinesisDebugMode.DualSwitch => "DUAL SWITCH",
            TelekinesisDebugMode.CommandPoint => "COMMAND POINT",
            TelekinesisDebugMode.FocusTarget => "FOCUS TARGET",
            TelekinesisDebugMode.WeaponThrow => "WEAPON THROW",
            TelekinesisDebugMode.FullAutoCommand => "FULL AUTO COMMAND",
            _ => "BASE"
        };

    private static string GetAnomalyTypeName(LocalAnomalyType type) =>
        type == LocalAnomalyType.ExplosiveZone ? "Explosive" : type.ToString();

    private static string GetWeaponName(WeaponData data) =>
        data == null ? "None" :
            string.IsNullOrWhiteSpace(data.weaponName) ? data.name : data.weaponName;

    private static string GetUpgradeName(UpgradeData data) =>
        data == null ? "None" :
            string.IsNullOrWhiteSpace(data.upgradeName) ? data.name : data.upgradeName;

    private static string GetEventDisplayName(WorldEvent worldEvent)
    {
        if (worldEvent == null)
            return "None";
        if (worldEvent is CaptureZoneEvent) return "Capture Zone";
        if (worldEvent is FalseSignalEvent) return "False Signal";
        if (worldEvent is EvacuationCorridorEvent) return "Evacuation Corridor";
        if (worldEvent is RescueCapsuleEvent) return "Rescue Capsule";
        if (worldEvent is CarrierHuntEvent) return "Carrier Hunt";
        return string.IsNullOrWhiteSpace(worldEvent.EventDisplayName)
            ? worldEvent.name
            : worldEvent.EventDisplayName;
    }

    private static bool ContainsEventPrefab(
        IReadOnlyList<WorldEvent> prefabs,
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

    private static string GetWorldRuleName(WorldRuleType type, WorldRuleData data) =>
        data != null && !string.IsNullOrWhiteSpace(data.DisplayName)
            ? data.DisplayName
            : type.ToString();

    private void AddSectionTitle(string title, string subtitle)
    {
        RectTransform section = CreateRect(title, contentRoot);
        section.gameObject.AddComponent<LayoutElement>().preferredHeight = 62f;
        section.gameObject.AddComponent<Image>().color =
            new Color(accentColor.r, accentColor.g, accentColor.b, 0.18f);
        TextMeshProUGUI text = CreateText(
            "Label", section,
            $"<b>{title}</b>\n<size=16><color=#A6AFBC>{subtitle}</color></size>",
            22f, TextAlignmentOptions.MidlineLeft, Color.white
        );
        Stretch(text.rectTransform, 16f, 12f, 7f, 7f);
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
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 54f;

        float buttonWidth = GetButtonWidth(buttonLabel);
        float rightPadding = string.IsNullOrEmpty(buttonLabel)
            ? 18f
            : buttonWidth + 36f;
        TextMeshProUGUI labelText = CreateText(
            "Name", row, label, 19f,
            TextAlignmentOptions.MidlineLeft, Color.white
        );
        labelText.rectTransform.anchorMin = Vector2.zero;
        labelText.rectTransform.anchorMax = new Vector2(0.48f, 1f);
        labelText.rectTransform.offsetMin = new Vector2(16f, 0f);
        labelText.rectTransform.offsetMax = Vector2.zero;

        TextMeshProUGUI statusText = CreateText(
            "Status", row, status, 15f,
            TextAlignmentOptions.MidlineRight, statusColor
        );
        statusText.rectTransform.anchorMin = new Vector2(0.43f, 0f);
        statusText.rectTransform.anchorMax = Vector2.one;
        statusText.rectTransform.offsetMin = Vector2.zero;
        statusText.rectTransform.offsetMax = new Vector2(-rightPadding, 0f);

        if (string.IsNullOrEmpty(buttonLabel))
            return;

        Button button = CreateButton(
            row, buttonLabel, action, buttonWidth, buttonEnabled
        );
        RectTransform buttonRect = button.GetComponent<RectTransform>();
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(1f, 0.5f);
        buttonRect.pivot = new Vector2(1f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(-12f, 0f);
        buttonRect.sizeDelta = new Vector2(buttonWidth, 38f);
    }

    private static float GetButtonWidth(string label) => label switch
    {
        "CLEAR DEBUG ENEMIES" => 218f,
        "CLEAR ANOMALIES" => 176f,
        "MANUAL POSITION" => 184f,
        "MANUAL FIRE" => 164f,
        "DUAL CONTROL" => 164f,
        "DUAL SWITCH" => 164f,
        "COMMAND POINT" => 174f,
        "FOCUS TARGET" => 164f,
        "WEAPON THROW" => 174f,
        "FULL AUTO COMMAND" => 220f,
        "SPAWN TURRET" => 166f,
        "SPAWN EYES" => 150f,
        "SPAWN BOSS" => 156f,
        "KILL BOSS" => 146f,
        "COMPLETE LEVEL" => 188f,
        "OPEN LEVEL CARDS" => 210f,
        "SET INVESTED 0" => 190f,
        "SET INVESTED 50%" => 214f,
        "+1000 GOLD" => 150f,
        "SELECT GERA" => 164f,
        "SELECT DI-MAG" => 184f,
        "SELECT VIKA" => 164f,
        "CLEAR CURRENT EVENT" => 228f,
        "CLEAR EVENT" => 150f,
        "TURN OFF" => 130f,
        "TURN ON" => 130f,
        _ => 116f
    };

    private void AddHint(string message)
    {
        TextMeshProUGUI hint = CreateText(
            "Hint", contentRoot, message, 15f,
            TextAlignmentOptions.Center, mutedColor
        );
        hint.textWrappingMode = TextWrappingModes.Normal;
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
            "Label", rect, label, 16f,
            TextAlignmentOptions.Center, Color.white
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
