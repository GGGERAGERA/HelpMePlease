#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Subject42.Combat.OrbitalStation;
using Object = UnityEngine.Object;

public sealed partial class Subject42GoldenPathTests
{

    [Category("Extended")]
    [Test]
    public void RewardCheckRejectsDoubleGrantWithinOneCommit()
    {
        var check = typeof(GoldenPathResult).GetMethod("RewardDeltaIsExact");
        Assert.That(check, Is.Not.Null, "Exact reward delta validator is required");
        var before = OrbitalRunState.CreateDefault(1);
        var after = JsonUtility.FromJson<OrbitalRunState>(JsonUtility.ToJson(before));
        after.AddRing();
        Assert.That(check.Invoke(null, new object[] { before, after, OrbitalRewardKind.NewRing }), Is.EqualTo(true));
        after.AddRing();
        Assert.That(check.Invoke(null, new object[] { before, after, OrbitalRewardKind.NewRing }), Is.EqualTo(false));
    }

    [Category("Extended")]
    [Test]
    public void BaselineRejectsLeakedDamageAndRingPower()
    {
        var state = OrbitalRunState.CreateDefault(29);
        Assert.That(GoldenPathResult.BaselineMatches(state), Is.True);
        state.Modules[0].DamageLevel = 1;
        Assert.That(GoldenPathResult.BaselineMatches(state), Is.False);
        state.Modules[0].DamageLevel = 0;
        state.Rings[0].PowerUpgradeLevel = 1;
        Assert.That(GoldenPathResult.BaselineMatches(state), Is.False);
    }
}
#endif
