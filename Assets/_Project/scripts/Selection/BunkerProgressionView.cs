using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>Shared renderer for station and selected-item meta progression.</summary>
public class BunkerProgressionView : MonoBehaviour
{
    private const float InvestmentRate = 180f;

    [FormerlySerializedAs("stationName")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private Image[] progressSegments;
    [SerializeField] private Image[] progressFills;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private TextMeshProUGUI currencyText;
    [SerializeField] private TextMeshProUGUI bonusText;
    [FormerlySerializedAs("nextUnlockText")]
    [SerializeField] private TextMeshProUGUI contextText;
    [SerializeField] private TextMeshProUGUI stateText;
    [FormerlySerializedAs("investButton")]
    [SerializeField] private Button upgradeButton;
    [FormerlySerializedAs("investButtonText")]
    [SerializeField] private TextMeshProUGUI upgradeButtonText;

    private BunkerProgressionModel model;
    private HoldInvestmentInput holdInput;
    private bool investing;
    private bool investedDuringPress;
    private float accumulator;

    protected virtual void Awake()
    {
        if (upgradeButton != null)
        {
            upgradeButton.onClick.AddListener(Upgrade);
            holdInput = upgradeButton.GetComponent<HoldInvestmentInput>();
        }
        if (holdInput != null)
        {
            holdInput.PointerDown = BeginInvestment;
            holdInput.PointerUp = EndInvestment;
        }
    }

    protected virtual void OnDisable() => StopInvestment();

    protected virtual void OnDestroy()
    {
        upgradeButton?.onClick.RemoveListener(Upgrade);
        if (holdInput != null)
        {
            holdInput.PointerDown = null;
            holdInput.PointerUp = null;
        }
    }

    protected virtual void Update()
    {
        if (!investing || model == null)
            return;
        if (Time.timeScale <= 0f)
        {
            StopInvestment();
            return;
        }
        if (model.CanUpgrade == null || !model.CanUpgrade())
        {
            StopInvestment();
            return;
        }

        accumulator += Time.deltaTime * InvestmentRate;
        int amount = Mathf.FloorToInt(accumulator);
        if (amount <= 0)
            return;
        accumulator -= amount;
        model.Invest?.Invoke(amount);
        investedDuringPress = true;
    }

    public void Bind(BunkerProgressionModel value)
    {
        bool keepInvesting = investing && value != null && model != null &&
            !string.IsNullOrWhiteSpace(value.TargetId) &&
            value.TargetId == model.TargetId;
        if (!keepInvesting)
            StopInvestment();
        model = value;
        gameObject.SetActive(value != null);
        if (value == null)
            return;

        bool maxed = value.Level >= value.MaxLevel;
        bool locked = value.Locked;
        bool canUpgrade = !maxed && !locked && value.CanUpgrade != null && value.CanUpgrade();

        SetText(titleText, value.Title);
        SetText(levelText, $"{value.LevelPrefix} {value.Level} / {value.MaxLevel}");
        SetText(currencyText, maxed ? null : $"GOLD: {value.AvailableCurrency}");
        SetText(bonusText, value.BonusText);
        SetText(contextText, value.ContextText);
        SetText(stateText, maxed ? "МАКСИМАЛЬНЫЙ УРОВЕНЬ" : locked
            ? value.LockReason : !canUpgrade ? "НЕДОСТАТОЧНО ЗОЛОТА" : null);

        int required = Mathf.Max(0, value.RequiredProgress);
        int progress = Mathf.Clamp(value.Progress, 0, required);
        float fill = required > 0
            ? Mathf.Clamp01((float)progress / required)
            : Mathf.Clamp01((float)value.Level / Mathf.Max(1, value.MaxLevel));
        if (maxed)
            fill = 1f;
        SetText(progressText, required > 0 && !maxed ? $"{progress} / {required}" : null);
        RenderSegments(fill);

        if (upgradeButton != null)
        {
            upgradeButton.gameObject.SetActive(!maxed);
            upgradeButton.interactable = canUpgrade;
        }
        string price = value.SupportsPartialInvestment
            ? Mathf.Max(0, value.RequiredProgress - value.Progress).ToString()
            : Mathf.Max(0, value.Cost).ToString();
        SetText(upgradeButtonText, $"{value.ButtonText} — {price}");
    }

    private void Upgrade()
    {
        if (model == null || model.SupportsPartialInvestment || model.Locked ||
            model.Level >= model.MaxLevel || model.CanUpgrade == null || !model.CanUpgrade())
            return;
        model.Upgrade?.Invoke();
    }

    private void BeginInvestment()
    {
        if (model == null || !model.SupportsPartialInvestment || model.CanUpgrade == null ||
            !model.CanUpgrade())
            return;
        investing = true;
        investedDuringPress = false;
        accumulator = 0f;
    }

    private void EndInvestment()
    {
        if (investing && !investedDuringPress)
            model?.Invest?.Invoke(1);
        StopInvestment();
    }

    private void StopInvestment()
    {
        investing = false;
        accumulator = 0f;
    }

    public void CancelInvestment() => StopInvestment();

    private void RenderSegments(float fill)
    {
        int count = Mathf.Min(progressSegments?.Length ?? 0, progressFills?.Length ?? 0);
        for (int i = 0; i < count; i++)
        {
            if (progressSegments[i] != null)
                progressSegments[i].color = StationPixelVisuals.Window;
            Image segmentFill = progressFills[i];
            if (segmentFill == null)
                continue;
            float amount = Mathf.Clamp01(fill * count - i);
            segmentFill.gameObject.SetActive(amount > 0f);
            segmentFill.rectTransform.anchorMax = new Vector2(amount, 1f);
        }
    }

    private static void SetText(TextMeshProUGUI target, string value)
    {
        if (target == null)
            return;
        target.text = value ?? string.Empty;
        target.gameObject.SetActive(!string.IsNullOrWhiteSpace(value));
    }
}
