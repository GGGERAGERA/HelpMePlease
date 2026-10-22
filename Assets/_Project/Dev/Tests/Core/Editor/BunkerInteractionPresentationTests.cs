#if UNITY_EDITOR
using System.Collections;
using System;
using System.IO;
using System.Reflection;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class BunkerInteractionPresentationTests
{
    static T Read<T>(Object o,string field) => (T)o.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
    static T[] All<T>() where T:Component => Object.FindObjectsByType<T>(FindObjectsInactive.Include,FindObjectsSortMode.None);
    static void Teleport(Rigidbody2D body,Vector2 position)
    {
        body.position=position;
        body.transform.position=new Vector3(position.x,position.y,body.transform.position.z);
        body.linearVelocity=Vector2.zero;
        Physics2D.SyncTransforms();
    }
    [SetUp] public void Preserve()
    {
        CoreTestSupport.PreservePreferences();
        if(!EditorApplication.isPlaying) EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
    }
    [UnityTearDown] public IEnumerator Cleanup() => CoreTestSupport.CleanupPlayMode();
    [Test]
    public void EditorSyncRepairsDeletedArrowWithoutDuplicates()
    {
        var host=new GameObject("Arrow editor lifecycle",typeof(BunkerInteractionIndicators));
        var stationObject=new GameObject("Editor station",typeof(BunkerStation));
        try
        {
            var manager=host.GetComponent<BunkerInteractionIndicators>();
            var station=stationObject.GetComponent<BunkerStation>();
            var data=new SerializedObject(manager);
            data.FindProperty("arrowPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/prefabs/Bunker/PF_InteractionArrow.prefab");
            data.ApplyModifiedPropertiesWithoutUndo();
            manager.RefreshBindings();
            Assert.That(manager.TryGetArrow(station,out var original),Is.True);
            Object.DestroyImmediate(original.gameObject);
            manager.RefreshBindings();
            Assert.That(manager.TryGetArrow(station,out var repaired),Is.True,"Hierarchy repair must replace a destroyed cached arrow.");
            manager.RefreshBindings();
            Assert.That(All<BunkerInteractionArrow>().Count(a=>a.Source==station),Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(host); Object.DestroyImmediate(stationObject); }
    }

    [Test]
    public void ArrowsAreAuthoredInProductionSceneForEditMode()
    {
        var scene=EditorSceneManager.OpenPreviewScene(BunkerInteractionAuthoring.ScenePath);
        try
        {
            var arrows=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<BunkerInteractionArrow>(true)).ToArray();
            Assert.That(arrows.Length,Is.GreaterThanOrEqualTo(11));
            foreach(var arrow in arrows)
                Assert.That(PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(arrow.gameObject),
                    Is.EqualTo("Assets/_Project/prefabs/Bunker/PF_InteractionArrow.prefab"));
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [UnityTest]
    public IEnumerator OriginalCyanArrowSurvivesInitializationAndAnimates()
    {
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
        yield return new EnterPlayMode();
        yield return CoreTestSupport.LoadBunker();
        yield return ExerciseOriginalAnimation();
    }

    private static IEnumerator ExerciseOriginalAnimation()
    {
        yield return CoreTestSupport.Await(()=>Object.FindFirstObjectByType<BunkerPlayerLoadoutController>() is { IsReady:true });
        var body=PlayerRuntimeReference.CachedPlayer.GetComponent<Rigidbody2D>();
        body.interpolation=RigidbodyInterpolation2D.None; body.constraints=RigidbodyConstraints2D.FreezeAll;
        Teleport(body,new Vector2(-14f,4f)); yield return null; yield return null;
        var arrow=All<BunkerInteractionArrow>().Single(a=>a.Source is BunkerStation && Read<BunkerStationType>(a.Source,"stationType")==BunkerStationType.CharacterSelection);
        var report=new System.Text.StringBuilder();
        foreach(var animator in All<Animator>().Where(a=>a.runtimeAnimatorController!=null && a.runtimeAnimatorController.name=="PF_InteractionArrow"))
            report.AppendLine(BunkerInteractionAuthoring.PathOf(animator.transform)+" active="+animator.gameObject.activeInHierarchy+" enabled="+animator.enabled+" scale="+animator.transform.localScale);
        report.AppendLine("Bound character arrow: visible="+arrow.IsVisible+" active="+arrow.gameObject.activeInHierarchy+" animator="+arrow.GetComponent<Animator>()+" color="+arrow.GetComponentInChildren<SpriteRenderer>(true).color);
        File.WriteAllText(BunkerInteractionAuthoring.Output+"/original-runtime-proof.txt",report.ToString());
        var animation=arrow.GetComponent<Animator>();
        Assert.That(animation,Is.Not.Null,"The interaction must use the original animated prefab, not a static replacement.");
        Assert.That(animation.runtimeAnimatorController,Is.SameAs(AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Project/prefabs/Bunker/PF_InteractionArrow.controller")));
        Assert.That(arrow.IsVisible,Is.True);
        Assert.That(arrow.transform.localScale,Is.EqualTo(Vector3.one));
        var visual=arrow.transform.Find("Visual");
        Assert.That(visual.localScale,Is.EqualTo(new Vector3(1.4f,1.4f,1f)));
        Assert.That(visual.GetComponent<SpriteRenderer>().color,Is.EqualTo(Color.white));
        float start=Time.time, minimum=float.MaxValue, maximum=float.MinValue;
        for(int i=0;i<5;i++)
        {
            while(Time.time<start+i*.45f) yield return null;
            minimum=Mathf.Min(minimum,visual.localPosition.y); maximum=Mathf.Max(maximum,visual.localPosition.y);
            report.AppendLine("t="+(Time.time-start).ToString("F3")+" Visual.y="+visual.localPosition.y.ToString("F4")+" visible="+arrow.IsVisible+" camera="+Object.FindFirstObjectByType<CameraFollow>().ControlledCamera.name);
            yield return Capture("original-animation-"+i,new Vector2(-14f,6f),6f);
        }
        File.WriteAllText(BunkerInteractionAuthoring.Output+"/original-runtime-proof.txt",report.ToString());
        Assert.That(maximum-minimum,Is.GreaterThan(.08f),"The original 1.8-second bob must run in real Play Mode.");
    }

    [UnityTest]
    public IEnumerator CharacterDoorApproachShowsArrowAndCabinetHover()
    {
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
        yield return new EnterPlayMode();
        yield return CoreTestSupport.LoadBunker();
        yield return ExerciseDoorApproach();
    }

    private static IEnumerator ExerciseDoorApproach()
    {
        yield return CoreTestSupport.Await(()=>Object.FindFirstObjectByType<BunkerPlayerLoadoutController>() is { IsReady:true });
        var player=PlayerRuntimeReference.CachedPlayer.GetComponent<Rigidbody2D>();
        player.interpolation=RigidbodyInterpolation2D.None; player.constraints=RigidbodyConstraints2D.FreezeAll;
        Teleport(player,new Vector2(-9.5f,2f));
        yield return null; yield return null;
        var arrow=All<BunkerInteractionArrow>().Single(a=>a.Source is BunkerStation && Read<BunkerStationType>(a.Source,"stationType")==BunkerStationType.CharacterSelection);
        Assert.That(arrow.IsVisible,Is.True,"The arrow must already be visible at the player's doorway position from the report.");
        var cursor=Object.FindFirstObjectByType<BunkerCursorInteractor>();
        var proxy=arrow.Source.GetComponentsInChildren<BunkerInteractableCollider>().First(p=>p.GetComponent<Collider2D>().enabled);
        cursor.UpdateHoverAt(proxy.GetComponent<Collider2D>().bounds.center);
        Assert.That(Read<BunkerInteractableCollider>(cursor,"current"),Is.SameAs(proxy),"Hovering the physical cabinet must keep its existing outline.");
        Assert.That(cursor.TryInteractAt(proxy.GetComponent<Collider2D>().bounds.center),Is.True);
        Assert.That(Object.FindFirstObjectByType<BunkerPanelManager>().IsAnyPanelOpen,Is.True);
        Object.FindFirstObjectByType<BunkerPanelManager>().CloseAll(false);
        yield return Capture("06-character-door-approach",new Vector2(-12f,4.5f),8f);
        cursor.enabled=false;
        cursor.UpdateHoverAt(proxy.GetComponent<Collider2D>().bounds.center);
        var outline=arrow.Source.GetComponent<BunkerHoverOutline>();
        var materials=Read<Material[]>(outline,"materials");
        var boundsOutline=Read<LineRenderer>(outline,"boundsOutlineRenderer");
        Assert.That(materials.Any(m=>m!=null && m.HasProperty("_EnableOutline") && m.GetFloat("_EnableOutline")>0f) ||
            boundsOutline!=null && boundsOutline.enabled,Is.True,"Body hover must enable a rendered outline, not only select a proxy.");
        yield return Capture("07-character-cabinet-hover",new Vector2(-12f,4.5f),8f);
        cursor.enabled=true;
    }

    [Test]
    public void ProductionSceneHasSharedArrowAdapter()
    {
        var scene=EditorSceneManager.OpenPreviewScene(BunkerInteractionAuthoring.ScenePath);
        try
        {
            var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MonoBehaviour>(true));
            Assert.That(all.Count(c=>c!=null && c.GetType().Name=="BunkerInteractionIndicators"),Is.EqualTo(1));
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
    [Test]
    public void ProductionFootballIsPreservedButUnavailable()
    {
        var scene=EditorSceneManager.OpenPreviewScene(BunkerInteractionAuthoring.ScenePath);
        try
        {
            var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
            var football=all.OfType<FootballMinigame>().Single();
            Assert.That(football.enabled,Is.False);
            Assert.That(all.OfType<FootballStartStation>().All(s=>!s.isActiveAndEnabled),Is.True);
            Assert.That(all.OfType<FootballMinigameHUD>().All(h=>!h.gameObject.activeInHierarchy),Is.True);
            Assert.That(scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true))
                .Count(t=>t.name=="FootballComingSoon"),Is.EqualTo(1));
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [UnityTest]
    public IEnumerator ProductionArrowsRouteHoverClicksAndRespectModals()
    {
        typeof(BunkerStationPresentationTests).GetMethod("SetResolution",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);
        yield return new EnterPlayMode();
        yield return CoreTestSupport.LoadBunker();
        yield return ExerciseArrows();
    }

    private static IEnumerator ExerciseArrows()
    {
        yield return CoreTestSupport.Await(()=>Object.FindFirstObjectByType<BunkerPlayerLoadoutController>() is { IsReady:true });
        var player=PlayerRuntimeReference.CachedPlayer;
        var body=player.GetComponent<Rigidbody2D>(); body.constraints=RigidbodyConstraints2D.FreezeAll;
        body.interpolation=RigidbodyInterpolation2D.None;
        var panels=Object.FindFirstObjectByType<BunkerPanelManager>();
        var cursor=Object.FindFirstObjectByType<BunkerCursorInteractor>();
        var manager=Object.FindFirstObjectByType<BunkerInteractionIndicators>();
        var arrows=All<BunkerInteractionArrow>().Where(a=>a.gameObject.scene==manager.gameObject.scene).ToArray();
        Assert.That(arrows.Length,Is.EqualTo(arrows.Select(a=>a.Source).Distinct().Count()),"Exactly one indicator per source.");
        Assert.That(arrows.Any(a=>a.Source is CatPetInteractable || a.Source is FootballStartStation),Is.False);
        Assert.That(arrows.Count(a=>a.Source is BunkerStation),Is.EqualTo(7));
        var trace=new System.Text.StringBuilder();
        int captureIndex=0;
        foreach(var arrow in arrows.Where(a=>a.Source.gameObject.activeInHierarchy && ((IBunkerInteractable)a.Source).CanInteract))
        {
            Teleport(body,(Vector2)arrow.Source.transform.position+Vector2.down*1.5f);
            Physics2D.SyncTransforms(); yield return null; yield return null;
            Assert.That(arrow.IsVisible,Is.True,arrow.name+" player="+PlayerRuntimeReference.PlayerTransform.position+" body="+body.position+
                " source="+arrow.Source.transform.position+" blocked="+manager.IsBlocked+" time="+Time.timeScale+" CanInteract="+((IBunkerInteractable)arrow.Source).CanInteract);
            Assert.That(arrow.GetComponent<Animator>().runtimeAnimatorController.name,Is.EqualTo("PF_InteractionArrow"));
            yield return Capture("provider-"+(++captureIndex).ToString("00"),(Vector2)arrow.Source.transform.position+Vector2.up*2f,8f);
            Physics2D.SyncTransforms();
            cursor.UpdateHoverAt(arrow.ClickPosition);
            Assert.That(arrow.IsHovered,Is.True,arrow.name+" hover");
            Assert.That(arrow.Visual.color,Is.EqualTo(Color.white));
            Teleport(body,(Vector2)arrow.Source.transform.position+Vector2.down*20f);
            Assert.That(arrow.CanInteractNow,Is.True,"Pointer interaction must not depend on player position.");
            Assert.That(cursor.TryInteractAt(arrow.ClickPosition),Is.True,arrow.name+" click routing");
            yield return null; yield return null;
            {
                Assert.That(panels.IsAnyPanelOpen,Is.True,arrow.name+" opens its existing interface");
                Assert.That(arrows.All(a=>!a.IsVisible),Is.True,"All arrows hide behind modal UI.");
                Assert.That(cursor.TryInteractAt(arrow.ClickPosition),Is.False);
                panels.CloseAll(false);
                yield return null; yield return null;
                Assert.That(arrow.IsVisible,Is.True,"Original arrow returns after closing "+arrow.Source.name);
            }
            trace.AppendLine(BunkerInteractionAuthoring.PathOf(arrow.Source.transform)+": unrestricted pointer hover/click, route, UI PASS");
            File.WriteAllText(BunkerInteractionAuthoring.Output+"/runtime-checks.txt",trace.ToString());
            Teleport(body,(Vector2)arrow.Source.transform.position+Vector2.down*20f);
            Physics2D.SyncTransforms(); yield return null; yield return null;
            Assert.That(arrow.IsVisible,Is.True,arrow.name+" remains available independently of player position");
            cursor.UpdateHoverAt(arrow.ClickPosition);
            Assert.That(arrow.IsHovered,Is.True,"Moving the player away must not prevent pointer hover.");
        }
        File.WriteAllText(BunkerInteractionAuthoring.Output+"/runtime-checks.txt",trace.ToString());

        var future=new GameObject("FutureStation",typeof(BunkerStation));
        var futureStation=future.GetComponent<BunkerStation>(); futureStation.enabled=false;
        typeof(BunkerStation).GetField("stationType",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(futureStation,BunkerStationType.CharacterSelection);
        future.transform.position=body.position;
        manager.Register(futureStation); yield return null; yield return null;
        var futureArrow=All<BunkerInteractionArrow>().Single(a=>a.Source==futureStation);
        Assert.That(futureArrow.IsVisible,Is.False);
        futureStation.enabled=true; yield return null; yield return null;
        Assert.That(futureArrow.IsVisible,Is.True,"Enabling an existing target requires no station-specific arrow logic.");
        Object.Destroy(future); Object.Destroy(futureArrow.gameObject); yield return null;

        // Nearby interfaces stay separate; selecting one must not leak through to another.
        Teleport(body,new Vector2(10.5f,14.5f)); yield return null; yield return null;
        var nearby=arrows.Where(a=>a.IsVisible && Vector2.Distance(a.ClickPosition,new Vector2(10.5f,14.5f))<8f).ToArray();
        Assert.That(nearby.Length,Is.GreaterThanOrEqualTo(3),"Player="+PlayerRuntimeReference.PlayerTransform.position+" Visible="+string.Join(",",nearby.Select(a=>a.name)));
        foreach(var a in nearby) foreach(var b in nearby.Where(b=>b!=a))
            Assert.That(a.GetComponent<Collider2D>().bounds.Intersects(b.GetComponent<Collider2D>().bounds),Is.False,a.name+" / "+b.name);
        Assert.That(Read<GameObject>(Object.FindFirstObjectByType<InteractionPromptUI>(),"promptPanel").activeSelf,Is.False);
        yield return Capture("02-nearby-objects",new Vector2(10.3f,15.3f),8f);
        var characters=arrows.Single(a=>a.Source is BunkerStation && Read<BunkerStationType>(a.Source,"stationType")==BunkerStationType.CharacterSelection);
        Teleport(body,(Vector2)characters.Source.transform.position+new Vector2(0,-1.5f)); yield return null; yield return null;
        yield return Capture("01-station-arrow",new Vector2(-14f,6f),6f);

        // The cat retains its original keyboard selection, localized prompt and pet action.
        var cat=Object.FindFirstObjectByType<CatPetInteractable>();
        var wander=cat.GetComponent<CatWanderController>();
        wander.enabled=false; // Freeze only the QA framing; the separate production cat test exercises wandering.
        typeof(CatWanderController).GetField("stateUntil",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(wander,float.MaxValue);
        var catBody=cat.GetComponent<Rigidbody2D>();
        Teleport(catBody,new Vector2(11.47f,-1.97f));
        Teleport(body,catBody.position+new Vector2(-1.7f,-.1f));
        var interactor=player.GetComponent<PlayerInteractor>();
        yield return CoreTestSupport.Await(()=>ReferenceEquals(interactor.GetCurrentInteractable(),cat));
        var prompt=Object.FindFirstObjectByType<InteractionPromptUI>();
        foreach(var language in new[]{GameLanguage.Russian,GameLanguage.English})
        {
            LocalizationService.Instance.SetLanguage(language); yield return null; yield return null;
            Assert.That(Read<TextMeshProUGUI>(prompt,"promptText").text,Is.EqualTo("[E] "+LocalizationService.Instance.Get("bunker.cat.pet")));
            Assert.That(Read<GameObject>(prompt,"promptPanel").activeSelf,Is.True);
            yield return Capture("03-cat-"+language,new Vector2(11.5f,-1.5f),5.6f);
        }
        interactor.GetCurrentInteractable().Interact();
        Assert.That(cat.CanInteract,Is.False);
        Assert.That(wander.State,Is.EqualTo(CatWanderController.CatState.Interacting));
        Assert.That(Object.FindObjectsByType<CatHeartFx>(FindObjectsSortMode.None).Length,Is.EqualTo(1));

        var football=All<FootballMinigame>().Single();
        Assert.That(football.CanStart,Is.False); football.StartGame(); Assert.That(football.IsRunning,Is.False);
        Assert.That(All<FootballMinigameHUD>().All(h=>!h.gameObject.activeInHierarchy),Is.True);
        Assert.That(All<FootballStartStation>().All(s=>!s.CanInteract),Is.True);
        var gate=All<BunkerGateVisual>().Single(g=>g.name=="Door_Football");
        Teleport(body,new Vector2(-17f,0f));
        yield return CoreTestSupport.Await(()=>gate.IsOpen);
        var barrier=GameObject.Find("FootballMaintenanceBarrier").GetComponent<BoxCollider2D>();
        Assert.That(barrier.enabled && !barrier.isTrigger,Is.True,"The field is closed with a visible physical wall.");
        var route=new[]{new Vector2(-17f,0f),new Vector2(-23f,0f),new Vector2(-32f,-5f),new Vector2(-32f,-14f)};
        var filter=new ContactFilter2D { useTriggers=false, useLayerMask=false };
        var hits=new RaycastHit2D[32];
        for(int i=1;i<route.Length;i++)
        {
            var delta=route[i]-route[i-1];
            int count=Physics2D.CircleCast(route[i-1],.3f,delta.normalized,filter,hits,delta.magnitude);
            Assert.That(hits.Take(count).Where(h=>h.rigidbody!=body).Select(h=>h.collider.name),Is.Empty,"Keep the shared route to the arcade open: "+route[i-1]+" -> "+route[i]);
        }
        Assert.That(Physics2D.CircleCast(new Vector2(-35f,0f),.3f,Vector2.up,filter,hits,4f),Is.GreaterThan(0));
        Assert.That(hits.Any(h=>h.collider==barrier),Is.True,"The playing field cannot be entered from its transit strip.");
        Teleport(body,new Vector2(-31f,-1.6f)); yield return null; yield return null;
        var sign=GameObject.Find("FootballComingSoon").GetComponentInChildren<TextMeshPro>();
        Assert.That(sign.GetComponentInParent<Canvas>(),Is.Null,"Physical world sign, not overlay UI.");
        foreach(var language in new[]{GameLanguage.Russian,GameLanguage.English})
        {
            LocalizationService.Instance.SetLanguage(language); yield return null; yield return null;
            Assert.That(sign.text,Is.EqualTo(language==GameLanguage.Russian?"В РАЗРАБОТКЕ":"COMING SOON"));
            sign.ForceMeshUpdate(); Assert.That(sign.isTextOverflowing,Is.False);
            yield return Capture("04-football-"+language,new Vector2(-31f,3.8f),10f);
        }
        File.AppendAllText(BunkerInteractionAuthoring.Output+"/runtime-checks.txt","Nearby targets separated; non-cat prompts hidden; cat prompt RU/EN + original pet action; disabled football entry/HUD; world sign RU/EN: PASS\nPhysical mouse/keyboard events: not simulated by this test.\n");
        yield return Capture("05-bunker-overview",new Vector2(0,-2f),32f);
    }

    private static IEnumerator Capture(string name,Vector2 center,float zoom)
    {
        var follow=Object.FindFirstObjectByType<CameraFollow>(); follow.IsPresentationSuspended=true;
        var camera=follow.ControlledCamera;
        camera.transform.position=new Vector3(center.x,center.y,camera.transform.position.z); camera.orthographicSize=zoom;
        Canvas.ForceUpdateCanvases();
        foreach(var text in All<TMP_Text>().Where(t=>t.gameObject.activeInHierarchy)) text.ForceMeshUpdate();
        yield return null; yield return null;
        string path=BunkerInteractionAuthoring.Output+"/"+name+".png";
        if(File.Exists(path)) File.Delete(path);
        ScreenCapture.CaptureScreenshot(path); yield return CoreTestSupport.Await(()=>File.Exists(path));
    }
}
#endif
