#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

[Category("Core")]
public sealed class BunkerLootAudioTests
{
    private const string BackupKey = "Subject42.BunkerLootAudio.Backup";
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    [Serializable] private sealed class Saved { public string key; public bool exists; public bool text; public int number; public string value; }
    [Serializable] private sealed class Backup { public List<Saved> items = new(); }

    [SetUp]
    public void Preserve()
    {
        CoreTestSupport.PreservePreferences();
        string existing = SessionState.GetString(BackupKey, "");
        var backup = string.IsNullOrEmpty(existing) ? new Backup() : JsonUtility.FromJson<Backup>(existing);
        void PreserveInteger(string key)
        {
            if (backup.items.All(item => item.key != key))
                backup.items.Add(new Saved { key = key, exists = PlayerPrefs.HasKey(key), number = PlayerPrefs.GetInt(key) });
        }
        foreach (BunkerStationId id in Enum.GetValues(typeof(BunkerStationId)))
            foreach (string prefix in new[] { "BunkerStationLevel_", "BunkerStationInvested_" })
            {
                string key = prefix + id;
                PreserveInteger(key);
            }
        var itemKeys = new List<string> { "META_HP_LEVEL", "META_UPGRADE_INVESTED_Hp", "META_ORBITAL_MODULE_LEVEL_Pistol", "META_ORBITAL_MODULE_INVESTED_Pistol" };
        foreach (var anomaly in AssetDatabase.LoadAssetAtPath<BunkerSelectionCatalog>("Assets/_Project/Data/SurfaceMap/BunkerSelectionCatalog.asset").anomalies)
        {
            itemKeys.Add("META_ANOMALY_LEVEL_" + anomaly.Id);
            itemKeys.Add("META_ANOMALY_INVESTED_" + anomaly.Id);
        }
        foreach (string key in itemKeys)
            PreserveInteger(key);
        var map = AssetDatabase.LoadAssetAtPath<SurfaceMapDefinition>("Assets/_Project/Data/SurfaceMap/SurfaceMap_MVP.asset");
        foreach (string key in new[] { PlayerPrefsSurfaceMapStorage.KeyPrefix + map.Id, PlayerPrefsSurfaceMapStorage.KeyPrefix + map.Id + ":content" })
            if (backup.items.All(item => item.key != key))
                backup.items.Add(new Saved { key = key, exists = PlayerPrefs.HasKey(key), text = true, value = PlayerPrefs.GetString(key) });
        SessionState.SetString(BackupKey, JsonUtility.ToJson(backup));
        PlayerPrefs.DeleteKey(MetaProgressionManager.EscapeProtocolKey);
        PlayerPrefs.SetInt(MetaProgressionManager.EscapeAccessKey, 0);
        PlayerPrefs.SetInt(CurrencyManager.CoresKey, 0);
        PlayerPrefs.Save();
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        var cleanup = CoreTestSupport.CleanupPlayMode();
        while (cleanup.MoveNext()) yield return cleanup.Current;
        string json = SessionState.GetString(BackupKey, "");
        if (string.IsNullOrEmpty(json)) yield break;
        foreach (var item in JsonUtility.FromJson<Backup>(json).items)
            if (!item.exists) PlayerPrefs.DeleteKey(item.key);
            else if (item.text) PlayerPrefs.SetString(item.key, item.value);
            else PlayerPrefs.SetInt(item.key, item.number);
        PlayerPrefs.Save();
        SessionState.EraseString(BackupKey);
    }

