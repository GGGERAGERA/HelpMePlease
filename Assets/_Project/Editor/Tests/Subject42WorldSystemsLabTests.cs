#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

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

    [Test]
    public void AuthoringKeepsLabRainLinkedToTheSharedPrefab()
    {
        Type authoring = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("WorldSystemsLabAuthoring"))
            .FirstOrDefault(type => type != null);

        Assert.That(authoring, Is.Not.Null);
        authoring.GetMethod("Create", BindingFlags.Public | BindingFlags.Static)
            ?.Invoke(null, null);

        string[] dependencies = AssetDatabase.GetDependencies(
            WorldSystemsLabController.ScenePath,
            true
        );

        Assert.That(
            dependencies,
            Does.Contain("Assets/_Project/prefabs/fx/WorldRules/WorldRule_Rain_Visual.prefab"),
            "The Lab must preview the shared production rain prefab, " +
            "not a disconnected scene copy."
        );
    }

    [Test]
    public void AuthoringCanRebuildWhileTheLabSceneIsOpen()
    {
        Type authoring = AppDomain.CurrentDomain.GetAssemblies()
            .Select(assembly => assembly.GetType("WorldSystemsLabAuthoring"))
            .FirstOrDefault(type => type != null);

        Assert.That(authoring, Is.Not.Null);
        authoring.GetMethod("Create", BindingFlags.Public | BindingFlags.Static)
            ?.Invoke(null, null);
        Scene previousActive = SceneManager.GetActiveScene();
        Light2D[] existingLights = UnityEngine.Object.FindObjectsByType<Light2D>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None
        );
        bool[] lightStates = existingLights
            .Select(light => light.enabled)
            .ToArray();
        foreach (Light2D light in existingLights)
            light.enabled = false;
        Scene lab = SceneManager.GetSceneByPath(
            WorldSystemsLabController.ScenePath
        );
        bool openedByTest = !lab.IsValid() || !lab.isLoaded;
        if (openedByTest)
        {
            lab = EditorSceneManager.OpenScene(
                WorldSystemsLabController.ScenePath,
                OpenSceneMode.Additive
            );
        }

        try
        {
            SceneManager.SetActiveScene(lab);
            Assert.DoesNotThrow(() =>
                authoring.GetMethod(
                    "Create",
                    BindingFlags.Public | BindingFlags.Static
                )?.Invoke(null, null)
            );
            Assert.That(
                SceneManager.GetActiveScene().path,
                Is.EqualTo(WorldSystemsLabController.ScenePath)
            );
        }
        finally
        {
            Scene rebuiltLab = SceneManager.GetSceneByPath(
                WorldSystemsLabController.ScenePath
            );
            if (openedByTest && rebuiltLab.IsValid() && rebuiltLab.isLoaded)
                EditorSceneManager.CloseScene(rebuiltLab, true);
            if (previousActive.IsValid() && previousActive.isLoaded)
                SceneManager.SetActiveScene(previousActive);
            for (int i = 0; i < existingLights.Length; i++)
            {
                if (existingLights[i] != null)
                    existingLights[i].enabled = lightStates[i];
            }
        }
    }
}
#endif
