#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

// Explicit, bounded transfer checks; never changes production on import.
[InitializeOnLoad]
public static partial class EnemyProductionTransferPreview
{
    public const string Output = "Artifacts/GeneratedQA/EnemyProductionTransfer";
    public const string Enemies = "Assets/_Project/prefabs/Enemies/";
    static EnemyProductionTransferPreview()
    {
        EditorApplication.update += Poll;
        EditorApplication.playModeStateChanged += DemoState;
        ScriptableObject.CreateInstance<TestRunnerApi>().RegisterCallbacks(new Results());
    }
    static void Poll()
    {
        TickDemo();
        string request = Output + "/editor.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        string command = File.ReadAllText(request).Trim();
        if (EditorApplication.isPlayingOrWillChangePlaymode && command != "capture") return;
        File.Delete(request);
        try
        {
            if (command == "capture" && SessionState.GetBool("EnemyProductionTransfer.Demo", false))
            { demo = RefreshCapture(); nextDemo = 0; demoDeadline = EditorApplication.timeSinceStartup + 10; }
            if (command == "demo") StartDemo();
            if (command == "smoke")
            {
                SessionState.SetBool("EnemyProductionTransfer.Smoke", true);
                typeof(EnemyOrbitalInterceptionPreview).GetMethod("SetGameView", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).Invoke(null, null);
                ScriptableObject.CreateInstance<TestRunnerApi>().Execute(new ExecutionSettings(new Filter
                    { testMode = TestMode.EditMode, testNames = new[] { "EnemyProductionTransferSmoke.BunkerSpawnerMovementShieldsSpidersAndWeapons" } }));
            }
            if (command == "author") TacticalEnemyMvpAuthoring.Author();
            if (command == "check" || command == "author")
            { CheckAssets(); File.WriteAllText(Output + "/" + command + ".txt", "PASS: production assets, sector gates, weights and uncapped tactical entries."); }
        }
        catch (Exception error) { File.WriteAllText(Output + "/" + command + ".txt", "FAIL: " + error); Debug.LogException(error); }
    }
    sealed class Results : IErrorCallbacks
    {
        public void RunStarted(ITestAdaptor tests) { }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result) { }
        public void RunFinished(ITestResultAdaptor result)
        {
            if (!SessionState.GetBool("EnemyProductionTransfer.Smoke", false)) return;
            TestRunnerApi.SaveResultToFile(result, Output + "/smoke.xml");
            File.WriteAllText(Output + "/smoke.txt", $"{result.ResultState}: passed={result.PassCount}, failed={result.FailCount}");
            SessionState.EraseBool("EnemyProductionTransfer.Smoke");
        }
        public void OnError(string message) { if (SessionState.GetBool("EnemyProductionTransfer.Smoke", false)) File.WriteAllText(Output + "/smoke-error.txt", message); }
    }
    static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
    public static void CheckAssets()
    {
        foreach (string name in new[] { "Flanker", "Breacher" })
        {
            var actor = AssetDatabase.LoadAssetAtPath<GameObject>(Enemies + "p_Enemy_" + name + ".prefab");
            Require(actor.GetComponent<EnemyHealth>().MaxHealth == (name == "Flanker" ? 16 : 32), name + " approved HP");
            Require(actor.GetComponent<EnemyChaseMovement>().UsesProductionTacticalTuning, name + " release tactical tuning enabled");
            var timer = actor.GetComponent<EnemyTacticalTimedShield>();
            Require(timer.SpawnChance == (name == "Flanker" ? .20f : .25f) && timer.Shield != null && !timer.Shield.enabled && !timer.Shield.ShieldCollider.enabled && timer.Shield.MaximumBlockedHits == 2, name + " authored one-shot directional shield");
        }
        foreach (string kind in new[] { "White", "Dark", "BigGait", "Blue" })
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Dev/Labs/OrbitalLab/ArtistComparison/LabSpider" + kind + ".prefab");
            var actor = AssetDatabase.LoadAssetAtPath<GameObject>(Enemies + "p_Enemy_Spider" + kind + ".prefab");
            Require(actor.GetComponent<EnemyHealth>().MaxHealth == source.GetComponent<EnemyHealth>().MaxHealth && actor.GetComponent<EnemyCollisionHandler>().damage == source.GetComponent<EnemyCollisionHandler>().damage, kind + " original lab health/contact");
            Require(File.ReadAllText(AssetDatabase.GetAssetPath(actor)) == File.ReadAllText(AssetDatabase.GetAssetPath(source)), kind + " exact source prefab, including health/loot/events");
            Require(actor.GetComponentInChildren<Animator>()?.runtimeAnimatorController == source.GetComponentInChildren<Animator>()?.runtimeAnimatorController, kind + " source animation controller");
        }
        for (int sector = 1; sector <= RunRoute.TotalSectors; sector++)
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnemySpawnProfile>($"Assets/_Project/Data/Stages/EnemySpawnProfiles/ESP_Level_{sector:00}.asset");
            foreach (var phase in profile.Phases)
            {
                var flanker = phase.enemies.SingleOrDefault(e => e.enemyPrefab.name == "p_Enemy_Flanker");
                var breacher = phase.enemies.SingleOrDefault(e => e.enemyPrefab.name == "p_Enemy_Breacher");
                Require((flanker != null) == (sector >= 2) && (breacher != null) == (sector >= 3), "sector gates");
                foreach (var entry in new[] { flanker, breacher }.Where(e => e != null))
                    Require(entry.maxAliveOfType == 0 && !entry.useExactAliveLimit && entry.weight == (entry == flanker ? 6 : 4), "unchanged weight; no tactical cap");
                var family = phase.enemies.Where(e => e.enemyPrefab.name.StartsWith("p_Enemy_classic") || e.enemyPrefab.name.StartsWith("p_Enemy_Spider")).ToArray();
                Require(family.Length == 5 && family.All(e => Mathf.Approximately(e.weight, family[0].weight)), "five equally weighted ordinary chaser visuals");
                Require(family.Select(e => e.enemyPrefab.GetComponent<EnemyIdentity>().EnemyId).Distinct().Count() == 1, "shared existing class cap");
                Require(phase.enemies.All(e => e.enemyPrefab.name != "p_Boss2"), "boss excluded from ordinary spawn");
            }
        }
    }
}
#endif
