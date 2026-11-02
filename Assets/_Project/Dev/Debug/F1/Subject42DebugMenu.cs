#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Subject42.Combat.OrbitalStation;

public sealed partial class Subject42DebugMenu : MonoBehaviour
{
    [Header("Existing scene systems")]
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private CharacterSpawner characterSpawner;
    [SerializeField] private UpgradeManager upgradeManager;
    [SerializeField] private RunFlowController runFlowController;
    [SerializeField] private LevelChoiceManager levelChoiceManager;
    [SerializeField] private GameObject turretEnemyPrefab;
    [SerializeField] private GameObject eyesEnemyPrefab;
    private static Subject42DebugMenu activeInstance;
    public static bool IsDebugMenuOpen =>
        activeInstance != null && (activeInstance.isOpen || activeInstance.IsHubOpen);
    private OrbitalInteractionController inputOwner;
    private Func<bool> inputBlocker;
    private enum DebugTab { Bunker, PlayerBuild, WorldEvents, Additional, OrbitalProduction, Sandbox }
    private static readonly string[] TabLabels =
        { "Бункер", "Игрок", "Мир", "Инструменты", "Орбиты", "ПЕСОЧНИЦА" };
    private GameObject menuRoot, fullMenuBlocker, visualBackButton;
    private Image fullMenuBlockerImage;
    private RectTransform menuPanel, menuHeader, menuTabBar, menuPages, closeButtonRect, contentRoot;
    private TextMeshProUGUI menuTitle;
    private readonly GameObject[] tabRoots = new GameObject[TabLabels.Length];
    private readonly Image[] tabButtonImages = new Image[HubCategories.Length];
    private DebugTab activeTab = DebugTab.PlayerBuild;
    private bool isOpen, menuLiveSimulation, waitingForF1Release;
    private float previousTimeScale;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;
    private readonly List<GameObject> debugEnemies = new();
    private string enemyDebugStatus = "Готово к ручному тесту.";
    private TextMeshProUGUI activeXpCounter;
    private float nextXpCounterRefresh;
    private readonly Color panelColor = new(0.035f, 0.045f, 0.06f, 0.97f);
    private readonly Color rowColor = new(0.09f, 0.11f, 0.145f, 0.95f);
    private readonly Color accentColor = new(0.13f, 0.58f, 0.72f, 1f);
    private readonly Color mutedColor = new(0.65f, 0.69f, 0.74f, 1f);
    private readonly Color successColor = new(0.36f, 0.82f, 0.48f, 1f);
    private readonly Color warningColor = new(1f, 0.69f, 0.25f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapDevelopmentMenu()
    {
        SceneManager.sceneLoaded -= AttachDevelopmentMenu;
        SceneManager.sceneLoaded += AttachDevelopmentMenu;
        SceneManager.sceneUnloaded -= RebindDevelopmentMenu;
        SceneManager.sceneUnloaded += RebindDevelopmentMenu;
        AttachDevelopmentMenu(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }

    private static void AttachDevelopmentMenu(Scene scene, LoadSceneMode mode)
    {
        if (activeInstance != null) return;
        var roots = scene.GetRootGameObjects();
        if (roots.SelectMany(root => root.GetComponentsInChildren<Subject42DebugMenu>(true)).Any()) return;
        var host = roots.SelectMany(root => root.GetComponentsInChildren<HUDManager>(true)).FirstOrDefault();
        if (host != null) { host.gameObject.AddComponent<Subject42DebugMenu>(); return; }
        foreach (var context in roots.SelectMany(root => root.GetComponentsInChildren<BunkerContext>(true)))
            if (context.GetComponent<Subject42DebugMenu>() == null) context.gameObject.AddComponent<Subject42DebugMenu>();
    }

    private static void RebindDevelopmentMenu(Scene unloaded)
    {
        for (int i = 0; i < SceneManager.sceneCount && activeInstance == null; i++)
            AttachDevelopmentMenu(SceneManager.GetSceneAt(i), LoadSceneMode.Additive);
    }

    private void BindInputOwner()
    {
        var next = PlayerRuntimeReference.CachedPlayer != null
            ? PlayerRuntimeReference.CachedPlayer.GetComponentInChildren<OrbitalInteractionController>(true) : null;
        if (next == inputOwner) return;
        ReleaseInputOwner();
        inputOwner = next;
        // A live side panel only owns Escape; pointer interactions already respect UI raycasts.
        inputBlocker = () => isOpen && (!mvpOrbitSurfaceOpen || Input.GetKeyDown(KeyCode.Escape));
        if (inputOwner != null) inputOwner.DevelopmentInputBlocker = inputBlocker;
    }

    private void ReleaseInputOwner()
    {
        if (inputOwner != null && inputOwner.DevelopmentInputBlocker == inputBlocker)
            inputOwner.DevelopmentInputBlocker = null;
        inputOwner = null;
    }

    private void Awake()
    {
        if (activeInstance != null && activeInstance != this)
        {
            enabled = false;
            Destroy(this);
            return;
        }
        activeInstance = this;
        ResolveSceneReferences();
    }

    private void Start()
    {
        BindBotLabScene();
        if (IsMvpDebugMenu)
            activeTab = (DebugTab)0;
        else if (IsProductionBunker)
            activeTab = DebugTab.Bunker;
        else if (characterSpawner == null)
            activeTab = DebugTab.Additional;
        BuildMenu();
        BuildHub();
        RefreshTab(activeTab);
        SelectTab(activeTab, false);
        if (menuRoot != null)
            menuRoot.SetActive(false);
    }

    private void Update()
    {
        BindInputOwner();
        if (IsHubOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            inputOwner?.DebugConsumeExternalUiEscape();
            CloseHub();
            return;
        }
        if (isOpen && mvpOrbitSurfaceOpen &&
            (!MvpCanEdit || MvpStation.IsRunEnded || Time.timeScale <= 0f))
            CloseMenu();
        if (IsSandboxScene && Input.GetKeyDown(KeyCode.F9))
        {
            ToggleMvpOrbitSurface();
            return;
        }
        if (isOpen && mvpOrbitSurfaceOpen) ConstrainMvpOrbitSurfacePosition();
        if (isOpen && Input.GetKeyDown(KeyCode.Escape))
        {
            inputOwner?.DebugConsumeExternalUiEscape();
            CloseMenu();
            return;
        }
        UpdateBotLabTelemetry();
        if (isOpen && activeXpCounter != null &&
            activeXpCounter.gameObject.activeInHierarchy &&
            Time.unscaledTime >= nextXpCounterRefresh)
        {
            RefreshActiveXpCounter();
            nextXpCounterRefresh = Time.unscaledTime + 0.25f;
        }

        if (waitingForF1Release)
        {
            if (!Input.GetKey(KeyCode.F1))
                waitingForF1Release = false;

            return;
        }

        if (!Input.GetKeyDown(KeyCode.F1))
            return;

        waitingForF1Release = true;
        ToggleHub();
    }

    private void OnDisable()
    {
        CloseHub();
        ReleaseInputOwner();
        if (isOpen) CloseMenu();
    }

    private void OnDestroy()
    {
        DisposeHub();
        ReleaseTestPickups();

        if (isOpen)
            RestoreGameState(!mvpOrbitSurfaceOpen);

        if (menuRoot != null)
            Destroy(menuRoot);
        if (activeInstance == this)
            activeInstance = null;
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
        if (IsMvpDebugMenu) upgradeManager?.DebugCancelCatalogueReward();
        FindFirstObjectByType<OrbitalInteractionController>()?.PrepareForExternalPause();
        previousTimeScale = Time.timeScale;
        previousCursorVisible = Cursor.visible;
        previousCursorLockMode = Cursor.lockState;
        menuLiveSimulation = false;
        ApplyMenuViewportLayout();
        RefreshTab(activeTab);

        Time.timeScale = menuLiveSimulation ? previousTimeScale : 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        isOpen = true;
        menuRoot.SetActive(true);
        fullMenuBlocker?.SetActive(true);
    }

    private void CloseMenu()
    {
        if (!isOpen)
            return;

        if (menuRoot != null)
            menuRoot.SetActive(false);
        RestoreGameState(!mvpOrbitSurfaceOpen);
        isOpen = false;
        mvpOrbitSurfaceOpen = false;
        mvpOrbitSaveFooter?.gameObject.SetActive(false);
    }

    private void RestoreGameState(bool restoreTime = true)
    {
        bool productionChoiceIsOpen =
            (levelChoiceManager != null && levelChoiceManager.IsChoosing) ||
            (upgradeManager != null && upgradeManager.IsChoosingUpgrade);

        if (productionChoiceIsOpen)
        {
            if (restoreTime) Time.timeScale = 0f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            return;
        }

        if (restoreTime) Time.timeScale = previousTimeScale;
        else if (Time.timeScale <= 0f || SceneTransitionOverlay.IsTransitioning ||
            RunStateManager.Instance != null && RunStateManager.Instance.IsRunEnded) return;
        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockMode;
    }

    private void ResolveSceneReferences()
    {
        enemySpawner ??= FindFirstObjectByType<EnemySpawner>();
        characterSpawner ??= FindFirstObjectByType<CharacterSpawner>();
        turretEnemyPrefab ??= enemySpawner?.FindDebugEnemyPrefab(EnemySpawner.DebugEnemyArchetype.Turret);
        eyesEnemyPrefab ??= enemySpawner?.FindDebugEnemyPrefab(EnemySpawner.DebugEnemyArchetype.Eyes);
        upgradeManager ??= UpgradeManager.Instance != null
            ? UpgradeManager.Instance
            : FindFirstObjectByType<UpgradeManager>();
        runFlowController ??= RunFlowController.Instance != null
            ? RunFlowController.Instance
            : FindFirstObjectByType<RunFlowController>();
        levelChoiceManager ??= FindFirstObjectByType<LevelChoiceManager>();


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
        fullMenuBlocker = blocker.gameObject;
        Stretch(blocker);
        Image blockerImage = blocker.gameObject.AddComponent<Image>();
        blockerImage.color = new Color(0f, 0f, 0f, 0.68f);
        blockerImage.raycastTarget = true;
        fullMenuBlockerImage = blockerImage;

        RectTransform panel = CreateRect("Panel", blocker);
        menuPanel = panel;
        panel.anchorMin = new Vector2(0.07f, 0.05f);
        panel.anchorMax = new Vector2(0.93f, 0.95f);
        panel.offsetMin = Vector2.zero;
        panel.offsetMax = Vector2.zero;
        panel.gameObject.AddComponent<Image>().color = panelColor;

        RectTransform header = CreateRect("Header", panel);
        menuHeader = header;
        header.anchorMin = new Vector2(0f, 1f);
        header.anchorMax = Vector2.one;
        header.pivot = new Vector2(0.5f, 1f);
        header.sizeDelta = new Vector2(0f, 62f);
        header.anchoredPosition = Vector2.zero;

        TextMeshProUGUI title = CreateText(
            "Title", header, "Отладка · F1", 24f,
            TextAlignmentOptions.MidlineLeft, Color.white
        );
        menuTitle = title;
        Stretch(title.rectTransform, 22f, 90f);
        if (IsMvpDebugMenu) ConfigureMvpOrbitSurfaceDrag();

        Button closeButton = CreateButton(header, "X", CloseMenu, 52f);
        RectTransform closeRect = closeButton.GetComponent<RectTransform>();
        closeButtonRect = closeRect;
        closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 0.5f);
        closeRect.pivot = new Vector2(1f, 0.5f);
        closeRect.anchoredPosition = new Vector2(-14f, 0f);
        closeRect.sizeDelta = new Vector2(52f, 40f);

        RectTransform tabBar = CreateRect("Tabs", panel);
        menuTabBar = tabBar;
        tabBar.anchorMin = new Vector2(0f, 1f);
        tabBar.anchorMax = Vector2.one;
        tabBar.pivot = new Vector2(0.5f, 1f);
        tabBar.anchoredPosition = new Vector2(0f, -62f);
        tabBar.sizeDelta = new Vector2(0f, 54f);

        string[] labels = IsMvpDebugMenu ? MvpTabLabels : TabLabels;
        for (int i = 0; i < HubCategories.Length; i++)
        {
            int category = i;
            RectTransform slot = CreateRect(HubCategories[i] + " Slot", tabBar);
            slot.anchorMin = new Vector2((float)i / HubCategories.Length, 0f);
            slot.anchorMax = new Vector2((float)(i + 1) / HubCategories.Length, 1f);
            slot.offsetMin = new Vector2(3f, 3f);
            slot.offsetMax = new Vector2(-3f, -3f);
            Button button = CreateButton(slot, HubCategories[i], () => HubOpenSection(category), 100f,
                i != 0 || IsSandboxScene);
            Stretch(button.GetComponent<RectTransform>());
            var tabText = button.GetComponentInChildren<TextMeshProUGUI>();
            tabText.fontSize = 14f;
            tabText.textWrappingMode = TextWrappingModes.Normal;
            tabButtonImages[i] = button.targetGraphic as Image;
        }

        RectTransform pages = CreateRect("Tab Pages", panel);
        menuPages = pages;
        pages.anchorMin = Vector2.zero;
        pages.anchorMax = Vector2.one;
        pages.offsetMin = new Vector2(18f, 18f);
        pages.offsetMax = new Vector2(-18f, -120f);

        for (int i = 0; i < labels.Length; i++)
        {
            if (i == (int)DebugTab.Bunker && !IsMvpDebugMenu && !IsProductionBunker) continue;
            bool compactLab = true;
            tabRoots[i] = CreateTabPage(labels[i], pages, out _, compactLab);
        }

        BuildVisualBackButton(header);
        var searchButton = CreateButton(header, "ПОИСК / ИЗБР.", ShowHub, 100f);
        hubSearchButton = searchButton.gameObject;
        var searchButtonRect = searchButton.GetComponent<RectTransform>();
        searchButtonRect.anchorMin = searchButtonRect.anchorMax = new Vector2(1f, .5f);
        searchButtonRect.pivot = new Vector2(1f, .5f);
        searchButtonRect.anchoredPosition = new Vector2(-78f, 0f);
        searchButtonRect.sizeDelta = new Vector2(104f, 30f);
        searchButton.GetComponentInChildren<TextMeshProUGUI>().fontSize = 12f;
        if (IsMvpDebugMenu) BuildMvpOrbitSaveFooter();
    }

    private void BuildVisualBackButton(Transform header)
    {
        Button button = CreateButton(
            header,
            "МЕНЮ",
            () => { if (mvpOrbitSurfaceOpen) ReturnFromMvpOrbitSurface(); else SelectTab(DebugTab.Additional); },
            54f
        );
        visualBackButton = button.gameObject;
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-48f, 0f);
        rect.sizeDelta = new Vector2(54f, 26f);
        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
            text.fontSize = 11f;
        visualBackButton.SetActive(false);
    }

