#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Subject42.Combat.OrbitalStation;
using Object = UnityEngine.Object;

public sealed class SettingsPresentationTests
{
    const string Prefab = "Assets/_Project/prefabs/UI/SettingsPanel/SettingsPanel.prefab";
    const string Output = "Artifacts/SettingsQA";
    const string FireKey = "accessibility.weapon.autoAim";
    static readonly string[] FloatKeys = { AudioSettingsService.MasterVolumeKey, AudioSettingsService.MusicVolumeKey, AudioSettingsService.SoundsVolumeKey };

    [Test]
    public void SharedSettingsHasAuthoredControlsWithoutLegacySpritesOrAnimations()
    {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        Assert.That(root.GetComponent<SettingsPanelLayout>(), Is.Not.Null);
        foreach (var field in new[] { "masterSlider", "musicSlider", "soundsSlider", "languageDropdown", "automaticFireToggle", "backButton" })
            Assert.That(Get<Object>(root.GetComponent<AudioSettingsPanel>(), field), Is.Not.Null, field);
        var pause = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/UI/PausePanel.prefab");
        var reference = pause.GetComponentInChildren<TMP_Text>(true);
        foreach (var shell in new[] { root, pause })
        {
            Assert.That(shell.GetComponentsInChildren<Animator>(true), Is.Empty);
            Assert.That(shell.GetComponentsInChildren<Animation>(true), Is.Empty);
            foreach (var image in shell.GetComponentsInChildren<Image>(true)) Assert.That(image.sprite, Is.Null, image.name);
            foreach (var text in shell.GetComponentsInChildren<TMP_Text>(true))
            {
                Assert.That(text.font, Is.SameAs(reference.font), text.name);
                Assert.That(text.fontSharedMaterial, Is.SameAs(reference.fontSharedMaterial), text.name);
                Assert.That(new SerializedObject(text).FindProperty("m_characterHorizontalScale").floatValue, Is.EqualTo(1f), text.name + " must not inherit legacy condensed type");
            }
        }
    }

