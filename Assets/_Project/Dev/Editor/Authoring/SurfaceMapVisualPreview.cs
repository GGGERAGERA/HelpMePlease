#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Isolated presentation capture. Never opens/saves gameplay scenes or uses player storage.</summary>
[InitializeOnLoad]
public static class SurfaceMapVisualPreview
{
    const string Output="Artifacts/GeneratedQA/SurfaceMap";
    const string Prefab="Assets/_Project/prefabs/UI/SurfaceMap/PF_SurfaceMap.prefab";
    static SurfaceMapVisualPreview() { EditorApplication.update+=Poll; }
    static void Poll()
    {
        string request=Output+"/visual-preview.request";
        if(!File.Exists(request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode) return;
        try { File.Delete(request); } catch(IOException) { return; }
        try { Capture(); File.WriteAllText(Output+"/visual-preview-result.txt","SUCCESS: isolated Unity camera render; no logic tests or user storage writes."); }
        catch(Exception e) { File.WriteAllText(Output+"/visual-preview-result.txt",e.ToString()); Debug.LogException(e); }
    }
    [MenuItem("Tools/Subject42/Surface Map/Capture Presentation Preview")]
    public static void Capture()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) return;
        Directory.CreateDirectory(Output);
        var scene=EditorSceneManager.NewPreviewScene();
        RenderTexture rt=null; Texture2D image=null; var previousLocalization=LocalizationService.Instance;
        try
        {
            var camObject=new GameObject("Surface map preview camera",typeof(Camera)); SceneManager.MoveGameObjectToScene(camObject,scene);
            var cam=camObject.GetComponent<Camera>(); cam.scene=scene; cam.transform.position=new Vector3(0,0,-10);
            cam.clearFlags=CameraClearFlags.SolidColor; cam.backgroundColor=new Color(.012f,.021f,.034f);
            cam.orthographic=true; cam.orthographicSize=540; cam.nearClipPlane=.1f; cam.farClipPlane=100;
            rt=new RenderTexture(1920,1080,24); cam.targetTexture=rt;
            var canvasObject=new GameObject("Surface map preview canvas",typeof(RectTransform),typeof(Canvas)); SceneManager.MoveGameObjectToScene(canvasObject,scene);
            var canvas=canvasObject.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=cam; canvas.planeDistance=5;
            var scaler=canvasObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1920,1080); scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight=.5f;
            scaler.enabled=false; scaler.enabled=true;
            var instance=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),canvasObject.transform);
            var view=instance.GetComponent<SurfaceMapView>();
            typeof(SurfaceMapView).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(view,null);
            var starter=canvasObject.AddComponent<BunkerRunStarter>();
            var localization=canvasObject.AddComponent<LocalizationService>();
            typeof(LocalizationService).GetField("table",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(localization,AssetDatabase.LoadAssetAtPath<LocalizationTable>("Assets/_Project/Data/Localization/LocalizationTable.asset"));
            typeof(LocalizationService).GetProperty("CurrentLanguage").SetValue(localization,GameLanguage.Russian);
            typeof(LocalizationService).GetProperty("Instance").SetValue(null,localization);
            var definition=AssetDatabase.LoadAssetAtPath<SurfaceMapDefinition>(SurfaceMapProductionAuthoring.MapPath);
            var service=new SurfaceMapService(definition,new PreviewStorage());
            Canvas.ForceUpdateCanvases();
            service.TrySelect("C1"); view.Show(service,starter,null,null);
            Render("surface-map-visual.png");
            var navigation=(SurfaceMapNavigation)new SerializedObject(view).FindProperty("navigation").objectReferenceValue;
            navigation.Zoom(1,Vector2.zero); navigation.Pan(new Vector2(-220,100));
            typeof(SurfaceMapView).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(view,null);
            Render("surface-map-panned.png"); navigation.ResetView();
            view.Inspect("C2"); Render("surface-map-locked.png");
            // Disposable completed branches expose the later icons without touching player saves.
            foreach(string first in new[]{"A1","C1","D1"})
            {
                service.TrySelect(first); service.TryBuildRunConfig(out var config);
                service.CompleteVictory(config,RunEndReason.Victory,true);
            }
            view.Inspect("C2"); Render("surface-map-explored.png");
            // Only disposable memory state is changed: preview reconnaissance never touches player progress.
            var fixture=new SurfaceMapService(definition,new PreviewStorage(true));
            fixture.TrySelect("D2");
            view.Show(fixture,starter,null,null); Render("surface-map-unknown.png");
            view.ScanInspected(); Render("surface-map-scanned.png");
            // Same canvas reference and aspect ratio as the bunker, at a smaller output resolution.
            cam.targetTexture=null; rt.Release(); UnityEngine.Object.DestroyImmediate(rt);
            rt=new RenderTexture(1280,720,24); cam.targetTexture=rt;
            scaler.enabled=false; scaler.enabled=true; Canvas.ForceUpdateCanvases();
            ((SurfaceMapNavigation)new SerializedObject(view).FindProperty("navigation").objectReferenceValue).ResetView();
            view.Inspect("C1"); Render("surface-map-720p.png");
            void Render(string name)
            {
                foreach(var fog in instance.GetComponentsInChildren<SurfaceMapFog>(true))fog.SetVerticesDirty();
                Canvas.ForceUpdateCanvases();
                foreach(var text in instance.GetComponentsInChildren<TMP_Text>(true)) text.ForceMeshUpdate(true,true);
                Canvas.ForceUpdateCanvases(); cam.Render();
                var previous=RenderTexture.active;
                try
                {
                    RenderTexture.active=rt; image=new Texture2D(rt.width,rt.height,TextureFormat.RGB24,false);
                    image.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0); image.Apply(); File.WriteAllBytes(Output+"/"+name,image.EncodeToPNG());
                }
                finally { RenderTexture.active=previous; if(image!=null) UnityEngine.Object.DestroyImmediate(image); image=null; }
            }
        }
        finally { typeof(LocalizationService).GetProperty("Instance").SetValue(null,previousLocalization); EditorSceneManager.ClosePreviewScene(scene); if(rt!=null) { rt.Release(); UnityEngine.Object.DestroyImmediate(rt); } }
    }
    sealed class PreviewStorage:ISurfaceMapStorage
    {
        readonly bool completed;
        public PreviewStorage(bool completed=false) { this.completed=completed; }
        public string Load(string id) => completed&&!id.EndsWith(":content")?"{\"unlocked\":[\"A1\",\"C1\",\"D1\",\"D2\"],\"completed\":[\"D1\"]}":null;
        public void Save(string id,string json) { }
    }
}
#endif


