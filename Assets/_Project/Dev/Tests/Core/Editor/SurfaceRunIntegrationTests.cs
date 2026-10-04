#if UNITY_EDITOR
using NUnit.Framework;
public sealed class SurfaceRunIntegrationTests
{
    [Test] public void ConfigRuleIsInitialOnlyAndMultipliersSurviveInternalStageChange()
    {
        var rule = UnityEngine.ScriptableObject.CreateInstance<WorldRuleData>();
        var stage = UnityEngine.ScriptableObject.CreateInstance<StageProfileData>();
        try
        {
            var config = new RunConfig("map","node",new RunConfigParameters { worldRule = rule, experience = 1.4f, spawnPressure = .8f });
            var sector = new RunSector(2,stage,null,null);
            typeof(RunSector).GetMethod("ApplyRunConfig",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(sector,new object[]{config});
            Assert.That(sector.WorldRule,Is.Null,"Initial rule must not override the next location choice");
            Assert.That(sector.ExperienceGainMultiplier,Is.EqualTo(stage.ExperienceGainMultiplier * 1.4f));
            Assert.That(sector.SpawnPressureMultiplier,Is.EqualTo(stage.SpawnPressureMultiplier * .8f));
        }
        finally { UnityEngine.Object.DestroyImmediate(rule); UnityEngine.Object.DestroyImmediate(stage); }
    }
    [Test] public void RunLifecycleCarriesConfig()
    {
        Assert.That(typeof(RunStateManager).GetProperty("CurrentConfig"), Is.Not.Null);
        Assert.That(typeof(BunkerRunStarter).GetMethod("StartSurfaceRun"), Is.Not.Null);
    }
}
#endif
