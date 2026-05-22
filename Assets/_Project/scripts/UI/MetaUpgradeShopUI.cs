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

    [Header("Crit Damage")]
    [SerializeField] private TextMeshProUGUI CritDamageText;
    [SerializeField] private Button CritDamageBtn;

    [Header("Crit Probability")]
    [SerializeField] private TextMeshProUGUI CritProbabilityText;
    [SerializeField] private Button CritProbabilityBtn;

    [Header("Pierce")]
    [SerializeField] private TextMeshProUGUI PierceText;
    [SerializeField] private Button PierceButton;

    [Header("Multishot")]
    [SerializeField] private TextMeshProUGUI MultishotText;
    [SerializeField] private Button MultishotButton;

    [Header("Attack Speed")]
    [SerializeField] private TextMeshProUGUI AttackSpeedText;
    [SerializeField] private Button AttackSpeedButton;

    [Header("Ricochet")]
    [SerializeField] private TextMeshProUGUI RicochetText;
    [SerializeField] private Button RicochetButton;

    private void Start()
    {
        Refresh();
    }

    public void BuyHp()
    {
        MetaProgressionManager.Instance.BuyHp();
        Refresh();
    }

    public void BuyDamage()
    {
        MetaProgressionManager.Instance.BuyDamage();
        Refresh();
    }

    public void BuyMoveSpeed()
    {
        MetaProgressionManager.Instance.BuyMoveSpeed();
        Refresh();
    }

    public void BuyAttackSpeed()
    {
        MetaProgressionManager.Instance.BuyAttackSpeed();
        Refresh();
    }
    public void BuyCritDamage()
    {
        MetaProgressionManager.Instance.BuyCritDamage();
        Refresh();
    }
    public void BuyCritProbability()
    {
        MetaProgressionManager.Instance.BuyCritProbability();
        Refresh();
    }
    public void BuyRicochet()
    {
        MetaProgressionManager.Instance.BuyRicochet();
        Refresh();
    }
    public void BuyMultishot()
    {
        MetaProgressionManager.Instance.BuyMultishot();
        Refresh();
    }
    public void BuyPiercing()
    {
        MetaProgressionManager.Instance.BuyPiercing();
        Refresh();
    }

    public void Refresh()
    {
        if (CurrencyManager.Instance == null || MetaProgressionManager.Instance == null)
            return;

        int gold = CurrencyManager.Instance.TotalGold;

        goldText.text = $"GOLD: {gold}";

        SetUpgradeText(hpText, hpButton, "HP", MetaProgressionManager.Instance.HpLevel, gold);
        SetUpgradeText(damageText, damageButton, "DAMAGE", MetaProgressionManager.Instance.DamageLevel, gold);
        SetUpgradeText(moveSpeedText, moveSpeedButton, "MOVE SPEED", MetaProgressionManager.Instance.MoveSpeedLevel, gold);
        SetUpgradeText(AttackSpeedText, AttackSpeedButton, "ATTACK SPEED", MetaProgressionManager.Instance.AttackSpeedLevel, gold);
        SetUpgradeText(CritDamageText, CritDamageBtn, "CRIT DAMAGE", MetaProgressionManager.Instance.CritDamageLevel, gold);
        SetUpgradeText(CritProbabilityText, CritProbabilityBtn, "CRIT PROBABILITY", MetaProgressionManager.Instance.CritProbabilityLevel, gold);
        SetUpgradeText(RicochetText, RicochetButton, "RICOCHET", MetaProgressionManager.Instance.RicochetLevel, gold);
        SetUpgradeText(MultishotText, MultishotButton, "MULTISHOT", MetaProgressionManager.Instance.MultishotLevel, gold);
        SetUpgradeText(PierceText, PierceButton, "PIERCING", MetaProgressionManager.Instance.PiercingLevel, gold);
    }

    private void SetUpgradeText(TextMeshProUGUI text, Button button, string name, int level, int gold)
    {
        int cost = MetaProgressionManager.Instance.GetUpgradeCost(level);

        string effect = "";

        if (name == "HP");
            effect = $"+{level * 5}";

        if (name == "DAMAGE")
            effect = $"+{level}";

        if (name == "MOVE SPEED")
            effect = $"+{level * 0.15f:0.00}";
        if (name == "ATTACK SPEED")
            effect = $"+{level * 0.1f:0.00}";
        if (name == "CRIT DAMAGE")
            effect = $"+{level * 0.5f:0.00}";
        if (name == "CRIT PROBABILITY")
            effect = $"+{level * 2}%";
        if (name == "RICOCHET")
            effect = $"+{level}";
        if (name == "MULTISHOT")
            effect = $"+{level}";
        if (name == "PIERCING")
            effect = $"+{level}";

        text.text =
            $"{name}\n" +
            $"lvl: {level}\n" +
            $"{effect}\n" +
            $"cost: {cost}";

        button.interactable = gold >= cost;
    }
}