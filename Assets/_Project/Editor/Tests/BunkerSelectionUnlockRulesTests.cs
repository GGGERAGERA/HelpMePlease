using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public sealed class BunkerSelectionUnlockRulesTests
{
    [TestCase(1, 1, true)]
    [TestCase(1, 2, false)]
    [TestCase(2, 2, true)]
    [TestCase(3, 4, false)]
    [TestCase(1, 0, true)]
    public void VisibilityUsesNormalizedStationRequirement(
        int stationLevel,
        int requiredLevel,
        bool expected)
    {
        Assert.That(BunkerSelectionUnlockRules.IsVisible(
            stationLevel,
            requiredLevel), Is.EqualTo(expected));
    }

    [Test]
    public void UnlockDataProvidesConfiguredCharacterTier()
    {
        UnlockableContentData data = ScriptableObject.CreateInstance<UnlockableContentData>();
        data.condition = new UnlockConditionData
        {
            type = UnlockConditionType.StationLevelRequirement,
            stationId = BunkerStationId.Character,
            requiredAmount = 3
        };

        Assert.That(BunkerSelectionUnlockRules.GetRequiredStationLevel(
            data,
            BunkerStationId.Character), Is.EqualTo(3));
        Object.DestroyImmediate(data);
    }

    [Test]
    public void RequirementForAnotherStationDoesNotLeakContentIntoLevelOne()
    {
        UnlockableContentData data = ScriptableObject.CreateInstance<UnlockableContentData>();
        data.condition = new UnlockConditionData
        {
            type = UnlockConditionType.StationLevelRequirement,
            stationId = BunkerStationId.Character,
            requiredAmount = 2
        };

        int required = BunkerSelectionUnlockRules.GetRequiredStationLevel(
            data,
            BunkerStationId.Weapon);

        Assert.That(BunkerSelectionUnlockRules.IsVisible(3, required), Is.False);
        Object.DestroyImmediate(data);
    }

    [Test]
    public void NextUnlockTextUsesSameDescriptorListAsVisibility()
    {
        var unlocks = new List<BunkerSelectionUnlockModel>
        {
            new("Pistol", 1),
            new("LaserCannon", 2),
            new("Future", 4)
        };

        string text = BunkerSelectionUnlockRules.BuildNextUnlockText(unlocks, 1, 3);

        Assert.That(text, Does.Contain("LaserCannon"));
        Assert.That(text, Does.Not.Contain("Future"));
        Assert.That(BunkerSelectionUnlockRules.BuildNextUnlockText(unlocks, 3, 3),
            Is.EqualTo("ВСЕ ДОСТУПНЫЕ ОБЪЕКТЫ ОТКРЫТЫ"));
    }
}
