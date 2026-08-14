using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
public enum CombatLabControlStyle
{
    Orbit,
    Remote
}

[DisallowMultipleComponent]
public sealed class CombatLabDebugController : MonoBehaviour
{
    private CharacterSpawner characterSpawner;
    private GameObject player;
    private TelekinesisDebugPrototype telekinesis;
    private BaseWeapon primaryWeapon;
    private WeaponData pistol;
    private WeaponData laser;
    private bool initialized;

    public CombatLabControlStyle ControlStyle { get; private set; } =
        CombatLabControlStyle.Orbit;
    public WeaponData SelectedWeapon { get; private set; }
    public WeaponControlMode FireMode { get; private set; }
    public WeaponData Pistol => pistol;
    public WeaponData Laser => laser;
    public bool IsAvailable => player != null && primaryWeapon != null &&
        telekinesis != null && telekinesis.IsAvailable;

    public string CurrentSummary =>
        $"{ControlStyle.ToString().ToUpperInvariant()} / " +
        $"{GetWeaponLabel(SelectedWeapon).ToUpperInvariant()} / " +
        $"{(FireMode == WeaponControlMode.AutoAim ? "AUTO" : "MANUAL")}";

    public void Configure(
        CharacterSpawner spawner,
        WeaponData pistolData,
        WeaponData laserData)
    {
        characterSpawner = spawner;
        pistol = pistolData;
        laser = laserData;

        if (!initialized)
        {
            FireMode = WeaponControlSettings.CurrentMode;
            initialized = true;
        }

        RefreshBinding();
    }

    public bool RefreshBinding()
    {
        GameObject currentPlayer = GameObject.FindGameObjectWithTag("Player");
        bool playerChanged = currentPlayer != player;

        if (playerChanged)
        {
            player = currentPlayer;
            telekinesis = null;
            primaryWeapon = null;
        }

        if (player == null)
            return false;

        telekinesis ??= player.GetComponent<TelekinesisDebugPrototype>();
        telekinesis ??= player.AddComponent<TelekinesisDebugPrototype>();
        telekinesis.Configure(characterSpawner);

        BaseWeapon resolved = ResolvePrimaryWeapon(player);
        bool weaponChanged = resolved != primaryWeapon;
        primaryWeapon = resolved;

        if (primaryWeapon != null)
        {
            SelectedWeapon = primaryWeapon.weaponData;
            telekinesis.SetPrimaryWeapon(primaryWeapon);
        }

        FireMode = WeaponControlSettings.CurrentMode;

        TelekinesisDebugMode expectedMode =
            ControlStyle == CombatLabControlStyle.Remote
                ? TelekinesisDebugMode.Remote
                : TelekinesisDebugMode.Base;
        bool controlModeChanged = telekinesis.CurrentMode != expectedMode;

        if ((playerChanged || weaponChanged || controlModeChanged) &&
            IsAvailable)
        {
            ApplyControlStyle();
        }

        return IsAvailable;
    }

    public bool SelectControlStyle(CombatLabControlStyle style)
    {
        ControlStyle = style;

        if (!RefreshBinding())
            return false;

        return ApplyControlStyle();
    }

    public void SelectFireMode(WeaponControlMode mode)
    {
        FireMode = mode;
        WeaponControlSettings.SetMode(mode);
    }

    public bool SelectWeapon(WeaponData weaponData)
    {
        if (!IsCombatLabWeapon(weaponData) || !RefreshBinding() ||
            characterSpawner == null)
        {
            return false;
        }

        if (!characterSpawner.TryReplaceDebugPrimaryWeapon(
                player,
                weaponData,
                out BaseWeapon replacement))
        {
            return false;
        }

        primaryWeapon = replacement;
        SelectedWeapon = replacement != null
            ? replacement.weaponData
            : weaponData;
        telekinesis = player.GetComponent<TelekinesisDebugPrototype>();

        if (telekinesis == null || primaryWeapon == null)
            return false;

        telekinesis.Configure(characterSpawner);
        telekinesis.SetPrimaryWeapon(primaryWeapon);
        WeaponControlSettings.SetMode(FireMode);
        return ApplyControlStyle();
    }

    private bool ApplyControlStyle()
    {
        if (telekinesis == null || !telekinesis.IsAvailable)
            return false;

        return telekinesis.ApplyMode(
            ControlStyle == CombatLabControlStyle.Remote
                ? TelekinesisDebugMode.Remote
                : TelekinesisDebugMode.Base
        );
    }

    private bool IsCombatLabWeapon(WeaponData weaponData)
    {
        return weaponData != null &&
            (weaponData == pistol || weaponData == laser);
    }

    private static BaseWeapon ResolvePrimaryWeapon(GameObject owner)
    {
        if (owner == null)
            return null;

        BaseWeapon[] weapons = owner.GetComponentsInChildren<BaseWeapon>(true);

        for (int i = 0; i < weapons.Length; i++)
        {
            BaseWeapon candidate = weapons[i];

            if (candidate != null && !candidate.IsTelekinesisDebugSecondary)
                return candidate;
        }

        return null;
    }

