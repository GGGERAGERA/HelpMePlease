using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System;

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

    [Header("Crit Chance")]
    [SerializeField] private TextMeshProUGUI CritChanceText;
    [SerializeField] private Button CritChanceBtn;

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

    [Header("Knockback")]
    [SerializeField] private TextMeshProUGUI KnockbackText;
    [SerializeField] private Button KnockbackButton;

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
        Buy(MetaProgressionManager.Instance.BuyHp);
    }

    public void BuyDamage()
    {
        Buy(MetaProgressionManager.Instance.BuyDamage);
    }

    public void BuyMoveSpeed()
    {
        Buy(MetaProgressionManager.Instance.BuyMoveSpeed);
    }

    public void BuyAttackSpeed()
    {

       Buy(MetaProgressionManager.Instance.BuyAttackSpeed);
    }
    public void BuyCritDamage()
    {
       Buy(MetaProgressionManager.Instance.BuyCritDamage);
    }
    public void BuyCritChance()
    {
        Buy(MetaProgressionManager.Instance.BuyCritChance);
    }
    public void BuyRicochet()
    {
        Buy(MetaProgressionManager.Instance.BuyRicochet);
    }
    public void BuyMultishot()
    {
        Buy(MetaProgressionManager.Instance.BuyMultishot);
       ;
    }
    public void BuyPiercing()
    {
        Buy(MetaProgressionManager.Instance.BuyPiercing);
    }
    public void BuyKnockback()
    {
        Buy(MetaProgressionManager.Instance.BuyKnockback);
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
        SetUpgradeText(CritChanceText, CritChanceBtn, "CRIT CHANCE", MetaProgressionManager.Instance.CritChanceLevel, gold);
        SetUpgradeText(RicochetText, RicochetButton, "RICOCHET", MetaProgressionManager.Instance.RicochetLevel, gold);
        SetUpgradeText(MultishotText, MultishotButton, "MULTISHOT", MetaProgressionManager.Instance.MultishotLevel, gold);
        SetUpgradeText(PierceText, PierceButton, "PIERCING", MetaProgressionManager.Instance.PiercingLevel, gold);
        SetUpgradeText(KnockbackText, KnockbackButton, "KNOCKBACK", MetaProgressionManager.Instance.KnockbackLevel, gold);

        if (summaryUI != null)
            summaryUI.Refresh();
    }

    private void SetUpgradeText(TextMeshProUGUI text, Button button, string name, int level, int gold)
    {
        int cost = MetaProgressionManager.Instance.GetUpgradeCost(level);

        string effect = "";

        if (name == "HP")
            effect = $"+{level * 5}";

        if (name == "DAMAGE")
            effect = $"+{level}";

        if (name == "MOVE SPEED")
            effect = $"+{level * 0.15f:0.00}";
        if (name == "ATTACK SPEED")
            effect = $"+{level * 0.1f:0.00}";
        if (name == "CRIT DAMAGE")
            effect = $"+{level * 0.5f:0.00}";
        if (name == "CRIT CHANCE")
            effect = $"+{level * 2}%";
        if (name == "RICOCHET")
            effect = $"+{level}";
        if (name == "MULTISHOT")
            effect = $"+{level}";
        if (name == "PIERCING")
            effect = $"+{level}";
        if (name == "KNOCKBACK")
            effect = $"+{level}";

        text.text =
            $"{name}\n" +
            $"lvl: {level}\n" +
            $"{effect}\n" +
            $"cost: {cost}";

        button.interactable = gold >= cost;
    }

    private void Buy(Func<bool> buyAction)
    {
        if (buyAction == null)
            return;

        bool success = buyAction.Invoke();

        if (success)
            PlayPurchaseSound();

        Refresh();
    }

    private void PlayPurchaseSound()
    {
        UISoundPlayer.Instance?.Play(purchaseSound, purchaseVolume);
    }

}