using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Subject42.Combat.OrbitalStation;

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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
public sealed class CombatFeelTooltipTrigger : MonoBehaviour,
    IPointerEnterHandler, IPointerMoveHandler, IPointerExitHandler
{
    public Func<string> TextProvider;
    public Action<string, RectTransform> Show;
    public Action Hide;

    public void OnPointerEnter(PointerEventData eventData) => Present();
    public void OnPointerMove(PointerEventData eventData) => Present();
    public void OnPointerExit(PointerEventData eventData) => Hide?.Invoke();
    private void OnDisable() => Hide?.Invoke();

    private void Present()
    {
        string text = TextProvider?.Invoke();
        if (!string.IsNullOrWhiteSpace(text))
            Show?.Invoke(text, transform as RectTransform);
    }
}
#endif

public sealed partial class Subject42DebugMenu : MonoBehaviour
{
    [Header("Existing scene systems")]
    [SerializeField] private LevelAnomalyController anomalyController;
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private CharacterSpawner characterSpawner;
    [SerializeField] private UpgradeManager upgradeManager;
    [SerializeField] private RunFlowController runFlowController;
    [SerializeField] private LevelChoiceManager levelChoiceManager;

    [Header("Known project content")]
    [SerializeField] private GameObject turretEnemyPrefab;
    [SerializeField] private GameObject eyesEnemyPrefab;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private static Subject42DebugMenu activeInstance;
    public static bool IsDebugMenuOpen =>
        activeInstance != null && activeInstance.isOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void BindOrbitalInputBlocker() =>
        OrbitalDevelopmentInput.DevelopmentBlocker = () => IsDebugMenuOpen;

    private enum DebugTab
    {
        Run,
        OrbitalProduction,
        Bunker,
        FeelTest,
        VisualTest,
        QA
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

    private enum VisualSection
    {
        Scene,
        Grass,
        Plants,
        World,
        Background,
        Enemies,
        Player,
        Weapon,
        PlayerRing,
        PostFx,
        Atmosphere,
        Anomalies,
        Projectiles,
        HitFx,
        Camera
    }


    private static readonly string[] TabLabels =
    {
        "RUN", "ORBITAL", "BUNKER", "COMBAT", "VISUAL", "QA"
    };



    private GameObject menuRoot;
    private GameObject fullMenuBlocker;
    private Image fullMenuBlockerImage;
    private RectTransform menuPanel;
    private RectTransform menuHeader;
    private RectTransform menuTabBar;
    private RectTransform menuPages;
    private RectTransform closeButtonRect;
    private TextMeshProUGUI menuTitle;
    private GameObject visualBackButton;
    private GameObject previewPanelRoot;
    private TextMeshProUGUI previewText;
    private RectTransform feelTooltipRoot;
    private TextMeshProUGUI feelTooltipText;
    private TextMeshProUGUI feelSaveStatusText;
    private string feelSaveMessage;
    private RectTransform contentRoot;
    private readonly GameObject[] tabRoots = new GameObject[TabLabels.Length];
    private readonly Image[] tabButtonImages = new Image[TabLabels.Length];
    private DebugTab activeTab = DebugTab.Run;
    private ProductionSectorDebugController productionSectorDebug;
    private bool isOpen;
    private bool isPreview;
    private bool menuLiveSimulation;
    private PreviewParameter previewParameter;
    private bool waitingForF1Release;
    private float previousTimeScale;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;
    private string lastOrbitalStateResult;
    private OrbitalStationRuntime.GrowthPreset nextOrbitalGrowthPreset;

    private readonly StringBuilder previewSummary = new();
    private readonly List<CharacterData> debugCharacters = new();
    private readonly List<GameObject> debugEnemies = new();
    private string enemyDebugStatus = "Готово к ручному тесту.";
    private TextMeshProUGUI activeXpCounter;
    private float nextXpCounterRefresh;
    private ProductionVisualTuningController productionVisualTuning;
    private ProductionFeelTuningController productionFeelTuning;
    private CombatFeelTestDummyController feelTestDummy;
    private WorldRuleVisual worldRuleVisual;
    private PlayerWeaponOrbitVisual playerOrbitVisual;
    private CameraFollow cameraFollow;
    private readonly bool[] visualSectionExpanded =
    {
        true, true, true, true, true, false, true, true, true, true,
        true, true, true, false, true
    };
    private CombatFeelGroup selectedFeelLabGroup = CombatFeelGroup.Global;
    private TextMeshProUGUI visualSaveStatusText;
    private string visualSavedBaselineJson;
    private string visualSaveMessage;
    private bool visualHasSavedPreset;
    private bool visualProductionLoaded;
    private VisualTuningSnapshot visualProductionSnapshot;
    private WorldRuleVisual productionAppliedWorldRuleVisual;
    private PlayerWeaponOrbitVisual productionAppliedOrbitVisual;
    private LevelAnomalyController productionAppliedAnomalyController;
    private CameraFollow productionAppliedCameraFollow;

    private readonly Color panelColor = new(0.035f, 0.045f, 0.06f, 0.97f);
    private readonly Color rowColor = new(0.09f, 0.11f, 0.145f, 0.95f);
    private readonly Color accentColor = new(0.13f, 0.58f, 0.72f, 1f);
    private readonly Color mutedColor = new(0.65f, 0.69f, 0.74f, 1f);
    private readonly Color successColor = new(0.36f, 0.82f, 0.48f, 1f);
    private readonly Color warningColor = new(1f, 0.69f, 0.25f, 1f);

    private void Awake()
    {
        activeInstance = this;
        ResolveSceneReferences();
    }

    private void OnEnable()
    {
        ProductionAnomalySite.VisualTargetsChanged +=
            HandleAnomalyVisualTargetsChanged;
    }

    private void Start()
    {
        BindBotLabScene();
        if (characterSpawner != null)
        {
            EnsureProductionSectorDebug();
            LoadVisualProductionValues();
        }
        else
            activeTab = DebugTab.Bunker;
        BuildMenu();
        RefreshTab(activeTab);
        SelectTab(activeTab, false);
        if (menuRoot != null)
            menuRoot.SetActive(false);
    }

    private void Update()
    {
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
        ProductionAnomalySite.VisualTargetsChanged -=
            HandleAnomalyVisualTargetsChanged;

        if (isOpen)
            CloseMenu();

        isPreview = false;
        if (previewPanelRoot != null)
            previewPanelRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        ProductionAnomalySite.VisualTargetsChanged -=
            HandleAnomalyVisualTargetsChanged;

        if (isOpen)
            RestoreGameState();

        if (menuRoot != null)
            Destroy(menuRoot);
        if (activeInstance == this)
            activeInstance = null;
    }

    private void HandleAnomalyVisualTargetsChanged()
    {
        if (isOpen && activeTab == DebugTab.VisualTest &&
            visualSectionExpanded[(int)VisualSection.Anomalies])
        {
            RefreshCurrentTab();
        }
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        PhysicalCombatFeedbackRuntime.CancelHitStopForExternalTimeControl();
#endif
        ResolveSceneReferences();
        FindFirstObjectByType<OrbitalInteractionController>()?.PrepareForExternalPause();
        previousTimeScale = Time.timeScale;
        previousCursorVisible = Cursor.visible;
        previousCursorLockMode = Cursor.lockState;
        menuLiveSimulation = IsRuntimeLabTab(activeTab);
        RefreshTab(activeTab);

        Time.timeScale = menuLiveSimulation ? previousTimeScale : 0f;
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
        previewSummary.AppendLine("Основное mouse-only управление: F1 → VISUAL");
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
        anomalyController ??= FindFirstObjectByType<LevelAnomalyController>();
        enemySpawner ??= FindFirstObjectByType<EnemySpawner>();
        characterSpawner ??= FindFirstObjectByType<CharacterSpawner>();
        upgradeManager ??= UpgradeManager.Instance != null
            ? UpgradeManager.Instance
            : FindFirstObjectByType<UpgradeManager>();
        runFlowController ??= RunFlowController.Instance != null
            ? RunFlowController.Instance
            : FindFirstObjectByType<RunFlowController>();
        levelChoiceManager ??= FindFirstObjectByType<LevelChoiceManager>();
        worldRuleVisual ??= FindFirstObjectByType<WorldRuleVisual>();
        playerOrbitVisual ??= FindFirstObjectByType<PlayerWeaponOrbitVisual>();
        cameraFollow ??= FindFirstObjectByType<CameraFollow>();
        ApplyVisualProductionToLateTargets();


    }

    private void EnsureProductionSectorDebug()
    {
        productionSectorDebug ??=
            GetComponent<ProductionSectorDebugController>();
        productionSectorDebug ??=
            gameObject.AddComponent<ProductionSectorDebugController>();

        productionVisualTuning ??=
            GetComponent<ProductionVisualTuningController>();
        productionVisualTuning ??=
            gameObject.AddComponent<ProductionVisualTuningController>();
        productionVisualTuning.Configure();

        productionFeelTuning ??=
            GetComponent<ProductionFeelTuningController>();
        productionFeelTuning ??=
            gameObject.AddComponent<ProductionFeelTuningController>();
        productionFeelTuning.Configure();

        feelTestDummy ??= GetComponent<CombatFeelTestDummyController>();
        feelTestDummy ??= gameObject.AddComponent<CombatFeelTestDummyController>();
        feelTestDummy.Configure(enemySpawner);
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
            "Title", header, "SUBJECT#42 — ОТЛАДОЧНОЕ МЕНЮ", 28f,
            TextAlignmentOptions.MidlineLeft, Color.white
        );
        menuTitle = title;
        Stretch(title.rectTransform, 22f, 90f);

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
            button.interactable = (DebugTab)i == DebugTab.Bunker
                ? FindFirstObjectByType<BunkerRunStarter>() != null
                : characterSpawner != null;
            Stretch(button.GetComponent<RectTransform>());
            TextMeshProUGUI tabText = button.GetComponentInChildren<TextMeshProUGUI>();
            if (tabText != null && labels.Length > 7)
                tabText.fontSize = 12f;
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
            bool compactLab = IsRuntimeLabTab((DebugTab)i);
            tabRoots[i] = CreateTabPage(labels[i], pages, out _, compactLab);
        }

        BuildVisualBackButton(header);
        if (characterSpawner != null)
        {
            BuildVisualActionBar(tabRoots[(int)DebugTab.VisualTest].transform);
            BuildFeelActionBar(tabRoots[(int)DebugTab.FeelTest].transform);
        }

