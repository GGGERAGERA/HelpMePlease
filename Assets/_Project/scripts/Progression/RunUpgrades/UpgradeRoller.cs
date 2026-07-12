using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds upgrade choices from ScriptableObject data.
/// It owns rarity/level rules only. It does not apply upgrades and does not touch UI.
/// </summary>
public sealed class UpgradeRoller
{
    private readonly UpgradeData[] allUpgrades;

    public UpgradeRoller(UpgradeData[] allUpgrades)
    {
        this.allUpgrades = allUpgrades;
    }

    public List<UpgradeData> RollChoices(int playerLevel, int count)
    {
        List<UpgradeData> result = new List<UpgradeData>();

        if (allUpgrades == null || allUpgrades.Length == 0 || count <= 0)
            return result;

        List<UpgradeData> pool = BuildPool(playerLevel);

        while (result.Count < count && pool.Count > 0)
        {
            UpgradeRarity rarity = RollRarity(playerLevel);
            UpgradeData selected = PickRandomByRarity(pool, rarity);

            if (selected == null)
                selected = pool[Random.Range(0, pool.Count)];

            result.Add(selected);
            pool.Remove(selected);
        }

        return result;
    }

    private List<UpgradeData> BuildPool(int playerLevel)
    {
        List<UpgradeData> pool = new List<UpgradeData>();

        foreach (UpgradeData upgrade in allUpgrades)
        {
            if (upgrade == null)
                continue;

            if (playerLevel < upgrade.minPlayerLevel)
                continue;

            if (!IsRarityUnlocked(upgrade.rarity, playerLevel))
                continue;

            pool.Add(upgrade);
        }

        return pool;
    }

    private bool IsRarityUnlocked(UpgradeRarity rarity, int playerLevel)
    {
        return rarity switch
        {
            UpgradeRarity.Gray => true,
            UpgradeRarity.Blue => playerLevel >= 3,
            UpgradeRarity.Purple => playerLevel >= 6,
            UpgradeRarity.Legendary => playerLevel >= 10,
            _ => false
        };
    }

    private UpgradeRarity RollRarity(int playerLevel)
    {
        if (playerLevel < 3)
            return UpgradeRarity.Gray;

        if (playerLevel < 6)
            return Random.value < 0.75f ? UpgradeRarity.Gray : UpgradeRarity.Blue;

        if (playerLevel < 10)
        {
            float roll = Random.value;
            if (roll < 0.60f) return UpgradeRarity.Gray;
            if (roll < 0.90f) return UpgradeRarity.Blue;
            return UpgradeRarity.Purple;
        }

        float lateRoll = Random.value;
        if (lateRoll < 0.50f) return UpgradeRarity.Gray;
        if (lateRoll < 0.80f) return UpgradeRarity.Blue;
        if (lateRoll < 0.95f) return UpgradeRarity.Purple;
        return UpgradeRarity.Legendary;
    }

    private UpgradeData PickRandomByRarity(List<UpgradeData> pool, UpgradeRarity rarity)
    {
        List<UpgradeData> matching = new List<UpgradeData>();

        foreach (UpgradeData upgrade in pool)
        {
            if (upgrade.rarity == rarity)
                matching.Add(upgrade);
        }

        if (matching.Count == 0)
            return null;

        return matching[Random.Range(0, matching.Count)];
    }
}

