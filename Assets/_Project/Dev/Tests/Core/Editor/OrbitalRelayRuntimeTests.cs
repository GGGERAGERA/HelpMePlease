#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class OrbitalRelayRuntimeTests
{
    [SetUp] public void Setup()
    {
        CoreTestSupport.PreservePreferences();
        PlayerPrefs.SetInt("TOTAL_GOLD", 100);
    }
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();
    [UnityTest, Timeout(90000)] public IEnumerator ProductionContactsBonusAndAllExitPathsInLab()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return ExerciseLab();
    }
    private static IEnumerator ExerciseLab()
    {
        // Existing lab shell predates the authored map markers; relay acceptance doesn't rewrite the map.
        LogAssert.Expect(LogType.Error, "[TacticalMapHUD] Authored shell or scene references are missing.");
        EditorSceneManager.LoadSceneInPlayMode(WorldSystemsLabController.ScenePath,
            new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        yield return null;
        Application.runInBackground = true;
        var lab = Object.FindFirstObjectByType<WorldSystemsLabController>();
        Debug.Log("[RelaySmoke] Lab=" + lab + " play=" + Application.isPlaying);
        Assert.That(lab, Is.Not.Null);
        Set(lab, "relayEnemyPressure", false); // Pressure lease still applies to the stopped real spawner.
        var prefab = lab.EventPrefabs.OfType<OrbitalRelayEvent>().Single();
        Debug.Log("[RelaySmoke] source=" + prefab + " player=" + lab.Player);
        Assert.That(lab.SpawnEvent(prefab), Is.True, "Same authored production prefab must start in lab.");
        var relay = (OrbitalRelayEvent)lab.Events.ActiveEvent;
        Debug.Log("[RelaySmoke] active=" + relay);
        var adapter = lab.GetComponent<WorldSystemsLabRelayAdapter>();
        Assert.That(lab.GetComponent<CorridorV2Lab>().enabled, Is.False, "Other prototype HUD must not cover the production Relay HUD.");
        var movement = lab.Player.GetComponent<CharacterMovement2D>();
        var station = adapter.Station;
        Assert.That(relay.Snapshot.Phase, Is.EqualTo(OrbitalRelayPhase.Stabilization));
        Assert.That(relay.Snapshot.RemainingTime, Is.EqualTo(20).Within(.1f));
        var module = station.Modules.First(); var mount = module.CurrentMount;
        Assert.That(module.HasBodyContact(module.WorldPosition, .35f), Is.True);
        module.BeginPresentationDrag(station.transform);
        Assert.That(module.HasBodyContact(module.WorldPosition, .35f), Is.False);
        module.CancelPresentationDrag(); module.SetRewardPresentationVisible(false);
        Assert.That(module.HasBodyContact(module.WorldPosition, .35f), Is.False);
        module.SetRewardPresentationVisible(true); module.Detach();
        Assert.That(module.HasBodyContact(module.WorldPosition, .35f), Is.False);
        module.Attach(mount);
        int rendererCount = relay.GetComponentsInChildren<Renderer>(true).Length;
        var phases = new HashSet<OrbitalRelayPhase> { relay.Snapshot.Phase };
        OrbitalRelayResult? result = null;
        relay.PhaseChanged += phase => { phases.Add(phase); Debug.Log("[RelaySmoke] Phase=" + phase); };
        relay.Finished += completed => result = completed;
        movement.MovementIntent = () => relay != null ? OrbitalRelayBotSteering.GetDesiredMovement(relay, station, lab.Player.position) : Vector2.zero;
        yield return Await(() => relay == null || relay.Snapshot.Phase == OrbitalRelayPhase.Transition, 22f);
        Assert.That(relay, Is.Not.Null, "Real movement contacts must stabilize before timeout.");
        Assert.That(relay.Snapshot.StabilizationActivations, Is.EqualTo(3));
        Assert.That(relay.Snapshot.BonusActivations, Is.Zero);
        Assert.That(adapter.Enemies.WorldEventSpawnPressureMultiplier, Is.EqualTo(1));
        yield return Capture("transition");
        yield return Await(() => relay.Snapshot.Phase == OrbitalRelayPhase.Bonus, 2f);
        Assert.That(relay.Snapshot.RemainingTime, Is.GreaterThan(14));
        Assert.That(adapter.Enemies.WorldEventSpawnPressureMultiplier, Is.EqualTo(1.6f));
        Assert.That(relay.GetComponentsInChildren<Renderer>(true).Length, Is.EqualTo(rendererCount));
        yield return new WaitForSeconds(.8f);
        yield return Capture("bonus");
        yield return Await(() => result.HasValue, 17f);
        movement.MovementIntent = () => Vector2.zero;
        Assert.That(result.Value.Success, Is.True);
        Assert.That(result.Value.BonusActivations, Is.GreaterThan(0));
        Assert.That(result.Value.Gold, Is.EqualTo(result.Value.BonusActivations * 10));
        Assert.That(result.Value.UpgradeSelections, Is.EqualTo(1));
        Assert.That(adapter.Enemies.WorldEventSpawnPressureMultiplier, Is.EqualTo(1));
        Assert.That(UpgradeManager.Instance, Is.Null, "Preview must not open a parallel or real reward queue.");
        yield return new WaitForSecondsRealtime(.5f);
        foreach (var group in RunMessageService.Instance.View.GetComponentsInParent<CanvasGroup>())
            Assert.That(group.alpha, Is.GreaterThan(.9f), "Finished result must be visible.");
        yield return Capture("result");
        Debug.Log("[RelaySmoke] Default flow result: " + result.Value.BonusActivations + " bonus activations / " + result.Value.Gold + " Gold");

        // Temporary settings are cloned in memory; shared authored defaults are restored before the first frame.
        var config = ScriptableObject.CreateInstance<OrbitalRelayConfig>();
        try
        {
            lab.ClearEvents(); yield return null;
            // Exercise the normal production entry path, without the lab's StartEvent shortcut.
            Set(lab, "relayRewardQueue", true);
            adapter.EnableRewards = true;
            Assert.That(adapter.Prepare(lab.Player, (CharacterData)typeof(WorldSystemsLabController)
                .GetField("orbitalCharacter", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(lab),
                lab.Events, null, false), Is.True);
            Assert.That(lab.Events.SpawnDebugEventAt(prefab, lab.Player.position, false, out var normalEntry), Is.True);
            var interactor = lab.Player.GetComponent<PlayerInteractor>() ?? lab.Player.gameObject.AddComponent<PlayerInteractor>();
            Physics2D.SyncTransforms();
            typeof(PlayerInteractor).GetMethod("FindInteractable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(interactor, null);
            Assert.That(interactor.GetCurrentInteractable(), Is.EqualTo(normalEntry));
            normalEntry.Interact();
            Assert.That(normalEntry.IsStarted, Is.True);
            Assert.That(((OrbitalRelayEvent)normalEntry).Snapshot.Phase, Is.EqualTo(OrbitalRelayPhase.Stabilization));
            normalEntry.Cancel(); lab.ClearEvents(); yield return null;
            Set(lab, "relayRewardQueue", false);
            config.stabilizationDuration = .3f;
            relay = Spawn(lab, prefab, config);
            result = null; relay.Finished += completed => result = completed;
            yield return Await(() => result.HasValue, 2f);
            Assert.That(result.Value.Success, Is.False); Assert.That(result.Value.Gold, Is.Zero);
            Assert.That(result.Value.UpgradeSelections, Is.Zero);
            Assert.That(adapter.Enemies.WorldEventSpawnPressureMultiplier, Is.EqualTo(1));

            lab.ClearEvents(); yield return null;
            config.stabilizationDuration = 20; config.requiredActivations = 1;
            relay = Spawn(lab, prefab, config); station = adapter.Station;
            movement.MovementIntent = () => relay != null ? OrbitalRelayBotSteering.GetDesiredMovement(relay, station, lab.Player.position) : Vector2.zero;
            yield return Await(() => relay.Snapshot.Phase == OrbitalRelayPhase.Bonus, 15f);
            var enemies = adapter.Enemies;
            Assert.That(enemies.WorldEventSpawnPressureMultiplier, Is.EqualTo(1.6f));
            lab.ClearEvents(); movement.MovementIntent = () => Vector2.zero;
            Assert.That(enemies.WorldEventSpawnPressureMultiplier, Is.EqualTo(1));
            Assert.That(enemies.IsSpawningEnabled, Is.False);
            Assert.That(lab.Events.ActiveEvent, Is.Null);

            yield return null;
            Set(lab, "relayRewardQueue", true);
            relay = Spawn(lab, prefab, config); station = adapter.Station;
            var currency = CurrencyManager.Instance;
            int goldBefore = currency.TotalGold;
            int opened = 0; UpgradeManager.Instance.RewardOpened += _ => opened++;
            movement.MovementIntent = () => relay != null ? OrbitalRelayBotSteering.GetDesiredMovement(relay, station, lab.Player.position) : Vector2.zero;
            yield return Await(() => relay.Snapshot.Phase == OrbitalRelayPhase.Transition, 15f);
            result = null; relay.Finished += completed => result = completed;
            relay.Cancel(); movement.MovementIntent = () => Vector2.zero;
            Assert.That(result.Value.Success, Is.True); Assert.That(result.Value.Gold, Is.Zero);
            yield return Await(() => UpgradeManager.Instance.IsChoosingUpgrade, 3f);
            Assert.That(opened, Is.EqualTo(1));
            lab.Events.NotifyEventCompleted(relay);
            Assert.That(opened, Is.EqualTo(1), "Duplicate/reentrant notification must not issue a second selection.");
            Assert.That(currency.TotalGold, Is.EqualTo(goldBefore), "Dev run must not persist Gold.");
            Assert.That(UpgradeManager.Instance.DebugSelectCurrentChoice(0), Is.True);
            if (station.RewardFlow.PendingReward.HasValue) Assert.That(station.RewardFlow.DebugChooseFirstValidTarget(), Is.True);
            yield return Await(() => UpgradeManager.Instance.IsRewardQueueIdle, 5f);
            Assert.That(adapter.Enemies.WorldEventSpawnPressureMultiplier, Is.EqualTo(1));
            Debug.Log("[RelaySmoke] fail, mid-Bonus reset, qualified cancel and shared one-selection queue passed.");

            // Isolated dispatcher contract: production currency mode, shared queue, fixed payout.
            currency.AddGoldGainPercent(1f);
            var payoutRoot = new GameObject("isolated fixed payout");
            var payout = payoutRoot.AddComponent<RelayRewardTestEvent>();
            typeof(WorldEvent).GetField("<IsCompleted>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(payout, true);
            ((List<WorldEvent>)typeof(WorldEventSpawner).GetField("spawnedEvents", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(lab.Events)).Add(payout);
            var devMode = typeof(RunStateManager).GetField("developmentRun", BindingFlags.Instance | BindingFlags.NonPublic);
            var run = RunStateManager.Instance;
            try
            {
                devMode.SetValue(run, false);
                lab.Events.NotifyEventCompleted(payout); lab.Events.NotifyEventCompleted(payout);
                Assert.That(currency.TotalGold, Is.EqualTo(goldBefore + 20), "Exact payout bypasses meta multiplier and duplicates.");
                Assert.That(opened, Is.EqualTo(2), "One new queue selection for one new completion.");
            }
            finally { devMode.SetValue(run, true); Object.Destroy(payoutRoot); }
            Assert.That(UpgradeManager.Instance.DebugSelectCurrentChoice(0), Is.True);
            if (station.RewardFlow.PendingReward.HasValue) Assert.That(station.RewardFlow.DebugChooseFirstValidTarget(), Is.True);
            yield return Await(() => UpgradeManager.Instance.IsRewardQueueIdle, 5f);

            lab.ClearEvents(); yield return null;
            Set(lab, "relayRewardQueue", false);
            relay = Spawn(lab, prefab, config); station = adapter.Station;
            movement.MovementIntent = () => relay != null ? OrbitalRelayBotSteering.GetDesiredMovement(relay, station, lab.Player.position) : Vector2.zero;
            yield return Await(() => relay.Snapshot.Phase == OrbitalRelayPhase.Bonus, 15f);
            enemies = adapter.Enemies;
            int administrativeResults = 0;
            lab.Events.EventCompleted += _ => administrativeResults++;
            var scene = lab.gameObject.scene;
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(UnityEngine.SceneManagement.SceneManager.CreateScene("RelayUnloadCheck"));
            yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
            yield return null;
            Assert.That(enemies.WorldEventSpawnPressureMultiplier, Is.EqualTo(1));
            Assert.That(administrativeResults, Is.Zero, "Unload must dispose without opening rewards.");
            Debug.Log("[RelaySmoke] Exact Gold (meta multiplier bypass), deduplication and mid-Bonus unload passed.");
        }
        finally { if (movement != null) movement.MovementIntent = null; Object.Destroy(config); if (lab != null) lab.ClearEvents(); }
    }
    private static OrbitalRelayEvent Spawn(WorldSystemsLabController lab, OrbitalRelayEvent prefab, OrbitalRelayConfig config)
    {
        var field = typeof(OrbitalRelayEvent).GetField("config", BindingFlags.NonPublic | BindingFlags.Instance);
        var original = field.GetValue(prefab);
        try { field.SetValue(prefab, config); Assert.That(lab.SpawnEvent(prefab), Is.True); return (OrbitalRelayEvent)lab.Events.ActiveEvent; }
        finally { field.SetValue(prefab, original); }
    }
    private static void Set(object target, string name, object value) => target.GetType().GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static IEnumerator Await(Func<bool> condition, float seconds)
    {
        float deadline = Time.realtimeSinceStartup + seconds;
        while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(condition(), Is.True, "Bounded relay-only wait expired.");
    }
    private static IEnumerator Capture(string name)
    {
        Directory.CreateDirectory("Artifacts/OrbitalRelay");
        Canvas.ForceUpdateCanvases();
        ScreenCapture.CaptureScreenshot("Artifacts/OrbitalRelay/" + name + ".png");
        yield return null;
    }
}
#endif
