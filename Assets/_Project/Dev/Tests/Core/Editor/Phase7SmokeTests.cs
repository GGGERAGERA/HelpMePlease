#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

// Explicit filters keep these two bounded flows separate from Golden Path/batch runs.
public sealed class Phase7SmokeTests
{
    [SetUp] public void Setup() => CoreTestSupport.PreservePreferences();
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();

    [UnityTest, Timeout(90000)]
    public IEnumerator StartScreenBunkerEventRewardAndReturn()
    {
        EditorSceneManager.OpenScene("Assets/_Project/Scenes/MainBuild/StartScreen.unity");
        yield return new EnterPlayMode();
        // Build captured event/steering callbacks after the Play Mode domain reload.
        yield return ExerciseProductionRoute();
    }

    private static IEnumerator ExerciseProductionRoute()
    {
        Object.FindFirstObjectByType<StartScreenController>().Begin();
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == RunEndService.BunkerSceneName &&
            Object.FindFirstObjectByType<BunkerRunStarter>() != null && !SceneTransitionOverlay.IsTransitioning);
        RunSelectionManager.Instance.SelectCharacter(AssetDatabase.LoadAssetAtPath<CharacterData>(
            "Assets/_Project/Data/Characters/01_Gera.asset"));
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        starter.StartRun(starter.transform);
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MVP" &&
            Object.FindFirstObjectByType<CharacterSpawner>() is { SpawnedPlayer: not null, Station: { IsInitialized: true } } &&
            !SceneTransitionOverlay.IsTransitioning);

        var characters = Object.FindFirstObjectByType<CharacterSpawner>();
        Assert.That(characters, Is.Not.Null, $"Run player owner missing in {SceneManager.GetActiveScene().name}; play={Application.isPlaying}.");
        var station = characters.Station;
        Assert.That(station, Is.Not.Null, "Run player must own its ORBITAL station.");
        var movement = characters.SpawnedPlayer.GetComponent<CharacterMovement2D>();
        Assert.That(movement, Is.Not.Null);
        movement.GetComponent<PlayerHealth>().AddMaxHealth(1000000f);
        Object.FindFirstObjectByType<EnemySpawner>().StopSpawning();
        var spawner = Object.FindFirstObjectByType<WorldEventSpawner>();
        Assert.That(spawner, Is.Not.Null, "Production world event owner must be active.");
        Assert.That(spawner.EventPrefabs, Is.Not.Empty);
        spawner.ConfigureDebugConcurrentEventCapacity(16);
        var prefab = spawner.EventPrefabs.OfType<OrbitalRelayEvent>().Single();
        // Fixture admission uses the authored prefab; start, contacts and reward delivery remain production-owned.
        Assert.That(spawner.SpawnDebugEventAt(prefab, movement.transform.position, false, out var spawned), Is.True);
        var relay = (OrbitalRelayEvent)spawned;
        OrbitalRelayResult? result = null;
        relay.Finished += value => result = value;
        movement.MovementIntent = () => relay == null ? Vector2.zero : relay.IsStarted
            ? OrbitalRelayBotSteering.GetDesiredMovement(relay, station, movement.transform.position)
            : Vector2.ClampMagnitude(relay.transform.position - movement.transform.position, 1f);
        float deadline = Time.realtimeSinceStartup + 45f;
        while (!result.HasValue && Time.realtimeSinceStartup < deadline)
        {
            if (relay != null && relay.CanInteract) relay.Interact();
            Assert.That(UpgradeManager.Instance, Is.Not.Null, "Production reward queue must survive event admission.");
            if (!result.HasValue && UpgradeManager.Instance.IsChoosingUpgrade)
                yield return RewardScenarioAssertions.ClickDisplayedCardAndVerifyGrant();
            yield return null;
        }
        movement.MovementIntent = () => Vector2.zero;
        Assert.That(result.HasValue && result.Value.Success, Is.True, "Relay must finish through real module/body contacts.");
        Assert.That(result.Value.UpgradeSelections, Is.EqualTo(1));
        yield return CoreTestSupport.Await(() => UpgradeManager.Instance.IsChoosingUpgrade);
        yield return RewardScenarioAssertions.ClickDisplayedCardAndVerifyGrant();
        Assert.That(spawner.ActiveEvent, Is.Null);
        int runId = RunStateManager.Instance.RunId;
        RunEndService.Instance.ReturnToBunker();
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == RunEndService.BunkerSceneName &&
            Object.FindFirstObjectByType<BunkerRunStarter>() != null && !SceneTransitionOverlay.IsTransitioning);
        Assert.That(RunStateManager.Instance.IsActiveRun(runId), Is.False);
        Assert.That(ProductionAnomalySite.ActiveSites, Is.Empty);
        Assert.That(Object.FindObjectsByType<WorldEvent>(FindObjectsSortMode.None), Is.Empty);
    }

    [UnityTest, Timeout(30000)]
    public IEnumerator WorldSystemsLabStartsSharedRelayAndReleasesItsOwners()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        EditorSceneManager.LoadSceneInPlayMode(WorldSystemsLabController.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
        yield return null;
        var lab = Object.FindFirstObjectByType<WorldSystemsLabController>();
        Assert.That(lab, Is.Not.Null);
        Assert.That(lab.TacticalMap.isActiveAndEnabled, Is.True, "Authored map references must remain intact.");
        Assert.That(lab.SpawnIndependentStasisTerritory(), Is.True);
        var territory = lab.SpawnedSites.Single();
        var prefab = lab.EventPrefabs.OfType<OrbitalRelayEvent>().Single();
        Assert.That(lab.SpawnEvent(prefab), Is.True);
        var relay = (OrbitalRelayEvent)lab.Events.ActiveEvent;
        var adapter = lab.GetComponent<WorldSystemsLabRelayAdapter>();
        Assert.That(relay.SourcePrefab, Is.SameAs(prefab));
        Assert.That(relay.Snapshot.Phase, Is.EqualTo(OrbitalRelayPhase.Stabilization));
        Assert.That(adapter.Station.IsInitialized, Is.True);
        lab.ClearEvents();
        yield return null;
        Assert.That(relay == null, Is.True);
        Assert.That(lab.Events.SpawnedEvents, Is.Empty);
        Assert.That(territory != null && territory.AnomalyZone != null, Is.True,
            "Clearing the event cannot retire an independently owned territory.");
        Assert.That(lab.GetComponent<CorridorLab>().enabled, Is.True);
        lab.ResetLab();
        yield return null;
        Assert.That(lab.SpawnedSites, Is.Empty);
        Assert.That(lab.WorldRules.ActiveRule, Is.Null);
    }
}
#endif
