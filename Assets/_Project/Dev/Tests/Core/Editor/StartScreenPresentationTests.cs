#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class StartScreenPresentationTests
{
    [SetUp] public void PreservePreferences() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    private const string ScenePath = "Assets/_Project/Scenes/MainBuild/StartScreen.unity";
    private static Type PresentationType => typeof(StartScreenController).Assembly.GetType("StartScreenAtmosphere");

    [Test]
    public void MenuHasStaticPixelCyrillicAndSharedButtonPrefab()
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Fonts/Subject42 Menu Pixel.asset");
        Assert.That(font, Is.Not.Null, "Menu needs its own static pixel atlas, including Cyrillic");
        Assert.That(font.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static));
        Assert.That(font.fallbackFontAssetTable, Is.Empty);
        Assert.That(font.atlasTexture.filterMode, Is.EqualTo(FilterMode.Point));
        foreach (char c in "SUBJECT#42НАЧАТЬНАСТРОЙКИВЫХОДСТАТУС СУБЪЕКТА: АКТИВЕН")
            Assert.That(font.HasCharacter((int)c), Is.True, c.ToString());
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            var controller = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<StartScreenController>(true)).Single();
            var data = new SerializedObject(controller);
            var title = controller.GetComponentsInChildren<Image>(true).Single(x => x.name == "Subject42 title");
            Assert.That(title.sprite, Is.Not.Null);
            Assert.That(title.raycastTarget, Is.False);
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(title.sprite));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
            foreach (string field in new[] { "beginButton", "settingsButton", "exitButton" })
            {
                var button = (Button)data.FindProperty(field).objectReferenceValue;
                Assert.That(PrefabUtility.IsPartOfPrefabInstance(button), Is.True, field);
                Assert.That(button.GetComponentInChildren<TMP_Text>().font, Is.EqualTo(font));
                Assert.That(button.GetComponent<Animator>(), Is.Null, "Menu buttons must not inherit bouncing reward animation");
            }
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    [Test]
    public void SharedSettingsOfferWindowMode()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/UI/SettingsPanel/SettingsPanel.prefab");
        var serialized = new SerializedObject(prefab.GetComponent<AudioSettingsPanel>());
        var property = serialized.FindProperty("windowModeDropdown");
        Assert.That(property, Is.Not.Null, "Shared settings must expose the screen mode control.");
        var dropdown = property.objectReferenceValue as TMP_Dropdown;
        Assert.That(dropdown, Is.Not.Null);
        Assert.That(dropdown.options.Count, Is.EqualTo(2));
    }

    private static void AssertWindowModeSelectionPersists(AudioSettingsPanel panel)
    {
        var dropdown = (TMP_Dropdown)new SerializedObject(panel)
            .FindProperty("windowModeDropdown").objectReferenceValue;
        const string key = "display.fullscreen";
        bool existed = PlayerPrefs.HasKey(key);
        int saved = PlayerPrefs.GetInt(key);
        try
        {
            dropdown.SetValueWithoutNotify(0);
            dropdown.value = 1;
            Assert.That(PlayerPrefs.GetInt(key), Is.EqualTo(1));
            dropdown.value = 0;
            Assert.That(PlayerPrefs.GetInt(key), Is.Zero);
            Assert.That(dropdown.options[1].text,
                Is.EqualTo(LocalizationService.Instance.Get("settings.fullscreen")));
        }
        finally
        {
            if (existed) PlayerPrefs.SetInt(key, saved);
            else PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }

    [Test]
    public void BackgroundIsAuthoredAndDoesNotInterceptButtons()
    {
        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            Assert.That(PresentationType, Is.Not.Null, "Missing capsule atmosphere component");
            var effect = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren(PresentationType, true)).Single();
            var image = effect.GetComponent<Image>();
            Assert.That(image.sprite, Is.Not.Null);
            Assert.That(image.raycastTarget, Is.False);
            Assert.That(image.sprite.texture.width, Is.GreaterThan(1000));
            var hand = (Image)new SerializedObject(effect).FindProperty("hand").objectReferenceValue;
            Assert.That(hand, Is.Not.Null, "Hand must be authored in the existing scene");
            Assert.That(hand.color.a, Is.Zero);
            Assert.That(hand.raycastTarget, Is.False);
            Assert.That(hand.sprite, Is.Not.Null);
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(hand.sprite));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.DoesSourceTextureHaveAlpha(), Is.True);
            var audio = AssetDatabase.LoadAssetAtPath<AudioCatalog>("Assets/_Project/Data/Audio/VerticalSliceAudioCatalog.asset");
            foreach (var cue in new[] { AudioCueId.CapsuleGlassTap, AudioCueId.CapsuleBodyResonance,
                AudioCueId.CapsuleGlassBreak, AudioCueId.StartScreenMusic })
            {
                Assert.That(audio.TryGet(cue, out var definition), Is.True, cue.ToString());
                Assert.That(definition.TryGetRandomClip(out _), Is.True, cue.ToString());
            }
            var controller = scene.GetRootGameObjects().SelectMany(x => x.GetComponentsInChildren<StartScreenController>(true)).Single();
            var data = new SerializedObject(controller);
            foreach (var field in new[] { "beginButton", "settingsButton", "exitButton", "settings", "menu", "atmosphere" })
                Assert.That(data.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        }
        finally { EditorSceneManager.CloseScene(scene, true); }
    }

    [TestCase(1920, 1080)]
    [TestCase(1280, 720)]
    [TestCase(1024, 768)]
    [TestCase(2560, 1080)]
    public void BackgroundCoversViewportAtBothParallaxExtremes(int width, int height)
    {
        Assert.That(PresentationType, Is.Not.Null);
        var method = PresentationType.GetMethod("CalculateCoverSize", BindingFlags.Public | BindingFlags.Static);
        var size = (Vector2)method.Invoke(null, new object[] { new Vector2(width, height), 16f / 9f, new Vector2(39, 21) });
        Assert.That(size.x, Is.GreaterThanOrEqualTo(width + 78));
        Assert.That(size.y, Is.GreaterThanOrEqualTo(height + 42));
        Assert.That(size.x / size.y, Is.EqualTo(16f / 9f).Within(.001));
    }

    [UnityTest]
    public IEnumerator SettingsCloseRestoresMenuAndStartLoadsBunker()
    {
        EditorSceneManager.OpenScene(ScenePath);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Show();
        yield return new EnterPlayMode();
        // Create captured callbacks after the domain reload performed by EnterPlayMode.
        yield return VerifyActiveStartScreen();
    }

    private static IEnumerator VerifyActiveStartScreen()
    {
        yield return null;
        var controller = Object.FindFirstObjectByType<StartScreenController>();
        Assert.That(controller, Is.Not.Null);
        var menuLoops = AudioService.Instance.GetComponentsInChildren<AudioSource>()
            .Where(x => x.isPlaying && x.loop).ToArray();
        Assert.That(menuLoops.Length, Is.EqualTo(1), "Start screen must own one atmospheric loop");
        Assert.That(menuLoops[0].clip, Is.Not.Null);
        CollectionAssert.Contains(new[] { "Life Support Loop", "Life Support Loop_2" }, menuLoops[0].clip.name);
        AudioClip firstMenuTrack = menuLoops[0].clip;
        var serviceData = new SerializedObject(AudioService.Instance);
        var originalCatalog = (AudioCatalog)serviceData.FindProperty("catalog").objectReferenceValue;
        var catalogData = new SerializedObject(originalCatalog);
        var menuCue = MenuCue(catalogData);
        var configuredClips = menuCue.FindPropertyRelative("clips");
        Assert.That(configuredClips.arraySize, Is.EqualTo(2));
        var secondMenuTrack = Enumerable.Range(0, configuredClips.arraySize)
            .Select(i => (AudioClip)configuredClips.GetArrayElementAtIndex(i).objectReferenceValue)
            .Single(x => x != firstMenuTrack);
        Assert.That(menuCue.FindPropertyRelative("category").intValue, Is.EqualTo((int)AudioCategory.Music));
        Assert.That(menuCue.FindPropertyRelative("loop").boolValue, Is.True);
        var data = new SerializedObject(controller);
        var settings = (AudioSettingsPanel)data.FindProperty("settings").objectReferenceValue;
        var menu = (CanvasGroup)data.FindProperty("menu").objectReferenceValue;
        var settingsButton = (Button)data.FindProperty("settingsButton").objectReferenceValue;
        var beginButton = (Button)data.FindProperty("beginButton").objectReferenceValue;
        var exitButton = (Button)data.FindProperty("exitButton").objectReferenceValue;
        System.IO.Directory.CreateDirectory("Artifacts/GeneratedQA/StartScreen");
        LocalizationService.Instance.SetLanguage(GameLanguage.Russian);
        yield return VerifyMenuPresentation(menu, beginButton, settingsButton, exitButton);
        yield return null;
        ScreenCapture.CaptureScreenshot("Artifacts/GeneratedQA/StartScreen/menu-clear.png");
        yield return new WaitForSecondsRealtime(.4f);
        AssertReachable(beginButton);
        AssertReachable(settingsButton);
        settingsButton.OnSubmit(new BaseEventData(EventSystem.current));
        Assert.That(settings.IsOpen, Is.True);
        Assert.That(menu.interactable, Is.False);
        AssertWindowModeSelectionPersists(settings);
        yield return null;
        ScreenCapture.CaptureScreenshot("Artifacts/GeneratedQA/StartScreen/menu-settings.png");
        yield return new WaitForSecondsRealtime(.4f);
        settings.Close();
        Assert.That(menuLoops[0].clip, Is.EqualTo(firstMenuTrack), "Track must remain selected while in menu");
        Assert.That(menu.interactable, Is.True);
        Assert.That(settings.IsOpen, Is.False);
        var fog = Object.FindObjectsByType<RawImage>(FindObjectsSortMode.None).Single(x => x.name == "Capsule condensation");
        Assert.That(fog.raycastTarget, Is.False);
        var atmosphere = fog.GetComponentInParent(PresentationType);
        var nextBreath = PresentationType.GetField("nextCondensation", BindingFlags.Instance | BindingFlags.NonPublic);
        nextBreath.SetValue(atmosphere, Time.unscaledTime - 3f);
        float previousTimeScale = Time.timeScale;
        Time.timeScale = 0;
        yield return null;
        yield return null;
        Assert.That(fog.color.a, Is.GreaterThan(.1f), "Condensation must animate even while time is paused");
        ScreenCapture.CaptureScreenshot("Artifacts/GeneratedQA/StartScreen/menu-condensation.png");
        yield return new WaitForSecondsRealtime(.4f);
        nextBreath.SetValue(atmosphere, Time.unscaledTime - 10f);
        yield return null;
        yield return null;
        Assert.That(fog.color.a, Is.Zero, "Glass must clear between breaths");
        Time.timeScale = previousTimeScale;
        PlayerPrefs.DeleteKey(BunkerIntroController.ViewedPreferenceKey);
        var capsuleCues = new System.Collections.Generic.List<AudioCueId>();
        float contactAt = -1f;
        float glassAt = -1f;
        AudioSource breakSource = null;
        var transitionView = (SceneTransitionView)new SerializedObject(SceneTransitionOverlay.Instance)
            .FindProperty("view").objectReferenceValue;
        System.Action<AudioCueId> observeContact = cue =>
        {
            if (cue == AudioCueId.CapsuleGlassTap) contactAt = Time.realtimeSinceStartup;
            if (cue == AudioCueId.CapsuleGlassBreak)
            {
                glassAt = Time.realtimeSinceStartup;
                Assert.That(transitionView.Alpha, Is.EqualTo(1f), "Glass must break only at full black");
                Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("StartScreen"));
                var black = (Image)new SerializedObject(transitionView).FindProperty("cinematicBlackout").objectReferenceValue;
                Assert.That(black.enabled, Is.True);
                Assert.That(black.color, Is.EqualTo(Color.black));
                breakSource = AudioService.Instance.GetComponentsInChildren<AudioSource>()
                    .Single(x => x.isPlaying && x.clip != null && x.clip.name == "Broken_glass_window");
            }
        };
        AudioService.Instance.DebugCuePlayed += observeContact;
        AudioService.Instance.DebugCuePlayed += capsuleCues.Add;
        var mixer = (UnityEngine.Audio.AudioMixer)new SerializedObject(AudioSettingsService.Instance)
            .FindProperty("mixer").objectReferenceValue;
        Assert.That(mixer.GetFloat(AudioSettingsService.MasterVolumeParameter, out float masterBefore), Is.True);
        float beginningAt = Time.realtimeSinceStartup;
        beginButton.onClick.Invoke();
        Assert.That(menu.alpha, Is.Zero, "New game must immediately hide the menu");
        Assert.That(menu.interactable, Is.False);
        Assert.That(menu.blocksRaycasts, Is.False);
        Assert.That(SceneTransitionOverlay.IsTransitioning, Is.False,
            "The hand must appear before the shared loading fade");
        beginButton.onClick.Invoke();
        controller.OpenSettings();
        Assert.That(settings.IsOpen, Is.False, "Starting cannot reopen settings");
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(.65f);
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("StartScreen"));
        var hand = Object.FindObjectsByType<Image>(FindObjectsSortMode.None)
            .Single(x => x.name == "Subject42 hand on glass");
        Assert.That(hand.color.a, Is.Zero, "Hand must remain hidden during the initial two-second pause");
        Assert.That(contactAt, Is.LessThan(0f));
        yield return new WaitForSecondsRealtime(2f);
        Assert.That(hand.color.a, Is.GreaterThan(.95f));
        Assert.That(hand.raycastTarget, Is.False);
        ScreenCapture.CaptureScreenshot("Artifacts/GeneratedQA/StartScreen/menu-hand.png");
        float deadline = Time.realtimeSinceStartup + 30;
        while (glassAt < 0f && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(glassAt - beginningAt, Is.InRange(3.45f, 3.9f), "Initial pause, hand hold and one-second fade precede glass");
        Assert.That(contactAt - beginningAt, Is.InRange(2.06f, 2.25f), "Contact and both impact layers must follow the two-second pause");
        CollectionAssert.AreEqual(new[] { AudioCueId.CapsuleGlassTap, AudioCueId.CapsuleBodyResonance,
            AudioCueId.CapsuleGlassBreak }, capsuleCues);
        AudioService.Instance.DebugCuePlayed -= capsuleCues.Add;
        AudioService.Instance.DebugCuePlayed -= observeContact;
        Assert.That(mixer.GetFloat(AudioSettingsService.MusicVolumeParameter, out float musicDb), Is.True);
        Assert.That(musicDb, Is.LessThanOrEqualTo(-79f), "Music must be silent at full black");
        Assert.That(mixer.GetFloat(AudioSettingsService.MasterVolumeParameter, out float masterDb), Is.True);
        Assert.That(masterDb, Is.EqualTo(masterBefore).Within(.01f), "Blackout must preserve the user's SFX/master gain");
        yield return new WaitForSecondsRealtime(1.8f);
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("StartScreen"), "Hold black before loading");
        Assert.That(transitionView.Alpha, Is.EqualTo(1f));
        while (SceneManager.GetActiveScene().name != RunEndService.BunkerSceneName && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(RunEndService.BunkerSceneName));
        Assert.That(Time.realtimeSinceStartup - glassAt, Is.GreaterThanOrEqualTo(2f));
        if (Time.realtimeSinceStartup - glassAt < 3.3f)
            Assert.That(breakSource.isPlaying, Is.True, "Persistent glass tail must survive unloading StartScreen");
        while (SceneTransitionOverlay.IsTransitioning && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(SceneTransitionOverlay.IsTransitioning, Is.False, "Loading overlay must release input");
        Assert.That(ProductionSceneComposition.Active.IsReady, Is.True);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Assert.That(menuLoops[0].isPlaying, Is.False, "Menu music source must stop in the bunker");
        var player = PlayerRuntimeReference.CachedPlayer;
        var movement = player.GetComponent<CharacterMovement2D>();
        var camera = Object.FindFirstObjectByType<CameraFollow>();
        Assert.That(movement.enabled, Is.True);
        Assert.That(camera.enabled, Is.True);
        Assert.That(camera.target, Is.EqualTo(player.transform));
        Vector3 initialPosition = player.transform.position;
        movement.MovementIntent = () => Vector2.down;
        // This test is driven by the EditMode runner even after entering Play Mode.
        yield return new WaitForSecondsRealtime(.25f);
        movement.MovementIntent = () => Vector2.zero;
        Assert.That(Vector3.Distance(initialPosition, player.transform.position), Is.GreaterThan(.05f),
            "After awakening the actual bunker player must move");
        ScreenCapture.CaptureScreenshot("Artifacts/GeneratedQA/StartScreen/bunker-control.png");
        yield return new WaitForSecondsRealtime(1f);
        var bunkerLoops = AudioService.Instance.GetComponentsInChildren<AudioSource>()
            .Where(x => x.isPlaying && x.loop).ToArray();
        var pause = Object.FindFirstObjectByType<PauseMenuUI>();
        pause.Pause();
        Assert.That(pause.IsPaused, Is.True);
        pause.Resume();
        Assert.That(pause.IsPaused, Is.False);
        CollectionAssert.AreEquivalent(bunkerLoops, AudioService.Instance.GetComponentsInChildren<AudioSource>()
            .Where(x => x.isPlaying && x.loop).ToArray(), "Continue must retain the existing bunker background");
        int sourceCount = AudioService.Instance.GetComponentsInChildren<AudioSource>().Length;
        // A runtime clone exercises each playlist variant without changing the authored asset.
        var testCatalog = Object.Instantiate(originalCatalog);
        SetMenuPlaylist(testCatalog, secondMenuTrack);
        serviceData.Update();
        serviceData.FindProperty("catalog").objectReferenceValue = testCatalog;
        serviceData.ApplyModifiedPropertiesWithoutUndo();
        Assert.That(SceneTransitionOverlay.Load("StartScreen"), Is.True);
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "StartScreen" &&
            !SceneTransitionOverlay.IsTransitioning);
        controller = Object.FindFirstObjectByType<StartScreenController>();
        menu = (CanvasGroup)new SerializedObject(controller).FindProperty("menu").objectReferenceValue;
        Assert.That(menu.alpha, Is.EqualTo(1f));
        Assert.That(menu.interactable && menu.blocksRaycasts, Is.True);
        var returnLoops = AudioService.Instance.GetComponentsInChildren<AudioSource>()
            .Where(x => x.isPlaying && x.loop).ToArray();
        Assert.That(returnLoops.Length, Is.EqualTo(1), "Returning to menu must stop both music crossfade sources");
        Assert.That(returnLoops[0].clip, Is.EqualTo(secondMenuTrack), "Second configured track must also play");
        Assert.That(AudioService.Instance.GetComponentsInChildren<AudioSource>().Length, Is.EqualTo(sourceCount));
        Assert.That(returnLoops[0].volume, Is.GreaterThan(0f));
        controller.Begin();
        controller.Begin();
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == RunEndService.BunkerSceneName &&
            !SceneTransitionOverlay.IsTransitioning);
        Assert.That(PlayerRuntimeReference.CachedPlayer.GetComponent<CharacterMovement2D>().enabled, Is.True);
        SetMenuPlaylist(testCatalog);
        Assert.That(SceneTransitionOverlay.Load("StartScreen"), Is.True);
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "StartScreen" &&
            !SceneTransitionOverlay.IsTransitioning);
        Assert.That(AudioService.Instance.GetComponentsInChildren<AudioSource>().Count(x => x.isPlaying && x.loop), Is.Zero,
            "Empty menu playlist must leave silence, including stopping the bunker background");
        Assert.That(AudioService.Instance.GetComponentsInChildren<AudioSource>().Length, Is.EqualTo(sourceCount));
        serviceData.Update();
        serviceData.FindProperty("catalog").objectReferenceValue = originalCatalog;
        serviceData.ApplyModifiedPropertiesWithoutUndo();
        Object.Destroy(testCatalog);
        // Shared cleanup unloads the bunker before persistent services are destroyed.
        // Exiting Play Mode directly destroys localization before the football HUD resets.
    }

    private static IEnumerator VerifyMenuPresentation(CanvasGroup menu, Button begin, Button settings, Button exit)
    {
        var effect = menu.GetComponentInParent<Canvas>().GetComponentInChildren<StartScreenAtmosphere>();
        // Keep the cursor outside the visual QA result: test the actual focus-loss return.
        PresentationType.GetMethod("OnApplicationFocus", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(effect, new object[] { false });
        yield return new WaitForSecondsRealtime(1.5f);
        Assert.That(effect.GetComponent<RectTransform>().anchoredPosition.magnitude, Is.LessThan(1f));
        foreach (var dimensions in new[] { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) })
        {
            SetGameViewSize(dimensions.x, dimensions.y);
            yield return new WaitForSecondsRealtime(.4f);
            Assert.That(new Vector2Int(Screen.width, Screen.height), Is.EqualTo(dimensions));
            Canvas.ForceUpdateCanvases();
            foreach (var label in menu.GetComponentsInChildren<TMP_Text>())
            {
                label.ForceMeshUpdate();
                Assert.That(label.isTextOverflowing, Is.False, label.text);
                for (int i = 0; i < label.textInfo.characterCount; i++)
                {
                    var glyph = label.textInfo.characterInfo[i];
                    if (!char.IsWhiteSpace(glyph.character))
                        Assert.That(glyph.fontAsset, Is.EqualTo(label.font), "No smooth fallback: " + label.text);
                }
            }
            ScreenCapture.CaptureScreenshot($"Artifacts/GeneratedQA/StartScreen/menu-{dimensions.x}.png");
            yield return new WaitForSecondsRealtime(.2f);
        }
        SetGameViewSize(1920, 1080);
        yield return new WaitForSecondsRealtime(.3f);
        AssertReachable(exit);
        EventSystem.current.SetSelectedGameObject(begin.gameObject);
        ExecuteEvents.Execute(begin.gameObject, new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Down }, ExecuteEvents.moveHandler);
        Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(settings.gameObject));
        ExecuteEvents.Execute(settings.gameObject, new AxisEventData(EventSystem.current) { moveDir = MoveDirection.Down }, ExecuteEvents.moveHandler);
        Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(exit.gameObject));
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        begin.OnPointerEnter(pointer);
        begin.OnPointerDown(pointer);
        var face = (RectTransform)new SerializedObject(begin).FindProperty("face").objectReferenceValue;
        Assert.That(face.anchoredPosition, Is.EqualTo(new Vector2(0, -3)));
        Assert.That(begin.transform.localScale, Is.EqualTo(Vector3.one));
        ScreenCapture.CaptureScreenshot("Artifacts/GeneratedQA/StartScreen/menu-pressed.png");
        yield return new WaitForSecondsRealtime(.2f);
        begin.OnPointerUp(pointer);
        begin.OnPointerExit(pointer);
        Assert.That(face.anchoredPosition, Is.EqualTo(Vector2.zero));
        begin.interactable = false;
        yield return new WaitForSecondsRealtime(.2f);
        ScreenCapture.CaptureScreenshot("Artifacts/GeneratedQA/StartScreen/menu-disabled.png");
        yield return new WaitForSecondsRealtime(.2f);
        begin.interactable = true;
        begin.Select();
        foreach (var offset in new[] { new Vector2(27, 15), new Vector2(-27, -15) })
        {
            effect.enabled = false;
            effect.GetComponent<RectTransform>().anchoredPosition = offset;
            Canvas.ForceUpdateCanvases();
            ScreenCapture.CaptureScreenshot($"Artifacts/GeneratedQA/StartScreen/parallax-{(offset.x > 0 ? "right" : "left")}.png");
            yield return new WaitForSecondsRealtime(.2f);
        }
        effect.enabled = true;
        yield return null;
    }

    private static void SetGameViewSize(int width, int height)
    {
        var assembly = typeof(Editor).Assembly;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var sizes = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
        var group = sizesType.GetMethod("GetGroup").Invoke(sizes, new object[] { 0 });
        var groupType = group.GetType();
        int count = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null), index = -1;
        for (int i = 0; i < count; i++)
        {
            var size = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
            if ((int)size.GetType().GetProperty("width").GetValue(size) == width &&
                (int)size.GetType().GetProperty("height").GetValue(size) == height) { index = i; break; }
        }
        if (index < 0)
        {
            var mode = Enum.ToObject(assembly.GetType("UnityEditor.GameViewSizeType"), 1);
            var size = Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),
                new object[] { mode, width, height, $"StartScreen QA {width}" });
            groupType.GetMethod("AddCustomSize").Invoke(group, new[] { size });
            index = count;
        }
        var view = EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        view.GetType().GetProperty("selectedSizeIndex").SetValue(view, index);
        view.Show();
    }

    [UnityTest]
    public IEnumerator ExitButtonLeavesPlayMode()
    {
        EditorSceneManager.OpenScene(ScenePath);
        yield return new EnterPlayMode();
        yield return null;
        var controller = Object.FindFirstObjectByType<StartScreenController>();
        var exit = (Button)new SerializedObject(controller).FindProperty("exitButton").objectReferenceValue;
        exit.OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
        yield return null;
        Assert.That(EditorApplication.isPlaying, Is.False);
    }

    private static SerializedProperty MenuCue(SerializedObject catalog)
    {
        var cues = catalog.FindProperty("cues");
        return Enumerable.Range(0, cues.arraySize).Select(i => cues.GetArrayElementAtIndex(i))
            .Single(x => x.FindPropertyRelative("id").intValue == (int)AudioCueId.StartScreenMusic);
    }

    private static void SetMenuPlaylist(AudioCatalog catalog, params AudioClip[] clips)
    {
        var data = new SerializedObject(catalog);
        var playlist = MenuCue(data).FindPropertyRelative("clips");
        playlist.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++) playlist.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
        data.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AssertReachable(Button button)
    {
        var pointer = new PointerEventData(EventSystem.current)
        {
            position = RectTransformUtility.WorldToScreenPoint(null, button.transform.position)
        };
        var hits = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(pointer, hits);
        Assert.That(hits.Count, Is.GreaterThan(0), button.name);
        Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.EqualTo(button),
            "Background or decoration intercepted " + button.name);
    }
}
#endif
