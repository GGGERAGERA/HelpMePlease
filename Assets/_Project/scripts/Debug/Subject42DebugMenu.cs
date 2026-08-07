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
        Telekinesis,
        SectorTest
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
        Background,
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
        "TELEKINESIS",
        "ТЕСТ СЕКТОРА"
    };

    private static readonly string[] SandboxTabLabels =
    {
        "ИССЛЕДОВАНИЕ",
        "ВИЗУАЛ СЕКТОРА",
        "ФОН / НАБЛЮДАТЕЛЬ",
        "ЧИТАЕМОСТЬ",
        "ГЛОБАЛЬНОЕ ПРАВИЛО",
        "АНОМАЛИИ",
        "ОРУЖИЕ И СПОСОБНОСТИ"
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
    private ProductionSectorDebugController productionSectorDebug;
    private GiantObserverBackgroundController giantObserverController;
    private AnomalySiteDebugSelector anomalySiteSelector;
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
        EnsureProductionSectorDebug();
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
        EnvironmentReadabilityDebugController readability,
        GiantObserverBackgroundController giantObserver,
        AnomalySiteDebugSelector siteSelector)
    {
        sandboxLabMode = true;
        explorationSector = exploration;
        sectorVisualController = sectorVisual;
        anomalyPowerController = powers;
        trajectoryPreview = trajectory;
        eventStatusOverlay = eventOverlay;
        readabilityController = readability;
        giantObserverController = giantObserver;
        anomalySiteSelector = siteSelector;
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

    private void EnsureProductionSectorDebug()
    {
        if (sandboxLabMode || productionSectorDebug != null)
            return;

        productionSectorDebug = GetComponent<ProductionSectorDebugController>();
        productionSectorDebug ??=
            gameObject.AddComponent<ProductionSectorDebugController>();
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
            "Title", header, "SUBJECT#42 — ОТЛАДОЧНОЕ МЕНЮ", 28f,
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
            TextMeshProUGUI tabText = button.GetComponentInChildren<TextMeshProUGUI>();
            if (tabText != null && (sandboxLabMode || labels.Length > 7))
                tabText.fontSize = sandboxLabMode ? 13f : 12f;
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
            case SandboxTab.Background:
                AddSandboxBackgroundSection();
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
        AddHint("F1 закрывает меню. Пока меню открыто, игровой процесс на паузе.");
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
            case DebugTab.SectorTest:
                AddProductionSectorTestSection();
                break;
        }

        AddHint("F1 закрывает меню. Пока меню открыто, игровой процесс на паузе.");
    }

    private void AddSandboxExplorationSection()
    {
        bool available = explorationSector != null;
        AddSectionTitle("СЕССИЯ СЕКТОРА",
            "Управление только текущим Exploration Sandbox");
        AddRow("НОВАЯ РАСКЛАДКА", available ? "ГОТОВО" : "НЕ НАЙДЕНО",
            available ? mutedColor : warningColor,
            "СОЗДАТЬ", available, () =>
            {
                explorationSector.NewLayout();
                RefreshCurrentTab();
            });
        AddHint("Что делает: генерирует новые позиции игрока, сайтов и выхода; " +
            "полностью перезапускает текущий Sandbox-сектор.");
        AddRow("СБРОСИТЬ СЕКТОР", available ? "СОХРАНИТ РАСКЛАДКУ" : "НЕ НАЙДЕНО",
            available ? mutedColor : warningColor,
            "СБРОСИТЬ", available, () =>
            {
                explorationSector.ResetSector();
                RefreshCurrentTab();
            });
        AddHint("Что делает: перезапускает врагов, события и прогресс сайтов, " +
            "не меняя сгенерированные позиции.");
        AddToggleRow("БЕССМЕРТИЕ",
            available && explorationSector.InvulnerabilityEnabled,
            available,
            () => explorationSector.SetInvulnerability(
                !explorationSector.InvulnerabilityEnabled));
        AddHint("Игрок не теряет HP и не умирает. Гравитация, knockback и hit feedback " +
            "продолжают работать. Только Exploration Sandbox.");
        AddToggleRow("HUD ИССЛЕДОВАНИЯ",
            available && explorationSector.HudVisible,
            available,
            () => explorationSector.SetHudVisible(!explorationSector.HudVisible));
        AddHint("Показывает runtime-панели Exploration: threat, сайты, powers и FPS.");
        AddToggleRow("DEBUG-КАРТА",
            available && explorationSector.MapVisible,
            available,
            () => explorationSector.SetMapVisible(!explorationSector.MapVisible));
        AddHint("Показывает игрока, сайты и выход. Это диагностический overlay, " +
            "не gameplay-механика.");

        AddSectionTitle("ЛИМИТ ВРАГОВ",
            "Меняет только максимум одновременно живых врагов; используется для FPS-теста");
        AddOptionRow("50%", available && Mathf.Approximately(
            explorationSector.EnemyCapScale, 0.5f), available,
            () => explorationSector.SetEnemyCapScale(0.5f));
        AddOptionRow("75%", available && Mathf.Approximately(
            explorationSector.EnemyCapScale, 0.75f), available,
            () => explorationSector.SetEnemyCapScale(0.75f));
        AddOptionRow("100%", available && Mathf.Approximately(
            explorationSector.EnemyCapScale, 1f), available,
            () => explorationSector.SetEnemyCapScale(1f));
        AddRow("УБИТЬ ВСЕХ ВРАГОВ", $"ЖИВЫХ: {EnemyHealth.ActiveInstances.Count}",
            mutedColor, "УБИТЬ", anomalyPowerController != null,
            () => anomalyPowerController.KillAllEnemiesDebug());
        AddHint("Вызывает EnemyHealth.TakeDamage для каждого живого enemy; " +
            "сохраняет обычный death lifecycle.");

        if (!available)
            return;
        AddSectionTitle("ДИАГНОСТИКА", "Фактическое состояние runtime-систем");
        AddRow("Exploration", explorationSector.IsRunning ? "АКТИВНО" :
                explorationSector.IsCompleted ? "ЗАВЕРШЕНО" : "НЕАКТИВНО",
            explorationSector.IsRunning ? successColor : warningColor,
            null, false, null);
        AddRow("EnemySpawner", $"АКТИВЕН · {explorationSector.EnemiesAlive} / " +
                $"{explorationSector.CurrentEnemyCap}", mutedColor,
            null, false, null);
        AddRow("Уровень угрозы", explorationSector.ThreatLevel.ToString(), mutedColor,
            null, false, null);
        AddRow("Прошло времени", FormatDebugElapsed(explorationSector.Elapsed), mutedColor,
            null, false, null);
        AddRow("Сайтов завершено", $"{explorationSector.SitesCompleted}/4", mutedColor,
            null, false, null);
    }

    private void AddSandboxSectorVisualSection()
    {
        bool available = sectorVisualController != null;
        string active = available
            ? GetRussianSectorPresetName(sectorVisualController.CurrentPreset)
            : "НЕ НАЙДЕНО";
        AddSectionTitle("ВИЗУАЛЬНЫЙ ПРЕСЕТ", $"Активен: {active}");
        AddSectorPresetRow(SectorVisualDebugController.SectorPreset.Calibration);
        AddSectorPresetRow(SectorVisualDebugController.SectorPreset.CorruptedTest);
        AddSectorPresetRow(SectorVisualDebugController.SectorPreset.Containment);
        AddSectorPresetRow(SectorVisualDebugController.SectorPreset.SystemFailure);
        AddSectorPresetRow(SectorVisualDebugController.SectorPreset.CoreFinalTest);

        AddHint("Что делает: меняет только цвет и оформление debug-сектора. " +
            "Не меняет threat, врагов, аномалии или правила мира.");
        AddSectionTitle("СЛОИ", "Только визуал; нет collider или gameplay-state");
        AddToggleRow("СЕТКА", available && sectorVisualController.GridVisible,
            available, () => sectorVisualController.SetGridVisible(
                !sectorVisualController.GridVisible));
        AddToggleRow("ЛИНИИ СЕКТОРА",
            available && sectorVisualController.SectorLinesVisible,
            available, () => sectorVisualController.SetSectorLinesVisible(
                !sectorVisualController.SectorLinesVisible));
        AddToggleRow("DEBUG-ГРАНИЦЫ",
            available && sectorVisualController.BoundariesVisible,
            available, () => sectorVisualController.SetBoundariesVisible(
                !sectorVisualController.BoundariesVisible));
        AddToggleRow("HUD ИССЛЕДОВАНИЯ",
            explorationSector != null && explorationSector.HudVisible,
            explorationSector != null,
            () => explorationSector.SetHudVisible(!explorationSector.HudVisible));
    }

    private void AddSectorPresetRow(SectorVisualDebugController.SectorPreset preset)
    {
        bool available = sectorVisualController != null;
        bool selected = available && sectorVisualController.CurrentPreset == preset;
        AddRow(GetRussianSectorPresetName(preset),
            selected ? "ВЫБРАНО" : "ДОСТУПНО",
            selected ? successColor : mutedColor,
            ((int)preset).ToString(), available, () =>
            {
                sectorVisualController.ApplyPreset(preset);
                RefreshCurrentTab();
            });
    }

    private void AddSandboxBackgroundSection()
    {
        bool available = giantObserverController != null;
        bool observerOn = available && giantObserverController.ObserverEnabled;
        bool visible = available && giantObserverController.IsVisible;

        AddSectionTitle("ГИГАНТСКИЙ НАБЛЮДАТЕЛЬ",
            "Perspective background camera + orthographic gameplay camera");
        AddToggleRow("ГИГАНТСКИЙ НАБЛЮДАТЕЛЬ", observerOn, available,
            () => giantObserverController.SetObserverEnabled(
                !giantObserverController.ObserverEnabled));
        AddHint("Sandbox-only visual prototype. По умолчанию выключен.");
        AddToggleRow("АВТОМАТИЧЕСКОЕ ПОЯВЛЕНИЕ",
            available && giantObserverController.AutoTrigger,
            available,
            () => giantObserverController.SetAutoTrigger(
                !giantObserverController.AutoTrigger));
        AddHint("Запускает фоновое появление через выбранный случайный интервал.");
        AddRow("ПОКАЗАТЬ СЕЙЧАС", visible ? "СОБЫТИЕ АКТИВНО" :
                observerOn ? "ГОТОВО" : "СНАЧАЛА ВКЛЮЧИТЕ НАБЛЮДАТЕЛЯ",
            visible ? successColor : observerOn ? mutedColor : warningColor,
            "ПОКАЗАТЬ", observerOn && !visible,
            () =>
            {
                giantObserverController.TriggerNow();
                RefreshCurrentTab();
            });
        AddHint("Событие: дальнее проявление → приближение по Z → наблюдение → исчезновение.");

        AddSectionTitle("ИНТЕРВАЛ", available
            ? $"Активен: {giantObserverController.IntervalMin:0}-" +
              $"{giantObserverController.IntervalMax:0} сек между запусками"
            : "Controller не найден");
        AddObserverIntervalRow(10f, 15f);
        AddObserverIntervalRow(15f, 25f);
        AddObserverIntervalRow(20f, 30f);

        AddSectionTitle("ПЕРСПЕКТИВА", available
            ? $"FOV: {GetObserverFov(giantObserverController.Perspective):0}°"
            : "Controller не найден");
        AddObserverPerspectiveRow(
            GiantObserverBackgroundController.PerspectivePreset.Narrow);
        AddObserverPerspectiveRow(
            GiantObserverBackgroundController.PerspectivePreset.Normal);
        AddObserverPerspectiveRow(
            GiantObserverBackgroundController.PerspectivePreset.Wide);

        AddSectionTitle("СТАРТОВАЯ ДИСТАНЦИЯ", "Реальная дальняя позиция робота по Z");
        AddObserverFarDistanceRow(
            GiantObserverBackgroundController.FarDistancePreset.Far);
        AddObserverFarDistanceRow(
            GiantObserverBackgroundController.FarDistancePreset.VeryFar);

        AddSectionTitle("КОНЕЧНАЯ ДИСТАНЦИЯ", "Ближняя позиция без анимации масштаба");
        AddObserverNearDistanceRow(
            GiantObserverBackgroundController.NearDistancePreset.Close);
        AddObserverNearDistanceRow(
            GiantObserverBackgroundController.NearDistancePreset.VeryClose);

        AddSectionTitle("СВЕТ", "Холодный key + rim свет на слое Observer3D");
        AddObserverIntensityRow(
            GiantObserverBackgroundController.ObserverIntensity.Low);
        AddObserverIntensityRow(
            GiantObserverBackgroundController.ObserverIntensity.Normal);
        AddObserverIntensityRow(
            GiantObserverBackgroundController.ObserverIntensity.High);

        AddToggleRow("ПОКАЗЫВАТЬ ПОСТОЯННО",
            available && giantObserverController.ShowConstantly,
            available,
            () => giantObserverController.SetShowConstantly(
                !giantObserverController.ShowConstantly));
        AddHint("Удерживает наблюдателя в ближней точке для визуальной настройки.");

        AddRow("ДИАГНОСТИКА: ПОКАЗАТЬ РОБОТА",
            available && giantObserverController.CurrentStateName == "FORCE_VISIBLE"
                ? "FORCE VISIBLE ACTIVE" : "DISTANCE 25 · HIGH LIGHT",
            available && giantObserverController.CurrentStateName == "FORCE_VISIBLE"
                ? successColor : mutedColor,
            "ПОКАЗАТЬ", available, () =>
            {
                giantObserverController.ShowForceVisible();
                RefreshCurrentTab();
            });
        AddRow("ТЕСТ BACKGROUND CAMERA",
            available && giantObserverController.CurrentStateName == "BACKGROUND_CAMERA_TEST"
                ? "MAGENTA ACTIVE" : "MAGENTA · 2 СЕК",
            available && giantObserverController.CurrentStateName == "BACKGROUND_CAMERA_TEST"
                ? successColor : mutedColor,
            "ТЕСТ", available, () =>
            {
                giantObserverController.StartBackgroundCameraTest();
                RefreshCurrentTab();
            });

        AddSectionTitle("ДИАГНОСТИКА", available
            ? $"Camera Stack: {(giantObserverController.CameraStackOk ? "OK" : "ERROR")}  |  " +
              $"Prefab: {(giantObserverController.PrefabLoaded ? "LOADED" : "MISSING")}"
            : "Camera Stack: ERROR | Prefab: MISSING");
        AddHint(available
            ? $"Background Camera: {(giantObserverController.BackgroundCameraActive ? "ACTIVE" : "DISABLED")}  |  " +
              $"Background Renderer: {(giantObserverController.ForwardRendererOk ? "Universal Forward" : "ERROR")}  |  " +
              $"Gameplay Renderer: {(giantObserverController.GameplayForwardRendererOk ? "Universal Forward" : "2D / RESTORED")}"
            : "Background Camera: DISABLED | Renderers: ERROR");
        WorldRuleData observerActiveRule = worldRuleController != null
            ? worldRuleController.ActiveRule : null;
        AddHint("Active World Rule: " + (observerActiveRule != null
            ? observerActiveRule.RuleType.ToString().ToUpperInvariant()
            : "NONE"));
        AddHint(available
            ? $"Culling Mask: Observer3D {(giantObserverController.ObserverMaskOk ? "YES" : "NO")}  |  " +
              $"Observer Layer: {giantObserverController.ObserverLayerIndex}  |  " +
              $"Robot Layers: {(giantObserverController.RobotLayersOk ? "OK" : "MISMATCH")}"
            : "Culling Mask: Observer3D NO | Observer Layer: 31 | Robot Layers: MISMATCH");
        AddHint(available
            ? $"Robot Active: {(giantObserverController.RobotActive ? "YES" : "NO")}  |  " +
              $"MeshRenderers: {giantObserverController.RendererCount} / " +
              $"{giantObserverController.EnabledRendererCount} enabled"
            : "Robot Active: NO | MeshRenderers: 0 / 0 enabled");
        AddHint(available
            ? $"Materials: {giantObserverController.MaterialCount}  |  " +
              $"Unsupported shaders: {giantObserverController.UnsupportedShaderCount}"
            : "Materials: 0 | Unsupported shaders: 0");
        if (available)
        {
            Vector3 position = giantObserverController.RobotWorldPosition;
            Vector3 forward = giantObserverController.BackgroundCameraForward;
            AddHint($"Robot World Position: {position.x:0.0} {position.y:0.0} {position.z:0.0}");
            AddHint($"Camera Forward: {forward.x:0.00} {forward.y:0.00} {forward.z:0.00}");
            AddHint($"Distance: {giantObserverController.CurrentDistance:0.0}  |  " +
                $"Near/Far: {giantObserverController.BackgroundNearClip:0.0} / " +
                $"{giantObserverController.BackgroundFarClip:0}");
            AddHint("Is Robot In Front Of Camera: " +
                (giantObserverController.RobotInFrontOfCamera ? "YES" : "NO"));
            AddHint($"Estimated Screen Coverage: " +
                $"{giantObserverController.RobotScreenCoverage * 100f:0.0}%");
            AddHint($"State: {giantObserverController.CurrentStateName}  |  " +
                $"Last Render State: {giantObserverController.LastRenderState}");
        }
        else
        {
            AddHint("Robot World Position: 0 0 0 | Camera Forward: 0 0 1");
            AddHint("Distance: 0 | Near/Far: 0 / 0 | Is Robot In Front Of Camera: NO");
            AddHint("Estimated Screen Coverage: 0% | Last Render State: CAMERA OFF");
        }
    }

    private void AddObserverIntervalRow(float minimum, float maximum)
    {
        bool available = giantObserverController != null;
        bool selected = available &&
            Mathf.Approximately(giantObserverController.IntervalMin, minimum) &&
            Mathf.Approximately(giantObserverController.IntervalMax, maximum);
        AddRow($"{minimum:0}-{maximum:0} сек",
            selected ? "ВЫБРАНО" : "ДОСТУПНО",
            selected ? successColor : mutedColor,
            "ВЫБРАТЬ", available, () =>
            {
                giantObserverController.SetInterval(minimum, maximum);
                RefreshCurrentTab();
            });
    }

    private static string GetRussianSectorPresetName(
        SectorVisualDebugController.SectorPreset preset) => preset switch
    {
        SectorVisualDebugController.SectorPreset.CorruptedTest => "ИСКАЖЁННЫЙ ТЕСТ",
        SectorVisualDebugController.SectorPreset.Containment => "УДЕРЖАНИЕ",
        SectorVisualDebugController.SectorPreset.SystemFailure => "СБОЙ СИСТЕМЫ",
        SectorVisualDebugController.SectorPreset.CoreFinalTest => "ФИНАЛЬНЫЙ ТЕСТ ЯДРА",
        _ => "КАЛИБРОВКА"
    };

    private void AddObserverIntensityRow(
        GiantObserverBackgroundController.ObserverIntensity value)
    {
        bool available = giantObserverController != null;
        bool selected = available && giantObserverController.Intensity == value;
        AddRow(GetRussianObserverIntensity(value),
            selected ? "ВЫБРАНО" : "ДОСТУПНО",
            selected ? successColor : mutedColor,
            "ВЫБРАТЬ", available, () =>
            {
                giantObserverController.SetIntensity(value);
                RefreshCurrentTab();
            });
    }

    private static string GetRussianObserverIntensity(
        GiantObserverBackgroundController.ObserverIntensity value) => value switch
    {
        GiantObserverBackgroundController.ObserverIntensity.Low => "LOW",
        GiantObserverBackgroundController.ObserverIntensity.High => "HIGH",
        _ => "NORMAL"
    };

    private void AddObserverPerspectiveRow(
        GiantObserverBackgroundController.PerspectivePreset value)
    {
        bool available = giantObserverController != null;
        bool selected = available && giantObserverController.Perspective == value;
        AddRow($"FOV {GetObserverFov(value):0}°",
            selected ? "ВЫБРАНО" : "ДОСТУПНО",
            selected ? successColor : mutedColor,
            "ВЫБРАТЬ", available, () =>
            {
                giantObserverController.SetPerspective(value);
                RefreshCurrentTab();
            });
    }

    private static float GetObserverFov(
        GiantObserverBackgroundController.PerspectivePreset value) => value switch
    {
        GiantObserverBackgroundController.PerspectivePreset.Narrow => 30f,
        GiantObserverBackgroundController.PerspectivePreset.Wide => 50f,
        _ => 40f
    };

    private void AddObserverFarDistanceRow(
        GiantObserverBackgroundController.FarDistancePreset value)
    {
        bool available = giantObserverController != null;
        bool selected = available && giantObserverController.FarPreset == value;
        string label = value == GiantObserverBackgroundController.FarDistancePreset.VeryFar
            ? "VERY FAR" : "FAR";
        AddRow(label, selected ? "ВЫБРАНО" : "ДОСТУПНО",
            selected ? successColor : mutedColor,
            "ВЫБРАТЬ", available, () =>
            {
                giantObserverController.SetFarDistance(value);
                RefreshCurrentTab();
            });
    }

    private void AddObserverNearDistanceRow(
        GiantObserverBackgroundController.NearDistancePreset value)
    {
        bool available = giantObserverController != null;
        bool selected = available && giantObserverController.NearPreset == value;
        string label = value == GiantObserverBackgroundController.NearDistancePreset.VeryClose
            ? "VERY CLOSE" : "CLOSE";
        AddRow(label, selected ? "ВЫБРАНО" : "ДОСТУПНО",
            selected ? successColor : mutedColor,
            "ВЫБРАТЬ", available, () =>
            {
                giantObserverController.SetNearDistance(value);
                RefreshCurrentTab();
            });
    }

    private void AddSandboxVisualReadabilitySection()
    {
        bool controllerAvailable = readabilityController != null;
        bool canEnable = controllerAvailable && readabilityController.CanEnable;
        bool testEnabled = controllerAvailable && readabilityController.TestEnabled;
        AddSectionTitle("ТЕСТ ЧИТАЕМОСТИ ОКРУЖЕНИЯ",
            "Sandbox-only обработка environment; при выключении сцена не изменяется");
        AddRow("ТЕСТ ЧИТАЕМОСТИ",
            testEnabled ? "ВКЛЮЧЕНО" : "ВЫКЛЮЧЕНО",
            testEnabled ? successColor : mutedColor,
            "ПЕРЕКЛЮЧИТЬ",
            canEnable,
            () =>
            {
                readabilityController.SetTestEnabled(!readabilityController.TestEnabled);
                RefreshCurrentTab();
            });
        AddHint(testEnabled
            ? "Включено: выбранный preset временно применяется к копии окружения."
            : "Выключено. Окружение не изменяется.");

        string active = testEnabled
            ? GetRussianReadabilityPreset(readabilityController.Preset)
            : "ВЫКЛЮЧЕНО";
        AddSectionTitle(
            "ПРЕСЕТ ЧИТАЕМОСТИ",
            $"Активен: {active} | Renderer-объектов: " +
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

        AddHint("ОРИГИНАЛ — без обработки. ПРИГЛУШЁННЫЙ МИР снижает яркость и " +
            "насыщенность. ВЫСОКИЙ КОНТРАСТ сильнее уводит environment назад. " +
            "ТЁМНЫЙ МИР — экстремальное затемнение.");
        AddSectionTitle("ЯРКОСТЬ ДЕКОРАЦИЙ",
            "Деревья, растения, декоративные props и их тени");
        AddReadabilityValueRow("Декорации 100%", 1f,
            testEnabled ? readabilityController.PropsIntensity : 1f,
            testEnabled, value => readabilityController.SetPropsIntensity(value));
        AddReadabilityValueRow("Декорации 75%", 0.75f,
            testEnabled ? readabilityController.PropsIntensity : 1f,
            testEnabled, value => readabilityController.SetPropsIntensity(value));
        AddReadabilityValueRow("Декорации 50%", 0.5f,
            testEnabled ? readabilityController.PropsIntensity : 1f,
            testEnabled, value => readabilityController.SetPropsIntensity(value));
        AddReadabilityValueRow("Декорации 25%", 0.25f,
            testEnabled ? readabilityController.PropsIntensity : 1f,
            testEnabled, value => readabilityController.SetPropsIntensity(value));

        AddSectionTitle("АКЦЕНТ АНОМАЛИЙ",
            "Только яркость, alpha и ширина линий; radius не меняется");
        AddReadabilityValueRow("Аномалии 100%", 1f,
            testEnabled ? readabilityController.AnomalyEmphasis : 1f,
            testEnabled, value => readabilityController.SetAnomalyEmphasis(value));
        AddReadabilityValueRow("Аномалии 125%", 1.25f,
            testEnabled ? readabilityController.AnomalyEmphasis : 1f,
            testEnabled, value => readabilityController.SetAnomalyEmphasis(value));
        AddReadabilityValueRow("Аномалии 150%", 1.5f,
            testEnabled ? readabilityController.AnomalyEmphasis : 1f,
            testEnabled, value => readabilityController.SetAnomalyEmphasis(value));

        AddSectionTitle("ЧИТАЕМОСТЬ ВРАГОВ", "Слабое цветовое отделение от environment");
        AddOptionRow("Подсветка врагов ВЫКЛ",
            testEnabled && !readabilityController.EnemyHighlight,
            testEnabled,
            () => readabilityController.SetEnemyHighlight(false));
        AddOptionRow("Подсветка врагов СЛАБАЯ",
            testEnabled && readabilityController.EnemyHighlight,
            testEnabled,
            () => readabilityController.SetEnemyHighlight(true));

        AddRow("СБРОСИТЬ ВИЗУАЛ", testEnabled ? "ВОССТАНОВИТЬ ОРИГИНАЛ" : "ВЫКЛЮЧЕНО",
            testEnabled ? warningColor : mutedColor,
            "СБРОСИТЬ", testEnabled, () =>
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
        AddRow(GetRussianReadabilityPreset(value),
            selected ? "ВЫБРАНО" : "ДОСТУПНО",
            selected ? successColor : mutedColor,
            "ВЫБРАТЬ", available, () =>
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
        AddRow(label, selected ? "ВЫБРАНО" : "ДОСТУПНО",
            selected ? successColor : mutedColor,
            "ВЫБРАТЬ", available, () =>
            {
                setter?.Invoke(value);
                RefreshCurrentTab();
            });
    }

    private static string GetRussianReadabilityPreset(
        EnvironmentReadabilityDebugController.ReadabilityPreset value) => value switch
    {
        EnvironmentReadabilityDebugController.ReadabilityPreset.MutedWorld =>
            "ПРИГЛУШЁННЫЙ МИР",
        EnvironmentReadabilityDebugController.ReadabilityPreset.HighGameplayContrast =>
            "ВЫСОКИЙ КОНТРАСТ GAMEPLAY",
        EnvironmentReadabilityDebugController.ReadabilityPreset.DarkWorld =>
            "ТЁМНЫЙ МИР",
        _ => "ОРИГИНАЛ"
    };

    private void AddSandboxWorldRuleSection()
    {
        WorldRuleData active = worldRuleController != null
            ? worldRuleController.ActiveRule : null;
        AddSectionTitle("ГЛОБАЛЬНОЕ ПРАВИЛО",
            $"Активно: {(active != null ? GetRussianWorldRuleName(active.RuleType) : "НЕТ")}");
        AddRow("БЕЗ ПРАВИЛА", active == null ? "ВЫБРАНО" : "ДОСТУПНО",
            active == null ? successColor : mutedColor,
            "ПРИМЕНИТЬ", worldRuleController != null, ClearWorldRule);
        AddHint("Отключает активное правило через WorldRuleController и " +
            "восстанавливает его visual/gameplay modifiers.");

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
            AddRow(GetRussianWorldRuleName(order[i]), data == null ? "ASSET НЕ НАЗНАЧЕН" :
                selected ? "ВЫБРАНО" : "ДОСТУПНО",
                data == null ? warningColor : selected ? successColor : mutedColor,
                "ПРИМЕНИТЬ", worldRuleController != null && data != null,
                () => ApplyWorldRule(captured));
            AddHint(GetWorldRuleDebugDescription(order[i], data));
        }
        AddRow("ОЧИСТИТЬ ПРАВИЛО", active != null ? "АКТИВНО" : "УЖЕ ЧИСТО",
            active != null ? warningColor : mutedColor,
            "ОЧИСТИТЬ", worldRuleController != null, ClearWorldRule);
    }

    private static string GetRussianWorldRuleName(WorldRuleType type) => type switch
    {
        WorldRuleType.Snow => "СНЕГ",
        WorldRuleType.Rain => "ДОЖДЬ",
        WorldRuleType.Wind => "ВЕТЕР",
        WorldRuleType.Darkness => "ТЕМНОТА",
        WorldRuleType.Condensation => "КОНДЕНСАТ",
        WorldRuleType.Golden => "ЗОЛОТОЙ",
        _ => type.ToString()
    };

    private static string GetWorldRuleDebugDescription(
        WorldRuleType type,
        WorldRuleData data)
    {
        if (data == null)
            return "Недоступно: соответствующий WorldRuleData не назначен в GameplaySandbox.";

        string modifiers = $"Player speed ×{data.PlayerMoveSpeedMultiplier:0.##}; " +
            $"enemy speed ×{data.EnemyMoveSpeedMultiplier:0.##}; " +
            $"enemy HP ×{data.EnemyHealthMultiplier:0.##}; " +
            $"spawn pressure ×{data.SpawnPressureMultiplier:0.##}.";
        return type switch
        {
            WorldRuleType.Snow => "Снег и цикл метели с существующими modifiers. " + modifiers,
            WorldRuleType.Rain => "Дождевой visual и modifiers из WorldRuleData. " + modifiers,
            WorldRuleType.Wind => "Поток сдвигает player, enemies и совместимые projectiles. " + modifiers,
            WorldRuleType.Darkness => "Ограничивает видимость; выстрелы и свет кратко раскрывают область. " + modifiers,
            WorldRuleType.Condensation => "Экран запотевает; слой очищается движением мыши. " + modifiers,
            WorldRuleType.Golden => "Golden enemies получают отдельные HP/reward modifiers и роняют монеты. " +
                "Постоянная Sandbox-валюта зависит от существующего CurrencyManager.",
            _ => modifiers
        };
    }

    private void AddSandboxAnomalySection()
    {
        bool trajectoryAvailable = trajectoryPreview != null;
        AddSectionTitle("ТЕКУЩИЙ EXPLORATION",
            "В основном секторе используется только специальный сайт GRAVITY");
        AddRow("Special Site", explorationSector != null && explorationSector.IsRunning
                ? "GRAVITY · АКТИВЕН" : "EXPLORATION НЕАКТИВЕН",
            explorationSector != null && explorationSector.IsRunning
                ? successColor : warningColor, null, false, null);
        AddHint("Electric и Beam не входят в текущую Exploration-раскладку. " +
            "Их controllers исправны и запускаются отдельными тестами ниже.");

        AddSectionTitle("ТРАЕКТОРИЯ ГРАВИТАЦИИ",
            "Прогноз движения опасных enemies внутри активной Gravity Anomaly");
        AddToggleRow("ТРАЕКТОРИЯ ГРАВИТАЦИИ",
            trajectoryAvailable && trajectoryPreview.PreviewEnabled,
            trajectoryAvailable,
            () => trajectoryPreview.SetPreviewEnabled(!trajectoryPreview.PreviewEnabled));
        AddHint("Вне активной orbital Gravity Zone линии не показываются — " +
            "это нормальное ограничение, а не ошибка.");
        float[] times = { 0.75f, 1.25f, 1.5f, 2f };
        for (int i = 0; i < times.Length; i++)
        {
            float captured = times[i];
            AddOptionRow($"ПРОГНОЗ НА {captured:0.00} СЕК",
                trajectoryAvailable && Mathf.Approximately(
                    trajectoryPreview.PredictionTime, captured),
                trajectoryAvailable,
                () => trajectoryPreview.SetPredictionTime(captured));
        }
        AddHint("Prediction определяет, на сколько секунд вперёд строится траектория.");

        AddSectionTitle("ОТДЕЛЬНЫЕ ТЕСТЫ САЙТОВ",
            "Останавливают Exploration безопасным debug-путём; F1 остаётся доступен");
        AddSiteTestRow(AnomalySiteDebugSelector.SiteTestType.Gravity,
            "GRAVITY SITE");
        AddSiteTestRow(AnomalySiteDebugSelector.SiteTestType.Electric,
            "ELECTRIC SITE");
        AddSiteTestRow(AnomalySiteDebugSelector.SiteTestType.Beam,
            "BEAM SITE");
        AddSiteTestRow(AnomalySiteDebugSelector.SiteTestType.Normal,
            "NORMAL SITE");
        AddRow("ВЕРНУТЬСЯ В EXPLORATION", "ПЕРЕЗАГРУЗИТ SANDBOX",
            warningColor, "ВЕРНУТЬСЯ", explorationSector != null,
            () => explorationSector.ReturnToExploration());
        AddHint("Возврат перезагружает только GameplaySandbox и восстанавливает " +
            "его стандартную Exploration-конфигурацию.");

        AddSectionTitle("ДИАГНОСТИКА СОБЫТИЙ",
            "Фактические runtime-state и interaction overlay");
        AddSiteRuntimeStatusRows();
        AddToggleRow("ПОКАЗЫВАТЬ EVENT DEBUG",
            eventStatusOverlay != null && eventStatusOverlay.OverlayVisible,
            eventStatusOverlay != null,
            () => eventStatusOverlay.SetOverlayVisible(
                !eventStatusOverlay.OverlayVisible));
        AddHint("Показывает CanInteract, distance, E-state и progress текущего World Event.");
    }

    private void AddSiteTestRow(
        AnomalySiteDebugSelector.SiteTestType type,
        string label)
    {
        bool available = IsSiteTestAvailable(type);
        AddRow($"ЗАПУСТИТЬ {label}", available ? "ГОТОВО" : "НЕ НАСТРОЕНО",
            available ? mutedColor : warningColor,
            "ЗАПУСТИТЬ", available, () =>
            {
                explorationSector?.StopForStandaloneSiteTest();
                anomalySiteSelector?.StartStandaloneTest(type);
                RefreshCurrentTab();
            });
        AddHint(type switch
        {
            AnomalySiteDebugSelector.SiteTestType.Gravity =>
                "Orbital Gravity Zone + Hold Event; completion выдаёт Gravity Orb.",
            AnomalySiteDebugSelector.SiteTestType.Electric =>
                "Электрические nodes дают telegraph и разряды; Hold Event выдаёт Arc Node.",
            AnomalySiteDebugSelector.SiteTestType.Beam =>
                "Environmental beam даёт telegraph; Evacuation Corridor выдаёт Red Beam.",
            _ => "Обычная Local Anomaly + Hold Event; отдельной power-награды нет."
        });
    }

    private bool IsSiteTestAvailable(AnomalySiteDebugSelector.SiteTestType type)
    {
        if (anomalySiteSelector == null || explorationSector == null)
            return false;
        return type switch
        {
            AnomalySiteDebugSelector.SiteTestType.Gravity =>
                anomalySiteSelector.GravitySite != null &&
                anomalySiteSelector.GravitySite.IsConfigured,
            AnomalySiteDebugSelector.SiteTestType.Electric =>
                anomalySiteSelector.ElectricSite != null &&
                anomalySiteSelector.ElectricSite.IsConfigured,
            AnomalySiteDebugSelector.SiteTestType.Beam =>
                anomalySiteSelector.BeamSite != null &&
                anomalySiteSelector.BeamSite.IsConfigured,
            AnomalySiteDebugSelector.SiteTestType.Normal =>
                anomalySiteSelector.NormalSite != null &&
                anomalySiteSelector.NormalSite.IsConfigured,
            _ => false
        };
    }

    private void AddSiteRuntimeStatusRows()
    {
        AddRow("WorldEventSpawner", worldEventSpawner != null ? "ГОТОВ" : "НЕ НАЙДЕН",
            worldEventSpawner != null ? successColor : warningColor,
            null, false, null);
        if (anomalySiteSelector == null)
        {
            AddRow("Site selector", "НЕ НАЙДЕН", warningColor, null, false, null);
            return;
        }
        GravityAnomalySiteController gravity = anomalySiteSelector.GravitySite;
        ElectricAnomalySiteController electric = anomalySiteSelector.ElectricSite;
        BeamAnomalySiteController beam = anomalySiteSelector.BeamSite;
        NormalAnomalySiteController normal = anomalySiteSelector.NormalSite;
        AddRow("Gravity Site", gravity != null ? TranslateSiteState(gravity.RuntimeState) :
            "НЕ НАЙДЕН", gravity != null && gravity.IsRunning ? successColor : mutedColor,
            null, false, null);
        AddRow("Electric Site", electric != null
                ? $"{TranslateSiteState(electric.RuntimeState)} · EVENT: " +
                  (electric.HasActiveEvent ? "ЕСТЬ" : "НЕТ") + " · HAZARD: " +
                  TranslatePowerState(electric.HazardRuntimeState)
                : "НЕ НАЙДЕН",
            electric != null && electric.IsRunning ? successColor : mutedColor,
            null, false, null);
        AddRow("Beam Site", beam != null
                ? $"{TranslateSiteState(beam.RuntimeState)} · EVENT: " +
                  (beam.HasActiveEvent ? "ЕСТЬ" : "НЕТ") + " · HAZARD: " +
                  TranslatePowerState(beam.HazardRuntimeState)
                : "НЕ НАЙДЕН",
            beam != null && beam.IsRunning ? successColor : mutedColor,
            null, false, null);
        AddRow("Normal Site", normal != null ? TranslateSiteState(normal.RuntimeState) :
            "НЕ НАЙДЕН", normal != null && normal.IsActive ? successColor : mutedColor,
            null, false, null);
    }

    private static string TranslateSiteState(string state) => state switch
    {
        "Active" => "АКТИВЕН",
        "Dormant" => "ОЖИДАЕТ",
        "Collapsing" => "СВОРАЧИВАЕТСЯ",
        "Completed" => "ЗАВЕРШЁН",
        "Stopped" => "ОСТАНОВЛЕН",
        _ => "НЕИЗВЕСТНО"
    };

    private void AddSandboxWeaponsPowersSection()
    {
        AddWeaponsSection();
        AddSectionTitle("ЯДРО ОРУЖИЯ", "Runtime-переключатель существующего debug core");
        AddOptionRow("БЕЗ ЯДРА", WeaponCoreDebugSelector.ActiveCore == WeaponCoreType.None,
            true, () => WeaponCoreDebugSelector.Select(WeaponCoreType.None));
        AddOptionRow("ЦЕПНОЕ ЯДРО", WeaponCoreDebugSelector.ActiveCore == WeaponCoreType.Chain,
            true, () => WeaponCoreDebugSelector.Select(WeaponCoreType.Chain));
        AddHint("Цепное ядро: попадание основного оружия передаёт часть урона " +
            "соседнему enemy. Без ядра оружие работает штатно.");

        AddSectionTitle("АНОМАЛЬНЫЕ СПОСОБНОСТИ",
            "Реальные runtime-components на player; reward-lock показан явно");
        bool available = anomalyPowerController != null;
        AddPowerToggle("ГРАВИТАЦИОННАЯ СФЕРА",
            available && anomalyPowerController.GravityOrbEnabled,
            available && !anomalyPowerController.GravityOrbSiteLocked,
            () => anomalyPowerController.SetGravityOrbEnabled(
                !anomalyPowerController.GravityOrbEnabled));
        AddHint("Вращается вокруг player и наносит 65 damage при контакте. " +
            "Повторный hit по одной цели возможен через 0.42 сек. При блокировке " +
            "завершите Gravity Site.");
        AddPowerToggle("ЭЛЕКТРИЧЕСКИЙ УЗЕЛ",
            available && anomalyPowerController.ArcNodeEnabled,
            available && !anomalyPowerController.ArcNodeSiteLocked,
            () => anomalyPowerController.SetArcNodeEnabled(
                !anomalyPowerController.ArcNodeEnabled));
        AddHint("Каждые 0.65 сек ищет enemy в радиусе 9 и цепляет до 4 целей. " +
            "Каждая получает 70 damage. При блокировке завершите Electric Site.");
        AddPowerToggle("КРАСНЫЙ ЛУЧ",
            available && anomalyPowerController.RedBeamEnabled,
            available && !anomalyPowerController.RedBeamSiteLocked,
            () => anomalyPowerController.SetRedBeamEnabled(
                !anomalyPowerController.RedBeamEnabled));
        AddHint("Раз в 3 сек выбирает линию с максимальным числом целей. " +
            "Дальность 18, полуширина 1.05, урон 120. При блокировке завершите Beam Site.");

        AddSectionTitle("ДИАГНОСТИКА POWERS",
            "Последнее фактическое срабатывание; значения обновляются не каждый frame");
        if (!available)
        {
            AddRow("AnomalyPower controller", "НЕ НАЙДЕН", warningColor,
                null, false, null);
            return;
        }
        AddRow("Gravity Orb component",
            anomalyPowerController.GravityOrbComponentPresent ? "ДА" : "НЕТ",
            anomalyPowerController.GravityOrbComponentPresent ? successColor : warningColor,
            null, false, null);
        AddRow("Gravity Orb · последний контакт",
            $"{FormatLastActivity(anomalyPowerController.GravityOrbLastContactTime)} · " +
            $"Hits: {anomalyPowerController.GravityOrbLastContactHits} · " +
            $"Kills: {anomalyPowerController.GravityOrbLastContactKills} · Урон: 65",
            mutedColor, null, false, null);
        AddRow("Arc Node component",
            anomalyPowerController.ArcNodeComponentPresent ? "ДА" : "НЕТ",
            anomalyPowerController.ArcNodeComponentPresent ? successColor : warningColor,
            null, false, null);
        AddRow("Arc Node · последний разряд",
            $"{FormatLastActivity(anomalyPowerController.ArcNodeLastDischargeTime)} · " +
            $"Targets: {anomalyPowerController.ArcNodeLastTargetCount} · " +
            $"Kills: {anomalyPowerController.ArcNodeLastKillCount} · Урон: 70",
            mutedColor, null, false, null);
        AddRow("Arc Node · разрядить сейчас",
            anomalyPowerController.ArcNodeEnabled ? "ГОТОВО" : "СНАЧАЛА ВКЛЮЧИТЕ",
            anomalyPowerController.ArcNodeEnabled ? mutedColor : warningColor,
            "РАЗРЯД", anomalyPowerController.ArcNodeEnabled, () =>
            {
                anomalyPowerController.DischargeArcNodeNowDebug();
                RefreshCurrentTab();
            });
        AddRow("Red Beam component",
            anomalyPowerController.RedBeamComponentPresent ? "ДА" : "НЕТ",
            anomalyPowerController.RedBeamComponentPresent ? successColor : warningColor,
            null, false, null);
        AddRow("Red Beam · runtime",
            $"{TranslatePowerState(anomalyPowerController.RedBeamRuntimeState)} · cooldown " +
            $"{anomalyPowerController.RedBeamCooldownRemaining:0.00} сек",
            mutedColor, null, false, null);
        AddRow("Red Beam · последний выстрел",
            $"{FormatLastActivity(anomalyPowerController.RedBeamLastFireTime)} · " +
            $"Кандидатов: {anomalyPowerController.RedBeamLastCandidateCount} · " +
            $"Hits: {anomalyPowerController.RedBeamLastHitCount} · " +
            $"Kills: {anomalyPowerController.RedBeamLastKillCount} · Урон: 120",
            mutedColor, null, false, null);
        AddRow("Red Beam · выстрелить сейчас",
            anomalyPowerController.RedBeamEnabled ? "ГОТОВО" : "СНАЧАЛА ВКЛЮЧИТЕ",
            anomalyPowerController.RedBeamEnabled ? mutedColor : warningColor,
            "ВЫСТРЕЛ", anomalyPowerController.RedBeamEnabled, () =>
            {
                anomalyPowerController.FireRedBeamNowDebug();
                RefreshCurrentTab();
            });
        AddHint("Принудительные actions используют тот же target selection и " +
            "EnemyHealth.TakeDamage, что и автоматическая способность.");
    }

    private static string FormatLastActivity(float timestamp)
    {
        if (float.IsNegativeInfinity(timestamp))
            return "ЕЩЁ НЕ БЫЛО";
        return $"{Mathf.Max(0f, Time.time - timestamp):0.0} сек назад";
    }

    private static string TranslatePowerState(string state) => state switch
    {
        "Waiting" => "ОЖИДАНИЕ",
        "Telegraph" => "ПРЕДУПРЕЖДЕНИЕ",
        "Firing" => "ВЫСТРЕЛ",
        _ => "НЕТ COMPONENT"
    };

    private void AddPowerToggle(string label, bool enabled, bool available,
        UnityEngine.Events.UnityAction action)
    {
        AddRow(label, available ? (enabled ? "ВКЛЮЧЕНО" : "ВЫКЛЮЧЕНО") : "ЗАБЛОКИРОВАНО",
            enabled ? successColor : available ? mutedColor : warningColor,
            enabled ? "ВЫКЛЮЧИТЬ" : "ВКЛЮЧИТЬ", available, () =>
            {
                action?.Invoke();
                RefreshCurrentTab();
            });
    }

    private void AddToggleRow(string label, bool enabled, bool available,
        UnityEngine.Events.UnityAction action)
    {
        AddRow(label, !available ? "НЕДОСТУПНО В ТЕКУЩЕМ РЕЖИМЕ" :
                enabled ? "ВКЛЮЧЕНО" : "ВЫКЛЮЧЕНО",
            enabled ? successColor : available ? mutedColor : warningColor,
            enabled ? "ВЫКЛЮЧИТЬ" : "ВКЛЮЧИТЬ", available, () =>
            {
                action?.Invoke();
                RefreshCurrentTab();
            });
    }

    private void AddOptionRow(string label, bool selected, bool available,
        UnityEngine.Events.UnityAction action)
    {
        AddRow(label, !available ? "НЕДОСТУПНО В ТЕКУЩЕМ РЕЖИМЕ" :
                selected ? "ВЫБРАНО" : "ДОСТУПНО",
            selected ? successColor : available ? mutedColor : warningColor,
            "ВЫБРАТЬ", available, () =>
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

    private void AddProductionSectorTestSection()
    {
        EnsureProductionSectorDebug();
        ProductionSectorDebugController debug = productionSectorDebug;
        bool available = debug != null;

        if (!available)
        {
            AddSectionTitle("ТЕСТ СЕКТОРА", "Development / Editor only");
            AddHint("ProductionSectorDebugController не создан.");
            return;
        }

        AddSectionTitle(
            "ИГРОК",
            "Урон HP ×0; knockback, gravity, hit flash, sound и camera shake остаются"
        );
        AddToggleRow(
            "НЕУЯЗВИМОСТЬ ИГРОКА",
            debug.InvulnerabilityEnabled,
            true,
            () => debug.SetInvulnerability(!debug.InvulnerabilityEnabled)
        );

        AddSectionTitle(
            "ЧИТАЕМОСТЬ ЗОНЫ",
            "Только CURRENT ZONE: ground, trees, vegetation, props и decor"
        );
        AddProductionReadabilityPreset(
            ProductionSectorDebugController.ReadabilityPreset.Original
        );
        AddProductionReadabilityPreset(
            ProductionSectorDebugController.ReadabilityPreset.Muted
        );
        AddProductionReadabilityPreset(
            ProductionSectorDebugController.ReadabilityPreset.HighGameplayContrast
        );
        AddProductionReadabilityPreset(
            ProductionSectorDebugController.ReadabilityPreset.DarkWorld
        );
        AddHint(
            "Player, enemies, projectiles, weapons, anomaly visuals, Event/Exit/chest markers, HUD и World Rule overlays исключены. " +
            "Активный World Rule остаётся нижним слоем визуала."
        );

        AddSectionTitle(
            "ЯРКОСТЬ ДЕКОРА",
            "Trees, plants, grass, props и clutter внутри CURRENT ZONE"
        );
        AddProductionFloatOptions(
            new[] { 1f, 0.75f, 0.5f, 0.25f },
            debug.DecorBrightness,
            value => debug.SetDecorBrightness(value),
            value => $"{value * 100f:0}%"
        );

        AddSectionTitle(
            "АКЦЕНТ АНОМАЛИИ",
            "Только visual brightness/alpha/line width; geometry, damage и силы не меняются"
        );
        AddProductionFloatOptions(
            new[] { 1f, 1.25f, 1.5f, 1.75f },
            debug.AnomalyAccent,
            value => debug.SetAnomalyAccent(value),
            value => $"{value * 100f:0}%"
        );

        AddSectionTitle(
            "ВРАГИ",
            "Мультипликативный lift/tint сохраняет Golden и специальные цвета"
        );
        AddProductionEnemyMode(
            ProductionSectorDebugController.EnemyReadability.Off
        );
        AddProductionEnemyMode(
            ProductionSectorDebugController.EnemyReadability.Light
        );
        AddProductionEnemyMode(
            ProductionSectorDebugController.EnemyReadability.Strong
        );
        AddOptionRow(
            "ПРИМЕНЯТЬ: В ТЕКУЩЕЙ ЗОНЕ",
            debug.CurrentEnemyScope ==
                ProductionSectorDebugController.EnemyScope.CurrentZone,
            true,
            () => debug.SetEnemyScope(
                ProductionSectorDebugController.EnemyScope.CurrentZone
            )
        );
        AddOptionRow(
            "ПРИМЕНЯТЬ: ВСЕ",
            debug.CurrentEnemyScope ==
                ProductionSectorDebugController.EnemyScope.All,
            true,
            () => debug.SetEnemyScope(
                ProductionSectorDebugController.EnemyScope.All
            )
        );

        AddSectionTitle(
            "ОСОБАЯ АНОМАЛИЯ",
            "Применится только при безопасной полной пересборке текущего сектора"
        );
        AddRow(
            "ТЕКУЩАЯ",
            debug.CurrentSpecialName,
            debug.CurrentSpecialName != "NONE" ? successColor : mutedColor,
            "ИНФО",
            false,
            null
        );
        AddProductionSpecialOverride(
            ProductionSectorDebugController.SpecialOverride.Random
        );
        AddProductionSpecialOverride(
            ProductionSectorDebugController.SpecialOverride.Gravity
        );
        AddProductionSpecialOverride(
            ProductionSectorDebugController.SpecialOverride.Electric
        );
        AddProductionSpecialOverride(
            ProductionSectorDebugController.SpecialOverride.Beam
        );
        AddRow(
            "ПЕРЕСОЗДАТЬ ТЕКУЩИЙ СЕКТОР",
            "FULL SCENE REBUILD / RUNSTATE СОХРАНЁН",
            warningColor,
            "ПЕРЕСОЗДАТЬ",
            RunStateManager.Instance != null,
            () => debug.RebuildCurrentSector()
        );
        AddHint(
            "Live hot-swap Special Site отключён: reset не двигает маршрут, не завершает Event и не выдаёт награду."
        );

        AddRow(
            "СБРОСИТЬ ВИЗУАЛ",
            "ORIGINAL / 100 / 100 / ENEMY OFF",
            mutedColor,
            "СБРОСИТЬ",
            true,
            () => debug.ResetVisualSettings()
        );
        AddHint(
            "Visual reset не меняет invulnerability и Special override. Все defaults нейтральны: Original, 100%, 100%, Enemy Off, Current Zone, Random, Invulnerability Off."
        );

        AddSectionTitle(
            "ДИАГНОСТИКА",
            "Session-only инструменты; сохранение и meta-прогресс не меняются"
        );
        AddRow(
            "ТЕКУЩИЙ СЕКТОР",
            debug.CurrentSectorNumber > 0
                ? $"{debug.CurrentSectorNumber}/5"
                : "НЕТ АКТИВНОГО RUNSTATE",
            debug.CurrentSectorNumber > 0 ? successColor : warningColor,
            "ИНФО",
            false,
            null
        );
        AddRow(
            "ТЕКУЩАЯ ЗОНА",
            debug.CurrentZoneName,
            debug.CurrentSite != null ? successColor : mutedColor,
            "ИНФО",
            false,
            null
        );
        AddHint(
            $"Читаемость: {GetProductionPresetName(debug.Preset)} | " +
            $"декор {debug.DecorBrightness * 100f:0}% | " +
            $"акцент аномалии {debug.AnomalyAccent * 100f:0}% | " +
            $"враги {GetEnemyReadabilityName(debug.EnemyMode)} " +
            $"({GetEnemyScopeName(debug.CurrentEnemyScope)})\n" +
            $"Override особой аномалии: {debug.Override.ToString().ToUpperInvariant()} | " +
            $"бессмертие: {(debug.InvulnerabilityEnabled ? "ДА" : "НЕТ")} | " +
            $"environment renderers в зоне: {debug.EnvironmentRendererCount}"
        );
    }

    private void AddProductionReadabilityPreset(
        ProductionSectorDebugController.ReadabilityPreset value)
    {
        ProductionSectorDebugController debug = productionSectorDebug;
        AddOptionRow(
            GetProductionPresetName(value),
            debug != null && debug.Preset == value,
            debug != null,
            () => debug.SetPreset(value)
        );
    }

    private void AddProductionFloatOptions(
        float[] values,
        float current,
        System.Action<float> setter,
        System.Func<float, string> label)
    {
        for (int i = 0; i < values.Length; i++)
        {
            float captured = values[i];
            AddOptionRow(
                label(captured),
                Mathf.Approximately(captured, current),
                true,
                () => setter(captured)
            );
        }
    }

    private void AddProductionEnemyMode(
        ProductionSectorDebugController.EnemyReadability value)
    {
        ProductionSectorDebugController debug = productionSectorDebug;
        AddOptionRow(
            GetEnemyReadabilityName(value),
            debug != null && debug.EnemyMode == value,
            debug != null,
            () => debug.SetEnemyReadability(value)
        );
    }

    private void AddProductionSpecialOverride(
        ProductionSectorDebugController.SpecialOverride value)
    {
        ProductionSectorDebugController debug = productionSectorDebug;
        AddOptionRow(
            value.ToString().ToUpperInvariant(),
            debug != null && debug.Override == value,
            debug != null,
            () => debug.SetSpecialOverride(value)
        );
    }

    private static string GetProductionPresetName(
        ProductionSectorDebugController.ReadabilityPreset value) =>
        value switch
        {
            ProductionSectorDebugController.ReadabilityPreset.Muted =>
                "ПРИГЛУШЁННЫЙ МИР",
            ProductionSectorDebugController.ReadabilityPreset.HighGameplayContrast =>
                "ВЫСОКИЙ КОНТРАСТ GAMEPLAY",
            ProductionSectorDebugController.ReadabilityPreset.DarkWorld =>
                "ТЁМНЫЙ МИР",
            _ => "ОРИГИНАЛ"
        };

    private static string GetEnemyReadabilityName(
        ProductionSectorDebugController.EnemyReadability value) =>
        value switch
        {
            ProductionSectorDebugController.EnemyReadability.Light =>
                "ЛЁГКАЯ",
            ProductionSectorDebugController.EnemyReadability.Strong =>
                "СИЛЬНАЯ",
            _ => "ВЫКЛ"
        };

    private static string GetEnemyScopeName(
        ProductionSectorDebugController.EnemyScope value) =>
        value == ProductionSectorDebugController.EnemyScope.All
            ? "ВСЕ"
            : "ТЕКУЩАЯ ЗОНА";

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
        AddSectionTitle("АКТИВНОЕ ОРУЖИЕ",
            "Замена только на текущую сессию через CharacterSpawner");
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        BaseWeapon current = FindPrimaryWeapon(player);
        AddRow("ТЕКУЩЕЕ ОРУЖИЕ",
            current != null ? GetWeaponName(current.weaponData) : "НЕ НАЙДЕНО",
            current != null ? successColor : warningColor,
            null, false, null);

        if (debugWeapons == null || debugWeapons.Length == 0)
        {
            AddHint("Недоступно: в debug menu не назначены WeaponData assets.");
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
                active ? "АКТИВНО" : data.weaponPrefab == null
                    ? "PREFAB НЕ НАЗНАЧЕН"
                    : available ? "ДОСТУПНО" : "PLAYER/SPAWNER НЕ НАЙДЕН",
                active ? successColor : available ? mutedColor : warningColor,
                "ВЫБРАТЬ", available, () => UseWeapon(captured));
        }
        AddHint("Что делает: заменяет primary weapon у текущего player. " +
            "Сохранение и production loadout не изменяются.");
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
        _ => Mathf.Clamp(
            60f + (string.IsNullOrEmpty(label) ? 0f : label.Length * 8.5f),
            116f,
            280f
        )
    };

    private void AddHint(string message)
    {
        TextMeshProUGUI hint = CreateText(
            "Hint", contentRoot, message, 15f,
            TextAlignmentOptions.Center, mutedColor
        );
        hint.textWrappingMode = TextWrappingModes.Normal;
        hint.gameObject.AddComponent<LayoutElement>().preferredHeight = 56f;
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
