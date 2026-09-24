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


    [Test]
    public void HudUsesAuthoredGraphicsOnly()
    {
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
        hud.SetHealth(78,100);
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
