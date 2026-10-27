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
    private const string RunningKey = "Subject42.FontTests.Running";
    private const string AnyRunKey = "Subject42.Verification.AnyRunActive";

    static ProductionFontTests()
    {
        ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Results());
        EditorApplication.update += ConsumeRequest;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode)
                EditorApplication.delayCall += ConsumeRequest;
        };
    }

    private static void ConsumeRequest()
    {
        string request = Path.Combine(Output, "run-tests.request");
        if (!File.Exists(request) || SessionState.GetBool(AnyRunKey, false) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
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
            Assert.That(languageDropdown.captionText, Is.Not.Null, "Language dropdown must have a visible caption.");
            Assert.That(languageDropdown.captionText.text,
                Is.EqualTo(language == GameLanguage.Russian ? "Русский" : "English"),
                "Language caption must match the selected language.");
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
            var service = LocalizationService.EnsureExists();
            Assert.That(service.CurrentLanguage, Is.EqualTo(language), "Settings dropdown must switch the service language.");
            var localized = panel.GetComponentsInChildren<LocalizedText>()
                .Where(label => label.isActiveAndEnabled).ToArray();
            var keys = localized.Select(label => new SerializedObject(label)
                .FindProperty("localizationKey").stringValue).ToArray();
            Assert.That(keys.All(key => !string.IsNullOrWhiteSpace(key) && service.HasKey(key)), Is.True);
            Assert.That(SettingsContractMatches(keys,
                localized.Select(label => label.GetComponent<TMP_Text>()?.text).ToArray(), service.Get),
                Is.True, SettingsLabelDiagnostics(panel, labels));
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

    // Explicit authored contract, independent of the discovered components.
    private static readonly string[] SettingsKeys =
    {
        "settings.audio", "settings.master", "settings.music", "settings.sounds",
        "settings.back", "settings.backHint", "settings.system", "settings.language",
        "settings.savedHint", "settings.title", "settings.windowMode"
    };

    private static bool SettingsContractMatches(string[] keys, string[] values, Func<string, string> translate)
    {
        if (keys.Length != SettingsKeys.Length || values.Length != keys.Length) return false;
        foreach (string key in SettingsKeys)
        {
            if (keys.Count(candidate => candidate == key) != 1) return false;
            int index = Array.IndexOf(keys, key);
            if (string.IsNullOrWhiteSpace(values[index]) || values[index] != translate(key)) return false;
        }
        return true;
    }

    [Test]
    public void SettingsContractRejectsMissingDuplicateAndWrongTranslation()
    {
        string Translation(string key) => "translated:" + key;
        var keys = SettingsKeys.ToArray();
        var values = keys.Select(Translation).ToArray();
        Assert.That(SettingsContractMatches(keys, values, Translation), Is.True);
        Assert.That(SettingsContractMatches(keys.Skip(1).ToArray(), values.Skip(1).ToArray(), Translation), Is.False);
        var duplicate = keys.ToArray();
        duplicate[1] = duplicate[0];
        Assert.That(SettingsContractMatches(duplicate, duplicate.Select(Translation).ToArray(), Translation), Is.False);
        values[0] = "wrong translation";
        Assert.That(SettingsContractMatches(keys, values, Translation), Is.False);
    }

    private static string SettingsLabelDiagnostics(AudioSettingsPanel panel, TMP_Text[] labels)
    {
        string PathOf(Component component)
        {
            string path = component.name;
            for (var parent = component.transform.parent; parent != null; parent = parent.parent)
                path = parent.name + "/" + path;
            return component.gameObject.scene.name + "/" + path;
        }

        var localized = panel.GetComponentsInChildren<LocalizedText>(true);
        return $"Open panel {PathOf(panel)}; active TMP_Text={labels.Length}, LocalizedText including inactive={localized.Length}; " +
            "showing at most 60 of each.\nActive TMP_Text paths:\n" +
            string.Join("\n", labels.Where(label => label.gameObject.activeInHierarchy)
                .Select(label => PathOf(label)).OrderBy(path => path).Take(60)) +
            "\nLocalizedText keys:\n" + string.Join("\n", localized.OrderBy(label => PathOf(label)).Take(60)
                .Select(label => $"{PathOf(label)} activeInHierarchy={label.gameObject.activeInHierarchy} " +
                    $"key={new SerializedObject(label).FindProperty("localizationKey").stringValue}"));
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
        if (SessionState.GetBool(AnyRunKey, false)) return;
        SessionState.SetBool(AnyRunKey, true);
        SessionState.SetBool(RunningKey, true);
        SessionState.EraseString(BaselineKey);
        var api = ScriptableObject.CreateInstance<TestRunnerApi>();
        try { api.Execute(new ExecutionSettings(new Filter { testMode = UnityEditor.TestTools.TestRunner.Api.TestMode.EditMode,
            testNames = new[] { nameof(ProductionFontTests) } })); }
        catch { SessionState.EraseBool(AnyRunKey); SessionState.EraseBool(RunningKey); throw; }
    }

    private sealed class Results : IErrorCallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { SessionState.SetBool(AnyRunKey, true); }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            SessionState.EraseBool(AnyRunKey);
            if (!SessionState.GetBool(RunningKey, false)) return;
            SessionState.EraseBool(RunningKey);
            Directory.CreateDirectory(Output);
            TestRunnerApi.SaveResultToFile(result, Path.Combine(Output, "results.xml"));
            if (result.PassCount + result.FailCount == 0)
            {
                const string message = "FAIL: the request produced no passing or failing test cases. Check filter names and results.xml for skipped/inconclusive cases.";
                File.WriteAllText(Path.Combine(Output, "run-error.txt"), message);
                Debug.LogError(message);
            }
            else if (File.Exists(Path.Combine(Output, "run-error.txt"))) File.Delete(Path.Combine(Output, "run-error.txt"));
            Debug.Log($"ProductionFonts: {result.ResultState}, passed={result.PassCount}, failed={result.FailCount}");
        }
        public void OnError(string message)
        {
            SessionState.EraseBool(AnyRunKey);
            if (!SessionState.GetBool(RunningKey, false)) return;
            SessionState.EraseBool(RunningKey);
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, "run-error.txt"), message);
        }
    }
}
#endif
