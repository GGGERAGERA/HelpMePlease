using NUnit.Framework;
using UnityEngine;

public sealed class BunkerHoldInvestmentTests
{
    private const string GoldKey = "TOTAL_GOLD";
    private const string DamageLevelKey = "META_DAMAGE_LEVEL";
    private const string DamageInvestmentKey = "META_UPGRADE_INVESTED_Damage";
    private const string UpgradeStationLevelKey = "BunkerStationLevel_Upgrades";
    private const string AnomalyStationLevelKey = "BunkerStationLevel_Anomaly";
    private const string AnomalyId = "hold_test_anomaly";
    private const string AnomalyLevelKey = "META_ANOMALY_LEVEL_" + AnomalyId;
    private const string AnomalyInvestmentKey = "META_ANOMALY_INVESTED_" + AnomalyId;

    private readonly System.Collections.Generic.Dictionary<string, int?> backups = new();
    private GameObject currencyObject;
    private GameObject progressionObject;

    [SetUp]
    public void SetUp()
    {
        Backup(GoldKey);
        Backup(DamageLevelKey);
        Backup(DamageInvestmentKey);
        Backup(UpgradeStationLevelKey);
        Backup(AnomalyStationLevelKey);
        Backup(AnomalyLevelKey);
        Backup(AnomalyInvestmentKey);
        PlayerPrefs.SetInt(GoldKey, 0);
        PlayerPrefs.SetInt(DamageLevelKey, 0);
        PlayerPrefs.SetInt(DamageInvestmentKey, 0);
        PlayerPrefs.SetInt(UpgradeStationLevelKey, 1);
        PlayerPrefs.SetInt(AnomalyStationLevelKey, 2);
        PlayerPrefs.SetInt(AnomalyLevelKey, 1);
        PlayerPrefs.SetInt(AnomalyInvestmentKey, 0);
        PlayerPrefs.Save();

        currencyObject = new GameObject("TestCurrency");
        currencyObject.AddComponent<CurrencyManager>().AddGold(200);
        progressionObject = new GameObject("TestMetaProgression");
        progressionObject.AddComponent<MetaProgressionManager>();
    }

    [TearDown]
    public void TearDown()
    {
        if (progressionObject != null)
            Object.DestroyImmediate(progressionObject);
        if (currencyObject != null)
            Object.DestroyImmediate(currencyObject);
        foreach (var pair in backups)
        {
            if (pair.Value.HasValue)
                PlayerPrefs.SetInt(pair.Key, pair.Value.Value);
            else
                PlayerPrefs.DeleteKey(pair.Key);
        }
        PlayerPrefs.Save();
        backups.Clear();
    }

    [Test]
    public void MetaUpgradePersistsPartialInvestmentAndLevelsOnlyWhenFull()
    {
        MetaProgressionManager manager = MetaProgressionManager.Instance;

        Assert.That(manager.TryInvestGold(MetaUpgradeType.Damage, 40, out int first), Is.True);
        Assert.That(first, Is.EqualTo(40));
        Assert.That(manager.GetLevel(MetaUpgradeType.Damage), Is.Zero);
        Assert.That(manager.GetInvestedGold(MetaUpgradeType.Damage), Is.EqualTo(40));
        Assert.That(PlayerPrefs.GetInt(DamageInvestmentKey), Is.EqualTo(40));

        Assert.That(manager.TryInvestGold(MetaUpgradeType.Damage, 60, out int second), Is.True);
        Assert.That(second, Is.EqualTo(60));
        Assert.That(manager.GetLevel(MetaUpgradeType.Damage), Is.EqualTo(1));
        Assert.That(manager.GetInvestedGold(MetaUpgradeType.Damage), Is.Zero);
        Assert.That(CurrencyManager.Instance.TotalGold, Is.EqualTo(100));
    }

    [Test]
    public void AnomalyPersistsPartialInvestmentAndLevelsOnlyWhenFull()
    {
        AnomalyStabilizerData anomaly = ScriptableObject.CreateInstance<AnomalyStabilizerData>();
        SetField(anomaly, "id", AnomalyId);
        SetField(anomaly, "maxMetaLevel", 3);
        SetField(anomaly, "metaUpgradeCosts", new[] { 100, 250 });
        SetField(anomaly, "metaEffectValues", new[] { 1f, 1f, 1f });
        AnomalyMetaProgressionManager manager =
            progressionObject.AddComponent<AnomalyMetaProgressionManager>();

        Assert.That(manager.TryInvestGold(anomaly, 40, out int first), Is.True);
        Assert.That(first, Is.EqualTo(40));
        Assert.That(manager.GetLevel(anomaly), Is.EqualTo(1));
        Assert.That(manager.GetInvestedGold(anomaly), Is.EqualTo(40));
        Assert.That(PlayerPrefs.GetInt(AnomalyInvestmentKey), Is.EqualTo(40));

        Assert.That(manager.TryInvestGold(anomaly, 60, out int second), Is.True);
        Assert.That(second, Is.EqualTo(60));
        Assert.That(manager.GetLevel(anomaly), Is.EqualTo(2));
        Assert.That(manager.GetInvestedGold(anomaly), Is.Zero);
        Assert.That(CurrencyManager.Instance.TotalGold, Is.EqualTo(100));
        Object.DestroyImmediate(anomaly);
    }

    private void Backup(string key)
    {
        backups[key] = PlayerPrefs.HasKey(key) ? PlayerPrefs.GetInt(key) : null;
    }

    private static void SetField(object target, string name, object value)
    {
        target.GetType().GetField(
            name,
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic).SetValue(target, value);
    }
}
