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
        if (MetaProgressionManager.Instance == null)
            return;

        int level = MetaProgressionManager.Instance.GetLevel(type);
        int maxLevel = MetaProgressionManager.Instance.MaxLevel;
        int cost = MetaProgressionManager.Instance.GetUpgradeCost(type);
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
        if (MetaProgressionManager.Instance == null)
            return;

        bool success = MetaProgressionManager.Instance.BuyUpgrade(type);

        if (!success)
            return;

        MetaUpgradeShopUI shop = GetComponentInParent<MetaUpgradeShopUI>();

        if (shop != null)
            shop.Refresh();
    }
}