    private GameObject CreateTabPage(
        string tabName,
        Transform parent,
        out RectTransform pageContent,
        bool compactVisual = false)
    {
        RectTransform page = CreateRect(tabName + " Page", parent);
        Stretch(page);

        ScrollRect scroll = page.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.scrollSensitivity = 34f;

        RectTransform viewport = CreateRect("Viewport", page);
        Stretch(viewport);
        if (compactVisual)
            viewport.offsetMax = new Vector2(-7f, 0f);
        viewport.gameObject.AddComponent<Image>().color =
            new Color(0f, 0f, 0f, 0.12f);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;
        scroll.viewport = viewport;

        pageContent = CreateRect("Content", viewport);
        pageContent.anchorMin = new Vector2(0f, 1f);
        pageContent.anchorMax = Vector2.one;
        pageContent.pivot = new Vector2(0.5f, 1f);
        pageContent.offsetMin = new Vector2(compactVisual ? 3f : 10f, 0f);
        pageContent.offsetMax = new Vector2(compactVisual ? -3f : -10f, 0f);

        VerticalLayoutGroup layout =
            pageContent.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = compactVisual
            ? new RectOffset(3, 3, 3, 5)
            : new RectOffset(10, 10, 10, 16);
        layout.spacing = compactVisual ? 2f : 7f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter =
            pageContent.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = pageContent;

        if (compactVisual)
        {
            RectTransform scrollbarRect = CreateRect("Scrollbar", page);
            scrollbarRect.anchorMin = new Vector2(1f, 0f);
            scrollbarRect.anchorMax = Vector2.one;
            scrollbarRect.pivot = new Vector2(1f, 0.5f);
            scrollbarRect.offsetMin = new Vector2(-4f, 2f);
            scrollbarRect.offsetMax = new Vector2(0f, -2f);
            Image track = scrollbarRect.gameObject.AddComponent<Image>();
            track.color = new Color(0.18f, 0.21f, 0.25f, 0.65f);

            RectTransform handle = CreateRect("Handle", scrollbarRect);
            Stretch(handle);
            Image handleImage = handle.gameObject.AddComponent<Image>();
            handleImage.color = new Color(
                accentColor.r, accentColor.g, accentColor.b, 0.8f);

            Scrollbar scrollbar = scrollbarRect.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle;
            scrollbar.targetGraphic = handleImage;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.navigation = new Navigation { mode = Navigation.Mode.None };
            scroll.verticalScrollbar = scrollbar;
            scroll.verticalScrollbarVisibility =
                ScrollRect.ScrollbarVisibility.AutoHide;
            scroll.verticalScrollbarSpacing = 3f;
        }

        return page.gameObject;
    }

