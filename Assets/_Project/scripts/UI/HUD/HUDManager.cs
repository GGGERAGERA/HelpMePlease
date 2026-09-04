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

    [Header("Low HP")]
    [SerializeField] private CanvasGroup lowHpVignette;
    [SerializeField] private float lowHpThreshold = 0.3f;

    [Header("Boss HP")]
    [SerializeField] private GameObject bossHpPanel;
    [SerializeField] private Slider bossHpSlider;
    [SerializeField] private TextMeshProUGUI bossNameText;

    [Header("World Event Marker")]
    [SerializeField] private WorldEventMarker worldEventMarker;

    [SerializeField] private RunMessageView runMessageView;
    [Header("Tactical Map")]
    [SerializeField] private TacticalMapHUD tacticalMap;
    [SerializeField] private AnomalySlotHUD anomalySlot;
    [SerializeField] private WorldLootRewardReel lootReel;
    private RunStatsManager runStatsManager;
    private RunStateManager runStateManager;
    private int lastDisplayedTimerSecond = int.MinValue;
    private ThreatTier? lastDisplayedThreatTier;

    private void Awake()
    {
        Instance = this;
        ConfigureIndicatorSlider(healthSlider);
        ConfigureIndicatorSlider(experienceSlider);
        ConfigureIndicatorSlider(bossHpSlider);

        if (bossHpPanel != null)
            bossHpPanel.SetActive(false);

        if (tacticalMap == null || anomalySlot == null || lootReel == null ||
            threatPanel == null || threatLevelText == null || threatValueText == null || threatFill == null)
        {
            Debug.LogError("[HUDManager] Authored map, anomaly, loot or threat references are missing.", this);
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

    public void SetTimer(float timeLeft)
    {
        timeLeft = Mathf.Max(0f, timeLeft);
        int totalSeconds = Mathf.FloorToInt(timeLeft);

        if (totalSeconds == lastDisplayedTimerSecond)
            return;

        lastDisplayedTimerSecond = totalSeconds;

        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;

        if (timerText != null)
            timerText.SetText("{0:00}:{1:00}", minutes, seconds);
    }

    public void SetTimerVisible(bool visible)
    {
        if (timerText != null)
            timerText.gameObject.SetActive(visible);
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
                $"THREAT {ThreatTierPresentation.Format(tier)}";
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
            killsText.text = kills.ToString();
        }
    }

    public void SetCurrentRunCurrency(int amount)
    {
        if (currencyText != null)
            currencyText.text = amount.ToString();
    }

    public void BindPlayer(GameObject player)
    {
        CharacterMovement2D movement = player != null
            ? player.GetComponent<CharacterMovement2D>()
            : null;
        dashCooldownView?.Bind(movement);
        tacticalMap?.BindPlayer(player != null ? player.transform : null);
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
            levelText.text = $"Lv. {level}";
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

        if (bossNameText != null)
            bossNameText.text = bossName;

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

    public void ShowRunMessage(string title, string description, float duration = 3f)
    {
        if (runMessageView != null)
            runMessageView.Show(title, description, duration);
    }
}
