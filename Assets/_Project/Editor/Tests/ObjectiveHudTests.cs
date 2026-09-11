#if UNITY_EDITOR
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

public sealed class ObjectiveHudTests
{
    [TestCase(RunPhase.NormalSector, false, "hud.objective.explore")]
    [TestCase(RunPhase.NormalSector, true, "hud.objective.exit")]
    [TestCase(RunPhase.WaitingForRewards, true, "hud.objective.prepare")]
    [TestCase(RunPhase.FinalBossIntro, true, "hud.objective.prepare")]
    [TestCase(RunPhase.FinalBossCombat, true, "hud.objective.boss")]
    [TestCase(RunPhase.Victory, true, null)]
    [TestCase(RunPhase.Stopped, true, null)]
    public void ObjectiveFollowsFlowRatherThanSectorNumber(RunPhase phase, bool exit, string expected)
    {
        var method = typeof(HUDManager).GetMethod("ResolveObjectiveKey", BindingFlags.Public | BindingFlags.Static);
        Assert.That(method, Is.Not.Null, "HUD needs an authoritative flow projection");
        Assert.That(method.Invoke(null, new object[] { phase, exit }), Is.EqualTo(expected));
    }

    [Test]
    public void ProductionSceneHasAuthoredObjectiveAndContextReferences()
    {
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/MVP.unity");
            var hud = Object.FindFirstObjectByType<HUDManager>();
            foreach (string name in new[] { "runFlow", "pauseMenu", "interactionPrompt", "runMessages" })
                AssertReference(hud, name);
            var route = Object.FindFirstObjectByType<RunRouteProgressView>();
            foreach (string name in new[] { "sectorText", "objectiveText", "optionalText", "canvasGroup" })
                AssertReference(route, name);
            var messages = Object.FindFirstObjectByType<RunMessageService>();
            foreach (string name in new[] { "movementHint", "movementText", "hud" })
                AssertReference(messages, name);
        }
        finally
        {
            if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        }
    }

    private static void AssertReference(Object target, string name)
    {
        var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, name);
        Assert.That(field.GetValue(target) as Object, Is.Not.Null, name);
    }
}
#endif
