using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class Phase7EProductionUpgradeValidation
{
    private const string StationLevelKey = "BunkerStationLevel_Upgrades";

    private static readonly string[] ProductionPaths =
    {
        "Assets/_Project/Scriptable Objects/Upgrade/Gray/Damage.asset",
        "Assets/_Project/Scriptable Objects/Upgrade/Gray/Fire Rate.asset",
        "Assets/_Project/Scriptable Objects/Upgrade/Gray/HP.asset",
        "Assets/_Project/Scriptable Objects/Upgrade/Gray/Move Speed.asset",
        "Assets/_Project/Scriptable Objects/Upgrade/Gray/XP Radius.asset",
        "Assets/_Project/Scriptable Objects/Upgrade/Gray/Crit Chance.asset",
        "Assets/_Project/Scriptable Objects/Upgrade/Blue/Crit Damage.asset",
        "Assets/_Project/Scriptable Objects/Upgrade/Blue/Hit Explosion Chance.asset",
        "Assets/_Project/Scriptable Objects/Upgrade/Production/Advanced/Pierce.asset",
        "Assets/_Project/Scriptable Objects/Upgrade/Production/Advanced/Ricochet.asset",
        "Assets/_Project/Scriptable Objects/Upgrade/Production/Build/Heavy Shot.asset",
        "Assets/_Project/Scriptable Objects/Upgrade/Production/Build/Overclock.asset"
    };

    [MenuItem("Tools/Subject 42/Validate Phase 7E Production Upgrades")]
    public static void Run()
    {
        bool hadStoredLevel = PlayerPrefs.HasKey(StationLevelKey);
        int storedLevel = PlayerPrefs.GetInt(StationLevelKey, 1);

        try
        {
            UpgradeData[] pool = LoadPool();
            ValidateWeaponPrefabs();
            ValidateAssets(pool);
            ValidateEligibilityMatrix(pool);
            ValidateExclusivity(pool);
            ValidateLevelTargets();
            ValidateProductionWeaponRuntime(pool);
            Debug.Log(
                "[Phase7EValidation] PASS: pool, capabilities, exclusivity " +
                "and level-target modifiers are valid.");
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

    private static UpgradeData[] LoadPool()
    {
        UpgradeData[] pool = new UpgradeData[ProductionPaths.Length];
        for (int i = 0; i < ProductionPaths.Length; i++)
        {
            pool[i] = AssetDatabase.LoadAssetAtPath<UpgradeData>(ProductionPaths[i]);
            Require(pool[i] != null, $"Missing production asset: {ProductionPaths[i]}");
        }

        return pool;
    }

    private static void ValidateAssets(UpgradeData[] pool)
    {
        Require(pool.Length == 12, "Production pool must contain 12 upgrades.");
        ValidateNew(pool[8], UpgradeType.Pierce, 3, 2,
            WeaponUpgradeCapability.Pierce, string.Empty);
        ValidateNew(pool[9], UpgradeType.Ricochet, 3, 2,
            WeaponUpgradeCapability.Ricochet, string.Empty);
        ValidateNew(pool[10], UpgradeType.HeavyShot, 5, 3,
            WeaponUpgradeCapability.None, "WeaponTempo");
        ValidateNew(pool[11], UpgradeType.Overclock, 5, 3,
            WeaponUpgradeCapability.None, "WeaponTempo");
    }

    private static void ValidateWeaponPrefabs()
    {
        WeaponData pistol = AssetDatabase.LoadAssetAtPath<WeaponData>(
            "Assets/_Project/Scriptable Objects/Weapon/Pistol.asset");
        WeaponData laser = AssetDatabase.LoadAssetAtPath<WeaponData>(
            "Assets/_Project/Scriptable Objects/Weapon/LaserCannon.asset");
        Require(pistol != null && pistol.weaponPrefab != null,
            "Pistol WeaponData/prefab is missing.");
        Require(laser != null && laser.weaponPrefab != null,
            "Laser WeaponData/prefab is missing.");

        BaseWeapon pistolWeapon =
            pistol.weaponPrefab.GetComponentInChildren<BaseWeapon>(true);
        BaseWeapon laserWeapon =
            laser.weaponPrefab.GetComponentInChildren<BaseWeapon>(true);
        WeaponUpgradeCapability projectileRequirements =
            WeaponUpgradeCapability.Pierce |
            WeaponUpgradeCapability.Ricochet;
        Require(pistolWeapon != null &&
                (pistolWeapon.UpgradeCapabilities & projectileRequirements) ==
                projectileRequirements,
            "Pistol prefab must support Pierce and Ricochet.");
        Require(laserWeapon != null &&
                (laserWeapon.UpgradeCapabilities & projectileRequirements) == 0,
            "Laser prefab must not advertise Pierce or Ricochet.");
    }

    private static void ValidateNew(
        UpgradeData upgrade,
        UpgradeType type,
        int playerLevel,
        int stationLevel,
        WeaponUpgradeCapability capabilities,
        string exclusiveGroup)
    {
        Require(upgrade.upgradeType == type, $"{upgrade.name}: wrong UpgradeType.");
        Require(upgrade.category == UpgradeCategory.Behavior,
            $"{upgrade.name}: expected Behavior category.");
        Require(upgrade.minPlayerLevel == playerLevel,
            $"{upgrade.name}: wrong player-level gate.");
        Require(upgrade.requiredWeaponCapabilities == capabilities,
            $"{upgrade.name}: wrong weapon capabilities.");
        Require(string.Equals(
                upgrade.exclusiveGroup ?? string.Empty,
                exclusiveGroup,
                StringComparison.Ordinal),
            $"{upgrade.name}: wrong exclusive group.");
        Require(upgrade.unlockData != null && upgrade.unlockData.condition != null,
            $"{upgrade.name}: missing station unlock.");
        Require(upgrade.unlockData.condition.stationId == BunkerStationId.Upgrades &&
                upgrade.unlockData.condition.requiredAmount == stationLevel,
            $"{upgrade.name}: wrong station-level gate.");
    }

    private static void ValidateEligibilityMatrix(UpgradeData[] pool)
    {
        WeaponUpgradeCapability pistol =
            WeaponUpgradeCapability.Pierce |
            WeaponUpgradeCapability.Ricochet |
            WeaponUpgradeCapability.MultiProjectile;

        Require(Count(pool, 1, 1, pistol) == 6,
            "Pistol / Station Lv1 / Player Lv1 must expose 6 upgrades.");
        Require(Count(pool, 2, 3, pistol) == 10,
            "Pistol / Station Lv2 / Player Lv3 must expose 10 upgrades.");
        Require(Count(pool, 2, 3, WeaponUpgradeCapability.None) == 8,
            "Laser / Station Lv2 / Player Lv3 must expose 8 upgrades.");
        Require(Count(pool, 3, 5, pistol) == 12,
            "Pistol / Station Lv3 / Player Lv5 must expose 12 upgrades.");
        Require(Count(pool, 3, 5, WeaponUpgradeCapability.None) == 10,
            "Laser / Station Lv3 / Player Lv5 must expose 10 upgrades.");
    }

    private static int Count(
        UpgradeData[] pool,
        int stationLevel,
        int playerLevel,
        WeaponUpgradeCapability capabilities)
    {
        PlayerPrefs.SetInt(StationLevelKey, stationLevel);
        return new UpgradeRoller(pool, new RunItemSlots(), capabilities)
            .CountEligibleChoices(playerLevel);
    }

    private static void ValidateExclusivity(UpgradeData[] pool)
    {
        PlayerPrefs.SetInt(StationLevelKey, 3);
        WeaponUpgradeCapability pistol =
            WeaponUpgradeCapability.Pierce |
            WeaponUpgradeCapability.Ricochet;

        ValidateExclusiveDirection(pool, pool[10], pool[11], pistol);
        ValidateExclusiveDirection(pool, pool[11], pool[10], pistol);
    }

    private static void ValidateExclusiveDirection(
        UpgradeData[] pool,
        UpgradeData owned,
        UpgradeData blocked,
        WeaponUpgradeCapability capabilities)
    {
        RunItemSlots slots = new();
        Require(slots.TryAdd(owned) == ItemGrantResult.Added,
            $"Could not add {owned.name} for exclusivity validation.");
        List<UpgradeData> choices = new UpgradeRoller(pool, slots, capabilities)
            .RollChoices(5, 99);
        Require(choices.Contains(owned), $"Owned {owned.name} must remain levelable.");
        Require(!choices.Contains(blocked),
            $"{blocked.name} must be blocked by owned {owned.name}.");
    }

    private static void ValidateLevelTargets()
    {
        ValidateProfiles(
            UpgradeType.HeavyShot,
            new[] { 1.75f, 2.25f, 3f },
            new[] { 0.75f, 0.65f, 0.55f },
            new[] { 1.2f, 1.35f, 1.5f });
        ValidateProfiles(
            UpgradeType.Overclock,
            new[] { 0.8f, 0.7f, 0.6f },
            new[] { 1.5f, 1.9f, 2.4f },
            new[] { 1f, 1f, 1f });

        GameObject go = new("Phase7E_RuntimeStats_Test");
        WeaponData weapon = ScriptableObject.CreateInstance<WeaponData>();
        try
        {
            weapon.damage = 100;
            weapon.fireRate = 10f;
            WeaponRuntimeStats stats = go.AddComponent<WeaponRuntimeStats>();
            stats.InitializeFromWeaponData(weapon);

            for (int level = 1; level <= 3; level++)
            {
                WeaponTempoValues values =
                    WeaponTempoProfiles.Get(UpgradeType.HeavyShot, level);
                stats.SetTempoProfile(
                    values.DamageMultiplier,
                    values.FireRateMultiplier,
                    values.VisualScale);
                Require(stats.GetDamage(null) == Mathf.RoundToInt(100f * values.DamageMultiplier),
                    $"Heavy Shot Lv{level}: damage target was compounded.");
                Require(Approximately(
                        stats.GetShotsPerSecond(null),
                        10f * values.FireRateMultiplier),
                    $"Heavy Shot Lv{level}: fire-rate target was compounded.");
                Require(Approximately(stats.ShotVisualScale, values.VisualScale),
                    $"Heavy Shot Lv{level}: wrong visual scale.");
            }

            stats.SetPierceBonus(3);
            stats.SetRicochetBonus(3);
            Require(stats.Pierce == 3 && stats.Ricochet == 3,
                "Pierce/Ricochet level targets did not reach 3.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(weapon);
        }
    }

    private static void ValidateProductionWeaponRuntime(UpgradeData[] pool)
    {
        Require(GameObject.FindGameObjectWithTag("Player") == null,
            "Run Phase 7E validation outside Play Mode with no spawned Player.");

        WeaponData pistol = AssetDatabase.LoadAssetAtPath<WeaponData>(
            "Assets/_Project/Scriptable Objects/Weapon/Pistol.asset");
        WeaponData laser = AssetDatabase.LoadAssetAtPath<WeaponData>(
            "Assets/_Project/Scriptable Objects/Weapon/LaserCannon.asset");
        ValidateWeaponRuntime(pistol, pool, supportsProjectileEffects: true);
        ValidateWeaponRuntime(laser, pool, supportsProjectileEffects: false);
    }

    private static void ValidateWeaponRuntime(
        WeaponData data,
        UpgradeData[] pool,
        bool supportsProjectileEffects)
    {
        GameObject player = new($"Phase7E_{data.name}_Player");
        GameObject applierObject = new($"Phase7E_{data.name}_Applier");
        try
        {
            player.tag = "Player";
            GameObject weaponObject = UnityEngine.Object.Instantiate(
                data.weaponPrefab,
                player.transform);
            BaseWeapon weapon = weaponObject.GetComponentInChildren<BaseWeapon>(true);
            Require(weapon != null, $"{data.name}: BaseWeapon is missing.");
            weapon.Initialize(data);
            UpgradeApplier applier = applierObject.AddComponent<UpgradeApplier>();

            if (supportsProjectileEffects)
            {
                for (int level = 1; level <= 3; level++)
                {
                    Require(applier.Apply(pool[8], level),
                        $"Pistol Pierce Lv{level} failed to apply.");
                    Require(weapon.RuntimePierce == level,
                        $"Pistol Pierce Lv{level} produced {weapon.RuntimePierce}.");
                    Require(applier.Apply(pool[9], level),
                        $"Pistol Ricochet Lv{level} failed to apply.");
                    Require(weapon.RuntimeRicochet == level,
                        $"Pistol Ricochet Lv{level} produced {weapon.RuntimeRicochet}.");
                }
            }

            ValidateTempoRuntime(applier, weapon, data, pool[10]);
            ValidateTempoRuntime(applier, weapon, data, pool[11]);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(player);
            UnityEngine.Object.DestroyImmediate(applierObject);
        }
    }

    private static void ValidateTempoRuntime(
        UpgradeApplier applier,
        BaseWeapon weapon,
        WeaponData data,
        UpgradeData upgrade)
    {
        for (int level = 1; level <= 3; level++)
        {
            Require(applier.Apply(upgrade, level),
                $"{data.name} {upgrade.name} Lv{level} failed to apply.");
            WeaponTempoValues values =
                WeaponTempoProfiles.Get(upgrade.upgradeType, level);
            Require(weapon.GetDamage() ==
                    Mathf.RoundToInt(data.damage * values.DamageMultiplier),
                $"{data.name} {upgrade.name} Lv{level}: wrong damage.");
            Require(Approximately(
                    1f / weapon.GetAttackCooldown(),
                    data.fireRate * values.FireRateMultiplier),
                $"{data.name} {upgrade.name} Lv{level}: wrong attack rate.");
            Require(Approximately(
                    weapon.RuntimeShotVisualScale,
                    values.VisualScale),
                $"{data.name} {upgrade.name} Lv{level}: wrong visual scale.");
        }
    }

    private static void ValidateProfiles(
        UpgradeType type,
        float[] damage,
        float[] fireRate,
        float[] visualScale)
    {
        for (int i = 0; i < 3; i++)
        {
            WeaponTempoValues values = WeaponTempoProfiles.Get(type, i + 1);
            Require(Approximately(values.DamageMultiplier, damage[i]) &&
                    Approximately(values.FireRateMultiplier, fireRate[i]) &&
                    Approximately(values.VisualScale, visualScale[i]),
                $"{type} Lv{i + 1}: wrong target profile.");
        }
    }

    private static bool Approximately(float a, float b)
    {
        return Mathf.Abs(a - b) < 0.0001f;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
