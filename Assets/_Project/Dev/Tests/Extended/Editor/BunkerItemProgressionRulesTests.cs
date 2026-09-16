using NUnit.Framework;

public sealed class BunkerItemProgressionRulesTests
{

    [Category("Extended")]
    [TestCase(1, 3)]
    [TestCase(2, 6)]
    [TestCase(3, 10)]
    public void UpgradeStationUsesProductionCapCurve(int stationLevel, int expectedCap)
    {
        Assert.That(BunkerItemProgressionRules.GetLevelCap(
            BunkerStationId.Upgrades,
            stationLevel,
            10), Is.EqualTo(expectedCap));
    }

    [Category("Extended")]
    [TestCase(1, 1)]
    [TestCase(2, 2)]
    [TestCase(3, 3)]
    public void AnomalyCapScalesConfiguredMaximum(int stationLevel, int expectedCap)
    {
        Assert.That(BunkerItemProgressionRules.GetLevelCap(
            BunkerStationId.Anomaly,
            stationLevel,
            3), Is.EqualTo(expectedCap));
    }
}
