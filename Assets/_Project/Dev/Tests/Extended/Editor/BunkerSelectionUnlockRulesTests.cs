using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class BunkerSelectionUnlockRulesTests
{

    [Category("Extended")]
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

    [Category("Extended")]
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

    [Category("Extended")]
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

    [Category("Extended")]
    [Test]
    public void NextUnlockTextUsesSameDescriptorListAsVisibility()
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var previousLocalization = LocalizationService.Instance;
        var instanceField = typeof(LocalizationService).GetField(
            "<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        try
        {
            var localizationObject = new GameObject("Test Localization");
            localizationObject.SetActive(false);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(localizationObject, scene);
            var localization = localizationObject.AddComponent<LocalizationService>();
            typeof(LocalizationService).GetField("table", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(localization, Resources.Load<LocalizationTable>("Localization/LocalizationTable"));
            // EditMode does not bootstrap the runtime service. Keep this fixture's
            // language isolated without writing the user's saved preference.
            typeof(LocalizationService).GetField("<CurrentLanguage>k__BackingField",
                BindingFlags.Instance | BindingFlags.NonPublic).SetValue(localization, GameLanguage.Russian);
            instanceField.SetValue(null, localization);
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
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
            instanceField.SetValue(null, previousLocalization);
        }
    }
}
