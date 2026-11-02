using Subject42.Combat.Telekinetic;
#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Subject42.Combat.OrbitalStation;
using Subject42.DebugLabs.Constructor;
using Subject42.DebugLabs.Telekinetic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

public static partial class TelekineticConstructorAuthoring
{
    [MenuItem("Tools/Subject42/Dev/Telekinetic Constructor/Open Lab")]
    public static void Open()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Create(); EditorSceneManager.OpenScene(TelekineticConstructorLab.ScenePath); ConfigureStructureView();
    }
    public static void ConfigureStructureView()
    {
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.path!=TelekineticConstructorLab.ScenePath) throw new InvalidOperationException("Open TelekineticConstructorLab before authoring its visual rig");
        var lab=scene.GetRootGameObjects().Select(g=>g.GetComponent<TelekineticConstructorLab>()).First(l=>l!=null);
        const string label="Telekinetic Constructor Lab V2 / isolated experiment";
        bool changed=lab.gameObject.name!=label;
        lab.gameObject.name=label;
        if (lab.StructureView==null)
        {
            lab.StructureView=lab.GetComponent<TelekineticStructureView>();
            if (lab.StructureView==null) lab.StructureView=lab.gameObject.AddComponent<TelekineticStructureView>();
            changed=true;
        }
        changed|=ConfigureIcons(lab);
        if (!changed) return;
        EditorUtility.SetDirty(lab); EditorSceneManager.SaveScene(scene);
    }
    public static void Create()
    {
        if (File.Exists(TelekineticConstructorLab.ScenePath)) return;
        // Copy the authored arena and inactive production-character template; never run either source lab.
        if (!AssetDatabase.CopyAsset(OrbitalConstructorLab.ScenePath,TelekineticConstructorLab.ScenePath))
            throw new InvalidOperationException("Cannot copy training arena");
        var scene=EditorSceneManager.OpenScene(TelekineticConstructorLab.ScenePath);
        var source=Object.FindFirstObjectByType<OrbitalConstructorLab>();
        var lab=new GameObject("Telekinetic Constructor Lab V2 / isolated experiment").AddComponent<TelekineticConstructorLab>();
        lab.PlayerTemplate=source.PlayerTemplate; lab.PlayerTemplate.transform.SetParent(lab.transform,false);
        lab.EnemyPrefab=source.MeleePrefab; lab.ArenaCamera=source.ArenaCamera;
        lab.SawClip=source.SawHitClip;
        lab.ShotClip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/AudioEffects/SFX/Game/Pistol_shot_SFX.wav");
        Object.DestroyImmediate(source.gameObject);
        foreach (var root in scene.GetRootGameObjects())
            if (root.name=="Training floor grid") Object.DestroyImmediate(root);
        var presentation=AssetDatabase.LoadAssetAtPath<OrbitalPresentationConfig>("Assets/_Project/Data/Orbital/OrbitalPresentationConfig.asset");
        lab.Presentation=presentation; lab.SpriteMaterial=presentation.VisualMaterial; lab.ModuleSprite=presentation.PixelSprite;
        lab.Pistol=presentation.GetWeaponSettings(OrbitalModuleKind.Pistol);
        lab.Shotgun=presentation.GetWeaponSettings(OrbitalModuleKind.Shotgun);
        lab.Saw=presentation.GetWeaponSettings(OrbitalModuleKind.Saw);
        lab.Pistol.AimLeadSeconds=lab.Shotgun.AimLeadSeconds=0;
        lab.Pistol.MaxAimCorrectionDegrees=lab.Shotgun.MaxAimCorrectionDegrees=0;
        lab.PistolVisual=Template(OrbitalModuleKind.Pistol);
        lab.ShotgunVisual=Template(OrbitalModuleKind.Shotgun);
        lab.SawVisual=Template(OrbitalModuleKind.Saw);
        GameObject Template(OrbitalModuleKind kind)
        {
            var visual=(GameObject)PrefabUtility.InstantiatePrefab(presentation.GetPrefab(kind),scene);
            visual.transform.SetParent(lab.transform,false); visual.name=kind+" / authored inactive visual";
            visual.transform.localScale=Vector3.one*presentation.GetScale(kind);
            visual.SetActive(false); return visual;
        }
        lab.ArenaCamera.rect=new Rect(0,0,1,1); lab.ArenaCamera.orthographicSize=9;
        lab.ArenaCamera.transform.position=new Vector3(0,0,-10);
        lab.ArenaCamera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        lab.ArenaCamera.backgroundColor=new Color(.045f,.065f,.085f);
        lab.StructureView=lab.gameObject.AddComponent<TelekineticStructureView>();
        EditorSceneManager.SaveScene(scene,TelekineticConstructorLab.ScenePath); AssetDatabase.SaveAssets();
    }
}
#endif
