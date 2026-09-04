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

public sealed class Subject42OrbitalTransactionSceneTests
{
    private static object Get(object target, string field) => target.GetType()
        .GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void Call(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    private static string Snapshot(OrbitalStationRuntime station) => Subject42OrbitalRestoreTests.Snapshot(station.State);
    [UnityTest]
    public IEnumerator RealScene_RewardQueueF1PauseAndWorld()
    {
        yield return new EnterPlayMode();
        yield return Run();
        yield return new ExitPlayMode();
    }
    private static IEnumerator Run()
    {
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

        station.enabled = false; // Freeze phase only, real scene/controllers/UI/queue remain alive.
        var upgrades = UpgradeManager.Instance;
        var flow = station.RewardFlow;
        var owner = station.InputOwner;
        var choices = Object.FindFirstObjectByType<LevelChoiceManager>();
        var sector = RunStateManager.Instance.CurrentSector;
        Assert.That(upgrades.IsRewardQueueIdle, Is.True);
        Time.timeScale = 1f;
        Assert.That(upgrades.DebugForceOrbitalReward(OrbitalRewardKind.ArcEmitter), Is.True);
        Assert.That(upgrades.DebugSelectCurrentChoice(0), Is.True);
        string before = Snapshot(station);
        Assert.That(flow.DebugChooseMount(1, 1), Is.True);
        Assert.That(upgrades.IsRewardQueueIdle, Is.False);
        Call(choices, "TransitionToSector", sector);
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("MVP"));
        Assert.That(Snapshot(station), Is.EqualTo(before));
        var rewardPause = Object.FindFirstObjectByType<PauseMenuUI>();
        Call(rewardPause, "HandleEscape");
        Assert.That(rewardPause.IsPaused, Is.False, "reward consumes first Escape");
        Assert.That(Snapshot(station), Is.EqualTo(before));
        Assert.That(upgrades.IsRewardQueueIdle, Is.False, "cancel returns to same card request");
        Assert.That((bool)Get(upgrades, "hasCurrentRequest"), Is.True);
        Assert.That(Time.timeScale, Is.Zero);
        yield return null;
        Call(rewardPause, "HandleEscape");
        Assert.That(rewardPause.IsPaused, Is.True, "next Escape opens Pause over reward cards");
        rewardPause.Resume();
        Assert.That(Time.timeScale, Is.Zero, "closing Pause cannot unpause reward queue");
        Assert.That(upgrades.DebugSelectCurrentChoice(0), Is.True);
        flow.DebugChooseMount(1, 1);
        deadline = Time.realtimeSinceStartup + 3f;
        while (flow.PendingReward != null && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(upgrades.IsRewardQueueIdle, Is.True);
        Assert.That(station.State.Modules.Count, Is.EqualTo(2));
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        Debug.Log("PASS3 real queue normal cancel: current request retained, state unchanged; retry completes queue once. " + Snapshot(station));
        station.AddRing();
        Assert.That(upgrades.DebugForceOrbitalReward(OrbitalRewardKind.LinkPair), Is.True);
        upgrades.DebugSelectCurrentChoice(0);
        before = Snapshot(station);
        flow.DebugChooseMount(2, 0);
        deadline = Time.realtimeSinceStartup + 3f;
        while (flow.State != OrbitalRewardFlowState.SecondLinkPlacement && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(Snapshot(station), Is.EqualTo(before));
        Assert.That(owner.CanTransition, Is.False);
        flow.CancelForSceneTransition();
        Assert.That(Snapshot(station), Is.EqualTo(before));
        Assert.That(upgrades.IsRewardQueueIdle, Is.False);
        upgrades.DebugSelectCurrentChoice(0);
        flow.DebugChooseMount(2, 0);
        deadline = Time.realtimeSinceStartup + 3f;
        while (flow.State != OrbitalRewardFlowState.SecondLinkPlacement && Time.realtimeSinceStartup < deadline) yield return null;
        flow.DebugChooseMount(2, 1);
        ulong session = flow.SessionToken, flight = (ulong)Get(flow, "flightToken");
        deadline = Time.realtimeSinceStartup + 3f;
        while (flow.PendingReward != null && Time.realtimeSinceStartup < deadline) yield return null;
        string committed = Snapshot(station);
        Call(flow, "FinishFlight", session, flight, true);
        Assert.That(Snapshot(station), Is.EqualTo(committed));
        Assert.That(upgrades.IsRewardQueueIdle, Is.True);
        Assert.That((bool)Get(upgrades, "hasCurrentRequest"), Is.False);
        Assert.That(station.State.Modules.Count, Is.EqualTo(4));
        Debug.Log("PASS3 real queue Link cancel: current request retained; success + duplicate: current=0, pending=0, milestone=0; " + committed);
        yield return null;
        var relocation = station.GetComponent<OrbitalRelocationController>();
        relocation.enabled = false;
        Call(relocation, "BeginDrag", station.Modules[0]);
        Assert.That(relocation.IsDragging, Is.True);
        var menu = Object.FindFirstObjectByType<Subject42DebugMenu>();
        Assert.That(menu, Is.Not.Null);
        Call(menu, "SetOpen", true);
        Assert.That(owner.IsGameplayInputBlocked, Is.True);
        Assert.That(relocation.IsDragging, Is.False);
        Assert.That(owner.CanStartRelocation, Is.False);
        Assert.That(owner.CanStartWorldTelekinesis, Is.False);
        Call(menu, "SetOpen", false);
        Assert.That(owner.IsGameplayInputBlocked, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(1f), "F1 must not restore abandoned drag slow motion");
        yield return null;
        var pause = Object.FindFirstObjectByType<PauseMenuUI>();
        Assert.That(pause, Is.Not.Null);
        Call(relocation, "BeginDrag", station.Modules[0]);
        Call(pause, "HandleEscape"); // Pause executes before interaction Update.
        Assert.That(relocation.IsDragging, Is.False);
        Assert.That((bool)Get(pause, "isPaused"), Is.False);
        Call(pause, "HandleEscape"); // Same-frame repeated observer.
        Assert.That((bool)Get(pause, "isPaused"), Is.False);
        yield return null;
        Call(pause, "HandleEscape");
        Assert.That((bool)Get(pause, "isPaused"), Is.True);
        Assert.That(Time.timeScale, Is.Zero);
        pause.Resume();
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        var world = station.GetComponent<OrbitalWorldTelekinesisController>();
        world.enabled = false;
        var pickup = new GameObject("Telekinesis smoke pickup").AddComponent<ExperiencePickup>();
        pickup.transform.position = station.transform.position + Vector3.right * 2f;
        Call(world, "BeginHold", pickup);
        Assert.That(world.IsHolding, Is.True);
        Assert.That(owner.Mode, Is.EqualTo(OrbitalInteractionMode.WorldTelekinesis));
        Call(world, "ReleaseHeld");
        Assert.That(world.IsHolding, Is.False);
        Assert.That(owner.IsIdle, Is.True);
        Object.Destroy(pickup.gameObject);
        Debug.Log("PASS3 real scene smoke: F1 closed/open/closed, drag cancel, Pause-first Escape arbitration, next Escape Pause, RMB hold/release domain path PASS.");
        Time.timeScale = 1f;
    }
}
#endif