    [UnityTest]
    public IEnumerator StationInvestmentSoundsOnlyAfterAcceptedPayment()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return CoreTestSupport.LoadBunker();
        yield return CheckStation();
    }

    [UnityTest]
    public IEnumerator ModuleMetaAndAnomalyPartialPaymentsHavePurchaseFeedback()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return CoreTestSupport.LoadBunker();
        yield return CheckItemPayments();
    }

    [UnityTest]
    public IEnumerator EscapeCorePaymentUsesDistinctStageCompletionFeedback()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return CoreTestSupport.LoadBunker();
        yield return CheckCores();
    }

    [UnityTest]
    public IEnumerator ProductionChestReelPlaysStartTicksStopAndOneRewardThenCancelsCleanly()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return StartRun();
        yield return CheckReel();
    }

    private static IEnumerator CheckStation()
    {
        var service = BunkerStationProgressionService.Instance;
        service.DebugSetStationLevel(BunkerStationId.Character, 1);
        var wallet = CurrencyManager.Instance;
        wallet.AddGoldExact(10000);
        var hub = Object.FindFirstObjectByType<BunkerSelectionSourceHub>();
        Assert.That(hub, Is.Not.Null);
        var cues = new List<AudioCueId>();
        var audio = AudioService.Instance;
        audio.DebugCuePlayed += cues.Add;
        try
        {
            var model = hub.Characters.BuildModel();
            int before = wallet.TotalGold;
            model.Station.Invest(0);
            Assert.That(wallet.TotalGold, Is.EqualTo(before));
            Assert.That(cues.Count(c => c == AudioCueId.Purchase), Is.Zero);
            model.Station.Invest(1);
            Assert.That(wallet.TotalGold, Is.EqualTo(before - 1));
            Assert.That(service.GetInvestedGold(BunkerStationId.Character), Is.EqualTo(1));
            AssertHeard(audio, cues, AudioCueId.Purchase, 1);
            yield return WaitForCue(audio, AudioCueId.Purchase);
            model.Station.Invest(int.MaxValue);
            Assert.That(service.GetLevel(BunkerStationId.Character), Is.EqualTo(2));
            AssertHeard(audio, cues, AudioCueId.Purchase, 2);
            wallet.AddGoldExact(-wallet.TotalGold);
            model.Station.Invest(1);
            Assert.That(cues.Count(c => c == AudioCueId.Purchase), Is.EqualTo(2));
            Assert.That(cues.Count(c => c == AudioCueId.PurchaseFail), Is.Zero, "Disabled station investment must not invent a failure interaction.");
        }
        finally { audio.DebugCuePlayed -= cues.Add; }
    }

    private static IEnumerator CheckCores()
    {
        var meta = MetaProgressionManager.Instance;
        Assert.That(meta.CurrentEscapeStage, Is.Zero);
        var terminal = Object.FindObjectsByType<BunkerStation>(FindObjectsSortMode.None)
            .Single(s => new SerializedObject(s).FindProperty("stationType").intValue == (int)BunkerStationType.EscapeProtocol);
        terminal.Interact();
        yield return null;
        var view = Object.FindFirstObjectByType<EscapeProtocolView>();
        Assert.That(Read<GameObject>(view, "projectWindow").activeInHierarchy, Is.True);
        var button = Read<Button>(view, "investButton");
        var cues = new List<AudioCueId>();
        var audio = AudioService.Instance;
        audio.DebugCuePlayed += cues.Add;
        try
        {
            view.InvestCores();
            Assert.That(cues, Is.Empty, "Rejected payment is silent.");
            SeedCores(2);
            button.onClick.Invoke();
            Assert.That(meta.GetEscapeInvestment(0), Is.EqualTo(2));
            Assert.That(CurrencyManager.Instance.TotalCores, Is.Zero);
            AssertHeard(audio, cues, AudioCueId.CoreInvest, 1);
            Assert.That(cues.Count(c => c == AudioCueId.CoreInvestComplete), Is.Zero);
            yield return WaitForCue(audio, AudioCueId.CoreInvest);
            SeedCores(meta.EscapeProtocol.Stages[0].cost - 2);
            button.onClick.Invoke();
            Assert.That(meta.IsEscapeStageCompleted(0), Is.True);
            AssertHeard(audio, cues, AudioCueId.CoreInvestComplete, 1);
            Assert.That(cues.Count(c => c == AudioCueId.CoreInvest), Is.EqualTo(1), "Stage completion replaces the partial-payment sound.");
            view.InvestCores();
            Assert.That(cues.Count(c => c == AudioCueId.CoreInvestComplete), Is.EqualTo(1));
            Assert.That(cues.Count(c => c == AudioCueId.CoreInvest), Is.EqualTo(1));
        }
        finally { audio.DebugCuePlayed -= cues.Add; }
    }

    private static IEnumerator CheckItemPayments()
    {
        foreach (var id in new[] { BunkerStationId.Weapon, BunkerStationId.Upgrades, BunkerStationId.Anomaly })
            BunkerStationProgressionService.Instance.DebugSetStationLevel(id, 3);
        PlayerPrefs.SetInt("META_HP_LEVEL", 0);
        PlayerPrefs.SetInt("META_UPGRADE_INVESTED_Hp", 0);
        PlayerPrefs.SetInt("META_ORBITAL_MODULE_LEVEL_Pistol", 1);
        PlayerPrefs.SetInt("META_ORBITAL_MODULE_INVESTED_Pistol", 0);
        var anomaly = AssetDatabase.LoadAssetAtPath<BunkerSelectionCatalog>("Assets/_Project/Data/SurfaceMap/BunkerSelectionCatalog.asset").anomalies.First();
        PlayerPrefs.SetInt("META_ANOMALY_LEVEL_" + anomaly.Id, 1);
        PlayerPrefs.SetInt("META_ANOMALY_INVESTED_" + anomaly.Id, 0);
        var hub = Object.FindFirstObjectByType<BunkerSelectionSourceHub>();
        var wallet = CurrencyManager.Instance;
        wallet.AddGoldExact(10000);
        var audio = AudioService.Instance;
        var cues = new List<AudioCueId>();
        var counts = new List<(string name, int partial, int completed, bool playing)>();
        audio.DebugCuePlayed += cues.Add;
        try
        {
            var models = new[]
            {
                hub.Weapons.BuildModel().Entries.Single(e => e.Progression?.TargetId == "orbital-module:Pistol").Progression,
                hub.Upgrades.BuildModel().Entries.Single(e => e.Progression?.TargetId == "upgrade:Hp").Progression,
                hub.Anomalies.BuildModel().Entries.Single(e => e.Id == anomaly.Id).Progression
            };
            foreach (var model in models)
            {
                Assert.That(model.Cost, Is.GreaterThan(1), model.TargetId);
                int before = wallet.TotalGold;
                int initial = cues.Count(c => c == AudioCueId.Purchase);
                model.Invest(1);
                Assert.That(wallet.TotalGold, Is.EqualTo(before - 1), model.TargetId + " actual partial payment");
                int partial = cues.Count(c => c == AudioCueId.Purchase) - initial;
                var clips = (AudioClip[])typeof(AudioCueDefinition).GetField("clips", Hidden).GetValue(
                    GetCue(audio, AudioCueId.Purchase));
                bool playing = audio.GetComponentsInChildren<AudioSource>().Any(s => s.isPlaying && clips.Contains(s.clip) && s.volume > 0f);
                yield return WaitForCue(audio, AudioCueId.Purchase);
                model.Invest(int.MaxValue);
                Assert.That(wallet.TotalGold, Is.EqualTo(before - model.Cost), model.TargetId + " completed purchase");
                int completed = cues.Count(c => c == AudioCueId.Purchase) - initial - partial;
                counts.Add((model.TargetId, partial, completed, playing));
                model.Invest(0);
                Assert.That(cues.Count(c => c == AudioCueId.Purchase) - initial, Is.EqualTo(partial + completed));
                yield return WaitForCue(audio, AudioCueId.Purchase);
            }
            var failures = new List<string>();
            foreach (var result in counts)
            {
                if (result.partial != 1) failures.Add(result.name + " partial payment cue count=" + result.partial + ", expected 1");
                if (result.completed != 1) failures.Add(result.name + " completion cue count=" + result.completed + ", expected 1");
                if (!result.playing) failures.Add(result.name + " partial payment has no playing pooled source");
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }
        finally { audio.DebugCuePlayed -= cues.Add; }
    }

    private static AudioCueDefinition GetCue(AudioService audio, AudioCueId id)
    {
        Assert.That(Read<AudioCatalog>(audio, "catalog").TryGet(id, out var definition), Is.True, id.ToString());
        return definition;
    }

    private static IEnumerator StartRun()
    {
        yield return CoreTestSupport.LoadBunker();
        RunSelectionManager.Instance.SelectCharacter(AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/01_Gera.asset"));
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        starter.StartRun(starter.transform);
        yield return CoreTestSupport.Await(() => SceneManager.GetActiveScene().name == "MVP" &&
            Object.FindFirstObjectByType<OrbitalStationRuntime>() is { IsInitialized: true } && !SceneTransitionOverlay.IsTransitioning);
        PlayerRuntimeReference.CachedPlayer.GetComponent<PlayerHealth>().AddMaxHealth(1000000f);
        Object.FindFirstObjectByType<EnemySpawner>().StopSpawning();
        foreach (var enemy in EnemyHealth.ActiveInstances.ToArray()) Object.Destroy(enemy.gameObject);
        yield return null;
    }

    private static IEnumerator CheckReel()
    {
        var audio = AudioService.Instance;
        var cues = new List<AudioCueId>();
        audio.DebugCuePlayed += cues.Add;
        try
        {
            var sector = Object.FindFirstObjectByType<ProductionExplorationSectorController>();
            var chest = WorldLootChestSpawner.SpawnChest(sector.Config.WorldLootChestPrefab, PlayerRuntimeReference.CachedPlayer.transform.position);
            chest.Interact();
            chest.NotifyOpeningAnimationComplete();
            var reel = Object.FindFirstObjectByType<WorldLootRewardReel>();
            var stop = reel.GetComponentsInChildren<Button>(true).Single();
            yield return CoreTestSupport.Await(() => stop.isActiveAndEnabled && stop.interactable);
            AssertHeard(audio, cues, AudioCueId.LootReelStart, 1, false);
            yield return CoreTestSupport.Await(() => cues.Contains(AudioCueId.LootReelTick));
            Assert.That(cues.IndexOf(AudioCueId.LootReelTick), Is.GreaterThan(cues.IndexOf(AudioCueId.LootReelStart)));
            stop.onClick.Invoke();
            AssertHeard(audio, cues, AudioCueId.LootReelStop, 1);
            stop.onClick.Invoke();
            Assert.That(cues.Count(c => c == AudioCueId.LootReelStop), Is.EqualTo(1));
            yield return CoreTestSupport.Await(() => WorldLootRewardReel.LastStoppedUpgrade != null);
            Assert.That(WorldLootRewardReel.LastClaimedUpgrade, Is.SameAs(WorldLootRewardReel.LastStoppedUpgrade));
            AssertHeard(audio, cues, AudioCueId.RewardSelect, 1);
            yield return RewardScenarioAssertions.FinishReward();
            int previousTicks = cues.Count(c => c == AudioCueId.LootReelTick);
            yield return WaitRealtime(.4f);
            Assert.That(cues.Count(c => c == AudioCueId.LootReelTick), Is.EqualTo(previousTicks));

            // Exercise cancellation after a real spin starts, before a result is committed.
            var pool = UpgradeManager.Instance.GetEligibleNormalRewards();
            int applied = 0, claimed = 0;
            Assert.That(WorldLootRewardReel.TryShow(pool, Vector3.zero, _ => { applied++; return false; }, _ => claimed++), Is.True);
            yield return CoreTestSupport.Await(() => cues.Count(c => c == AudioCueId.LootReelStart) == 2);
            WorldLootRewardReel.CancelForSceneExit();
            int afterCancel = cues.Count;
            yield return WaitRealtime(.4f);
            Assert.That(WorldLootRewardReel.IsActive, Is.False);
            Assert.That(applied, Is.Zero); Assert.That(claimed, Is.Zero);
            Assert.That(cues.Count(c => c == AudioCueId.RewardSelect), Is.EqualTo(1), "Cancellation never sounds like a claimed reward.");
            Assert.That(cues.Skip(afterCancel).Any(c => c == AudioCueId.LootReelStart || c == AudioCueId.LootReelTick || c == AudioCueId.LootReelStop), Is.False);
        }
        finally { audio.DebugCuePlayed -= cues.Add; WorldLootRewardReel.CancelForSceneExit(); }
    }

    private static T Read<T>(Object target, string field) => (T)target.GetType().GetField(field, Hidden).GetValue(target);

    private static void AssertHeard(AudioService audio, List<AudioCueId> cues, AudioCueId id, int count, bool stillPlaying = true)
    {
        Assert.That(cues.Count(c => c == id), Is.EqualTo(count), id + " must reach the central playback path exactly once per accepted action.");
        Assert.That(Read<AudioCatalog>(audio, "catalog").TryGet(id, out var definition), Is.True, id.ToString());
        Assert.That(definition.TryGetRandomClip(out _), Is.True, id.ToString());
        var clips = (AudioClip[])typeof(AudioCueDefinition).GetField("clips", Hidden).GetValue(definition);
        if (stillPlaying)
            Assert.That(audio.GetComponentsInChildren<AudioSource>().Any(s => s.isPlaying && clips.Contains(s.clip) && s.volume > 0f), Is.True,
                id + " must reach a playing, audible-volume pooled AudioSource.");
    }

    private static IEnumerator WaitForCue(AudioService audio, AudioCueId id)
    {
        Assert.That(Read<AudioCatalog>(audio, "catalog").TryGet(id, out var definition), Is.True);
        Assert.That(definition.TryGetRandomClip(out var clip), Is.True);
        yield return WaitRealtime(clip.length / Mathf.Max(.1f, definition.PitchMin) + definition.Cooldown + .05f);
    }

    private static IEnumerator WaitRealtime(float duration)
    {
        float until = Time.realtimeSinceStartup + duration;
        while (Time.realtimeSinceStartup < until) yield return null;
    }

    private static void SeedCores(int amount)
    {
        string receipt = "qa_bunker_audio_" + Guid.NewGuid().ToString("N");
        try { Assert.That(CurrencyManager.Instance.TryGrantCoresOnce(receipt, amount, () => { }), Is.True); }
        finally { PlayerPrefs.DeleteKey(CurrencyManager.CoreRewardReceiptPrefix + receipt); PlayerPrefs.Save(); }
    }
}
#endif
