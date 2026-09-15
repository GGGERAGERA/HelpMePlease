using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class HUDManager : MonoBehaviour
{
    private const int ProductionSectorCount =
        RunRoute.ExplorationSectorCount;

    public static HUDManager Instance { get; private set; }

    [Header("Health")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TextMeshProUGUI healthText;

    [Header("Experience")]
    [SerializeField] private Slider experienceSlider;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI experienceText;
    [SerializeField] private GameplayTargetTracker targetTracker;

    [Header("Stats")]
    [SerializeField] private TextMeshProUGUI killsText;
    [SerializeField] private TextMeshProUGUI currencyText;

    [Header("Timer")]
    [SerializeField] private TextMeshProUGUI timerText;

    [Header("Threat")]
    [SerializeField] private RectTransform threatPanel;
    [SerializeField] private TextMeshProUGUI threatLevelText;
    [SerializeField] private TextMeshProUGUI threatValueText;
    [SerializeField] private RectTransform threatFill;

    [Header("Dash")]
    [SerializeField] private DashCooldownView dashCooldownView;

    [Header("Route Progress")]
    [SerializeField] private RunRouteProgressView routeProgressView;
    [SerializeField] private RunFlowController runFlow;
    [SerializeField] private PauseMenuUI pauseMenu;
    [SerializeField] private InteractionPromptUI interactionPrompt;
    [SerializeField] private RunMessageService runMessages;
    [SerializeField] private CanvasGroup informationGroup;
    private PlayerHealth boundPlayer;

    public bool IsInformationVisible => boundPlayer != null && !boundPlayer.IsDead &&
        runStateManager != null && !runStateManager.IsRunEnded &&
        runFlow != null && runFlow.Phase != RunPhase.Victory && runFlow.Phase != RunPhase.Stopped &&
        !(runFlow.IsLevelCompleted && runFlow.Phase == RunPhase.NormalSector) && !pauseMenu.IsPaused &&
        !SceneTransitionOverlay.IsTransitioning && Time.timeScale > 0f &&
        (UpgradeManager.Instance == null || UpgradeManager.Instance.IsRewardQueueIdle);

    public bool IsBossForegroundVisible =>
        runFlow.Phase == RunPhase.FinalBossIntro ||
        (bossHpPanel != null && bossHpPanel.activeInHierarchy);

    [Header("Low HP")]
    [SerializeField] private CanvasGroup lowHpVignette;
    [SerializeField] private float lowHpThreshold = 0.3f;

    [Header("Boss HP")]
    [SerializeField] private GameObject bossHpPanel;
    [SerializeField] private Slider bossHpSlider;
    [SerializeField] private TextMeshProUGUI bossNameText;

    [Header("World Event Marker")]
    [SerializeField] private WorldEventMarker worldEventMarker;

    [Header("Tactical Map")]
    [SerializeField] private TacticalMapHUD tacticalMap;
    [SerializeField] private WorldLootRewardReel lootReel;
    private RunStatsManager runStatsManager;
    private RunStateManager runStateManager;
    private int lastDisplayedTimerSecond = int.MinValue;
    private int displayedLevel = 1;
    private string displayedBossName;
    private void OnEnable() => LocalizationService.EnsureExists().LanguageChanged += RefreshLanguage;
    private void OnDisable()
    {
        if (LocalizationService.Instance != null) LocalizationService.Instance.LanguageChanged -= RefreshLanguage;
    }
    private void RefreshLanguage(GameLanguage language)
    {
        var localization = LocalizationService.EnsureExists();
        if (levelText != null) levelText.text = string.Format(localization.Get("hud.level"), displayedLevel);
        if (bossNameText != null && displayedBossName != null) bossNameText.text = localization.Get(displayedBossName);
        if (threatLevelText != null && lastDisplayedThreatTier.HasValue)
            threatLevelText.text = string.Format(localization.Get("hud.threat"), ThreatTierPresentation.Format(lastDisplayedThreatTier.Value));
    }
    private ThreatTier? lastDisplayedThreatTier;
    private readonly Vector3[] framingCorners = new Vector3[4];

    // Screen-space allowance from the existing authored HUD; never moves the player/station.
    public Rect GetOrbitalSafePixelRect(Camera camera, bool includeMessage = true)
    {
        Rect pixels = camera.pixelRect;
        Rect device = Screen.safeArea;
        Rect safe = Rect.MinMaxRect(Mathf.Max(pixels.xMin, device.xMin), Mathf.Max(pixels.yMin, device.yMin),
            Mathf.Min(pixels.xMax, device.xMax), Mathf.Min(pixels.yMax, device.yMax));
        float padding = pixels.height * .015f;
        void Reserve(Component component, bool bottom)
        {
            if (component == null || !component.gameObject.activeInHierarchy || component.transform is not RectTransform rect) return;
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            rect.GetWorldCorners(framingCorners);
            float min = float.PositiveInfinity, max = float.NegativeInfinity;
            foreach (var corner in framingCorners)
            {
                float y = RectTransformUtility.WorldToScreenPoint(uiCamera, corner).y;
                min = Mathf.Min(min, y); max = Mathf.Max(max, y);
            }
            if (bottom && max < pixels.center.y) safe.yMin = Mathf.Max(safe.yMin, max + padding);
            if (!bottom && min > pixels.center.y) safe.yMax = Mathf.Min(safe.yMax, min - padding);
        }
        Reserve(experienceSlider, true);
        Reserve(dashCooldownView, true);
        if (IsInformationVisible) Reserve(routeProgressView, false);
        if (bossHpPanel != null && bossHpPanel.activeInHierarchy) Reserve(bossHpPanel.transform, false);
        if (includeMessage && IsInformationVisible && runMessages.View.IsPanelVisible)
            Reserve(runMessages.View, false);
        void ReserveSide(Component component, bool left)
        {
            if (component == null || !component.gameObject.activeInHierarchy || component.transform is not RectTransform rect) return;
            Canvas canvas = rect.GetComponentInParent<Canvas>();
            Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            rect.GetWorldCorners(framingCorners);
            float min = float.PositiveInfinity, max = float.NegativeInfinity;
            foreach (var corner in framingCorners)
            {
                float x = RectTransformUtility.WorldToScreenPoint(uiCamera, corner).x;
                min = Mathf.Min(min, x); max = Mathf.Max(max, x);
            }
            if (left && max < pixels.center.x) safe.xMin = Mathf.Max(safe.xMin, max + padding);
            if (!left && min > pixels.center.x) safe.xMax = Mathf.Min(safe.xMax, min - padding);
        }
        ReserveSide(healthSlider, true);
        ReserveSide(threatPanel, true);
        if (tacticalMap != null) ReserveSide(tacticalMap.LayoutRoot, false);
        return safe;
    }

    private void Awake()
    {
        Instance = this;
        ConfigureIndicatorSlider(healthSlider);
        ConfigureIndicatorSlider(experienceSlider);
        ConfigureIndicatorSlider(bossHpSlider);

        if (bossHpPanel != null)
            bossHpPanel.SetActive(false);

        if (tacticalMap == null || targetTracker == null || lootReel == null || runFlow == null || pauseMenu == null ||
            informationGroup == null || interactionPrompt == null || runMessages == null ||
            threatPanel == null || threatLevelText == null || threatValueText == null || threatFill == null)
        {
            Debug.LogError("[HUDManager] Authored HUD or scene references are missing.", this);
            enabled = false;
            return;
        }

        HideLowHpVignette();
    }

    private void Start()
    {
        runStatsManager = RunStatsManager.Instance;
        runStateManager = RunStateManager.Instance;

        if (runStateManager != null && runStateManager.CurrentSector != null)
        {
            routeProgressView?.ShowCurrent(
                runStateManager.CurrentSector.SectorNumber,
                ProductionSectorCount
            );
        }
        else
        {
            routeProgressView?.Hide();
        }

        if (runStatsManager != null)
            runStatsManager.RewardRelevantStatsChanged += RefreshRunCurrency;
        if (runStateManager != null)
            runStateManager.CurrentRewardChanged += RefreshRunCurrency;

        RefreshRunCurrency();
    }

    private void OnDestroy()
    {
        if (runStatsManager != null)
            runStatsManager.RewardRelevantStatsChanged -= RefreshRunCurrency;
        if (runStateManager != null)
            runStateManager.CurrentRewardChanged -= RefreshRunCurrency;
    }

    public static string ResolveObjectiveKey(RunPhase phase, bool exitAvailable) => phase switch
    {
        RunPhase.NormalSector => exitAvailable ? "hud.objective.exit" : "hud.objective.survive",
        RunPhase.WaitingForRewards or RunPhase.FinalBossIntro => "hud.objective.prepare",
        RunPhase.FinalBossCombat => "hud.objective.boss",
        _ => null
    };

    private void LateUpdate()
    {
        informationGroup.alpha = IsInformationVisible ? 1f : 0f;
        RefreshRunCounters();
        if (runStateManager == null || runStateManager.CurrentSector == null || runFlow == null)
            return;

        bool exitAvailable = false;
        var exits = ProductionSectorExit.ActiveExits;
        for (int i = 0; i < exits.Count; i++)
        {
            var exit = exits[i];
            if (exit.gameObject.scene == gameObject.scene && exit.IsAvailable)
            { exitAvailable = true; break; }
        }

        bool specialAvailable = false;
        if (runFlow.Phase == RunPhase.NormalSector)
        {
            var sites = ProductionAnomalySite.ActiveSites;
            for (int i = 0; i < sites.Count; i++)
            {
                var site = sites[i];
                if (site.gameObject.scene == gameObject.scene && site.IsSpecial && site.IsMapVisible)
                { specialAvailable = true; break; }
            }
        }

        routeProgressView.ShowObjective(runStateManager.CurrentSector.SectorNumber,
            ProductionSectorCount, ResolveObjectiveKey(runFlow.Phase, exitAvailable), specialAvailable);
    }

    private void RefreshRunCounters()
    {
        if (timerText != null && runFlow != null)
        {
            int seconds = Mathf.CeilToInt(runFlow.ExitMinimumTimeRemaining);
            if (seconds != lastDisplayedTimerSecond)
            {
                lastDisplayedTimerSecond = seconds;
                timerText.SetText("TIME: {0}:{1:00}", seconds / 60, seconds % 60);
            }
        }
        // Read after gameplay Update: KillManager notifies before run stats are committed.
        SetKills(0);
    }

    private static void ConfigureIndicatorSlider(Slider slider)
    {
        if (slider == null)
            return;

        slider.interactable = false;

        Navigation navigation = slider.navigation;
        navigation.mode = Navigation.Mode.None;
        slider.navigation = navigation;
    }

    public void SetHealth(float currentHealth, float maxHealth)
    {
        if (healthSlider != null)
        {
            healthSlider.maxValue = maxHealth;
            healthSlider.value = currentHealth;
        }

        if (healthText != null)
        {
            healthText.text = $"{Mathf.CeilToInt(currentHealth)} / {Mathf.CeilToInt(maxHealth)}";
        }
        UpdateLowHpVignette(currentHealth, maxHealth);
    }

    public void SetThreat(float value, ThreatTier tier)
    {

        if (threatPanel == null)
            return;

        threatPanel.gameObject.SetActive(true);

        float clampedValue = Mathf.Clamp(value, 0f, 100f);

        if (threatLevelText != null && tier != lastDisplayedThreatTier)
        {
            lastDisplayedThreatTier = tier;
            threatLevelText.text =
                string.Format(LocalizationService.EnsureExists().Get("hud.threat"), ThreatTierPresentation.Format(tier));
            SetRect(
                threatLevelText.rectTransform,
                new Vector2(0f, 0.34f),
                Vector2.one,
                new Vector2(10f, 0f),
                new Vector2(-10f, -1f)
            );
        }

        if (threatValueText != null)
            threatValueText.gameObject.SetActive(false);

        if (threatFill != null)
        {
            Vector2 max = threatFill.anchorMax;
            max.x = clampedValue / 100f;
            threatFill.anchorMax = max;
        }
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    public void SetKills(int kills)
    {
        if (killsText != null)
        {
            RunStateManager runState = RunStateManager.Instance;
            killsText.text = (runState != null
                ? runState.GetCurrentRunKills()
                : 0).ToString();
        }
    }

    public void SetCurrentRunCurrency(int amount)
    {
        if (currencyText != null)
            currencyText.text = amount.ToString();
    }

    public bool IsPlayerBound { get; private set; }

    public void BindPlayer(GameObject player)
    {
        IsPlayerBound = player != null;
        boundPlayer = player != null ? player.GetComponent<PlayerHealth>() : null;
        interactionPrompt.Bind(player != null ? player.GetComponent<PlayerInteractor>() : null);
        CharacterMovement2D movement = player != null
            ? player.GetComponent<CharacterMovement2D>()
            : null;
        dashCooldownView?.Bind(movement);
        tacticalMap?.BindPlayer(player != null ? player.transform : null);
        targetTracker.BindPlayer(player != null ? player.transform : null);
    }

    public bool IsTacticalMapVisible =>
        tacticalMap != null && tacticalMap.IsVisible;

    public void SetTacticalMapVisible(bool visible)
    {
        tacticalMap?.SetVisible(visible);
    }

    private void RefreshRunCurrency()
    {
        RunStateManager runState = RunStateManager.Instance;
        int amount = runState != null
            ? runState.GetCurrentGoldReward(RunEndReason.ReturnedToBunker)
            : 0;

        SetCurrentRunCurrency(amount);
    }

    public void SetExperience(int currentExp, int requiredExp, int level)
    {
        if (experienceSlider != null)
        {
            experienceSlider.maxValue = requiredExp;
            experienceSlider.value = currentExp;
        }

        if (levelText != null)
        {
            displayedLevel = level;
            levelText.text = string.Format(LocalizationService.EnsureExists().Get("hud.level"), level);
        }

        if (experienceText != null)
        {
            experienceText.text = $"{currentExp} / {requiredExp}";
        }
    }
    private void UpdateLowHpVignette(float currentHealth, float maxHealth)
    {
        if (lowHpVignette == null || maxHealth <= 0f)
            return;

        float healthPercent = currentHealth / maxHealth;

        if (healthPercent > lowHpThreshold)
        {
            lowHpVignette.alpha = 0f;
            return;
        }

        float danger = 1f - (healthPercent / lowHpThreshold);
        lowHpVignette.alpha = Mathf.Lerp(0.15f, 0.55f, danger);
    }



    public void ShowBossHp(string bossName, float currentHp, float maxHp)
    {
        if (bossHpPanel != null)
            bossHpPanel.SetActive(true);

        displayedBossName = bossName;
        if (bossNameText != null)
            bossNameText.text = LocalizationService.EnsureExists().Get(bossName);

        UpdateBossHp(currentHp, maxHp);
    }

    public void UpdateBossHp(float currentHp, float maxHp)
    {
        if (bossHpSlider == null)
            return;

        bossHpSlider.maxValue = maxHp;
        bossHpSlider.value = currentHp;
    }

    public void HideBossHp()
    {
        if (bossHpPanel != null)
            bossHpPanel.SetActive(false);
    }
    public void HideLowHpVignette()
    {
        if (lowHpVignette != null)
            lowHpVignette.alpha = 0f;
    }

    public void ShowWorldEventMarker(Transform target, string label)
    {
        if (worldEventMarker != null)
            worldEventMarker.Show(target, label);
    }

    public void HideWorldEventMarker()
    {
        if (worldEventMarker != null)
            worldEventMarker.Hide();
    }

    public WorldEventMarker CreateWorldEventMarker(
        Transform target,
        string label)
    {
        if (worldEventMarker == null || target == null)
            return null;

        WorldEventMarker marker = Instantiate(
            worldEventMarker,
            worldEventMarker.transform.parent
        );
        marker.gameObject.SetActive(true);
        marker.Show(target, label);
        return marker;
    }

    public void RemoveWorldEventMarker(WorldEventMarker marker)
    {
        if (marker == null || marker == worldEventMarker)
            return;

        marker.Hide();
        Destroy(marker.gameObject);
    }

}
