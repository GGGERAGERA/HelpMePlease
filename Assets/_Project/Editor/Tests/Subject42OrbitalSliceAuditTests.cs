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
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class Subject42OrbitalSliceAuditTests
{
    const string Output = "Artifacts/GeneratedQA/OrbitalProductionPass/Regression/";
    const string WeaponKey = "BunkerStationLevel_Weapon";
    static object Get(object target, string field) => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    [Serializable] public class Card { public string id, title, description, source; public float weight; public int width, height; }
    [Serializable] public class Cards { public List<Card> cards = new(); }

    [UnityTearDown]
    public IEnumerator CleanupPlayMode()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator AllProductionRewards_CardsApplyAndRestore()
    {
        Directory.CreateDirectory(Output);
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        bool hadKey = PlayerPrefs.HasKey(WeaponKey);
        int oldLevel = PlayerPrefs.GetInt(WeaponKey);
        var preferenceKeys = AssetDatabase.FindAssets("t:UnlockableContentData")
            .Select(id => AssetDatabase.LoadAssetAtPath<UnlockableContentData>(AssetDatabase.GUIDToAssetPath(id)))
            .SelectMany(c => new[] { "Unlock_" + c.id, "UnlockProgress_" + c.id })
            .Append("TOTAL_GOLD").Distinct().ToDictionary(k => k, k => (exists: PlayerPrefs.HasKey(k), value: PlayerPrefs.GetInt(k)));
        try
        {
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
            Assert.That(starter, Is.Not.Null);
            var character = AssetDatabase.FindAssets("t:CharacterData").Select(id => AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(id))).First(c => c.characterPrefab != null);
            RunSelectionManager.Instance.SelectCharacter(character);
            starter.StartRun((Transform)Get(starter, "cameraRig"));
            float deadline = Time.realtimeSinceStartup + 35;
            OrbitalStationRuntime station = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
                if (SceneManager.GetActiveScene().name == "MVP" && station != null && station.IsInitialized && !SceneTransitionOverlay.IsTransitioning) break;
                yield return null;
            }
            Assert.That(station != null && station.IsInitialized, Is.True);
            PlayerPrefs.SetInt(WeaponKey, 1); // fresh demo station; reward gates must not hide weapons
            Time.timeScale = 0;
            var manager = UpgradeManager.Instance;
            var cards = new Cards();
            Assert.That(station.State.RingOfferMissCount, Is.Zero);
            Assert.That(station.State.Rings.Count, Is.EqualTo(1));
            Assert.That(station.State.RingOfferMissCount, Is.Zero);
            ScreenCapture.CaptureScreenshot(Output + "start-one-ring.png"); yield return null;
            var countField = typeof(UpgradeManager).GetField("choicesCount", BindingFlags.Instance | BindingFlags.NonPublic);
            countField.SetValue(manager, 100);
            int SeedFor(bool offer)
            {
                for (int seed = 0; ; seed++)
                {
                    UnityEngine.Random.InitState(seed);
                    if ((UnityEngine.Random.value < .05f) == offer) return seed;
                }
            }
            int missSeed = SeedFor(false), offerSeed = SeedFor(true);
            UnityEngine.Random.InitState(missSeed);
            manager.ShowLevelUpChoices(2);
            Assert.That(station.State.Rings.Count, Is.EqualTo(1), "level 2 no automatic ring");
            Assert.That(station.State.RingOfferMissCount, Is.EqualTo(1));
            int Choice(OrbitalRewardKind k) => manager.DebugCurrentChoices.ToList().FindIndex(c => ((OrbitalRewardData)c).RewardKind == k);
            Assert.That(Choice(OrbitalRewardKind.NewRing), Is.EqualTo(-1));
            manager.DebugSelectCurrentChoice(Choice(OrbitalRewardKind.RingPower));
            station.RewardFlow.DebugChooseRing(1);
            UnityEngine.Random.InitState(offerSeed);
            manager.ShowLevelUpChoices(3);
            Assert.That(Choice(OrbitalRewardKind.NewRing), Is.GreaterThanOrEqualTo(0));
            manager.DebugSelectCurrentChoice(Choice(OrbitalRewardKind.RingSpeed));
            station.RewardFlow.CancelForSceneTransition();
            Assert.That(station.State.RingOfferMissCount, Is.EqualTo(2), "cancel does not reroll or reset");
            manager.DebugSelectCurrentChoice(Choice(OrbitalRewardKind.RingSpeed));
            station.RewardFlow.DebugChooseRing(1);
            Assert.That(station.State.RingOfferMissCount, Is.EqualTo(2), "offered and skipped");
            UnityEngine.Random.InitState(offerSeed);
            countField.SetValue(manager, 3);
            manager.ShowLevelUpChoices(4);
            yield return null; Canvas.ForceUpdateCanvases();
            ScreenCapture.CaptureScreenshot(Output + "new-ring-card.png"); yield return null;
            manager.DebugSelectCurrentChoice(Choice(OrbitalRewardKind.NewRing));
            Assert.That(station.State.Rings.Count, Is.EqualTo(2));
            Assert.That(station.State.RingOfferMissCount, Is.Zero);
            yield return null;
            yield return new WaitForSecondsRealtime(.65f);
            ScreenCapture.CaptureScreenshot(Output + "acquired-second-ring.png"); yield return null;

            using var provider = new OrbitalRewardProvider(manager.AllUpgrades.ToArray());
            File.WriteAllText(Output + "flow.txt", "Real MainMenu -> BunkerRunStarter -> MVP; DebugForce/Select use production panel/flow. No physical input claim.\n");
            Assert.That(manager.DebugForceOrbitalReward(OrbitalRewardKind.Pistol), Is.True);
            manager.DebugSelectCurrentChoice(0);
            string cancelSnapshot = Subject42OrbitalRestoreTests.Snapshot(station.State);
            var escapeMenu = Object.FindFirstObjectByType<PauseMenuUI>();
            var handleEscape = typeof(PauseMenuUI).GetMethod("HandleEscape", BindingFlags.Instance | BindingFlags.NonPublic);
            handleEscape.Invoke(escapeMenu, null);
            Assert.That(escapeMenu.IsPaused, Is.False);
            Assert.That(station.RewardFlow.PendingReward, Is.Null);
            int escapeFrame = Time.frameCount;
            while (Time.frameCount <= escapeFrame) yield return null;
            handleEscape.Invoke(escapeMenu, null);
            Assert.That(escapeMenu.IsPaused, Is.True);
            escapeMenu.Resume();
            Assert.That(Time.timeScale, Is.Zero);
            manager.DebugSelectCurrentChoice(0);
            typeof(OrbitalInteractionController).GetMethod("CancelActive", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(station.InputOwner, null);
            Assert.That(Subject42OrbitalRestoreTests.Snapshot(station.State), Is.EqualTo(cancelSnapshot));
            manager.enabled = false; manager.enabled = true;
            File.AppendAllText(Output + "flow.txt", "PASS Escape handler cancels target, next real frame opens Pause, Resume retains reward pause; RMB shared CancelActive path leaves state unchanged. Physical key injection not used.\n");
            foreach (OrbitalRewardKind kind in Enum.GetValues(typeof(OrbitalRewardKind)).Cast<OrbitalRewardKind>().OrderBy(k => OrbitalRewardProvider.IsDemoReward(k) ? 0 : 1))
            {
                if (kind == OrbitalRewardKind.LinkPair || !station.State.Rings.Any(r => station.State.HasFreeMount(r.StableRingId))) station.AddRing();
                if (kind == OrbitalRewardKind.AddMount)
                    for (int m = 0; m < station.State.Rings[0].MountCapacity; m++)
                        if (station.State.IsMountFree(1, m)) station.InstallModule(OrbitalModuleKind.Pistol, 1, m, out _);
                Assert.That(manager.DebugForceOrbitalReward(kind), Is.True, kind + " eligible/appears");
                var definition = (OrbitalRewardData)manager.DebugCurrentChoices[0];
                ExportIcon(definition, cards);
                yield return null;
                Canvas.ForceUpdateCanvases();
                ScreenCapture.CaptureScreenshot(Output + "card-" + kind + ".png");
                yield return null;
                int revision = station.State.Revision;
                int bodyLevel = definition.BodyUpgrade != null ? RunStateManager.Instance.ItemSlots.GetLevel(definition.BodyUpgrade) : -1;
                Assert.That(manager.DebugSelectCurrentChoice(0), Is.True, kind + " selectable");
                var flow = station.RewardFlow;
                if (kind == OrbitalRewardKind.RingSpeed || kind == OrbitalRewardKind.RingPower || kind == OrbitalRewardKind.AddMount)
                    Assert.That(flow.DebugChooseRing(station.State.Rings[0].StableRingId), Is.True);
                else if (kind == OrbitalRewardKind.ModuleDamage)
                    Assert.That(flow.DebugChooseModule(station.State.Modules.First(m => m.ModuleType != OrbitalModuleKind.LinkNode).StableModuleId), Is.True);
                else if (definition.RequiresArenaSelection)
                {
                    var ring = station.State.Rings.First(r => station.State.HasFreeMount(r.StableRingId));
                    int mount = Enumerable.Range(0, ring.MountCapacity).First(m => station.State.IsMountFree(ring.StableRingId, m));
                    Assert.That(flow.DebugChooseMount(ring.StableRingId, mount), Is.True);
                    deadline = Time.realtimeSinceStartup + 3;
                    while (flow.State == OrbitalRewardFlowState.ModuleFlight && Time.realtimeSinceStartup < deadline) yield return null;
                    if (kind == OrbitalRewardKind.LinkPair)
                    {
                        Assert.That(flow.State, Is.EqualTo(OrbitalRewardFlowState.SecondLinkPlacement));
                        var second = station.State.Rings.SelectMany(r => Enumerable.Range(0, r.MountCapacity).Select(m => (r.StableRingId, m))).First(p => station.State.CanInstallLinkPair(ring.StableRingId, mount, p.StableRingId, p.m, out _));
                        flow.DebugChooseMount(second.StableRingId, second.m);
                    }
                }
                deadline = Time.realtimeSinceStartup + 3;
                while (!manager.IsRewardQueueIdle && Time.realtimeSinceStartup < deadline) yield return null;
                Assert.That(manager.IsRewardQueueIdle, Is.True, kind + " queue completed");
                Assert.That(station.State.Validate(out var error), Is.True, error);
                if (bodyLevel >= 0)
                {
                    Assert.That(RunStateManager.Instance.ItemSlots.GetLevel(definition.BodyUpgrade), Is.EqualTo(bodyLevel + 1));
                    if (kind == OrbitalRewardKind.MaxHealth)
                        Assert.That(station.Owner.Transform.GetComponent<PlayerHealth>().RunUpgradeMaxHealthBonus, Is.EqualTo(20));
                    else Assert.That(station.Owner.Transform.GetComponent<CharacterMovement2D>().RunUpgradeMoveSpeedMultiplier, Is.EqualTo(1.1f));
                }
                else Assert.That(station.State.Revision, Is.EqualTo(revision + 1), kind + " one commit");
                if (kind == OrbitalRewardKind.NewRing)
                {
                    // Capture the demo-only build before exercising retained future/debug rewards.
                    Assert.That(station.UpgradeRingSpeed(station.State.Rings[1].StableRingId), Is.True);
                    Time.timeScale = 1f;
                    yield return new WaitForSecondsRealtime(.65f);
                    ScreenCapture.CaptureScreenshot(Output + "mixed-upgraded-rings.png"); yield return null;
                    var demoPause = Object.FindFirstObjectByType<PauseMenuUI>();
                    demoPause.Pause(); Assert.That(demoPause.IsPaused, Is.True);
                    yield return null;
                    var overview = Object.FindFirstObjectByType<PauseBuildOverview>(FindObjectsInactive.Include);
                    string text = string.Join("\n", overview.GetComponentsInChildren<TMPro.TMP_Text>(true).Select(t => t.text));
                    Assert.That(text, Does.Contain("CORE I"));
                    Assert.That(text, Does.Not.Contain("Core Pulse").And.Not.Contain("Core Cascade").And.Not.Contain("pity"));
                    File.WriteAllText(Output + "demo-pause.txt", text);
                    ScreenCapture.CaptureScreenshot(Output + "pause-build-overview.png"); yield return null;
                    demoPause.Resume(); Time.timeScale = 0;
                }
                File.AppendAllText(Output + "flow.txt", "PASS " + kind + " appears/select/target/apply/queue; " + station.State.ToCompactString(1) + "\n");
                yield return null;
            }
            for (int level = 2; level <= 3; level++)
            {
                Assert.That(manager.DebugForceOrbitalReward(OrbitalRewardKind.CoreUpgrade), Is.True);
                ExportIcon((OrbitalRewardData)manager.DebugCurrentChoices[0], cards, "CoreUpgrade" + level);
                manager.DebugSelectCurrentChoice(0);
                Assert.That(station.State.CoreState.Level, Is.EqualTo(level));
            }
            File.WriteAllText(Output + "cards.json", JsonUtility.ToJson(cards, true));
            string snapshot = Subject42OrbitalRestoreTests.Snapshot(station.State);
            station.RebuildRuntimeFromState();
            Assert.That(Subject42OrbitalRestoreTests.Snapshot(station.State), Is.EqualTo(snapshot));
            File.AppendAllText(Output + "flow.txt", "PASS all selected orbital stats retained after runtime rebuild. Body values verified as slot levels only.\n");
            var pause = Object.FindFirstObjectByType<PauseBuildOverview>(FindObjectsInactive.Include);
            pause.Refresh(RunStateManager.Instance);
            File.WriteAllText(Output + "pause.txt", string.Join("\n", pause.GetComponentsInChildren<TMPro.TMP_Text>(true).Select(t => t.text)));

            // The last valid ring disappears between card preview and its click.
            foreach (var r in station.State.Rings.Take(station.State.Rings.Count - 1))
                while (station.UpgradeRingSpeed(r.StableRingId)) { }
            var last = station.State.Rings.Last();
            if (!station.State.Modules.Any(m => m.StableRingId == last.StableRingId))
                station.InstallModule(OrbitalModuleKind.Pistol, last.StableRingId, 0, out _);
            Assert.That(manager.DebugForceOrbitalReward(OrbitalRewardKind.RingSpeed), Is.True);
            while (station.UpgradeRingSpeed(last.StableRingId)) { }
            manager.DebugSelectCurrentChoice(0);
            Assert.That(station.RewardFlow.PendingReward, Is.Null, "stale card must not enter targetless ring selection");
            Assert.That(manager.DebugCurrentChoices.OfType<OrbitalRewardData>().Any(c => c.RewardKind == OrbitalRewardKind.RingSpeed), Is.False);
            File.AppendAllText(Output + "flow.txt", "PASS stale RingSpeed preview rejected; refreshed hand excludes capped reward.\n");
            // Finish the replacement hand through an immediate body or ring reward.
            var choiceIndex = manager.DebugCurrentChoices.ToList().FindIndex(c => c is OrbitalRewardData d && (d.RewardKind == OrbitalRewardKind.RingPower || d.RewardKind == OrbitalRewardKind.AddMount || d.BodyUpgrade != null));
            if (choiceIndex < 0) choiceIndex = 0;
            var replacement = (OrbitalRewardData)manager.DebugCurrentChoices[choiceIndex];
            manager.DebugSelectCurrentChoice(choiceIndex);
            if (station.RewardFlow.PendingReward != null) station.RewardFlow.CancelForSceneTransition();
            // Close pending hand via existing manager lifecycle before starting dedicated cancellation case.
            manager.enabled = false; manager.enabled = true;
            Assert.That(manager.IsRewardQueueIdle, Is.True);
            foreach (var r in station.State.Rings)
                for (int m = 0; m < r.MountCapacity; m++)
                    if (station.State.IsMountFree(r.StableRingId, m)) station.InstallModule(OrbitalModuleKind.Pistol, r.StableRingId, m, out _);
            Assert.That(manager.DebugForceOrbitalReward(OrbitalRewardKind.AddMount), Is.True);
            manager.DebugSelectCurrentChoice(0);
            foreach (var r in station.State.Rings) while (station.AddMount(r.StableRingId, out _)) { }
            station.RewardFlow.CancelForSceneTransition();
            Assert.That(manager.DebugCurrentChoices.OfType<OrbitalRewardData>().Any(c => c.RewardKind == OrbitalRewardKind.AddMount), Is.False, "cancel must not revive stale cards");
            manager.enabled = false; manager.enabled = true;
            File.AppendAllText(Output + "flow.txt", "PASS cancel after target mutation filters old hand; manager disable/re-enable clears queue.\n");

            station.State.BeginLevelUpOpportunity(5, .99f);
            station.State.BeginLevelUpOpportunity(6, .99f);
            Assert.That(station.State.RingOfferMissCount, Is.EqualTo(2));
            ScreenCapture.CaptureScreenshot(Output + "debug-all-systems-rings.png"); yield return null;
            escapeMenu = Object.FindFirstObjectByType<PauseMenuUI>();
            Time.timeScale = 1f;
            escapeFrame = Time.frameCount;
            while (Time.frameCount <= escapeFrame) yield return null;
            handleEscape.Invoke(escapeMenu, null);
            Assert.That(escapeMenu.IsPaused, Is.True);
            yield return null;
            ScreenCapture.CaptureScreenshot(Output + "debug-all-systems-pause.png"); yield return null;
            escapeMenu.Resume();
            // One complete upgraded build traverses the real sector transition path twice.
            for (int sectorNumber = 2; sectorNumber <= 3; sectorNumber++)
            {
                var run = RunStateManager.Instance;
                var state = station.State;
                string before = Subject42OrbitalRestoreTests.Snapshot(state);
                var stage = AssetDatabase.FindAssets("t:StageProfileData").Select(id => AssetDatabase.LoadAssetAtPath<StageProfileData>(AssetDatabase.GUIDToAssetPath(id))).First(s => s.SectorNumber == sectorNumber);
                var next = new RunSector(sectorNumber, stage, run.CurrentSector.WorldRule, run.CurrentSector.LocalAnomaly);
                int oldScene = station.gameObject.scene.handle;
                typeof(LevelChoiceManager).GetMethod("TransitionToSector", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(Object.FindFirstObjectByType<LevelChoiceManager>(), new object[] { next });
                deadline = Time.realtimeSinceStartup + 45;
                while (Time.realtimeSinceStartup < deadline)
                {
                    station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
                    if (station != null && station.gameObject.scene.handle != oldScene && station.IsInitialized && !SceneTransitionOverlay.IsTransitioning) break;
                    yield return null;
                }
                Assert.That(station, Is.Not.Null);
                Time.timeScale = 0;
                Assert.That(station.gameObject.scene.handle, Is.Not.EqualTo(oldScene));
                Assert.That(station.State, Is.SameAs(state));
                Assert.That(station.State.RingOfferMissCount, Is.EqualTo(2));
                string WithoutPhase(string s) => System.Text.RegularExpressions.Regex.Replace(s, @"CurrentPhase=[^,}]+", "CurrentPhase=<live>");
                Assert.That(WithoutPhase(Subject42OrbitalRestoreTests.Snapshot(station.State)), Is.EqualTo(WithoutPhase(before)));
                Assert.That(station.Owner.Transform.GetComponent<PlayerHealth>().RunUpgradeMaxHealthBonus, Is.GreaterThanOrEqualTo(20));
                Assert.That(station.Owner.Transform.GetComponent<CharacterMovement2D>().RunUpgradeMoveSpeedMultiplier, Is.GreaterThanOrEqualTo(1.1f));
                File.AppendAllText(Output + "flow.txt", $"PASS actual sector {sectorNumber}: all orbital fields except live phase retained, HP/Speed replayed.\n");
            }

            // Exercise actual final-boss lifecycle, with a forced lethal hit (not a combat balance test).
            station.enabled = false;
            Object.FindFirstObjectByType<EnemySpawner>().StopSpawning();
            foreach (var enemy in Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None)) Object.Destroy(enemy.gameObject);
            Assert.That(RunFlowController.Instance.HandleExitReached(), Is.True);
            Time.timeScale = 1;
            deadline = Time.realtimeSinceStartup + 45;
            while (RunFlowController.Instance.FinalBoss == null && Time.realtimeSinceStartup < deadline) yield return null;
            var boss = RunFlowController.Instance.FinalBoss;
            Assert.That(boss, Is.Not.Null);
            boss.TakeDamage(1000000, boss.transform.position);
            yield return WaitForScene("MainMenu");
            Assert.That(RunStateManager.Instance.OrbitalStationState, Is.Null, "victory ends station ownership");
            File.AppendAllText(Output + "flow.txt", "PASS final exit -> boss intro/spawn -> forced real boss death -> victory -> bunker; state cleared. Not a boss balance playthrough.\n");

            starter = Object.FindFirstObjectByType<BunkerRunStarter>();
            RunSelectionManager.Instance.SelectCharacter(character);
            starter.StartRun((Transform)Get(starter, "cameraRig"));
            yield return WaitForScene("MVP");
            station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
            Assert.That(station.State.RingOfferMissCount, Is.Zero);
            Assert.That(station.State.Rings.Count, Is.EqualTo(1));
            Assert.That(station.State.Modules.Count, Is.EqualTo(1));
            Assert.That(station.State.CoreState.Level, Is.Zero);
            Time.timeScale = 0;
            int runId = station.State.RunId;
            var pauseMenu = Object.FindFirstObjectByType<PauseMenuUI>();
            typeof(PauseMenuUI).GetMethod("RestartConfirmed", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pauseMenu, null);
            yield return WaitForReload(station.gameObject.scene.handle);
            station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
            Assert.That(station.State.RunId, Is.EqualTo(runId + 1));
            Assert.That(station.State.RingOfferMissCount, Is.Zero);
            Assert.That(station.State.Rings.Count, Is.EqualTo(1));
            Time.timeScale = 0;
            manager = UpgradeManager.Instance;
            Assert.That(manager.DebugForceOrbitalReward(OrbitalRewardKind.Pistol), Is.True);
            manager.DebugSelectCurrentChoice(0);
            var deathState = station.State;
            deathState.BeginLevelUpOpportunity(2, .99f);
            deathState.BeginLevelUpOpportunity(3, .99f);
            var deathFlow = station.RewardFlow;
            station.Owner.Transform.GetComponent<PlayerHealth>().TakeDamage(1000000, Vector2.zero);
            int deathFrame = Time.frameCount;
            while (Time.frameCount <= deathFrame + 1) yield return null;
            Assert.That(deathFlow.PendingReward, Is.Null, "death cancels pending placement before teardown clears runtime references");
            Assert.That(manager.IsRewardQueueIdle, Is.True, "death terminates the reward queue");
            manager.ShowLevelUpChoices(4);
            manager.GrantSpecialAnomalyRing();
            Assert.That(manager.IsRewardQueueIdle, Is.True, "late XP callback cannot reopen a dead run");
            Assert.That(deathState.RingOfferMissCount, Is.EqualTo(2), "late callback cannot advance pity");
            Assert.That(deathState.Rings.Count, Is.EqualTo(1), "late anomaly completion cannot reward a dead run");
            Assert.That(station.IsInitialized, Is.False, "dead station tears down runtime");
            Assert.That(deathState.Modules.Count, Is.EqualTo(1), "uncommitted reward not installed after death");
            runId = deathState.RunId;
            GameOverManager.Instance.RestartGame();
            yield return WaitForReload(station.gameObject.scene.handle);
            station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
            Assert.That(station.State.RunId, Is.EqualTo(runId + 1));
            Assert.That(station.State.Modules.Count, Is.EqualTo(1));
            Assert.That(UpgradeManager.Instance.IsRewardQueueIdle, Is.True);
            Assert.That(station.State.RingOfferMissCount, Is.Zero, "restart discards dead run pity");
            Time.timeScale = 0;
            Assert.That(UpgradeManager.Instance.DebugForceOrbitalReward(OrbitalRewardKind.Pistol), Is.True);
            UpgradeManager.Instance.DebugSelectCurrentChoice(0);
            RunEndService.Instance.ReturnToBunker();
            yield return WaitForScene("MainMenu");
            Assert.That(RunStateManager.Instance.OrbitalStationState, Is.Null);
            File.AppendAllText(Output + "flow.txt", "PASS repeated run, Pause restart, death during reward placement, GameOver restart, return to bunker during reward. Production lifecycle methods invoked directly; preferences restored.\n");
        }
        finally
        {
            foreach (var p in preferenceKeys)
                if (p.Value.exists) PlayerPrefs.SetInt(p.Key, p.Value.value); else PlayerPrefs.DeleteKey(p.Key);
            if (hadKey) PlayerPrefs.SetInt(WeaponKey, oldLevel); else PlayerPrefs.DeleteKey(WeaponKey);
            PlayerPrefs.Save();
            Time.timeScale = 1;
        }
        yield return new ExitPlayMode();
    }

    static IEnumerator WaitForScene(string scene)
    {
        float deadline = Time.realtimeSinceStartup + 45;
        do { yield return null; }
        while ((SceneManager.GetActiveScene().name != scene || SceneTransitionOverlay.IsTransitioning) && Time.realtimeSinceStartup < deadline);
        Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo(scene));
        Assert.That(SceneTransitionOverlay.IsTransitioning, Is.False);
    }

    [UnityTest]
    public IEnumerator FullStation_EligibilityAndFlightConflict()
    {
        yield return new EnterPlayMode();
        Application.runInBackground = true;
        Directory.CreateDirectory(Output);
        yield return SceneManager.LoadSceneAsync("MainMenu");
        yield return null;
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        var character = AssetDatabase.FindAssets("t:CharacterData").Select(id => AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(id))).First(c => c.characterPrefab != null);
        RunSelectionManager.Instance.SelectCharacter(character);
        starter.StartRun((Transform)Get(starter, "cameraRig"));
        yield return WaitForScene("MVP");
        Time.timeScale = 0;
        var station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
        var manager = UpgradeManager.Instance;
        var flow = station.RewardFlow;
        using (var provider = new OrbitalRewardProvider(manager.AllUpgrades.ToArray()))
        {
        // An external installation takes the selected mount while a reward is in flight.
        Assert.That(manager.DebugForceOrbitalReward(OrbitalRewardKind.Pistol), Is.True);
        manager.DebugSelectCurrentChoice(0);
        flow.DebugChooseMount(1, 1);
        for (int click = 0; click < 10; click++) Assert.That(flow.DebugChooseMount(1, 1), Is.False, "flight ignores repeated target clicks");
        Assert.That(station.InstallModule(OrbitalModuleKind.ArcEmitter, 1, 1, out _), Is.True);
        float deadline = Time.realtimeSinceStartup + 5;
        while (flow.State == OrbitalRewardFlowState.ModuleFlight && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(flow.State, Is.EqualTo(OrbitalRewardFlowState.DirectMountSelection));
        Assert.That(manager.IsRewardQueueIdle, Is.False);
        Assert.That(station.State.Modules.Count, Is.EqualTo(2), "only the external install committed");
        flow.DebugChooseMount(1, 2);
        ulong session = flow.SessionToken;
        ulong flight = (ulong)Get(flow, "flightToken");
        deadline = Time.realtimeSinceStartup + 5;
        while (!manager.IsRewardQueueIdle && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(manager.IsRewardQueueIdle, Is.True);
        string committed = Subject42OrbitalRestoreTests.Snapshot(station.State);
        for (int click = 0; click < 10; click++)
            typeof(OrbitalRewardFlowController).GetMethod("FinishFlight", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(flow, new object[] { session, flight, false });
        Assert.That(Subject42OrbitalRestoreTests.Snapshot(station.State), Is.EqualTo(committed));
        File.WriteAllText(Output + "stress.txt", "PASS target became occupied during real flight -> retry another mount -> one reward commit; 10 repeated target clicks and 10 duplicate completion callbacks ignored.\n");

        while (station.State.Rings.Count < 8) Assert.That(station.AddRing(), Is.Not.Null);
        foreach (var ring in station.State.Rings)
        {
            Assert.That(station.AddMount(ring.StableRingId, out _), Is.True);
            foreach (int mount in Enumerable.Range(0, ring.MountCapacity))
                if (station.State.IsMountFree(ring.StableRingId, mount))
                    Assert.That(station.InstallModule(OrbitalModuleKind.Pistol, ring.StableRingId, mount, out _), Is.True);
        }
        Assert.That(station.State.Modules.Count, Is.EqualTo(32));
        Assert.That(provider.GetEligibleKinds().Any(k => k <= OrbitalRewardKind.LinkPair || k == OrbitalRewardKind.AddMount), Is.False);
        Assert.That(manager.DebugForceOrbitalReward(OrbitalRewardKind.Pistol), Is.False);
        Assert.That(manager.DebugForceOrbitalReward(OrbitalRewardKind.LinkPair), Is.False);
        Assert.That(manager.DebugForceOrbitalReward(OrbitalRewardKind.RingPower), Is.True);
        manager.DebugSelectCurrentChoice(0);
        flow.DebugChooseRing(8);
        Assert.That(station.State.FindRing(8).PowerUpgradeLevel, Is.EqualTo(1));
        Assert.That(station.State.FindRing(1).PowerUpgradeLevel, Is.Zero);
        Assert.That(manager.IsRewardQueueIdle, Is.True);
        Assert.That(manager.DebugForceOrbitalReward(OrbitalRewardKind.ModuleDamage), Is.True);
        manager.DebugSelectCurrentChoice(0);
        int targetId = station.State.Modules.Last().StableModuleId;
        flow.DebugChooseModule(targetId);
        Assert.That(station.State.FindModule(targetId).DamageLevel, Is.EqualTo(1));
        Assert.That(station.State.Modules.Count(m => m.DamageLevel > 0), Is.EqualTo(1));
        committed = Subject42OrbitalRestoreTests.Snapshot(station.State);
        station.RebuildRuntimeFromState();
        Assert.That(station.Rings.Count, Is.EqualTo(8));
        Assert.That(station.Modules.Count, Is.EqualTo(32));
        Assert.That(Subject42OrbitalRestoreTests.Snapshot(station.State), Is.EqualTo(committed));
        float settle = Time.realtimeSinceStartup + 1;
        while (Time.realtimeSinceStartup < settle) yield return null;
        ScreenCapture.CaptureScreenshot(Output + "eight-rings-full.png");
        yield return null;
        File.AppendAllText(Output + "stress.txt", "PASS real authored runtime 8 rings/32 mounts full; Pistol/LinkPair/AddMount ineligible; RingPower targets ring8 only; ModuleDamage targets one of repeated Pistol copies; complete rebuild preserves 32 modules. Screenshot after camera settle.\n");
        }
        yield return new ExitPlayMode();
    }

    static IEnumerator WaitForReload(int previousScene)
    {
        float deadline = Time.realtimeSinceStartup + 45;
        do { yield return null; }
        while ((SceneManager.GetActiveScene().handle == previousScene || SceneTransitionOverlay.IsTransitioning) && Time.realtimeSinceStartup < deadline);
        Assert.That(SceneManager.GetActiveScene().handle, Is.Not.EqualTo(previousScene));
        Assert.That(SceneTransitionOverlay.IsTransitioning, Is.False);
    }

    static void ExportIcon(OrbitalRewardData reward, Cards cards, string id = null)
    {
        var icon = OrbitalRewardIconResolver.Resolve(reward);
        if (reward.RewardKind == OrbitalRewardKind.NewRing)
        {
            Assert.That(icon.Sprite, Is.Null, "No authored ring-plus art: no random substitute");
            cards.cards.Add(new Card { id = "NewRing", title = reward.upgradeName, description = reward.description, source = "ART REQUIRED: ring +" });
            return;
        }
        Assert.That(icon.Sprite, Is.Not.Null, reward.RewardKind + " sprite");
        var sprite = icon.Sprite;
        var texture = sprite.texture;
        var rect = sprite.rect;
        var previous = RenderTexture.active;
        var rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
        var readable = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
        try
        {
            Graphics.Blit(texture, rt);
            RenderTexture.active = rt;
            readable.ReadPixels(rect, 0, 0);
            var pixels = readable.GetPixels();
            for (int i = 0; i < pixels.Length; i++) pixels[i] *= icon.Tint;
            readable.SetPixels(pixels); readable.Apply();
            File.WriteAllBytes(Output + "icon-" + (id ?? reward.RewardKind.ToString()) + ".png", readable.EncodeToPNG());
            cards.cards.Add(new Card { id = id ?? reward.RewardKind.ToString(), title = reward.upgradeName, description = reward.description, source = AssetDatabase.GetAssetPath(sprite) + "#" + sprite.name, weight = reward.Weight, width = readable.width, height = readable.height });
        }
        finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); Object.DestroyImmediate(readable); }
    }

    [UnityTest]
    public IEnumerator Pool_First15Choices_ProductionProviderMeasurements()
    {
        yield return new EnterPlayMode();
        Directory.CreateDirectory(Output);
        bool had = PlayerPrefs.HasKey(WeaponKey);
        int old = PlayerPrefs.GetInt(WeaponKey);
        var random = UnityEngine.Random.state;
        var run = RunStateManager.EnsureExists();
        var startingStage = AssetDatabase.FindAssets("t:StageProfileData").Select(id => AssetDatabase.LoadAssetAtPath<StageProfileData>(AssetDatabase.GUIDToAssetPath(id))).First(s => s.SectorNumber == 1);
        var startingRule = AssetDatabase.FindAssets("t:WorldRuleData").Select(id => AssetDatabase.LoadAssetAtPath<WorldRuleData>(AssetDatabase.GUIDToAssetPath(id))).First();
        var startingAnomaly = AssetDatabase.FindAssets("t:LocalAnomalyData").Select(id => AssetDatabase.LoadAssetAtPath<LocalAnomalyData>(AssetDatabase.GUIDToAssetPath(id))).First();
        var body = new[] {
            AssetDatabase.LoadAssetAtPath<UpgradeData>("Assets/_Project/Scriptable Objects/Upgrade/Gray/HP.asset"),
            AssetDatabase.LoadAssetAtPath<UpgradeData>("Assets/_Project/Scriptable Objects/Upgrade/Gray/Move Speed.asset") };
        File.WriteAllText(Output + "pool.txt", "Simulation: real provider + state commands; levels 2..16; first offered card selected. 200 seeded runs per Weapon station level. Not a human playthrough.\n");
        try
        {
            foreach (int weaponLevel in new[] { 1, 3 })
            {
                PlayerPrefs.SetInt(WeaponKey, weaponLevel);
                int hands = 0, noModule = 0, earlyNoModule = 0, coreHands = 0;
                using var provider = new OrbitalRewardProvider(body);
                for (int seed = 42; seed < 242; seed++)
                {
                    UnityEngine.Random.InitState(seed);
                    run.BeginNewRun(null, null, startingStage, startingRule, startingAnomaly);
                    var state = run.OrbitalStationState;
                    for (int pick = 1; pick <= 15; pick++)
                    {
                        bool offer = state.BeginLevelUpOpportunity(pick + 1, UnityEngine.Random.value);
                        var choices = provider.BuildChoices(3, offer).Cast<OrbitalRewardData>().ToArray();
                        if (choices.Length == 0) continue;
                        bool IsModule(OrbitalRewardKind k) => k <= OrbitalRewardKind.LinkPair;
                        hands++;
                        if (!choices.Any(c => IsModule(c.RewardKind))) { noModule++; if (pick <= 3) earlyNoModule++; }
                        if (choices.Any(c => c.RewardKind == OrbitalRewardKind.CoreUpgrade)) coreHands++;
                        var choice = choices[0];
                        if (seed == 42) File.AppendAllText(Output + "pool.txt", $"station={weaponLevel}; choice={pick}; [{string.Join(", ", choices.Select(c => c.RewardKind))}]; pick={choice.RewardKind}\n");
                        var kind = choice.RewardKind;
                        if (IsModule(kind))
                        {
                            var free = state.Rings.SelectMany(r => Enumerable.Range(0, r.MountCapacity).Where(m => state.IsMountFree(r.StableRingId, m)).Select(m => (r.StableRingId, m))).ToArray();
                            if (kind == OrbitalRewardKind.LinkPair) Assert.That(state.InstallLinkPair(free[0].StableRingId, free[0].m, free[1].StableRingId, free[1].m, out _, out _, out _), Is.True);
                            else Assert.That(state.InstallModule((OrbitalModuleKind)Enum.Parse(typeof(OrbitalModuleKind), kind.ToString()), free[0].StableRingId, free[0].m, out _), Is.True);
                        }
                        else if (kind == OrbitalRewardKind.RingSpeed) state.UpgradeRingSpeed(state.Rings.First(r => state.CanTargetRingReward(OrbitalRewardKind.RingSpeed, r.StableRingId)).StableRingId);
                        else if (kind == OrbitalRewardKind.RingPower) state.UpgradeRingPower(state.Rings.First(r => state.CanTargetRingReward(OrbitalRewardKind.RingPower, r.StableRingId)).StableRingId);
                        else if (kind == OrbitalRewardKind.AddMount) state.AddMount(state.Rings.First(r => state.CanTargetRingReward(OrbitalRewardKind.AddMount, r.StableRingId)).StableRingId, out _);
                        else if (kind == OrbitalRewardKind.ModuleDamage) state.UpgradeModuleDamage(state.Modules.First(m => state.CanUpgradeModuleDamage(m.StableModuleId, out _)).StableModuleId);
                        else if (kind == OrbitalRewardKind.NewRing) state.AddRing();
                        else if (kind == OrbitalRewardKind.CoreUpgrade) state.UpgradeCore();
                        else if (kind == OrbitalRewardKind.LinkMatrix) state.UpgradeLinkMatrix();
                        else run.ItemSlots.TryAdd(choice.BodyUpgrade);
                        Assert.That(state.Validate(out var error), Is.True, error);
                    }
                }
                File.AppendAllText(Output + "pool.txt", $"SUMMARY station={weaponLevel}: hands={hands}, without installable module={noModule}, first 3 hands without module={earlyNoModule}/600, Core offered={coreHands}\n");
            }
        }
        finally
        {
            if (had) PlayerPrefs.SetInt(WeaponKey, old); else PlayerPrefs.DeleteKey(WeaponKey);
            PlayerPrefs.Save(); UnityEngine.Random.state = random;
        }
        yield return new ExitPlayMode();
    }
}
#endif