    private static string GetWeaponLabel(WeaponData weaponData)
    {
        if (weaponData == null)
            return "None";

        return string.IsNullOrWhiteSpace(weaponData.weaponName)
            ? weaponData.name
            : weaponData.weaponName;
    }
}
#endif

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
        VisualTest,
        SectorTest
    }

    private enum UpgradeFilter
    {
        All,
        Numeric,
        Behavior,
        OutOfPool
    }

    private enum PreviewParameter
    {
        EnemyBrightness,
        EnemySaturation,
        EnemyTint,
        EnemyOutline,
        EnemyOutlineWidth,
        OutsideDarkness,
        OutsideColor,
        FocusTransition
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
        "VISUAL TEST",
        "ТЕСТ СЕКТОРА"
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
    private GameObject fullMenuBlocker;
    private Image fullMenuBlockerImage;
    private RectTransform menuPanel;
    private GameObject previewPanelRoot;
    private TextMeshProUGUI previewText;
    private RectTransform contentRoot;
    private readonly GameObject[] tabRoots = new GameObject[TabLabels.Length];
    private readonly Image[] tabButtonImages = new Image[TabLabels.Length];
    private DebugTab activeTab = DebugTab.Run;
    private UpgradeFilter upgradeFilter = UpgradeFilter.All;
    private ProductionSectorDebugController productionSectorDebug;
    private bool isOpen;
    private bool isPreview;
    private bool menuLiveSimulation;
    private PreviewParameter previewParameter;
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
    private readonly StringBuilder previewSummary = new();
    private readonly List<WorldEvent> addedEventPrefabs = new();
    private readonly List<CharacterData> debugCharacters = new();
    private readonly List<GameObject> debugEnemies = new();
    private string enemyDebugStatus = "Готово к ручному тесту.";
    private string lootChestDebugStatus = "Готово к тесту сундука.";
    private readonly List<UpgradeData> visibleUpgrades = new();
    private TelekinesisDebugPrototype telekinesisPrototype;
    private CombatLabDebugController combatLab;

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
        SelectTab(activeTab, false);
        if (menuRoot != null)
            menuRoot.SetActive(false);
    }

    private void Update()
    {
        if (waitingForF1Release)
        {
            if (!Input.GetKey(KeyCode.F1))
                waitingForF1Release = false;

            if (isPreview)
                UpdatePreviewInput();

            return;
        }

        if (isPreview)
        {
            UpdatePreviewInput();

            if (Input.GetKeyDown(KeyCode.F1))
            {
                waitingForF1Release = true;
                ReturnFromPreviewToFullMenu();
            }

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

        isPreview = false;
        if (previewPanelRoot != null)
            previewPanelRoot.SetActive(false);
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
        previousTimeScale = Time.timeScale;
        previousCursorVisible = Cursor.visible;
        previousCursorLockMode = Cursor.lockState;
        menuLiveSimulation = false;
        RefreshTab(activeTab);

        Time.timeScale = 0f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        isOpen = true;
        menuRoot.SetActive(true);
        fullMenuBlocker?.SetActive(true);
        previewPanelRoot?.SetActive(false);
    }

    private void CloseMenu()
    {
        if (!isOpen)
            return;

        if (menuRoot != null)
            menuRoot.SetActive(false);
        RestoreGameState();
        isOpen = false;
        isPreview = false;
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

    private void EnterScenePreview()
    {
        if (!isOpen || productionSectorDebug == null)
            return;

        isOpen = false;
        isPreview = true;
        fullMenuBlocker?.SetActive(false);
        previewPanelRoot?.SetActive(true);
        RestoreGameState();
        UpdatePreviewPanel();
    }

    private void ReturnFromPreviewToFullMenu()
    {
        if (!isPreview)
            return;

        isPreview = false;
        previewPanelRoot?.SetActive(false);
        OpenMenu();
    }

    private void UpdatePreviewInput()
    {
        if (Input.GetKeyDown(KeyCode.PageUp))
        {
            int count = System.Enum.GetValues(typeof(PreviewParameter)).Length;
            previewParameter = (PreviewParameter)(
                ((int)previewParameter - 1 + count) % count
            );
            UpdatePreviewPanel();
        }
        else if (Input.GetKeyDown(KeyCode.PageDown))
        {
            int count = System.Enum.GetValues(typeof(PreviewParameter)).Length;
            previewParameter = (PreviewParameter)(
                ((int)previewParameter + 1) % count
            );
            UpdatePreviewPanel();
        }

        bool largeStep = Input.GetKey(KeyCode.LeftShift) ||
            Input.GetKey(KeyCode.RightShift);

        if (Input.GetKeyDown(KeyCode.LeftBracket))
            AdjustPreviewParameter(-1f, largeStep);
        else if (Input.GetKeyDown(KeyCode.RightBracket))
            AdjustPreviewParameter(1f, largeStep);

        if (Input.GetKeyDown(KeyCode.F5))
            SelectPreviewPreset(ProductionSectorDebugController.EnemyReadability.Off);
        else if (Input.GetKeyDown(KeyCode.F6))
            SelectPreviewPreset(ProductionSectorDebugController.EnemyReadability.Low);
        else if (Input.GetKeyDown(KeyCode.F7))
            SelectPreviewPreset(ProductionSectorDebugController.EnemyReadability.Medium);
        else if (Input.GetKeyDown(KeyCode.F8))
            SelectPreviewPreset(ProductionSectorDebugController.EnemyReadability.High);
    }

    private void SelectPreviewPreset(
        ProductionSectorDebugController.EnemyReadability preset)
    {
        productionSectorDebug?.SetEnemyReadability(preset);
        UpdatePreviewPanel();
    }

    private void AdjustPreviewParameter(float direction, bool largeStep)
    {
        ProductionSectorDebugController debug = productionSectorDebug;
        if (debug == null)
            return;

        float step = largeStep ? 0.25f : 0.1f;

        switch (previewParameter)
        {
            case PreviewParameter.EnemyBrightness:
                debug.SetEnemyBrightness(debug.EnemyBrightness + direction * step);
                break;
            case PreviewParameter.EnemySaturation:
                debug.SetEnemySaturation(debug.EnemySaturation + direction * step);
                break;
            case PreviewParameter.EnemyTint:
                debug.SetEnemyTintStrength(debug.EnemyTintStrength + direction * step);
                break;
            case PreviewParameter.EnemyOutline:
                float outline = debug.EnemyOutlineStrength + direction * step;
                debug.SetEnemyOutlineStrength(outline);
                debug.SetEnemyOutlineEnabled(outline > 0f);
                break;
            case PreviewParameter.EnemyOutlineWidth:
                debug.SetEnemyOutlineWidth(
                    debug.EnemyOutlineWidth + direction * (largeStep ? 1f : 0.5f)
                );
                break;
            case PreviewParameter.OutsideDarkness:
                anomalyController?.SetOutsideDarkness(
                    anomalyController.OutsideDarkness + direction * step
                );
                break;
            case PreviewParameter.OutsideColor:
                anomalyController?.SetOutsideColor(
                    anomalyController.OutsideColor + direction * step
                );
                break;
            case PreviewParameter.FocusTransition:
                anomalyController?.SetFocusTransition(
                    anomalyController.FocusTransition +
                    direction * (largeStep ? 0.05f : 0.01f)
                );
                break;
        }

        UpdatePreviewPanel();
    }

    private void UpdatePreviewPanel()
    {
        if (previewText == null || productionSectorDebug == null)
            return;

        ProductionSectorDebugController debug = productionSectorDebug;
        previewSummary.Clear();
        previewSummary.AppendLine("<b>ВИЗУАЛЬНЫЙ ТЕСТ</b>");
        previewSummary.Append("Читаемость: <b>");
        previewSummary.Append(GetEnemyReadabilityName(debug.EnemyMode));
        previewSummary.AppendLine("</b>");
        previewSummary.AppendLine("F5 ВЫКЛ  F6 СЛАБО  F7 СРЕДНЕ  F8 СИЛЬНО");
        previewSummary.AppendLine();
        AppendPreviewLine(PreviewParameter.EnemyBrightness,
            "ЯРКОСТЬ ВРАГОВ", debug.EnemyBrightness.ToString("0.00"));
        AppendPreviewLine(PreviewParameter.EnemySaturation,
            "НАСЫЩЕННОСТЬ", debug.EnemySaturation.ToString("0.00"));
        AppendPreviewLine(PreviewParameter.EnemyTint,
            "ОТТЕНОК", debug.EnemyTintStrength.ToString("0.00"));
        AppendPreviewLine(PreviewParameter.EnemyOutline,
            "КОНТУР", debug.EnemyOutlineEnabled
                ? debug.EnemyOutlineStrength.ToString("0.00")
                : "ВЫКЛ");
        AppendPreviewLine(PreviewParameter.EnemyOutlineWidth,
            "ТОЛЩИНА КОНТУРА", debug.EnemyOutlineWidth.ToString("0.0"));
        AppendPreviewLine(PreviewParameter.OutsideDarkness,
            "ЗАТЕМНЕНИЕ СНАРУЖИ",
            anomalyController != null
                ? anomalyController.OutsideDarkness.ToString("0.00")
                : "НЕТ CONTROLLER");
        AppendPreviewLine(PreviewParameter.OutsideColor,
            "ЦВЕТ СНАРУЖИ",
            anomalyController != null
                ? anomalyController.OutsideColor.ToString("0.00")
                : "НЕТ CONTROLLER");
        AppendPreviewLine(PreviewParameter.FocusTransition,
            "ПЕРЕХОД",
            anomalyController != null
                ? anomalyController.FocusTransition.ToString("0.00") + " сек"
                : "НЕТ CONTROLLER");
        previewSummary.AppendLine();
        previewSummary.AppendLine("Optional shortcuts: PageUp/PageDown, [ / ], Shift");
        previewSummary.AppendLine("Основное mouse-only управление: F1 → VISUAL TEST");
        previewSummary.AppendLine("F1 — назад в полное меню");
        previewSummary.AppendLine("Контур ограничен геометрией sprite.");
        previewText.text = previewSummary.ToString();
    }

    private void AppendPreviewLine(
        PreviewParameter parameter,
        string label,
        string value)
    {
        bool selected = previewParameter == parameter;
        if (selected)
            previewSummary.Append("<color=#42D9F5><b>▶ ");
        else
            previewSummary.Append("  ");

        previewSummary.Append(label);
        previewSummary.Append(": ");
        previewSummary.Append(value);

        if (selected)
            previewSummary.Append("</b></color>");

        previewSummary.AppendLine();
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
        if (productionSectorDebug != null)
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

        string[] labels = TabLabels;
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
                () => SelectTab((DebugTab)captured),
                100f
            );
            Stretch(button.GetComponent<RectTransform>());
            TextMeshProUGUI tabText = button.GetComponentInChildren<TextMeshProUGUI>();
            if (tabText != null && labels.Length > 7)
                tabText.fontSize = 12f;
            tabButtonImages[i] = button.targetGraphic as Image;
        }

        RectTransform pages = CreateRect("Tab Pages", panel);
        pages.anchorMin = Vector2.zero;
        pages.anchorMax = Vector2.one;
        pages.offsetMin = new Vector2(18f, 18f);
        pages.offsetMax = new Vector2(-18f, -120f);

        for (int i = 0; i < labels.Length; i++)
            tabRoots[i] = CreateTabPage(labels[i], pages, out _);

        BuildPreviewPanel();
    }

    private void BuildPreviewPanel()
    {
        RectTransform panel = CreateRect(
            "Sector Visual Preview",
            menuRoot.transform
        );
        previewPanelRoot = panel.gameObject;
        panel.anchorMin = panel.anchorMax = new Vector2(0f, 1f);
        panel.pivot = new Vector2(0f, 1f);
        panel.anchoredPosition = new Vector2(18f, -18f);
        panel.sizeDelta = new Vector2(430f, 520f);

        Image background = panel.gameObject.AddComponent<Image>();
        background.color = new Color(0.025f, 0.035f, 0.05f, 0.9f);
        background.raycastTarget = false;

        previewText = CreateText(
            "Preview Values",
            panel,
            string.Empty,
            18f,
            TextAlignmentOptions.TopLeft,
            Color.white
        );
        previewText.textWrappingMode = TextWrappingModes.Normal;
        Stretch(previewText.rectTransform, 18f, 18f, 16f, 16f);
        previewPanelRoot.SetActive(false);
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
        ApplyMenuViewportLayout();

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

    private void RefreshAllTabs()
    {
        for (int i = 0; i < tabRoots.Length; i++)
            RefreshTab((DebugTab)i);
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
                AddCombatLabSection();
                AddWeaponsSection();
                AddGravityConstructSection();
                AddRiftConstructSection();
                AddUpgradesSection();
                break;
            case DebugTab.Telekinesis:
                AddTelekinesisSection();
                break;
            case DebugTab.VisualTest:
                AddInteractiveAnomalyVisualTest();
                break;
            case DebugTab.SectorTest:
                AddProductionSectorTestSection();
                break;
        }

        AddHint(menuLiveSimulation
            ? "F1 закрывает меню. LIVE: симуляция продолжает работать."
            : "F1 закрывает меню. PAUSED: симуляция остановлена.");
    }

    private void ApplyMenuViewportLayout()
    {
        if (menuPanel == null)
            return;

        bool compact = activeTab == DebugTab.VisualTest;
        menuPanel.anchorMin = compact
            ? new Vector2(0.015f, 0.03f)
            : new Vector2(0.07f, 0.05f);
        menuPanel.anchorMax = compact
            ? new Vector2(0.72f, 0.97f)
            : new Vector2(0.93f, 0.95f);
        menuPanel.offsetMin = Vector2.zero;
        menuPanel.offsetMax = Vector2.zero;

        if (fullMenuBlockerImage != null)
        {
            fullMenuBlockerImage.color = compact
                ? new Color(0f, 0f, 0f, 0.24f)
                : new Color(0f, 0f, 0f, 0.68f);
        }
    }

    private void AddGravityConstructSection()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        AnomalyCoreRuntime coreRuntime = player != null
            ? player.GetComponent<AnomalyCoreRuntime>()
            : null;
        bool enabled = coreRuntime != null &&
            coreRuntime.IsCoreActive(AnomalyCoreId.Gravity);
        bool available = coreRuntime != null &&
            coreRuntime.CurrentWeapon != null;

        AddSectionTitle(
            "GRAVITY CONSTRUCT CONTRACT",
            "Independent gravity orb with optional weapon payload"
        );
        AddRow(
            "GRAVITY CONSTRUCT",
            enabled
                ? $"ACTIVE - {GetWeaponName(coreRuntime.CurrentWeapon.weaponData)}"
                : available ? "READY" : "PLAYER/WEAPON NOT FOUND",
            enabled ? successColor : available ? mutedColor : warningColor,
            enabled ? "TURN OFF" : "TURN ON",
            available,
            ToggleGravityConstruct
        );

        bool payloadEnabled = false;
        bool hasPayloadToggle = coreRuntime != null &&
            coreRuntime.TryGetWeaponPayloadEnabled(
                AnomalyCoreId.Gravity,
                out payloadEnabled);
        AddRow(
            "WEAPON PAYLOAD",
            hasPayloadToggle
                ? payloadEnabled ? "PISTOL/LASER PAYLOAD ON" : "BASE GRAVITY ONLY"
                : "ACTIVATE GRAVITY FIRST",
            hasPayloadToggle && payloadEnabled ? successColor : mutedColor,
            hasPayloadToggle && payloadEnabled ? "TURN OFF" : "TURN ON",
            hasPayloadToggle,
            ToggleGravityWeaponPayload
        );
        AddHint(
            "The orb always orbits and deals direct base damage. Weapon " +
            "payload optionally emits the current BaseWeapon attack outward."
        );
    }

    private void ToggleGravityConstruct()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        AnomalyCoreRuntime coreRuntime = player != null
            ? player.GetComponent<AnomalyCoreRuntime>()
            : null;

        if (coreRuntime == null)
            return;

        if (coreRuntime.IsCoreActive(AnomalyCoreId.Gravity))
            coreRuntime.DeactivateCore(AnomalyCoreId.Gravity);
        else
            coreRuntime.ActivateCore(AnomalyCoreId.Gravity);

        RefreshTab(DebugTab.WeaponsAndUpgrades);
    }

    private void ToggleGravityWeaponPayload()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        AnomalyCoreRuntime coreRuntime = player != null
            ? player.GetComponent<AnomalyCoreRuntime>()
            : null;

        if (coreRuntime == null ||
            !coreRuntime.TryGetWeaponPayloadEnabled(
                AnomalyCoreId.Gravity,
                out bool enabled))
        {
            return;
        }

        coreRuntime.TrySetWeaponPayloadEnabled(
            AnomalyCoreId.Gravity,
            !enabled
        );
        RefreshTab(DebugTab.WeaponsAndUpgrades);
    }

    private void AddRiftConstructSection()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        AnomalyCoreRuntime coreRuntime = player != null
            ? player.GetComponent<AnomalyCoreRuntime>()
            : null;
        bool enabled = coreRuntime != null &&
            coreRuntime.IsCoreActive(AnomalyCoreId.Rift);
        bool available = coreRuntime != null &&
            coreRuntime.CurrentWeapon != null;

        AddSectionTitle(
            "RIFT CONSTRUCT CONTRACT",
            "Delayed impact with radial polymorphic weapon burst"
        );
        AddRow(
            "RIFT",
            enabled
                ? $"ACTIVE - {GetWeaponName(coreRuntime.CurrentWeapon.weaponData)}"
                : available ? "READY" : "PLAYER/WEAPON NOT FOUND",
            enabled ? successColor : available ? mutedColor : warningColor,
            enabled ? "TURN OFF" : "TURN ON",
            available,
            ToggleRiftConstruct
        );
        AddHint(
            "AnomalyCoreRuntime owns Rift independently from Gravity. " +
            "Both may be active at the same time."
        );
    }

    private void ToggleRiftConstruct()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        AnomalyCoreRuntime coreRuntime = player != null
            ? player.GetComponent<AnomalyCoreRuntime>()
            : null;

        if (coreRuntime == null)
            return;

        if (coreRuntime.IsCoreActive(AnomalyCoreId.Rift))
            coreRuntime.DeactivateCore(AnomalyCoreId.Rift);
        else
            coreRuntime.ActivateCore(AnomalyCoreId.Rift);

        RefreshTab(DebugTab.WeaponsAndUpgrades);
    }

    private void AddWeaponCoreSection()
    {
        AddSectionTitle("ЯДРО ОРУЖИЯ",
            "Runtime-переключатель существующего debug core");
        AddOptionRow("БЕЗ ЯДРА",
            WeaponCoreDebugSelector.ActiveCore == WeaponCoreType.None,
            true, () => WeaponCoreDebugSelector.Select(WeaponCoreType.None));
        AddOptionRow("ЦЕПНОЕ ЯДРО",
            WeaponCoreDebugSelector.ActiveCore == WeaponCoreType.Chain,
            true, () => WeaponCoreDebugSelector.Select(WeaponCoreType.Chain));
        AddHint("Цепное ядро передаёт часть урона основного оружия " +
            "соседней цели; без ядра оружие работает штатно.");
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
            "ПРЕДПРОСМОТР СЦЕНЫ",
            "Компактная панель; gameplay продолжает работать без паузы"
        );
        AddRow(
            "ВИЗУАЛЬНЫЙ TUNING",
            "80–90% сцены остаётся открытым",
            successColor,
            "ПРЕДПРОСМОТР",
            true,
            EnterScenePreview
        );

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
            "Меняет существующее окружение сектора сразу; gameplay-объекты исключены"
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
            $"Меняет деревья / траву / декор карты. Объектов: {debug.DecorObjectCount}; renderer'ов: {debug.DecorRendererCount}"
        );
        AddProductionFloatOptions(
            new[] { 1.2f, 1f, 0.75f, 0.5f, 0.25f },
            debug.DecorBrightness,
            value => debug.SetDecorBrightness(value),
            value => $"{value * 100f:0}%"
        );
        AddHint(debug.DecorRendererCount > 0
            ? $"✓ Применено к {debug.DecorRendererCount} renderer'ам декора."
            : "⚠ Renderer'ы декора не найдены.");
        AddRow(
            "ОБНОВИТЬ ЦЕЛИ ВИЗУАЛА",
            "Ручной поиск нового runtime-декора и anomaly renderer'ов",
            mutedColor,
            "ОБНОВИТЬ",
            true,
            RefreshSectorVisualTargets
        );

        AddSectionTitle(
            "АКЦЕНТ АНОМАЛИИ",
            $"Визуальное выделение активных зон. Зон: {debug.AnomalyZoneCount}; renderer'ов: {debug.AnomalyRendererCount}"
        );
        AddProductionFloatOptions(
            new[] { 1f, 1.25f, 1.5f, 1.75f },
            debug.AnomalyAccent,
            value => debug.SetAnomalyAccent(value),
            value => $"{value * 100f:0}%"
        );
        AddHint(debug.AnomalyZoneCount > 0
            ? $"✓ Акцент применён к {debug.AnomalyZoneCount} зонам."
            : "⚠ Активные anomaly visuals не найдены.");

        AddHint("Anomaly instance tuning перенесён во вкладку VISUAL TEST.");

        AddSectionTitle(
            "ФОКУС ВНУТРИ АНОМАЛИИ",
            anomalyController != null && anomalyController.IsAnomalyFocusActive
                ? $"Активен: {anomalyController.FocusedZoneName}"
                : "Снаружи зоны: эффект не активен"
        );
        if (anomalyController != null)
        {
            AddToggleRow(
                "PRODUCTION FOCUS",
                anomalyController.AnomalyFocusEnabled,
                true,
                () => anomalyController.SetAnomalyFocusEnabled(
                    !anomalyController.AnomalyFocusEnabled
                )
            );
            AddSectionTitle("ЗАТЕМНЕНИЕ СНАРУЖИ", "Внутри зоны остаётся прозрачное окно");
            AddProductionFloatOptions(
                new[] { 0f, 0.25f, 0.5f, 0.75f, 1f },
                anomalyController.OutsideDarkness,
                value => anomalyController.SetOutsideDarkness(value),
                value => $"{value:0.00}"
            );
            AddSectionTitle("ЦВЕТ СНАРУЖИ", "0 = полностью серый; 1 = исходные цвета");
            AddProductionFloatOptions(
                new[] { 0f, 0.25f, 0.5f, 0.75f, 1f },
                anomalyController.OutsideColor,
                value => anomalyController.SetOutsideColor(value),
                value => $"{value:0.00}"
            );
            AddSectionTitle("ПЛАВНОСТЬ ПЕРЕХОДА", "Вход / выход / collapse");
            AddProductionFloatOptions(
                new[] { 0.2f, 0.25f, 0.3f, 0.35f },
                anomalyController.FocusTransition,
                value => anomalyController.SetFocusTransition(value),
                value => $"{value:0.00} сек"
            );
        }
        else
        {
            AddHint("⚠ LevelAnomalyController не найден.");
        }

        AddSectionTitle(
            "ЧИТАЕМОСТЬ ВРАГОВ",
            "Яркость, насыщенность, холодный оттенок и контур"
        );
        AddProductionEnemyMode(
            ProductionSectorDebugController.EnemyReadability.Off
        );
        AddProductionEnemyMode(
            ProductionSectorDebugController.EnemyReadability.Low
        );
        AddProductionEnemyMode(
            ProductionSectorDebugController.EnemyReadability.Medium
        );
        AddProductionEnemyMode(
            ProductionSectorDebugController.EnemyReadability.High
        );
        AddHint(
            $"Режим: {GetEnemyReadabilityName(debug.EnemyMode)} | " +
            $"насыщенность {debug.EnemySaturation:0.00} | " +
            $"яркость {debug.EnemyBrightness:0.00} | " +
            $"оттенок {debug.EnemyTintStrength:0.00} | " +
            $"контур {(debug.EnemyOutlineEnabled ? debug.EnemyOutlineStrength.ToString("0.00") : "ВЫКЛ")} | " +
            $"толщина {debug.EnemyOutlineWidth:0.0} texel"
        );

        AddSectionTitle("НАСЫЩЕННОСТЬ", "0 = серый; 1 = исходный цвет; 2+ = усиленный цвет");
        AddProductionFloatOptions(
            new[] { 0f, 0.5f, 1f, 1.5f, 2f, 2.5f, 3f },
            debug.EnemySaturation,
            value => debug.SetEnemySaturation(value),
            value => $"{value:0.00}"
        );
        AddSectionTitle("ЯРКОСТЬ", "0.5 = темнее; 1 = исходная; 2.5 = экстремально ярко");
        AddProductionFloatOptions(
            new[] { 0.5f, 0.75f, 1f, 1.25f, 1.5f, 2f, 2.5f },
            debug.EnemyBrightness,
            value => debug.SetEnemyBrightness(value),
            value => $"{value:0.00}"
        );
        AddSectionTitle("ХОЛОДНЫЙ ОТТЕНОК", "0 = нет; 1 = максимально заметный cyan/teal");
        AddProductionFloatOptions(
            new[] { 0f, 0.25f, 0.5f, 0.75f, 1f },
            debug.EnemyTintStrength,
            value => debug.SetEnemyTintStrength(value),
            value => $"{value:0.00}"
        );
        AddSectionTitle("КОНТУР", "0 = нет; 2 = максимальная сила");
        AddHint(
            "⚠ Контур ограничен tight-геометрией production sprite. " +
            "Он оставлен для диагностики и не используется preset СИЛЬНО."
        );
        AddToggleRow(
            "КОНТУР",
            debug.EnemyOutlineEnabled,
            true,
            () => debug.SetEnemyOutlineEnabled(
                !debug.EnemyOutlineEnabled
            )
        );
        AddProductionFloatOptions(
            new[] { 0f, 0.5f, 1f, 1.5f, 2f },
            debug.EnemyOutlineStrength,
            value => debug.SetEnemyOutlineStrength(value),
            value => $"{value:0.00}"
        );
        AddSectionTitle("ТОЛЩИНА КОНТУРА", "Ширина выборки прозрачности: 0.5–4 texel");
        AddProductionFloatOptions(
            new[] { 0.5f, 1f, 2f, 3f, 4f },
            debug.EnemyOutlineWidth,
            value => debug.SetEnemyOutlineWidth(value),
            value => $"{value:0.0} texel"
        );

        AddSectionTitle(
            "ОБЛАСТЬ ЧИТАЕМОСТИ",
            "ВСЕ / текущая зона / тип врага; ЭЛИТА = Eyes и Turret"
        );
        AddProductionEnemyScope(
            ProductionSectorDebugController.EnemyScope.All
        );
        AddProductionEnemyScope(
            ProductionSectorDebugController.EnemyScope.CurrentZone
        );
        AddProductionEnemyScope(
            ProductionSectorDebugController.EnemyScope.Basic
        );
        AddProductionEnemyScope(
            ProductionSectorDebugController.EnemyScope.Elite
        );
        AddProductionEnemyScope(
            ProductionSectorDebugController.EnemyScope.Shooter
        );
        AddProductionEnemyScope(
            ProductionSectorDebugController.EnemyScope.Bomber
        );
        AddProductionEnemyScope(
            ProductionSectorDebugController.EnemyScope.Boss
        );
        AddHint(
            $"Зарегистрировано врагов: {debug.RegisteredEnemyCount}; " +
            $"renderer'ов: {debug.RegisteredEnemyRendererCount}. " +
            $"Изменено врагов: {debug.AffectedEnemyCount}; " +
            $"renderer'ов: {debug.AffectedEnemyRendererCount}."
        );
        AddHint(!debug.EnemyReadabilityMaterialReady
            ? "⚠ Материал EnemyReadability не загружен."
            : debug.EnemyMode == ProductionSectorDebugController.EnemyReadability.Off
                ? "Читаемость выключена: исходные материалы восстановлены."
                : $"✓ Применено к {debug.AffectedEnemyCount} врагам. " +
                  $"Активный EnemyReadability material: " +
                  $"{debug.ActiveReadabilityMaterialRendererCount}/" +
                  $"{debug.AffectedEnemyRendererCount} renderer'ов.");

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
            "ORIGINAL / 100 / 100 / ВРАГИ СИЛЬНО",
            mutedColor,
            "СБРОСИТЬ",
            true,
            () => debug.ResetVisualSettings()
        );
        AddHint(
            "Сброс визуала не меняет неуязвимость и выбор особой аномалии. " +
            "Текущий test default: враги СИЛЬНО, область — все."
        );

        AddSectionTitle(
            "ДИАГНОСТИКА",
            "Session-only инструменты; сохранение и meta-прогресс не меняются"
        );
        AddRow(
            "ТЕКУЩИЙ СЕКТОР",
            debug.CurrentSectorNumber > 0
                ? RunRoute.IsBossSector(debug.CurrentSectorNumber)
                    ? "BOSS / EXIT COMPLETE"
                    : $"{debug.CurrentSectorNumber}/{debug.ProductionSectorCount}"
                : "НЕТ АКТИВНОГО RUNSTATE",
            debug.CurrentSectorNumber > 0 ? successColor : warningColor,
            "ИНФО",
            false,
            null
        );
        AddRow(
            "THREAT TIER",
            ThreatTierPresentation.Format(debug.CurrentThreatTier),
            successColor,
            "ИНФО",
            false,
            null
        );
        AddRow(
            "INTERNAL PRESSURE",
            $"{debug.InternalPressure:0.0} / 100",
            mutedColor,
            "ИНФО",
            false,
            null
        );
        AddRow(
            "ПРОВЕРИТЬ THREAT I",
            "PRESSURE 0",
            mutedColor,
            "TIER I",
            true,
            () => SetDebugThreatTier(ThreatTier.Tier1)
        );
        AddRow(
            "ПРОВЕРИТЬ THREAT II",
            $"PRESSURE {ThreatTierPresentation.Tier2Minimum:0}",
            mutedColor,
            "TIER II",
            true,
            () => SetDebugThreatTier(ThreatTier.Tier2)
        );
        AddRow(
            "ПРОВЕРИТЬ THREAT III",
            $"PRESSURE {ThreatTierPresentation.Tier3Minimum:0}",
            mutedColor,
            "TIER III",
            true,
            () => SetDebugThreatTier(ThreatTier.Tier3)
        );
        AddRow(
            "ПРОВЕРИТЬ THREAT IV",
            $"PRESSURE {ThreatTierPresentation.Tier4Minimum:0}",
            mutedColor,
            "TIER IV",
            true,
            () => SetDebugThreatTier(ThreatTier.Tier4)
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
            $"renderer'ов окружения в секторе: {debug.EnvironmentRendererCount}"
        );
    }

    private void AddInteractiveAnomalyVisualTest()
    {
        EnsureProductionSectorDebug();
        ProductionSectorDebugController debug = productionSectorDebug;

        if (debug == null)
        {
            AddSectionTitle("ANOMALY VISUAL TEST", "Development / Editor only");
            AddHint("ProductionSectorDebugController не создан.");
            return;
        }

        bool choiceIsOpen =
            (levelChoiceManager != null && levelChoiceManager.IsChoosing) ||
            (upgradeManager != null && upgradeManager.IsChoosingUpgrade);
        bool liveAvailable = !choiceIsOpen && previousTimeScale > 0f;
        AddSectionTitle(
            "ANOMALY VISUAL TEST",
            "Mouse-first live tuning; правая часть viewport остаётся открытой"
        );
        AddRow(
            "SIMULATION",
            menuLiveSimulation ? "LIVE" : "PAUSED",
            menuLiveSimulation ? successColor : warningColor,
            menuLiveSimulation ? "PAUSE" : "GO LIVE",
            menuLiveSimulation || liveAvailable,
            () => SetMenuLiveSimulation(!menuLiveSimulation)
        );
        AddAnomalyTargetPanel(debug);

        AddSectionTitle("VISUAL TEST", "One-click strength and renderer controls");
        AddEnemyReadabilityPresetStrip(debug);
        AddFourStepRow(
            "Enemy Brightness", debug.EnemyBrightness, 0.05f, 0.25f,
            0.5f, 2.5f, debug.SetEnemyBrightness, "0.00");
        AddFourStepRow(
            "Saturation", debug.EnemySaturation, 0.05f, 0.25f,
            0f, 3f, debug.SetEnemySaturation, "0.00");
        AddFourStepRow(
            "Hue / Tint Strength", debug.EnemyTintStrength, 0.02f, 0.1f,
            0f, 1f, debug.SetEnemyTintStrength, "0.00");
        AddToggleRow(
            "OUTLINE", debug.EnemyOutlineEnabled, true,
            () => debug.SetEnemyOutlineEnabled(!debug.EnemyOutlineEnabled));
        AddFourStepRow(
            "Outline Strength", debug.EnemyOutlineStrength, 0.05f, 0.25f,
            0f, 2f, debug.SetEnemyOutlineStrength, "0.00");
        AddFourStepRow(
            "Outline Thickness", debug.EnemyOutlineWidth, 0.1f, 0.5f,
            0.5f, 4f, debug.SetEnemyOutlineWidth, "0.0");

        if (anomalyController != null)
        {
            AddFourStepRow(
                "Interior / Outside Darkness",
                anomalyController.OutsideDarkness, 0.05f, 0.2f,
                0f, 1f, anomalyController.SetOutsideDarkness, "0.00");
            AddFourStepRow(
                "Exterior Color / Intensity",
                anomalyController.OutsideColor, 0.05f, 0.2f,
                0f, 1f, anomalyController.SetOutsideColor, "0.00");
            AddFourStepRow(
                "Transition Duration",
                anomalyController.FocusTransition, 0.01f, 0.05f,
                0.2f, 0.35f, anomalyController.SetFocusTransition, "0.00");
        }
        else
        {
            AddHint("LevelAnomalyController не найден: focus controls недоступны.");
        }

        AddRow(
            "RESET VISUAL TEST",
            "Enemy readability + focus presentation baseline",
            mutedColor,
            "RESET TEST",
            true,
            () =>
            {
                debug.ResetVisualTestSettings();
                anomalyController?.ResetFocusPresentationForDebug();
                RefreshCurrentTab();
            }
        );

        AddAnomalyVisualTuner(debug);
    }

    private void SetMenuLiveSimulation(bool live)
    {
        bool choiceIsOpen =
            (levelChoiceManager != null && levelChoiceManager.IsChoosing) ||
            (upgradeManager != null && upgradeManager.IsChoosingUpgrade);
        menuLiveSimulation = live && !choiceIsOpen && previousTimeScale > 0f;
        Time.timeScale = menuLiveSimulation ? previousTimeScale : 0f;
        RefreshCurrentTab();
    }

    private void SetDebugThreatTier(ThreatTier tier)
    {
        productionSectorDebug?.SetThreatTier(tier);
        RefreshCurrentTab();
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

    private void AddAnomalyVisualTuner(
        ProductionSectorDebugController debug)
    {
        ProductionAnomalySite target = debug.VisualTunerTarget;
        AddSectionTitle(
            "ANOMALY",
            target != null
                ? "Capability-driven runtime presentation values"
                : "NO ACTIVE SUPPORTED ANOMALY"
        );

        AddRow(
            "MONOCHROME ANOMALIES",
            debug.MonochromeAnomaliesEnabled
                ? "ON | neutral palette; geometry and motion preserved"
                : "OFF | authored runtime colors",
            debug.MonochromeAnomaliesEnabled ? warningColor : mutedColor,
            debug.MonochromeAnomaliesEnabled ? "DISABLE" : "ENABLE",
            true,
            () =>
            {
                debug.SetMonochromeAnomalies(
                    !debug.MonochromeAnomaliesEnabled);
                RefreshCurrentTab();
            }
        );

        if (target == null)
        {
            return;
        }

        AddSectionTitle("ART", "Optional artist-authored presentation layers");
        AddRow(
            "ART LAYERS",
            $"{debug.VisualTunerArtHookRootCount} ROOTS | " +
            $"{debug.VisualTunerInstantiatedArtCount} ASSIGNED | " +
            (debug.VisualTunerArtHooksVisible ? "VISIBLE" : "HIDDEN"),
            debug.VisualTunerInstantiatedArtCount > 0
                ? successColor
                : mutedColor,
            debug.VisualTunerArtHooksVisible ? "HIDE" : "SHOW",
            debug.VisualTunerArtHookRootCount > 0,
            () =>
            {
                debug.SetVisualTunerArtHooksVisible(
                    !debug.VisualTunerArtHooksVisible);
                RefreshCurrentTab();
            }
        );

        AddSectionTitle("BOUNDARY", "Presentation only; collider is unchanged");
        AddVisualTunerFloat(
            debug, "Boundary Width",
            AnomalyVisualTuningCapabilities.BoundaryWidth,
            0.01f, 0.1f, 0.01f, 3f,
            values => values.BoundaryWidth,
            (values, value) =>
            {
                values.BoundaryWidth = value;
                return values;
            }
        );
        AddVisualTunerFloat(
            debug, "Inner Line Width",
            AnomalyVisualTuningCapabilities.InnerLineWidth,
            0.01f, 0.1f, 0.01f, 3f,
            values => values.InnerLineWidth,
            (values, value) =>
            {
                values.InnerLineWidth = value;
                return values;
            }
        );
        AddVisualTunerFloat(
            debug, "Visual Scale",
            AnomalyVisualTuningCapabilities.VisualScale,
            0.05f, 0.25f, 0.25f, 3f,
            values => values.VisualScale,
            (values, value) =>
            {
                values.VisualScale = value;
                return values;
            }
        );

        AddSectionTitle("COLORS", "Runtime instance RGBA; assets are untouched");
        AddVisualTunerColor(
            debug,
            "Primary",
            AnomalyVisualTuningCapabilities.PrimaryColor,
            values => values.PrimaryColor,
            (values, value) =>
            {
                values.PrimaryColor = value;
                return values;
            }
        );
        AddVisualTunerColor(
            debug,
            "Secondary",
            AnomalyVisualTuningCapabilities.SecondaryColor,
            values => values.SecondaryColor,
            (values, value) =>
            {
                values.SecondaryColor = value;
                return values;
            }
        );
        AddVisualTunerColor(
            debug,
            "Fill",
            AnomalyVisualTuningCapabilities.FillColor,
            values => values.FillColor,
            (values, value) =>
            {
                values.FillColor = value;
                return values;
            },
            false
        );
        AddVisualTunerFloat(
            debug, "Fill Alpha",
            AnomalyVisualTuningCapabilities.FillAlpha,
            0.05f, 0.2f, 0f, 1f,
            values => values.FillAlpha,
            (values, value) =>
            {
                values.FillAlpha = value;
                return values;
            }
        );

        AddSectionTitle("PATTERN / FX", "Only supported renderer properties");
        AddVisualTunerFloat(
            debug, "Edge Glow",
            AnomalyVisualTuningCapabilities.EdgeGlow,
            0.1f, 0.5f, 0.01f, 10f,
            values => values.EdgeGlow,
            (values, value) =>
            {
                values.EdgeGlow = value;
                return values;
            }
        );
        AddVisualTunerFloat(
            debug, "Pulse Speed",
            AnomalyVisualTuningCapabilities.PulseSpeed,
            0.05f, 0.25f, 0f, 10f,
            values => values.PulseSpeed,
            (values, value) =>
            {
                values.PulseSpeed = value;
                return values;
            }
        );
        AddVisualTunerFloat(
            debug, "Pulse Strength",
            AnomalyVisualTuningCapabilities.PulseStrength,
            0.05f, 0.2f, 0f, 1f,
            values => values.PulseStrength,
            (values, value) =>
            {
                values.PulseStrength = value;
                return values;
            }
        );
        AddVisualTunerFloat(
            debug, "Pattern Speed",
            AnomalyVisualTuningCapabilities.PatternSpeed,
            0.05f, 0.25f, 0f, 10f,
            values => values.PatternSpeed,
            (values, value) =>
            {
                values.PatternSpeed = value;
                return values;
            }
        );

        AddSectionTitle("DEBUG PRESETS", "Session-only starting points");
        AddVisualTunerPreset(debug, "CLEAN");
        AddVisualTunerPreset(debug, "AGGRESSIVE");
        AddVisualTunerPreset(debug, "MINIMAL");
        AddRow(
            "RESET VISUAL",
            "Restore values captured when this instance was initialized",
            successColor,
            "RESET",
            true,
            () =>
            {
                debug.ResetVisualTuner();
                RefreshCurrentTab();
            }
        );
        AddRow(
            "COPY VALUES",
            "Console + system clipboard",
            successColor,
            "COPY",
            true,
            () =>
            {
                debug.CopyVisualTunerValues();
                RefreshCurrentTab();
            }
        );
    }

    private void AddAnomalyTargetPanel(
        ProductionSectorDebugController debug)
    {
        ProductionAnomalySite target = debug.VisualTunerTarget;
        string distance = debug.VisualTunerDistance >= 0f
            ? $"{debug.VisualTunerDistance:0.0}"
            : "--";
        AddSectionTitle(
            "TARGET",
            target != null
                ? $"Type: {debug.VisualTunerTypeName} | Distance: {distance}"
                : "NO ACTIVE SUPPORTED ANOMALY"
        );

        if (target == null)
        {
            AddRow(
                "NO ACTIVE SUPPORTED ANOMALY",
                "Uses ProductionAnomalySite.ActiveSites",
                warningColor,
                "REFRESH",
                true,
                RefreshSectorVisualTargets
            );
            return;
        }

        AddTargetSelectorRow(debug);
    }

    private void SelectVisualTunerTarget(
        ProductionSectorDebugController debug,
        bool next)
    {
        if (next)
            debug.SelectNextVisualTunerTarget();
        else
            debug.SelectPreviousVisualTunerTarget();

        RefreshCurrentTab();
    }

    private void AddTargetSelectorRow(
        ProductionSectorDebugController debug)
    {
        RectTransform row = CreateRect("Target Selector", contentRoot);
        row.gameObject.AddComponent<Image>().color = rowColor;
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 54f;

        bool canCycle = debug.VisualTunerTargetCount > 1;
        Button previous = CreateButton(
            row, "PREV", () => SelectVisualTunerTarget(debug, false),
            104f, canCycle);
        RectTransform previousRect = previous.GetComponent<RectTransform>();
        previousRect.anchorMin = previousRect.anchorMax =
            new Vector2(0f, 0.5f);
        previousRect.pivot = new Vector2(0f, 0.5f);
        previousRect.anchoredPosition = new Vector2(12f, 0f);
        previousRect.sizeDelta = new Vector2(104f, 38f);

        TextMeshProUGUI target = CreateText(
            "Target", row,
            $"{debug.VisualTunerTargetName}  " +
            $"({debug.VisualTunerTargetIndex + 1}/" +
            $"{debug.VisualTunerTargetCount})",
            17f, TextAlignmentOptions.Center, successColor);
        Stretch(target.rectTransform, 126f, 126f);

        Button next = CreateButton(
            row, "NEXT", () => SelectVisualTunerTarget(debug, true),
            104f, canCycle);
        RectTransform nextRect = next.GetComponent<RectTransform>();
        nextRect.anchorMin = nextRect.anchorMax = new Vector2(1f, 0.5f);
        nextRect.pivot = new Vector2(1f, 0.5f);
        nextRect.anchoredPosition = new Vector2(-12f, 0f);
        nextRect.sizeDelta = new Vector2(104f, 38f);
    }

    private void AddVisualTunerPreset(
        ProductionSectorDebugController debug,
        string preset)
    {
        AddRow(
            preset,
            "Runtime visual only",
            mutedColor,
            "APPLY",
            true,
            () =>
            {
                debug.ApplyVisualTunerPreset(preset);
                RefreshCurrentTab();
            }
        );
    }

    private void AddVisualTunerFloat(
        ProductionSectorDebugController debug,
        string label,
        AnomalyVisualTuningCapabilities capability,
        float smallStep,
        float largeStep,
        float minimum,
        float maximum,
        System.Func<AnomalyVisualTuningValues, float> getter,
        System.Func<
            AnomalyVisualTuningValues,
            float,
            AnomalyVisualTuningValues> setter)
    {
        if ((debug.VisualTunerCapabilities & capability) == 0)
            return;

        float current = getter(debug.VisualTunerValues);
        AddFourStepRow(
            label,
            current,
            smallStep,
            largeStep,
            minimum,
            maximum,
            value =>
            {
                AnomalyVisualTuningValues values = debug.VisualTunerValues;
                debug.ApplyVisualTunerValues(setter(values, value));
            },
            "0.###"
        );
    }

    private void AddVisualTunerColor(
        ProductionSectorDebugController debug,
        string label,
        AnomalyVisualTuningCapabilities capability,
        System.Func<AnomalyVisualTuningValues, Color> getter,
        System.Func<
            AnomalyVisualTuningValues,
            Color,
            AnomalyVisualTuningValues> setter,
        bool includeAlpha = true)
    {
        if ((debug.VisualTunerCapabilities & capability) == 0)
            return;

        AddVisualTunerColorChannel(debug, label + " R", getter, setter, 0);
        AddVisualTunerColorChannel(debug, label + " G", getter, setter, 1);
        AddVisualTunerColorChannel(debug, label + " B", getter, setter, 2);

        if (includeAlpha)
            AddVisualTunerColorChannel(debug, label + " A", getter, setter, 3);
    }

    private void AddVisualTunerColorChannel(
        ProductionSectorDebugController debug,
        string label,
        System.Func<AnomalyVisualTuningValues, Color> getter,
        System.Func<
            AnomalyVisualTuningValues,
            Color,
            AnomalyVisualTuningValues> setter,
        int channel)
    {
        Color color = getter(debug.VisualTunerValues);
        float current = GetColorChannel(color, channel);
        AddFourStepRow(
            label,
            current,
            0.02f,
            0.1f,
            0f,
            1f,
            value => SetVisualTunerColorChannel(
                debug, getter, setter, channel, value),
            "0.00"
        );
    }

    private void SetVisualTunerColorChannel(
        ProductionSectorDebugController debug,
        System.Func<AnomalyVisualTuningValues, Color> getter,
        System.Func<
            AnomalyVisualTuningValues,
            Color,
            AnomalyVisualTuningValues> setter,
        int channel,
        float channelValue)
    {
        AnomalyVisualTuningValues values = debug.VisualTunerValues;
        Color color = getter(values);
        SetColorChannel(
            ref color,
            channel,
            Mathf.Clamp01(channelValue)
        );
        debug.ApplyVisualTunerValues(setter(values, color));
        RefreshCurrentTab();
    }

    private static float GetColorChannel(Color color, int channel)
    {
        return channel switch
        {
            0 => color.r,
            1 => color.g,
            2 => color.b,
            _ => color.a
        };
    }

    private static void SetColorChannel(
        ref Color color,
        int channel,
        float value)
    {
        switch (channel)
        {
            case 0:
                color.r = value;
                break;
            case 1:
                color.g = value;
                break;
            case 2:
                color.b = value;
                break;
            default:
                color.a = value;
                break;
        }
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

    private void RefreshSectorVisualTargets()
    {
        productionSectorDebug?.RefreshVisualTargets();
        RefreshCurrentTab();
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

    private void AddProductionEnemyScope(
        ProductionSectorDebugController.EnemyScope value)
    {
        ProductionSectorDebugController debug = productionSectorDebug;
        AddOptionRow(
            $"ОБЛАСТЬ: {GetEnemyScopeName(value)}",
            debug != null && debug.CurrentEnemyScope == value,
            debug != null,
            () => debug.SetEnemyScope(value)
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
            ProductionSectorDebugController.EnemyReadability.Low => "СЛАБО",
            ProductionSectorDebugController.EnemyReadability.Medium =>
                "СРЕДНЕ",
            ProductionSectorDebugController.EnemyReadability.High => "СИЛЬНО",
            _ => "ВЫКЛ"
        };

    private static string GetEnemyScopeName(
        ProductionSectorDebugController.EnemyScope value) =>
        value switch
        {
            ProductionSectorDebugController.EnemyScope.All => "ВСЕ",
            ProductionSectorDebugController.EnemyScope.Basic => "ОБЫЧНЫЕ",
            ProductionSectorDebugController.EnemyScope.Elite => "ЭЛИТА",
            ProductionSectorDebugController.EnemyScope.Shooter => "СТРЕЛКИ",
            ProductionSectorDebugController.EnemyScope.Bomber => "БОМБЕРЫ",
            ProductionSectorDebugController.EnemyScope.Boss => "БОСС",
            _ => "ТЕКУЩАЯ ЗОНА"
        };

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

        AddSectionTitle(
            "LEVEL FLOW",
            "Production Sector 1 -> 2 -> 3 -> Exit/Boss lifecycle"
        );
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
        AddRow("Advance through production Exit/Boss flow",
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
        AddRoomStateRows();

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
        CharacterData selectedCharacter =
            RunSelectionManager.Instance != null
                ? RunSelectionManager.Instance.SelectedCharacter
                : null;
        AddRow("Selected Character",
            selectedCharacter != null
                ? selectedCharacter.characterName
                : "NOT SELECTED",
            selectedCharacter != null ? successColor : mutedColor,
            null, false, null);
        AddRow("Combat Type",
            selectedCharacter != null
                ? selectedCharacter.combatType.ToString()
                : "-",
            selectedCharacter != null ? successColor : mutedColor,
            null, false, null);
        CharacterSelectionUI characterUi = FindFirstObjectByType<CharacterSelectionUI>();
        bool uiAvailable = characterUi != null;
        AddRow("Character Selection", uiAvailable ? "OPEN" : "NOT OPEN",
            uiAvailable ? successColor : warningColor,
            "REFRESH", uiAvailable, () => DebugRefreshCharacterUi(characterUi));

        debugCharacters.Clear();
        characterUi?.CollectDebugCharacters(debugCharacters);

        if (debugCharacters.Count == 0)
        {
            AddRow("Debug character source",
                characterUi == null ? "NOT AVAILABLE" : "NO CHARACTER DATA",
                warningColor, null, false, null);
            return;
        }

        for (int i = 0; i < debugCharacters.Count; i++)
            AddCharacterDebugSelection(characterUi, debugCharacters[i]);
    }

    private void AddCharacterDebugSelection(
        CharacterSelectionUI characterUi,
        CharacterData character)
    {
        string characterName = string.IsNullOrWhiteSpace(
            character.characterName)
            ? "UNNAMED CHARACTER"
            : character.characterName;
        bool canSelect = characterUi != null &&
            characterUi.CanDebugSelectCharacter(character);
        string availability = canSelect ? "AVAILABLE" : "LOCKED";
        AddRow($"Select {characterName}",
            $"{availability} / {character.combatType}",
            canSelect ? mutedColor : warningColor,
            $"SELECT {characterName.ToUpperInvariant()}", canSelect,
            () => DebugSelectCharacter(characterUi, character));
    }

    private void DebugRefreshCharacterUi(CharacterSelectionUI characterUi)
    {
        characterUi?.DebugRefresh();
        RefreshCurrentTab();
    }

    private void DebugSelectCharacter(
        CharacterSelectionUI characterUi,
        CharacterData character)
    {
        characterUi?.DebugSelectCharacter(character);
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
        AddRow(
            "ВСЕ АКТИВНЫЕ ВРАГИ",
            $"Найдено: {EnemyHealth.ActiveInstances.Count}",
            EnemyHealth.ActiveInstances.Count > 0 ? warningColor : mutedColor,
            "УБИТЬ ВСЕХ",
            EnemyHealth.ActiveInstances.Count > 0,
            KillAllEnemies
        );
        AddHint(enemyDebugStatus);

        AddSectionTitle("РУЧНОЙ SPAWN", "Безопасные разные позиции вокруг игрока");
        AddManualEnemyRows("ОБЫЧНЫЙ", ResolveEnemyPrefab(EnemySpawner.DebugEnemyArchetype.Basic));
        AddManualEnemyRows("СТРЕЛОК", ResolveEnemyPrefab(EnemySpawner.DebugEnemyArchetype.Shooter));
        AddManualEnemyRows("БОМБЕР", ResolveEnemyPrefab(EnemySpawner.DebugEnemyArchetype.Bomber));
        AddManualEnemyRows("EYES", eyesEnemyPrefab != null ? eyesEnemyPrefab : ResolveEnemyPrefab(EnemySpawner.DebugEnemyArchetype.Eyes));
        AddManualEnemyRows("ТУРЕЛЬ", turretEnemyPrefab != null ? turretEnemyPrefab : ResolveEnemyPrefab(EnemySpawner.DebugEnemyArchetype.Turret));
        AddManualEnemyRows("БОСС", runTimer != null ? runTimer.DebugBossPrefab : null);

        AddRow("Создано вручную",
            debugEnemies.Count > 0 ? $"Активно: {debugEnemies.Count}" : "Нет",
            debugEnemies.Count > 0 ? successColor : mutedColor,
            "УБРАТЬ РУЧНЫХ", debugEnemies.Count > 0,
            ClearDebugEnemies);
    }

    private void AddWorldEventsSection()
    {
        AddLootChestSection();
        AddSectionTitle("WORLD EVENTS", "Spawn/Clear through WorldEventSpawner");
        WorldEvent current = worldEventSpawner != null
            ? worldEventSpawner.CurrentEvent
            : null;
        AddRow($"Active event: {GetEventDisplayName(current)}",
            current != null ? "ACTIVE" : "None",
            current != null ? successColor : mutedColor,
            "CLEAR EVENT", current != null, ClearWorldEvent);

        addedEventPrefabs.Clear();

        if (worldEventPrefabs != null)
        {
            for (int i = 0; i < worldEventPrefabs.Length; i++)
            {
                WorldEvent prefab = worldEventPrefabs[i];

                if (prefab != null && !addedEventPrefabs.Contains(prefab))
                    AddEventRow(GetEventDisplayName(prefab), prefab);
            }
        }

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

    private void AddRoomStateRows()
    {
        AddSectionTitle("BUNKER ROOMS", "Independent production room states");
        BunkerRoomState[] rooms =
            FindObjectsByType<BunkerRoomState>(FindObjectsSortMode.None);

        foreach (BunkerRoomId roomId in Enum.GetValues(typeof(BunkerRoomId)))
        {
            BunkerRoomState room = null;
            for (int i = 0; i < rooms.Length; i++)
            {
                if (rooms[i] != null && rooms[i].RoomId == roomId)
                {
                    room = rooms[i];
                    break;
                }
            }

            string state = room == null
                ? "MISSING"
                : room.IsOpen ? "OPEN" : "CLOSED";
            Color color = room == null
                ? warningColor
                : room.IsOpen ? successColor : mutedColor;
            AddRow(roomId.ToString(), state, color, null, false, null);
        }
    }

    private void AddLootChestSection()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        WorldLootChest prefab = Resources.Load<WorldLootChest>(
            "WorldLoot/WorldLootChestV1"
        );
        bool available = player != null && prefab != null;

        AddSectionTitle(
            "СУНДУКИ",
            "Production World Loot Chest V1.2; non-blocking Reel"
        );
        AddRow(
            "WORLD LOOT CHEST",
            !available
                ? player == null ? "ИГРОК НЕ НАЙДЕН" : "PREFAB НЕ НАЙДЕН"
                : "ГОТОВ",
            available ? successColor : warningColor,
            "СОЗДАТЬ СУНДУК РЯДОМ",
            available,
            SpawnWorldLootChestNearPlayer
        );
        AddHint(
            "Reward Pool: 50 GOLD x6 · 100 GOLD x3 · 300 GOLD x1\n" +
            "REEL PRESENTATION: " +
            $"{WorldLootRewardReel.PresentationPanelSize.x:0}x" +
            $"{WorldLootRewardReel.PresentationPanelSize.y:0} · " +
            $"transfer {WorldLootRewardReel.PresentationTransferDuration:0.00}s\n" +
            "Active Reel: " +
            (WorldLootRewardReel.IsActive ? "YES" : "NO") + "\n" +
            "State: " + WorldLootRewardReel.ActiveStateLabel + "\n" +
            lootChestDebugStatus + "\n" +
            "Последняя награда: " +
            (string.IsNullOrWhiteSpace(WorldLootRewardReel.LastClaimedReward)
                ? "нет"
                : WorldLootRewardReel.LastClaimedReward)
        );
    }

    private void SpawnWorldLootChestNearPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
        {
            lootChestDebugStatus = "⚠ Игрок не найден.";
            RefreshCurrentTab();
            return;
        }

        Vector2 position = (Vector2)player.transform.position +
            Vector2.right * 2.5f;
        WorldLootChest chest = WorldLootChestSpawner.SpawnChest(position);
        lootChestDebugStatus = chest != null
            ? "✓ Сундук создан в 2.5 м справа от игрока."
            : "⚠ Не удалось создать production prefab.";
        RefreshCurrentTab();
    }

    private void AddCombatLabSection()
    {
        ResolveCombatLab();

        bool available = combatLab != null && combatLab.RefreshBinding();
        CombatLabControlStyle style = combatLab != null
            ? combatLab.ControlStyle
            : CombatLabControlStyle.Orbit;
        WeaponControlMode fireMode = combatLab != null
            ? combatLab.FireMode
            : WeaponControlSettings.CurrentMode;
        WeaponData selectedWeapon = combatLab != null
            ? combatLab.SelectedWeapon
            : null;
        WeaponData pistol = combatLab != null ? combatLab.Pistol : null;
        WeaponData laser = combatLab != null ? combatLab.Laser : null;

        AddSectionTitle(
            "COMBAT LAB",
            combatLab != null ? $"CURRENT: {combatLab.CurrentSummary}" :
                "CURRENT: PLAYER/WEAPON NOT FOUND"
        );

        AddRow(
            "CONTROL / ORBIT",
            style == CombatLabControlStyle.Orbit ? "ACTIVE" : "AVAILABLE",
            style == CombatLabControlStyle.Orbit ? successColor : mutedColor,
            "ORBIT",
            available,
            () => SelectCombatLabControl(CombatLabControlStyle.Orbit)
        );
        AddRow(
            "CONTROL / REMOTE",
            style == CombatLabControlStyle.Remote ? "ACTIVE" : "AVAILABLE",
            style == CombatLabControlStyle.Remote ? successColor : mutedColor,
            "REMOTE",
            available,
            () => SelectCombatLabControl(CombatLabControlStyle.Remote)
        );

        AddRow(
            "WEAPON / PISTOL",
            selectedWeapon == pistol ? "ACTIVE" :
                pistol != null ? "AVAILABLE" : "WEAPON DATA NOT FOUND",
            selectedWeapon == pistol ? successColor :
                pistol != null ? mutedColor : warningColor,
            "PISTOL",
            available && pistol != null && pistol.weaponPrefab != null,
            () => SelectCombatLabWeapon(pistol)
        );
        AddRow(
            "WEAPON / LASER",
            selectedWeapon == laser ? "ACTIVE" :
                laser != null ? "AVAILABLE" : "WEAPON DATA NOT FOUND",
            selectedWeapon == laser ? successColor :
                laser != null ? mutedColor : warningColor,
            "LASER",
            available && laser != null && laser.weaponPrefab != null,
            () => SelectCombatLabWeapon(laser)
        );

        AddRow(
            "FIRE / AUTO",
            fireMode == WeaponControlMode.AutoAim ? "ACTIVE" : "AVAILABLE",
            fireMode == WeaponControlMode.AutoAim ? successColor : mutedColor,
            "AUTO",
            combatLab != null,
            () => SelectCombatLabFire(WeaponControlMode.AutoAim)
        );
        AddRow(
            "FIRE / MANUAL",
            fireMode == WeaponControlMode.Manual ? "ACTIVE" : "AVAILABLE",
            fireMode == WeaponControlMode.Manual ? successColor : mutedColor,
            "MANUAL",
            combatLab != null,
            () => SelectCombatLabFire(WeaponControlMode.Manual)
        );
        AddHint(
            "REMOTE: RMB sets the primary weapon position inside radius 8. " +
            "AUTO targets from the weapon. MANUAL aims at mouse and fires on LMB."
        );
    }

    private void ResolveCombatLab()
    {
        combatLab ??= GetComponent<CombatLabDebugController>();
        combatLab ??= gameObject.AddComponent<CombatLabDebugController>();
        combatLab.Configure(
            characterSpawner,
            FindCombatLabWeapon("pistol"),
            FindCombatLabWeapon("laser")
        );
    }

    private WeaponData FindCombatLabWeapon(string namePart)
    {
        if (debugWeapons == null)
            return null;

        for (int i = 0; i < debugWeapons.Length; i++)
        {
            WeaponData data = debugWeapons[i];
            if (data == null)
                continue;

            string displayName = string.IsNullOrWhiteSpace(data.weaponName)
                ? data.name
                : data.weaponName;

            if (displayName.IndexOf(
                    namePart,
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return data;
            }
        }

        return null;
    }

    private void SelectCombatLabControl(CombatLabControlStyle style)
    {
        ResolveCombatLab();
        combatLab?.SelectControlStyle(style);
        RefreshTab(DebugTab.WeaponsAndUpgrades);
        RefreshTab(DebugTab.Telekinesis);
    }

    private void SelectCombatLabWeapon(WeaponData weaponData)
    {
        ResolveCombatLab();
        combatLab?.SelectWeapon(weaponData);
        telekinesisPrototype = GameObject.FindGameObjectWithTag("Player")
            ?.GetComponent<TelekinesisDebugPrototype>();
        RefreshTab(DebugTab.WeaponsAndUpgrades);
        RefreshTab(DebugTab.Telekinesis);
    }

    private void SelectCombatLabFire(WeaponControlMode mode)
    {
        ResolveCombatLab();
        combatLab?.SelectFireMode(mode);
        RefreshTab(DebugTab.WeaponsAndUpgrades);
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

        RunStateManager runState = RunStateManager.Instance;
        int playerLevel = ExperienceManager.Instance != null
            ? ExperienceManager.Instance.CurrentLevel
            : 1;
        int usedSlots = runState != null
            ? runState.ItemSlots.UsedSlotCount
            : 0;
        int eligibleProduction = upgradeManager != null
            ? upgradeManager.GetEligibleProductionUpgradeCount(playerLevel)
            : 0;
        int stationLevel = BunkerStationProgressionService.GetStoredLevel(
            BunkerStationId.Upgrades);
        int stationAvailable = upgradeManager != null
            ? upgradeManager.GetStationAvailableProductionUpgradeCount()
            : 0;
        int productionPoolSize = upgradeManager?.AllUpgrades?.Count ?? 0;
        WeaponUpgradeCapability weaponCapabilities =
            WeaponUpgradeCapabilityResolver.GetCurrentCapabilities();
        AddHint(
            $"Upgrade Station: Lv{stationLevel} | " +
            $"Production Pool Available: {stationAvailable}/{productionPoolSize}"
        );
        AddHint(
            $"Slots: {usedSlots} / {RunItemSlots.SlotCount} | " +
            $"Eligible Current Choices: {eligibleProduction}"
        );
        AddHint($"Current Weapon Capabilities: {weaponCapabilities}");

        if (visibleUpgrades.Count == 0)
        {
            AddHint("No UpgradeData assets are available for this filter.");
            return;
        }

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
            bool hasEligibleLevel = playerLevel >= data.minPlayerLevel;
            bool hasEligibleSlot = runState == null ||
                runState.ItemSlots.CanAccept(data);
            bool stationLocked = !isUnlocked &&
                data.unlockData != null &&
                data.unlockData.condition != null &&
                data.unlockData.condition.type == UnlockConditionType.StationLevelRequirement;
            bool weaponCompatible = UpgradeEligibilityRules.IsWeaponCompatible(
                data,
                weaponCapabilities);
            bool exclusiveConflict = UpgradeEligibilityRules.HasExclusiveConflict(
                data,
                runState != null ? runState.ItemSlots : null);

            if (!inPool)
                status += " - OUT OF CURRENT POOL";
            else if (stationLocked)
                status += $" - REQUIRES {data.unlockData.condition.stationId.ToString().ToUpperInvariant()} " +
                    $"STATION LV{Mathf.Max(1, data.unlockData.condition.requiredAmount)}";
            else if (!isUnlocked)
                status += " - LOCKED";
            else if (!hasEligibleLevel)
                status += $" - REQUIRES PLAYER LV{data.minPlayerLevel}";
            else if (!weaponCompatible)
                status += " - INCOMPATIBLE WITH CURRENT WEAPON";
            else if (exclusiveConflict)
                status += $" - BLOCKED BY EXCLUSIVE GROUP {data.exclusiveGroup}";
            else if (stack >= RunItemSlots.MaxItemLevel)
                status += " - MAXED";
            else if (!hasEligibleSlot)
                status += " - NOT OWNED / NO FREE SLOT";
            else
                status += " - ELIGIBLE";

            bool canApply = upgradeManager != null &&
                GameObject.FindGameObjectWithTag("Player") != null &&
                isUnlocked &&
                hasEligibleLevel &&
                weaponCompatible &&
                !exclusiveConflict &&
                hasEligibleSlot;
            UpgradeData captured = data;
            AddRow(displayName, status,
                stack > 0 ? successColor :
                    inPool && isUnlocked && hasEligibleLevel &&
                    weaponCompatible && !exclusiveConflict && hasEligibleSlot
                        ? mutedColor
                        : warningColor,
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

        if (!characterSpawner.TryReplaceDebugPrimaryWeapon(
            player,
            data,
            out _
        ))
        {
            return;
        }

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

    private void SpawnDebugEnemies(GameObject prefab, string displayName, int count)
    {
        Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (enemySpawner == null || prefab == null || player == null)
        {
            enemyDebugStatus = "⚠ Spawn невозможен: отсутствует player, prefab или EnemySpawner.";
            return;
        }

        int spawned = 0;
        for (int i = 0; i < Mathf.Max(1, count); i++)
        {
            GameObject enemy = enemySpawner.SpawnSpecificEnemyAround(
                prefab,
                player.position,
                3f,
                8f,
                3f,
                false,
                0.45f
            );

            if (enemy == null)
                continue;

            debugEnemies.Add(enemy);
            spawned++;
        }

        enemyDebugStatus = spawned == count
            ? $"✓ {displayName}: создано {spawned}."
            : $"⚠ {displayName}: создано {spawned} из {count}; не хватило безопасных позиций.";

        RefreshCurrentTab();
    }

    private void ToggleEnemyAutoSpawn()
    {
        if (enemySpawner == null)
            return;

        if (enemySpawner.IsSpawningEnabled)
        {
            enemySpawner.StopSpawning();
            enemyDebugStatus = "✓ Автоспавн остановлен; ручной spawn доступен.";
        }
        else
        {
            enemySpawner.ResumeSpawning();
            enemyDebugStatus = "✓ Production автоспавн возобновлён.";
        }

        RefreshCurrentTab();
    }

    private void ToggleEnemyAiFreeze()
    {
        bool frozen = !EnemyDebugAiFreeze.IsFrozen;

        if (productionSectorDebug != null)
            productionSectorDebug.SetEnemyAiFrozen(frozen);
        else
            EnemyDebugAiFreeze.SetFrozen(frozen);

        enemyDebugStatus = frozen
            ? $"✓ AI остановлен у: {EnemyHealth.ActiveInstances.Count} врагов. Новые враги наследуют режим."
            : "✓ Собственное движение и атаки врагов восстановлены.";
        RefreshCurrentTab();
    }

    private void KillAllEnemies()
    {
        List<EnemyHealth> enemies = new(EnemyHealth.ActiveInstances);
        int killed = 0;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyHealth enemy = enemies[i];
            if (enemy == null)
                continue;

            Destroy(enemy.gameObject);
            killed++;
        }

        enemyDebugStatus = $"✓ Убрано врагов: {killed}; награды и meta-прогресс не начислялись.";
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

    private static string GetTelekinesisModeName(TelekinesisDebugMode mode) =>
        mode switch
        {
            TelekinesisDebugMode.Remote => "REMOTE",
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

    private void AddStepperRow(
        string label,
        string value,
        bool canDecrease,
        bool canIncrease,
        UnityEngine.Events.UnityAction decrease,
        UnityEngine.Events.UnityAction increase)
    {
        RectTransform row = CreateRect(label, contentRoot);
        row.gameObject.AddComponent<Image>().color = rowColor;
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 54f;

        TextMeshProUGUI labelText = CreateText(
            "Name", row, label, 19f,
            TextAlignmentOptions.MidlineLeft, Color.white
        );
        labelText.rectTransform.anchorMin = Vector2.zero;
        labelText.rectTransform.anchorMax = new Vector2(0.48f, 1f);
        labelText.rectTransform.offsetMin = new Vector2(16f, 0f);
        labelText.rectTransform.offsetMax = Vector2.zero;

        TextMeshProUGUI valueText = CreateText(
            "Value", row, value, 17f,
            TextAlignmentOptions.MidlineRight, successColor
        );
        valueText.rectTransform.anchorMin = new Vector2(0.45f, 0f);
        valueText.rectTransform.anchorMax = Vector2.one;
        valueText.rectTransform.offsetMin = Vector2.zero;
        valueText.rectTransform.offsetMax = new Vector2(-158f, 0f);

        Button minus = CreateButton(
            row,
            "−",
            decrease,
            64f,
            canDecrease
        );
        RectTransform minusRect = minus.GetComponent<RectTransform>();
        minusRect.anchorMin = minusRect.anchorMax = new Vector2(1f, 0.5f);
        minusRect.pivot = new Vector2(1f, 0.5f);
        minusRect.anchoredPosition = new Vector2(-82f, 0f);
        minusRect.sizeDelta = new Vector2(64f, 38f);

        Button plus = CreateButton(
            row,
            "+",
            increase,
            64f,
            canIncrease
        );
        RectTransform plusRect = plus.GetComponent<RectTransform>();
        plusRect.anchorMin = plusRect.anchorMax = new Vector2(1f, 0.5f);
        plusRect.pivot = new Vector2(1f, 0.5f);
        plusRect.anchoredPosition = new Vector2(-12f, 0f);
        plusRect.sizeDelta = new Vector2(64f, 38f);
    }

    private void AddFourStepRow(
        string label,
        float value,
        float smallStep,
        float largeStep,
        float minimum,
        float maximum,
        System.Action<float> setter,
        string format)
    {
        RectTransform row = CreateRect(label, contentRoot);
        row.gameObject.AddComponent<Image>().color = rowColor;
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 50f;

        TextMeshProUGUI labelText = CreateText(
            "Name", row, label, 17f,
            TextAlignmentOptions.MidlineLeft, Color.white);
        labelText.rectTransform.anchorMin = Vector2.zero;
        labelText.rectTransform.anchorMax = new Vector2(0.68f, 1f);
        labelText.rectTransform.offsetMin = new Vector2(14f, 0f);
        labelText.rectTransform.offsetMax = Vector2.zero;

        TextMeshProUGUI valueText = CreateText(
            "Value", row, value.ToString(format), 16f,
            TextAlignmentOptions.Center, successColor);
        RectTransform valueRect = valueText.rectTransform;
        valueRect.anchorMin = valueRect.anchorMax = new Vector2(1f, 0.5f);
        valueRect.pivot = new Vector2(1f, 0.5f);
        valueRect.anchoredPosition = new Vector2(-132f, 0f);
        valueRect.sizeDelta = new Vector2(82f, 38f);

        AddFourStepButton(row, "--", -280f, value > minimum,
            () => ApplyFourStep(setter, value - largeStep, minimum, maximum));
        AddFourStepButton(row, "-", -220f, value > minimum,
            () => ApplyFourStep(setter, value - smallStep, minimum, maximum));
        AddFourStepButton(row, "+", -72f, value < maximum,
            () => ApplyFourStep(setter, value + smallStep, minimum, maximum));
        AddFourStepButton(row, "++", -12f, value < maximum,
            () => ApplyFourStep(setter, value + largeStep, minimum, maximum));
    }

    private void AddFourStepButton(
        Transform parent,
        string label,
        float rightOffset,
        bool interactable,
        UnityEngine.Events.UnityAction action)
    {
        Button button = CreateButton(parent, label, action, 54f, interactable);
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(rightOffset, 0f);
        rect.sizeDelta = new Vector2(54f, 36f);
    }

    private void ApplyFourStep(
        System.Action<float> setter,
        float value,
        float minimum,
        float maximum)
    {
        setter?.Invoke(Mathf.Clamp(value, minimum, maximum));
        RefreshCurrentTab();
    }

    private void AddEnemyReadabilityPresetStrip(
        ProductionSectorDebugController debug)
    {
        RectTransform row = CreateRect("Visual Test Strength", contentRoot);
        row.gameObject.AddComponent<Image>().color = rowColor;
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 52f;

        TextMeshProUGUI label = CreateText(
            "Name", row, "STRENGTH", 17f,
            TextAlignmentOptions.MidlineLeft, Color.white);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = new Vector2(0.36f, 1f);
        label.rectTransform.offsetMin = new Vector2(14f, 0f);
        label.rectTransform.offsetMax = Vector2.zero;

        ProductionSectorDebugController.EnemyReadability[] values =
        {
            ProductionSectorDebugController.EnemyReadability.Off,
            ProductionSectorDebugController.EnemyReadability.Low,
            ProductionSectorDebugController.EnemyReadability.Medium,
            ProductionSectorDebugController.EnemyReadability.High
        };
        string[] labels = { "OFF", "WEAK", "MEDIUM", "STRONG" };

        for (int i = 0; i < values.Length; i++)
        {
            ProductionSectorDebugController.EnemyReadability captured = values[i];
            bool selected = debug.EnemyMode == captured;
            Button button = CreateButton(
                row, labels[i],
                () =>
                {
                    debug.SetEnemyReadability(captured);
                    RefreshCurrentTab();
                },
                100f);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.38f + i * 0.15f, 0.14f);
            rect.anchorMax = new Vector2(0.515f + i * 0.15f, 0.86f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            if (selected && button.targetGraphic is Image image)
                image.color = successColor;
        }
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
