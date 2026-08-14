using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds upgrade choices from ScriptableObject data.
/// It owns category/level rules only. It does not apply upgrades and does not touch UI.
/// </summary>
public sealed class UpgradeRoller
{
    private readonly UpgradeData[] allUpgrades;
    private readonly RunItemSlots itemSlotsOverride;
    private readonly bool useItemSlotsOverride;
    private readonly WeaponUpgradeCapability capabilityOverride;
    private readonly bool useCapabilityOverride;

    public UpgradeRoller(UpgradeData[] allUpgrades)
    {
        this.allUpgrades = allUpgrades;
    }

    /// <summary>
    /// Supplies an explicit slot state for deterministic validation tooling.
    /// Production uses the current RunStateManager through the main constructor.
    /// </summary>
    public UpgradeRoller(
        UpgradeData[] allUpgrades,
        RunItemSlots itemSlots)
    {
        this.allUpgrades = allUpgrades;
        itemSlotsOverride = itemSlots;
        useItemSlotsOverride = true;
    }

    public UpgradeRoller(
        UpgradeData[] allUpgrades,
        RunItemSlots itemSlots,
        WeaponUpgradeCapability capabilities)
    {
        this.allUpgrades = allUpgrades;
        itemSlotsOverride = itemSlots;
        useItemSlotsOverride = true;
        capabilityOverride = capabilities;
        useCapabilityOverride = true;
    }

    public List<UpgradeData> RollChoices(int playerLevel, int count)
    {
        List<UpgradeData> result = new List<UpgradeData>();

        if (allUpgrades == null || allUpgrades.Length == 0 || count <= 0)
            return result;

        List<UpgradeData> pool = BuildPool(playerLevel);

        while (result.Count < count && pool.Count > 0)
        {
            UpgradeCategory category = RollCategory();
            UpgradeData selected = PickRandomByCategory(pool, category);

            if (selected == null)
                selected = pool[Random.Range(0, pool.Count)];

            result.Add(selected);
            pool.Remove(selected);
        }

        return result;
    }

    public List<UpgradeData> RollRewardChoices(int playerLevel, int count)
    {
        List<UpgradeData> result = new List<UpgradeData>();

        if (count <= 0)
            return result;

        List<UpgradeData> pool = BuildPool(playerLevel);
        UpgradeData behavior = PickRandomByCategory(
            pool,
            UpgradeCategory.Behavior
        );

        if (behavior != null)
        {
            result.Add(behavior);
            pool.Remove(behavior);
        }

        while (result.Count < count && pool.Count > 0)
        {
            UpgradeData selected = pool[Random.Range(0, pool.Count)];

            result.Add(selected);
            pool.Remove(selected);
        }

        return result;
    }

    public List<UpgradeData> RollNumericChoices(int playerLevel, int count)
    {
        List<UpgradeData> pool = BuildPool(playerLevel);
        pool.RemoveAll(upgrade =>
            upgrade == null || upgrade.category != UpgradeCategory.Numeric
        );
        List<UpgradeData> result = new();

        while (result.Count < count && pool.Count > 0)
        {
            int index = Random.Range(0, pool.Count);
            result.Add(pool[index]);
            pool.RemoveAt(index);
        }

        return result;
    }

    public int CountEligibleChoices(int playerLevel)
    {
        return BuildPool(playerLevel).Count;
    }

    private List<UpgradeData> BuildPool(int playerLevel)
    {
        List<UpgradeData> pool = new List<UpgradeData>();
        if (allUpgrades == null)
            return pool;

        RunItemSlots itemSlots = useItemSlotsOverride
            ? itemSlotsOverride
            : RunStateManager.Instance != null
                ? RunStateManager.Instance.ItemSlots
                : null;
        WeaponUpgradeCapability capabilities = useCapabilityOverride
            ? capabilityOverride
            : WeaponUpgradeCapabilityResolver.GetCurrentCapabilities();

        foreach (UpgradeData upgrade in allUpgrades)
        {
            if (upgrade == null)
                continue;

            if (!UnlockProgressService.IsUnlockedNow(upgrade.unlockData))
                continue;

            if (playerLevel < upgrade.minPlayerLevel)
                continue;

            if (!UpgradeEligibilityRules.IsWeaponCompatible(
                    upgrade,
                    capabilities))
            {
                continue;
            }

            if (UpgradeEligibilityRules.HasExclusiveConflict(
                    upgrade,
                    itemSlots))
            {
                continue;
            }

            if (itemSlots != null && !itemSlots.CanAccept(upgrade))
                continue;

            pool.Add(upgrade);
        }

        return pool;
    }

    private UpgradeCategory RollCategory()
    {
        return Random.value < 0.75f
            ? UpgradeCategory.Numeric
            : UpgradeCategory.Behavior;
    }

    private UpgradeData PickRandomByCategory(
        List<UpgradeData> pool,
        UpgradeCategory category
    )
    {
        List<UpgradeData> matching = new List<UpgradeData>();

        foreach (UpgradeData upgrade in pool)
        {
            if (upgrade.category == category)
                matching.Add(upgrade);
        }

        if (matching.Count == 0)
            return null;

        return matching[Random.Range(0, matching.Count)];
    }
}

