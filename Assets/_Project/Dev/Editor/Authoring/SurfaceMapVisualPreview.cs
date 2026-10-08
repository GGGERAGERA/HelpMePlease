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
        // Only font/layout defaults on the map prefab. Runtime rendering also covers existing scene instances.
        var asset=PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var pixel=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Fonts/Subject42 Intro SDF.asset");
            var body=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Project/Fonts/Subject42 UI SDF.asset");
            foreach(var text in asset.GetComponentsInChildren<TMP_Text>(true))
            {
                text.font=text.name=="Details"?body:pixel;
                text.fontSharedMaterial=text.font.material;
            }
            asset.GetComponent<RectTransform>().sizeDelta=new Vector2(1160,700);
            PrefabUtility.SaveAsPrefabAsset(asset,Prefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(asset); }
        var scene=EditorSceneManager.NewPreviewScene();
        RenderTexture rt=null; Texture2D image=null; var previousLocalization=LocalizationService.Instance;
        try
        {
            var camObject=new GameObject("Surface map preview camera",typeof(Camera)); SceneManager.MoveGameObjectToScene(camObject,scene);
            var cam=camObject.GetComponent<Camera>(); cam.scene=scene; cam.transform.position=new Vector3(0,0,-10);
            cam.clearFlags=CameraClearFlags.SolidColor; cam.backgroundColor=new Color(.012f,.021f,.034f);
            cam.orthographic=true; cam.orthographicSize=400; cam.nearClipPlane=.1f; cam.farClipPlane=100;
            rt=new RenderTexture(1600,1000,24); cam.targetTexture=rt;
            var canvasObject=new GameObject("Surface map preview canvas",typeof(RectTransform),typeof(Canvas)); SceneManager.MoveGameObjectToScene(canvasObject,scene);
            var canvas=canvasObject.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=cam; canvas.planeDistance=5;
            var scaler=canvasObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1280,800); scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight; scaler.matchWidthOrHeight=.5f;
            var instance=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),canvasObject.transform);
            var view=instance.GetComponent<SurfaceMapView>();
            typeof(SurfaceMapView).GetMethod("Awake",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(view,null);
            var starter=canvasObject.AddComponent<BunkerRunStarter>();
            if(previousLocalization==null)
            {
                var localization=canvasObject.AddComponent<LocalizationService>();
                typeof(LocalizationService).GetField("table",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(localization,AssetDatabase.LoadAssetAtPath<LocalizationTable>("Assets/_Project/Data/Localization/LocalizationTable.asset"));
                typeof(LocalizationService).GetProperty("Instance").SetValue(null,localization);
            }
            var definition=AssetDatabase.LoadAssetAtPath<SurfaceMapDefinition>(SurfaceMapProductionAuthoring.MapPath);
            var service=new SurfaceMapService(definition,new PreviewStorage());
            service.TrySelect("C1"); view.Show(service,starter,null,null);
            Render("surface-map-visual.png");
            view.Inspect("C2"); Render("surface-map-locked.png");
            service.TrySelect("D1"); Render("surface-map-unknown.png");
            // Render-only fixture exercises the completed/mission presentation using disposable in-memory state.
            var fixture=new SurfaceMapService(definition,new PreviewStorage(true));
            var mission=AssetDatabase.LoadAssetAtPath<SurfaceSectorContent>("Assets/_Project/Data/SurfaceMap/Content_SignalMission.asset");
            fixture.Content.AssignMission("visual-preview","D2",mission); fixture.TrySelect("A2");
            view.Show(fixture,starter,null,null); Render("surface-map-states.png");
            void Render(string name)
            {
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
        public string Load(string id) => completed&&!id.EndsWith(":content")?"{\"unlocked\":[\"A1\",\"C1\",\"D1\",\"A2\"],\"completed\":[\"A1\"]}":null;
        public void Save(string id,string json) { }
    }
}
#endif


