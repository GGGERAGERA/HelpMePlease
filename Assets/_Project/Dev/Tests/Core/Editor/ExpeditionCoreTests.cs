#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class ExpeditionCoreTests
{
    private sealed class Storage : ISurfaceMapStorage
    {
        public string json;
        public string Load(string id) => json;
        public void Save(string id, string value) => json = value;
    }
    private static void Set(Object obj, string name, object value) => obj.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj, value);

    [TestCase(true, RunEndReason.Victory, 5)]
    [TestCase(true, RunEndReason.PlayerDied, 2)]
    [TestCase(false, RunEndReason.PlayerDied, 0)]
    public void CompletionPaysImmediatelyAndResultsNeverPayAgain(bool complete, RunEndReason reason, int expected)
    {
        Exercise((task, config, wallet, map, storage) =>
        {
            int notifications = 0;
            wallet.OnCoresUpdated += value =>
            {
                Assert.That(PlayerPrefs.GetInt(CurrencyManager.CoresKey), Is.EqualTo(value));
                Assert.That(PlayerPrefs.HasKey(ExpeditionTaskRun.LastRewardsKey), Is.True);
                notifications++;
            };
            var run = new ExpeditionTaskRun(config, wallet, map);
            if (complete) { run.Observe(75, 0, 0); run.Observe(100, 0, 0); }
            Assert.That(wallet.TotalCores, Is.EqualTo(complete ? 2 : 0), "Task payout precedes the end of the expedition.");
            run.Finish(reason, reason == RunEndReason.Victory);
            int balance = wallet.TotalCores;
            run.Finish(reason, true);
            var snapshot = run.Snapshot;
            snapshot.TaskCores = 999;
            Assert.That(run.Snapshot.Total, Is.EqualTo(expected));
            Assert.That(wallet.TotalCores, Is.EqualTo(expected));
            Assert.That(wallet.TotalCores, Is.EqualTo(balance));
            Assert.That(wallet.TotalGold, Is.Zero);
            Assert.That(notifications, Is.EqualTo(reason == RunEndReason.Victory ? 3 : complete ? 1 : 0));
        });
    }

    [Test]
    public void NewRunRepeatsTaskButExistingCompletedProgressSuppressesFirstClear()
    {
        Exercise((task, config, wallet, map, storage) =>
        {
            var first = new ExpeditionTaskRun(config, wallet, map);
            first.Observe(75, 0, 0); first.Finish(RunEndReason.Victory, true);
            Assert.That(map.CompleteVictory(config, RunEndReason.Victory, true), Is.True);
            var reloaded = new SurfaceMapService(map.Definition, storage);
            var second = new ExpeditionTaskRun(config, wallet, reloaded);
            second.Observe(75, 0, 0); second.Finish(RunEndReason.Victory, true);
            Assert.That(second.Snapshot.FirstClearCores, Is.Zero);
            Assert.That(second.Snapshot.Total, Is.EqualTo(4));
            Assert.That(wallet.TotalCores, Is.EqualTo(9));
            Assert.That(reloaded.GetStatus(config.SectorId), Is.EqualTo(SurfaceSectorStatus.Completed));
            Assert.That(PlayerPrefs.GetInt(CurrencyManager.CoresKey), Is.EqualTo(9));
        });
    }

    [Test]
    public void OldCompletedSaveHasNoRetroactiveBonusAndReceiptRejectsReentry()
    {
        Exercise((task, config, wallet, map, storage) =>
        {
            map.CompleteVictory(config, RunEndReason.Victory, true);
            var run = new ExpeditionTaskRun(config, wallet, new SurfaceMapService(map.Definition, storage));
            run.Finish(RunEndReason.Victory, true);
            Assert.That(run.Snapshot.FirstClearCores, Is.Zero);
            Assert.That(wallet.TotalCores, Is.EqualTo(2));
            string receipt = run.Snapshot.RunToken + "/sector";
            Assert.That(wallet.TryGrantCoresOnce(receipt, 99, () => Assert.Fail("Duplicate receipt staged state")), Is.False);
        });
    }

    [Test]
    public void CompletionObserverCanFinishVictoryWithoutLosingRewardsOrLeavingStaleBalance()
    {
        Exercise((task, config, wallet, map, storage) =>
        {
            var run = new ExpeditionTaskRun(config, wallet, map);
            int displayed = 0;
            wallet.OnCoresUpdated += _ => run.Finish(RunEndReason.Victory, true);
            wallet.OnCoresUpdated += value => displayed = value;
            run.Observe(75, 0, 0);
            Assert.That(wallet.TotalCores, Is.EqualTo(5));
            Assert.That(run.Snapshot.Total, Is.EqualTo(5));
            Assert.That(displayed, Is.EqualTo(5));
        });
    }

    [Test]
    public void SelectionIsOptionalSectorScopedAndEveryConditionUsesRealCounters()
    {
        Exercise((task, config, wallet, map, storage) =>
        {
            Assert.That(map.TrySelectTask("missing"), Is.False);
            Assert.That(map.TrySelectTask(task.Id), Is.True);
            Assert.That(map.TryBuildRunConfig(out var selected), Is.True);
            Assert.That(selected.ExpeditionTask, Is.SameAs(task));
            Assert.That(map.TrySelectTask(null), Is.True);
            map.TryBuildRunConfig(out var without);
            Assert.That(without.ExpeditionTask, Is.Null);
            new ExpeditionTaskRun(without, wallet, map).Observe(999, 999, 3);
            Assert.That(wallet.TotalCores, Is.Zero);
            Set(task, "condition", ExpeditionTaskCondition.SurviveMinutes); Set(task, "target", 3);
            var survivor = new ExpeditionTaskRun(config, wallet, map);
            survivor.Observe(1000, 179, 3); Assert.That(survivor.IsCompleted, Is.False);
            survivor.Observe(0, 180, 0); Assert.That(survivor.IsCompleted, Is.True);
            Set(task, "condition", ExpeditionTaskCondition.CompletedLocations); Set(task, "target", 2);
            var traveller = new ExpeditionTaskRun(config, wallet, map);
            traveller.Observe(1000, 999, 1); Assert.That(traveller.IsCompleted, Is.False);
            traveller.Observe(0, 0, 2); Assert.That(traveller.IsCompleted, Is.True);
        });
    }

    private static void Exercise(Action<ExpeditionTaskDefinition, RunConfig, CurrencyManager, SurfaceMapService, Storage> check)
    {
        var previous = CurrencyManager.Instance;
        bool hadCores = PlayerPrefs.HasKey(CurrencyManager.CoresKey), hadLedger = PlayerPrefs.HasKey(ExpeditionTaskRun.LastRewardsKey);
        int oldCores = PlayerPrefs.GetInt(CurrencyManager.CoresKey);
        string oldLedger = PlayerPrefs.GetString(ExpeditionTaskRun.LastRewardsKey);
        var root = new GameObject("Isolated expedition wallet"); root.SetActive(false);
        var wallet = root.AddComponent<CurrencyManager>();
        var task = ScriptableObject.CreateInstance<ExpeditionTaskDefinition>();
        Set(task, "id", "qa_hunter"); Set(task, "title", "task.hunter.title"); Set(task, "target", 75);
        Set(task, "condition", ExpeditionTaskCondition.Kills); Set(task, "rewardCores", 2);
        var sector = ScriptableObject.CreateInstance<SurfaceSectorDefinition>();
        Set(sector, "id", "QA"); Set(sector, "victoryCores", 2); Set(sector, "firstClearCores", 1);
        Set(sector, "expeditionTasks", new[] { task });
        var definition = ScriptableObject.CreateInstance<SurfaceMapDefinition>();
        string mapId = "qa_cores_" + Guid.NewGuid().ToString("N");
        Set(definition, "id", mapId); Set(definition, "sectors", new[] { sector }); Set(definition, "startingSectors", new[] { "QA" });
        var storage = new Storage(); var map = new SurfaceMapService(definition, storage); map.TrySelect("QA");
        var receipts = new System.Collections.Generic.List<string>();
        wallet.OnCoresUpdated += _ =>
        {
            var saved = JsonUtility.FromJson<ExpeditionCoreRewards>(PlayerPrefs.GetString(ExpeditionTaskRun.LastRewardsKey));
            receipts.Add(saved.RunToken + "/task"); receipts.Add(saved.RunToken + "/sector");
        };
        PlayerPrefs.DeleteKey(CurrencyManager.CoresKey); PlayerPrefs.DeleteKey(ExpeditionTaskRun.LastRewardsKey);
        try { check(task, sector.BuildRunConfig(mapId, task), wallet, map, storage); }
        finally
        {
            foreach (string receipt in receipts) PlayerPrefs.DeleteKey(CurrencyManager.CoreRewardReceiptPrefix + receipt);
            PlayerPrefs.DeleteKey(CurrencyManager.CoreRewardReceiptPrefix + "first/" + mapId + "/QA");
            if (hadCores) PlayerPrefs.SetInt(CurrencyManager.CoresKey, oldCores); else PlayerPrefs.DeleteKey(CurrencyManager.CoresKey);
            if (hadLedger) PlayerPrefs.SetString(ExpeditionTaskRun.LastRewardsKey, oldLedger); else PlayerPrefs.DeleteKey(ExpeditionTaskRun.LastRewardsKey);
            PlayerPrefs.Save();
            Object.DestroyImmediate(root); Object.DestroyImmediate(task); Object.DestroyImmediate(sector); Object.DestroyImmediate(definition);
            CurrencyManager.Instance = previous;
        }
    }
    [Test]
    public void CoreBalanceHasIndependentPersistentReceiptAndUpdateEvent()
    {
        var method = typeof(CurrencyManager).GetMethod("TryGrantCoresOnce");
        Assert.That(method, Is.Not.Null, "Cores require their own saved transaction, independent of gold.");
        Assert.That(typeof(CurrencyManager).GetProperty("TotalCores"), Is.Not.Null);
        Assert.That(typeof(CurrencyManager).GetField("OnCoresUpdated"), Is.Not.Null);
    }

    [Test]
    public void AllProductionSectorsHaveThreeReachableTasksAndAuthoredCoreRewards()
    {
        foreach (string id in new[] { "A1", "A2", "C1", "C2", "D1", "D2" })
        {
            var sector = AssetDatabase.LoadAssetAtPath<SurfaceSectorDefinition>(
                "Assets/_Project/Data/SurfaceMap/Sector_" + id + ".asset");
            var data = new SerializedObject(sector);
            var reward = data.FindProperty("victoryCores");
            Assert.That(reward, Is.Not.Null, "Sector " + id + " must configure core payouts.");
            Assert.That(reward.intValue, Is.InRange(1, 4), id);
            Assert.That(reward.intValue, Is.EqualTo(id == "A1" ? 1 : id == "C1" || id == "D1" ? 2 : id == "A2" ? 3 : 4), id);
            Assert.That(data.FindProperty("firstClearCores").intValue, Is.InRange(1, 2), id);
            var tasks = data.FindProperty("expeditionTasks");
            Assert.That(tasks.arraySize, Is.EqualTo(3), id);
            for (int i = 0; i < tasks.arraySize; i++)
            {
                Assert.That(tasks.GetArrayElementAtIndex(i).objectReferenceValue, Is.Not.Null, id);
                var task = (ExpeditionTaskDefinition)tasks.GetArrayElementAtIndex(i).objectReferenceValue;
                Assert.That(task.IsAvailable(sector.BuildRunConfig("surface")), Is.True, id + "/" + task.Id);
            }
        }
    }

    [Test]
    public void ProductionUiKeepsSceneBindingsAndRenderedCoreIcons()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/MainBuild/MVP.unity");
        try
        {
            var components = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true)).Where(c => c != null).ToArray();
            var pause = components.OfType<PauseMenuUI>().Single();
            var data = new SerializedObject(pause);
            foreach (string field in new[] { "pausePanel", "overview", "audioSettingsPanel", "characterSpawner" })
                Assert.That(data.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            var death = new SerializedObject(components.OfType<DeathResultPresentation>().Single());
            Assert.That(death.FindProperty("coreRewards").objectReferenceValue, Is.Not.Null);
            var summary = (ExpeditionCoreSummaryView)death.FindProperty("coreRewards").objectReferenceValue;
            var comment = (TMP_Text)death.FindProperty("comment").objectReferenceValue;
            var window = (RectTransform)death.FindProperty("window").objectReferenceValue;
            // PanelAppearAnimation authors the hidden window at zero scale;
            // Show/FitToCanvas restores it before the result becomes visible.
            for (Transform parent = window; parent != null; parent = parent.parent)
                if (parent.localScale.sqrMagnitude < .0001f) parent.localScale = Vector3.one;
            // The authored result window is inactive in a preview scene. The
            // convenience bounds helper ignores inactive RectTransforms entirely.
            Bounds Measure(RectTransform rect)
            {
                var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                var bounds = new Bounds(window.InverseTransformPoint(corners[0]),Vector3.zero);
                foreach (var corner in corners) bounds.Encapsulate(window.InverseTransformPoint(corner));
                return bounds;
            }
            var summaryBounds = Measure((RectTransform)summary.transform);
            var commentBounds = Measure(comment.rectTransform);
            Assert.That(summaryBounds.Intersects(commentBounds), Is.False, $"Cores must not overlap the existing result comment. Cores={summaryBounds}; Comment={commentBounds}");
            Assert.That(components.OfType<ExpeditionTaskHud>().Single(), Is.Not.Null);
            var icons = components.OfType<CoreCurrencyIcon>().ToArray();
            Assert.That(icons, Is.Not.Empty);
            foreach (var icon in icons) Assert.That(icon.GetComponent<CanvasRenderer>(), Is.Not.Null);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
#endif