    private void SelectTab(DebugTab tab, bool refresh = true)
    {
        if (IsMvpDebugMenu && (int)tab >= MvpTabLabels.Length) return;
        if (!IsMvpDebugMenu && tab == DebugTab.Bunker && !IsProductionBunker) return;
        activeTab = tab;
        if (isOpen)
        {
            menuLiveSimulation = false;
            Time.timeScale = 0f;
        }
        ApplyMenuViewportLayout();

        for (int i = 0; i < tabRoots.Length; i++)
        {
            bool selected = i == (int)tab;
            if (tabRoots[i] != null) tabRoots[i].SetActive(selected);

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

    private void RefreshCurrentTab()
    {
        RefreshTab(activeTab);
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
        {
            GameObject oldRow = contentRoot.GetChild(i).gameObject;
            oldRow.SetActive(false);
            Destroy(oldRow);
        }

        if (IsMvpDebugMenu)
        {
            BuildMvpTab((int)tab);
            return;
        }

        if (tab != DebugTab.Bunker && tab != DebugTab.Sandbox)
            AddTabHeading(TabLabels[(int)tab]);

        switch (tab)
        {
            case DebugTab.Bunker:
                AddBunkerSection();
                break;
            case DebugTab.PlayerBuild:
                AddPlayerBuildSection();
                break;
            case DebugTab.WorldEvents:
                AddWorldEventsSection();
                break;
            case DebugTab.Additional:
                AddAdditionalSection();
                break;
            case DebugTab.OrbitalProduction:
                AddOrbitalProductionSection();
                break;
            case DebugTab.Sandbox:
                BuildSandbox();
                break;
        }

        AddHint("F1 / Esc — закрыть · Пауза");
    }

    private void ApplyMenuViewportLayout()
    {
        if (menuPanel == null) return;
        if (IsMvpDebugMenu) { ApplyMvpViewportLayout(); HubApplyDetailsLayout(); return; }
        menuPanel.anchorMin = new Vector2(.24f, .09f);
        menuPanel.anchorMax = new Vector2(.76f, .91f);
        menuPanel.pivot = new Vector2(.5f, .5f);
        menuPanel.sizeDelta = Vector2.zero;
        menuPanel.anchoredPosition = Vector2.zero;
        menuHeader.sizeDelta = new Vector2(0f, 62f);
        menuTitle.text = activeTab == DebugTab.Sandbox ? "ПЕСОЧНИЦА" : "Отладка · F1";
        menuTitle.fontSize = 24f;
        Stretch(menuTitle.rectTransform, 22f, 90f);
        closeButtonRect.anchoredPosition = new Vector2(-14f, 0f);
        closeButtonRect.sizeDelta = new Vector2(52f, 40f);
        menuTabBar.gameObject.SetActive(true);
        menuPages.offsetMin = new Vector2(18f, 18f);
        menuPages.offsetMax = new Vector2(-18f, -120f);
        fullMenuBlockerImage.color = new Color(0f, 0f, 0f, .68f);
        fullMenuBlockerImage.raycastTarget = true;
        HubApplyDetailsLayout();
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

    private void AddOrbitalProductionSection()
    {
        ResolveSceneReferences();
        BuildMvpOrbits();
    }

    private void AddBunkerProgressionSection()
    {
        if (!IsProductionBunker) return;
        AddFootballMinigameSection();
#if UNITY_EDITOR
        UnlockProgressService unlocks = UnlockProgressService.Instance;
        AddSectionTitle("Открытый контент", "Эти изменения сохраняются");
        AddRow("Открыть всё", "С сохранением", warningColor, "Открыть", unlocks != null,
            () => { unlocks.DebugUnlockAll(); RefreshCurrentTab(); });
        AddRow("Сбросить открытый контент", "С сохранением", warningColor, "Сбросить", unlocks != null,
            () => { unlocks.DebugResetAll(); RefreshCurrentTab(); });
#endif

        BunkerStationProgressionService service = BunkerStationProgressionService.Instance;
        AddSectionTitle("Станция персонажей", "Уровень и вложения сохраняются");
        if (service == null ||
            !service.TryGetData(BunkerStationId.Character, out BunkerStationProgressionData data))
        {
            AddRow("Развитие станции", "Недоступно", warningColor,
                null, false, null);
            return;
        }

        int level = service.GetLevel(BunkerStationId.Character);
        int cost = service.GetUpgradeCost(BunkerStationId.Character);
        int invested = service.GetInvestedGold(BunkerStationId.Character);
        int gold = CurrencyManager.Instance != null ? CurrencyManager.Instance.TotalGold : 0;
        AddRow("Уровень станции",
            level >= data.MaxLevel ? $"{level} · максимум" : $"{level} · вложено {invested}/{cost}",
            successColor, null, false, null);
        AddRow("Деньги", gold.ToString(), gold > 0 ? successColor : warningColor,
            null, false, null);
        AddRow("Уровень 1", "Без вложений", mutedColor,
            "Выбрать", true, () => DebugSetCharacterLevel(1));
        AddRow("Уровень 2", "Без вложений", mutedColor,
            "Выбрать", true, () => DebugSetCharacterLevel(2));
        AddRow("Уровень 3", "Без вложений", mutedColor,
            "Выбрать", true, () => DebugSetCharacterLevel(3));
        AddRow("Убрать вложения", "0", mutedColor,
            "Применить", level < data.MaxLevel,
            () => DebugSetCharacterInvestment(0));
        AddRow("Вложить половину стоимости", "50%", mutedColor,
            "Применить", level < data.MaxLevel,
            DebugSetCharacterInvestmentHalf);
        AddRow("Сбросить станцию", "Уровень 1, без вложений", warningColor,
            "Сбросить", true, DebugResetCharacterStation);

        AddSectionTitle("Персонаж", "Текущий выбор");
        CharacterData selectedCharacter =
            RunSelectionManager.Instance != null
                ? RunSelectionManager.Instance.SelectedCharacter
                : null;
        AddRow("Выбранный персонаж",
            selectedCharacter != null
                ? selectedCharacter.characterName
                : "Не выбран",
            selectedCharacter != null ? successColor : mutedColor,
            null, false, null);
        AddRow("Тип боя",
            selectedCharacter != null
                ? selectedCharacter.combatType.ToString()
                : "-",
            selectedCharacter != null ? successColor : mutedColor,
            null, false, null);
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

    private void CompleteLevel()
    {
        CloseMenu();
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

    private void AddEnemiesSection()
    {
        RemoveDestroyedDebugEnemies();
        AddSectionTitle("ВРАГИ", "Production spawn API; ручной spawn работает при выключенном автоспавне");

        bool aiFrozen = EnemyDebugAiFreeze.IsFrozen;
        AddSectionTitle(
            "ПОВЕДЕНИЕ ВРАГОВ",
            "Отключает только собственные движение и атаки AI"
        );
        AddRow(
            "РЕЖИМ AI",
            aiFrozen ? "AI ЗАМОРОЖЕН" : "АКТИВНО",
            aiFrozen ? warningColor : successColor,
            aiFrozen ? "АКТИВНО" : "AI ЗАМОРОЖЕН",
            true,
            ToggleEnemyAiFreeze
        );
        AddHint(
            aiFrozen
                ? $"Состояние: AI остановлен у: {EnemyHealth.ActiveInstances.Count} врагов"
                : "Состояние: собственное поведение врагов активно"
        );

        bool spawnerAvailable = enemySpawner != null;
        bool autoSpawn = spawnerAvailable && enemySpawner.IsSpawningEnabled;
        AddRow(
            "АВТОСПАВН",
            !spawnerAvailable ? "SPAWNER НЕ НАЙДЕН" : autoSpawn ? "ВКЛ" : "ВЫКЛ",
            autoSpawn ? successColor : spawnerAvailable ? mutedColor : warningColor,
            autoSpawn ? "ВЫКЛЮЧИТЬ" : "ВКЛЮЧИТЬ",
            spawnerAvailable,
            ToggleEnemyAutoSpawn
        );
        AddHint(enemyDebugStatus);

        AddSectionTitle("ASSAULT EVENTS", "Force пропускает таймер; одно событие одновременно");
        foreach (AssaultEventType type in Enum.GetValues(typeof(AssaultEventType)))
        {
            AssaultEventType captured = type;
            string label = type switch
            {
                AssaultEventType.BomberRush => "Force Bomber Rush",
                AssaultEventType.ShooterSquad => "Force Shooter Squad",
                AssaultEventType.Encirclement => "Force Encirclement",
                AssaultEventType.Crossfire => "Force Crossfire",
                _ => "Force Stampede"
            };
            AddRow(label, enemySpawner != null && enemySpawner.IsAssaultActive ? "ACTIVE" : "READY",
                accentColor, "FORCE", enemySpawner != null && !enemySpawner.IsAssaultActive, () =>
                {
                    CloseMenu();
                    if (!enemySpawner.ForceAssaultEvent(captured))
                        enemyDebugStatus = "Assault заблокирован состоянием run или нет безопасной формации.";
                });
        }

        AddSectionTitle("РУЧНОЙ SPAWN", "Безопасные разные позиции вокруг игрока");
        AddManualEnemyRows("ОБЫЧНЫЙ", ResolveEnemyPrefab(EnemySpawner.DebugEnemyArchetype.Basic));
        AddManualEnemyRows("СТРЕЛОК", ResolveEnemyPrefab(EnemySpawner.DebugEnemyArchetype.Shooter));
        AddManualEnemyRows("БОМБЕР", ResolveEnemyPrefab(EnemySpawner.DebugEnemyArchetype.Bomber));
        AddManualEnemyRows("EYES", eyesEnemyPrefab != null ? eyesEnemyPrefab : ResolveEnemyPrefab(EnemySpawner.DebugEnemyArchetype.Eyes));
        AddManualEnemyRows("ТУРЕЛЬ", turretEnemyPrefab != null ? turretEnemyPrefab : ResolveEnemyPrefab(EnemySpawner.DebugEnemyArchetype.Turret));
        // Real boss fights are owned by RunFlow; raw spawning stays in BossPracticeLab.

        AddRow("Создано вручную",
            debugEnemies.Count > 0 ? $"Активно: {debugEnemies.Count}" : "Нет",
            debugEnemies.Count > 0 ? successColor : mutedColor,
            "УБРАТЬ РУЧНЫХ", debugEnemies.Count > 0,
            ClearDebugEnemies);
    }

    private void AddFootballMinigameSection()
    {
        FootballMinigame football = FindFirstObjectByType<FootballMinigame>(FindObjectsInactive.Include);
        bool available = football != null && football.isActiveAndEnabled;
        if (!available) return;
        AddSectionTitle("Футбол", "Проверка мини-игры");
        string state = football.IsRunning ? "Идёт матч" : football.CanStart ? "Готов к старту" : "Матч завершён";
        AddRow("Матч", state, mutedColor,
            "Начать", football.CanStart, () => { football.StartGame(); RefreshCurrentTab(); });
        AddRow("Мячи", football.ActiveBallCount.ToString(), mutedColor,
            "Вернуть", football.IsRunning, () => football.ResetBall());
        AddRow("Вернуться в бункер", null, mutedColor,
            "Завершить", true, () => { football.ResetGame(); RefreshCurrentTab(); });
        AddRow("Границы поля", $"{football.ArenaWidth:0} × {football.ArenaHeight:0}", mutedColor,
            "Показать", true, () => football.ToggleDebugZones());
    }

    private EnemyHealth ResolveExperienceLootSource()
    {
        EnemyHealth source = turretEnemyPrefab != null
            ? turretEnemyPrefab.GetComponent<EnemyHealth>() : null;
        if (source != null && source.HasExperienceLoot)
            return source;
        source = eyesEnemyPrefab != null
            ? eyesEnemyPrefab.GetComponent<EnemyHealth>() : null;
        if (source != null && source.HasExperienceLoot)
            return source;
        source = enemySpawner?.FindDebugEnemyPrefab(EnemySpawner.DebugEnemyArchetype.Basic)
            ?.GetComponent<EnemyHealth>();
        return source != null && source.HasExperienceLoot ? source : null;
    }

    private void AddExperienceVisualLabSection()
    {
        AddSectionTitle("XP VISUALS / COMBAT LAB", "Production enemy loot / dev only");
        activeXpCounter = CreateText("Active XP", contentRoot, "", 15f,
            TextAlignmentOptions.MidlineLeft, successColor);
        activeXpCounter.gameObject.AddComponent<LayoutElement>().preferredHeight = 28f;
        RefreshActiveXpCounter();
        bool available = ResolveExperienceLootSource() != null &&
            GameObject.FindGameObjectWithTag("Player") != null;
        foreach (int count in new[] { 10, 50, 100, 250 })
        {
            int captured = count;
            AddRow($"{count} PICKUPS", "RADIUS 4–10", mutedColor,
                $"Spawn {count} XP", available, () => SpawnDebugExperience(captured));
        }
        AddRow("STRESS / 250 PICKUPS", "5 RINGS", mutedColor,
            "Spawn Stress Field", available, () => SpawnDebugExperience(250, true));
        AddRow("ALL ACTIVE XP", "NO XP AWARD", warningColor,
            "Clear XP", true, ClearDebugExperience);
        AddHint(available
            ? "Counts are pickups, not XP values. Close F1 to test pickup."
            : "Requires a player and an enemy prefab with production XP loot.");
    }

    private void RefreshActiveXpCounter()
    {
        if (activeXpCounter != null)
            activeXpCounter.text = $"ACTIVE XP: {FindObjectsByType<ExperiencePickup>(FindObjectsSortMode.None).Length}";
    }

    private void SpawnDebugExperience(int count, bool stress = false, bool trackForCleanup = false)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        EnemyHealth source = ResolveExperienceLootSource();
        if (player == null || source == null)
            return;

        if (trackForCleanup)
        {
            if (!CanUsePickupDebugTools) return;
            PruneTestPickups();
            RunStateManager.Instance?.DebugMarkDevelopmentRun();
        }
        int created = 0;
        Vector3 center = player.transform.position;
        float minimum = trackForCleanup
            ? Mathf.Max(4f, (player.GetComponent<PlayerPickupRadius>()?.CurrentRadius ?? 3f) + 1f) : 4f;
        for (int i = 0; i < count; i++)
        {
            Vector3 position;
            if (trackForCleanup)
            {
                if (!TryGetTestPickupPosition(player.transform, minimum, minimum + 6f, out position)) continue;
            }
            else
            {
                float angle = stress
                    ? (i / 5 + UnityEngine.Random.value) * (Mathf.PI * 2f / 50f)
                    : UnityEngine.Random.value * Mathf.PI * 2f;
                float radius = stress
                    ? 4.5f + i % 5 * 1.25f + UnityEngine.Random.Range(-0.4f, 0.4f)
                    : Mathf.Sqrt(UnityEngine.Random.Range(16f, 100f));
                position = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            }
            var spawned = source.SpawnLootPickup(position);
            if (spawned == null) continue;
            created++;
            if (trackForCleanup) testPickups.Add(spawned.GetComponent<ExperiencePickup>());
        }
        RefreshActiveXpCounter();
        if (trackForCleanup)
        {
            mvpStatus = $"Создано кристаллов опыта: {created}/{count}. Закройте F1 для подбора.";
            RefreshCurrentTab();
        }
    }

    private void ClearDebugExperience()
    {
        foreach (ExperiencePickup pickup in
            FindObjectsByType<ExperiencePickup>(FindObjectsSortMode.None))
            pickup.Despawn();
        RefreshActiveXpCounter();
    }

    private GameObject ResolveEnemyPrefab(
        EnemySpawner.DebugEnemyArchetype archetype)
    {
        return enemySpawner != null
            ? enemySpawner.FindDebugEnemyPrefab(archetype)
            : null;
    }

    private void AddManualEnemyRows(string displayName, GameObject prefab)
    {
        bool available = enemySpawner != null && prefab != null;
        string status = prefab == null ? "PREFAB НЕ НАЙДЕН" :
            enemySpawner == null ? "SPAWNER НЕ НАЙДЕН" : "ДОСТУПЕН";
        AddRow(displayName, status,
            available ? successColor : warningColor,
            "+1", available, () => SpawnDebugEnemies(prefab, displayName, 1));
        AddRow("↳ пакет", "Ровно десять попыток в разных позициях",
            available ? mutedColor : warningColor,
            "+10", available, () => SpawnDebugEnemies(prefab, displayName, 10));
    }

    private void SpawnDebugEnemies(GameObject prefab, string displayName, int count, bool flanking = false)
    {
        if (flanking && !MvpCanEdit) return;
        Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (enemySpawner == null || prefab == null || player == null)
        {
            enemyDebugStatus = "⚠ Spawn невозможен: отсутствует player, prefab или EnemySpawner.";
            return;
        }

        int spawned = 0;
        for (int i = 0; i < Mathf.Max(1, count); i++)
        {
            string flankingStatus = null;
            GameObject enemy = flanking
                ? enemySpawner.DebugSpawnFlankingChaserAround(prefab, player.position, out flankingStatus)
                : enemySpawner.SpawnSpecificEnemyAround(
                prefab,
                player.position,
                3f,
                8f,
                3f,
                false,
                0.45f
            );

            if (enemy == null && flanking)
            {
                enemyDebugStatus = flankingStatus;
                RefreshCurrentTab();
                return;
            }
            if (enemy == null)
                continue;

            debugEnemies.Add(enemy);
            spawned++;
        }

        enemyDebugStatus = spawned == count
            ? $"✓ {displayName}: создано {spawned}."
            : $"⚠ {displayName}: создано {spawned} из {count}; не хватило безопасных позиций.";

        if (flanking && spawned > 0)
        {
            CloseMenu();
            return;
        }
        RefreshCurrentTab();
    }

    private void ToggleEnemyAutoSpawn()
    {
        if (enemySpawner == null) return;
        SetEnemyAutoSpawn(!enemySpawner.IsSpawningEnabled);
        enemyDebugStatus = enemySpawner.IsSpawningEnabled
            ? "✓ Production автоспавн возобновлён."
            : "✓ Автоспавн остановлен; ручной spawn доступен.";
        RefreshCurrentTab();
    }

    private void SetEnemyAutoSpawn(bool enabled, bool captureSettings = false)
    {
        if (captureSettings) CaptureSession.SetSpawning(enabled);
        else if (enemySpawner != null)
        {
            if (enabled) enemySpawner.ResumeSpawning();
            else enemySpawner.StopSpawning();
        }
    }

    private void ToggleEnemyAiFreeze()
    {
        bool frozen = !EnemyDebugAiFreeze.IsFrozen;
        SetEnemyAiFrozen(frozen);
        enemyDebugStatus = frozen
            ? $"✓ AI остановлен у: {EnemyHealth.ActiveInstances.Count} врагов. Новые враги наследуют режим."
            : "✓ Собственное движение и атаки врагов восстановлены.";
        RefreshCurrentTab();
    }

    private void SetEnemyAiFrozen(bool frozen, bool captureSettings = false)
    {
        if (captureSettings) CaptureSession.SetEnemyAiFrozen(frozen);
        else EnemyDebugAiFreeze.SetFrozen(frozen);
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

    private void AddSectionTitle(string title, string subtitle)
    {
        RectTransform section = CreateRect(title, contentRoot);
        bool compact = mvpOrbitSurfaceOpen;
        section.gameObject.AddComponent<LayoutElement>().preferredHeight =
            compact ? 26f : 46f;
        section.gameObject.AddComponent<Image>().color =
            new Color(accentColor.r, accentColor.g, accentColor.b, 0.18f);
        TextMeshProUGUI text = CreateText(
            "Label", section,
            compact
                ? $"<b>{title}</b>"
                : $"<b>{title}</b>\n<size=13><color=#A6AFBC>{subtitle}</color></size>",
            compact ? 11f : 17f,
            TextAlignmentOptions.MidlineLeft,
            Color.white
        );
        Stretch(text.rectTransform,
            compact ? 7f : 16f,
            compact ? 7f : 12f,
            compact ? 2f : 7f,
            compact ? 2f : 7f);
    }

    private void AddRow(
        string label,
        string status,
        Color statusColor,
        string buttonLabel,
        bool buttonEnabled,
        UnityEngine.Events.UnityAction action)
    {
        bool compact = mvpOrbitSurfaceOpen;
        RectTransform row = CreateRect(label, contentRoot);
        row.gameObject.AddComponent<Image>().color = rowColor;
        row.gameObject.AddComponent<LayoutElement>().preferredHeight =
            compact ? 30f : 54f;

        float buttonWidth = compact
            ? Mathf.Clamp(GetButtonWidth(buttonLabel), 58f, 76f)
            : GetButtonWidth(buttonLabel);
        float rightPadding = string.IsNullOrEmpty(buttonLabel)
            ? (compact ? 7f : 18f)
            : buttonWidth + (compact ? 9f : 36f);
        TextMeshProUGUI labelText = CreateText(
            "Name", row, label, compact ? 11f : 19f,
            TextAlignmentOptions.MidlineLeft, Color.white
        );
        labelText.rectTransform.anchorMin = Vector2.zero;
        labelText.rectTransform.anchorMax = new Vector2(
            compact ? 0.56f : 0.48f, 1f);
        labelText.rectTransform.offsetMin = new Vector2(
            compact ? 7f : 16f, 0f);
        labelText.rectTransform.offsetMax = Vector2.zero;
        labelText.overflowMode = TextOverflowModes.Ellipsis;

        TextMeshProUGUI statusText = CreateText(
            "Status", row, status, compact ? 9f : 15f,
            TextAlignmentOptions.MidlineRight, statusColor
        );
        statusText.rectTransform.anchorMin = new Vector2(
            compact ? 0.50f : 0.43f, 0f);
        statusText.rectTransform.anchorMax = Vector2.one;
        statusText.rectTransform.offsetMin = Vector2.zero;
        statusText.rectTransform.offsetMax = new Vector2(-rightPadding, 0f);
        statusText.overflowMode = TextOverflowModes.Ellipsis;

        if (string.IsNullOrEmpty(buttonLabel))
            return;

        Button button = CreateButton(
            row, buttonLabel, action, buttonWidth, buttonEnabled
        );
        RectTransform buttonRect = button.GetComponent<RectTransform>();
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(1f, 0.5f);
        buttonRect.pivot = new Vector2(1f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(compact ? -4f : -12f, 0f);
        buttonRect.sizeDelta = new Vector2(
            buttonWidth, compact ? 22f : 38f);
        if (compact)
        {
            TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
                text.fontSize = 9f;
        }
    }

    private void AddSliderRow(
        string label,
        float value,
        float minimum,
        float maximum,
        System.Action<float> setter,
        string format)
    {
        minimum = Mathf.Min(minimum, value);
        maximum = Mathf.Max(maximum, value);
        if (Mathf.Approximately(minimum, maximum))
            maximum = minimum + 1f;

        RectTransform row = CreateRect(label, contentRoot);
        row.gameObject.AddComponent<Image>().color =
            new Color(rowColor.r, rowColor.g, rowColor.b, 0.72f);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;

        TextMeshProUGUI labelText = CreateText(
            "Name", row, label, 11f,
            TextAlignmentOptions.MidlineLeft, Color.white);
        labelText.rectTransform.anchorMin = Vector2.zero;
        labelText.rectTransform.anchorMax = new Vector2(0.42f, 1f);
        labelText.rectTransform.offsetMin = new Vector2(7f, 0f);
        labelText.rectTransform.offsetMax = Vector2.zero;
        labelText.overflowMode = TextOverflowModes.Ellipsis;

        RectTransform sliderRoot = CreateRect("Slider", row);
        sliderRoot.anchorMin = new Vector2(0.43f, 0.5f);
        sliderRoot.anchorMax = new Vector2(0.82f, 0.5f);
        sliderRoot.pivot = new Vector2(0.5f, 0.5f);
        sliderRoot.offsetMin = new Vector2(0f, -10f);
        sliderRoot.offsetMax = new Vector2(0f, 10f);
        Image sliderHitArea = sliderRoot.gameObject.AddComponent<Image>();
        sliderHitArea.color = Color.clear;
        sliderHitArea.raycastTarget = true;

        RectTransform background = CreateRect("Background", sliderRoot);
        background.anchorMin = new Vector2(0f, 0.5f);
        background.anchorMax = new Vector2(1f, 0.5f);
        background.sizeDelta = new Vector2(0f, 4f);
        Image backgroundImage = background.gameObject.AddComponent<Image>();
        backgroundImage.color = new Color(0.28f, 0.31f, 0.35f, 1f);
        backgroundImage.raycastTarget = false;

        RectTransform fillArea = CreateRect("Fill Area", sliderRoot);
        fillArea.anchorMin = new Vector2(0f, 0.5f);
        fillArea.anchorMax = new Vector2(1f, 0.5f);
        fillArea.offsetMin = new Vector2(5f, -2f);
        fillArea.offsetMax = new Vector2(-5f, 2f);
        RectTransform fill = CreateRect("Fill", fillArea);
        Stretch(fill);
        Image fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = new Color(
            accentColor.r, accentColor.g, accentColor.b, 0.9f);
        fillImage.raycastTarget = false;

        RectTransform handleArea = CreateRect("Handle Slide Area", sliderRoot);
        handleArea.anchorMin = new Vector2(0f, 0.5f);
        handleArea.anchorMax = new Vector2(1f, 0.5f);
        handleArea.offsetMin = new Vector2(6f, -8f);
        handleArea.offsetMax = new Vector2(-6f, 8f);
        RectTransform handle = CreateRect("Handle", handleArea);
        handle.sizeDelta = new Vector2(12f, 14f);
        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = accentColor;

        Slider slider = sliderRoot.gameObject.AddComponent<Slider>();
        slider.enabled = true;
        slider.interactable = true;
        slider.minValue = minimum;
        slider.maxValue = maximum;
        slider.wholeNumbers = false;
        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };

        TextMeshProUGUI valueText = CreateText(
            "Value", row, value.ToString(format), 11f,
            TextAlignmentOptions.MidlineRight, successColor);
        valueText.rectTransform.anchorMin = new Vector2(0.83f, 0f);
        valueText.rectTransform.anchorMax = Vector2.one;
        valueText.rectTransform.offsetMin = Vector2.zero;
        valueText.rectTransform.offsetMax = new Vector2(-7f, 0f);
        valueText.raycastTarget = false;

        slider.SetValueWithoutNotify(Mathf.Clamp(value, minimum, maximum));
        slider.onValueChanged.AddListener(current =>
        {
            valueText.text = current.ToString(format);
            setter?.Invoke(current);
        });
    }

    private static float GetButtonWidth(string label) =>
        Mathf.Clamp(60f + (string.IsNullOrEmpty(label) ? 0f : label.Length * 8.5f), 116f, 280f);

    private void AddHint(string message)
    {
        bool compact = mvpOrbitSurfaceOpen;
        TextMeshProUGUI hint = CreateText(
            "Hint", contentRoot, message, compact ? 10f : 15f,
            compact
                ? TextAlignmentOptions.MidlineLeft
                : TextAlignmentOptions.Center,
            mutedColor
        );
        hint.textWrappingMode = TextWrappingModes.Normal;
        hint.gameObject.AddComponent<LayoutElement>().preferredHeight =
            compact ? 32f : 56f;
        if (compact)
        {
            hint.margin = new Vector4(7f, 0f, 7f, 0f);
            hint.overflowMode = TextOverflowModes.Ellipsis;
        }
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
}
#endif
