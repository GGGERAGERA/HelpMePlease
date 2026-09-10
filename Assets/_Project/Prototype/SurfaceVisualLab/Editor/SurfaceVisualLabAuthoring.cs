using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Subject42.Combat.OrbitalStation;
using Object = UnityEngine.Object;

public static partial class SurfaceVisualLabEditor
{
    public const string Root = "Assets/_Project/Prototype/SurfaceVisualLab";
    public const string ScenePath = Root + "/SurfaceVisualLab.unity";
    public const string Output = "Artifacts/GeneratedQA/SurfaceVisualLab";

    [MenuItem("Tools/Subject42/Surface Visual Lab/Build isolated scene")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before building.");
        if (Enumerable.Range(0, SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))
            throw new InvalidOperationException("Unsaved scene edits: save them before building SurfaceVisualLab.");
        Directory.CreateDirectory(Root + "/Art");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var lab = new GameObject("Surface Visual Lab — isolated art direction comparison").AddComponent<SurfaceVisualLab>();
        var camera = new GameObject("Gameplay Camera").AddComponent<Camera>();
        camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = 6.75f;
        camera.transform.position = new Vector3(0, 0, -10); camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = C(40,46,51); camera.allowMSAA = false; camera.allowHDR = false;
        camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
        camera.GetUniversalAdditionalCameraData().antialiasing = AntialiasingMode.None;
        camera.gameObject.AddComponent<AudioListener>(); lab.GameplayCamera = camera;
        var pixelCamera=camera.gameObject.AddComponent<PixelPerfectCamera>();
        pixelCamera.assetsPPU=32;pixelCamera.refResolutionX=768;pixelCamera.refResolutionY=432;
        pixelCamera.gridSnapping=PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;
        pixelCamera.cropFrame=PixelPerfectCamera.CropFrame.Windowbox;
        var pixels=new SerializedObject(pixelCamera);pixels.FindProperty("m_FilterMode").intValue=(int)PixelPerfectCamera.PixelPerfectFilterMode.Point;pixels.ApplyModifiedPropertiesWithoutUndo();
        var light = new GameObject("Neutral global light").AddComponent<Light2D>();
        light.lightType=Light2D.LightType.Global; light.intensity=1;
        lab.Character=AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Scriptable Objects/Characters/01_Gera.asset");
        lab.Player=(GameObject)PrefabUtility.InstantiatePrefab(OrbitalPresentationConfig.Active.GetPlayerPrefab(lab.Character.characterPrefab));
        lab.Player.name="Production player — Gera / ORBITAL";
        lab.Player.GetComponent<EnemySpawner>().enabled=false;
        PrefabUtility.RecordPrefabInstancePropertyModifications(lab.Player.GetComponent<EnemySpawner>());
        lab.Player.SetActive(false); PrefabUtility.RecordPrefabInstancePropertyModifications(lab.Player);
        var experience=new GameObject("Production experience").AddComponent<ExperienceManager>();
        experience.levelData=AssetDatabase.FindAssets("t:LevelData").Select(g=>AssetDatabase.LoadAssetAtPath<LevelData>(AssetDatabase.GUIDToAssetPath(g))).First();
        var enemyRoot=new GameObject("Production enemies — 24 comparison + 18 live stress targets");
        lab.EnemyRoot=enemyRoot.transform;
        string[] paths={"p_Enemy_classic","p_Enemy_default","p_Enemy_Shooter","p_Enemy_Bomber"};
        lab.EnemyPrefabs=paths.Select(p=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Enemies/"+p+".prefab")).ToArray();
        lab.Enemies=new GameObject[42];
        for(int i=0;i<lab.Enemies.Length;i++)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Enemies/"+paths[i%4]+".prefab");
            var enemy=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
            enemy.transform.SetParent(enemyRoot.transform,false);
            // Irregular concentric bands provide threats at every approach, with room for the station.
            float a=(i%12)*Mathf.PI*2/12 + (i/12)*.21f;
            float radius= i<12?4.1f:i<24?5.9f:i<36?7.5f:9f;
            enemy.transform.position=new Vector3(Mathf.Cos(a)*radius*1.43f,Mathf.Sin(a)*radius*.77f,0);
            enemy.SetActive(false); PrefabUtility.RecordPrefabInstancePropertyModifications(enemy); PrefabUtility.RecordPrefabInstancePropertyModifications(enemy.transform);
            lab.Enemies[i]=enemy;
        }
        lab.PickupGroup=new GameObject("Production XP gems");
        var health=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Enemies/p_Enemy_classic.prefab").GetComponent<EnemyHealth>();
        var xp=(GameObject)new SerializedObject(health).FindProperty("lootPrefab").objectReferenceValue;
        lab.XPPrefab=xp;
        lab.CratePrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Pickups/p_Case1.prefab");
        for(int i=0;i<20;i++)
        {
            var gem=(GameObject)PrefabUtility.InstantiatePrefab(xp);
            gem.transform.SetParent(lab.PickupGroup.transform,false);
            float a=i*2.39996f; float r=3.5f+(i%5)*.55f;
            gem.transform.position=new Vector3(Mathf.Cos(a)*r*1.55f,Mathf.Sin(a)*r*.8f,0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(gem.transform);
        }
        lab.Fixtures=new GameObject[2];
        for(int i=0;i<2;i++)
        {
            var crate=(GameObject)PrefabUtility.InstantiatePrefab(lab.CratePrefab);
            crate.transform.position=new Vector3(i==0?-7.5f:8, i==0?-3.4f:3.8f,0);
            PrefabUtility.RecordPrefabInstancePropertyModifications(crate.transform);lab.Fixtures[i]=crate;
        }
        var effects=new System.Collections.Generic.List<ParticleSystem>();
        foreach(var source in new[]{health.BloodHitPrefab,health.DeathFxPrefab}.Where(p=>p!=null))
        {
            var effect=(GameObject)PrefabUtility.InstantiatePrefab(source);
            effect.name="Production FX sample / "+source.name;
            effect.transform.position=new Vector3(effects.Count==0?-4.5f:4.5f,-2,0);
            foreach(var ps in effect.GetComponentsInChildren<ParticleSystem>(true))
            {var main=ps.main;main.playOnAwake=false;main.stopAction=ParticleSystemStopAction.None;}
            effects.Add(effect.GetComponent<ParticleSystem>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(effect.transform);
        }
        lab.ImpactSamples=effects.ToArray();
        BuildEnvironments(lab);
        // Boundary is outside the comparison viewport, the central floor stays open.
        foreach(var v in new[]{new Vector4(-36,0,1,56),new Vector4(36,0,1,56),new Vector4(0,-27,72,1),new Vector4(0,27,72,1)})
        { var wall=new GameObject("Lab extent");wall.transform.position=new Vector3(v.x,v.y);wall.AddComponent<BoxCollider2D>().size=new Vector2(v.z,v.w); }
        lab.SelectPreset(3);
        EditorSceneManager.SaveScene(scene,ScenePath);
    }
    static Color32 C(int r,int g,int b,int a=255)=>new Color32((byte)r,(byte)g,(byte)b,(byte)a);
    static void BuildEnvironments(SurfaceVisualLab lab)
    {
        var sprites=AssetDatabase.LoadAllAssetsAtPath(Root+"/Art/SurfaceComparisons.png").OfType<Sprite>().ToDictionary(s=>s.name);
        var allTiles=AssetDatabase.LoadAllAssetsAtPath(Root+"/Art/SurfaceComparisonsTiles.asset").OfType<Tile>().ToDictionary(t=>t.sprite.name);
        var rock=sprites["Shale"];
        var shrub=sprites["Dead growth"];
        var crack=sprites["Fracture"];
        var cable=sprites["Buried cable"];
        var stain=sprites["Dormant anomaly residue"];
        var tank=sprites["Collapsed containment vessel"];
        var slab=sprites["Broken concrete footing"];
        var pipe=sprites["Exposed service pipe"];
        var vent=AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/art/Sprites/Environment/BunkerElements1.png").OfType<Sprite>().Single(s=>s.name=="BunkerElements1_1");
        lab.Presets=new GameObject[4];
        for(int preset=0;preset<4;preset++)
        {
            if(preset==3)
            {
                lab.Presets[preset]=ColdAshSurfaceAuthoring.CreateField(new Rect(-36,-28,72,56));
                lab.Presets[preset].name="4 ART DIRECTED — COLD ASH";
                continue;
            }
            var root=new GameObject(new[]{"1 QUIET WASTELAND","2 BIOPUNK WASTELAND","3 RUINED FACILITY OUTSKIRTS","4 ART DIRECTED — COLD ASH"}[preset]);
            lab.Presets[preset]=root; var grid=root.AddComponent<Grid>(); grid.cellSize=new Vector3(2,2,1);
            var floor=new GameObject("Ground / repeating 64px tiles",typeof(Tilemap),typeof(TilemapRenderer));floor.transform.SetParent(root.transform,false);
            var map=floor.GetComponent<Tilemap>();floor.GetComponent<TilemapRenderer>().sortingLayerName="Background";floor.GetComponent<TilemapRenderer>().sortingOrder=-100;
            var tiles=Enumerable.Range(0,4).Select(variant=>allTiles[$"Ground {preset+1}-{variant}"]).ToArray();
            for(int y=-14;y<14;y++)for(int x=-18;x<18;x++)map.SetTile(new Vector3Int(x,y,0),tiles[((x*73856093)^(y*19349663))&3]);
            // One repeatable decoration motif per 24x18 sector; no bespoke handcrafted map.
            for(int sy=-1;sy<=1;sy++)for(int sx=-1;sx<=1;sx++)
            {
                Vector2 o=new Vector2(sx*24,sy*18);
                Place(root,rock,o+new Vector2(-9,2.6f));Place(root,rock,o+new Vector2(7,-4.5f),true);
                Place(root,shrub,o+new Vector2(-5.4f,-4.8f));Place(root,crack,o+new Vector2(5.8f,4.4f));
                if(preset==0) {Place(root,shrub,o+new Vector2(9.5f,4.6f)); continue;}
                if(preset==1)
                {
                    Place(root,stain,o+new Vector2(-8,3.5f));Place(root,tank,o+new Vector2(8,4.7f));Place(root,cable,o+new Vector2(7,3.1f));Place(root,cable,o+new Vector2(-8,-4.8f));
                    Place(root,vent,o+new Vector2(-9.8f,-4.7f)).color=C(130,141,146);
                }
                if(preset==2)
                {
                    Place(root,slab,o+new Vector2(-7.4f,4.4f));Place(root,slab,o+new Vector2(8,4.4f),true);Place(root,pipe,o+new Vector2(-9,-4.3f));Place(root,slab,o+new Vector2(5.7f,-5.4f));Place(root,cable,o+new Vector2(9,2.8f));
                }
            }
        }
        AssetDatabase.SaveAssets();
    }
    static SpriteRenderer Place(GameObject root,Sprite sprite,Vector2 position,bool flip=false)
    {
        var go=new GameObject(sprite.name);go.transform.SetParent(root.transform,false);go.transform.position=new Vector3(Mathf.Round(position.x*32)/32,Mathf.Round(position.y*32)/32,0);
        var r=go.AddComponent<SpriteRenderer>();r.sprite=sprite;r.flipX=flip;r.sortingLayerName="Background";r.sortingOrder=-50;
        return r;
    }
}
