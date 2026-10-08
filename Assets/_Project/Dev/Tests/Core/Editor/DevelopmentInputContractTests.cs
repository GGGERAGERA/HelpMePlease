#if UNITY_EDITOR
using System;
using NUnit.Framework;
using UnityEngine;
using System.Collections;
using System.Linq;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

[Category("Core")]
public sealed class DevelopmentInputContractTests
{
    [UnityTearDown]
    public IEnumerator ExitFixturePlayMode()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator AdditiveDevelopmentMenusHaveOneInputOwner()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        var first = new GameObject("first development menu");
        var second = new GameObject("additive development menu");
        var additive = SceneManager.CreateScene("DevelopmentMenuOwnership");
        SceneManager.MoveGameObjectToScene(second, additive);
        try
        {
            first.AddComponent<Subject42DebugMenu>();
            second.AddComponent<Subject42DebugMenu>();
            Assert.That(UnityEngine.Object.FindObjectsByType<Subject42DebugMenu>(FindObjectsSortMode.None)
                .Count(menu => menu.enabled), Is.EqualTo(1), "Only one menu may process F1 input across loaded scenes.");
        }
        finally { UnityEngine.Object.DestroyImmediate(first); UnityEngine.Object.DestroyImmediate(second); }
    }

    [Test]
    public void SeededStreamsReplayIndependentlyAndReleaseOwnership()
    {
        var unityState = UnityEngine.Random.state;
        GameplayRandom.End();
        try
        {
            GameplayRandom.Begin(12345);
            float world = GameplayRandom.WorldRandom.value;
            int spawn = GameplayRandom.SpawnRandom.Range(0, 10000);
            float reward = GameplayRandom.RewardRandom.value;
            float drop = GameplayRandom.DropRandom.value;
            Vector2 eventPoint = GameplayRandom.EventRandom.insideUnitCircle;
            float rule = GameplayRandom.RuleRandom.value;
            Assert.Throws<InvalidOperationException>(() => GameplayRandom.Begin(999));
            Assert.That(UnityEngine.Random.state, Is.EqualTo(unityState));
            GameplayRandom.End();
            GameplayRandom.Begin(12345);
            for (int i = 0; i < 20; i++) _ = GameplayRandom.EventRandom.value;
            Assert.That(GameplayRandom.WorldRandom.value, Is.EqualTo(world));
            Assert.That(GameplayRandom.SpawnRandom.Range(0, 10000), Is.EqualTo(spawn));
            Assert.That(GameplayRandom.RewardRandom.value, Is.EqualTo(reward));
            Assert.That(GameplayRandom.DropRandom.value, Is.EqualTo(drop));
            Assert.That(GameplayRandom.RuleRandom.value, Is.EqualTo(rule));
            GameplayRandom.End();
            GameplayRandom.Begin(12345);
            Assert.That(GameplayRandom.EventRandom.insideUnitCircle, Is.EqualTo(eventPoint));
        }
        finally { GameplayRandom.End(); UnityEngine.Random.state = unityState; }
        Assert.That(GameplayRandom.IsActive, Is.False);
    }

    [Test]
    public void UnseededDrawUsesUnityRandomState()
    {
        var state = UnityEngine.Random.state;
        GameplayRandom.End();
        try
        {
            UnityEngine.Random.InitState(6789);
            float expected = UnityEngine.Random.value;
            UnityEngine.Random.InitState(6789);
            Assert.That(GameplayRandom.WorldRandom.value, Is.EqualTo(expected));
        }
        finally { UnityEngine.Random.state = state; }
    }

    [Test]
    public void TutorialSuppressionIsOptionalAndDoesNotWriteProgress()
    {
        string key = TutorialController.CompletionKey;
        bool existed = PlayerPrefs.HasKey(key);
        int saved = PlayerPrefs.GetInt(key);
        var previous = RunDevelopmentOverrides.SuppressTutorial;
        try
        {
            PlayerPrefs.DeleteKey(key);
            RunDevelopmentOverrides.SuppressTutorial = () => true;
            Assert.That(TutorialController.NeedsTutorial, Is.False);
            RunDevelopmentOverrides.SuppressTutorial = null;
            Assert.That(TutorialController.NeedsTutorial, Is.True);
            Assert.That(PlayerPrefs.HasKey(key), Is.False);
        }
        finally
        {
            RunDevelopmentOverrides.SuppressTutorial = previous;
            if (existed) PlayerPrefs.SetInt(key, saved); else PlayerPrefs.DeleteKey(key);
        }
    }

    [Test]
    public void RuntimeNeutralFeedbackMatchesDevelopmentAdapterDefaults()
    {
        var settings = new CombatFeelLabSettings();
        foreach (var descriptor in CombatFeelLabSettings.Descriptors)
            Assert.That(CombatFeelParameterDefaults.Get(descriptor.Parameter),
                Is.EqualTo(settings.Get(descriptor.Parameter)), descriptor.Parameter.ToString());
    }
}
#endif
