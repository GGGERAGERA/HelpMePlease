#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class Subject42OrbitalRestoreFlowTests
{
    [UnityTest]
    public IEnumerator BunkerStart_AndActualSectorReload_PreserveAuthoritativeState()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("MainMenu");
        float deadline = Time.realtimeSinceStartup + 20f;
        BunkerRunStarter starter;
        while ((starter = Object.FindFirstObjectByType<BunkerRunStarter>()) == null && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(starter, Is.Not.Null);
        CharacterData character = AssetDatabase.FindAssets("t:CharacterData")
            .Select(id => AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(id)))
            .First(c => c != null && c.characterPrefab != null);
        Assert.That(RunSelectionManager.Instance, Is.Not.Null);
        RunSelectionManager.Instance.SelectCharacter(character);
        var target = (Transform)typeof(BunkerRunStarter).GetField("cameraRig", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(starter);
        starter.StartRun(target);
        Assert.That(starter.IsTransitioning, Is.True);
        deadline = Time.realtimeSinceStartup + 30f;
        OrbitalStationRuntime station = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
            if (SceneManager.GetActiveScene().name == "MVP" && station != null && station.IsInitialized) break;
            yield return null;
        }
        Assert.That(station, Is.Not.Null);
        Assert.That(station.IsInitialized, Is.True);
        Time.timeScale = 0f;
        RunStateManager manager = RunStateManager.Instance;
        var state = manager.OrbitalStationState;
        Assert.That(station.State, Is.SameAs(state));
        Assert.That(state.Rings.Count, Is.EqualTo(1));
        Assert.That(state.Modules.Count, Is.EqualTo(1));
        Assert.That(state.Modules[0].ModuleType, Is.EqualTo(OrbitalModuleKind.Pistol));
        Subject42OrbitalRestoreTests.Modify(state);
        string before = Subject42OrbitalRestoreTests.Snapshot(state);
        Assert.That(station.SimulateSectorRestore(), Is.True);
        Assert.That(station.SimulateSectorRestore(), Is.True);
        Assert.That(Subject42OrbitalRestoreTests.Snapshot(state), Is.EqualTo(before));
        Debug.Log("PASS1 normal Bunker StartRun -> MVP -> restore x2: " + before);

        // Exercise the real production transition method, including saves and LoadScene.
        var choice = Object.FindFirstObjectByType<LevelChoiceManager>();
        Assert.That(choice, Is.Not.Null);
        var stage = AssetDatabase.FindAssets("t:StageProfileData")
            .Select(id => AssetDatabase.LoadAssetAtPath<StageProfileData>(AssetDatabase.GUIDToAssetPath(id)))
            .First(p => p.SectorNumber == 2);
        var next = new RunSector(2, stage, manager.CurrentSector.WorldRule, manager.CurrentSector.LocalAnomaly);
        int oldScene = station.gameObject.scene.handle;
        UnityEngine.Events.UnityAction<Scene, LoadSceneMode> freeze = (scene, mode) => Time.timeScale = 0f;
        SceneManager.sceneLoaded += freeze;
        try
        {
            typeof(LevelChoiceManager).GetMethod("TransitionToSector", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(choice, new object[] { next });
            deadline = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < deadline)
            {
                station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
                if (station != null && station.gameObject.scene.handle != oldScene && station.IsInitialized) break;
                yield return null;
            }
            Assert.That(station, Is.Not.Null);
            Assert.That(station.gameObject.scene.handle, Is.Not.EqualTo(oldScene));
            Assert.That(station.State, Is.SameAs(state));
            Assert.That(manager.OrbitalStationState, Is.SameAs(state));
            Assert.That(manager.CurrentSector.SectorNumber, Is.EqualTo(2));
            // CharacterSpawner.Start restores timeScale=1; phase may advance before the test resumes.
            // Compare every other field; explicit restore above compares phase too.
            string WithoutPhase(string snapshot) => System.Text.RegularExpressions.Regex.Replace(
                snapshot, @"CurrentPhase=[^,}]+", "CurrentPhase=<live>");
            Assert.That(WithoutPhase(Subject42OrbitalRestoreTests.Snapshot(state)), Is.EqualTo(WithoutPhase(before)));
            Debug.Log("PASS1 actual sector 1 -> 2: same object; all fields preserved except live phase ticks.");
        }
        finally { SceneManager.sceneLoaded -= freeze; Time.timeScale = 1f; }
        yield return new ExitPlayMode();
    }
}
#endif
