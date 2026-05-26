using UnityEngine;

public class MetaUpgradeSummaryUI : MonoBehaviour
{
    [SerializeField] private Transform container;
    [SerializeField] private MetaUpgradeStatItemUI itemPrefab;

    [Header("Icons")]
    [SerializeField] private Sprite hpIcon;
    [SerializeField] private Sprite damageIcon;
    [SerializeField] private Sprite moveSpeedIcon;
    [SerializeField] private Sprite attackSpeedIcon;
    [SerializeField] private Sprite critDamageIcon;
    [SerializeField] private Sprite critChanceIcon;
    [SerializeField] private Sprite piercingIcon;
    [SerializeField] private Sprite multishotIcon;
    [SerializeField] private Sprite ricochetIcon;
    [SerializeField] private Sprite knockbackIcon;

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        Clear();

        if (MetaProgressionManager.Instance == null)
            return;

        AddIfOwned(hpIcon, "HP", MetaProgressionManager.Instance.HpLevel);
        AddIfOwned(moveSpeedIcon, "SPD", MetaProgressionManager.Instance.MoveSpeedLevel);
        AddIfOwned(damageIcon, "DMG", MetaProgressionManager.Instance.DamageLevel);
        AddIfOwned(attackSpeedIcon, "ATK SPD", MetaProgressionManager.Instance.AttackSpeedLevel);
        AddIfOwned(critDamageIcon, "CRIT DMG", MetaProgressionManager.Instance.CritDamageLevel);
        AddIfOwned(critChanceIcon, "CRIT %", MetaProgressionManager.Instance.CritChanceLevel);
        AddIfOwned(ricochetIcon, "RICO", MetaProgressionManager.Instance.RicochetLevel);
        AddIfOwned(piercingIcon, "PIERCE", MetaProgressionManager.Instance.PiercingLevel);
        AddIfOwned(multishotIcon, "BULLETS", MetaProgressionManager.Instance.MultishotLevel);
        AddIfOwned(knockbackIcon, "KNOCKBACK", MetaProgressionManager.Instance.KnockbackLevel);

    }

    private void AddIfOwned(Sprite icon, string upgradeName, int level)
    {
        if (level <= 0)
            return;

        MetaUpgradeStatItemUI item = Instantiate(itemPrefab, container);
        item.Setup(icon, upgradeName, level);
    }

    private void Clear()
    {
        foreach (Transform child in container)
        {
            Destroy(child.gameObject);
        }
    }
}