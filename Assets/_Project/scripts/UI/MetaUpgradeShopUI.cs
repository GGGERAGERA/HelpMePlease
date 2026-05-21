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

    public void Refresh()
    {
        if (CurrencyManager.Instance == null || MetaProgressionManager.Instance == null)
            return;

        int gold = CurrencyManager.Instance.TotalGold;

        goldText.text = $"GOLD: {gold}";

        SetUpgradeText(hpText, hpButton, "HP", MetaProgressionManager.Instance.HpLevel, gold);
        SetUpgradeText(damageText, damageButton, "DAMAGE", MetaProgressionManager.Instance.DamageLevel, gold);
        SetUpgradeText(moveSpeedText, moveSpeedButton, "MOVE SPEED", MetaProgressionManager.Instance.MoveSpeedLevel, gold);
    }

    private void SetUpgradeText(TextMeshProUGUI text, Button button, string name, int level, int gold)
    {
        int cost = MetaProgressionManager.Instance.GetUpgradeCost(level);

        string effect = "";

        if (name == "HP")
            effect = $"+{level * 5} ��������";

        if (name == "DAMAGE")
            effect = $"+{level} �����";

        if (name == "MOVE SPEED")
            effect = $"+{level * 0.15f:0.00} ��������";

        text.text =
            $"{name}\n" +
            $"��.: {level}\n" +
            $"{effect}\n" +
            $"����: {cost}";

        button.interactable = gold >= cost;
    }
}