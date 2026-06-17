using UnityEngine;

public class MetaUpgradeSummaryUI : MonoBehaviour
{
    [SerializeField] private Transform container;
    [SerializeField] private MetaUpgradeStatItemUI itemPrefab;

    [Header("Icons")]
    [SerializeField] private Sprite hpIcon;
    [SerializeField] private Sprite damageIcon;
    [SerializeField] private Sprite moveSpeedIcon;
    [SerializeField] private Sprite xpGainIcon;
    [SerializeField] private Sprite goldGainIcon;
    [SerializeField] private Sprite pickupRadiusIcon;

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
        AddIfOwned(damageIcon, "DMG", MetaProgressionManager.Instance.DamageLevel);
        AddIfOwned(moveSpeedIcon, "SPD", MetaProgressionManager.Instance.MoveSpeedLevel);
        AddIfOwned(xpGainIcon, "XP", MetaProgressionManager.Instance.XpGainLevel);
        AddIfOwned(goldGainIcon, "GOLD", MetaProgressionManager.Instance.GoldGainLevel);
        AddIfOwned(pickupRadiusIcon, "PICKUP", MetaProgressionManager.Instance.PickupRadiusLevel);
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
            Destroy(child.gameObject);
    }
}