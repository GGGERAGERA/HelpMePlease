#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Subject42.DebugLabs.Constructor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class OrbitalConstructorAuthoring
{
    public static void ConfigureTrainingScene()
    {
        var lab=Object.FindFirstObjectByType<OrbitalConstructorLab>();
        lab.SawHitClip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/AudioEffects/packs/kenney_impact-sounds/impactMetal_medium_000.mp3");
        if (lab.SawHitClip==null) throw new InvalidOperationException("Existing metal impact sound is missing");
        EditorUtility.SetDirty(lab); EditorSceneManager.SaveScene(lab.gameObject.scene);
    }
    [MenuItem("Tools/Subject42/Dev/OrbitalLab/Open OrbitalConstructorLab")]
    public static void Open()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Create(); EditorSceneManager.OpenScene(OrbitalConstructorLab.ScenePath);
    }

    public static void Create()
    {
        if (File.Exists(OrbitalConstructorLab.ScenePath)) return;
        // Reuse the actual training arena, light, walls and camera without touching its source.
        if (!AssetDatabase.CopyAsset(EnemyOrbitalLabController.ScenePath,OrbitalConstructorLab.ScenePath))
            throw new InvalidOperationException("Cannot copy EnemyOrbitalLab");
        var scene=EditorSceneManager.OpenScene(OrbitalConstructorLab.ScenePath);
        var source=Object.FindFirstObjectByType<EnemyOrbitalLabController>();
        var prefabs=source.EnemyPrefabs;
        var shooter=prefabs.First(p=>p.GetComponent<EnemyShooterMovement>()?.ProjectilePrefab?.GetComponent<EnemyProjectile>()!=null);
        var melee=prefabs.First(p=>p.GetComponent<EnemyChaseMovement>()!=null);
        var camera=source.CameraRig.ControlledCamera;
        Object.DestroyImmediate(source.CameraRig);
        Object.DestroyImmediate(source.gameObject);
        foreach (var root in scene.GetRootGameObjects())
            if (root.GetComponent<ProductionSceneComposition>()!=null) Object.DestroyImmediate(root);
        var lab=new GameObject("Orbital Constructor / isolated training").AddComponent<OrbitalConstructorLab>();
        lab.MeleePrefab=melee; lab.ShooterPrefab=shooter; lab.ArenaCamera=camera;
        lab.SawHitClip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/AudioEffects/packs/kenney_impact-sounds/impactMetal_medium_000.mp3");
        camera.orthographicSize=8.5f; camera.rect=new Rect(.31f,0,.69f,1);
        camera.backgroundColor=new Color(.065f,.085f,.11f); camera.transform.position=new Vector3(0,0,-10);
        camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;

        var character=AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Data/Characters/03_Vika.asset");
        var template=(GameObject)PrefabUtility.InstantiatePrefab(character.ProductionPrefab,scene);
        PrefabUtility.UnpackPrefabInstance(template,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        template.name="Vika / isolated inactive template";
        template.SetActive(false); template.transform.SetParent(lab.transform,false);
        // Keep movement, health and authored hit presentation; remove spawners, station, pickups and services BEFORE Awake.
        foreach (var behaviour in template.GetComponentsInChildren<MonoBehaviour>(true))
            if (!(behaviour is CharacterMovement2D) && !(behaviour is PlayerHealth) && !(behaviour is PlayerWhiteFlash) &&
                !(behaviour is PlayerHitSound) && !(behaviour is PlayerFeedbackPresentation) && !(behaviour is Light2D))
                Object.DestroyImmediate(behaviour);
        foreach (var collider in template.GetComponentsInChildren<Collider2D>(true))
            if (collider.gameObject!=template) Object.DestroyImmediate(collider);
        foreach (var group in template.GetComponentsInChildren<SortingGroup>(true)) group.sortingOrder=40;
        template.GetComponent<PlayerHealth>().maxHealth=150;
        lab.PlayerTemplate=template;

        var grid=new GameObject("Training floor grid").transform;
        string materialPath="Assets/_Project/Dev/Labs/OrbitalLab/ConstructorGrid.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material==null)
        {
            material=new Material(Shader.Find("Sprites/Default")); AssetDatabase.CreateAsset(material,materialPath);
        }
        for (int i=-30;i<=30;i+=2)
        {
            AddLine(grid,material,new Vector2(i,-30),new Vector2(i,30));
            AddLine(grid,material,new Vector2(-30,i),new Vector2(30,i));
        }
        EditorSceneManager.SaveScene(scene,OrbitalConstructorLab.ScenePath);
        AssetDatabase.SaveAssets();
    }
    private static void AddLine(Transform root,Material material,Vector2 a,Vector2 b)
    {
        var line=new GameObject("Floor seam").AddComponent<LineRenderer>();
        line.transform.SetParent(root,false); line.sharedMaterial=material; line.widthMultiplier=.018f;
        line.startColor=line.endColor=new Color(.15f,.2f,.25f); line.sortingLayerName="Background";
        line.positionCount=2; line.SetPosition(0,a); line.SetPosition(1,b);
    }
}
#endif
