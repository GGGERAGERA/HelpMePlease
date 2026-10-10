#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class FalseSignalTests
{
    [Test] public void EventPromptsHaveRussianAndEnglishTranslations()
    {
        var table = AssetDatabase.LoadAssetAtPath<LocalizationTable>("Assets/_Project/Data/Localization/LocalizationTable.asset");
        foreach (var language in new[] { GameLanguage.Russian, GameLanguage.English })
            foreach (string key in new[] { "event.signal.activate", "event.corridor.start" })
            {
                Assert.That(table.TryGet(key, language, out string text), Is.True, key);
                Assert.That(text, Is.Not.EqualTo(key));
            }
    }
    [Test] public void TransmitterUsesExplicitPlayerInteraction()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/Environment/WorldEvents/FalseSignalPoint.prefab");
        Assert.That(prefab.GetComponent<Interactable>(), Is.Not.Null,
            "A transmitter must be selected with the existing E interaction, never by walking into it.");
    }
    [Test] public void AuthoredTransmitterAndEntranceContainRenderableSprites()
    {
        foreach (string path in new[] {
            "Assets/_Project/prefabs/Environment/WorldEvents/FalseSignalPoint.prefab",
            "Assets/_Project/prefabs/Environment/WorldEvents/Corridor/PF_CorridorEvent.prefab" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            foreach (var renderer in prefab.GetComponentsInChildren<SpriteRenderer>(true))
                Assert.That(renderer.sprite, Is.Not.Null, path + ": " + renderer.name);
        }
    }
}
#endif
