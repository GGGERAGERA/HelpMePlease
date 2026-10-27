#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
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
            .Where(text => text.isActiveAndEnabled &&
                (text.GetComponent<TMP_Text>() != null || text.GetComponent<TextMesh>() != null)).ToArray();
        var localized = Object.FindObjectsByType<LocalizedText>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        TestContext.WriteLine($"Bunker LocalizedText (including inactive): {localized.Length}; showing at most 80.");
        foreach (var text in localized.OrderBy(text => DiagnosticPath(text)).Take(80))
        {
            var tmp = text.GetComponent<TMP_Text>();
            var mesh = text.GetComponent<TextMesh>();
            string value = tmp != null ? tmp.text : mesh != null ? mesh.text : "<no target>";
            TestContext.WriteLine($"{DiagnosticPath(text)} activeInHierarchy={text.gameObject.activeInHierarchy} " +
                $"enabled={text.enabled} localizationKey={new SerializedObject(text).FindProperty("localizationKey").stringValue} " +
                $"target={(tmp != null ? tmp.GetType().Name : mesh != null ? nameof(TextMesh) : "none")} " +
                $"text={DiagnosticText(value)}");
        }
        var panels = Object.FindObjectsByType<AudioSettingsPanel>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        TestContext.WriteLine($"Bunker AudioSettingsPanel (including inactive): {panels.Length}; showing at most 20.");
        foreach (var panel in panels.OrderBy(panel => DiagnosticPath(panel)).Take(20))
            TestContext.WriteLine($"{DiagnosticPath(panel)} activeSelf={panel.gameObject.activeSelf} " +
                $"activeInHierarchy={panel.gameObject.activeInHierarchy} enabled={panel.enabled}");
        Assert.That(labels, Is.Not.Empty, "Production bunker must contain active localized labels.");
        AssertTranslations(labels, service);
        var russian = labels.Select(LabelText).ToArray();
        service.SetLanguage(GameLanguage.English);
        yield return null;
        Assert.That(labels.Where((label, i) => LabelText(label) != russian[i]).Any(), Is.True,
            "Language switch must update authored labels.");
        AssertTranslations(labels, service);
        Assert.That(labels.All(label => !string.IsNullOrWhiteSpace(LabelText(label))), Is.True);
        service.SetLanguage(GameLanguage.Russian);
        yield return null;
        AssertTranslations(labels, service);
        Assert.That(labels.Select(LabelText), Is.EqualTo(russian));
    }

    private static string LabelText(LocalizedText label)
    {
        var tmp = label.GetComponent<TMP_Text>();
        return tmp != null ? tmp.text : label.GetComponent<TextMesh>().text;
    }

    private static void AssertTranslations(LocalizedText[] labels, LocalizationService service)
    {
        foreach (var label in labels)
        {
            string key = new SerializedObject(label).FindProperty("localizationKey").stringValue;
            Assert.That(string.IsNullOrWhiteSpace(key), Is.False, DiagnosticPath(label));
            Assert.That(service.HasKey(key), Is.True, key);
            Assert.That(string.IsNullOrWhiteSpace(LabelText(label)), Is.False, key);
            Assert.That(LabelText(label), Is.EqualTo(service.Get(key)), key);
        }
    }

    private static string DiagnosticPath(Component component)
    {
        string path = component.name;
        for (var parent = component.transform.parent; parent != null; parent = parent.parent)
            path = parent.name + "/" + path;
        return component.gameObject.scene.name + "/" + path;
    }

    private static string DiagnosticText(string value)
    {
        value = (value ?? "<null>").Replace("\r", "\\r").Replace("\n", "\\n");
        return value.Length <= 160 ? value : value.Substring(0, 160) + "…";
    }
}
#endif
