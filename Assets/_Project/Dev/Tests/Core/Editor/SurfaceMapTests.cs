#if UNITY_EDITOR
using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public sealed class SurfaceMapTests
{
    [Test] public void BuildValidatorRecognisesProductionRoutes()
    {
        var report = new Subject42ValidationReport();
        typeof(Subject42ProjectValidator).GetMethod("ValidateBuildScenes",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{report});
        Assert.That(report.ErrorCount,Is.Zero,report.FormatErrors());
    }
    [Test] public void ProductionBunkerValidatorUsesSharedComposition()
    {
        var report = new Subject42ValidationReport();
        typeof(Subject42ProjectValidator).GetMethod("ValidateMainMenuScene",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static).Invoke(null,new object[]{report});
        Assert.That(report.ErrorCount,Is.Zero,report.FormatErrors());
    }
    [Test] public void SurfaceContractExists()
    {
        foreach (string name in new[] { "SurfaceMapDefinition", "SurfaceSectorDefinition", "SurfaceMapProgressionState", "SurfaceMapService", "RunConfig", "SurfaceMapView" })
            Assert.That(typeof(RunStateManager).Assembly.GetType(name), Is.Not.Null, name + " is missing");
    }

    [Test] public void ProductionBunkerHasContextAndWorkingStations()
    {
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/MainMenu.unity");
            var contexts = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BunkerContext>(true)).ToArray();
            Assert.That(contexts.Length, Is.EqualTo(1), "Production bunker needs one wired context");
            var stations = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<BunkerStation>(true)).ToArray();
            var kinds = stations.Select(s => new SerializedObject(s).FindProperty("stationType").intValue).ToArray();
            foreach (var kind in new[] { BunkerStationType.CharacterSelection, BunkerStationType.WeaponSelection, BunkerStationType.Upgrade, BunkerStationType.AnomalyStabilizer, BunkerStationType.StartRun })
                Assert.That(kinds, Does.Contain((int)kind));
        }
        finally { if (setup.Any(s => s.isLoaded && s.isActive)) EditorSceneManager.RestoreSceneManagerSetup(setup); else EditorSceneManager.NewScene(NewSceneSetup.EmptyScene); }
    }
}
#endif
