#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class Subject42SpecialSiteRewardTests
{
    private const string BackupKey = "Subject42SpecialSiteTests.Preferences";
    [Serializable] private sealed class Preference { public string key; public bool exists; public int value; }
    [Serializable] private sealed class Preferences { public Preference[] values; }

    [SetUp]
    public void PreservePreferences()
    {
        var keys = new System.Collections.Generic.List<string> { "TOTAL_GOLD", BunkerIntroController.ViewedPreferenceKey };
        foreach (string id in AssetDatabase.FindAssets("t:UnlockableContentData"))
        {
            var content = AssetDatabase.LoadAssetAtPath<UnlockableContentData>(AssetDatabase.GUIDToAssetPath(id));
            keys.Add("Unlock_" + content.id);
            keys.Add("UnlockProgress_" + content.id);
        }
        SessionState.SetString(BackupKey, JsonUtility.ToJson(new Preferences
        {
            values = keys.Distinct().Select(key => new Preference
                { key = key, exists = PlayerPrefs.HasKey(key), value = PlayerPrefs.GetInt(key) }).ToArray()
        }));
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (Application.isPlaying)
        {
            Scene scene = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("SpecialSiteTestCleanup"));
            yield return SceneManager.UnloadSceneAsync(scene);
            yield return new ExitPlayMode();
        }
        var saved = JsonUtility.FromJson<Preferences>(SessionState.GetString(BackupKey, ""));
        foreach (var value in saved.values)
            if (value.exists) PlayerPrefs.SetInt(value.key, value.value);
            else PlayerPrefs.DeleteKey(value.key);
        PlayerPrefs.Save();
        SessionState.EraseString(BackupKey);
    }

    private static FieldInfo Field(object target, string name) => target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private static object Get(object target, string name) => Field(target, name).GetValue(target);
    private static void Set(object target, string name, object value) => Field(target, name).SetValue(target, value);
    private static void Call(object target, string name, params object[] args) => target.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    private static IEnumerator Await(Func<bool> condition)
    {
        float deadline = Time.realtimeSinceStartup + 30f;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(condition(), Is.True, "Timed out waiting for production flow");
    }
    private static void AssertNoLegacy(OrbitalStationRuntime station)
    {
        Assert.That(RunStateManager.Instance.AnomalyInventory.IsEmpty, Is.True);
        var player = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer;
        Assert.That(player.GetComponents<MonoBehaviour>().OfType<IAnomalyPowerRuntime>(), Is.Empty);
        Assert.That(player.GetComponent<EvolutionRuntimeController>(), Is.Null);
        Assert.That(player.GetComponent<AnomalyCoreRuntime>(), Is.Null);
        Assert.That(Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None)
            .Any(t => t.text.Contains("[ EMPTY ]") && t.transform.root.name.Contains("Anomaly")), Is.False);
    }
    private static void Select(UpgradeManager upgrades, OrbitalRewardKind kind)
    {
        int index = upgrades.DebugCurrentChoices.ToList().FindIndex(
            x => x is OrbitalRewardData reward && reward.RewardKind == kind);
        Assert.That(index, Is.GreaterThanOrEqualTo(0), kind.ToString());
        Assert.That(upgrades.DebugSelectCurrentChoice(index), Is.True);
    }

    [UnityTest]
    public IEnumerator SpecialSitesGrantRingsAndUseExistingQueueAtCap()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return ExerciseRun();
    }

    private static IEnumerator ExerciseRun()
    {
        yield return SceneManager.LoadSceneAsync("MainMenu");
        yield return Await(() => Object.FindFirstObjectByType<BunkerRunStarter>() != null && !SceneTransitionOverlay.IsTransitioning);
        var character = AssetDatabase.FindAssets("t:CharacterData")
            .Select(id => AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(id)))
            .First(c => c.characterPrefab != null);
        RunSelectionManager.Instance.SelectCharacter(character);
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        starter.StartRun((Transform)Get(starter, "cameraRig"));
        yield return Await(() => SceneManager.GetActiveScene().name == "MVP" &&
            ProductionAnomalySite.ActiveSites.Count == 4);
        OrbitalStationRuntime station = null;
        yield return Await(() => (station = Object.FindFirstObjectByType<OrbitalStationRuntime>()) != null && station.IsInitialized);
        yield return Await(() => !SceneTransitionOverlay.IsTransitioning);
        station.enabled = false; // Stable snapshot; production input/placement controllers remain enabled.
        var spawner = Object.FindFirstObjectByType<CharacterSpawner>();
        Assert.That(spawner, Is.Not.Null, "production CharacterSpawner");
        var player = spawner.SpawnedPlayer;
        Assert.That(player, Is.Not.Null, "spawned production player");
        Assert.That(player.GetComponent<PlayerHealth>(), Is.Not.Null, "player health");
        player.GetComponent<PlayerHealth>().SetIncomingDamageMultiplier(0f);
        Assert.That(station.State.Modules.Count, Is.EqualTo(1));
        Assert.That(station.State.Modules[0].ModuleType, Is.EqualTo(OrbitalModuleKind.Pistol));
        AssertNoLegacy(station);
        var upgrades = UpgradeManager.Instance;
        Set(upgrades, "choicesCount", 100); // Deterministic coverage of the unchanged eligible pool.
        upgrades.ShowLevelUpChoices(2);
        yield return Await(() => upgrades.DebugCurrentChoices != null);
        Select(upgrades, OrbitalRewardKind.RingPower);
        Assert.That(station.RewardFlow.DebugChooseRing(1), Is.True);
        Assert.That(upgrades.IsRewardQueueIdle, Is.True);

        var original = ProductionAnomalySite.ActiveSites.Single(s => s.IsSpecial);
        var events = Object.FindFirstObjectByType<WorldEventSpawner>();
        var oldEvent = (WorldEvent)Get(original, "activeEvent");
        events.ClearDebugEvent(oldEvent);
        Call(original, "CollapseEnvironment");
        Vector2 position = original.transform.position;
        Vector2 size = original.SiteSize;
        Vector2 exitPosition = (Vector2)Get(original, "exitPosition");
        Object.Destroy(original.gameObject);
        yield return null;
        var exploration = ProductionExplorationSectorController.ActiveInstance;
        var config = (ExplorationSectorConfig)Get(exploration, "config");
        var capturePrefab = AssetDatabase.FindAssets("t:Prefab")
            .Select(id => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(id)))
            .Select(p => p.GetComponent<CaptureZoneEvent>()).First(p => p != null && p.AllowedInSite);
        var runState = RunStateManager.Instance;
        var stage = AssetDatabase.FindAssets("t:StageProfileData")
            .Select(id => AssetDatabase.LoadAssetAtPath<StageProfileData>(AssetDatabase.GUIDToAssetPath(id)))
            .First(p => p.SectorNumber == 2);
        var next = new RunSector(2, stage, runState.CurrentSector.WorldRule, runState.CurrentSector.LocalAnomaly);
        System.IO.Directory.CreateDirectory(Subject42OrbitalProductionPassTests.Output);
        foreach (var type in new[] { AnomalyPowerType.GravityOrb, AnomalyPowerType.ArcNode, AnomalyPowerType.RedBeam })
        {
            int ringCount = station.State.Rings.Count;
            station.State.BeginLevelUpOpportunity(station.State.LastProcessedPlayerLevel + 1, .99f);
            var site = new GameObject("Special Site Reward Test " + type).AddComponent<ProductionAnomalySite>();
            Assert.That(site.InitializeSpecial(position, size, type, capturePrefab, events,
                LevelAnomalyController.Instance, exploration.gameObject, config, exitPosition, config.ExitRadius), Is.True);
            var encounter = (CaptureZoneEvent)Get(site, "activeEvent");
            Assert.That(Get(site, "specialEnvironment"), Is.Not.Null);
            player.transform.position = encounter.transform.position;
            yield return null;
            encounter.ConfigureDebugHoldTime(0.1f);
            encounter.Interact();
            Assert.That(encounter.IsStarted, Is.True);
            yield return Await(() => site.IsCompleted);
            Assert.That(Get(site, "specialEnvironment"), Is.Null, "hazard collapsed before reward placement");
            Assert.That(site.IsMapVisible, Is.False);
            Assert.That(upgrades.IsRewardQueueIdle, Is.True, "special reward is not a random card");
            Assert.That(station.State.Rings.Count, Is.EqualTo(ringCount + 1));
            Assert.That(station.State.RingOfferMissCount, Is.Zero);
            Call(site, "HandleEventCompleted", encounter);
            Call(site, "HandleEventCompleted", new object[] { null });
            Assert.That(station.State.Rings.Count, Is.EqualTo(ringCount + 1), "duplicate completion");
            yield return null;
            float settleUntil = Time.realtimeSinceStartup + .5f;
            while (Time.realtimeSinceStartup < settleUntil)
            {
                // Station simulation is frozen for snapshot equality; still let
                // its real unscaled ring presentation finish the acquisition.
                foreach (var ring in station.Rings) ring.Tick(0f);
                yield return null;
            }
            ScreenCapture.CaptureScreenshot(Subject42OrbitalProductionPassTests.Output + "strong-anomaly-" + type + ".png");
            yield return null;
            AssertNoLegacy(station);
            Object.Destroy(site.gameObject);
            yield return null;
        }
        // At cap use the same normal random reward queue, without a ninth ring.
        while (station.AddRing() != null) { }
        Assert.That(upgrades.DebugForceOrbitalReward(OrbitalRewardKind.Pistol), Is.True);
        upgrades.DebugSelectCurrentChoice(0);
        station.RewardFlow.DebugChooseMount(1, 1);
        upgrades.GrantSpecialAnomalyRing();
        bool transitionAllowed = false;
        upgrades.RunWhenRewardQueueIsIdle(() => transitionAllowed = true);
        yield return Await(() => station.RewardFlow.PendingReward == null && upgrades.DebugCurrentChoices.Count > 1);
        Assert.That(transitionAllowed, Is.False, "fallback hand must retain transition barrier");
        Assert.That(upgrades.IsChoosingUpgrade, Is.True);
        Assert.That(upgrades.DebugCurrentChoices.OfType<OrbitalRewardData>().Any(r => r.RewardKind == OrbitalRewardKind.NewRing), Is.False);
        Select(upgrades, OrbitalRewardKind.ArcEmitter);
        Call(station.GetComponent<OrbitalInteractionController>(), "CancelActive");
        Assert.That(station.State.Rings.Count, Is.EqualTo(8));
        Select(upgrades, OrbitalRewardKind.ArcEmitter);
        int freeRing = station.State.Rings.First(r => station.State.HasFreeMount(r.StableRingId)).StableRingId;
        int freeMount = Enumerable.Range(0, station.State.FindRing(freeRing).MountCapacity).First(m => station.State.IsMountFree(freeRing, m));
        station.RewardFlow.DebugChooseMount(freeRing, freeMount);
        yield return Await(() => upgrades.IsRewardQueueIdle);
        Assert.That(transitionAllowed, Is.True);
        string committed = Subject42OrbitalRestoreTests.Snapshot(station.State);
        var previousScene = SceneManager.GetActiveScene().handle;
        Call(Object.FindFirstObjectByType<LevelChoiceManager>(), "TransitionToSector", next);
        yield return Await(() => SceneManager.GetActiveScene().handle != previousScene &&
            Object.FindFirstObjectByType<OrbitalStationRuntime>() != null &&
            Object.FindFirstObjectByType<OrbitalStationRuntime>().IsInitialized && !SceneTransitionOverlay.IsTransitioning);
        station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        string WithoutLivePhase(string snapshot) => System.Text.RegularExpressions.Regex.Replace(
            snapshot, @"CurrentPhase=[^,}]+", "CurrentPhase=<live>");
        Assert.That(WithoutLivePhase(Subject42OrbitalRestoreTests.Snapshot(station.State)),
            Is.EqualTo(WithoutLivePhase(committed)));
        Assert.That(runState.CurrentSector.SectorNumber, Is.EqualTo(2));
        AssertNoLegacy(station);
        // Exit with an unresolved reward, then start a clean run from the bunker.
        UpgradeManager.Instance.ShowUpgradeChoices();
        RunEndService.Instance.ReturnToBunker();
        yield return Await(() => Object.FindFirstObjectByType<BunkerRunStarter>() != null && !SceneTransitionOverlay.IsTransitioning);
        RunSelectionManager.Instance.SelectCharacter(character);
        starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        starter.StartRun((Transform)Get(starter, "cameraRig"));
        yield return Await(() => Object.FindFirstObjectByType<OrbitalStationRuntime>() != null &&
            Object.FindFirstObjectByType<OrbitalStationRuntime>().IsInitialized && !SceneTransitionOverlay.IsTransitioning);
        station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        Assert.That(station.State.Modules.Count, Is.EqualTo(1));
        Assert.That(UpgradeManager.Instance.IsRewardQueueIdle, Is.True);
        AssertNoLegacy(station);
    }
}
#endif
