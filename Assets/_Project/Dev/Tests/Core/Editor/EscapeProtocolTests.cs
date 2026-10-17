#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Reflection;
using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class EscapeProtocolTests
{
    public const string Output = "Artifacts/GeneratedQA/EscapeProtocol";
    [Test] public void ExistingSixRoomsAndDedicatedProtocolTerminalAreAvailable()
    {
        var scene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/MainBuild/MainMenu.unity");
        try
        {
            var all = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).Where(c => c != null).ToArray();
            var roomNames = new[] { "HUB", "FOOTBALL", "ENEMY_GALLERY", "ARCADE", "CHILL_ROOM", "TROPHIES" };
            var rooms = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => roomNames.Contains(t.name)).ToArray();
            Assert.That(rooms.Select(r => r.name).Distinct().Count(), Is.EqualTo(6));
            var terminals = all.OfType<BunkerStation>().Where(s => new SerializedObject(s).FindProperty("stationType").intValue == (int)BunkerStationType.EscapeProtocol).ToArray();
            Assert.That(terminals.Length, Is.EqualTo(1));
            Assert.That(terminals[0].name, Is.EqualTo("PF_EscapeProtocolStation"));
            Assert.That(terminals[0].GetComponentInParent<BunkerMinigame>(), Is.Null);
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output + "/room-inventory.txt", string.Join("\n", rooms.Select(r => $"{r.name}, position={r.transform.position}, active={r.gameObject.activeSelf}")) +
                $"\nProtocol terminal: {terminals[0].name}, position={terminals[0].transform.position}, existing EscapeProtocol station type.\n");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
    [Test] public void ExistingProgressionAndCurrencyExposeConfirmedCoreInvestment()
    {
        Assert.That(typeof(MetaProgressionManager).GetMethod("TryInvestEscapeCores"), Is.Not.Null);
        Assert.That(typeof(CurrencyManager).GetMethod("TrySpendCoresExact"), Is.Not.Null);
        Assert.That(typeof(MetaProgressionManager).GetProperty("EscapeProtocolCompleted"), Is.Not.Null);
    }

    private static void Set(Object obj, string field, object value) => obj.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(obj,value);
    private static void Exercise(Action<MetaProgressionManager,CurrencyManager,EscapeProtocolDefinition,Action<int>> test, int legacy = 0)
    {
        var integerKeys = new[] { CurrencyManager.CoresKey, MetaProgressionManager.EscapeAccessKey };
        var numbers = integerKeys.ToDictionary(k=>k,k=>(exists:PlayerPrefs.HasKey(k),value:PlayerPrefs.GetInt(k)));
        bool hadProject = PlayerPrefs.HasKey(MetaProgressionManager.EscapeProtocolKey); string oldProject = PlayerPrefs.GetString(MetaProgressionManager.EscapeProtocolKey);
        var previous = CurrencyManager.Instance;
        var root = new GameObject("Isolated Escape Protocol services"); root.SetActive(false);
        var wallet = root.AddComponent<CurrencyManager>(); var meta = root.AddComponent<MetaProgressionManager>();
        var definition = AssetDatabase.LoadAssetAtPath<EscapeProtocolDefinition>("Assets/_Project/Data/Meta/EscapeProtocol.asset");
        var map = Object.Instantiate(AssetDatabase.LoadAssetAtPath<SurfaceMapDefinition>("Assets/_Project/Data/SurfaceMap/SurfaceMap_MVP.asset"));
        string mapId = "qa_escape_"+Guid.NewGuid().ToString("N"); Set(map,"id",mapId);
        var receipts = new List<string>();
        void Seed(int amount)
        {
            string id="qa_escape_"+Guid.NewGuid().ToString("N");receipts.Add(id);
            Assert.That(wallet.TryGrantCoresOnce(id,amount,()=>{}),Is.True);
        }
        try
        {
            Assert.That(definition,Is.Not.Null,"Production progression data is required.");Assert.That(definition.IsValid,Is.True);
            CurrencyManager.Instance=wallet;PlayerPrefs.DeleteKey(CurrencyManager.CoresKey);PlayerPrefs.DeleteKey(MetaProgressionManager.EscapeProtocolKey);
            PlayerPrefs.SetInt(MetaProgressionManager.EscapeAccessKey,legacy);
            Set(meta,"escapeProtocol",definition);meta.ReloadFromStorage();meta.ConfigureSurfaceMap(map);
            test(meta,wallet,definition,Seed);
        }
        finally
        {
            foreach(var pair in numbers)if(pair.Value.exists)PlayerPrefs.SetInt(pair.Key,pair.Value.value);else PlayerPrefs.DeleteKey(pair.Key);
            if(hadProject)PlayerPrefs.SetString(MetaProgressionManager.EscapeProtocolKey,oldProject);else PlayerPrefs.DeleteKey(MetaProgressionManager.EscapeProtocolKey);
            foreach(string receipt in receipts)PlayerPrefs.DeleteKey(CurrencyManager.CoreRewardReceiptPrefix+receipt);
            PlayerPrefs.DeleteKey(PlayerPrefsSurfaceMapStorage.KeyPrefix+mapId);PlayerPrefs.DeleteKey(PlayerPrefsSurfaceMapStorage.KeyPrefix+mapId+":content");PlayerPrefs.Save();
            Object.DestroyImmediate(root);Object.DestroyImmediate(map);CurrencyManager.Instance=previous;
        }
    }
    private static void Fund(MetaProgressionManager meta,EscapeProtocolDefinition definition)
    {
        while(!meta.EscapeProtocolFunded)
            Assert.That(meta.TryInvestEscapeCores(definition.Stages[meta.CurrentEscapeStage].id,int.MaxValue,out _),Is.True);
    }
    [Test] public void PartialInvestmentPersistsAndCompletedStagesCannotBeChargedAgain()
    {
        Exercise((meta,wallet,data,seed)=>
        {
            Assert.That(data.Stages.Select(s=>s.cost),Is.EqualTo(new[]{5,10,15}));
            Assert.That(data.Stages.All(s=>s.unlockIds.Length==0),Is.True,"Intermediate stages have no fictional reward.");
            seed(4);string first=data.Stages[0].id;
            Assert.That(meta.TryInvestEscapeCores("stale",5,out _),Is.False);Assert.That(wallet.TotalCores,Is.EqualTo(4));
            Assert.That(meta.TryInvestEscapeCores(first,5,out int paid),Is.True);Assert.That(paid,Is.EqualTo(4));Assert.That(wallet.TotalCores,Is.Zero);
            meta.ReloadFromStorage();Assert.That(meta.GetEscapeInvestment(0),Is.EqualTo(4));Assert.That(meta.CurrentEscapeStage,Is.Zero);
            Assert.That(meta.TryInvestEscapeCores(first,1,out _),Is.False);
            seed(30);Assert.That(meta.TryInvestEscapeCores(first,99,out paid),Is.True);Assert.That(paid,Is.EqualTo(1));
            Assert.That(meta.TryInvestEscapeCores(first,99,out _),Is.False);Assert.That(wallet.TotalCores,Is.EqualTo(29));
            Fund(meta,data);Assert.That(wallet.TotalCores,Is.EqualTo(4));Assert.That(wallet.TotalGold,Is.Zero);
            Assert.That(meta.EscapeProtocolFunded,Is.True);Assert.That(meta.EscapeProtocolCompleted,Is.False);
            Assert.That(meta.TryInvestEscapeCores(data.Stages[2].id,15,out _),Is.False);
            meta.ReloadFromStorage();Assert.That(meta.EscapeProtocolFunded,Is.True);Assert.That(PlayerPrefs.GetInt(CurrencyManager.CoresKey),Is.EqualTo(4));
        });
    }
    [Test] public void LegacyGuardianAccessIsPreservedAndCreditedOnlyAtMigration()
    {
        Exercise((meta,wallet,data,seed)=>
        {
            Assert.That(meta.EscapeAccess,Is.EqualTo(1));Assert.That(meta.CurrentEscapeStage,Is.EqualTo(1));
            Assert.That(meta.GetEscapeInvestment(0),Is.EqualTo(5));Assert.That(wallet.TotalCores,Is.Zero);
            Assert.That(meta.EscapeProtocolCompleted,Is.False);meta.ReloadFromStorage();Assert.That(meta.CurrentEscapeStage,Is.EqualTo(1));
        },1);
        Exercise((meta,wallet,data,seed)=>
        {
            Assert.That(meta.AcquireGuardianAccess(),Is.True);meta.ReloadFromStorage();
            Assert.That(meta.EscapeAccess,Is.EqualTo(1));Assert.That(meta.CurrentEscapeStage,Is.Zero,"A new story achievement never overwrites this project's paid stages.");
        });
    }
    [Test] public void FundingOpensOnlyTheFinalNodeAndVictoryCompletionIsSavedOnce()
    {
        Exercise((meta,wallet,data,seed)=>
        {
            var map=meta.SurfaceMap;string final=data.FinalSector.Id;
            var ordinary=map.Definition.Sectors.Where(s=>!s.IsEscapeFinal).ToDictionary(s=>s.Id,s=>map.GetStatus(s.Id));
            Assert.That(ordinary.Count,Is.EqualTo(6));Assert.That(map.GetStatus(final),Is.EqualTo(SurfaceSectorStatus.Locked));Assert.That(map.TrySelect(final),Is.False);
            seed(30);Fund(meta,data);
            foreach(var pair in ordinary)Assert.That(map.GetStatus(pair.Key),Is.EqualTo(pair.Value),pair.Key);
            Assert.That(map.TrySelect(final),Is.True);Assert.That(map.TryBuildRunConfig(out var config),Is.True);
            Assert.That(config.IsEscapeFinal,Is.True);Assert.That(config.Content.GuaranteedEvents,Does.Contain(config.RequiredFinalEvent));
            Assert.That(config.RequiredFinalEvent,Is.TypeOf<OrbitalRelayEvent>());
            Assert.That(meta.TryCompleteEscapeProtocol(config,RunEndReason.PlayerDied,true),Is.False);
            Assert.That(meta.TryCompleteEscapeProtocol(config,RunEndReason.Victory,false),Is.False);
            Assert.That(meta.TryCompleteEscapeProtocol(map.Definition.Find("A1").BuildRunConfig(map.Definition.Id),RunEndReason.Victory,true),Is.False);
            Assert.That(meta.TryCompleteEscapeProtocol(config,RunEndReason.Victory,true),Is.True);
            Assert.That(meta.TryCompleteEscapeProtocol(config,RunEndReason.Victory,true),Is.False);
            meta.ReloadFromStorage();Assert.That(meta.EscapeProtocolCompleted,Is.True);Assert.That(meta.HasEscapeProtocolCompletionUpdate,Is.True);
            meta.AcknowledgeEscapeProtocolCompletion();meta.ReloadFromStorage();Assert.That(meta.HasEscapeProtocolCompletionUpdate,Is.False);
            Assert.That(map.TrySelect("A1"),Is.True,"Ordinary expeditions remain playable after the global goal.");
        });
    }
}
#endif
