using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MetaUpgradeShopUI : MonoBehaviour
{
    [Header("Gold")]
    [SerializeField] private TextMeshProUGUI goldText;

    [Header("HP")]
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private Button hpButton;

    [Header("Damage")]
    [SerializeField] private TextMeshProUGUI damageText;
    [SerializeField] private Button damageButton;

    [Header("Move Speed")]
    [SerializeField] private TextMeshProUGUI moveSpeedText;
    [SerializeField] private Button moveSpeedButton;

    [Header("XP Gain")]
    [SerializeField] private TextMeshProUGUI xpGainText;
    [SerializeField] private Button xpGainButton;

    [Header("Gold Gain")]
    [SerializeField] private TextMeshProUGUI goldGainText;
    [SerializeField] private Button goldGainButton;

    [Header("Pickup Radius")]
    [SerializeField] private TextMeshProUGUI pickupRadiusText;
    [SerializeField] private Button pickupRadiusButton;

    [Header("Sounds")]
    [SerializeField] private AudioClip purchaseSound;
    [SerializeField] private float purchaseVolume = 0.6f;

    [SerializeField] private MetaUpgradeSummaryUI summaryUI;

    private void Start()
    {
        Refresh();
    }

    public void BuyHp()
    {
        Buy(MetaUpgradeType.Hp);
    }

    public void BuyDamage()
    {
        Buy(MetaUpgradeType.Damage);
    }

    public void BuyMoveSpeed()
    {
        Buy(MetaUpgradeType.MoveSpeed);
    }

    public void BuyXpGain()
    {
        Buy(MetaUpgradeType.XpGain);
    }

    public void BuyGoldGain()
    {
        Buy(MetaUpgradeType.GoldGain);
    }

    public void BuyPickupRadius()
    {
        Buy(MetaUpgradeType.PickupRadius);
    }

    public void Refresh()
    {
        if (CurrencyManager.Instance == null || MetaProgressionManager.Instance == null)
            return;

        int gold = CurrencyManager.Instance.TotalGold;

        if (goldText != null)
            goldText.text = $"GOLD: {gold}";

        SetUpgradeText(hpText, hpButton, MetaUpgradeType.Hp, "HP", "+1 HP / lvl", gold);
        SetUpgradeText(damageText, damageButton, MetaUpgradeType.Damage, "DAMAGE", "+5% / lvl", gold);
        SetUpgradeText(moveSpeedText, moveSpeedButton, MetaUpgradeType.MoveSpeed, "MOVE SPEED", "+3% / lvl", gold);
        SetUpgradeText(xpGainText, xpGainButton, MetaUpgradeType.XpGain, "XP GAIN", "+5% / lvl", gold);
        SetUpgradeText(goldGainText, goldGainButton, MetaUpgradeType.GoldGain, "GOLD GAIN", "+10% / lvl", gold);
        SetUpgradeText(pickupRadiusText, pickupRadiusButton, MetaUpgradeType.PickupRadius, "PICKUP RADIUS", "+5% / lvl", gold);

        if (summaryUI != null)
            summaryUI.Refresh();
    }

    private void SetUpgradeText(
        TextMeshProUGUI text,
        Button button,
        MetaUpgradeType type,
        string upgradeName,
        string effectText,
        int gold
    )
    {
        if (text == null || button == null)
            return;

        int level = MetaProgressionManager.Instance.GetLevel(type);
        int maxLevel = MetaProgressionManager.Instance.MaxLevel;
        int cost = MetaProgressionManager.Instance.GetUpgradeCost(type);

        bool isMaxed = level >= maxLevel;

        text.text =
            $"{upgradeName}\n" +
            $"lvl: {level}/{maxLevel}\n" +
            $"{effectText}\n" +
            (isMaxed ? "MAX" : $"cost: {cost}");

        button.interactable = !isMaxed && gold >= cost;
    }

    private void Buy(MetaUpgradeType type)
    {
        if (MetaProgressionManager.Instance == null)
            return;

        bool success = MetaProgressionManager.Instance.BuyUpgrade(type);

        if (success)
            PlayPurchaseSound();

        Refresh();
    }

    private void PlayPurchaseSound()
    {
        UISoundPlayer.Instance?.Play(purchaseSound, purchaseVolume);
    }
}