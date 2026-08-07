using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class HUDManager : MonoBehaviour
{
    private const int TotalRouteSectors = RunRoute.TotalSectors;

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
    private RunStatsManager runStatsManager;
    private RunStateManager runStateManager;

    private void Awake()
    {
        Instance = this;
        ConfigureIndicatorSlider(healthSlider);
        ConfigureIndicatorSlider(experienceSlider);
        ConfigureIndicatorSlider(bossHpSlider);

        if (bossHpPanel != null)
            bossHpPanel.SetActive(false);

        if (tacticalMap == null)
            tacticalMap = GetComponent<TacticalMapHUD>();

        if (tacticalMap == null)
            tacticalMap = gameObject.AddComponent<TacticalMapHUD>();

        HideLowHpVignette();
        EnsureThreatView();
    }

    private void Start()
    {
        runStatsManager = RunStatsManager.Instance;
        runStateManager = RunStateManager.Instance;

        if (runStateManager != null && runStateManager.CurrentSector != null)
        {
            routeProgressView?.ShowCurrent(
                runStateManager.CurrentSector.SectorNumber,
                TotalRouteSectors
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

        int minutes = Mathf.FloorToInt(timeLeft / 60f);
        int seconds = Mathf.FloorToInt(timeLeft % 60f);

        if (timerText != null)
            timerText.text = $"{minutes:00}:{seconds:00}";
    }

    public void SetTimerVisible(bool visible)
    {
        if (timerText != null)
            timerText.gameObject.SetActive(visible);
    }

    public void SetThreat(float value, int level)
    {
        EnsureThreatView();

        if (threatPanel == null)
            return;

        threatPanel.gameObject.SetActive(true);

        float clampedValue = Mathf.Clamp(value, 0f, 100f);

        if (threatLevelText != null)
            threatLevelText.text = $"THREAT {ToRoman(level)}";

        if (threatValueText != null)
            threatValueText.text = $"{clampedValue:0}%";

        if (threatFill != null)
        {
            Vector2 max = threatFill.anchorMax;
            max.x = clampedValue / 100f;
            threatFill.anchorMax = max;
        }
    }

    private void EnsureThreatView()
    {
        if (threatPanel != null)
        {
            ConfigureThreatPanelRect();
            return;
        }

        GameObject panelObject = new(
            "ThreatPanel",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image),
            typeof(Outline)
        );
        panelObject.transform.SetParent(transform, false);
        panelObject.SetActive(false);

        threatPanel = panelObject.GetComponent<RectTransform>();
        ConfigureThreatPanelRect();

        Image panelImage = panelObject.GetComponent<Image>();
        panelImage.color = new Color(0.01f, 0.025f, 0.03f, 0.92f);
        panelImage.raycastTarget = false;

        Outline panelOutline = panelObject.GetComponent<Outline>();
        panelOutline.effectColor = new Color(0f, 0.75f, 0.78f, 0.9f);
        panelOutline.effectDistance = new Vector2(1f, -1f);

        threatLevelText = CreateThreatText(
            "ThreatLevel",
            threatPanel,
            TextAlignmentOptions.MidlineLeft
        );
        SetRect(
            threatLevelText.rectTransform,
            new Vector2(0f, 0.34f),
            new Vector2(0.72f, 1f),
            new Vector2(10f, 0f),
            new Vector2(-2f, -1f)
        );

        threatValueText = CreateThreatText(
            "ThreatValue",
            threatPanel,
            TextAlignmentOptions.MidlineRight
        );
        SetRect(
            threatValueText.rectTransform,
            new Vector2(0.72f, 0.34f),
            Vector2.one,
            Vector2.zero,
            new Vector2(-10f, -1f)
        );

        GameObject barObject = new(
            "ThreatBar",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        barObject.transform.SetParent(threatPanel, false);
        RectTransform barRect = barObject.GetComponent<RectTransform>();
        SetRect(
            barRect,
            new Vector2(0f, 0f),
            new Vector2(1f, 0f),
            new Vector2(10f, 8f),
            new Vector2(-10f, 16f)
        );
        Image barImage = barObject.GetComponent<Image>();
        barImage.color = new Color(0.08f, 0.12f, 0.13f, 1f);
        barImage.raycastTarget = false;

        GameObject fillObject = new(
            "Fill",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );
        fillObject.transform.SetParent(barRect, false);
        threatFill = fillObject.GetComponent<RectTransform>();
        threatFill.anchorMin = Vector2.zero;
        threatFill.anchorMax = new Vector2(0f, 1f);
        threatFill.offsetMin = Vector2.zero;
        threatFill.offsetMax = Vector2.zero;
        Image fillImage = fillObject.GetComponent<Image>();
        fillImage.color = new Color(0f, 0.88f, 0.82f, 1f);
        fillImage.raycastTarget = false;
    }

    private void ConfigureThreatPanelRect()
    {
        if (threatPanel == null)
            return;

        if (threatPanel.parent != transform)
            threatPanel.SetParent(transform, false);
        threatPanel.anchorMin = new Vector2(0f, 1f);
        threatPanel.anchorMax = new Vector2(0f, 1f);
        threatPanel.pivot = new Vector2(0f, 1f);
        threatPanel.anchoredPosition = new Vector2(40f, -207f);
        threatPanel.sizeDelta = new Vector2(240f, 50f);
    }

    private TextMeshProUGUI CreateThreatText(
        string objectName,
        Transform parent,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = new(
            objectName,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );
        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = timerText != null ? timerText.font : null;
        text.fontSize = 19f;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
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

    private static string ToRoman(int level)
    {
        return level switch
        {
            1 => "I",
            2 => "II",
            3 => "III",
            4 => "IV",
            5 => "V",
            _ => "VI"
        };
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
