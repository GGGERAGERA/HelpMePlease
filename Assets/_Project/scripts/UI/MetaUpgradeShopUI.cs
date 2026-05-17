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
            effect = $"+{level * 5} здоровья";

        if (name == "DAMAGE")
            effect = $"+{level} урона";

        if (name == "MOVE SPEED")
            effect = $"+{level * 0.15f:0.00} скорости";

        text.text =
            $"{name}\n" +
            $"Ур.: {level}\n" +
            $"{effect}\n" +
            $"Цена: {cost}";

        button.interactable = gold >= cost;
    }
}