        BuildPreviewPanel();
        BuildFeelTooltip();
    }

    private void BuildFeelTooltip()
    {
        feelTooltipRoot = CreateRect("Combat Feel Tooltip", menuRoot.transform);
        feelTooltipRoot.anchorMin = feelTooltipRoot.anchorMax = new Vector2(.5f, .5f);
        feelTooltipRoot.pivot = new Vector2(0f, 1f);
        feelTooltipRoot.sizeDelta = new Vector2(540f, 270f);
        Image background = feelTooltipRoot.gameObject.AddComponent<Image>();
        background.color = new Color(.025f, .035f, .05f, .985f);
        background.raycastTarget = false;
        Outline outline = feelTooltipRoot.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(accentColor.r, accentColor.g, accentColor.b, .8f);
        outline.effectDistance = new Vector2(1f, -1f);
        feelTooltipText = CreateText("Tooltip Text", feelTooltipRoot, string.Empty,
            15f, TextAlignmentOptions.TopLeft, Color.white);
        feelTooltipText.textWrappingMode = TextWrappingModes.Normal;
        feelTooltipText.richText = true;
        Stretch(feelTooltipText.rectTransform, 14f, 14f, 10f, 10f);
        feelTooltipRoot.gameObject.SetActive(false);
    }

    private void AttachFeelTooltip(GameObject target, Func<string> textProvider)
    {
        if (target == null) return;
        CombatFeelTooltipTrigger trigger = target.AddComponent<CombatFeelTooltipTrigger>();
        trigger.TextProvider = textProvider;
        trigger.Show = ShowFeelTooltip;
        trigger.Hide = HideFeelTooltip;
    }

    private void ShowFeelTooltip(string text, RectTransform hoveredRect)
    {
        if (feelTooltipRoot == null || feelTooltipText == null || menuRoot == null ||
            hoveredRect == null) return;
        feelTooltipText.text = text;
        feelTooltipRoot.gameObject.SetActive(true);
        feelTooltipRoot.SetAsLastSibling();

        float tooltipWidth = Mathf.Clamp(Screen.width - 16f, 280f, 540f);
        float preferredHeight = feelTooltipText.GetPreferredValues(
            text, tooltipWidth - 28f, 0f).y + 24f;
        feelTooltipRoot.sizeDelta = new Vector2(tooltipWidth,
            Mathf.Clamp(preferredHeight, 150f, 330f));

        RectTransform root = menuRoot.GetComponent<RectTransform>();
        Vector3[] corners = new Vector3[4];
        hoveredRect.GetWorldCorners(corners);
        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
        Vector2 topRight = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
        Vector2 size = feelTooltipRoot.rect.size;
        const float gap = 12f;
        const float edge = 8f;

        Vector2 screenPoint;
        if (topRight.x + gap + size.x <= Screen.width - edge)
            screenPoint = new Vector2(topRight.x + gap, topRight.y);
        else if (bottomLeft.x - gap - size.x >= edge)
            screenPoint = new Vector2(bottomLeft.x - gap - size.x, topRight.y);
        else if (bottomLeft.y - gap - size.y >= edge)
            screenPoint = new Vector2(bottomLeft.x, bottomLeft.y - gap);
        else
            screenPoint = new Vector2(bottomLeft.x, topRight.y + gap + size.y);

        screenPoint.x = Mathf.Clamp(screenPoint.x, edge, Screen.width - size.x - edge);
        screenPoint.y = Mathf.Clamp(screenPoint.y, size.y + edge, Screen.height - edge);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                root, screenPoint, null, out Vector2 point)) return;
        Rect bounds = root.rect;
        float x = Mathf.Clamp(point.x, bounds.xMin + edge,
            bounds.xMax - size.x - edge);
        float y = Mathf.Clamp(point.y, bounds.yMin + size.y + edge,
            bounds.yMax - edge);
        feelTooltipRoot.anchoredPosition = new Vector2(x, y);
    }

    private void HideFeelTooltip()
    {
        if (feelTooltipRoot != null) feelTooltipRoot.gameObject.SetActive(false);
    }

    private void BuildVisualBackButton(Transform header)
    {
        Button button = CreateButton(
            header,
            "MENU",
            () => SelectTab(characterSpawner != null ? DebugTab.Run : DebugTab.Bunker),
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

    private void BuildVisualActionBar(Transform visualPage)
    {
        RectTransform viewport = visualPage.Find("Viewport") as RectTransform;
        if (viewport != null)
            viewport.offsetMin = new Vector2(viewport.offsetMin.x, 42f);

        RectTransform scrollbar = visualPage.Find("Scrollbar") as RectTransform;
        if (scrollbar != null)
            scrollbar.offsetMin = new Vector2(scrollbar.offsetMin.x, 44f);

        RectTransform bar = CreateRect("Visual Actions", visualPage);
        bar.anchorMin = Vector2.zero;
        bar.anchorMax = new Vector2(1f, 0f);
        bar.pivot = new Vector2(0.5f, 0f);
        bar.offsetMin = new Vector2(0f, 0f);
        bar.offsetMax = new Vector2(0f, 38f);
        Image background = bar.gameObject.AddComponent<Image>();
        background.color = new Color(0.045f, 0.055f, 0.07f, 0.98f);

        Button reset = CreateButton(
            bar,
            "RESET ALL VISUAL",
            ResetAllVisualLabValues,
            120f
        );
        RectTransform resetRect = reset.GetComponent<RectTransform>();
        resetRect.anchorMin = new Vector2(0f, 0f);
        resetRect.anchorMax = new Vector2(1f / 3f, 1f);
        resetRect.offsetMin = new Vector2(4f, 5f);
        resetRect.offsetMax = new Vector2(-2f, -5f);

        Button save = CreateButton(
            bar, "СОХРАНИТЬ ЗНАЧЕНИЯ", SaveVisualLabValues, 120f);
        RectTransform saveRect = save.GetComponent<RectTransform>();
        saveRect.anchorMin = new Vector2(1f / 3f, 0f);
        saveRect.anchorMax = new Vector2(2f / 3f, 1f);
        saveRect.offsetMin = new Vector2(2f, 5f);
        saveRect.offsetMax = new Vector2(-2f, -5f);

        Button copy = CreateButton(
            bar,
            "COPY TO CLIPBOARD",
            CopyVisualLabValues,
            120f
        );
        RectTransform copyRect = copy.GetComponent<RectTransform>();
        copyRect.anchorMin = new Vector2(2f / 3f, 0f);
        copyRect.anchorMax = Vector2.one;
        copyRect.offsetMin = new Vector2(2f, 5f);
        copyRect.offsetMax = new Vector2(-4f, -5f);

        TextMeshProUGUI resetText = reset.GetComponentInChildren<TextMeshProUGUI>();
        TextMeshProUGUI saveText = save.GetComponentInChildren<TextMeshProUGUI>();
        TextMeshProUGUI copyText = copy.GetComponentInChildren<TextMeshProUGUI>();
        if (resetText != null) resetText.fontSize = 10f;
        if (saveText != null) saveText.fontSize = 8.5f;
        if (copyText != null) copyText.fontSize = 8.5f;
        AttachFeelTooltip(save.gameObject, () =>
            "Сохраняет текущие параметры как рабочие значения проекта. " +
            "Они останутся после выхода из Play Mode.");
        AttachFeelTooltip(copy.gameObject, () =>
            "Копирует текущие параметры в буфер обмена. Не сохраняет их в игру.");
    }

    private void BuildFeelActionBar(Transform feelPage)
    {
        RectTransform viewport = feelPage.Find("Viewport") as RectTransform;
        if (viewport != null)
            viewport.offsetMin = new Vector2(viewport.offsetMin.x, 42f);

        RectTransform scrollbar = feelPage.Find("Scrollbar") as RectTransform;
        if (scrollbar != null)
            scrollbar.offsetMin = new Vector2(scrollbar.offsetMin.x, 44f);

        RectTransform bar = CreateRect("Feel Actions", feelPage);
        bar.anchorMin = Vector2.zero;
        bar.anchorMax = new Vector2(1f, 0f);
        bar.pivot = new Vector2(0.5f, 0f);
        bar.offsetMin = Vector2.zero;
        bar.offsetMax = new Vector2(0f, 38f);
        bar.gameObject.AddComponent<Image>().color =
            new Color(0.045f, 0.055f, 0.07f, 0.98f);

        Button reset = CreateButton(
            bar, "RESET ALL", ResetAllFeelLabValues, 120f);
        RectTransform resetRect = reset.GetComponent<RectTransform>();
        resetRect.anchorMin = Vector2.zero;
        resetRect.anchorMax = new Vector2(1f / 3f, 1f);
        resetRect.offsetMin = new Vector2(4f, 5f);
        resetRect.offsetMax = new Vector2(-2f, -5f);

        Button save = CreateButton(
            bar, "SAVE VALUES", SaveFeelLabValues, 120f);
        RectTransform saveRect = save.GetComponent<RectTransform>();
        saveRect.anchorMin = new Vector2(1f / 3f, 0f);
        saveRect.anchorMax = new Vector2(2f / 3f, 1f);
        saveRect.offsetMin = new Vector2(2f, 5f);
        saveRect.offsetMax = new Vector2(-2f, -5f);

        Button copy = CreateButton(
            bar, "COPY TO CLIPBOARD", CopyFeelLabValues, 120f);
        RectTransform copyRect = copy.GetComponent<RectTransform>();
        copyRect.anchorMin = new Vector2(2f / 3f, 0f);
        copyRect.anchorMax = Vector2.one;
        copyRect.offsetMin = new Vector2(2f, 5f);
        copyRect.offsetMax = new Vector2(-4f, -5f);

        AttachFeelTooltip(save.gameObject, () =>
            "Сохраняет все текущие значения в отдельный asset проекта. " +
            "Asset остаётся после выхода из Play Mode; production defaults " +
            "не меняются автоматически.");
        AttachFeelTooltip(copy.gameObject, () =>
            "Копирует все текущие значения FEEL LAB в буфер обмена. " +
            "Это не сохраняет их в production-настройки.");
        AttachFeelTooltip(reset.gameObject, () =>
            "Сразу возвращает параметры к production-значениям. " +
            "RESET не сохраняет результат.");

        TextMeshProUGUI resetText = reset.GetComponentInChildren<TextMeshProUGUI>();
        TextMeshProUGUI saveText = save.GetComponentInChildren<TextMeshProUGUI>();
        TextMeshProUGUI copyText = copy.GetComponentInChildren<TextMeshProUGUI>();
        if (resetText != null) resetText.fontSize = 10f;
        if (saveText != null) saveText.fontSize = 10f;
        if (copyText != null) copyText.fontSize = 8.5f;
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
        activeTab = tab;
        if (isOpen)
        {
            bool choiceIsOpen =
                (levelChoiceManager != null && levelChoiceManager.IsChoosing) ||
                (upgradeManager != null && upgradeManager.IsChoosingUpgrade);
            menuLiveSimulation = IsRuntimeLabTab(tab) &&
                !choiceIsOpen && previousTimeScale > 0f;
            Time.timeScale = menuLiveSimulation ? previousTimeScale : 0f;
        }
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

    private void RefreshCurrentTab()
    {
        HideFeelTooltip();
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

        if (!IsRuntimeLabTab(tab))
            AddTabHeading(TabLabels[(int)tab]);

        bool runTab = tab == DebugTab.Run || tab == DebugTab.OrbitalProduction ||
            tab == DebugTab.FeelTest || tab == DebugTab.VisualTest || tab == DebugTab.QA;
        if (runTab && characterSpawner == null)
        {
            AddHint("Start a run from Bunker to use these tools.");
            return;
        }

        switch (tab)
        {
            case DebugTab.Run:
                AddRunSection();
                AddSectorFlowSection();
                break;
            case DebugTab.OrbitalProduction:
                AddOrbitalProductionSection();
                break;
            case DebugTab.Bunker:
                AddBunkerSection();
                AddRoomStateRows();
                break;
            case DebugTab.FeelTest:
                AddInteractiveFeelLab();
                break;
            case DebugTab.VisualTest:
                AddInteractiveAnomalyVisualTest();
                break;
            case DebugTab.QA:
                AddQaSection();
                break;
        }

        if (!IsRuntimeLabTab(activeTab))
        {
            AddHint(menuLiveSimulation
                ? "F1 закрывает меню. LIVE: симуляция продолжает работать."
                : "F1 закрывает меню. PAUSED: симуляция остановлена.");
        }
    }

    private void ApplyMenuViewportLayout()
    {
        if (menuPanel == null)
            return;

        bool compact = IsRuntimeLabTab(activeTab);
        if (compact)
        {
            menuPanel.anchorMin = new Vector2(1f, 0.02f);
            menuPanel.anchorMax = new Vector2(1f, 0.98f);
            menuPanel.pivot = new Vector2(1f, 0.5f);
            menuPanel.sizeDelta = new Vector2(390f, 0f);
            menuPanel.anchoredPosition = new Vector2(-16f, 0f);
        }
        else
        {
            menuPanel.anchorMin = new Vector2(0.07f, 0.05f);
            menuPanel.anchorMax = new Vector2(0.93f, 0.95f);
            menuPanel.pivot = new Vector2(0.5f, 0.5f);
            menuPanel.sizeDelta = Vector2.zero;
            menuPanel.anchoredPosition = Vector2.zero;
        }

        if (menuHeader != null)
            menuHeader.sizeDelta = new Vector2(0f, compact ? 44f : 62f);
        if (menuTitle != null)
        {
            menuTitle.text = activeTab == DebugTab.VisualTest
                ? "RUNTIME VISUAL LAB"
                : activeTab == DebugTab.FeelTest
                    ? "COMBAT"
                    : activeTab == DebugTab.OrbitalProduction
                        ? "ORBITAL"
                        : "SUBJECT#42 — ОТЛАДОЧНОЕ МЕНЮ";
            menuTitle.fontSize = compact ? 17f : 28f;
            Stretch(menuTitle.rectTransform,
                compact ? 12f : 22f,
                compact ? 112f : 90f);
        }
        if (closeButtonRect != null)
        {
            closeButtonRect.anchoredPosition = new Vector2(
                compact ? -8f : -14f, 0f);
            closeButtonRect.sizeDelta = compact
                ? new Vector2(32f, 26f)
                : new Vector2(52f, 40f);
        }
        if (menuTabBar != null)
            menuTabBar.gameObject.SetActive(!compact);
        if (visualBackButton != null)
            visualBackButton.SetActive(compact);
        if (menuPages != null)
        {
            menuPages.offsetMin = compact
                ? new Vector2(6f, 6f)
                : new Vector2(18f, 18f);
            menuPages.offsetMax = compact
                ? new Vector2(-6f, -46f)
                : new Vector2(-18f, -120f);
        }

        if (fullMenuBlockerImage != null)
        {
            fullMenuBlockerImage.color = compact
                ? Color.clear
                : new Color(0f, 0f, 0f, 0.68f);
            fullMenuBlockerImage.raycastTarget = !compact;
        }
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
        if (activeTab == DebugTab.VisualTest && contentRoot.childCount > 0)
            AttachFeelTooltip(contentRoot.GetChild(contentRoot.childCount - 1).gameObject,
                () => BuildVisualTooltipRu(label, 0f, 1f, enabled ? 1f : 0f));
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

    private void AddInteractiveAnomalyVisualTest()
    {
        EnsureProductionSectorDebug();
        ProductionSectorDebugController debug = productionSectorDebug;

        if (debug == null)
        {
            AddSectionTitle("VISUAL", "Development / Editor only");
            AddHint("ProductionSectorDebugController не создан.");
            return;
        }

        AddVisualLiveStatus();
        AddRow("Scene preview", "F1 RETURNS TO MENU", accentColor, "PREVIEW", true, EnterScenePreview);
        AddVisualObjectFocusButtons();

        ProductionVisualTuningController environment = productionVisualTuning;
        if (AddVisualSectionHeader(
                VisualSection.Scene, "СЦЕНА",
                "Global Volume; Screen Space Overlay UI остаётся нейтральным"))
        {
            if (environment != null)
            {
                AddVisualTintChannels("Цвет сцены", environment.SceneTint,
                    environment.SetSceneTint);
                AddSliderRow("Сила цвета сцены", environment.SceneTintAmount,
                    0f, 1f, environment.SetSceneTintAmount, "0.00",
                    visualProductionLoaded ? visualProductionSnapshot.SceneTintAmount : 0f);
                AddSliderRow("Насыщенность сцены", environment.SceneSaturation,
                    0f, 3f, environment.SetSceneSaturation, "0.00",
                    visualProductionLoaded ? visualProductionSnapshot.SceneSaturation : 1f);
                AddSliderRow("Яркость сцены", environment.SceneBrightness,
                    .25f, 2.5f, environment.SetSceneBrightness, "0.00",
                    visualProductionLoaded ? visualProductionSnapshot.SceneBrightness : 1f);
                AddVisualSectionReset("SCENE", () =>
                    ResetVisualSectionToProduction(VisualSection.Scene));
            }
        }

        if (AddVisualSectionHeader(
                VisualSection.Grass, "ТРАВА",
                environment != null
                    ? $"{environment.GrassRendererCount} renderer(s), только Grass"
                    : "Environment controller unavailable"))
        {
            if (environment != null)
            {
                AddVisualTintChannels("Цвет травы", environment.GrassTint,
                    environment.SetGrassTint);
                AddSliderRow("Сила цвета травы", environment.GrassTintAmount,
                    0f, 1f, environment.SetGrassTintAmount, "0.00",
                    visualProductionLoaded ? visualProductionSnapshot.GrassTintAmount : 0f);
                AddSliderRow("Насыщенность травы", environment.GrassSaturation,
                    0f, 3f, environment.SetGrassSaturation, "0.00",
                    visualProductionLoaded ? visualProductionSnapshot.GrassSaturation : 1f);
                AddSliderRow("Яркость травы", environment.GrassBrightness,
                    .25f, 2.5f, environment.SetGrassBrightness, "0.00",
                    visualProductionLoaded ? visualProductionSnapshot.GrassBrightness : 1f);
                AddVisualSectionReset("GRASS", () =>
                    ResetVisualSectionToProduction(VisualSection.Grass));
            }
        }

        if (AddVisualSectionHeader(
                VisualSection.Plants, "РАСТЕНИЯ / ДЕРЕВЬЯ",
                environment != null
                    ? $"{environment.PlantRendererCount} renderer(s), root Plants"
                    : "Environment controller unavailable"))
        {
            if (environment != null)
            {
                AddVisualTintChannels("Цвет растений", environment.PlantsTint,
                    environment.SetPlantsTint);
                AddSliderRow("Сила цвета растений", environment.PlantsTintAmount,
                    0f, 1f, environment.SetPlantsTintAmount, "0.00",
                    visualProductionLoaded ? visualProductionSnapshot.PlantsTintAmount : 0f);
                AddSliderRow("Насыщенность растений", environment.PlantsSaturation,
                    0f, 3f, environment.SetPlantsSaturation, "0.00",
                    visualProductionLoaded ? visualProductionSnapshot.PlantsSaturation : 1f);
                AddSliderRow("Яркость растений", environment.PlantsBrightness,
                    .25f, 2.5f, environment.SetPlantsBrightness, "0.00",
                    visualProductionLoaded ? visualProductionSnapshot.PlantsBrightness : 1f);
                AddVisualSectionReset("PLANTS", () =>
                    ResetVisualSectionToProduction(VisualSection.Plants));
            }
        }

        if (AddVisualSectionHeader(
                VisualSection.World,
                "ENVIRONMENT",
                "Environment readability and decor"))
        {
            AddProductionReadabilityPreset(
                ProductionSectorDebugController.ReadabilityPreset.Original);
            AddProductionReadabilityPreset(
                ProductionSectorDebugController.ReadabilityPreset.Muted);
            AddProductionReadabilityPreset(
                ProductionSectorDebugController.ReadabilityPreset.HighGameplayContrast);
            AddProductionReadabilityPreset(
                ProductionSectorDebugController.ReadabilityPreset.DarkWorld);
            AddSliderRow(
                "Decor Brightness", debug.DecorBrightness,
                0.25f, 1.5f, debug.SetDecorBrightness, "0.00",
                debug.ProductionDecorBrightness);
            AddSliderRow(
                "Environment Darken", debug.EnvironmentDarken,
                0f, 1f, debug.SetEnvironmentDarken, "0.00",
                visualProductionLoaded
                    ? visualProductionSnapshot.EnvironmentDarken : 0f);
            AddVisualSectionReset("ENVIRONMENT", () =>
                ResetVisualSectionToProduction(VisualSection.World));
        }

        if (AddVisualSectionHeader(
                VisualSection.Background,
                "BACKGROUND TILES",
                environment != null
                    ? $"{environment.BackgroundRendererCount} TilemapRenderer(s)"
                    : "Environment controller unavailable"))
        {
            if (environment != null)
            {
                AddVisualTintChannels("Цвет background", environment.BackgroundTint,
                    environment.SetBackgroundTint);
                AddSliderRow("Сила цвета background", environment.BackgroundTintAmount,
                    0f, 1f, environment.SetBackgroundTintAmount, "0.00",
                    visualProductionLoaded ? visualProductionSnapshot.BackgroundTintAmount : 0f);
                AddSliderRow("Насыщенность background", environment.BackgroundSaturation,
                    0f, 3f, environment.SetBackgroundSaturation, "0.00",
                    visualProductionLoaded ? visualProductionSnapshot.BackgroundSaturation : 1f);
                AddSliderRow("Яркость background", environment.BackgroundBrightness,
                    .25f, 2.5f, environment.SetBackgroundBrightness, "0.00",
                    visualProductionLoaded ? visualProductionSnapshot.BackgroundBrightness : 1f);
                AddVisualSectionReset("BACKGROUND", () =>
                    ResetVisualSectionToProduction(VisualSection.Background));
            }
        }

        if (AddVisualSectionHeader(
                VisualSection.Enemies,
                "ENEMIES",
                $"{debug.RegisteredEnemyCount} registered via spawn hooks"))
        {
            AddEnemyReadabilityPresetStrip(debug);
            AddHint("AFFECTED ENEMIES");
            AddProductionEnemyScope(
                ProductionSectorDebugController.EnemyScope.All);
            AddProductionEnemyScope(
                ProductionSectorDebugController.EnemyScope.CurrentZone);
            AddProductionEnemyScope(
                ProductionSectorDebugController.EnemyScope.Basic);
            AddProductionEnemyScope(
                ProductionSectorDebugController.EnemyScope.Elite);
            AddProductionEnemyScope(
                ProductionSectorDebugController.EnemyScope.Shooter);
            AddProductionEnemyScope(
                ProductionSectorDebugController.EnemyScope.Bomber);
            AddProductionEnemyScope(
                ProductionSectorDebugController.EnemyScope.Boss);
            AddSliderRow(
                "Brightness", debug.EnemyBrightness,
                0.5f, 2.5f, debug.SetEnemyBrightness, "0.00",
                debug.ProductionEnemyBrightness);
            AddSliderRow(
                "Saturation", debug.EnemySaturation,
                0f, 3f, debug.SetEnemySaturation, "0.00",
                debug.ProductionEnemySaturation);
            AddSliderRow(
                "Tint Strength", debug.EnemyTintStrength,
                0f, 1f, debug.SetEnemyTintStrength, "0.00",
                debug.ProductionEnemyTintStrength);
            AddHint("RECOLOR PRESETS");
            AddEnemyRecolorPresetStrip(debug);
            AddSliderRow(
                "Hue Shift", debug.EnemyHueShift,
                -180f, 180f, debug.SetEnemyHueShift, "0",
                debug.ProductionEnemyHueShift);
            Color recolorTarget = debug.EnemyRecolorTarget;
            AddRow(
                "Target Color",
                "#" + ColorUtility.ToHtmlStringRGB(recolorTarget),
                recolorTarget,
                string.Empty,
                false,
                null);
            AddEnemyRecolorColorChannel(debug, "Target Color R", 0);
            AddEnemyRecolorColorChannel(debug, "Target Color G", 1);
            AddEnemyRecolorColorChannel(debug, "Target Color B", 2);
            AddSliderRow(
                "Recolor Strength", debug.EnemyRecolorStrength,
                0f, 1f, debug.SetEnemyRecolorStrength, "0.00",
                debug.ProductionEnemyRecolorStrength);
            AddToggleRow(
                "OUTLINE", debug.EnemyOutlineEnabled, true,
                () => debug.SetEnemyOutlineEnabled(!debug.EnemyOutlineEnabled));
            AddSliderRow(
                "Outline Strength", debug.EnemyOutlineStrength,
                0f, 2f,
                value =>
                {
                    debug.SetEnemyOutlineStrength(value);
                    debug.SetEnemyOutlineEnabled(value > 0f);
                },
                "0.00", debug.ProductionEnemyOutlineStrength);
            AddSliderRow(
                "Outline Width", debug.EnemyOutlineWidth,
                0.5f, 4f, debug.SetEnemyOutlineWidth, "0.00",
                debug.ProductionEnemyOutlineWidth);
            AddVisualSectionReset("ENEMIES", () =>
                ResetVisualSectionToProduction(VisualSection.Enemies));
        }

        if (AddVisualSectionHeader(
                VisualSection.Player,
                "PLAYER",
                "Production SpriteLight2D and weapon orbit"))
        {
            if (worldRuleVisual != null && worldRuleVisual.PlayerGlowAvailable)
            {
                AddSliderRow(
                    "Player Glow Intensity",
                    worldRuleVisual.PlayerGlowIntensityMultiplier,
                    0f, 5f,
                    worldRuleVisual.SetPlayerGlowIntensityMultiplier,
                    "0.00", visualProductionLoaded
                        ? visualProductionSnapshot.PlayerGlowIntensity : 1f);
                AddSliderRow(
                    "Player Glow Radius",
                    worldRuleVisual.PlayerGlowRadiusMultiplier,
                    0.1f, 5f,
                    worldRuleVisual.SetPlayerGlowRadiusMultiplier,
                    "0.00", visualProductionLoaded
                        ? visualProductionSnapshot.PlayerGlowRadius : 1f);
                AddHint("Multipliers: 1.00 = authored production value.");
            }
            else
            {
                AddHint("Player SpriteLight2D is not available yet.");
            }

            ProductionVisualTuningController tuning = productionVisualTuning;
            if (tuning != null && tuning.PlayerRendererCount > 0)
            {
                AddSliderRow("Размер игрока", tuning.PlayerVisualScale,
                    .5f, 2f, tuning.SetPlayerVisualScale, "0.00",
                    visualProductionLoaded ? visualProductionSnapshot.PlayerScale : 1f);
                AddSliderRow("Смещение игрока X", tuning.PlayerVisualOffset.x,
                    -2f, 2f, tuning.SetPlayerVisualOffsetX, "0.00",
                    visualProductionLoaded ? visualProductionSnapshot.PlayerOffsetX : 0f);
                AddSliderRow("Смещение игрока Y", tuning.PlayerVisualOffset.y,
                    -2f, 2f, tuning.SetPlayerVisualOffsetY, "0.00",
                    visualProductionLoaded ? visualProductionSnapshot.PlayerOffsetY : 0f);
                AddSliderRow("Яркость игрока", tuning.PlayerBrightness,
                    0f, 4f, tuning.SetPlayerBrightness, "0.00",
                    visualProductionLoaded ? visualProductionSnapshot.PlayerBrightness : 1f);
                AddSliderRow("Насыщенность игрока", tuning.PlayerSaturation,
                    0f, 3f, tuning.SetPlayerSaturation, "0.00",
                    visualProductionLoaded ? visualProductionSnapshot.PlayerSaturation : 1f);
                AddSliderRow("Прозрачность игрока", tuning.PlayerOpacity,
                    0f, 1f, tuning.SetPlayerOpacity, "0.00",
                    visualProductionLoaded ? visualProductionSnapshot.PlayerOpacity : 1f);
                AddSliderRow("Сила оттенка игрока", tuning.PlayerTintStrength,
                    0f, 1f, tuning.SetPlayerTintStrength, "0.00",
                    visualProductionLoaded ? visualProductionSnapshot.PlayerTintStrength : 0f);
                AddVisualTintChannels("Оттенок игрока", tuning.PlayerTint,
                    tuning.SetPlayerTint);
                AddHint("Меняются только SpriteRenderer. Collider и gameplay root остаются без изменений.");
            }
            else AddHint("SpriteRenderer игрока пока не найден.");

            AddVisualSectionReset(
                "PLAYER",
                () => ResetVisualSectionToProduction(VisualSection.Player));
        }

        if (productionVisualTuning != null && productionVisualTuning.WeaponRendererCount > 0 && AddVisualSectionHeader(
                VisualSection.Weapon, "WEAPON", "Visual дочерних SpriteRenderer оружия"))
        {
            ProductionVisualTuningController tuning = productionVisualTuning;
            if (tuning != null && tuning.WeaponRendererCount > 0)
            {
                AddSliderRow("Размер оружия", tuning.WeaponVisualScale,
                    .5f, 2.5f, tuning.SetWeaponVisualScale, "0.00",
                    visualProductionLoaded ? visualProductionSnapshot.WeaponScale : 1f);
                AddSliderRow("Смещение оружия X", tuning.WeaponVisualOffset.x,
                    -2f, 2f, tuning.SetWeaponVisualOffsetX, "0.00",
                    visualProductionLoaded ? visualProductionSnapshot.WeaponOffsetX : 0f);
                AddSliderRow("Смещение оружия Y", tuning.WeaponVisualOffset.y,
                    -2f, 2f, tuning.SetWeaponVisualOffsetY, "0.00",
                    visualProductionLoaded ? visualProductionSnapshot.WeaponOffsetY : 0f);
                AddSliderRow("Яркость оружия", tuning.WeaponBrightness,
                    0f, 4f, tuning.SetWeaponBrightness, "0.00",
                    visualProductionLoaded ? visualProductionSnapshot.WeaponBrightness : 1f);
                AddSliderRow("Насыщенность оружия", tuning.WeaponSaturation,
                    0f, 3f, tuning.SetWeaponSaturation, "0.00",
                    visualProductionLoaded ? visualProductionSnapshot.WeaponSaturation : 1f);
                AddSliderRow("Прозрачность оружия", tuning.WeaponOpacity,
                    0f, 1f, tuning.SetWeaponOpacity, "0.00",
                    visualProductionLoaded ? visualProductionSnapshot.WeaponOpacity : 1f);
                AddSliderRow("Сила оттенка оружия", tuning.WeaponTintStrength,
                    0f, 1f, tuning.SetWeaponTintStrength, "0.00",
                    visualProductionLoaded ? visualProductionSnapshot.WeaponTintStrength : 0f);
                AddVisualTintChannels("Оттенок оружия", tuning.WeaponTint,
                    tuning.SetWeaponTint);
                AddHint("Fire Point и физическая точка вылета не смещаются.");
            }
            else AddHint("Активный SpriteRenderer оружия пока не найден.");
            AddVisualSectionReset("WEAPON", () =>
                ResetVisualSectionToProduction(VisualSection.Weapon));
        }

        if (playerOrbitVisual != null && AddVisualSectionHeader(
                VisualSection.PlayerRing, "PLAYER RING", "Декоративное кольцо оружейной орбиты"))
        {
            if (playerOrbitVisual != null && playerOrbitVisual.HasOrbitSource)
            {
                AddToggleRow("КОЛЬЦО ВКЛЮЧЕНО", playerOrbitVisual.RingEnabled,
                    true, () => playerOrbitVisual.SetRingEnabled(!playerOrbitVisual.RingEnabled));
                AddSliderRow("Радиус кольца", playerOrbitVisual.RingRadiusMultiplier,
                    .25f, 3f, playerOrbitVisual.SetRingRadiusMultiplier, "0.00",
                    visualProductionLoaded ? visualProductionSnapshot.RingRadius : 1f);
                AddSliderRow("Толщина кольца", playerOrbitVisual.RingWidth,
                    .005f, .15f, playerOrbitVisual.SetRingWidth, "0.000",
                    visualProductionLoaded ? visualProductionSnapshot.RingWidth : .035f);
                AddSliderRow("Яркость кольца", playerOrbitVisual.RingIntensity,
                    0f, 4f, playerOrbitVisual.SetRingIntensity, "0.00",
                    visualProductionLoaded ? visualProductionSnapshot.RingBrightness : 1.25f);
                AddSliderRow("Прозрачность кольца", playerOrbitVisual.RingAlpha,
                    0f, 1f, playerOrbitVisual.SetRingAlpha, "0.00",
                    visualProductionLoaded ? visualProductionSnapshot.RingOpacity : .72f);
                AddSliderRow("Сила пульсации", playerOrbitVisual.RingPulseAmount,
                    0f, .75f, playerOrbitVisual.SetRingPulseAmount, "0.00",
                    visualProductionLoaded ? visualProductionSnapshot.RingPulseAmount : .06f);
                AddSliderRow("Скорость пульсации", playerOrbitVisual.RingPulseSpeed,
                    0f, 5f, playerOrbitVisual.SetRingPulseSpeed, "0.00",
                    visualProductionLoaded ? visualProductionSnapshot.RingPulseSpeed : .22f);
                AddSliderRow("Скорость вращения", playerOrbitVisual.RingRotationSpeed,
                    -180f, 180f, playerOrbitVisual.SetRingRotationSpeed, "0.0",
                    visualProductionLoaded ? visualProductionSnapshot.RingRotationSpeed : 4f);
                AddSliderRow("Смещение кольца X", playerOrbitVisual.RingOffset.x,
                    -3f, 3f, playerOrbitVisual.SetRingOffsetX, "0.00", 0f);
                AddSliderRow("Смещение кольца Y", playerOrbitVisual.RingOffset.y,
                    -3f, 3f, playerOrbitVisual.SetRingOffsetY, "0.00", 0f);
                AddVisualTintChannels("Цвет кольца", playerOrbitVisual.RingTint,
                    playerOrbitVisual.SetRingTint);
                AddHint($"Gameplay radius остаётся {playerOrbitVisual.CurrentOrbitRadius:0.###}; меняется только нарисованное кольцо.");
            }
            else AddHint("Production weapon orbit source is not available yet.");
            AddVisualSectionReset("PLAYER RING", () =>
                ResetVisualSectionToProduction(VisualSection.PlayerRing));
        }

        if (AddVisualSectionHeader(
                VisualSection.PostFx,
                "CAMERA / SCREEN FX",
                "URP global default VolumeProfile"))
        {
            ProductionVisualTuningController tuning = productionVisualTuning;
            bool available = tuning != null && tuning.VignetteAvailable;
            if (available)
            {
                AddSliderRow(
                    "Vignette Intensity", tuning.VignetteIntensity,
                    0f, 1f, tuning.SetVignetteIntensity, "0.00",
                    tuning.ProductionVignetteIntensity);
                AddVisualSectionReset("CAMERA / SCREEN FX", () =>
                    ResetVisualSectionToProduction(VisualSection.PostFx));
            }
            else
            {
                AddHint("Production Vignette was not found in the active profile.");
            }
        }

        if (AddVisualSectionHeader(
                VisualSection.Atmosphere,
                "ATMOSPHERE",
                "Production anomaly focus overlay"))
        {
            if (anomalyController != null)
            {
                AddToggleRow(
                    "ANOMALY FOCUS",
                    anomalyController.AnomalyFocusEnabled,
                    true,
                    () => anomalyController.SetAnomalyFocusEnabled(
                        !anomalyController.AnomalyFocusEnabled));
                AddSliderRow(
                    "Outside Darkness", anomalyController.OutsideDarkness,
                    0f, 1f, anomalyController.SetOutsideDarkness, "0.00",
                    visualProductionLoaded
                        ? visualProductionSnapshot.OutsideDarkness : null);
                AddSliderRow(
                    "Outside Color", anomalyController.OutsideColor,
                    0f, 1f, anomalyController.SetOutsideColor, "0.00",
                    visualProductionLoaded
                        ? visualProductionSnapshot.OutsideColor : null);
                AddSliderRow(
                    "Focus Transition", anomalyController.FocusTransition,
                    0.2f, 0.35f, anomalyController.SetFocusTransition, "0.000",
                    visualProductionLoaded
                        ? visualProductionSnapshot.FocusTransition : null);
            }
            else
            {
                AddHint("LevelAnomalyController not found in this scene.");
            }

            if (worldRuleVisual != null)
            {
                AddSliderRow(
                    "Wind Dust Amount",
                    worldRuleVisual.WindDustAmountMultiplier,
                    0f, 5f,
                    worldRuleVisual.SetWindDustAmountMultiplier,
                    "0.00", visualProductionLoaded
                        ? visualProductionSnapshot.WindDustAmount : 1f);
                AddHint(
                    "Tunes production WindDustParticles; visible while the " +
                    "Wind world rule is active. 1.00 = production.");
            }

            AddVisualSectionReset(
                "ATMOSPHERE",
                () => ResetVisualSectionToProduction(VisualSection.Atmosphere));
        }

        if (AddVisualSectionHeader(
                VisualSection.Anomalies,
                "ANOMALIES",
                "Registered production site renderers"))
        {
            AddSliderRow(
                "Global Accent", debug.AnomalyAccent,
                1f, 1.75f, debug.SetAnomalyAccent, "0.00",
                debug.ProductionAnomalyAccent);
            AddAnomalyTargetPanel(debug);
            AddAnomalyVisualTuner(debug);
            AddVisualSectionReset("ANOMALIES", () =>
                ResetVisualSectionToProduction(VisualSection.Anomalies));
        }

        if (AddVisualSectionHeader(
                VisualSection.Projectiles,
                "PROJECTILES",
                "Authored bullet visual child and TrailRenderer"))
        {
            ProductionVisualTuningController tuning = productionVisualTuning;
            if (tuning != null)
            {
                AddSliderRow(
                    "Projectile Visual Scale", tuning.ProjectileVisualScale,
                    0.1f, 4f, tuning.SetProjectileVisualScale, "0.00",
                    tuning.ProductionProjectileVisualScale);
                AddSliderRow(
                    "Trail Width", tuning.TrailWidth,
                    0.1f, 5f, tuning.SetTrailWidth, "0.00",
                    tuning.ProductionTrailWidth);
                AddSliderRow(
                    "Trail Time / Length", tuning.TrailTime,
                    0.1f, 6f, tuning.SetTrailTime, "0.00",
                    tuning.ProductionTrailTime);
                AddSliderRow(
                    "Trail Alpha", tuning.TrailAlpha,
                    0f, 3f, tuning.SetTrailAlpha, "0.00",
                    tuning.ProductionTrailAlpha);
                AddSliderRow(
                    "Яркость следа", tuning.TrailBrightness,
                    0f, 6f, tuning.SetTrailBrightness, "0.00",
                    tuning.ProductionTrailBrightness);
                AddSliderRow(
                    "Толщина ядра лазера", tuning.LaserCoreWidth,
                    .1f, 8f, tuning.SetLaserCoreWidth, "0.00",
                    tuning.ProductionLaserCoreWidth);
                AddSliderRow(
                    "Толщина свечения лазера", tuning.LaserGlowWidth,
                    .1f, 8f, tuning.SetLaserGlowWidth, "0.00",
                    tuning.ProductionLaserGlowWidth);
                AddSliderRow(
                    "Яркость лазера", tuning.LaserBrightness,
                    0f, 6f, tuning.SetLaserBrightness, "0.00",
                    tuning.ProductionLaserBrightness);
                AddHint(
                    "Multipliers: 1.00 = production. Visual child scale does " +
                    "not change the root collider.");
                AddVisualSectionReset(
                    "PROJECTILES", () => ResetVisualSectionToProduction(
                        VisualSection.Projectiles));
            }
            else
            {
                AddHint("Production projectile tuner could not be created.");
            }
        }

        if (AddVisualSectionHeader(
                VisualSection.HitFx, "HIT / COMBAT FX",
                "Существующие FX настраиваются в FEEL без дублирования"))
        {
            AddHint("Impact particles, hit flash, death FX и damage popup уже имеют live-параметры в COMBAT FEEL LAB. Здесь они не продублированы, чтобы сохранить один runtime source of truth.");
        }

        if (AddVisualSectionHeader(
                VisualSection.Camera,
                "CAMERA ZOOM",
                "Production CameraFollow orthographic source"))
        {
            if (cameraFollow != null && cameraFollow.OrthographicZoomAvailable)
            {
                float productionSize = cameraFollow.ProductionOrthographicSize;
                AddSliderRow(
                    "Camera Zoom / Orthographic Size",
                    cameraFollow.DebugOrthographicSize,
                    2f, 16f,
                    cameraFollow.SetDebugOrthographicSize,
                    "0.00", visualProductionLoaded
                        ? visualProductionSnapshot.CameraOrthographicSize
                        : cameraFollow.ProductionOrthographicSize);
                AddRow(
                    "MVP DEFAULT", productionSize.ToString("0.00"),
                    successColor, "APPLY", true,
                    () =>
                    {
                        cameraFollow.SetDebugOrthographicSize(productionSize);
                        RefreshCurrentTab();
                    });
                AddRow(
                    "SANDBOX-LIKE", "12.50", successColor, "APPLY", true,
                    () =>
                    {
                        cameraFollow.SetDebugOrthographicSize(12.5f);
                        RefreshCurrentTab();
                    });
                AddHint(
                    "Sandbox value 12.5 comes from the removed " +
                    "GameplaySandboxBootstrap.CreateCamera().");
                AddVisualSectionReset(
                    "CAMERA", () =>
                        ResetVisualSectionToProduction(VisualSection.Camera));
            }
            else
            {
                AddHint("Production orthographic CameraFollow was not found.");
            }
        }
    }

    private static bool IsRuntimeLabTab(DebugTab tab) =>
        tab == DebugTab.VisualTest || tab == DebugTab.FeelTest ||
        tab == DebugTab.OrbitalProduction;

    private void AddOrbitalProductionSection()
    {
        ResolveSceneReferences();
        OrbitalStationRuntime station =
            FindFirstObjectByType<OrbitalStationRuntime>();
        bool available = station != null && station.IsInitialized;
        OrbitalRunState state = available ? station.State : null;
        int ringId = available && station.SelectedRing != null ? station.SelectedRing.RingId : 0;
        bool canEdit = available && (upgradeManager == null || upgradeManager.IsRewardQueueIdle);
        AddSectionTitle("ORBITAL",
            "Production runtime checks");

        CharacterSpawner spawner = FindFirstObjectByType<CharacterSpawner>();
        bool canStart = spawner != null &&
            (RunStateManager.Instance == null || RunStateManager.Instance.OrbitalStationState == null);
        AddRow("Direct scene launch", canStart ? "EXPLICIT DEV NEW RUN" : "RUN EXISTS",
            warningColor, "START", canStart, () =>
            {
                spawner.DebugStartDefaultRunIfMissing();
                RefreshCurrentTab();
            });

        AddSectionTitle("STATION", available
            ? "Arena placement: click ring, then a free mount"
            : "Runtime station is not ready");
        AddRow("Runtime", available
                ? $"CORE {station.Core.Level} / RINGS {station.Rings.Count} / MODULES {station.Modules.Count}"
                : "NOT ACTIVE",
            available ? successColor : mutedColor, string.Empty, false, null);
        bool canLoadGrowth = available &&
            (UpgradeManager.Instance == null || UpgradeManager.Instance.IsRewardQueueIdle);
        AddSectionTitle("GROWTH TEST", "Explicit dev reset / production state + restore");
        AddRow("BEGINNING", "1 RING / 1 MOUNT / CAPACITY 3 / 1 PISTOL", accentColor,
            "LOAD", canLoadGrowth, () => LoadOrbitalGrowthPreset(station, OrbitalStationRuntime.GrowthPreset.Beginning));
        AddRow("MID", "4 RINGS / 12 MOUNTS / 8 MODULES", accentColor,
            "LOAD", canLoadGrowth, () => LoadOrbitalGrowthPreset(station, OrbitalStationRuntime.GrowthPreset.Mid));
        AddRow("FINAL", "8 RINGS / 24 MOUNTS / 16 MODULES / 2 LINK PAIRS", accentColor,
            "LOAD", canLoadGrowth, () => LoadOrbitalGrowthPreset(station, OrbitalStationRuntime.GrowthPreset.Final));
        AddRow("BEGINNING → MID → FINAL", $"NEXT: {nextOrbitalGrowthPreset.ToString().ToUpperInvariant()}",
            accentColor, "NEXT", canLoadGrowth, () => LoadOrbitalGrowthPreset(station, nextOrbitalGrowthPreset));
        AddRow("Placement", available ? station.PlacementStatus : "NOT ACTIVE",
            available && station.PlacementStatus != "READY" ? warningColor : mutedColor,
            string.Empty, false, null);
        AddRow("New Ring (debug beyond cap)", available ? "SELECTS NEW RING" : "NOT ACTIVE",
            accentColor, "+RING", canEdit, () =>
            {
                station.DebugAddRingBeyondCap();
                RefreshCurrentTab();
            });
        AddOrbitalModuleRow("Add Pistol", OrbitalModuleKind.Pistol, station);
        AddOrbitalModuleRow("Add Laser Sword", OrbitalModuleKind.LaserSword, station);
        AddOrbitalModuleRow("Add Impulse Gun", OrbitalModuleKind.ImpulseGun, station);
        AddOrbitalModuleRow("Add Arc Emitter", OrbitalModuleKind.ArcEmitter, station);
        AddOrbitalModuleRow("Add Link Node", OrbitalModuleKind.LinkNode, station);
        AddRow("Selected Ring Speed", available ? "×1.25" : "NOT ACTIVE",
            accentColor, "UP", canEdit && state.CanUpgradeRingSpeed(ringId, out _), () =>
            {
                station.UpgradeSelectedRingSpeed();
                RefreshCurrentTab();
            });
        AddRow("Selected Ring Damage", available ? "×1.25" : "NOT ACTIVE",
            accentColor, "UP", canEdit && state.CanUpgradeRingPower(ringId, out _), () =>
            {
                station.UpgradeSelectedRingPower();
                RefreshCurrentTab();
            });
        var selectedState = state?.Rings.FirstOrDefault(ring => ring.StableRingId == ringId);
        AddRow("Selected ring mounts / capacity", selectedState != null
                ? $"R{ringId}: {selectedState.MountCount} / {selectedState.MountCapacity}" : "SELECT A RING",
            mutedColor, null, false, null);
        AddRow("New Mount Point", available ? "+1 ON SELECTED RING" : "NOT ACTIVE",
            accentColor, "+MOUNT", canEdit && state.CanAddMount(ringId, out _), () =>
            {
                station.AddMount();
                RefreshCurrentTab();
            });
        AddRow("Ring Capacity", available ? "+1 LIMIT / BUILD MOUNTS SEPARATELY" : "NOT ACTIVE",
            accentColor, "+CAPACITY", canEdit && state.CanUpgradeRingCapacity(ringId, out _), () =>
            {
                station.UpgradeRingCapacity(ringId);
                RefreshCurrentTab();
            });
        AddRow("Core I / II / III", available ? $"CORE {ToRomanLevel(station.Core.Level)}" : "NOT ACTIVE",
            accentColor, "UP", canEdit && state.CanUpgradeCore(out _), () =>
            {
                station.UpgradeCore();
                RefreshCurrentTab();
            });

        RunStateManager manager = RunStateManager.Instance;
        OrbitalRunState runState = manager != null
            ? manager.OrbitalStationState
            : null;
        if (runState != null && !runState.Validate(out string stateError))
        {
            AddHint($"ORBITAL state invalid: {stateError}. Restore does not repair or reset it.");
            return;
        }
        int mounts = runState != null
            ? runState.Rings.Sum(value => value.MountCount)
            : 0;
        AddSectionTitle("RUN STATE", "Data owned by the current RunStateManager");
        AddRow("Initialized", runState?.IsInitialized == true ? "YES" : "NO",
            runState?.IsInitialized == true ? successColor : mutedColor,
            string.Empty, false, null);
        AddRow("Revision", runState?.Revision.ToString() ?? "-", mutedColor,
            string.Empty, false, null);
        AddRow("Core / Rings", runState != null
                ? $"{runState.CoreState.Level} / {runState.Rings.Count}"
                : "-",
            mutedColor, string.Empty, false, null);
        AddRow("Mounts / Modules", runState != null
                ? $"{mounts} / {runState.Modules.Count}"
                : "-",
            mutedColor, string.Empty, false, null);
        AddRow("Sector / Restores", runState != null
                ? $"{manager.CurrentSector?.SectorNumber ?? manager.CurrentLevel} / {runState.RestoreCount}"
                : "-",
            mutedColor, string.Empty, false, null);
        AddRow("CAPTURE STATE", lastOrbitalStateResult ?? "PHASE IS LIVE-SYNCED",
            mutedColor, "CAPTURE", available, () =>
            {
                OrbitalRunState captured = station.CaptureState();
                lastOrbitalStateResult = captured != null
                    ? $"REV {captured.Revision}"
                    : "NO STATE";
                RefreshCurrentTab();
            });
        AddRow("REBUILD RUNTIME FROM STATE", "NO REWARD FX", accentColor,
            "REBUILD", canEdit, () =>
            {
                lastOrbitalStateResult = station.RebuildRuntimeFromState()
                    ? "RESTORED"
                    : "FAILED";
                RefreshCurrentTab();
            });
        AddRow("VALIDATE STATE", lastOrbitalStateResult ?? "READY", mutedColor,
            "VALIDATE", runState != null, () =>
            {
                bool valid = runState.Validate(out string error);
                lastOrbitalStateResult = valid ? "VALID" : error;
                RefreshCurrentTab();
            });
        AddRow("PRINT COMPACT STATE", "ONE STRUCTURED LOG", mutedColor,
            "PRINT", runState != null, () =>
            {
                int sector = manager.CurrentSector?.SectorNumber ?? manager.CurrentLevel;
                string compact = runState.ToCompactString(sector);
                lastOrbitalStateResult = "PRINTED";
                Debug.Log(compact, this);
                RefreshCurrentTab();
            });
        AddRow("SIMULATE SECTOR RESTORE", "DESTROY/RESTORE PRESENTATION",
            warningColor, "SIM", canEdit, () =>
            {
                lastOrbitalStateResult = station.SimulateSectorRestore()
                    ? "RESTORED"
                    : "FAILED";
                RefreshCurrentTab();
            });

        ExperienceManager experience = ExperienceManager.Instance;
        UpgradeManager rewards = UpgradeManager.Instance;
        int playerLevel = experience != null ? experience.CurrentLevel : 1;
        int freeMounts = runState != null
            ? mounts - runState.Modules.Count
            : 0;
        string rewardStatus = station?.RewardFlow != null
            ? station.RewardFlow.CompactStatus
            : "NO FLOW";
        AddSectionTitle("REWARD FLOW", "Production level-up provider and arena targeting");
        AddRow("Player / Ring offer", $"LV {playerLevel} / " +
            $"{OrbitalProgressionConfig.Default.GetRingOfferChance(runState?.RingOfferMissCount ?? 0):P0}",
            mutedColor, string.Empty, false, null);
        AddRow("Free Mounts", freeMounts.ToString(), mutedColor,
            string.Empty, false, null);
        AddRow("Pending Reward", rewardStatus,
            rewardStatus == "CardSelection" ? mutedColor : warningColor,
            string.Empty, false, null);
        AddRow("Show Reward Eligibility",
            rewards?.GetOrbitalEligibilitySummary() ?? "NO PROVIDER",
            mutedColor, "REFRESH", rewards != null, RefreshCurrentTab);
        AddRow("Force Level Up", "REAL EXPERIENCE FLOW", accentColor,
            "LEVEL", available && experience != null && rewards != null && rewards.IsRewardQueueIdle, () =>
            {
                CloseMenu();
                experience.AddExperience(experience.ExpToNextLevel);
            });
        AddOrbitalRewardDebugRow("Force Module Reward",
            OrbitalRewardKind.Pistol, rewards);
        AddOrbitalRewardDebugRow("Force Link Pair",
            OrbitalRewardKind.LinkPair, rewards);
        AddOrbitalRewardDebugRow("Force Ring Speed",
            OrbitalRewardKind.RingSpeed, rewards);
        AddOrbitalRewardDebugRow("Force Ring Damage",
            OrbitalRewardKind.RingPower, rewards);
        AddOrbitalRewardDebugRow("Force New Mount Point",
            OrbitalRewardKind.AddMount, rewards);
        AddOrbitalRewardDebugRow("Force Core I / II / III",
            OrbitalRewardKind.CoreUpgrade, rewards);
        AddOrbitalRewardDebugRow("Force Ring Capacity",
            OrbitalRewardKind.RingCapacity, rewards);
        AddOrbitalRewardDebugRow("Force New Ring", OrbitalRewardKind.NewRing, rewards);
        AddRow("Reset Orbital Progression", "BASE STATION / REWARD IDLE",
            warningColor, "RESET", available &&
                (rewards == null || rewards.IsRewardQueueIdle), () =>
            {
                station.ApplyPresetStart();
                RefreshCurrentTab();
            });

        AddSectionTitle("PRESET", "Single temporary visual QA state");
        AddRow("READABILITY TEST", "3 RINGS / ALL MODULE VISUALS / FREE MOUNTS",
            accentColor, "LOAD", canEdit,
            () => { station.ApplyReadabilityTestPreset(); RefreshCurrentTab(); });
        OrbitalPresentationConfig visual = OrbitalPresentationConfig.Active;
        System.Action<float> refreshVisuals = _ =>
        {
            station?.RebuildRuntimeFromState();
            RefreshCurrentTab();
        };
        AddSectionTitle("READABILITY SETTINGS", "Production config · visual only");
        AddSliderRow("Pistol Visual Scale", visual.PistolVisualScale, 0.5f, 2f,
            value => { visual.PistolVisualScale = value; refreshVisuals(value); }, "0.00");
        AddSliderRow("Laser Sword Visual Scale", visual.LaserSwordVisualScale, 0.5f, 2f,
            value => { visual.LaserSwordVisualScale = value; refreshVisuals(value); }, "0.00");
        AddSliderRow("Impulse Visual Scale", visual.ImpulseVisualScale, 0.5f, 2f,
            value => { visual.ImpulseVisualScale = value; refreshVisuals(value); }, "0.00");
        AddSliderRow("Arc Visual Scale", visual.ArcVisualScale, 0.15f, 0.8f,
            value => { visual.ArcVisualScale = value; refreshVisuals(value); }, "0.00");
        AddSliderRow("Link Node Visual Scale", visual.LinkNodeVisualScale, 0.15f, 0.8f,
            value => { visual.LinkNodeVisualScale = value; refreshVisuals(value); }, "0.00");
        AddSliderRow("Mounted Sorting Offset", visual.MountedWeaponSortingOffset, 0f, 8f,
            value => { visual.MountedWeaponSortingOffset = Mathf.RoundToInt(value); refreshVisuals(value); }, "0");
        AddSliderRow("Normal Mount Size", visual.NormalMountSize, 0.08f, 0.35f,
            value => { visual.NormalMountSize = value; refreshVisuals(value); }, "0.00");
        AddSliderRow("Selection Mount Size", visual.SelectionMountSize, 0.15f, 0.5f,
            value => { visual.SelectionMountSize = value; refreshVisuals(value); }, "0.00");
        AddSliderRow("Normal Alpha", visual.NormalAlpha, 0.2f, 1f,
            value => { visual.NormalAlpha = value; refreshVisuals(value); }, "0.00");
        AddSliderRow("Hover Alpha", visual.HoverAlpha, 0.2f, 1f,
            value => { visual.HoverAlpha = value; refreshVisuals(value); }, "0.00");
        AddSliderRow("Halo Size", visual.HaloSize, 0.15f, 0.7f,
            value => { visual.HaloSize = value; refreshVisuals(value); }, "0.00");
        AddSliderRow("Ring Line Alpha", visual.RingLineAlpha, 0.15f, 0.9f,
            value => { visual.RingLineAlpha = value; refreshVisuals(value); }, "0.00");
        AddSectionTitle("WORLD TELEKINESIS", "XP pickups and ordinary breakables");
        AddSliderRow("Throw Strength", visual.TelekinesisThrowStrength,
            0f, 2.5f, value => visual.TelekinesisThrowStrength = value, "0.00");
        AddSliderRow("Max Throw Speed", visual.TelekinesisMaxThrowSpeed,
            2f, 30f, value => visual.TelekinesisMaxThrowSpeed = value, "0.0");
        AddSliderRow("Throw Drag", visual.TelekinesisThrowDrag,
            0f, 10f, value => visual.TelekinesisThrowDrag = value, "0.0");
        AddHint("Number keys 1-9 select a ring. Module placement closes F1 so arena clicks remain explicit.");
    }

    private void LoadOrbitalGrowthPreset(OrbitalStationRuntime station,
        OrbitalStationRuntime.GrowthPreset preset)
    {
        if (station.DebugApplyGrowthPreset(preset))
        {
            lastOrbitalStateResult = $"GROWTH {preset.ToString().ToUpperInvariant()}";
            nextOrbitalGrowthPreset = (OrbitalStationRuntime.GrowthPreset)(((int)preset + 1) % 3);
            CloseMenu();
        }
        else
            lastOrbitalStateResult = "GROWTH RESET UNAVAILABLE / WAIT FOR REWARD IDLE";
        RefreshCurrentTab();
    }

    private void AddOrbitalModuleRow(string label, OrbitalModuleKind kind,
        OrbitalStationRuntime station)
    {
        bool available = station != null && station.IsInitialized &&
            station.InputOwner != null && station.InputOwner.CanQueueDebugPlacement &&
            station.State.FreeBuiltMounts > 0 &&
            (UpgradeManager.Instance == null || UpgradeManager.Instance.IsRewardQueueIdle);
        AddRow(label, available ? "ARENA PLACEMENT" : "NOT ACTIVE",
            available ? accentColor : mutedColor, "PLACE", available, () =>
            {
                station.BeginModulePlacement(kind);
                CloseMenu();
            });
    }

    private void AddOrbitalRewardDebugRow(string label,
        OrbitalRewardKind kind, UpgradeManager rewards)
    {
        bool enabled;
        using (var provider = new OrbitalRewardProvider(rewards != null ? rewards.AllUpgrades.ToArray() : null))
            enabled = rewards != null && rewards.IsRewardQueueIdle && provider.IsEligible(kind);
        AddRow(label, kind.ToString().ToUpperInvariant(), accentColor,
            "FORCE", enabled, () =>
            {
                CloseMenu();
                if (rewards.DebugForceOrbitalReward(kind))
                    return;
                else
                {
                    lastOrbitalStateResult = "REWARD INELIGIBLE";
                    Debug.LogWarning($"[OrbitalRewards] Forced reward {kind} is ineligible.");
                }
            });
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

        AnomalyVisualTuningCapabilities capabilities =
            target.VisualTunerCapabilities;

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

        AnomalyVisualTuningCapabilities boundaryCapabilities =
            AnomalyVisualTuningCapabilities.BoundaryWidth |
            AnomalyVisualTuningCapabilities.BoundaryAlpha |
            AnomalyVisualTuningCapabilities.InnerLineWidth |
            AnomalyVisualTuningCapabilities.VisualScale;
        if ((capabilities & boundaryCapabilities) != 0)
        {
            AddSectionTitle(
                "BOUNDARY",
                "Presentation only; collider is unchanged"
            );
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
            debug, "Boundary Alpha",
            AnomalyVisualTuningCapabilities.BoundaryAlpha,
            0.02f, 0.1f, 0f, 1f,
            values => values.BoundaryAlpha,
            (values, value) =>
            {
                values.BoundaryAlpha = value;
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
        }

        AnomalyVisualTuningCapabilities colorCapabilities =
            AnomalyVisualTuningCapabilities.PrimaryColor |
            AnomalyVisualTuningCapabilities.SecondaryColor |
            AnomalyVisualTuningCapabilities.FillColor |
            AnomalyVisualTuningCapabilities.FillAlpha;
        if ((capabilities & colorCapabilities) != 0)
        {
            AddSectionTitle(
                "COLORS",
                "Runtime instance RGBA; assets are untouched"
            );
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
        }

        AnomalyVisualTuningCapabilities patternCapabilities =
            AnomalyVisualTuningCapabilities.EdgeGlow |
            AnomalyVisualTuningCapabilities.PulseSpeed |
            AnomalyVisualTuningCapabilities.PulseStrength |
            AnomalyVisualTuningCapabilities.PatternSpeed |
            AnomalyVisualTuningCapabilities.PatternStrength;
        if ((capabilities & patternCapabilities) != 0)
        {
            AddSectionTitle(
                "PATTERN / FX",
                "Only supported renderer properties"
            );
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
        AddVisualTunerFloat(
            debug, "Pattern Strength",
            AnomalyVisualTuningCapabilities.PatternStrength,
            0.02f, 0.1f, 0f, 1f,
            values => values.PatternStrength,
            (values, value) =>
            {
                values.PatternStrength = value;
                return values;
            }
        );
        }

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
            "COPY TO CLIPBOARD",
            "Только буфер обмена; production и saved asset не меняются",
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
        debug.RefreshVisualTunerTargetCache();
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

        RectTransform typeRow = CreateRect("Anomaly Type Selector", contentRoot);
        typeRow.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
        string[] typeLabels = { "GRAVITY", "ARC", "BEAM" };
        for (int i = 0; i < typeLabels.Length; i++)
        {
            int index = i;
            bool supported = typeLabels[i] != "GRAVITY";
            Button button = CreateButton(typeRow, typeLabels[i], () =>
            {
                debug.SelectVisualTunerTargetByType(typeLabels[index]);
                RefreshCurrentTab();
            }, 90f, supported);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(index / 3f, 0f);
            rect.anchorMax = new Vector2((index + 1) / 3f, 1f);
            rect.offsetMin = new Vector2(2f, 3f);
            rect.offsetMax = new Vector2(-2f, -3f);
            if (!supported)
                AttachFeelTooltip(button.gameObject, () =>
                    "Gravity site пока не предоставляет безопасный visual-only tuning contract; gameplay radius не изменяется.");
        }

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
        bool compact = activeTab == DebugTab.VisualTest;
        row.gameObject.AddComponent<LayoutElement>().preferredHeight =
            compact ? 30f : 54f;

        bool canCycle = debug.VisualTunerTargetCount > 1;
        string targetLabel = debug.VisualTunerCapabilities !=
            AnomalyVisualTuningCapabilities.None
                ? debug.VisualTunerTypeName
                : debug.VisualTunerTargetName
                    .Replace("NORMAL ", string.Empty)
                    .Replace("SPECIAL ", string.Empty);
        Button previous = CreateButton(
            row, "PREV", () => SelectVisualTunerTarget(debug, false),
            104f, canCycle);
        RectTransform previousRect = previous.GetComponent<RectTransform>();
        previousRect.anchorMin = previousRect.anchorMax =
            new Vector2(0f, 0.5f);
        previousRect.pivot = new Vector2(0f, 0.5f);
        previousRect.anchoredPosition = new Vector2(compact ? 4f : 12f, 0f);
        previousRect.sizeDelta = compact
            ? new Vector2(48f, 22f)
            : new Vector2(104f, 38f);

        TextMeshProUGUI target = CreateText(
            "Target", row,
            $"{targetLabel}  " +
            $"({debug.VisualTunerTargetIndex + 1}/" +
            $"{debug.VisualTunerTargetCount})",
            compact ? 9f : 17f,
            TextAlignmentOptions.Center,
            successColor);
        Stretch(target.rectTransform,
            compact ? 56f : 126f,
            compact ? 56f : 126f);
        target.overflowMode = TextOverflowModes.Ellipsis;

        Button next = CreateButton(
            row, "NEXT", () => SelectVisualTunerTarget(debug, true),
            104f, canCycle);
        RectTransform nextRect = next.GetComponent<RectTransform>();
        nextRect.anchorMin = nextRect.anchorMax = new Vector2(1f, 0.5f);
        nextRect.pivot = new Vector2(1f, 0.5f);
        nextRect.anchoredPosition = new Vector2(compact ? -4f : -12f, 0f);
        nextRect.sizeDelta = compact
            ? new Vector2(48f, 22f)
            : new Vector2(104f, 38f);
        if (compact)
        {
            TextMeshProUGUI previousText =
                previous.GetComponentInChildren<TextMeshProUGUI>();
            TextMeshProUGUI nextText =
                next.GetComponentInChildren<TextMeshProUGUI>();
            if (previousText != null)
                previousText.fontSize = 9f;
            if (nextText != null)
                nextText.fontSize = 9f;
        }
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
        AddSliderRow(
            label,
            current,
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
        AddSliderRow(
            label,
            current,
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

    private void RefreshSectorVisualTargets()
    {
        productionSectorDebug?.RefreshVisualTargets();
        RefreshCurrentTab();
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

    private void AddSectorFlowSection()
    {
        EnsureProductionSectorDebug();
        var debug = productionSectorDebug;
        bool available = RunStateManager.Instance?.CurrentSector != null;
        AddSectionTitle("SECTOR", "Three-sector production route; transitions use the sector choice above");
        AddRow("Reload current sector", "PRESERVES RUN STATE", warningColor, "RELOAD", available, () =>
        {
            CloseMenu();
            debug.RebuildCurrentSector();
        });
        AddSectionTitle("SPECIAL SITE", "Applied on the next sector reload");
        foreach (ProductionSectorDebugController.SpecialOverride value in
                 Enum.GetValues(typeof(ProductionSectorDebugController.SpecialOverride)))
            AddProductionSpecialOverride(value);
        AddSectionTitle("THREAT", "Current production pressure / tier");
        AddRow("Pressure", $"{debug.InternalPressure:0.0} / {ThreatTierPresentation.Format(debug.CurrentThreatTier)}",
            mutedColor, null, false, null);
        foreach (ThreatTier tier in Enum.GetValues(typeof(ThreatTier)))
        {
            ThreatTier captured = tier;
            AddRow(ThreatTierPresentation.Format(tier), "SET PRESSURE", mutedColor,
                "SET", available, () => SetDebugThreatTier(captured));
        }
    }

    private int qaSection;

    private void SelectQaSection(int section)
    {
        qaSection = section;
        RefreshCurrentTab();
    }

    private void AddQaSection()
    {
        EnsureProductionSectorDebug();
        AddSectionTitle("PLAYER", "Current runtime only");
        AddToggleRow("INVULNERABILITY", productionSectorDebug.InvulnerabilityEnabled,
            characterSpawner != null && characterSpawner.SpawnedPlayer != null,
            () => productionSectorDebug.SetInvulnerability(!productionSectorDebug.InvulnerabilityEnabled));
        string[] sections = { "ENEMIES", "XP", "BOT LAB" };
        for (int i = 0; i < sections.Length; i++)
        {
            int captured = i;
            AddOptionRow(sections[i], qaSection == i, true, () => SelectQaSection(captured));
        }
        switch (qaSection)
        {
            case 0: AddEnemiesSection(); break;
            case 1: AddExperienceVisualLabSection(); break;
            case 2: AddBotLabSection(); break;
        }
    }

    private void AddRunSection()
    {
        ResolveSceneReferences();
        var rmbStation = FindFirstObjectByType<OrbitalStationRuntime>();
        AddSectionTitle("RMB MODE", "ПКМ: удержание Compress / нажатие Repulse и Reverse");
        string[] rmbLabels = { "Compress Rings", "Repulse", "Reverse Rotation" };
        for (int i = 0; i < rmbLabels.Length; i++)
        {
            var mode = (OrbitalStationRuntime.RmbMode)i;
            bool selected = rmbStation != null && rmbStation.RightMouseMode == mode;
            AddRow(rmbLabels[i], selected ? "ACTIVE" : "", selected ? successColor : mutedColor,
                "SELECT", rmbStation != null, () =>
                {
                    if (rmbStation != null) rmbStation.RightMouseMode = mode;
                    RefreshCurrentTab();
                });
        }
        RunStateManager runState = RunStateManager.Instance;
        RunSector sector = runState != null ? runState.CurrentSector : null;
        EnemyHealth boss = FindAliveBoss();
        bool choiceOpen = levelChoiceManager != null &&
            levelChoiceManager.IsChoosing;
        bool completed = runFlowController != null &&
            runFlowController.IsLevelCompleted;

        AddSectionTitle("CURRENT RUN", "Read-only production state");
        AddRow("Current sector",
            sector != null ? sector.SectorNumber.ToString() : "NOT AVAILABLE",
            sector != null ? successColor : warningColor,
            null, false, null);
        AddRow("Run phase", runFlowController != null ? runFlowController.Phase.ToString() : "NONE",
            mutedColor, null, false, null);
        AddRow("Boss alive", boss != null ? "YES" : "NO",
            boss != null ? successColor : mutedColor,
            null, false, null);
        AddRow("Sector choice open", choiceOpen ? "YES" : "NO",
            choiceOpen ? successColor : mutedColor,
            null, false, null);

        AddSectionTitle(
            "SECTOR FLOW",
            $"Production: {RunRoute.TotalSectors} sectors; Sector 3 objective -> boss"
        );
        bool canSpawnBoss = runFlowController != null &&
            RunRoute.IsFinalSector(RunStateManager.Instance?.CurrentLevel ?? 0) &&
            runFlowController.CanDebugCompleteCurrentLevel &&
            boss == null && !completed && !choiceOpen;
        AddRow("Spawn the configured current-sector boss",
            canSpawnBoss ? "READY" : "UNAVAILABLE IN CURRENT STATE",
            canSpawnBoss ? mutedColor : warningColor,
            "SPAWN BOSS", canSpawnBoss, SpawnBoss);

        bool canKillBoss = boss != null && !choiceOpen;
        AddRow("Defeat the live boss through EnemyHealth",
            canKillBoss ? "READY" : "NO LIVE BOSS",
            canKillBoss ? mutedColor : warningColor,
            "KILL BOSS", canKillBoss, KillBoss);

        bool canComplete = runFlowController != null &&
            runFlowController.CanDebugCompleteCurrentLevel;
        AddRow("Complete sector objective / final boss",
            canComplete ? "READY" : completed ? "ALREADY COMPLETED" : "UNAVAILABLE",
            canComplete ? successColor : warningColor,
            "COMPLETE SECTOR", canComplete, CompleteLevel);

        bool canOpenCards = runFlowController != null &&
            runFlowController.CanDebugOpenLevelChoice;
        AddRow("Reopen the completed intermediate sector choice",
            choiceOpen ? "ALREADY OPEN" : canOpenCards ? "READY" :
                "COMPLETE LEVEL FIRST",
            canOpenCards ? mutedColor : warningColor,
            "OPEN SECTOR CHOICE", canOpenCards, OpenLevelCards);

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
        if (FindFirstObjectByType<BunkerRunStarter>() == null)
        {
            AddHint("Bunker tools are available in MainMenu.");
            return;
        }
        AddFootballMinigameSection();
#if UNITY_EDITOR
        UnlockProgressService unlocks = UnlockProgressService.Instance;
        AddSectionTitle("CONTENT UNLOCKS", "Persistent debug actions");
        AddRow("Unlock all content", "PERSISTENT", warningColor, "UNLOCK ALL", unlocks != null,
            () => { unlocks.DebugUnlockAll(); RefreshCurrentTab(); });
        AddRow("Reset content unlocks", "PERSISTENT", warningColor, "RESET UNLOCKS", unlocks != null,
            () => { unlocks.DebugResetAll(); RefreshCurrentTab(); });
#endif

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
        CharacterSelectionUI characterUi = FindFirstObjectByType<CharacterSelectionUI>(FindObjectsInactive.Include);
        if (characterUi == null)
        {
            AddHint("Open the Character Station panel to inspect its selection controls.");
            return;
        }
        AddRow("Character Selection", "AVAILABLE", successColor,
            "REFRESH", true, () => DebugRefreshCharacterUi(characterUi));
        debugCharacters.Clear();
        characterUi.CollectDebugCharacters(debugCharacters);
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
        CloseMenu();
        runFlowController?.TryDebugCompleteCurrentLevel();
        RefreshCurrentTab();
    }

    private void KillBoss()
    {
        CloseMenu();
        EnemyHealth boss = FindAliveBoss();
        boss?.TakeDamage(float.MaxValue, boss.transform.position);
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

    private void OpenLevelCards()
    {
        CloseMenu();
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
        AddManualEnemyRows("БОСС", RunStateManager.Instance?.CurrentSector?.BossPrefab);

        AddRow("Создано вручную",
            debugEnemies.Count > 0 ? $"Активно: {debugEnemies.Count}" : "Нет",
            debugEnemies.Count > 0 ? successColor : mutedColor,
            "УБРАТЬ РУЧНЫХ", debugEnemies.Count > 0,
            ClearDebugEnemies);
    }


    private void AddRoomStateRows()
    {
        if (FindFirstObjectByType<BunkerRunStarter>() == null) return;
        AddSectionTitle("ROOM ACCESS", "Runtime only; values are not saved");
        BunkerRoomAccess[] rooms =
            FindObjectsByType<BunkerRoomAccess>(FindObjectsSortMode.None);

        foreach (BunkerRoomId roomId in Enum.GetValues(typeof(BunkerRoomId)))
        {
            BunkerRoomAccess room = null;
            for (int i = 0; i < rooms.Length; i++)
            {
                if (rooms[i] != null && rooms[i].RoomId == roomId)
                {
                    room = rooms[i];
                    break;
                }
            }

            if (room == null) continue;
            string state = room.Unlocked ? "OPEN" : "CLOSED";
            Color color = room.Unlocked ? successColor : mutedColor;
            BunkerRoomAccess captured = room;
            AddRow(
                GetRoomDisplayName(roomId),
                $"[ {state} ]",
                color,
                room.Unlocked ? "CLOSE" : "OPEN",
                true,
                () =>
                {
                    captured.SetUnlocked(!captured.Unlocked);
                    RefreshCurrentTab();
                });
        }

        AddSectionTitle("ALL ROOMS", "Bulk runtime controls");
        AddRow("OPEN ALL", "Set every room OPEN", successColor,
            "OPEN ALL", rooms.Length > 0, () => SetAllRooms(rooms, true));
        AddRow("CLOSE ALL", "Set every room CLOSED", mutedColor,
            "CLOSE ALL", rooms.Length > 0, () => SetAllRooms(rooms, false));
        AddRow("RESET DEFAULTS", "Read defaultUnlocked from scene", accentColor,
            "RESET DEFAULTS", rooms.Length > 0, () => ResetRoomDefaults(rooms));
    }

    private void AddFootballMinigameSection()
    {
        FootballMinigame football = FindFirstObjectByType<FootballMinigame>(FindObjectsInactive.Include);
        bool available = football != null;
        if (!available) return;
        AddSectionTitle("FOOTBALL", "Original rules / authored arena");
        AddRow("State", available ? football.State.ToString() : "NOT FOUND", mutedColor,
            "START", available && football.CanStart, () => { football.StartGame(); RefreshCurrentTab(); });
        AddRow("Balls", available ? football.ActiveBallCount.ToString() : "-", mutedColor,
            "RESET BALLS", available && football.IsRunning, () => football.ResetBall());
        AddRow("Round", "Return to bunker", mutedColor,
            "CANCEL", available, () => { football.ResetGame(); RefreshCurrentTab(); });
        AddRow("Arena", available ? $"{football.ArenaWidth:0} x {football.ArenaHeight:0}" : "-", mutedColor,
            "GIZMOS", available, () => football.ToggleDebugZones());
    }

    private void SetAllRooms(BunkerRoomAccess[] rooms, bool unlocked)
    {
        for (int i = 0; i < rooms.Length; i++)
            rooms[i]?.SetUnlocked(unlocked);

        RefreshCurrentTab();
    }

    private void ResetRoomDefaults(BunkerRoomAccess[] rooms)
    {
        for (int i = 0; i < rooms.Length; i++)
            rooms[i]?.ResetToDefault();

        RefreshCurrentTab();
    }

    private static string GetRoomDisplayName(BunkerRoomId roomId) => roomId switch
    {
        BunkerRoomId.CharacterSelection => "Character Selection",
        BunkerRoomId.WeaponSelection => "Weapon Selection",
        BunkerRoomId.UpgradeStation => "Upgrade Station",
        BunkerRoomId.AnomalyStation => "Anomaly Station",
        BunkerRoomId.FutureStation => "Future Station",
        BunkerRoomId.SecretRoom => "Secret Room",
        _ => roomId.ToString()
    };

    private EnemyHealth ResolveExperienceLootSource()
    {
        EnemyHealth source = turretEnemyPrefab != null
            ? turretEnemyPrefab.GetComponent<EnemyHealth>() : null;
        if (source != null && source.HasExperienceLoot)
            return source;
        source = eyesEnemyPrefab != null
            ? eyesEnemyPrefab.GetComponent<EnemyHealth>() : null;
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
            ? "Counts are pickups, not XP values. Close F1 to test pickup / telekinesis."
            : "Requires a player and an enemy prefab with production XP loot.");
    }

    private void RefreshActiveXpCounter()
    {
        if (activeXpCounter != null)
            activeXpCounter.text = $"ACTIVE XP: {FindObjectsByType<ExperiencePickup>(FindObjectsSortMode.None).Length}";
    }

    private void SpawnDebugExperience(int count, bool stress = false)
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        EnemyHealth source = ResolveExperienceLootSource();
        if (player == null || source == null)
            return;

        Vector3 center = player.transform.position;
        for (int i = 0; i < count; i++)
        {
            float angle = stress
                ? (i / 5 + UnityEngine.Random.value) * (Mathf.PI * 2f / 50f)
                : UnityEngine.Random.value * Mathf.PI * 2f;
            float radius = stress
                ? 4.5f + i % 5 * 1.25f + UnityEngine.Random.Range(-0.4f, 0.4f)
                : Mathf.Sqrt(UnityEngine.Random.Range(16f, 100f));
            source.SpawnLootPickup(center + new Vector3(
                Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }
        RefreshActiveXpCounter();
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


    private static string GetAnomalyTypeName(LocalAnomalyType type) =>
        type == LocalAnomalyType.ExplosiveZone ? "Explosive" : type.ToString();

    private static string GetWeaponName(WeaponData data) =>
        data == null ? "None" :
            string.IsNullOrWhiteSpace(data.weaponName) ? data.name : data.weaponName;

    private static string GetUpgradeName(UpgradeData data) =>
        data == null ? "None" :
            string.IsNullOrWhiteSpace(data.upgradeName) ? data.name : data.upgradeName;

    private static string GetWorldRuleName(WorldRuleType type, WorldRuleData data) =>
        data != null && !string.IsNullOrWhiteSpace(data.DisplayName)
            ? data.DisplayName
            : type.ToString();

    private bool AddVisualSectionHeader(
        VisualSection section,
        string title,
        string subtitle)
    {
        int index = (int)section;
        bool expanded = visualSectionExpanded[index];
        RectTransform header = CreateRect(title, contentRoot);
        Image image = header.gameObject.AddComponent<Image>();
        image.color = new Color(
            accentColor.r,
            accentColor.g,
            accentColor.b,
            expanded ? 0.28f : 0.16f
        );
        header.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;

        Button button = header.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(() =>
        {
            visualSectionExpanded[index] = !visualSectionExpanded[index];
            RefreshCurrentTab();
        });

        TextMeshProUGUI text = CreateText(
            "Label",
            header,
            $"<b>{(expanded ? "▼" : "▶")} {title}</b>",
            13f,
            TextAlignmentOptions.MidlineLeft,
            Color.white
        );
        text.raycastTarget = false;
        Stretch(text.rectTransform, 9f, 9f, 2f, 2f);
        return expanded;
    }

    private void AddInteractiveFeelLab()
    {
        EnsureProductionSectorDebug();
        ProductionFeelTuningController tuning = productionFeelTuning;
        AddCrowdMovementLab();

        if (tuning == null)
        {
            AddHint("ProductionFeelTuningController was not created.");
            return;
        }

        tuning.Configure();
        AddFeelTestDummySection();
        AddAdvancedCombatFeelLab(tuning);
    }

    private void AddCrowdMovementLab()
    {
        CrowdSteeringValues values = CrowdSteeringRuntime.Values;
        AddSectionTitle("CROWD MOVEMENT",
            "Runtime steering поверх production movement");
        AddHint("● LIVE — gameplay продолжается. PRODUCTION полностью " +
            "отключает слой и не сохраняет экспериментальные значения.");

        CrowdMovementPreset[] presets =
        {
            CrowdMovementPreset.Production, CrowdMovementPreset.Direct,
            CrowdMovementPreset.Spread, CrowdMovementPreset.Swarm,
            CrowdMovementPreset.Orbit, CrowdMovementPreset.Encircle,
            CrowdMovementPreset.Chaotic
        };
        for (int i = 0; i < presets.Length; i++)
        {
            CrowdMovementPreset captured = presets[i];
            AddOptionRow(captured.ToString().ToUpperInvariant(),
                CrowdSteeringRuntime.CurrentPreset == captured, true, () =>
                {
                    CrowdSteeringRuntime.ApplyPreset(captured);
                });
            RectTransform row = contentRoot.GetChild(contentRoot.childCount - 1)
                as RectTransform;
            AttachFeelTooltip(row.gameObject, () =>
                GetCrowdPresetTooltip(captured));
        }

        AddSectionTitle("GLOBAL", "");
        AddToggleRow("Enable Crowd Steering", values.Enabled, true,
            () => CrowdSteeringRuntime.SetEnabled(!CrowdSteeringRuntime.Values.Enabled));
        AttachLastCrowdTooltip("Включает универсальный steering-слой. " +
            "Выключение мгновенно возвращает штатное движение врагов.");
        AddCrowdSlider("Global Strength", values.GlobalStrength, 0f, 4f,
            CrowdSteeringRuntime.SetGlobalStrength,
            "Общий множитель всех steering-модификаторов, кроме прямого " +
            "production-направления. 0 отключает их влияние, 4 даёт экстремальный эффект.");
        AddToggleRow("Debug Draw Steering", CrowdSteeringRuntime.DebugDraw,
            true, () => CrowdSteeringRuntime.SetDebugDraw(!CrowdSteeringRuntime.DebugDraw));
        AttachLastCrowdTooltip("Рисует в Scene view векторы только у 16 ближайших " +
            "к игроку врагов: белый — итог, голубой — production, красный — " +
            "separation, жёлтый — orbit, фиолетовый — персональная target point.");

        AddSectionTitle("CHASE", "");
        AddCrowdSlider("Direct Pressure", values.DirectPressure, 0f, 5f,
            CrowdSteeringRuntime.SetDirectPressure,
            "Вес штатного направления конкретного врага. Для обычного врага это " +
            "движение к игроку, а для Shooter сохраняется его подход или отступление.");

        AddSectionTitle("SEPARATION", "");
        AddCrowdSlider("Separation Radius", values.SeparationRadius, 0f, 12f,
            CrowdSteeringRuntime.SetSeparationRadius,
            "Расстояние, внутри которого враги начинают расталкиваться. " +
            "Большой радиус делает поток шире.");
        AddCrowdSlider("Separation Strength", values.SeparationStrength, 0f, 5f,
            CrowdSteeringRuntime.SetSeparationStrength,
            "Насколько сильно враги расталкиваются друг от друга. Чем выше " +
            "значение, тем меньше плотные комки.");

        AddSectionTitle("COHESION", "");
        AddCrowdSlider("Cohesion Radius", values.CohesionRadius, 0f, 16f,
            CrowdSteeringRuntime.SetCohesionRadius,
            "Радиус поиска соседей, к центру которых мягко тянется враг.");
        AddCrowdSlider("Cohesion Strength", values.CohesionStrength, 0f, 5f,
            CrowdSteeringRuntime.SetCohesionStrength,
            "Насколько сильно соседние враги стараются двигаться одной группой. " +
            "Высокие значения собирают толпу в массу.");

        AddSectionTitle("ORBIT", "");
        AddCrowdSlider("Orbit Strength", values.OrbitStrength, 0f, 5f,
            CrowdSteeringRuntime.SetOrbitStrength,
            "Насколько сильно враги пытаются двигаться вокруг игрока по дуге " +
            "вместо прямого движения к нему.");
        AddCrowdSlider("Direction Bias", values.OrbitDirectionBias, -1f, 1f,
            CrowdSteeringRuntime.SetOrbitDirectionBias,
            "Задаёт общее направление вращения: -1 и +1 закручивают толпу в " +
            "противоположные стороны. При 0 сторона стабильно выбирается для каждого врага.");

        AddSectionTitle("TARGET OFFSET", "");
        AddCrowdSlider("Target Offset Radius", values.TargetOffsetRadius, 0f, 12f,
            CrowdSteeringRuntime.SetTargetOffsetRadius,
            "Насколько далеко от центра игрока разбросаны персональные точки, к " +
            "которым стремятся враги. Помогает толпе окружать игрока.");
        AddCrowdSlider("Target Offset Strength", values.TargetOffsetStrength, 0f, 5f,
            CrowdSteeringRuntime.SetTargetOffsetStrength,
            "Насколько сильно каждый враг стремится к своей стабильной точке вокруг игрока.");

        AddSectionTitle("WANDER", "");
        AddCrowdSlider("Wander Strength", values.WanderStrength, 0f, 5f,
            CrowdSteeringRuntime.SetWanderStrength,
            "Добавляет плавное блуждание направления. Большое значение делает " +
            "движение менее предсказуемым без покадрового jitter.");
        AddCrowdSlider("Wander Frequency", values.WanderFrequency, .02f, 4f,
            CrowdSteeringRuntime.SetWanderFrequency,
            "Скорость плавного изменения wander-направления. Малое значение даёт " +
            "длинные волны, большое — более частые изгибы траектории.");
    }

    private void AddCrowdSlider(string label, float value, float minimum,
        float maximum, Action<float> setter, string tooltip)
    {
        AddSliderRow(label, value, minimum, maximum, setter, "0.00");
        AttachLastCrowdTooltip(tooltip + $"\nDebug range: {minimum:0.##}…{maximum:0.##}.");
    }

    private void AttachLastCrowdTooltip(string tooltip)
    {
        if (contentRoot == null || contentRoot.childCount == 0)
            return;
        GameObject row = contentRoot.GetChild(contentRoot.childCount - 1).gameObject;
        AttachFeelTooltip(row, () => tooltip);
    }

    private static string GetCrowdPresetTooltip(CrowdMovementPreset preset)
    {
        return preset switch
        {
            CrowdMovementPreset.Production => "Точное штатное поведение: steering-слой выключен.",
            CrowdMovementPreset.Direct => "Почти чистое прямое давление с минимальным расталкиванием.",
            CrowdMovementPreset.Spread => "Сильное расталкивание и персональные цели широко распределяют толпу.",
            CrowdMovementPreset.Swarm => "Мягкая связность, separation и wander создают органическую массу.",
            CrowdMovementPreset.Orbit => "Сильная касательная составляющая формирует заметные дуговые потоки.",
            CrowdMovementPreset.Encircle => "Персональные точки и orbit заставляют врагов заходить с разных сторон.",
            CrowdMovementPreset.Chaotic => "Сильный плавный wander, разные стороны orbit и target offsets без jitter.",
            _ => "Пользовательские значения."
        };
    }

    private void AddFeelTestDummySection()
    {
        CombatFeelTestDummyController controller = feelTestDummy;
        AddSectionTitle("FEEL TEST DUMMY", "Debug-owned production enemy");
        // Keep SPAWN actionable so the controller can perform a late scene
        // lookup and report the precise missing dependency in its status/log.
        bool available = controller != null && characterSpawner != null &&
            characterSpawner.SpawnedPlayer != null && controller.CanSpawn(controller.Archetype);
        AddRow("DUMMY", controller != null ? controller.Status : "UNAVAILABLE",
            controller != null && controller.HasDummy ? successColor : mutedColor,
            "SPAWN", available, () =>
            {
                ResolveSceneReferences();
                EnsureProductionSectorDebug();
                feelTestDummy.Spawn();
                RefreshCurrentTab();
            });
        AddRow("DESPAWN", "No wave/progression registration", mutedColor,
            "DESPAWN", controller != null && controller.HasDummy,
            () => { controller.Despawn(); RefreshCurrentTab(); });
        AddRow("RESET POSITION", "Anchor in front of player", mutedColor,
            "RESET", controller != null && controller.HasDummy,
            () => controller.ResetPosition());
        AddRow("SIMULATE KILL", "Death FX + kill feel; no rewards", warningColor,
            "KILL", controller != null && controller.HasDummy,
            () => controller.SimulateHit(2.5f, true, true));

        foreach (CombatFeelDummyArchetype archetype in
                 (CombatFeelDummyArchetype[])System.Enum.GetValues(
                     typeof(CombatFeelDummyArchetype)))
        {
            CombatFeelDummyArchetype captured = archetype;
            AddOptionRow(archetype.ToString().ToUpperInvariant(),
                controller != null && controller.Archetype == archetype,
                controller != null && controller.CanSpawn(archetype),
                () => controller.SetArchetype(captured));
        }

        foreach (CombatFeelDummyMode mode in
                 (CombatFeelDummyMode[])System.Enum.GetValues(
                     typeof(CombatFeelDummyMode)))
        {
            CombatFeelDummyMode captured = mode;
            AddOptionRow(CombatFeelTestDummyController.GetModeLabel(mode),
                controller != null && controller.Mode == mode, available,
                () => controller.SetMode(captured));
        }

        AddToggleRow("INVULNERABLE", controller != null && controller.Invulnerable,
            available, () => controller.SetInvulnerable(!controller.Invulnerable));
        AddToggleRow("FACE PLAYER", controller != null && controller.FacePlayer,
            available, () => controller.SetFacePlayer(!controller.FacePlayer));
        AddToggleRow("ORBIT CLOCKWISE", controller != null && controller.Clockwise,
            available, () => controller.SetClockwise(!controller.Clockwise));
        if (controller == null)
            return;
        AddSliderRow("DISTANCE", controller.Distance, 1f, 12f,
            controller.SetDistance, "0.0");
        AddSliderRow("FOLLOW SPEED", controller.FollowSpeed, .25f, 12f,
            controller.SetFollowSpeed, "0.0");
        AddSliderRow("ORBIT SPEED", controller.OrbitSpeed, 1f, 180f,
            controller.SetOrbitSpeed, "0");
        AddSliderRow("ORBIT RADIUS", controller.OrbitRadius, 1f, 12f,
            controller.SetOrbitRadius, "0.0");

        AddRow("LIGHT HIT", "10% max HP feedback", mutedColor, "PLAY",
            controller.HasDummy, () => controller.SimulateHit(.1f, false, false));
        AddRow("NORMAL HIT", "25% max HP feedback", mutedColor, "PLAY",
            controller.HasDummy, () => controller.SimulateHit(.25f, false, false));
        AddRow("HEAVY HIT", "65% max HP feedback", mutedColor, "PLAY",
            controller.HasDummy, () => controller.SimulateHit(.65f, false, false));
        AddRow("CRIT HIT", "65% max HP critical feedback", mutedColor, "PLAY",
            controller.HasDummy, () => controller.SimulateHit(.65f, true, false));
        AddRow("OVERKILL", "250% max HP kill feedback", warningColor, "PLAY",
            controller.HasDummy, () => controller.SimulateHit(2.5f, true, true));
    }

    // Retained as a compact reference for the previous production-field view.
    // Advanced Combat Feel Lab intentionally uses neutral runtime deltas instead.
    private void AddAdvancedCombatFeelLab(ProductionFeelTuningController tuning)
    {
        CombatFeelLabSettings lab = tuning.Lab;
        AddSectionTitle("COMBAT FEEL LAB",
            "INPUT → ANTICIPATION → SHOT → PROJECTILE → HIT → TARGET → KILL → CROWD");
        AddFeelLiveStatus();
        AddFeelGroupTabs();
        AddHint(CombatFeelParameterMetadata.GetGroupDescriptionRu(selectedFeelLabGroup));
        AddFeelGlobalPresets(lab);
        AddFeelCharacterPresets(lab);
        AddFeelExperimentActions(lab);
        AddFeelGroupControls(lab);

        IReadOnlyList<CombatFeelDescriptor> descriptors =
            CombatFeelLabSettings.Descriptors;
        for (int i = 0; i < descriptors.Count; i++)
        {
            CombatFeelDescriptor descriptor = descriptors[i];
            if (descriptor.Group != selectedFeelLabGroup) continue;
            AddFeelParameterRow(lab, descriptor);
        }
    }

    private void AddFeelGroupTabs()
    {
        CombatFeelGroup[] groups =
        {
            CombatFeelGroup.Global, CombatFeelGroup.Shot,
            CombatFeelGroup.Projectile, CombatFeelGroup.Hit,
            CombatFeelGroup.Target, CombatFeelGroup.Kill,
            CombatFeelGroup.Camera, CombatFeelGroup.Time,
            CombatFeelGroup.Crowd
        };
        for (int rowIndex = 0; rowIndex < 2; rowIndex++)
        {
            RectTransform row = CreateRect("Feel Group Tabs", contentRoot);
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = 29f;
            int start = rowIndex * 5;
            int count = Mathf.Min(5, groups.Length - start);
            for (int i = 0; i < count; i++)
            {
                CombatFeelGroup group = groups[start + i];
                int index = i;
                Button button = CreateButton(row,
                    CombatFeelParameterMetadata.GetGroupShortNameRu(group), () =>
                    {
                        selectedFeelLabGroup = group;
                        RefreshCurrentTab();
                    }, 90f);
                AttachFeelTooltip(button.gameObject, () =>
                    CombatFeelParameterMetadata.GetGroupDescriptionRu(group));
                RectTransform rect = button.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(index / (float)count, 0f);
                rect.anchorMax = new Vector2((index + 1) / (float)count, 1f);
                rect.offsetMin = new Vector2(2f, 2f);
                rect.offsetMax = new Vector2(-2f, -2f);
                if (selectedFeelLabGroup == group &&
                    button.targetGraphic is Image image)
                    image.color = accentColor;
                TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();
                if (text != null) text.fontSize = 9f;
            }
        }
    }

    private void AddFeelCharacterPresets(CombatFeelLabSettings lab)
    {
        RectTransform row = CreateRect("Character Presets", contentRoot);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
        CombatFeelLabSettings.CharacterPreset[] presets =
            (CombatFeelLabSettings.CharacterPreset[])System.Enum.GetValues(
                typeof(CombatFeelLabSettings.CharacterPreset));
        for (int i = 0; i < presets.Length; i++)
        {
            CombatFeelLabSettings.CharacterPreset preset = presets[i];
            int index = i;
            Button button = CreateButton(row, preset.ToString().ToUpperInvariant(), () =>
            {
                lab.ApplyCharacterPreset(preset);
                RefreshCurrentTab();
            }, 90f);
            string label = preset.ToString().ToUpperInvariant();
            AttachFeelTooltip(button.gameObject, () =>
                CombatFeelParameterMetadata.GetPresetDescriptionRu(label));
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(index / 5f, 0f);
            rect.anchorMax = new Vector2((index + 1) / 5f, 1f);
            rect.offsetMin = new Vector2(2f, 2f);
            rect.offsetMax = new Vector2(-2f, -2f);
            TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.fontSize = 9f;
        }
    }

    private void AddFeelGlobalPresets(CombatFeelLabSettings lab)
    {
        string[] labels = { "PRODUCTION", "SOFT", "STRONG" };
        CombatFeelLabSettings.GroupPreset[] presets =
        {
            CombatFeelLabSettings.GroupPreset.Off,
            CombatFeelLabSettings.GroupPreset.Soft,
            CombatFeelLabSettings.GroupPreset.Hard
        };
        RectTransform row = CreateRect("Global Feel Presets", contentRoot);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
        for (int i = 0; i < labels.Length; i++)
        {
            int index = i;
            Button button = CreateButton(row, labels[i], () =>
            {
                lab.ApplyAllGroupsPreset(presets[index]);
                feelSaveMessage = labels[index] + " применён live; не сохранён.";
                RefreshCurrentTab();
            }, 100f);
            AttachFeelTooltip(button.gameObject, () =>
                CombatFeelParameterMetadata.GetPresetDescriptionRu(labels[index]));
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(index / 3f, 0f);
            rect.anchorMax = new Vector2((index + 1) / 3f, 1f);
            rect.offsetMin = new Vector2(2f, 3f);
            rect.offsetMax = new Vector2(-2f, -3f);
        }
    }

    private void AddFeelExperimentActions(CombatFeelLabSettings lab)
    {
        string[] labels = { "SAVE A", "LOAD A", "SAVE B", "LOAD B", "RANDOMIZE", "UNDO" };
        System.Action[] actions =
        {
            lab.SaveA, () => lab.LoadA(), lab.SaveB, () => lab.LoadB(),
            () => lab.Randomize(selectedFeelLabGroup), () => lab.UndoRandomize()
        };
        RectTransform row = CreateRect("A B Randomize", contentRoot);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
        for (int i = 0; i < labels.Length; i++)
        {
            int index = i;
            Button button = CreateButton(row, labels[i], () =>
            {
                actions[index]();
                RefreshCurrentTab();
            }, 76f, i != 1 || lab.HasA);
            string tooltipKey = labels[i];
            AttachFeelTooltip(button.gameObject, () =>
                CombatFeelParameterMetadata.GetPresetDescriptionRu(tooltipKey));
            if (i == 3) button.interactable = lab.HasB;
            if (i == 5) button.interactable = lab.CanUndoRandomize;
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(index / 6f, 0f);
            rect.anchorMax = new Vector2((index + 1) / 6f, 1f);
            rect.offsetMin = new Vector2(2f, 2f);
            rect.offsetMax = new Vector2(-2f, -2f);
            TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null) text.fontSize = 9f;
        }
    }

    private void AddFeelGroupControls(CombatFeelLabSettings lab)
    {
        RectTransform row = CreateRect("Group Presets", contentRoot);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
        CombatFeelLabSettings.GroupPreset[] presets =
            (CombatFeelLabSettings.GroupPreset[])System.Enum.GetValues(
                typeof(CombatFeelLabSettings.GroupPreset));
        for (int i = 0; i < presets.Length; i++)
        {
            CombatFeelLabSettings.GroupPreset preset = presets[i];
            int index = i;
            Button button = CreateButton(row, preset.ToString().ToUpperInvariant(), () =>
            {
                lab.ApplyGroupPreset(selectedFeelLabGroup, preset);
                RefreshCurrentTab();
            }, 70f);
            string label = preset.ToString().ToUpperInvariant();
            AttachFeelTooltip(button.gameObject, () =>
                CombatFeelParameterMetadata.GetPresetDescriptionRu(label));
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(index / 7f, 0f);
            rect.anchorMax = new Vector2((index + 1) / 7f, 1f);
            rect.offsetMin = new Vector2(2f, 2f);
            rect.offsetMax = new Vector2(-2f, -2f);
        }
        Button solo = CreateButton(row,
            lab.SoloGroup == selectedFeelLabGroup ? "UNSOLO" : "SOLO", () =>
            {
                lab.ToggleSolo(selectedFeelLabGroup);
                RefreshCurrentTab();
            }, 70f);
        RectTransform soloRect = solo.GetComponent<RectTransform>();
        AttachFeelTooltip(solo.gameObject, () =>
            CombatFeelParameterMetadata.GetPresetDescriptionRu(
                lab.SoloGroup == selectedFeelLabGroup ? "UNSOLO" : "SOLO"));
        soloRect.anchorMin = new Vector2(5f / 7f, 0f);
        soloRect.anchorMax = new Vector2(6f / 7f, 1f);
        soloRect.offsetMin = new Vector2(2f, 2f);
        soloRect.offsetMax = new Vector2(-2f, -2f);
        Button reset = CreateButton(row, "RESET GROUP", () =>
        {
            lab.ResetGroup(selectedFeelLabGroup);
            RefreshCurrentTab();
        }, 80f);
        AttachFeelTooltip(reset.gameObject, () =>
            CombatFeelParameterMetadata.GetPresetDescriptionRu("RESET GROUP"));
        RectTransform resetRect = reset.GetComponent<RectTransform>();
        resetRect.anchorMin = new Vector2(6f / 7f, 0f);
        resetRect.anchorMax = Vector2.one;
        resetRect.offsetMin = new Vector2(2f, 2f);
        resetRect.offsetMax = new Vector2(-2f, -2f);
    }

    private void AddFeelParameterRow(
        CombatFeelLabSettings lab, CombatFeelDescriptor descriptor)
    {
        string displayName = descriptor.Metadata.GameplayAffecting
            ? "⚠ PHYSICAL  " + descriptor.Name : descriptor.Name;
        if (descriptor.Toggle)
        {
            AddToggleRow(displayName,
                lab.GetRaw(descriptor.Parameter) >= .5f, true, () =>
                {
                    lab.Set(descriptor.Parameter,
                        lab.GetRaw(descriptor.Parameter) >= .5f ? 0f : 1f);
                    RefreshCurrentTab();
                });
        }
        else
        {
            float range = descriptor.Maximum - descriptor.Minimum;
            string format = range <= .5f ? "0.000" : range <= 10f ? "0.00" : "0.0";
            AddSliderRow(displayName, lab.GetRaw(descriptor.Parameter),
                descriptor.Minimum, descriptor.Maximum,
                value =>
                {
                    lab.Set(descriptor.Parameter, value);
                    UpdateFeelSaveStatus();
                }, format,
                descriptor.Metadata.Production,
                value => FormatFeelValue(value, descriptor.Metadata, format));
        }

        RectTransform row = contentRoot.GetChild(contentRoot.childCount - 1)
            as RectTransform;
        if (row != null)
        {
            AttachFeelTooltip(row.gameObject, () =>
                BuildFeelParameterTooltip(lab, descriptor));
            Transform value = row.Find("Value");
            if (value is RectTransform valueRect)
                valueRect.offsetMax = new Vector2(-48f, valueRect.offsetMax.y);
            Button reset = CreateButton(row, "↺", () =>
            {
                bool extreme = Input.GetKey(KeyCode.LeftShift) ||
                    Input.GetKey(KeyCode.RightShift);
                if (extreme) lab.Set(descriptor.Parameter,
                    descriptor.Metadata.DiagnosticExtreme);
                else lab.Reset(descriptor.Parameter);
                RefreshCurrentTab();
            }, 34f, !lab.IsNeutral(descriptor.Parameter));
            RectTransform resetRect = reset.GetComponent<RectTransform>();
            resetRect.anchorMin = resetRect.anchorMax = new Vector2(1f, .5f);
            resetRect.pivot = new Vector2(1f, .5f);
            resetRect.anchoredPosition = new Vector2(-5f, 0f);
            resetRect.sizeDelta = new Vector2(34f, 20f);
            TextMeshProUGUI resetText = reset.GetComponentInChildren<TextMeshProUGUI>();
            if (resetText != null) resetText.fontSize = 12f;
        }
    }

    private static string FormatFeelValue(float value,
        CombatFeelParameterMetadata metadata, string format)
    {
        string current = metadata.FormatValue(value);
        float delta = value - metadata.Production;
        float epsilon = Mathf.Max(.0005f,
            (metadata.Maximum - metadata.Minimum) * .0005f);
        if (Mathf.Abs(delta) <= epsilon) return current + "  P";
        return current + "  Δ" + (delta >= 0f ? "+" : string.Empty) +
            metadata.FormatValue(delta);
    }

    private static string BuildFeelParameterTooltip(CombatFeelLabSettings lab,
        CombatFeelDescriptor descriptor)
    {
        CombatFeelParameterMetadata m = descriptor.Metadata;
        float current = lab.GetRaw(descriptor.Parameter);
        string physical = m.GameplayAffecting
            ? "<color=#FFB040>⚠ PHYSICAL — влияет на gameplay/time/physics.</color>\n"
            : "<color=#68D98B>PRESENTATION ONLY</color>\n";
        return $"<b>{m.RussianName.ToUpperInvariant()}</b>  <color=#7FCFE6>{m.TechnicalName}</color>\n" +
            physical +
            $"<b>Что меняет:</b> {m.DescriptionRu}\n" +
            $"<b>Как это выглядит в игре:</b> {m.WhatToWatchRu}\n" +
            $"<b>Маленькое значение · MIN {m.FormatValue(m.Minimum)}:</b> {m.MinimumMeaningRu}\n" +
            $"<b>Большое значение · MAX {m.FormatValue(m.Maximum)}:</b> {m.MaximumMeaningRu}\n" +
            $"Сейчас: {m.FormatValue(current)}   Production: {m.FormatValue(m.Production)}   Safe random: {m.FormatValue(m.SafeRandomMinimum)}…{m.FormatValue(m.SafeRandomMaximum)}\n" +
            $"Audit: {m.AuditStatus}   Consumer: {m.ConsumerPath} → {m.ConsumerTarget}\n" +
            $"↺ click: DEFAULT   Shift+↺: MAX EFFECT ({m.FormatValue(m.DiagnosticExtreme)})";
    }

    private void LoadVisualProductionValues()
    {
        if (!VisualTuningPresetStorage.TryLoad(
                out VisualTuningSnapshot snapshot,
                out string source,
                out string message))
        {
            visualSaveMessage = message;
            return;
        }

        visualProductionSnapshot = snapshot;
        visualProductionLoaded = true;
        visualHasSavedPreset = true;
        visualSaveMessage = "Production values загружены: " + source;
        productionSectorDebug?.ApplyProductionSnapshot(snapshot);
        productionVisualTuning?.ApplyProductionSnapshot(snapshot);
        ApplyVisualProductionToLateTargets();
        visualSavedBaselineJson = GetVisualGlobalJson(snapshot);
        Debug.Log("[VisualLab LOAD]\n" +
            $"ProjectileVisualScale={snapshot.ProjectileScale:0.###}\n" +
            $"TrailWidth={snapshot.TrailWidth:0.###}\n" +
            $"TrailTime={snapshot.TrailLifetime:0.###}\n" +
            $"CameraZoom={snapshot.CameraOrthographicSize:0.###}\n" +
            "source=" + source, this);
    }

    private void ApplyVisualProductionToLateTargets()
    {
        if (!visualProductionLoaded)
            return;
        VisualTuningSnapshot values = visualProductionSnapshot;
        if (worldRuleVisual != null &&
            productionAppliedWorldRuleVisual != worldRuleVisual)
        {
            worldRuleVisual.SetPlayerGlowIntensityMultiplier(
                values.PlayerGlowIntensity);
            worldRuleVisual.SetPlayerGlowRadiusMultiplier(values.PlayerGlowRadius);
            worldRuleVisual.SetWindDustAmountMultiplier(values.WindDustAmount);
            productionAppliedWorldRuleVisual = worldRuleVisual;
        }
        if (playerOrbitVisual != null &&
            productionAppliedOrbitVisual != playerOrbitVisual)
        {
            ApplyRingProductionValues(values);
            productionAppliedOrbitVisual = playerOrbitVisual;
        }
        if (anomalyController != null &&
            productionAppliedAnomalyController != anomalyController)
        {
            ApplyAtmosphereProductionValues(values);
            productionAppliedAnomalyController = anomalyController;
        }
        if (cameraFollow != null &&
            productionAppliedCameraFollow != cameraFollow)
        {
            cameraFollow.SetDebugOrthographicSize(values.CameraOrthographicSize);
            productionAppliedCameraFollow = cameraFollow;
        }
    }

    private void ApplyRingProductionValues(VisualTuningSnapshot values)
    {
        if (playerOrbitVisual == null)
            return;
        playerOrbitVisual.SetRingEnabled(values.RingEnabled);
        playerOrbitVisual.SetRingRadiusMultiplier(values.RingRadius);
        playerOrbitVisual.SetRingWidth(values.RingWidth);
        playerOrbitVisual.SetRingAlpha(values.RingOpacity);
        playerOrbitVisual.SetRingIntensity(values.RingBrightness);
        playerOrbitVisual.SetRingPulseAmount(values.RingPulseAmount);
        playerOrbitVisual.SetRingPulseSpeed(values.RingPulseSpeed);
        playerOrbitVisual.SetRingRotationSpeed(values.RingRotationSpeed);
        playerOrbitVisual.SetRingOffsetX(values.RingOffsetX);
        playerOrbitVisual.SetRingOffsetY(values.RingOffsetY);
        playerOrbitVisual.SetRingTint(values.RingTint);
    }

    private void ApplyAtmosphereProductionValues(VisualTuningSnapshot values)
    {
        if (anomalyController == null)
            return;
        anomalyController.SetAnomalyFocusEnabled(values.AnomalyFocus);
        anomalyController.SetOutsideDarkness(values.OutsideDarkness);
        anomalyController.SetOutsideColor(values.OutsideColor);
        anomalyController.SetFocusTransition(values.FocusTransition);
    }

    private void AddVisualLiveStatus()
    {
        RectTransform row = CreateRect("Visual Save Status", contentRoot);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 38f;
        visualSaveStatusText = CreateText("Label", row, string.Empty, 10.5f,
            TextAlignmentOptions.MidlineLeft, successColor);
        visualSaveStatusText.textWrappingMode = TextWrappingModes.Normal;
        Stretch(visualSaveStatusText.rectTransform, 7f, 7f);
        if (visualSavedBaselineJson == null)
            visualSavedBaselineJson = GetVisualGlobalJson(BuildVisualSnapshot());
        UpdateVisualSaveStatus();
    }

    private void UpdateVisualSaveStatus()
    {
        if (visualSaveStatusText == null || productionSectorDebug == null)
            return;
        VisualTuningSnapshot snapshot = BuildVisualSnapshot();
        bool dirty = visualSavedBaselineJson != GetVisualGlobalJson(snapshot);
        visualSaveStatusText.color = dirty ? warningColor : successColor;
        string state = dirty ? "● Есть несохранённые изменения"
            : visualHasSavedPreset ? "✓ Значения сохранены"
            : "✓ Production values";
        visualSaveStatusText.text =
            "● LIVE — изменения применяются сразу   " +
            state +
            (string.IsNullOrWhiteSpace(visualSaveMessage)
                ? string.Empty : "\n" + visualSaveMessage);
    }

    private void AddVisualObjectFocusButtons()
    {
        VisualSection[] sections =
        {
            VisualSection.Player, VisualSection.Enemies, VisualSection.Anomalies
        };
        string[] labels = { "PLAYER", "ENEMY", "ANOMALY" };
        RectTransform row = CreateRect("Visual Object Focus", contentRoot);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;
        for (int i = 0; i < sections.Length; i++)
        {
            int index = i;
            Button button = CreateButton(row, labels[i], () =>
            {
                for (int section = 0; section < visualSectionExpanded.Length; section++)
                    visualSectionExpanded[section] = false;
                visualSectionExpanded[(int)sections[index]] = true;
                RefreshCurrentTab();
            }, 80f);
            RectTransform rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2((float)index / sections.Length, 0f);
            rect.anchorMax = new Vector2((float)(index + 1) / sections.Length, 1f);
            rect.offsetMin = new Vector2(2f, 3f);
            rect.offsetMax = new Vector2(-2f, -3f);
        }
    }

    private void AddVisualTintChannels(
        string label, Color value, System.Action<Color> setter)
    {
        Color current = value;
        AddSliderRow(label + " R", current.r, 0f, 1f, channel =>
        { current.r = channel; setter?.Invoke(current); }, "0.00", 1f);
        AddSliderRow(label + " G", current.g, 0f, 1f, channel =>
        { current.g = channel; setter?.Invoke(current); }, "0.00", 1f);
        AddSliderRow(label + " B", current.b, 0f, 1f, channel =>
        { current.b = channel; setter?.Invoke(current); }, "0.00", 1f);
    }

    private VisualTuningSnapshot BuildVisualSnapshot()
    {
        VisualTuningSnapshot result = visualProductionLoaded
            ? visualProductionSnapshot
            : new VisualTuningSnapshot();
        ProductionSectorDebugController debug = productionSectorDebug;
        ProductionVisualTuningController tuning = productionVisualTuning;
        if (debug != null)
        {
            result.WorldReadability = (int)debug.Preset;
            result.DecorBrightness = debug.DecorBrightness;
            result.EnvironmentDarken = debug.EnvironmentDarken;
            result.EnemyReadability = (int)debug.EnemyMode;
            result.EnemyScope = (int)debug.CurrentEnemyScope;
            result.EnemyBrightness = debug.EnemyBrightness;
            result.EnemySaturation = debug.EnemySaturation;
            result.EnemyTintStrength = debug.EnemyTintStrength;
            result.EnemyHueShift = debug.EnemyHueShift;
            result.EnemyRecolorTarget = debug.EnemyRecolorTarget;
            result.EnemyRecolorStrength = debug.EnemyRecolorStrength;
            result.EnemyOutlineEnabled = debug.EnemyOutlineEnabled;
            result.EnemyOutlineStrength = debug.EnemyOutlineStrength;
            result.EnemyOutlineWidth = debug.EnemyOutlineWidth;
            result.AnomalyAccent = debug.AnomalyAccent;
            result.MonochromeAnomalies = debug.MonochromeAnomaliesEnabled;
            result.Anomalies = debug.CaptureVisualTunerSnapshots();
            result.AnomalyArtHooksVisible = debug.VisualTunerArtHooksVisible;
            if (debug.VisualTunerTarget != null)
            {
                result.AnomalyTarget = debug.VisualTunerTargetName;
                result.AnomalyValues = debug.VisualTunerValues;
            }
        }
        if (tuning != null)
        {
            result.EnvironmentColorSchemaVersion =
                VisualTuningPresetStorage.CurrentEnvironmentColorSchemaVersion;
            result.SceneTint = tuning.SceneTint;
            result.SceneTintAmount = tuning.SceneTintAmount;
            result.SceneSaturation = tuning.SceneSaturation;
            result.SceneBrightness = tuning.SceneBrightness;
            result.GrassTint = tuning.GrassTint;
            result.GrassTintAmount = tuning.GrassTintAmount;
            result.GrassSaturation = tuning.GrassSaturation;
            result.GrassBrightness = tuning.GrassBrightness;
            result.PlantsTint = tuning.PlantsTint;
            result.PlantsTintAmount = tuning.PlantsTintAmount;
            result.PlantsSaturation = tuning.PlantsSaturation;
            result.PlantsBrightness = tuning.PlantsBrightness;
            result.BackgroundTint = tuning.BackgroundTint;
            result.BackgroundTintAmount = tuning.BackgroundTintAmount;
            result.BackgroundSaturation = tuning.BackgroundSaturation;
            result.BackgroundBrightness = tuning.BackgroundBrightness;
            result.PlayerScale = tuning.PlayerVisualScale;
            result.PlayerOffsetX = tuning.PlayerVisualOffset.x;
            result.PlayerOffsetY = tuning.PlayerVisualOffset.y;
            result.PlayerBrightness = tuning.PlayerBrightness;
            result.PlayerSaturation = tuning.PlayerSaturation;
            result.PlayerOpacity = tuning.PlayerOpacity;
            result.PlayerTint = tuning.PlayerTint;
            result.PlayerTintStrength = tuning.PlayerTintStrength;
            result.WeaponScale = tuning.WeaponVisualScale;
            result.WeaponOffsetX = tuning.WeaponVisualOffset.x;
            result.WeaponOffsetY = tuning.WeaponVisualOffset.y;
            result.WeaponBrightness = tuning.WeaponBrightness;
            result.WeaponSaturation = tuning.WeaponSaturation;
            result.WeaponOpacity = tuning.WeaponOpacity;
            result.WeaponTint = tuning.WeaponTint;
            result.WeaponTintStrength = tuning.WeaponTintStrength;
            result.ProjectileScale = tuning.ProjectileVisualScale;
            result.TrailWidth = tuning.TrailWidth;
            result.TrailLifetime = tuning.TrailTime;
            result.TrailOpacity = tuning.TrailAlpha;
            result.TrailBrightness = tuning.TrailBrightness;
            result.LaserCoreWidth = tuning.LaserCoreWidth;
            result.LaserGlowWidth = tuning.LaserGlowWidth;
            result.LaserBrightness = tuning.LaserBrightness;
            result.VignetteIntensity = tuning.VignetteIntensity;
        }
        if (worldRuleVisual != null)
        {
            result.PlayerGlowIntensity = worldRuleVisual.PlayerGlowIntensityMultiplier;
            result.PlayerGlowRadius = worldRuleVisual.PlayerGlowRadiusMultiplier;
            result.WindDustAmount = worldRuleVisual.WindDustAmountMultiplier;
        }
        if (playerOrbitVisual != null)
        {
            result.RingEnabled = playerOrbitVisual.RingEnabled;
            result.RingRadius = playerOrbitVisual.RingRadiusMultiplier;
            result.RingWidth = playerOrbitVisual.RingWidth;
            result.RingOpacity = playerOrbitVisual.RingAlpha;
            result.RingBrightness = playerOrbitVisual.RingIntensity;
            result.RingPulseAmount = playerOrbitVisual.RingPulseAmount;
            result.RingPulseSpeed = playerOrbitVisual.RingPulseSpeed;
            result.RingRotationSpeed = playerOrbitVisual.RingRotationSpeed;
            result.RingOffsetX = playerOrbitVisual.RingOffset.x;
            result.RingOffsetY = playerOrbitVisual.RingOffset.y;
            result.RingTint = playerOrbitVisual.RingTint;
        }
        if (anomalyController != null)
        {
            result.AnomalyFocus = anomalyController.AnomalyFocusEnabled;
            result.OutsideDarkness = anomalyController.OutsideDarkness;
            result.OutsideColor = anomalyController.OutsideColor;
            result.FocusTransition = anomalyController.FocusTransition;
        }
        if (cameraFollow != null)
            result.CameraOrthographicSize = cameraFollow.DebugOrthographicSize;
        return result;
    }

    private static string GetVisualGlobalJson(VisualTuningSnapshot snapshot)
    {
        snapshot.AnomalyTarget = null;
        snapshot.AnomalyValues = default;
        snapshot.AnomalyArtHooksVisible = false;
        return JsonUtility.ToJson(snapshot);
    }

    private void AddFeelLiveStatus()
    {
        RectTransform row = CreateRect("Feel Save Status", contentRoot);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 38f;
        feelSaveStatusText = CreateText(
            "Label", row, string.Empty, 10.5f,
            TextAlignmentOptions.MidlineLeft, successColor);
        feelSaveStatusText.textWrappingMode = TextWrappingModes.Normal;
        Stretch(feelSaveStatusText.rectTransform, 7f, 7f);
        UpdateFeelSaveStatus();
    }

    private void UpdateFeelSaveStatus()
    {
        if (feelSaveStatusText == null || productionFeelTuning == null)
            return;
        bool dirty = productionFeelTuning.Lab.HasUnsavedChanges;
        feelSaveStatusText.color = dirty ? warningColor : successColor;
        string state = dirty
            ? "● Есть несохранённые изменения"
            : "✓ Значения сохранены";
        if (string.IsNullOrWhiteSpace(feelSaveMessage) && !dirty)
            state = "✓ Production values";
        feelSaveStatusText.text =
            "● LIVE — изменения применяются сразу   " + state +
            (string.IsNullOrWhiteSpace(feelSaveMessage)
                ? string.Empty : "\n" + feelSaveMessage);
    }

    private void ResetAllVisualLabValues()
    {
        if (!visualProductionLoaded)
        {
            visualSaveMessage =
                "RESET недоступен: сохранённый production preset не найден.";
            RefreshCurrentTab();
            return;
        }
        ApplyFullVisualProductionSnapshot();
        visualSaveMessage =
            "RESET ALL применён: восстановлены последние сохранённые production values.";
        RefreshCurrentTab();
    }

    private void ApplyFullVisualProductionSnapshot()
    {
        if (!visualProductionLoaded)
            return;
        VisualTuningSnapshot values = visualProductionSnapshot;
        productionSectorDebug?.ApplyProductionSnapshot(values);
        productionVisualTuning?.ApplyProductionSnapshot(values);
        if (worldRuleVisual != null)
        {
            worldRuleVisual.SetPlayerGlowIntensityMultiplier(
                values.PlayerGlowIntensity);
            worldRuleVisual.SetPlayerGlowRadiusMultiplier(values.PlayerGlowRadius);
            worldRuleVisual.SetWindDustAmountMultiplier(values.WindDustAmount);
        }
        ApplyRingProductionValues(values);
        ApplyAtmosphereProductionValues(values);
        cameraFollow?.SetDebugOrthographicSize(values.CameraOrthographicSize);
    }

    private void SaveVisualLabValues()
    {
        // Bunker has no loaded production tuning snapshot to persist.
        if (characterSpawner == null || productionVisualTuning == null || !visualProductionLoaded)
            return;
        VisualTuningSnapshot snapshot = BuildVisualSnapshot();
        bool saved = VisualTuningPresetStorage.Save(
            snapshot, GetVisualLabValuesText(), out string message);
        visualSaveMessage = (saved ? "✓ " : "⚠ ") + message;
        if (saved)
        {
            visualProductionSnapshot = snapshot;
            visualProductionLoaded = true;
            visualHasSavedPreset = true;
            productionSectorDebug?.ApplyProductionSnapshot(snapshot);
            productionVisualTuning?.ApplyProductionSnapshot(snapshot);
            visualSavedBaselineJson = GetVisualGlobalJson(snapshot);
        }
        RefreshCurrentTab();
    }

    private void ResetAllFeelLabValues()
    {
        productionFeelTuning?.ResetAll();
        feelSaveMessage = "RESET применён live; значения не сохранены.";
        RefreshCurrentTab();
    }

    private void SaveFeelLabValues()
    {
        if (productionFeelTuning == null)
            return;
        bool saved = productionFeelTuning.SaveTuningPreset(out string message);
        feelSaveMessage = (saved ? "✓ " : "⚠ ") + message;
        RefreshCurrentTab();
    }

    private void ResetVisualSectionToProduction(VisualSection section)
    {
        if (!visualProductionLoaded)
        {
            switch (section)
            {
                case VisualSection.Scene:
                    productionVisualTuning?.ResetSceneLook(); break;
                case VisualSection.Grass:
                    productionVisualTuning?.ResetGrassLook(); break;
                case VisualSection.Plants:
                    productionVisualTuning?.ResetPlantsLook(); break;
                case VisualSection.Background:
                    productionVisualTuning?.ResetBackgroundLook(); break;
                case VisualSection.World:
                    productionSectorDebug?.ResetWorldVisualSettings(); break;
                case VisualSection.Enemies:
                    productionSectorDebug?.ResetEnemyVisualSettings(); break;
                case VisualSection.Player:
                    worldRuleVisual?.ResetPlayerGlowDebugSettings();
                    productionVisualTuning?.ResetPlayerSettings(); break;
                case VisualSection.Weapon:
                    productionVisualTuning?.ResetWeaponSettings(); break;
                case VisualSection.PlayerRing:
                    playerOrbitVisual?.ResetPresentationSettings(); break;
                case VisualSection.PostFx:
                    productionVisualTuning?.ResetVignette(); break;
                case VisualSection.Atmosphere:
                    anomalyController?.ResetFocusPresentationForDebug();
                    worldRuleVisual?.ResetWindDustDebugSettings(); break;
                case VisualSection.Anomalies:
                    productionSectorDebug?.ResetAnomalyVisualSettings(); break;
                case VisualSection.Projectiles:
                    productionVisualTuning?.ResetProjectileSettings();
                    productionVisualTuning?.ResetLaserSettings(); break;
                case VisualSection.Camera:
                    cameraFollow?.ResetDebugOrthographicSize(); break;
            }
            return;
        }

        VisualTuningSnapshot values = visualProductionSnapshot;
        switch (section)
        {
            case VisualSection.Scene:
                productionVisualTuning?.ResetSceneLook(); break;
            case VisualSection.Grass:
                productionVisualTuning?.ResetGrassLook(); break;
            case VisualSection.Plants:
                productionVisualTuning?.ResetPlantsLook(); break;
            case VisualSection.Background:
                productionVisualTuning?.ResetBackgroundLook(); break;
            case VisualSection.World:
                productionSectorDebug?.ResetWorldVisualSettings(); break;
            case VisualSection.Enemies:
                productionSectorDebug?.ResetEnemyVisualSettings(); break;
            case VisualSection.Player:
                productionVisualTuning?.ResetPlayerSettings();
                worldRuleVisual?.SetPlayerGlowIntensityMultiplier(
                    values.PlayerGlowIntensity);
                worldRuleVisual?.SetPlayerGlowRadiusMultiplier(
                    values.PlayerGlowRadius);
                break;
            case VisualSection.Weapon:
                productionVisualTuning?.ResetWeaponSettings(); break;
            case VisualSection.PlayerRing:
                ApplyRingProductionValues(values); break;
            case VisualSection.PostFx:
                productionVisualTuning?.ResetVignette(); break;
            case VisualSection.Atmosphere:
                ApplyAtmosphereProductionValues(values);
                worldRuleVisual?.SetWindDustAmountMultiplier(
                    values.WindDustAmount);
                break;
            case VisualSection.Anomalies:
                productionSectorDebug?.ResetAnomalyVisualSettings(); break;
            case VisualSection.Projectiles:
                productionVisualTuning?.ResetProjectileSettings();
                productionVisualTuning?.ResetLaserSettings();
                break;
            case VisualSection.Camera:
                cameraFollow?.SetDebugOrthographicSize(
                    values.CameraOrthographicSize);
                break;
        }
        visualSaveMessage =
            "RESET SECTION: восстановлены последние production values.";
    }

    private void AddVisualSectionReset(string section, System.Action reset)
    {
        RectTransform row = CreateRect(section + " Reset", contentRoot);
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;
        Button button = CreateButton(
            row,
            "Reset section",
            () =>
            {
                reset?.Invoke();
                RefreshCurrentTab();
            },
            92f
        );
        RectTransform rect = button.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.anchoredPosition = new Vector2(-4f, 0f);
        rect.sizeDelta = new Vector2(92f, 20f);
        if (button.targetGraphic is Image image)
            image.color = new Color(0.14f, 0.17f, 0.21f, 1f);
        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            text.fontSize = 10f;
            text.color = mutedColor;
        }
    }

    private string GetVisualLabValuesText()
    {
        ProductionSectorDebugController debug = productionSectorDebug;
        if (debug == null)
            return string.Empty;

        StringBuilder values = new();
        values.AppendLine("WORLD");
        values.AppendLine($"Readability = {debug.Preset}");
        values.AppendLine($"DecorBrightness = {debug.DecorBrightness:0.###}");
        values.AppendLine($"EnvironmentDarken = {debug.EnvironmentDarken:0.###}");
        values.AppendLine();
        values.AppendLine("ENEMIES");
        values.AppendLine($"Readability = {debug.EnemyMode}");
        values.AppendLine($"Scope = {debug.CurrentEnemyScope}");
        values.AppendLine($"Brightness = {debug.EnemyBrightness:0.###}");
        values.AppendLine($"Saturation = {debug.EnemySaturation:0.###}");
        values.AppendLine($"TintStrength = {debug.EnemyTintStrength:0.###}");
        values.AppendLine($"HueShift = {debug.EnemyHueShift:0.###}");
        Color recolor = debug.EnemyRecolorTarget;
        values.AppendLine(
            $"TargetColor = #{ColorUtility.ToHtmlStringRGB(recolor)}");
        values.AppendLine(
            $"RecolorStrength = {debug.EnemyRecolorStrength:0.###}");
        values.AppendLine($"OutlineEnabled = {debug.EnemyOutlineEnabled}");
        values.AppendLine($"OutlineStrength = {debug.EnemyOutlineStrength:0.###}");
        values.AppendLine($"OutlineWidth = {debug.EnemyOutlineWidth:0.###}");

        if (anomalyController != null)
        {
            values.AppendLine();
            values.AppendLine("ATMOSPHERE");
            values.AppendLine(
                $"AnomalyFocus = {anomalyController.AnomalyFocusEnabled}");
            values.AppendLine(
                $"OutsideDarkness = {anomalyController.OutsideDarkness:0.###}");
            values.AppendLine(
                $"OutsideColor = {anomalyController.OutsideColor:0.###}");
            values.AppendLine(
                $"FocusTransition = {anomalyController.FocusTransition:0.###}");
        }

        if (worldRuleVisual != null)
        {
            values.AppendLine($"WindDustAmount = " +
                $"{worldRuleVisual.WindDustAmountMultiplier:0.###}");
        }

        if (worldRuleVisual != null || playerOrbitVisual != null)
        {
            values.AppendLine();
            values.AppendLine("PLAYER");
            if (worldRuleVisual != null)
            {
                values.AppendLine($"GlowIntensity = " +
                    $"{worldRuleVisual.PlayerGlowIntensityMultiplier:0.###}");
                values.AppendLine($"GlowRadius = " +
                    $"{worldRuleVisual.PlayerGlowRadiusMultiplier:0.###}");
            }
            if (playerOrbitVisual != null)
            {
                values.AppendLine($"OrbitRing = {playerOrbitVisual.RingEnabled}");
                values.AppendLine($"OrbitRadius = " +
                    $"{playerOrbitVisual.CurrentOrbitRadius:0.###}");
                values.AppendLine($"OrbitVisualRadiusMultiplier = " +
                    $"{playerOrbitVisual.RingRadiusMultiplier:0.###}");
                values.AppendLine($"OrbitIntensity = " +
                    $"{playerOrbitVisual.RingIntensity:0.###}");
                values.AppendLine($"OrbitWidth = " +
                    $"{playerOrbitVisual.RingWidth:0.###}");
                values.AppendLine($"OrbitAlpha = " +
                    $"{playerOrbitVisual.RingAlpha:0.###}");
                values.AppendLine($"OrbitPulseAmount = {playerOrbitVisual.RingPulseAmount:0.###}");
                values.AppendLine($"OrbitPulseSpeed = {playerOrbitVisual.RingPulseSpeed:0.###}");
                values.AppendLine($"OrbitRotationSpeed = {playerOrbitVisual.RingRotationSpeed:0.###}");
                values.AppendLine($"OrbitOffset = {playerOrbitVisual.RingOffset}");
            }
        }

        values.AppendLine();
        values.AppendLine("ANOMALIES");
        values.AppendLine($"GlobalAccent = {debug.AnomalyAccent:0.###}");
        values.AppendLine($"Monochrome = {debug.MonochromeAnomaliesEnabled}");

        if (debug.VisualTunerTarget != null)
        {
            values.AppendLine($"Target = {debug.VisualTunerTargetName}");
            values.Append(debug.VisualTunerTarget.GetVisualTunerValuesText());
        }

        if (productionVisualTuning != null)
        {
            values.AppendLine();
            values.AppendLine("SCENE LOOK");
            values.AppendLine($"Tint = #{ColorUtility.ToHtmlStringRGB(productionVisualTuning.SceneTint)}");
            values.AppendLine($"TintAmount = {productionVisualTuning.SceneTintAmount:0.###}");
            values.AppendLine($"Saturation = {productionVisualTuning.SceneSaturation:0.###}");
            values.AppendLine($"Brightness = {productionVisualTuning.SceneBrightness:0.###}");
            values.AppendLine();
            values.AppendLine("GRASS LOOK");
            values.AppendLine($"Tint = #{ColorUtility.ToHtmlStringRGB(productionVisualTuning.GrassTint)}");
            values.AppendLine($"TintAmount = {productionVisualTuning.GrassTintAmount:0.###}");
            values.AppendLine($"Saturation = {productionVisualTuning.GrassSaturation:0.###}");
            values.AppendLine($"Brightness = {productionVisualTuning.GrassBrightness:0.###}");
            values.AppendLine();
            values.AppendLine("PLANTS LOOK");
            values.AppendLine($"Tint = #{ColorUtility.ToHtmlStringRGB(productionVisualTuning.PlantsTint)}");
            values.AppendLine($"TintAmount = {productionVisualTuning.PlantsTintAmount:0.###}");
            values.AppendLine($"Saturation = {productionVisualTuning.PlantsSaturation:0.###}");
            values.AppendLine($"Brightness = {productionVisualTuning.PlantsBrightness:0.###}");
            values.AppendLine();
            values.AppendLine("BACKGROUND LOOK");
            values.AppendLine($"Tint = #{ColorUtility.ToHtmlStringRGB(productionVisualTuning.BackgroundTint)}");
            values.AppendLine($"TintAmount = {productionVisualTuning.BackgroundTintAmount:0.###}");
            values.AppendLine($"Saturation = {productionVisualTuning.BackgroundSaturation:0.###}");
            values.AppendLine($"Brightness = {productionVisualTuning.BackgroundBrightness:0.###}");
            values.AppendLine();
            values.AppendLine("PLAYER SPRITE");
            values.AppendLine($"Scale = {productionVisualTuning.PlayerVisualScale:0.###}");
            values.AppendLine($"Offset = {productionVisualTuning.PlayerVisualOffset}");
            values.AppendLine($"Brightness = {productionVisualTuning.PlayerBrightness:0.###}");
            values.AppendLine($"Saturation = {productionVisualTuning.PlayerSaturation:0.###}");
            values.AppendLine($"Opacity = {productionVisualTuning.PlayerOpacity:0.###}");
            values.AppendLine();
            values.AppendLine("WEAPON SPRITE");
            values.AppendLine($"Scale = {productionVisualTuning.WeaponVisualScale:0.###}");
            values.AppendLine($"Offset = {productionVisualTuning.WeaponVisualOffset}");
            values.AppendLine($"Brightness = {productionVisualTuning.WeaponBrightness:0.###}");
            values.AppendLine($"Saturation = {productionVisualTuning.WeaponSaturation:0.###}");
            values.AppendLine($"Opacity = {productionVisualTuning.WeaponOpacity:0.###}");
            values.AppendLine();
            values.AppendLine("POST FX");
            values.AppendLine($"VignetteIntensity = " +
                $"{productionVisualTuning.VignetteIntensity:0.###}");
            values.AppendLine();
            values.AppendLine("PROJECTILES");
            values.AppendLine($"VisualScale = " +
                $"{productionVisualTuning.ProjectileVisualScale:0.###}");
            values.AppendLine($"TrailWidth = " +
                $"{productionVisualTuning.TrailWidth:0.###}");
            values.AppendLine($"TrailTime = " +
                $"{productionVisualTuning.TrailTime:0.###}");
            values.AppendLine($"TrailAlpha = " +
                $"{productionVisualTuning.TrailAlpha:0.###}");
            values.AppendLine($"TrailBrightness = {productionVisualTuning.TrailBrightness:0.###}");
            values.AppendLine($"LaserCoreWidth = {productionVisualTuning.LaserCoreWidth:0.###}");
            values.AppendLine($"LaserGlowWidth = {productionVisualTuning.LaserGlowWidth:0.###}");
            values.AppendLine($"LaserBrightness = {productionVisualTuning.LaserBrightness:0.###}");
        }

        if (cameraFollow != null)
        {
            values.AppendLine();
            values.AppendLine("CAMERA");
            values.AppendLine($"OrthographicSize = " +
                $"{cameraFollow.DebugOrthographicSize:0.###}");
        }

        return values.ToString().TrimEnd();
    }

    private void CopyVisualLabValues()
    {
        string result = GetVisualLabValuesText();
        if (string.IsNullOrWhiteSpace(result)) return;
        GUIUtility.systemCopyBuffer = result;
        visualSaveMessage =
            "Скопировано в буфер обмена; saved visual preset не изменён.";
        Debug.Log($"[RuntimeVisualLab]\n{result}", this);
        RefreshCurrentTab();
    }

    private void CopyFeelLabValues()
    {
        if (productionFeelTuning == null)
            return;

        string result = productionFeelTuning.GetValuesText();
        GUIUtility.systemCopyBuffer = result;
        feelSaveMessage =
            "Скопировано в буфер обмена; production и saved preset не изменены.";
        Debug.Log($"[RuntimeFeelLab]\n{result}", this);
        RefreshCurrentTab();
    }

    private void AddSectionTitle(string title, string subtitle)
    {
        RectTransform section = CreateRect(title, contentRoot);
        bool compact = IsRuntimeLabTab(activeTab);
        section.gameObject.AddComponent<LayoutElement>().preferredHeight =
            compact ? 26f : 62f;
        section.gameObject.AddComponent<Image>().color =
            new Color(accentColor.r, accentColor.g, accentColor.b, 0.18f);
        TextMeshProUGUI text = CreateText(
            "Label", section,
            compact
                ? $"<b>{title}</b>"
                : $"<b>{title}</b>\n<size=16><color=#A6AFBC>{subtitle}</color></size>",
            compact ? 11f : 22f,
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
        bool compact = IsRuntimeLabTab(activeTab);
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
        string format,
        float? productionMarker = null,
        Func<float, string> displayFormatter = null)
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

        RectTransform productionLine = null;
        if (productionMarker.HasValue)
        {
            float normalized = Mathf.InverseLerp(minimum, maximum,
                productionMarker.Value);
            RectTransform marker = CreateRect("Production Marker", sliderRoot);
            productionLine = marker;
            marker.anchorMin = marker.anchorMax = new Vector2(normalized, .5f);
            marker.pivot = new Vector2(.5f, .5f);
            marker.sizeDelta = new Vector2(2f, 16f);
            Image markerImage = marker.gameObject.AddComponent<Image>();
            markerImage.color = warningColor;
            markerImage.raycastTarget = false;
        }

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
        if (productionLine != null) productionLine.SetAsLastSibling();

        TextMeshProUGUI valueText = CreateText(
            "Value", row, displayFormatter != null
                ? displayFormatter(value) : value.ToString(format), 11f,
            TextAlignmentOptions.MidlineRight, successColor);
        valueText.rectTransform.anchorMin = new Vector2(0.83f, 0f);
        valueText.rectTransform.anchorMax = Vector2.one;
        valueText.rectTransform.offsetMin = Vector2.zero;
        valueText.rectTransform.offsetMax = new Vector2(-7f, 0f);
        valueText.raycastTarget = false;

        slider.SetValueWithoutNotify(Mathf.Clamp(value, minimum, maximum));
        if (activeTab == DebugTab.VisualTest)
            AttachFeelTooltip(row.gameObject, () =>
                BuildVisualTooltipRu(label, minimum, maximum,
                    productionMarker ?? value));
        slider.onValueChanged.AddListener(current =>
        {
            valueText.text = displayFormatter != null
                ? displayFormatter(current) : current.ToString(format);
            setter?.Invoke(current);
            if (activeTab == DebugTab.VisualTest)
                UpdateVisualSaveStatus();
        });
    }

    private static string BuildVisualTooltipRu(
        string label, float minimum, float maximum, float production)
    {
        string lower = label.ToLowerInvariant();
        string effect = lower.Contains("насыщенность травы")
                ? "Насколько яркими выглядят цвета травы. 0 — трава серая, 1 — исходный цвет, выше 1 — заметно насыщеннее. Меняет только траву."
            : lower.Contains("насыщенность сцены")
                ? "Насыщенность всего игрового мира. Не влияет на интерфейс. 0 — чёрно-белая картинка, 1 — обычный вид, выше 1 — более сочные цвета."
            : lower.Contains("насыщенность растений")
                ? "Насыщенность растений и деревьев. 0 — серые, 1 — исходный цвет, выше 1 — более сочные. Другие группы не меняются."
            : lower.Contains("насыщенность background")
                ? "Насыщенность фоновых Tilemap. 0 — серые, 1 — исходный цвет, выше 1 — более сочные. Трава и растения не меняются."
            : lower.Contains("сила цвета")
                ? "Смешивает исходные детали с выбранным оттенком: 0 — без перекраски, 1 — максимальное влияние цвета."
            : lower.StartsWith("цвет сцены")
                ? "Цветовой оттенок игрового мира. Интерфейс остаётся нейтральным; силу влияния задаёт отдельный параметр."
            : lower.StartsWith("цвет травы")
                ? "Выбранный оттенок только для Tilemap травы; исходные текстуры не изменяются."
            : lower.StartsWith("цвет растений")
                ? "Выбранный оттенок только для vegetation под root Plants."
            : lower.StartsWith("цвет background")
                ? "Выбранный оттенок только для фоновых Tilemap."
            : lower.Contains("размер") || lower.Contains("scale") ||
            lower.Contains("radius") || lower.Contains("радиус")
                ? "Меняет только видимый размер элемента. Gameplay collider и радиус действия не меняются."
            : lower.Contains("смещение") || lower.Contains("offset")
                ? "Сдвигает только изображение относительно его gameplay root."
            : lower.Contains("ярк") || lower.Contains("brightness") || lower.Contains("glow")
                ? "Управляет тем, насколько ярким и светящимся выглядит объект."
            : lower.Contains("насыщ") || lower.Contains("saturation")
                ? "Управляет насыщенностью цвета: минимум почти обесцвечивает, максимум делает цвета очень сочными."
            : lower.Contains("прозрач") || lower.Contains("alpha") || lower.Contains("opacity")
                ? "Управляет прозрачностью: минимум скрывает visual, максимум делает его полностью видимым."
            : lower.Contains("width") || lower.Contains("толщ")
                ? "Меняет видимую толщину линии или следа без изменения hitbox."
            : lower.Contains("duration") || lower.Contains("time") || lower.Contains("lifetime")
                ? "Меняет время, в течение которого визуальный эффект остаётся видимым."
            : "Меняет существующее визуальное свойство объекта и сразу показывает результат в сцене.";
        return $"<b>{label.ToUpperInvariant()}</b>\n<color=#68D98B>ТОЛЬКО VISUAL</color>\n" +
            $"<b>Что меняет:</b> {effect}\n" +
            $"<b>Маленькое значение:</b> эффект слабее или меньше.\n" +
            $"<b>Большое значение:</b> эффект сильнее или заметнее.\n" +
            $"Debug: {minimum:0.###}…{maximum:0.###}   Production: {production:0.###}";
    }

    private static string ToRomanLevel(int level) => level switch
    {
        1 => "I",
        2 => "II",
        3 => "III",
        _ => "—"
    };

    private void AddEnemyReadabilityPresetStrip(
        ProductionSectorDebugController debug)
    {
        RectTransform row = CreateRect("Visual Test Strength", contentRoot);
        row.gameObject.AddComponent<Image>().color = rowColor;
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;

        TextMeshProUGUI label = CreateText(
            "Name", row, "MODE", 10f,
            TextAlignmentOptions.MidlineLeft, Color.white);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = new Vector2(0.21f, 1f);
        label.rectTransform.offsetMin = new Vector2(7f, 0f);
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
            rect.anchorMin = new Vector2(0.22f + i * 0.195f, 0.12f);
            rect.anchorMax = new Vector2(0.405f + i * 0.195f, 0.88f);
            rect.offsetMin = new Vector2(1f, 0f);
            rect.offsetMax = new Vector2(-1f, 0f);
            TextMeshProUGUI buttonText =
                button.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
                buttonText.fontSize = 8.5f;
            if (selected && button.targetGraphic is Image image)
                image.color = successColor;
        }
    }

    private void AddEnemyRecolorPresetStrip(
        ProductionSectorDebugController debug)
    {
        RectTransform row = CreateRect("Enemy Recolor Presets", contentRoot);
        row.gameObject.AddComponent<Image>().color = rowColor;
        row.gameObject.AddComponent<LayoutElement>().preferredHeight = 30f;

        string[] labels = { "ORIGINAL", "CYAN", "MAGENTA", "RED", "WHITE" };
        Color[] colors =
        {
            Color.clear,
            Color.cyan,
            Color.magenta,
            Color.red,
            Color.white
        };

        for (int i = 0; i < labels.Length; i++)
        {
            int index = i;
            bool original = index == 0;
            bool selected = original
                ? debug.EnemyRecolorStrength <= 0.001f &&
                    Mathf.Abs(debug.EnemyHueShift) <= 0.001f
                : debug.EnemyRecolorStrength >= 0.999f &&
                    Mathf.Abs(debug.EnemyHueShift) <= 0.001f &&
                    ColorsApproximately(debug.EnemyRecolorTarget, colors[index]);
            Button button = CreateButton(
                row,
                labels[index],
                () =>
                {
                    if (original)
                        debug.ResetEnemyRecolor();
                    else
                        debug.SetEnemyRecolorPreset(colors[index]);
                    RefreshCurrentTab();
                },
                100f);
            RectTransform rect = button.GetComponent<RectTransform>();
            float width = 1f / labels.Length;
            rect.anchorMin = new Vector2(index * width, 0.12f);
            rect.anchorMax = new Vector2((index + 1) * width, 0.88f);
            rect.offsetMin = new Vector2(1f, 0f);
            rect.offsetMax = new Vector2(-1f, 0f);
            TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
                text.fontSize = 8f;
            if (selected && button.targetGraphic is Image image)
                image.color = successColor;
        }
    }

    private void AddEnemyRecolorColorChannel(
        ProductionSectorDebugController debug,
        string label,
        int channel)
    {
        AddSliderRow(
            label,
            GetColorChannel(debug.EnemyRecolorTarget, channel),
            0f,
            1f,
            value =>
            {
                Color color = debug.EnemyRecolorTarget;
                SetColorChannel(ref color, channel, value);
                debug.SetEnemyRecolorTarget(color);
            },
            "0.00");
    }

    private static bool ColorsApproximately(Color first, Color second)
    {
        Vector3 delta = new(
            first.r - second.r,
            first.g - second.g,
            first.b - second.b
        );
        return delta.sqrMagnitude <= 0.0001f;
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
        "COMPLETE SECTOR" => 188f,
        "OPEN SECTOR CHOICE" => 210f,
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
        bool compact = IsRuntimeLabTab(activeTab);
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
#else
    private void Awake()
    {
        enabled = false;
    }
#endif
}
