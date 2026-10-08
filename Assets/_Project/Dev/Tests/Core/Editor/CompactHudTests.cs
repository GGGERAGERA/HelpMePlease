#if UNITY_EDITOR
using System.IO;
using System.Collections;
using System.Linq;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class CompactHudTests
{
    [TestCase(NoDamageChallengeState.Completed)]
    [TestCase(NoDamageChallengeState.Failed)]
    public void MechanicNoticeHidesEntirePanelAfterTimeoutAndCanShowAgain(NoDamageChallengeState state)
    {
        const System.Reflection.BindingFlags fields = System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic;
        var instanceField = typeof(LocalizationService).GetField("<Instance>k__BackingField",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var previousService = LocalizationService.Instance;
        var services = new GameObject("Mechanic notice test services");
        services.SetActive(false);
        var service = services.AddComponent<LocalizationService>();
        var serviceData = new SerializedObject(service);
        serviceData.FindProperty("table").objectReferenceValue = AssetDatabase.LoadAssetAtPath<LocalizationTable>(
            "Assets/_Project/Data/Localization/LocalizationTable.asset");
        serviceData.ApplyModifiedPropertiesWithoutUndo();
        instanceField.SetValue(null, service);
        var root = PrefabUtility.LoadPrefabContents(CompactHudTestData.Folder + "GameplayHUD.prefab");
        var view = root.GetComponent<LevelMechanicsPanel>();
        void Invoke(string name) => typeof(LevelMechanicsPanel).GetMethod(name, fields).Invoke(view, null);
        try
        {
            var challenge = services.AddComponent<NoDamageChallenge>();
            typeof(NoDamageChallenge).GetField("<State>k__BackingField", fields).SetValue(challenge, state);
            var data = new SerializedObject(view);
            data.FindProperty("noDamageChallenge").objectReferenceValue = challenge;
            data.ApplyModifiedPropertiesWithoutUndo();
            var panel = (GameObject)data.FindProperty("panelRoot").objectReferenceValue;
            Invoke("OnEnable");
            Invoke("Update");
            Assert.That(panel.activeSelf, Is.True, "A new result should show its explanation.");
            typeof(LevelMechanicsPanel).GetField("noticeUntil", fields).SetValue(view, Time.unscaledTime - 1f);
            Invoke("Update");
            Assert.That(panel.activeSelf, Is.False, "The background must disappear with the explanation.");
            Invoke("Update");
            Assert.That(panel.activeSelf, Is.False, "An unchanged result must not reopen the panel.");
            Invoke("OnDisable");
            Invoke("OnEnable");
            Invoke("Update");
            Assert.That(panel.activeSelf, Is.True, "Re-entering the HUD should show current context again.");
        }
        finally
        {
            Invoke("OnDisable");
            PrefabUtility.UnloadPrefabContents(root);
            Object.DestroyImmediate(services);
            instanceField.SetValue(null, previousService);
        }
    }

    [Test]
    public void HudUsesAuthoredGraphicsOnly()
    {
        foreach(string path in new[]{"scripts/Combat/Player/PlayerHealth.cs","scripts/Combat/Enemies/EnemyHealth.cs","scripts/Run/Threat/RunThreatController.cs"})
        {
            string source=File.ReadAllText("Assets/_Project/"+path);
            Assert.That(source,Does.Not.Contain("HUDManager").And.Not.Contain("CameraShake").And.Not.Contain("DamagePopup"),path);
        }
        foreach (string name in new[] { "TacticalMapHUD", "RunRouteProgressView", "LevelAnomalyView" })
        {
            string source = File.ReadAllText("Assets/_Project/scripts/UI/HUD/" + name + ".cs");
            Assert.That(source, Does.Not.Contain("AddComponent<"), name);
            Assert.That(source, Does.Not.Contain("typeof(RectTransform)"), name);
        }
    }

    [Test]
    public void ExistingButtonsRemainClickable()
    {
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(CompactHudTestData.Folder+"GameplayHUD.prefab");
        foreach(var button in prefab.GetComponentsInChildren<Button>(true))
            Assert.That(button.targetGraphic!=null && button.targetGraphic.raycastTarget, Is.True, button.name);
    }

    [Test]
    public void ReusableViewsAreSavedPrefabs()
    {
        foreach (string name in new[] { "HudBar", "HudIconNumber", "HudSegments", "HudKeyHint", "BulletTimeHUD", "TacticalMapMarker" })
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/prefabs/UI/HUD/" + name + ".prefab"), Is.Not.Null, name);
    }
}
public sealed class CompactHudPlayTests
{
    [SetUp] public void Preserve() => CoreTestSupport.PreservePreferences();
    [UnitySetUp] public IEnumerator Begin() { Set1080p(); return CoreTestSupport.BeginRun(); }
    static void Set1080p()
    {
        var assembly=typeof(Editor).Assembly;
        var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
        var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes=singleton.GetProperty("instance").GetValue(null);
        var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new object[]{0});
        var groupType=group.GetType();
        var count=(int)groupType.GetMethod("GetTotalCount").Invoke(group,null);
        int index=-1;
        for(int i=0;i<count;i++)
        {
            var size=groupType.GetMethod("GetGameViewSize").Invoke(group,new object[]{i});
            var type=size.GetType();
            if((int)type.GetProperty("width").GetValue(size)==1920 && (int)type.GetProperty("height").GetValue(size)==1080)
            { index=i; break; }
        }
        if(index<0)
        {
            var sizeType=assembly.GetType("UnityEditor.GameViewSize");
            var mode=System.Enum.ToObject(assembly.GetType("UnityEditor.GameViewSizeType"),1);
            var size=System.Activator.CreateInstance(sizeType,new object[]{mode,1920,1080,"HUD QA 1080p"});
            groupType.GetMethod("AddCustomSize").Invoke(group,new[]{size}); index=count;
        }
        var view=EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        view.GetType().GetProperty("selectedSizeIndex").SetValue(view,index);
        view.Show();
    }
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();
    [UnityTest]
    public IEnumerator CombatHudAt1080p()
    {
        Application.runInBackground = true;

        Assert.That(new Vector2Int(Screen.width, Screen.height), Is.EqualTo(new Vector2Int(1920,1080)));
        var hud = Object.FindFirstObjectByType<HUDManager>();
        yield return CoreTestSupport.Await(() => hud.IsInformationVisible);
        yield return new WaitForSecondsRealtime(7);
        Assert.That(Object.FindFirstObjectByType<BulletTimeHudView>(), Is.Not.Null);
        hud.SetThreat(78, ThreatTier.Tier4);
        var segments = CompactHudTestData.Get<HudSegments>(hud,"threatSegments");
        var data = new SerializedObject(segments);
        var images = data.FindProperty("segments");
        Assert.That(images.arraySize, Is.EqualTo(4));
        for(int i=0;i<4;i++) Assert.That(((Image)images.GetArrayElementAtIndex(i).objectReferenceValue).color,
            Is.EqualTo(data.FindProperty("activeColor").colorValue));
        hud.SetThreat(0, ThreatTier.Tier1);
        Assert.That(((Image)images.GetArrayElementAtIndex(1).objectReferenceValue).color,
            Is.EqualTo(data.FindProperty("inactiveColor").colorValue));
        var map = CompactHudTestData.Get<TacticalMapHUD>(hud,"tacticalMap");
        Assert.That(map.LayoutRoot.GetComponentsInChildren<TMP_Text>(true), Is.Empty);
        Directory.CreateDirectory("Artifacts/GeneratedQA/CompactHud");
        var player=Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer;
        var health=player.GetComponent<PlayerHealth>();
        var healthBar=CompactHudTestData.Get<HudBar>(hud,"healthBar");
        var healthSlider=CompactHudTestData.Get<Slider>(healthBar,"slider");
        health.SetRuntimeHealth(100,78);
        Assert.That(healthSlider.value,Is.EqualTo(78));
        hud.BindPlayer(null); health.SetCurrentHealth(65);
        Assert.That(healthSlider.value,Is.EqualTo(78),"Released player must not write HUD.");
        hud.BindPlayer(player); Assert.That(healthSlider.value,Is.EqualTo(65),"Rebind snapshots current health.");
        health.Heal(13); Assert.That(healthSlider.value,Is.EqualTo(78));
        var bossObject=new GameObject("HUD boss probe"); bossObject.SetActive(false);
        var boss=bossObject.AddComponent<EnemyHealth>();
        var bossData=new SerializedObject(boss);bossData.FindProperty("isBoss").boolValue=true;bossData.ApplyModifiedPropertiesWithoutUndo();
        bossObject.SetActive(true);boss.SetRuntimeMaxHealth(100);
        var bossPanel=CompactHudTestData.Get<GameObject>(hud,"bossHpPanel");var bossSlider=CompactHudTestData.Get<Slider>(hud,"bossHpSlider");
        Assert.That(bossPanel.activeSelf,Is.True);boss.TakeDamage(10,Vector2.zero);Assert.That(bossSlider.value,Is.EqualTo(90));
        bossObject.SetActive(false);Assert.That(bossPanel.activeSelf,Is.False);
        bossObject.SetActive(true);Assert.That(bossPanel.activeSelf,Is.True);Object.Destroy(bossObject);
        yield return null;Assert.That(bossPanel.activeSelf,Is.False);
        ScreenCapture.CaptureScreenshot("Artifacts/GeneratedQA/CompactHud/gameplay-1920x1080.png");
        yield return new WaitForSecondsRealtime(.5f);
        var station = Object.FindFirstObjectByType<Subject42.Combat.OrbitalStation.OrbitalStationRuntime>();
        // Drive the production ability data directly: editor OS focus must not affect HUD QA.
        station.BulletTime.Tick(true,.66f);
        yield return null;
        Assert.That(station.BulletTime.Energy, Is.LessThan(1));
        var abilityView=Object.FindFirstObjectByType<BulletTimeHudView>();
        var bar=CompactHudTestData.Get<HudBar>(abilityView,"energy");
        var slider=CompactHudTestData.Get<Slider>(bar,"slider");
        Assert.That(slider.normalizedValue, Is.EqualTo(station.BulletTime.Energy).Within(.05f));
        ScreenCapture.CaptureScreenshot("Artifacts/GeneratedQA/CompactHud/bullet-time-1920x1080.png");
        yield return new WaitForSecondsRealtime(.5f);
        station.BulletTime.Release();
    }

}
static class CompactHudTestData
{
    public const string Folder="Assets/_Project/prefabs/UI/HUD/";
    public static T Get<T>(Object target,string field) where T:Object =>
        new SerializedObject(target).FindProperty(field).objectReferenceValue as T;
}
#endif
