#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Subject42.Combat.OrbitalStation;
using Subject42.DebugLabs.EliteOrbit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object=UnityEngine.Object;

public static class EliteOrbitalLabAuthoring
{
    private const string AssetsRoot="Assets/_Project/Dev/Labs/OrbitalLab/EliteOrbit";
    [MenuItem("Tools/Subject42/Dev/OrbitalLab/Open EliteOrbitalLab")]
    public static void Open()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Create(); EditorSceneManager.OpenScene(EliteOrbitalLab.ScenePath);
    }
    public static void Create()
    {
        var localization=AssetDatabase.LoadAssetAtPath<LocalizationService>("Assets/_Project/prefabs/Bootstrap/LocalizationService.prefab");
        if (File.Exists(EliteOrbitalLab.ScenePath))
        {
            var existingScene=EditorSceneManager.OpenScene(EliteOrbitalLab.ScenePath);
            var existingLab=Object.FindFirstObjectByType<EliteOrbitalLab>();
            if (existingLab.LocalizationPrefab==null)
            { existingLab.LocalizationPrefab=localization; EditorSceneManager.MarkSceneDirty(existingScene); EditorSceneManager.SaveScene(existingScene); }
            return;
        }
        Directory.CreateDirectory(AssetsRoot); AssetDatabase.Refresh();
        if (!AssetDatabase.CopyAsset(EnemyOrbitalLabController.ScenePath,EliteOrbitalLab.ScenePath)) throw new Exception("Cannot copy EnemyOrbitalLab");
        var scene=EditorSceneManager.OpenScene(EliteOrbitalLab.ScenePath);
        var source=Object.FindFirstObjectByType<EnemyOrbitalLabController>();
        var character=source.Character;
        var ordinary=source.EnemyPrefabs.First(p=>p.GetComponent<EnemyChaseMovement>()!=null);
        var cameraRig=source.CameraRig; Object.DestroyImmediate(source.gameObject);
        foreach (var root in scene.GetRootGameObjects()) if (root.GetComponent<ProductionSceneComposition>()!=null) Object.DestroyImmediate(root);
        var lab=new GameObject("EliteOrbitalLab / scene-owned encounter").AddComponent<EliteOrbitalLab>();
        lab.LocalizationPrefab=localization;
        lab.Character=character; lab.OrdinaryPrefab=ordinary; lab.CameraRig=cameraRig; lab.ArenaCamera=cameraRig.ControlledCamera;
        lab.Presentation=AssetDatabase.LoadAssetAtPath<OrbitalPresentationConfig>("Assets/_Project/Data/Orbital/OrbitalPresentationConfig.asset");
        lab.ArenaCamera.rect=new Rect(.31f,0,.69f,1); lab.ArenaCamera.orthographicSize=7.5f;
        lab.ArenaCamera.backgroundColor=new Color(.055f,.07f,.095f); lab.ArenaCamera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
        lab.BlockClip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/AudioEffects/packs/kenney_impact-sounds/impactMetal_heavy_000.mp3");
        lab.HitClip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/AudioEffects/packs/kenney_impact-sounds/impactMetal_medium_000.mp3");
        lab.NodeBreakClip=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/AudioEffects/packs/kenney_impact-sounds/impactGlass_medium_000.mp3");
        var player=(GameObject)PrefabUtility.InstantiatePrefab(character.ProductionPrefab,scene);
        PrefabUtility.UnpackPrefabInstance(player,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        player.name="Original player / isolated inactive template"; player.SetActive(false); player.transform.SetParent(lab.transform,false);
        foreach (var station in player.GetComponentsInChildren<OrbitalStationView>(true)) if (station.gameObject!=player) Object.DestroyImmediate(station.gameObject);
        foreach (var behaviour in player.GetComponentsInChildren<MonoBehaviour>(true))
            if (!(behaviour is CharacterMovement2D) && !(behaviour is PlayerHealth) && !(behaviour is PlayerWhiteFlash) &&
                !(behaviour is PlayerHitSound) && !(behaviour is PlayerHealthPresentation) && !(behaviour is Light2D)) Object.DestroyImmediate(behaviour);
        foreach (var collider in player.GetComponentsInChildren<Collider2D>(true)) if (collider.gameObject!=player) Object.DestroyImmediate(collider);
        lab.PlayerTemplate=player;
        lab.Presets=new[]
        {
            Preset("ShieldBearer","Щитоносец","Пластина прикрывает один сектор. Меняйте сторону захода или атакуйте изнутри радиуса.",1.9f,42,.36f,
                new[] { new EliteSlot { MountIndex=0,Kind=EliteModuleKind.ArmorPlate } }),
            Preset("Blades","Лезвийный","Два движущихся лезвия. Между ними есть проходы; тело получает обычный урон.",2.25f,52,.28f,
                new[] { new EliteSlot { MountIndex=0,Kind=EliteModuleKind.ContactBlade },new EliteSlot { MountIndex=3,Kind=EliteModuleKind.ContactBlade } }),
            Preset("Nodes","Узловой","Каждый узел снижает урон телу на 18%. Можно ломать узлы или атаковать тело: полной неуязвимости нет.",1.65f,40,.4f,
                new[] { new EliteSlot { MountIndex=0,Kind=EliteModuleKind.ProtectionNode },new EliteSlot { MountIndex=2,Kind=EliteModuleKind.ProtectionNode },new EliteSlot { MountIndex=4,Kind=EliteModuleKind.ProtectionNode } })
        };
        var elite=(GameObject)PrefabUtility.InstantiatePrefab(ordinary,scene);
        PrefabUtility.UnpackPrefabInstance(elite,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        elite.name="Orbital Elite / ordinary enemy base";
        var movement=elite.GetComponent<EnemyChaseMovement>();
        var serialized=new SerializedObject(movement);
        var graphic=serialized.FindProperty("visualRoot").objectReferenceValue as Transform;
        if (graphic!=null) graphic.localScale*=1.55f;
        foreach (var collider in elite.GetComponents<Collider2D>())
        {
            if (collider is CircleCollider2D circle) circle.radius*=1.35f;
            if (collider is CapsuleCollider2D capsule) capsule.size*=1.35f;
            if (collider is BoxCollider2D box) box.size*=1.35f;
        }
        foreach (var group in elite.GetComponentsInChildren<SortingGroup>()) group.sortingOrder=45;
        elite.GetComponent<EnemyHealth>().maxHealth=160;
        elite.AddComponent<CombatFeelTestDummy>(); elite.AddComponent<EliteOrbitEnemy>();
        elite.SetActive(false);
        lab.ElitePrefab=PrefabUtility.SaveAsPrefabAsset(elite,AssetsRoot+"/OrbitalElite.prefab"); Object.DestroyImmediate(elite);
        EditorSceneManager.SaveScene(scene,EliteOrbitalLab.ScenePath); AssetDatabase.SaveAssets();
    }
    private static EliteOrbitPreset Preset(string id,string name,string hint,float radius,float speed,float move,EliteSlot[] slots)
    {
        var data=ScriptableObject.CreateInstance<EliteOrbitPreset>(); data.DisplayName=name; data.Hint=hint; data.Radius=radius;
        data.RotationSpeed=speed; data.MovementMultiplier=move; data.MountCount=6; data.Slots=slots;
        data.BodyHealth=id=="Nodes" ? 180 : 160;
        AssetDatabase.CreateAsset(data,AssetsRoot+"/"+id+".asset"); return data;
    }
}
#endif
