#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class ProductionLocalizationTests
{
    private const string SlotKey = "ORBITAL_SLOT_PENDING";
    private const string StateKey = "ProductionLocalizationTests.EditorState";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Serializable] private sealed class SavedState
    {
        public SavedScene[] scenes;
        public bool hadLanguage;
        public int language;
        public bool runInBackground;
        public bool hadSlot;
        public int slot;
    }

    [Serializable] private sealed class SavedScene
    {
        public string path;
        public bool isLoaded;
        public bool isActive;
    }

    private static void PreserveEditorAndPreferences()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                Assert.Ignore("Localization scene smoke requires clean editor scenes; unsaved work is preserved.");
        SessionState.SetString(StateKey, JsonUtility.ToJson(new SavedState {
            scenes = EditorSceneManager.GetSceneManagerSetup().Select(scene => new SavedScene {
                path = scene.path, isLoaded = scene.isLoaded, isActive = scene.isActive }).ToArray(),
            hadLanguage = PlayerPrefs.HasKey(LocalizationService.LanguagePreferenceKey),
            language = PlayerPrefs.GetInt(LocalizationService.LanguagePreferenceKey),
            hadSlot = PlayerPrefs.HasKey(SlotKey),
            slot = PlayerPrefs.GetInt(SlotKey),
            runInBackground = Application.runInBackground
        }));
        new Subject42FinalBossFlowTests().PreserveRewardsAndUnlockProgress();
    }

    [UnityTearDown]
    public IEnumerator RestoreEditorAndPreferences()
    {
        string json = SessionState.GetString(StateKey, "");
        if (string.IsNullOrEmpty(json)) yield break;
        var saved = JsonUtility.FromJson<SavedState>(json);
        var cleanup = new Subject42FinalBossFlowTests().CleanupPlayMode();
        while (cleanup.MoveNext()) yield return cleanup.Current;
        if (saved.hadLanguage) PlayerPrefs.SetInt(LocalizationService.LanguagePreferenceKey, saved.language);
        else PlayerPrefs.DeleteKey(LocalizationService.LanguagePreferenceKey);
        if (saved.hadSlot) PlayerPrefs.SetInt(SlotKey, saved.slot);
        else PlayerPrefs.DeleteKey(SlotKey);
        PlayerPrefs.Save();
        Application.runInBackground = saved.runInBackground;
        SessionState.EraseString(StateKey);
        // The Unity runner may supply an empty temporary setup and restore its own backup.
        if (saved.scenes.Any(scene => scene.isLoaded) && saved.scenes.Count(scene => scene.isActive) == 1)
            EditorSceneManager.RestoreSceneManagerSetup(saved.scenes.Select(scene => new SceneSetup {
                path = scene.path, isLoaded = scene.isLoaded, isActive = scene.isActive }).ToArray());
    }

    [UnityTest]
    public IEnumerator ProductionScreensRefreshRussianEnglishRussianWithoutReopening()
    {
        PreserveEditorAndPreferences();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return ExerciseScreens();
    }

    private static IEnumerator ExerciseScreens()
    {
        Assert.That(Application.isPlaying, Is.True, "Screen smoke requires Play Mode");
        Application.runInBackground = true;
        PlayerPrefs.SetInt(BunkerIntroController.ViewedPreferenceKey, 1);
        PlayerPrefs.DeleteKey(SlotKey);
        yield return SceneManager.LoadSceneAsync("StartScreen");
        yield return null;
        yield return CycleLanguages("StartScreen", () => { });

        yield return SceneManager.LoadSceneAsync("MainMenu");
        yield return Await(() => BunkerContext.Instance != null &&
            RunSelectionManager.Instance != null && !SceneTransitionOverlay.IsTransitioning);
        var panels = One<BunkerPanelManager>();
        var gera = AssetDatabase.LoadAssetAtPath<CharacterData>(
            "Assets/_Project/Scriptable Objects/Characters/01_Gera.asset");
        RunSelectionManager.Instance.SelectCharacter(gera);
        panels.OpenCharacterSelection();
        yield return null;
        yield return CycleLanguages("Characters", () => {
            AssertVisible(gera.LocalizedCombatTypeDisplayName);
            AssertVisible(gera.LocalizedDescription);
        });

        panels.OpenDepthSelect(One<BunkerRunStarter>().transform);
        yield return null;
        var depth = One<EscapeProtocolView>();
        int selectedDepth = depth.SelectedDepthId;
        yield return CycleLanguages("Depth", () => {
            AssertText(depth, "titleText", "bunker.depth.select_title");
            Assert.That(depth.SelectedDepthId, Is.EqualTo(selectedDepth));
        });

        panels.OpenOrbitalSlot();
        yield return null;
        var slot = One<BunkerOrbitalSlotPanel>();
        yield return CycleLanguages("Casino", () => {
            AssertText(slot, "statusText", "bunker.slot_ready");
            AssertText(slot, "rewardText", "bunker.slot_rules");
        });
        panels.CloseAll(false);
        var starter = One<BunkerRunStarter>();
        starter.StartRun((Transform)Get(starter, "cameraRig"));
        yield return Await(() => SceneManager.GetActiveScene().name == "MVP" &&
            !SceneTransitionOverlay.IsTransitioning && HUDManager.Instance != null && HUDManager.Instance.IsPlayerBound);
        var hud = HUDManager.Instance;
        yield return CycleLanguages("HUD", () => {
            int level = (int)Get(hud, "displayedLevel");
            Assert.That(((TMP_Text)Get(hud, "levelText")).text,
                Is.EqualTo(string.Format(LocalizationService.Instance.Get("hud.level"), level)));
        });

        var pause = One<PauseMenuUI>();
        pause.Pause();
        Assert.That(pause.IsPaused, Is.True);
        yield return CycleLanguages("Pause", () => Assert.That(pause.IsPaused, Is.True));
        pause.Resume();
        Assert.That(UpgradeManager.Instance.DebugForceOrbitalReward(OrbitalRewardKind.NewRing), Is.True);
        yield return null;
        yield return CycleLanguages("Reward", () => {
            var choices = UpgradeManager.Instance.DebugCurrentChoices;
            Assert.That(choices.Count, Is.GreaterThan(0));
            foreach (var choice in choices)
                AssertVisible(LocalizationService.Instance.Get(choice.upgradeName));
            Assert.That(UpgradeManager.Instance.IsRewardQueueIdle, Is.False);
        });
    }

    private static IEnumerator CycleLanguages(string screen, Action assertDynamic)
    {
        foreach (GameLanguage language in new[] { GameLanguage.Russian, GameLanguage.English, GameLanguage.Russian })
        {
            LocalizationService.EnsureExists().SetLanguage(language);
            yield return null;
            var labels = Object.FindObjectsByType<LocalizedText>(FindObjectsSortMode.None)
                .Where(label => label.isActiveAndEnabled).ToArray();
            Assert.That(labels.Length, Is.GreaterThan(0), screen + " requires authored localized labels");
            var mismatches = new System.Collections.Generic.List<string>();
            foreach (var label in labels)
            {
                string key = (string)Get(label, "localizationKey");
                string expected = LocalizationService.Instance.Get(key);
                string actual = label.GetComponent<TMP_Text>().text;
                if (!LocalizationService.Instance.HasKey(key) || actual != expected)
                    mismatches.Add($"{screen}: {key}: expected '{expected}', actual '{actual}'");
            }
            Assert.That(mismatches, Is.Empty, string.Join("\n", mismatches));
            assertDynamic();
        }
    }

    private static void AssertText(object owner, string field, string key) =>
        Assert.That(((TMP_Text)Get(owner, field)).text, Is.EqualTo(LocalizationService.Instance.Get(key)), field);

    private static void AssertVisible(string expected)
    {
        Assert.That(Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
            .Any(text => text.isActiveAndEnabled && text.text.Contains(expected)), Is.True, expected);
    }

    private static T One<T>() where T : Object => Object.FindFirstObjectByType<T>();
    private static object Get(object owner, string name) => owner.GetType().GetField(name, Private).GetValue(owner);
    private static IEnumerator Await(Func<bool> condition)
    {
        float deadline = Time.realtimeSinceStartup + 30f;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(condition(), Is.True, "Production screen initialization timed out");
    }
}
#endif
