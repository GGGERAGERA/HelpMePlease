#if UNITY_EDITOR
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;

public sealed class RunPressureTests
{
    private static RunThreatConfig Config => AssetDatabase.LoadAssetAtPath<RunThreatConfig>(
        "Assets/_Project/Data/World/RunThreatConfig.asset");

    // Catches hidden difficulty changes between the four visible stage boundaries.
    [TestCase(0f, 0, .87f, .3f, true)]
    [TestCase(1f, 0, .87f, .3f, true)]
    [TestCase(24.999f, 0, .87f, .3f, true)]
    [TestCase(25f, 1, .87f, .3f, false)]
    [TestCase(49.999f, 1, .87f, .3f, false)]
    [TestCase(50f, 3, 1f, 0f, false)]
    [TestCase(74.999f, 3, 1f, 0f, false)]
    [TestCase(75f, 4, 1f, 0f, false)]
    [TestCase(99.999f, 4, 1f, 0f, false)]
    [TestCase(100f, 4, 1f, 0f, false)]
    public void DifficultyIsConstantWithinFourStages(float pressure, int preset, float speed, float response, bool opening)
    {
        Assert.That(Config.GetPresetIndex(pressure), Is.EqualTo(preset));
        Assert.That(Config.MeleeSpeed(pressure), Is.EqualTo(speed).Within(.0001f));
        Assert.That(Config.MeleeResponseSeconds(pressure), Is.EqualTo(response).Within(.0001f));
        Assert.That(Config.IsOpening(pressure, 1000f), Is.EqualTo(opening));
    }

    [TestCase(30f, 7.5f)]
    [TestCase(60f, 15f)]
    [TestCase(90f, 22.5f)]
    [TestCase(300f, 75f)]
    public void DefaultRateSpansTheFiveSectorExpedition(float seconds, float percentage)
    {
        Assert.That(Config.ValuePerSecond * seconds, Is.EqualTo(percentage).Within(.0001f));
    }

    [Test]
    public void RetiredRouteGrowthDoesNotAdvertiseDifferentPressure()
    {
        var neutral = new RunConfig("map", "neutral", new RunConfigParameters());
        var legacyGrowth = new RunConfig("map", "legacy", new RunConfigParameters { threatGrowth = .75f });
        Assert.That(SurfaceMapBriefing.RiskDrivers(legacyGrowth), Is.EqualTo(SurfaceMapBriefing.RiskDrivers(neutral)));
        Assert.That(SurfaceMapBriefing.Feature(legacyGrowth), Is.EqualTo(SurfaceMapBriefing.Feature(neutral)));
    }

    [TestCase(24.999f, "24.9%", .24999f)]
    [TestCase(25f, "25%", .25f)]
    [TestCase(50f, "50%", .5f)]
    [TestCase(75f, "75%", .75f)]
    [TestCase(99.999f, "99.9%", .99999f)]
    [TestCase(100f, "100%", 1f)]
    public void MeterDoesNotRoundUpToTheNextStage(float pressure, string text, float fill)
    {
        var root = new GameObject("pressure meter test", typeof(RectTransform));
        try
        {
            var meter = RunPressureMeter.Create((RectTransform)root.transform, null, new Vector2(176f, 30f), "percent");
            meter.SetValue(pressure);
            Assert.That(meter.GetComponentInChildren<TMP_Text>().text, Is.EqualTo(text));
            Assert.That(((RectTransform)meter.transform.Find("Pressure track/Pressure fill")).anchorMax.x,
                Is.EqualTo(fill).Within(.00001f));
        }
        finally { Object.DestroyImmediate(root); }
    }
}
#endif
