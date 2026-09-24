#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Subject42.Combat.OrbitalStation;
using Object = UnityEngine.Object;

// Only shared production-scene startup, bounded waits and preference restoration.
public static class CoreTestSupport
{
    [Serializable] private sealed class Preference { public string key; public bool exists; public int value; }
    [Serializable] private sealed class Preferences { public List<Preference> values = new(); }
    private const string BackupKey = "Subject42.Core.Preferences";

    public static void PreservePreferences()
    {
        var keys = new List<string> { "TOTAL_GOLD", "ORBITAL_SLOT_PENDING", TutorialController.CompletionKey,
            LocalizationService.LanguagePreferenceKey, BunkerIntroController.ViewedPreferenceKey,
            MetaProgressionManager.EscapeAccessKey, MetaProgressionManager.EscapeAnnouncedAccessKey };
        foreach (string guid in AssetDatabase.FindAssets("t:UnlockableContentData"))
        {
            var content = AssetDatabase.LoadAssetAtPath<UnlockableContentData>(AssetDatabase.GUIDToAssetPath(guid));
            keys.Add("Unlock_" + content.id);
            keys.Add("UnlockProgress_" + content.id);
        }
        var saved = new Preferences();
        foreach (string key in keys.Distinct()) saved.values.Add(new Preference
            { key = key, exists = PlayerPrefs.HasKey(key), value = PlayerPrefs.GetInt(key) });
        SessionState.SetString(BackupKey, JsonUtility.ToJson(saved));
        PlayerPrefs.SetInt(BunkerIntroController.ViewedPreferenceKey, 1);
        PlayerPrefs.SetInt(TutorialController.CompletionKey, 1);
        PlayerPrefs.DeleteKey("ORBITAL_SLOT_PENDING");
    }

    public static IEnumerator BeginRun()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return LoadBunker();
        var character = AssetDatabase.LoadAssetAtPath<CharacterData>(
            "Assets/_Project/Data/Characters/01_Gera.asset");
        RunSelectionManager.Instance.SelectCharacter(character);
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        starter.StartRun(starter.transform);
        yield return Await(() => SceneManager.GetActiveScene().name == "MVP" &&
            Object.FindFirstObjectByType<OrbitalStationRuntime>() is { IsInitialized: true } &&
            !SceneTransitionOverlay.IsTransitioning);
        Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer.GetComponent<PlayerHealth>().AddMaxHealth(1000000f);
    }

    public static IEnumerator LoadBunker()
    {
        yield return SceneManager.LoadSceneAsync("MainMenu");
        yield return Await(() => Object.FindFirstObjectByType<BunkerRunStarter>() != null &&
            !SceneTransitionOverlay.IsTransitioning);
    }

    public static IEnumerator Await(Func<bool> condition)
    {
        float deadline = Time.realtimeSinceStartup + 30f;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(condition(), Is.True, "Production flow did not finish within 30 seconds.");
    }

    public static IEnumerator CleanupPlayMode()
    {
        if (BotRunSession.Current != null)
        {
            var batch = BotRunSession.Current.GetComponent<BotBatchRunner>();
            if (batch != null && batch.IsActive) batch.StopBatch();
        }
        if (Application.isPlaying)
        {
            Time.timeScale = 1f;
            var scene = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("CoreCleanup"));
            yield return SceneManager.UnloadSceneAsync(scene);
            yield return new ExitPlayMode();
        }
        BotRunSeed.End();
        string json = SessionState.GetString(BackupKey, "");
        if (!string.IsNullOrEmpty(json))
            foreach (var value in JsonUtility.FromJson<Preferences>(json).values)
                if (value.exists) PlayerPrefs.SetInt(value.key, value.value);
                else PlayerPrefs.DeleteKey(value.key);
        PlayerPrefs.Save();
        SessionState.EraseString(BackupKey);
    }
}
#endif
