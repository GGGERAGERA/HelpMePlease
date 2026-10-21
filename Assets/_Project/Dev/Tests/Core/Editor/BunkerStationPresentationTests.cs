#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Subject42.Combat.OrbitalStation;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class BunkerStationPresentationTests
{
    private const string Folder = "Assets/_Project/prefabs/UI/Stations/";
    private static T Read<T>(Object target, string name)
    {
        for (var type=target.GetType(); type != null; type=type.BaseType)
        { var field=type.GetField(name,BindingFlags.Instance|BindingFlags.NonPublic); if(field!=null) return (T)field.GetValue(target); }
        throw new MissingFieldException(name);
    }

    [Test]
    public void ModuleCatalogContainsProductionOrbitalArt()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<BunkerSelectionCatalog>(
            "Assets/_Project/Data/SurfaceMap/BunkerSelectionCatalog.asset");
        Assert.That(catalog.modules, Is.Not.Null.And.Not.Empty, "Orbital catalog was not migrated.");
        Assert.That(catalog.modules.Select(m => m.Kind).Distinct().Count(), Is.EqualTo(catalog.modules.Length));
        foreach (var module in catalog.modules)
        {
            Assert.That(module.Icon, Is.Not.Null, module.Kind.ToString());
            Assert.That(module.Icon.rect.width / module.Icon.rect.height, Is.InRange(.65f, 1.5f),
                "Use the round orbital module art, not legacy firearms.");
            var image = new Texture2D(2,2);
            try
            {
                image.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(module.Icon)));
                Assert.That(image.GetPixels32().Count(p => p.a > 0), Is.GreaterThan(100), "Preview must contain rendered weapon art.");
            }
            finally { Object.DestroyImmediate(image); }
        }
    }

    [Test]
    public void ApprovedWindowHasNoUpgradeLamp()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "CharacterSelectionWindow.prefab");
        Assert.That(prefab.GetComponentsInChildren<Transform>(true).Any(t => t.name == "PaintedUpgradeLamp"), Is.False);
        var label = prefab.transform.Find("StationProgressPanel/UpgradeStationButton/Label") as RectTransform;
        Assert.That(label, Is.Not.Null);
        Assert.That(label.anchoredPosition.x, Is.Zero);
    }

    [UnityTest]
    public IEnumerator AllStationsReuseApprovedWindow()
    {
        SetResolution();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return CoreTestSupport.LoadBunker();
        var panels = Object.FindFirstObjectByType<BunkerPanelManager>();
        BunkerSelectionWindow common = null;
        int station = 0;
        Directory.CreateDirectory(BunkerStationPresentationAuthoring.Output);
        foreach (Action open in new Action[] { panels.OpenCharacterSelection, panels.OpenWeaponSelection,
                     panels.OpenUpgrade, panels.OpenAnomalyStabilizers })
        {
            open();
            yield return null;
            var window = Object.FindObjectsByType<BunkerSelectionWindow>(FindObjectsSortMode.None).Single(w => w.IsOpen);
            if (common == null) common = window;
            Assert.That(window, Is.SameAs(common), "All station frames must have one owner.");
            Assert.That(window.GetComponentsInChildren<BunkerSelectionCardView>().Length, Is.GreaterThan(0));
            foreach (var language in new[] { GameLanguage.Russian, GameLanguage.English })
            {
                LocalizationService.Instance.SetLanguage(language);
                yield return null;
                Canvas.ForceUpdateCanvases();
                foreach (var label in window.GetComponentsInChildren<TMP_Text>())
                {
                    label.ForceMeshUpdate();
                    Assert.That(label.isTextOverflowing, Is.False, label.name + ": " + label.text);
                }
                yield return null;
                yield return null;
                File.WriteAllLines(BunkerStationPresentationAuthoring.Output + "/" + station + "-cards.txt",
                    window.GetComponentsInChildren<BunkerSelectionCardView>().Select(c =>
                    { var image=Read<Image>(c,"icon"); return c.EntryId+" sprite="+image.sprite+" enabled="+image.enabled+
                        " active="+image.gameObject.activeInHierarchy+" cull="+image.canvasRenderer.cull+" tint="+image.color+
                        " rect="+image.rectTransform.rect+" scale="+image.rectTransform.lossyScale; }));
                string path = BunkerStationPresentationAuthoring.Output + "/" + station + "-" + language + ".png";
                if (File.Exists(path)) File.Delete(path);
                ScreenCapture.CaptureScreenshot(path);
                yield return CoreTestSupport.Await(() => File.Exists(path));
            }
            panels.CloseAll(false);
            Assert.That(window.IsOpen, Is.False);
            station++;
        }
    }

    [UnityTest]
    public IEnumerator PurchasesLocksAndReopeningDoNotLeakState()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return CoreTestSupport.LoadBunker();
        yield return ExercisePurchases();
    }

    // Allocate captured locals after EnterPlayMode's domain reload.
    private static IEnumerator ExercisePurchases()
    {
        var panels = Object.FindFirstObjectByType<BunkerPanelManager>();
        CurrencyManager.Instance.AddGold(2000);
        panels.OpenWeaponSelection(); yield return null;
        var window = Object.FindObjectsByType<BunkerSelectionWindow>(FindObjectsSortMode.None).Single(w => w.IsOpen);
        var displayed = Read<BunkerSelectionWindowModel>(window,"model");
        Assert.That(displayed, Is.Not.Null);
        File.WriteAllText(BunkerStationPresentationAuthoring.Output+"/interaction-trace.txt", "model entries="+displayed.Entries.Count+"\n");
        var locked = displayed.Entries.First(e => e != null && e.Locked);
        File.AppendAllText(BunkerStationPresentationAuthoring.Output+"/interaction-trace.txt", "locked="+locked.Id+"\n");
        var card = window.GetComponentsInChildren<BunkerSelectionCardView>().First(c => c.EntryId == locked.Id);
        File.AppendAllText(BunkerStationPresentationAuthoring.Output+"/interaction-trace.txt", "card="+card.name+" button="+card.GetComponent<Button>()+"\n");
        card.GetComponent<Button>().onClick.Invoke();
        File.AppendAllText(BunkerStationPresentationAuthoring.Output+"/interaction-trace.txt", "card clicked\n");
        Assert.That(Read<BunkerSelectionEntryModel>(window,"selected").Id, Is.EqualTo(locked.Id));
        var item = Read<BunkerProgressionView>(window,"itemProgression");
        File.AppendAllText(BunkerStationPresentationAuthoring.Output+"/interaction-trace.txt", "item="+item+"\n");
        Assert.That(Read<Button>(item,"upgradeButton").interactable, Is.False);
        panels.OpenUpgrade(); yield return null;
        File.AppendAllText(BunkerStationPresentationAuthoring.Output+"/interaction-trace.txt", "upgrade opened\n");
        var target = Read<BunkerSelectionEntryModel>(window,"selected");
        Assert.That(target.Id, Is.EqualTo(MetaUpgradeType.Hp.ToString()));
        var hold = Read<Button>(item,"upgradeButton").GetComponent<HoldInvestmentInput>();
        int before = CurrencyManager.Instance.TotalGold;
        var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
        ExecuteEvents.Execute(hold.gameObject, pointer, ExecuteEvents.pointerDownHandler);
        yield return new WaitForSeconds(.25f);
        ExecuteEvents.Execute(hold.gameObject, pointer, ExecuteEvents.pointerUpHandler);
        int spent = before - CurrencyManager.Instance.TotalGold;
        Assert.That(spent, Is.GreaterThan(0));
        Assert.That(MetaProgressionManager.Instance.GetInvestedGold(MetaUpgradeType.Hp), Is.EqualTo(spent));
        // An open-source change must cancel held investment, with no later debit.
        ExecuteEvents.Execute(hold.gameObject, pointer, ExecuteEvents.pointerDownHandler);
        panels.OpenAnomalyStabilizers();
        int afterClose = CurrencyManager.Instance.TotalGold;
        yield return new WaitForSeconds(.25f);
        Assert.That(CurrencyManager.Instance.TotalGold, Is.EqualTo(afterClose));
        panels.OpenCharacterSelection(); yield return null;
        Assert.That(Read<BunkerSelectionEntryModel>(window,"selected").IsCharacter, Is.True);
        Assert.That(item.gameObject.activeSelf, Is.False);
        // All authored future subjects remain anonymous in both cards and details.
        foreach (var entry in Read<BunkerSelectionWindowModel>(window,"model").Entries.Where(e => e.Locked))
        {
            Assert.That(entry.CharacterVisual, Is.Null); Assert.That(entry.Icon, Is.Null);
            Assert.That(entry.Feature, Is.Null.Or.Empty); Assert.That(entry.Stats, Is.Empty);
        }
        var stationProgress = Read<BunkerProgressionView>(window,"stationProgress");
        var stationInput = Read<Button>(stationProgress,"upgradeButton").GetComponent<HoldInvestmentInput>();
        int stationGold = CurrencyManager.Instance.TotalGold;
        int stationCost = BunkerStationProgressionService.Instance.GetUpgradeCost(BunkerStationId.Character);
        ExecuteEvents.Execute(stationInput.gameObject,pointer,ExecuteEvents.pointerDownHandler);
        yield return CoreTestSupport.Await(() => BunkerStationProgressionService.Instance.GetLevel(BunkerStationId.Character)==2);
        ExecuteEvents.Execute(stationInput.gameObject,pointer,ExecuteEvents.pointerUpHandler);
        Assert.That(stationGold-CurrencyManager.Instance.TotalGold,Is.EqualTo(stationCost+
            BunkerStationProgressionService.Instance.GetInvestedGold(BunkerStationId.Character)),"Exactly one station investment owner.");
        Assert.That(Read<BunkerSelectionWindowModel>(window,"model").Entries.Count(e=>!e.Locked),Is.GreaterThan(1));
        var subject=Read<BunkerSelectionWindowModel>(window,"model").Entries.First(e=>!e.Locked && e.Id!=RunSelectionManager.Instance.SelectedCharacter.Id.ToString());
        window.GetComponentsInChildren<BunkerSelectionCardView>().First(c=>c.EntryId==subject.Id).GetComponent<Button>().onClick.Invoke();
        Read<Button>(window,"confirmButton").onClick.Invoke();
        Assert.That(RunSelectionManager.Instance.SelectedCharacter.Id.ToString(),Is.EqualTo(subject.Id));
        Assert.That(window.IsOpen,Is.False);
        panels.OpenAnomalyStabilizers();
        var chosenAnomaly=Read<BunkerSelectionEntryModel>(window,"selected");
        Read<Button>(window,"confirmButton").onClick.Invoke();
        Assert.That(RunSelectionManager.Instance.SelectedAnomalyStabilizer.Id,Is.EqualTo(chosenAnomaly.Id));
        CurrencyManager.Instance.AddGold(-CurrencyManager.Instance.TotalGold);
        foreach (Action open in new Action[] { panels.OpenCharacterSelection, panels.OpenWeaponSelection,
                     panels.OpenUpgrade, panels.OpenAnomalyStabilizers })
        {
            open(); yield return null;
            foreach (var progression in window.GetComponentsInChildren<BunkerProgressionView>())
                Assert.That(Read<Button>(progression,"upgradeButton").interactable, Is.False);
        }
        panels.CloseAll(false);
        Assert.That(Read<IBunkerSelectionSource>(window,"source"), Is.Null, "Closed windows must release their source subscriptions.");
        // The real progression services re-read these stored values when sources prepare.
        foreach (var id in new[] {BunkerStationId.Character,BunkerStationId.Weapon,BunkerStationId.Upgrades,BunkerStationId.Anomaly})
            PlayerPrefs.SetInt("BunkerStationLevel_"+id,3);
        PlayerPrefs.SetInt("META_HP_LEVEL",MetaProgressionManager.Instance.MaxLevel);
        PlayerPrefs.SetInt("META_ORBITAL_MODULE_LEVEL_Pistol",MetaProgressionManager.Instance.MaxLevel);
        foreach (var anomaly in AssetDatabase.LoadAssetAtPath<BunkerSelectionCatalog>("Assets/_Project/Data/SurfaceMap/BunkerSelectionCatalog.asset").anomalies)
            PlayerPrefs.SetInt("META_ANOMALY_LEVEL_"+anomaly.Id,anomaly.MaxMetaLevel);
        foreach (Action open in new Action[] { panels.OpenCharacterSelection, panels.OpenWeaponSelection,panels.OpenUpgrade,panels.OpenAnomalyStabilizers })
        {
            open(); yield return null;
            var frame=window.transform.Find("PaintedTerminalSurround");
            Assert.That(frame, Is.Not.Null);
            foreach (var progression in window.GetComponentsInChildren<BunkerProgressionView>())
                Assert.That(Read<Button>(progression,"upgradeButton").gameObject.activeSelf, Is.False,"Maxed upgrades must hide purchase.");
            panels.CloseAll(false);
        }
    }

    [UnityTest]
    public IEnumerator CommonFrameChangesAndItemContentStayIndependent()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return CoreTestSupport.LoadBunker();
        yield return ExerciseArchitecture();
    }

    private static IEnumerator ExerciseArchitecture()
    {
        var panels=Object.FindFirstObjectByType<BunkerPanelManager>();
        panels.OpenCharacterSelection();
        var window=Object.FindObjectsByType<BunkerSelectionWindow>(FindObjectsSortMode.None).Single(w=>w.IsOpen);
        var frame=window.GetComponent<Image>();
        var characterPortrait=Read<Image>(Read<BunkerSelectionDetailView>(window,"detailView"),"portrait");
        var characterSprite=characterPortrait.sprite;
        Color original=frame.color;
        Color marker=new Color(.4f,.7f,.6f);
        try
        {
            frame.color=marker;
            foreach(Action open in new Action[]{panels.OpenWeaponSelection,panels.OpenUpgrade,panels.OpenAnomalyStabilizers,panels.OpenCharacterSelection})
            {
                open(); yield return null;
                var active=Object.FindObjectsByType<BunkerSelectionWindow>(FindObjectsSortMode.None).Single(w=>w.IsOpen);
                Assert.That(active,Is.SameAs(window));
                Assert.That(frame.color,Is.EqualTo(marker),"One frame change must reach every station.");
                Assert.That(characterPortrait.sprite,Is.SameAs(characterSprite),"Specialized item binding must not change character art.");
            }
        }
        finally { frame.color=original; panels.CloseAll(false); }
    }

    [Serializable] private sealed class Pref { public string key; public bool exists; public int value; }
    [Serializable] private sealed class Prefs { public List<Pref> values = new(); }
    private const string Backup = "BunkerStations.PreferenceBackup";
    [SetUp] public void Preserve()
    {
        Directory.CreateDirectory(BunkerStationPresentationAuthoring.Output);
        if (string.IsNullOrEmpty(SessionState.GetString(Backup,"")))
        {
            var keys = new List<string>();
            foreach (var id in Enum.GetValues(typeof(BunkerStationId)))
            { keys.Add("BunkerStationLevel_"+id); keys.Add("BunkerStationInvested_"+id); }
            foreach (var kind in Enum.GetValues(typeof(OrbitalModuleKind)))
            { keys.Add("META_ORBITAL_MODULE_LEVEL_"+kind); keys.Add("META_ORBITAL_MODULE_INVESTED_"+kind); }
            foreach (var type in Enum.GetValues(typeof(MetaUpgradeType))) keys.Add("META_UPGRADE_INVESTED_"+type);
            keys.AddRange(new[] { "META_HP_LEVEL", "META_DAMAGE_LEVEL", "META_MOVE_SPEED_LEVEL", "META_XP_GAIN_LEVEL", "META_GOLD_GAIN_LEVEL", "META_PICKUP_RADIUS_LEVEL" });
            var catalog = AssetDatabase.LoadAssetAtPath<BunkerSelectionCatalog>("Assets/_Project/Data/SurfaceMap/BunkerSelectionCatalog.asset");
            foreach (var anomaly in catalog.anomalies)
            { keys.Add("META_ANOMALY_LEVEL_"+anomaly.Id); keys.Add("META_ANOMALY_INVESTED_"+anomaly.Id); }
            var saved = new Prefs();
            foreach (string key in keys) saved.values.Add(new Pref {key=key,exists=PlayerPrefs.HasKey(key),value=PlayerPrefs.GetInt(key)});
            SessionState.SetString(Backup,JsonUtility.ToJson(saved));
        }
        CoreTestSupport.PreservePreferences();
        PlayerPrefs.SetInt("TOTAL_GOLD",0);
        foreach (var pref in JsonUtility.FromJson<Prefs>(SessionState.GetString(Backup, "")).values)
            PlayerPrefs.DeleteKey(pref.key);
    }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        var cleanup = CoreTestSupport.CleanupPlayMode();
        while (cleanup.MoveNext()) yield return cleanup.Current;
        string json=SessionState.GetString(Backup,"");
        if (!string.IsNullOrEmpty(json))
        {
            var values=JsonUtility.FromJson<Prefs>(json).values;
            foreach (var pref in values)
                if (pref.exists) PlayerPrefs.SetInt(pref.key,pref.value); else PlayerPrefs.DeleteKey(pref.key);
            foreach (var pref in values)
            {
                Assert.That(PlayerPrefs.HasKey(pref.key),Is.EqualTo(pref.exists),pref.key);
                if(pref.exists) Assert.That(PlayerPrefs.GetInt(pref.key),Is.EqualTo(pref.value),pref.key);
            }
            File.WriteAllText(BunkerStationPresentationAuthoring.Output+"/preferences-restored.txt","PASS: all "+values.Count+" station/item keys restored; common gold/language/unlocks restored by CoreTestSupport.");
        }
        PlayerPrefs.Save(); SessionState.EraseString(Backup);
    }
    private static void SetResolution()
    {
        var assembly=typeof(Editor).Assembly;
        var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
        var sizes=typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
        var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new object[]{0});
        var type=group.GetType(); int count=(int)type.GetMethod("GetTotalCount").Invoke(group,null), index=-1;
        for(int i=0;i<count;i++)
        { var size=type.GetMethod("GetGameViewSize").Invoke(group,new object[]{i});
          if((int)size.GetType().GetProperty("width").GetValue(size)==1600 && (int)size.GetType().GetProperty("height").GetValue(size)==900) {index=i;break;} }
        if(index<0)
        { var size=Activator.CreateInstance(assembly.GetType("UnityEditor.GameViewSize"),new object[]{Enum.ToObject(assembly.GetType("UnityEditor.GameViewSizeType"),1),1600,900,"Station QA 1600×900"});
          type.GetMethod("AddCustomSize").Invoke(group,new[]{size}); index=count; }
        var view=EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        view.GetType().GetProperty("selectedSizeIndex").SetValue(view,index); view.Show();
    }
}
#endif
