using System;
using UnityEditor;
using UnityEngine;

public static class Phase7DUpgradeStationValidation
{
    private const string StationLevelKey = "BunkerStationLevel_Upgrades";

    private static readonly string[] CorePaths =
    {
        "Assets/_Project/Scriptable Objects/Upgrade/Gray/Damage.asset",
        "Assets/_Project/Scriptable Objects/Upgrade/Gray/Fire Rate.asset",
        "Assets/_Project/Scriptable Objects/Upgrade/Gray/HP.asset",
        "Assets/_Project/Scriptable Objects/Upgrade/Gray/Move Speed.asset",
        "Assets/_Project/Scriptable Objects/Upgrade/Gray/XP Radius.asset",
        "Assets/_Project/Scriptable Objects/Upgrade/Gray/Crit Chance.asset"
    };

    private static readonly string[] AdvancedPaths =
    {
        "Assets/_Project/Scriptable Objects/Upgrade/Blue/Crit Damage.asset",
        "Assets/_Project/Scriptable Objects/Upgrade/Blue/Hit Explosion Chance.asset"
    };

    [MenuItem("Tools/Subject 42/Validate Phase 7D Upgrade Station Tiers")]
    public static void Run()
    {
        bool hadStoredLevel = PlayerPrefs.HasKey(StationLevelKey);
        int storedLevel = PlayerPrefs.GetInt(StationLevelKey, 1);

        try
        {
            UpgradeData[] pool = LoadProductionPool();
            ValidateAssignments(pool);
            ValidateCase(pool, stationLevel: 1, playerLevel: 1, expected: 6);
            ValidateCase(pool, stationLevel: 1, playerLevel: 3, expected: 6);
            ValidateCase(pool, stationLevel: 2, playerLevel: 1, expected: 6);
            ValidateCase(pool, stationLevel: 2, playerLevel: 3, expected: 8);
            ValidateCase(pool, stationLevel: 3, playerLevel: 3, expected: 8);
            Debug.Log("[Phase7DValidation] PASS: station/player matrix is 6, 6, 6, 8, 8.");
        }
        finally
        {
            if (hadStoredLevel)
                PlayerPrefs.SetInt(StationLevelKey, storedLevel);
            else
                PlayerPrefs.DeleteKey(StationLevelKey);

            PlayerPrefs.Save();
        }
    }

    private static UpgradeData[] LoadProductionPool()
    {
        UpgradeData[] pool = new UpgradeData[CorePaths.Length + AdvancedPaths.Length];
        int index = 0;

        for (int i = 0; i < CorePaths.Length; i++)
            pool[index++] = Load(CorePaths[i]);
        for (int i = 0; i < AdvancedPaths.Length; i++)
            pool[index++] = Load(AdvancedPaths[i]);

        return pool;
    }

    private static UpgradeData Load(string path)
    {
        UpgradeData upgrade = AssetDatabase.LoadAssetAtPath<UpgradeData>(path);
        Require(upgrade != null, $"Missing production upgrade asset: {path}");
        return upgrade;
    }

    private static void ValidateAssignments(UpgradeData[] pool)
    {
        Require(pool.Length == 8, "Production pool must contain exactly eight upgrades.");

        for (int i = 0; i < pool.Length; i++)
        {
            UpgradeData upgrade = pool[i];
            int expectedStationLevel = i < CorePaths.Length ? 1 : 2;
            int expectedPlayerLevel = i < CorePaths.Length ? 1 : 3;

            Require(upgrade.unlockData != null, $"{upgrade.name}: unlockData is missing.");
            Require(upgrade.unlockData.condition != null,
                $"{upgrade.name}: unlock condition is missing.");
            Require(upgrade.unlockData.condition.type ==
                UnlockConditionType.StationLevelRequirement,
                $"{upgrade.name}: expected StationLevelRequirement.");
            Require(upgrade.unlockData.condition.stationId == BunkerStationId.Upgrades,
                $"{upgrade.name}: expected Upgrades station.");
            Require(upgrade.unlockData.condition.requiredAmount == expectedStationLevel,
                $"{upgrade.name}: expected station Lv{expectedStationLevel}.");
            Require(upgrade.minPlayerLevel == expectedPlayerLevel,
                $"{upgrade.name}: expected player Lv{expectedPlayerLevel}.");
        }
    }

    private static void ValidateCase(
        UpgradeData[] pool,
        int stationLevel,
        int playerLevel,
        int expected)
    {
        PlayerPrefs.SetInt(StationLevelKey, stationLevel);
        int actual = new UpgradeRoller(pool, new RunItemSlots())
            .CountEligibleChoices(playerLevel);
        Require(actual == expected,
            $"Station Lv{stationLevel} / Player Lv{playerLevel}: " +
            $"expected {expected}, got {actual}.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