    [UnityTest]
    public IEnumerator BothProductionRoutesPreserveValuesNavigationAndPause()
    {
        Directory.CreateDirectory(Output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        var oldFloats = FloatKeys.Select(k => PlayerPrefs.GetFloat(k)).ToArray();
        var hadFloats = FloatKeys.Select(PlayerPrefs.HasKey).ToArray();
        string languageKey = LocalizationService.LanguagePreferenceKey;
        int oldLanguage = PlayerPrefs.GetInt(languageKey), oldFire = PlayerPrefs.GetInt(FireKey);
        bool hadLanguage = PlayerPrefs.HasKey(languageKey), hadFire = PlayerPrefs.HasKey(FireKey);
        try
        {
            SceneManager.LoadSceneAsync("MainMenu");
            yield return Await(() => SceneManager.GetActiveScene().name == "MainMenu" && One<BunkerContext>() != null);
            yield return Frames();
            var intro = One<BunkerIntroController>();
            if (intro != null) { intro.StopAllCoroutines(); Call(intro, "FinishIntro", false); }
            yield return Frames();
            var panels = One<BunkerPanelManager>();
            var open = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None).Single(b => b.name == "AudioSettingsButton");
            Click(open);
            var settings = One<AudioSettingsPanel>();
            Assert.That(settings.IsOpen, Is.True);
            Assert.That(panels.IsAnyPanelOpen, Is.True);
            foreach (var field in new[] { "masterSlider", "musicSlider", "soundsSlider" }) Get<Slider>(settings, field).value = .11f;
            Get<Slider>(settings, "masterSlider").value = .63f;
            Get<Slider>(settings, "musicSlider").value = .37f;
            Get<Slider>(settings, "soundsSlider").value = .82f;
            var dropdown = Get<TMP_Dropdown>(settings, "languageDropdown");
            dropdown.value = (int)GameLanguage.Russian;
            Get<Toggle>(settings, "automaticFireToggle").isOn = true;
            yield return Frames();
            Capture("1-bunker-settings");
            yield return Frames();
            // Open the real TMP popup, including its cloned item template and input blocker.
            ClickSelectable(dropdown);
            yield return Frames();
            Assert.That(settings.transform.GetComponentsInChildren<Toggle>().Length, Is.GreaterThan(1));
            Capture("4-language-dropdown");
            var english = settings.GetComponentsInChildren<Toggle>().Single(t => t.GetComponentInChildren<TMP_Text>()?.text == "English");
            ClickSelectable(english);
            yield return new WaitForSecondsRealtime(.25f);
            Assert.That(LocalizationService.Instance.CurrentLanguage, Is.EqualTo(GameLanguage.English));
            Capture("5-settings-english");
            yield return Frames();
            dropdown.value = (int)GameLanguage.Russian;
            Get<Toggle>(settings, "automaticFireToggle").isOn = false;
            Get<Toggle>(settings, "automaticFireToggle").isOn = true;
            yield return Frames();
            Click(Get<Button>(settings, "backButton"));
            Assert.That(settings.IsOpen, Is.False);
            Assert.That(panels.IsAnyPanelOpen, Is.False);
            CheckSaved();
            Click(open);
            CheckValues(settings);
            yield return Frames();
            Click(Get<Button>(settings, "backButton"));

            var starter = One<BunkerRunStarter>();
            var character = AssetDatabase.FindAssets("t:CharacterData").Select(id => AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(id))).First(c => c.characterPrefab != null);
            RunSelectionManager.Instance.SelectCharacter(character);
            starter.StartRun(Get<Transform>(starter, "cameraRig"));
            yield return Await(() => SceneManager.GetActiveScene().name == "MVP" && One<OrbitalStationRuntime>() != null && One<OrbitalStationRuntime>().IsInitialized);
            yield return Frames();
            var pause = One<PauseMenuUI>();
            Call(pause, "HandleEscape");
            Assert.That(pause.IsPaused, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            yield return Frames();
            Capture("2-gameplay-pause");
            yield return Frames();
            var overview = Get<PauseBuildOverview>(pause, "overview");
            Click(overview.GetComponentsInChildren<Button>().Single(b => b.name == "Settings"));
            settings = Get<AudioSettingsPanel>(pause, "audioSettingsPanel");
            Assert.That(settings.IsOpen, Is.True);
            Assert.That(Get<GameObject>(pause, "pausePanel").activeSelf, Is.False);
            CheckValues(settings);
            Assert.That(Time.timeScale, Is.Zero);
            yield return Frames();
            Capture("3-gameplay-settings");
            yield return Frames();
            Click(Get<Button>(settings, "backButton"));
            Assert.That(settings.IsOpen, Is.False);
            Assert.That(pause.IsPaused, Is.True);
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(Get<GameObject>(pause, "pausePanel").activeSelf, Is.True);
            yield return Frames();
            Click(overview.GetComponentsInChildren<Button>().Single(b => b.name == "Resume"));
            Assert.That(pause.IsPaused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            // Escape remains a settings Back action, rather than resuming the game underneath it.
            pause.Pause(); pause.OpenSettings(); Call(pause, "HandleEscape");
            Assert.That(pause.IsPaused, Is.True); Assert.That(settings.IsOpen, Is.False);
            pause.Resume();
            CheckSaved();
            File.WriteAllText(Output + "/routes.txt", "PASS: Bunker -> Settings -> Back -> reopen\nPASS: MVP -> Pause -> Settings -> Back -> Pause -> Resume\nPASS: settings Escape returns to Pause\nPASS: audio, language, automatic fire persistence across reopen and scene transition\nPASS: actual EventSystem raycasts for buttons and dropdown\n");
        }
        finally
        {
            var service = AudioSettingsService.Instance;
            if (service != null) { service.SetMasterVolume(oldFloats[0]); service.SetMusicVolume(oldFloats[1]); service.SetSoundsVolume(oldFloats[2]); service.Save(); }
            if (LocalizationService.Instance != null) LocalizationService.Instance.SetLanguage((GameLanguage)oldLanguage);
            WeaponControlSettings.SetMode((WeaponControlMode)oldFire);
            for (int i = 0; i < FloatKeys.Length; i++) { if (hadFloats[i]) PlayerPrefs.SetFloat(FloatKeys[i], oldFloats[i]); else PlayerPrefs.DeleteKey(FloatKeys[i]); }
            if (hadLanguage) PlayerPrefs.SetInt(languageKey, oldLanguage); else PlayerPrefs.DeleteKey(languageKey);
            if (hadFire) PlayerPrefs.SetInt(FireKey, oldFire); else PlayerPrefs.DeleteKey(FireKey);
            PlayerPrefs.Save();
            Time.timeScale = 1;
        }
        var completed = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(SceneManager.CreateScene("Settings QA completed"));
        yield return SceneManager.UnloadSceneAsync(completed);
        yield return new ExitPlayMode();
    }
    static void CheckValues(AudioSettingsPanel settings)
    {
        Assert.That(Get<Slider>(settings,"masterSlider").value, Is.EqualTo(.63f).Within(.001f));
        Assert.That(Get<Slider>(settings,"musicSlider").value, Is.EqualTo(.37f).Within(.001f));
        Assert.That(Get<Slider>(settings,"soundsSlider").value, Is.EqualTo(.82f).Within(.001f));
        Assert.That(Get<TMP_Dropdown>(settings,"languageDropdown").value, Is.EqualTo((int)GameLanguage.Russian));
        Assert.That(Get<Toggle>(settings,"automaticFireToggle").isOn, Is.True);
    }
    static void CheckSaved()
    {
        Assert.That(PlayerPrefs.GetFloat(FloatKeys[0]), Is.EqualTo(.63f).Within(.001f));
        Assert.That(PlayerPrefs.GetFloat(FloatKeys[1]), Is.EqualTo(.37f).Within(.001f));
        Assert.That(PlayerPrefs.GetFloat(FloatKeys[2]), Is.EqualTo(.82f).Within(.001f));
        Assert.That(PlayerPrefs.GetInt(LocalizationService.LanguagePreferenceKey), Is.EqualTo((int)GameLanguage.Russian));
        Assert.That(WeaponControlSettings.AutomaticFireEnabled, Is.True);
    }
    static void Capture(string name) => ScreenCapture.CaptureScreenshot(Path.GetFullPath(Output + "/" + name + ".png"));
    static IEnumerator Frames() { for (int i=0;i<12;i++) yield return null; }
    static IEnumerator Await(Func<bool> condition)
    {
        float deadline = Time.realtimeSinceStartup + 40;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(condition(), Is.True, "Production scene timed out");
    }
    static void Click(Button b) => ClickSelectable(b);
    static void ClickSelectable(Selectable s)
    {
        Canvas.ForceUpdateCanvases();
        var canvas=s.GetComponentInParent<Canvas>().rootCanvas;
        var rect=(RectTransform)s.transform;
        var pointer=new PointerEventData(EventSystem.current) {button=PointerEventData.InputButton.Left,
            position=RectTransformUtility.WorldToScreenPoint(canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera,rect.TransformPoint(rect.rect.center))};
        var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer,hits);
        Assert.That(hits.Count,Is.GreaterThan(0),s.name+" has no raycast target");
        Assert.That(hits[0].gameObject.GetComponentInParent<Selectable>(),Is.SameAs(s),s.name+" is occluded by "+hits[0].gameObject.name);
        ExecuteEvents.Execute(s.gameObject,pointer,ExecuteEvents.pointerClickHandler);
    }
    static T One<T>() where T:Object => Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    static T Get<T>(object target,string field) => (T)target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
    static void Call(object target,string method,params object[] args) => target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args);
}
#endif
