#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

public sealed class Subject42WorldSystemsLabTests
{
    [Test]
    public void ControllerExposesTheApprovedProductionLabContract()
    {
        Type controller = typeof(WorldRuleController).Assembly.GetType(
            "WorldSystemsLabController"
        );

        Assert.That(controller, Is.Not.Null,
            "The direct-play World Systems Lab controller is missing.");

        string[] requiredMethods =
        {
            "ApplyWorldRule",
            "SpawnNormalAnomaly",
            "SpawnSpecialAnomaly",
            "ClearAnomalies",
            "SpawnEvent",
            "ClearEvents",
            "SpawnPortalPair",
            "ClearPortals",
            "ResetLab",
            "TeleportPlayerCenter",
            "ToggleMap"
        };

        foreach (string method in requiredMethods)
        {
            Assert.That(
                controller.GetMethods(BindingFlags.Instance |
                    BindingFlags.Public).Any(candidate =>
                        candidate.Name == method),
                Is.True,
                $"World Systems Lab is missing '{method}'."
            );
        }
    }

    [Test]
    public void AuthoringCreatesTheDirectPlaySceneOutsideBuildSettings()
    {
        Type authoring = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("WorldSystemsLabAuthoring"))
            .FirstOrDefault(type => type != null);

        Assert.That(authoring, Is.Not.Null,
            "The World Systems Lab scene authoring entry point is missing.");
        authoring.GetMethod("Create", BindingFlags.Public | BindingFlags.Static)
            ?.Invoke(null, null);
        Assert.That(File.Exists(
            "Assets/_Project/Scenes/Dev/Labs/WorldSystemsLab.unity"),
            Is.True);
        Assert.That(EditorBuildSettings.scenes.Any(scene =>
                scene.enabled && scene.path ==
                "Assets/_Project/Scenes/Dev/Labs/WorldSystemsLab.unity"),
            Is.False,
            "The development-only Lab must stay out of Build Settings.");
    }
}
#endif
