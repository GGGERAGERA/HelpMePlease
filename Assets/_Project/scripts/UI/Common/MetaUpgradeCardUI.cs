using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MetaUpgradeCardUI : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private MetaUpgradeType type;
    [SerializeField] private string title;
    [SerializeField] private string description;
    [SerializeField] private string bonusFormat;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI currentBonusText;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private Button buyButton;

    public void Init(MetaUpgradeType upgradeType, string upgradeTitle, string upgradeDescription, string upgradeBonusFormat)
    {
        type = upgradeType;
        title = upgradeTitle;
        description = upgradeDescription;
        bonusFormat = upgradeBonusFormat;
    }

    private void Awake()
    {
        if (buyButton != null)
            buyButton.onClick.AddListener(Buy);
    }

    public void Refresh(int gold)
    {
        MetaProgressionManager progression =
            MetaProgressionManager.EnsureExists();

        progression.ReloadFromStorage();

        int level = progression.GetLevel(type);
        int maxLevel = progression.MaxLevel;
        int cost = progression.GetUpgradeCost(type);

        bool isMaxed = level >= maxLevel;

        if (titleText != null)
            titleText.text = title;

        if (levelText != null)
            levelText.text = $"LVL {level}/{maxLevel}";

        if (descriptionText != null)
            descriptionText.text = description;

        if (currentBonusText != null)
            currentBonusText.text = GetCurrentBonusText(level);

        if (costText != null)
            costText.text = isMaxed ? "MAX" : $"COST: {cost}";

        if (buyButton != null)
            buyButton.interactable = !isMaxed && gold >= cost;
    }

    private string GetCurrentBonusText(int level)
    {
        switch (type)
        {
            case MetaUpgradeType.Hp:
                return $"Current Bonus: +{level} HP";

            case MetaUpgradeType.Damage:
                return $"Current Bonus: +{level * 5}%";

            case MetaUpgradeType.MoveSpeed:
                return $"Current Bonus: +{level * 3}%";

            case MetaUpgradeType.XpGain:
                return $"Current Bonus: +{level * 5}%";

            case MetaUpgradeType.GoldGain:
                return $"Current Bonus: +{level * 10}%";

            case MetaUpgradeType.PickupRadius:
                return $"Current Bonus: +{level * 5}%";

            default:
                return "Current Bonus: -";
        }
    }

    private void Buy()
    {
        MetaProgressionManager progression =
            MetaProgressionManager.EnsureExists();

        if (!progression.BuyUpgrade(type))
        {
            AudioService.Instance?.Play(AudioCueId.PurchaseFail);
            return;
        }

        AudioService.Instance?.Play(AudioCueId.Purchase);
        MetaUpgradeShopUI shop =
            GetComponentInParent<MetaUpgradeShopUI>();

        shop?.Refresh();
    }
}
