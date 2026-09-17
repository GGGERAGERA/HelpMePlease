#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

[Category("Core")]
[InitializeOnLoad]
public sealed class ProductionFontTests
{
    private const string FontPath = "Assets/_Project/Fonts/Subject42 UI SDF.asset";
    private const string Output = "Artifacts/GeneratedQA/ProductionFonts";
    private const string BaselineKey = "Subject42.FontTests.Baseline";

    static ProductionFontTests()
    {
        ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Results());
        EditorApplication.delayCall += ConsumeRequest;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += ConsumeRequest;
        };
    }

    private static void ConsumeRequest()
    {
        string request = Path.Combine(Output, "run-tests.request");
        if (!File.Exists(request)) return;
        if (EditorApplication.isPlaying)
        {
            EditorApplication.isPlaying = false;
            return;
        }
        File.Delete(request);
        Run();
    }

    [SetUp] public void PreservePreferences() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [Test]
    public void ProductionFontContainsCyrillicAndLocalizationWithoutDynamicFallback()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        Assert.That(font, Is.Not.Null);
        Assert.That(TMP_Settings.defaultFontAsset, Is.EqualTo(font));
        Assert.That(font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static));
        Assert.That(font.fallbackFontAssetTable, Is.Empty);
        foreach (int code in Enumerable.Range(32, 95).Concat(Enumerable.Range(0x400, 256)))
            Assert.That(font.HasCharacter(code), Is.True, $"Missing U+{code:X4}");
        var table = new SerializedObject(AssetDatabase.LoadAssetAtPath<LocalizationTable>(
            "Assets/_Project/Data/Localization/LocalizationTable.asset"));
        var entries = table.FindProperty("entries");
        for (int i = 0; i < entries.arraySize; i++)
            foreach (string language in new[] { "ru", "en" })
                foreach (char c in entries.GetArrayElementAtIndex(i).FindPropertyRelative(language).stringValue)
                    if (!char.IsControl(c))
                        Assert.That(font.HasCharacter((int)c), Is.True, $"{language}: missing U+{(int)c:X4}");
        var intro = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Fonts/Subject42 Intro SDF.asset");
        Assert.That(intro.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static));
        Assert.That(intro.fallbackFontAssetTable, Is.EqualTo(new[] { font }));
    }

    [UnityTest]
    public IEnumerator SettingsRussianEnglishRussianDoesNotChangeFontAssets()
    {
        if (string.IsNullOrEmpty(SessionState.GetString(BaselineKey, "")))
            SessionState.SetString(BaselineKey, FontHashes());
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return CoreTestSupport.LoadBunker();
        var panel = Object.FindFirstObjectByType<AudioSettingsPanel>(FindObjectsInactive.Include);
        Assert.That(panel, Is.Not.Null);
        panel.Open();
        Directory.CreateDirectory(Output);
        foreach (var language in new[] { GameLanguage.Russian, GameLanguage.English, GameLanguage.Russian })
        {
            var languageDropdown = new SerializedObject(panel).FindProperty("languageDropdown")
                .objectReferenceValue as TMP_Dropdown;
            languageDropdown.value = (int)language;
            for (int frame = 0; frame < 5; frame++) yield return null;
            Canvas.ForceUpdateCanvases();
            var labels = panel.GetComponentsInChildren<TMP_Text>();
            Assert.That(labels, Is.Not.Empty);
            foreach (var label in labels)
            {
                Assert.That(label.font, Is.EqualTo(TMP_Settings.defaultFontAsset), label.name);
                label.ForceMeshUpdate();
                if (label.textInfo.characterCount > 0)
                    Assert.That(label.textInfo.characterInfo[0].bottomLeft.x,
                        Is.GreaterThanOrEqualTo(label.rectTransform.rect.xMin - 4f),
                        $"{label.name}: first glyph extends past the text rectangle: {label.text}");
                for (int i = 0; i < label.textInfo.characterCount; i++)
                {
                    var character = label.textInfo.characterInfo[i];
                    if (char.IsWhiteSpace(character.character)) continue;
                    Assert.That(character.fontAsset, Is.EqualTo(label.font), label.text);
                    Assert.That(character.textElement.unicode, Is.EqualTo((uint)character.character), label.text);
                    Assert.That(character.isVisible, Is.True, label.text);
                }
            }
            if (language == GameLanguage.Russian)
            {
                string text = string.Join("\n", labels.Select(label => label.text)).ToUpperInvariant();
                foreach (string expected in new[] { "НАСТРОЙКИ", "ЗВУК", "ОБЩАЯ ГРОМКОСТЬ", "МУЗЫКА", "ЗВУКИ",
                    "УПРАВЛЕНИЕ", "АВТОМАТИЧЕСКАЯ СТРЕЛЬБА", "ЯЗЫК", "РУССКИЙ", "РЕЖИМ ЭКРАНА", "НАЗАД" })
                    Assert.That(text, Does.Contain(expected));
            }
            ScreenCapture.CaptureScreenshot(Path.Combine(Output, language + ".png"));
            for (int frame = 0; frame < 5; frame++) yield return null;
        }
        panel.Close();
        yield return new ExitPlayMode();
        AssetDatabase.SaveAssets();
        Assert.That(FontHashes(), Is.EqualTo(SessionState.GetString(BaselineKey, "")),
            "Opening RU/EN Settings and exiting Play Mode modified a font asset.");
        SessionState.EraseString(BaselineKey);
    }

    private static string FontHashes()
    {
        using var sha = SHA256.Create();
        return string.Join("\n", new[] { "Assets/_Project/Fonts", "Assets/TextMesh Pro" }
            .SelectMany(path => Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => path + ":" + Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)))));
    }

    [MenuItem("Tools/Subject42/Verify Production Fonts")]
    public static void Run()
    {
        SessionState.SetBool("Subject42.FontTests.Running", true);
        SessionState.EraseString(BaselineKey);
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.Execute(new ExecutionSettings(new Filter { testMode = UnityEditor.TestTools.TestRunner.Api.TestMode.EditMode,
            testNames = new[] { nameof(ProductionFontTests) } }));
    }

    private sealed class Results : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            if (!SessionState.GetBool("Subject42.FontTests.Running", false)) return;
            SessionState.EraseBool("Subject42.FontTests.Running");
            Directory.CreateDirectory(Output);
            TestRunnerApi.SaveResultToFile(result, Path.Combine(Output, "results.xml"));
            Debug.Log($"ProductionFonts: {result.ResultState}, passed={result.PassCount}, failed={result.FailCount}");
        }
    }
}
#endif
