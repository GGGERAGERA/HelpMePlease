using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

// Editor-only authoring of ordinary serialized Tilemaps and SpriteRenderers.
public static class ColdAshSurfaceAuthoring
{
    public const string ArtPath="Assets/_Project/art/Sprites/Environment/ColdAsh";
    public const string PrefabPath=ArtPath+"/ColdAshSurface.prefab";
    const string MvpPath="Assets/_Project/Scenes/MainBuild/MVP.unity";

    public static GameObject CreateField(Rect area)
    {
        var root=new GameObject("Cold Ash Ground");
        root.AddComponent<Grid>().cellSize=new Vector3(2,2,1);
        var floor=new GameObject("Ground",typeof(Tilemap),typeof(TilemapRenderer));
        floor.transform.SetParent(root.transform,false);
        var map=floor.GetComponent<Tilemap>();
        var renderer=floor.GetComponent<TilemapRenderer>();
        renderer.sortingLayerName="Background";renderer.sortingOrder=-110;
        var allTiles=AssetDatabase.LoadAllAssetsAtPath(ArtPath+"/ColdAshTiles.asset").OfType<Tile>().ToDictionary(t=>t.sprite.name);
        var tiles=Enumerable.Range(0,4).Select(i=>allTiles[$"Ground 4-{i}"]).ToArray();
        if(tiles.Any(t=>t==null))throw new InvalidOperationException("Approved Cold Ash tiles are missing.");
        var rng=new System.Random(42017);
        for(int y=Mathf.FloorToInt(area.yMin/2);y<Mathf.CeilToInt(area.yMax/2);y++)
        for(int x=Mathf.FloorToInt(area.xMin/2);x<Mathf.CeilToInt(area.xMax/2);x++)
            map.SetTile(new Vector3Int(x,y),tiles[rng.Next(4)]);
        map.CompressBounds();
        var decor=new GameObject("Micro decor — no collision");decor.transform.SetParent(root.transform,false);
        var sprites=LoadMicroSprites();
        var points=new List<Vector2>();
        // Globally seeded rejection sampling: roughly 1–3 small marks per camera view.
        int count=Mathf.RoundToInt(area.width*area.height/160f);
        for(int attempts=0;attempts<count*30&&points.Count<count;attempts++)
        {
            var p=new Vector2(Mathf.Round((area.xMin+(float)rng.NextDouble()*area.width)*32)/32,
                Mathf.Round((area.yMin+(float)rng.NextDouble()*area.height)*32)/32);
            if(p.sqrMagnitude<36||points.Any(q=>(q-p).sqrMagnitude<81))continue;
            points.Add(p);
            var go=new GameObject(sprites[(points.Count-1)%sprites.Length].name);go.transform.SetParent(decor.transform,false);go.transform.localPosition=p;
            var r=go.AddComponent<SpriteRenderer>();r.sprite=sprites[(points.Count-1)%sprites.Length];r.flipX=rng.Next(2)==0;
            r.sortingLayerName="Background";r.sortingOrder=-109;
        }
        return root;
    }

    static Sprite[] LoadMicroSprites()
    {
        var sprites=AssetDatabase.LoadAllAssetsAtPath(ArtPath+"/ColdAsh.png").OfType<Sprite>().ToDictionary(s=>s.name);
        // The approved scenes use an eight-step sequence with the same scuff in its final two slots.
        // Reuse that Sprite reference twice to preserve placement; there are only seven unique micro sprites.
        return new[]{"Dry twig","Hairline crack","Pebble A","Pebble B","Dry grass A","Dry grass B","Ash scuff A","Ash scuff A"}
            .Select(name=>sprites[name]).ToArray();
    }

    [MenuItem("Tools/Subject42/Surface/Apply Cold Ash to MVP")]
    public static void ApplyToMvp()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))
            throw new InvalidOperationException("Stop Play Mode and save scene edits before authoring.");
        var scene=EditorSceneManager.OpenScene(MvpPath);
        var existing=scene.GetRootGameObjects().FirstOrDefault(g=>PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g)==PrefabPath);
        var old=scene.GetRootGameObjects().Where(g=>g.name.StartsWith("p_EnvirMine2",StringComparison.Ordinal)).ToArray();
        var allowed=new[]{typeof(Transform),typeof(Grid),typeof(Tilemap),typeof(TilemapRenderer),typeof(SpriteRenderer),typeof(BoxCollider2D),typeof(UnityEngine.Rendering.SortingGroup)};
        if(old.SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Any(c=>c==null||!allowed.Contains(c.GetType())))
            throw new InvalidOperationException("Environment contains an unexpected component: "+string.Join(",",old.SelectMany(g=>g.GetComponentsInChildren<Component>(true)).Where(c=>c==null||!allowed.Contains(c.GetType())).Select(c=>c?c.GetType().Name:"MISSING").Distinct()));
        var protectedComponents=scene.GetRootGameObjects().Except(old).SelectMany(g=>g.GetComponentsInChildren<MonoBehaviour>(true)).Where(c=>c).ToArray();
        var area=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<GameplayAreaService>()).Single();
        var bounds=area.SpawnArea.bounds;bounds.Expand(48);
        var surface=CreateField(new Rect(bounds.min.x,bounds.min.y,bounds.size.x,bounds.size.y));
        string materialPath=ArtPath+"/ColdAshLit.mat";
        var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if(!material)throw new InvalidOperationException("Approved Cold Ash material is missing.");
        // Keep production darkness and local light response; compensate only this palette for its 0.33 baseline light.
        // Reuse the approved material without creating or retuning it.
        foreach(var renderer in surface.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=material;
        PrefabUtility.SaveAsPrefabAssetAndConnect(surface,PrefabPath,InteractionMode.AutomatedAction);
        foreach(var go in old)Object.DestroyImmediate(go);
        if(existing)Object.DestroyImmediate(existing);
        if(protectedComponents.Any(c=>!c))throw new InvalidOperationException("Functional scene component removed.");
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
    }
}
