#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class PauseOverviewTests
{
    [Test]
    public void AuthoredOverviewHasAllRequiredReferences()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/UI/PausePanel.prefab");
        var view = prefab.GetComponent<PauseBuildOverview>();
        Assert.That(view, Is.Not.Null);
        foreach (var field in typeof(PauseBuildOverview).GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
            if (field.IsDefined(typeof(SerializeField), false))
                Assert.That((Object)field.GetValue(view), Is.Not.Null, field.Name);
        foreach (var component in prefab.GetComponentsInChildren<Component>(true))
            Assert.That(component, Is.Not.Null, "Missing prefab script");
        foreach (var text in prefab.GetComponentsInChildren<TMP_Text>(true))
            Assert.That(text.font, Is.Not.Null, text.name);
    }

    [Test]
    public void AuthoredOverviewReadsStateAndFitsSupportedAspectRatios()
    {
        RenderFixtures();
    }

    private static void RenderFixtures()
    {
        var scene=EditorSceneManager.NewPreviewScene();
        var previousLocalization = LocalizationService.Instance;
        var localizationObject = new GameObject("PreviewLocalization");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(localizationObject, scene);
        var localization = localizationObject.AddComponent<LocalizationService>();
        typeof(LocalizationService).GetField("table", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(localization, Resources.Load<LocalizationTable>("Localization/LocalizationTable"));
        var instanceField = typeof(LocalizationService).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        instanceField.SetValue(null, localization);
        var root=new GameObject("PreviewCanvas",typeof(RectTransform),typeof(Canvas));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
        var cameraObject=new GameObject("PreviewCamera",typeof(Camera));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject,scene);
        var camera=cameraObject.GetComponent<Camera>();camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.02f,.035f,.04f);camera.orthographic=true;camera.transform.position=new Vector3(0,0,-10);camera.scene=scene;
        var canvas=root.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=camera;
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/UI/PausePanel.prefab");
        var instance=Object.Instantiate(prefab,root.transform);instance.SetActive(true);
        var view=instance.GetComponent<PauseBuildOverview>();
        var bodyUpgrade=ScriptableObject.CreateInstance<UpgradeData>();bodyUpgrade.upgradeName="MAX HP";bodyUpgrade.upgradeType=UpgradeType.MaxHealthFlat;
        try
        {
            foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(1920,1200),new Vector2Int(2560,1080)})
            foreach(int count in new[]{1,3,9})
            {
                var state=OrbitalRunState.CreateDefault(1);
                while(state.Rings.Count<count) state.DebugAddRingBeyondCap();
                if(count>1)
                {
                    for(int i=0;i<count;i++)
                    {
                        var ring=state.Rings[i];
                        for(int n=0;n<i%4;n++)state.UpgradeRingPower(ring.StableRingId);
                        if(i%4==3) {state.UpgradeRingSpeed(ring.StableRingId);state.AddMount(ring.StableRingId,out _);}
                        if(i>0)state.InstallModule((OrbitalModuleKind)(i%5),ring.StableRingId,0,out _);
                    }
                    state.UpgradeCore();state.UpgradeCore();
                    if(count==9)
                    {
                        state.InstallModule(OrbitalModuleKind.LinkNode,state.Rings[4].StableRingId,1,out _);
                        state.UpgradeLinkMatrix();
                    }
                }
                var before=JsonUtility.ToJson(state);
                var items=new RunItemSlots();
                if(count==9)items.TryAdd(bodyUpgrade);
                view.RefreshBuild(state,items);
                Assert.That(JsonUtility.ToJson(state),Is.EqualTo(before));
                Assert.That(Field<GameObject>(view,"coreSection").activeSelf,Is.EqualTo(count>1));
                Assert.That(Field<GameObject>(view,"playerSection").activeSelf,Is.EqualTo(count==9));
                if(count==9)Assert.That(Field<TMP_Text>(view,"coreText").text,Does.Contain("Link Matrix"));
                Assert.That(Field<TMP_Text>(view,"ringsTitle").text,Does.EndWith(count.ToString()));
                Assert.That(Field<TMP_Text>(view,"ringsText").text,Does.Contain("POWER +0"));
                var texture=new RenderTexture(size.x,size.y,24);camera.targetTexture=texture;
                ((RectTransform)root.transform).sizeDelta = size;
                camera.orthographicSize = size.y / 2f;
                canvas.pixelPerfect=true;
                Canvas.ForceUpdateCanvases();
                foreach(var rt in instance.GetComponentsInChildren<RectTransform>()) LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
                Canvas.ForceUpdateCanvases();
                typeof(PauseBuildOverview).GetMethod("FitWindow",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(view,null);
                Canvas.ForceUpdateCanvases();
                var corners=new Vector3[4];Field<RectTransform>(view,"window").GetWorldCorners(corners);
                foreach(var corner in corners)
                {
                    var point=RectTransformUtility.WorldToScreenPoint(camera,corner);
                    Assert.That(point.x,Is.InRange(0,size.x));Assert.That(point.y,Is.InRange(0,size.y));
                }
                var scroll=Field<ScrollRect>(view,"ringsScroll");
                foreach (var scroller in instance.GetComponentsInChildren<ScrollRect>())
                    typeof(ScrollRect).GetMethod("LateUpdate", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(scroller, null);
                if(count==9)
                {
                    Assert.That(scroll.content.rect.height,Is.GreaterThan(scroll.viewport.rect.height));
                    scroll.verticalNormalizedPosition=0f;
                    Assert.That(scroll.content.anchoredPosition.y,Is.GreaterThan(0));
                    scroll.verticalNormalizedPosition=1f;
                    Assert.That(Field<TMP_Text>(view,"ringsText").text,Does.Contain("CYAN"));
                }
                Field<TMP_Text>(view,"runText").text=count==1?"Время  <color=#FFFFFF>00:12</color>     Убийства  <color=#FFFFFF>3</color>     Уровень  <color=#FFFFFF>1</color>     Сектор  <color=#FFFFFF>1 / 3</color>":"Время  <color=#FFFFFF>18:42</color>     Убийства  <color=#FFFFFF>824</color>     Уровень  <color=#FFFFFF>24</color>     Сектор  <color=#FFFFFF>3 / 3</color>";
                camera.Render();
                var old=RenderTexture.active;RenderTexture.active=texture;
                var png=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);png.ReadPixels(new Rect(0,0,size.x,size.y),0,0);png.Apply();
                Directory.CreateDirectory("../PauseScreenshots");File.WriteAllBytes($"../PauseScreenshots/pause-{count}rings-{size.x}x{size.y}.png",png.EncodeToPNG());
                RenderTexture.active=old;camera.targetTexture=null;Object.DestroyImmediate(texture);Object.DestroyImmediate(png);
            }
            int confirmations=0;
            view.AskConfirmation("pause.confirmRestart",()=>confirmations++);
            Assert.That(view.IsConfirming,Is.True);
            Assert.That(Field<CanvasGroup>(view,"content").interactable,Is.False);
            view.CancelConfirmation();
            Assert.That(confirmations,Is.Zero);
            Assert.That(view.IsConfirming,Is.False);
            view.AskConfirmation("pause.confirmBunker",()=>confirmations++);
            typeof(PauseBuildOverview).GetMethod("Confirm",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(view,null);
            typeof(PauseBuildOverview).GetMethod("Confirm",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(view,null);
            Assert.That(confirmations,Is.EqualTo(1));
        }
        finally {Object.DestroyImmediate(bodyUpgrade);EditorSceneManager.ClosePreviewScene(scene);instanceField.SetValue(null, previousLocalization);}
    }
    private static T Field<T>(object target,string name) => (T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
}
#endif

