using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class HUDManager : MonoBehaviour
{
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


        HideLowHpVignette();
    }

    private void Start()
    {
        runStatsManager = RunStatsManager.Instance;
        runStateManager = RunStateManager.Instance;

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

    public void SetKills(int kills)
    {
        if (killsText != null)
        {
            killsText.text = $"KILLS: {kills}";
        }
    }

    public void SetCurrentRunCurrency(int amount)
    {
        if (currencyText != null)
            currencyText.text = $"GOLD: {amount}";
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
