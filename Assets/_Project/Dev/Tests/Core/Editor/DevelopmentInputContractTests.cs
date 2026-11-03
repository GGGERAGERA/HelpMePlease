#if UNITY_EDITOR
using System;
using NUnit.Framework;
using UnityEngine;

[Category("Core")]
public sealed class DevelopmentInputContractTests
{
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
}
#endif
