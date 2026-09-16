#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

[Category("Core")]
public sealed class ProductionLocalizationTests
{
    [SetUp] public void PreservePreferences() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [UnityTest]
    public IEnumerator ProductionBunkerRefreshesRussianEnglishRussian()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return ExerciseLanguages();
    }

    private static IEnumerator ExerciseLanguages()
    {
        yield return CoreTestSupport.LoadBunker();
        var service = LocalizationService.EnsureExists();
        service.SetLanguage(GameLanguage.Russian);
        yield return null;
        var labels = Object.FindObjectsByType<LocalizedText>(FindObjectsSortMode.None)
            .Select(text => text.GetComponent<TMP_Text>()).Where(text => text != null).ToArray();
        Assert.That(labels, Is.Not.Empty, "Production bunker must contain active localized labels.");
        var russian = labels.Select(label => label.text).ToArray();
        service.SetLanguage(GameLanguage.English);
        yield return null;
        Assert.That(labels.Where((label, i) => label.text != russian[i]).Any(), Is.True,
            "Language switch must update authored labels.");
        Assert.That(labels.All(label => !string.IsNullOrWhiteSpace(label.text)), Is.True);
        service.SetLanguage(GameLanguage.Russian);
        yield return null;
        Assert.That(labels.Select(label => label.text), Is.EqualTo(russian));
    }
}
#endif
