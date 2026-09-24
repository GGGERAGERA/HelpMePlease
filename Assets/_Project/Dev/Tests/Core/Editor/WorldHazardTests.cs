using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class WorldHazardTests
{
    [Test]
    public void SharedRocketImplementationExists()
    {
        Assert.That(typeof(BossRocketAttack).Assembly.GetType("RocketAttackRunner"),
            Is.Not.Null, "Boss and world hazards need one shared rocket implementation.");
    }

    [Test]
    public void RoutePresetsEscalateAndReuseBossAssets()
    {
        float previousDelay = float.MaxValue;
        var boss = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/Enemies/p_Boss1.prefab").GetComponent<BossRocketAttack>();
        var bossData = new SerializedObject(boss);
        for (int i = 1; i <= RunRoute.TotalSectors; i++)
        {
            var stage = AssetDatabase.LoadAssetAtPath<StageProfileData>(
                $"Assets/_Project/Data/Stages/StageProfiles/StageProfile_{i:00}.asset");
            var preset = stage.WorldHazards;
            Assert.That(preset, Is.Not.Null);
            Assert.That(preset.InitialDelay, Is.LessThan(previousDelay));
            previousDelay = preset.InitialDelay;
            Assert.That(preset.Attacks.Length, Is.EqualTo(1));
            Assert.That(preset.Attacks[0].IsConfigured, Is.True);
            var rocket = new SerializedObject(preset.Attacks[0]);
            foreach (string field in new[] { "rocketPrefab", "targetPrefab", "explosionPrefab" })
                Assert.That(rocket.FindProperty(field).objectReferenceValue,
                    Is.EqualTo(bossData.FindProperty(field).objectReferenceValue), field);
            Assert.That(rocket.FindProperty("warningDelay").floatValue, Is.GreaterThanOrEqualTo(1.5f));
        }
    }
}
