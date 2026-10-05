#if UNITY_EDITOR
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEngine;

public sealed class ControlOnboardingTests
{
    [Test]
    public void MetaCompletionPersistsPerIdAndPublishesOnlyOnce()
    {
        const string id = "Verification.ControlOnboarding";
        string key = MetaProgressionManager.TutorialCompletionKeyPrefix + id;
        bool existed = PlayerPrefs.HasKey(key);
        int previous = PlayerPrefs.GetInt(key);
        var owner = new GameObject("Onboarding Meta Test");
        try
        {
            PlayerPrefs.DeleteKey(key);
            var meta = owner.AddComponent<MetaProgressionManager>();
            int completed = 0;
            meta.TutorialCompleted += _ => completed++;
            Assert.That(meta.IsTutorialCompleted(id), Is.False);
            Assert.That(meta.CompleteTutorial(id), Is.True);
            Assert.That(meta.CompleteTutorial(id), Is.False);
            Assert.That(completed, Is.EqualTo(1));
            meta.ReloadFromStorage();
            Assert.That(meta.IsTutorialCompleted(id), Is.True);
            Assert.That(PlayerPrefs.GetInt(key), Is.EqualTo(1));
            Assert.That(PlayerPrefs.HasKey(key + ".Other"), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            if (existed) PlayerPrefs.SetInt(key, previous); else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }

    [Test]
    public void ShiftClampsDiagonalAndReturnsWithoutDirectionOrModifier()
    {
        var offset = OrbitalCenterShift.AdvanceOffset(Vector2.zero, Vector2.one, true, 1f, 2f, 6f, 8f);
        Assert.That(offset.magnitude, Is.EqualTo(2f).Within(.0001f));
        Assert.That(offset.x, Is.EqualTo(offset.y).Within(.0001f));
        Assert.That(OrbitalCenterShift.AdvanceOffset(offset, Vector2.zero, true, .1f, 2f, 6f, 8f).magnitude,
            Is.EqualTo(1.2f).Within(.0001f));
        Assert.That(OrbitalCenterShift.AdvanceOffset(offset, Vector2.right, false, 1f, 2f, 6f, 8f), Is.EqualTo(Vector2.zero));
        Assert.That(OrbitalCenterShift.AdvanceOffset(offset, Vector2.right, true, 0f, 2f, 6f, 8f), Is.EqualTo(offset));
    }

    [Test]
    public void ProductionHintsAndStationHaveAuthoredDependencies()
    {
        var station = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Orbital/Core/Station.prefab");
        Assert.That(station.GetComponent<OrbitalStationView>().CenterShift, Is.Not.Null);
        foreach (var name in new[] { "BunkerControls", "OrbitalControls" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Onboarding/" + name + ".prefab");
            Assert.That(prefab.GetComponent<ControlOnboarding>(), Is.Not.Null);
            foreach (var hint in prefab.GetComponentsInChildren<OnboardingHint>())
            {
                Assert.That(hint.Definition, Is.Not.Null);
                Assert.That(hint.GetComponent<SpriteRenderer>().sprite, Is.Not.Null);
            }
        }
    }
}
#endif
