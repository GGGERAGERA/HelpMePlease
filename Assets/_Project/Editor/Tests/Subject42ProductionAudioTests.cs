#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class Subject42ProductionAudioTests
{
    [UnityTest, Timeout(180000)]
    public IEnumerator BothProductionEntryPathsUseOnePlayableCatalog()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/MVP.unity");
        yield return new EnterPlayMode();
        var service = AudioService.Instance;
        Assert.That(service, Is.Not.Null);
        var field = typeof(AudioService).GetField("catalog", BindingFlags.Instance | BindingFlags.NonPublic);
        var catalog = Resources.Load<AudioCatalog>("Audio/VerticalSliceAudioCatalog");
        Assert.That(field.GetValue(service), Is.SameAs(catalog));
        Assert.That(service.Play(AudioCueId.PistolShot), Is.True, "Direct MVP boot must play a catalog cue");
        yield return SceneManager.LoadSceneAsync("StartScreen");
        yield return null;
        UnityEngine.Object.FindFirstObjectByType<StartScreenController>().Begin();
        float deadline = Time.realtimeSinceStartup + 20;
        while ((SceneManager.GetActiveScene().name != "MainMenu" || SceneTransitionOverlay.IsTransitioning) && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MainMenu"));
        var character = AssetDatabase.FindAssets("t:CharacterData").Select(id => AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(id)))
            .First(c => c != null && c.characterPrefab != null);
        RunSelectionManager.Instance.SelectCharacter(character);
        var starter = UnityEngine.Object.FindFirstObjectByType<BunkerRunStarter>();
        starter.StartRun(starter.transform);
        deadline = Time.realtimeSinceStartup + 25;
        while ((SceneManager.GetActiveScene().name != "MVP" || SceneTransitionOverlay.IsTransitioning) && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MVP"));
        Assert.That(AudioService.Instance, Is.SameAs(service));
        Assert.That(field.GetValue(service), Is.SameAs(catalog));
        Assert.That(UnityEngine.Object.FindObjectsByType<AudioService>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        Assert.That(service.Play(AudioCueId.PlayerHurt), Is.True);
        yield return new ExitPlayMode();
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/MainMenu.unity");
    }

    [Test]
    public void ClipSelectionDoesNotConsumeGameplayRandom()
    {
        var saved = UnityEngine.Random.state;
        try
        {
            UnityEngine.Random.InitState(421);
            string before = JsonUtility.ToJson(UnityEngine.Random.state);
            var catalog = Resources.Load<AudioCatalog>("Audio/VerticalSliceAudioCatalog");
            Assert.That(catalog.TryGet(AudioCueId.EnemyHit, out var cue), Is.True);
            Assert.That(cue.TryGetRandomClip(out _), Is.True);
            Assert.That(JsonUtility.ToJson(UnityEngine.Random.state), Is.EqualTo(before));
        }
        finally { UnityEngine.Random.state = saved; }
    }

    [UnityTest]
    public IEnumerator SaturationPreservesPlayerHurtAndPauseStopsLoop()
    {
        yield return new EnterPlayMode();
        var service = AudioService.Instance;
        Assert.That(service, Is.Not.Null);
        foreach (var source in service.GetComponentsInChildren<AudioSource>()) source.Stop();
        var clip = AudioClip.Create("Audio QA long voice", 44100 * 4, 1, 44100, false);
        var catalog = ScriptableObject.CreateInstance<AudioCatalog>();
        var so = new SerializedObject(catalog);
        var cues = so.FindProperty("cues"); cues.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            var cue = cues.GetArrayElementAtIndex(i);
            cue.FindPropertyRelative("id").intValue = i == 0 ? 20 : i == 1 ? 30 : 100;
            var clips = cue.FindPropertyRelative("clips"); clips.arraySize = 1;
            clips.GetArrayElementAtIndex(0).objectReferenceValue = clip;
            cue.FindPropertyRelative("volume").floatValue = .05f;
            cue.FindPropertyRelative("pitchMin").floatValue = 1;
            cue.FindPropertyRelative("pitchMax").floatValue = 1;
            cue.FindPropertyRelative("cooldown").floatValue = 0;
            cue.FindPropertyRelative("maxSimultaneous").intValue = 20;
            cue.FindPropertyRelative("loop").boolValue = i == 2;
            var priority = cue.FindPropertyRelative("priority");
            if (priority != null) priority.intValue = i == 1 ? 100 : 0;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        typeof(AudioService).GetField("catalog", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(service, catalog);
        for (int i = 0; i < 20; i++) Assert.That(service.Play(AudioCueId.PistolShot), Is.True);
        Assert.That(service.Play(AudioCueId.PistolShot), Is.False);
        Assert.That(service.Play(AudioCueId.PlayerHurt), Is.True, "Hurt must evict a less important voice when pool is full");
        Assert.That(service.GetComponentsInChildren<AudioSource>().Length, Is.EqualTo(23));
        foreach (var source in service.GetComponentsInChildren<AudioSource>()) source.Stop();
        var owner = new GameObject("Compression QA owner");
        var loop = service.StartLoop((AudioCueId)100, owner.transform);
        Assert.That(loop, Is.Not.Null);
        Time.timeScale = 0;
        yield return null; yield return null;
        Assert.That(loop.IsPlaying, Is.False, "Pause must release a managed voice");
        Time.timeScale = 1;
        loop = service.StartLoop((AudioCueId)100, owner.transform);
        Assert.That(loop, Is.Not.Null);
        owner.SetActive(false);
        yield return null; yield return null;
        Assert.That(loop.IsPlaying, Is.False, "Inactive owner must not leak its voice");
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = 1;
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [Test]
    public void ProductionCatalogIsAvailableWithoutBunker()
    {
        var catalog = Resources.Load<AudioCatalog>("Audio/VerticalSliceAudioCatalog");
        Assert.That(catalog, Is.Not.Null, "Direct MVP must resolve the production catalog without visiting MainMenu");
        Assert.That(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(catalog)),
            Is.EqualTo("6ee2dbe59cb5456f944b59a72f4a0c31"), "Keep the original catalog GUID");
        Assert.That(AssetDatabase.FindAssets("t:AudioCatalog").Length, Is.EqualTo(1));
    }

    [Test]
    public void InspectExistingCandidates()
    {
        Directory.CreateDirectory("Artifacts/AudioPass");
        using var writer = new StreamWriter("Artifacts/AudioPass/candidates.tsv");
        writer.WriteLine("path\tduration\tpeak\trms");
        foreach (var id in AssetDatabase.FindAssets("t:AudioClip", new[]{"Assets/_Project/AudioEffects"}))
        {
            string path = AssetDatabase.GUIDToAssetPath(id);
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) continue;
            double squares = 0; float peak = 0;
            if (clip.length < 12f)
            {
                clip.LoadAudioData();
                var data = new float[clip.samples * clip.channels];
                if (clip.GetData(data, 0))
                    foreach (float sample in data) { peak = Mathf.Max(peak, Mathf.Abs(sample)); squares += sample * sample; }
                squares = data.Length > 0 ? Math.Sqrt(squares / data.Length) : 0;
            }
            writer.WriteLine(FormattableString.Invariant($"{path}\t{clip.length:F3}\t{peak:F4}\t{squares:F4}"));
        }
    }
}
#endif
