using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.Rendering.Universal;

// Editor-only prototype snapshots. No runtime lookups or scene bootstrap changes.
public static class BunkerSimplePrototype
{
    const string Main = "Assets/_Project/Scenes/MainBuild/MainMenu.unity";
    const string Root = "Assets/_Project/Dev/Labs/BunkerSimple/";
    const string Art = "Assets/_Project/art/BunkerSimple/";
    const string QA = "Artifacts/GeneratedQA/BunkerSimple/";
    static Material flat;
    static Transform[] All(Scene s) => s.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Transform>(true)).ToArray();
    static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;
    static T[] Components<T>(Scene s) where T : Component => s.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
    static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + name + ".png");
    static bool Environment(Transform t) => t.GetComponentInParent<Canvas>() == null &&
        (t.GetComponentInParent<CharacterMovement2D>() == null || PathOf(t).IndexOf("capsule", StringComparison.OrdinalIgnoreCase) >= 0) &&
        !PathOf(t).StartsWith("MinigameArena") && !PathOf(t).Contains("EnemyTurret") &&
        !PathOf(t).StartsWith("SlotMachine");

    [MenuItem("Tools/Subject42/Bunker Prototype/Build SIMPLE from OLD")]
    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        if (File.Exists(Root + "SIMPLE/MainMenu.unity")) throw new InvalidOperationException("SIMPLE already exists. Edit the authored snapshot instead of rebuilding over it.");
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Directory.CreateDirectory(QA);
        // OLD is an immutable source snapshot, created before touching any authored visuals.
        if (!File.Exists(Root + "OLD/MainMenu.unity")) throw new InvalidOperationException("Create OLD backup first.");
        AssetDatabase.Refresh();
        foreach (var file in Directory.GetFiles(Art, "*.png"))
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(file.Replace('\\', '/'));
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 16;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }
        flat = AssetDatabase.LoadAssetAtPath<Material>(Art + "SimpleUnlit.mat");
        if (flat == null)
        {
            flat = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            AssetDatabase.CreateAsset(flat, Art + "SimpleUnlit.mat");
        }
        var s = EditorSceneManager.OpenScene(Root + "OLD/MainMenu.unity");
        EditorSceneManager.SaveScene(s, Root + "SIMPLE/MainMenu.unity");
        var original = All(s);
        var gameplay = original.SelectMany(t => t.GetComponents<Component>()).Where(c => c is Collider2D ||
            c is BunkerStation || c is BunkerRoomAccess || c is BunkerGateVisual || c is BunkerInteractableCollider).ToArray();
        var before = gameplay.Select(EditorJsonUtility.ToJson).ToArray();
        var positions = original.Select(t => (t, t.position, t.rotation, t.localScale, t.gameObject.activeSelf)).ToArray();
        var map = original.Single(t => t.name == "Tilemap2" && t.gameObject.activeInHierarchy).GetComponent<Tilemap>();
        var floorObject = new GameObject("SIMPLE - authored surfaces", typeof(Tilemap), typeof(TilemapRenderer));
        floorObject.transform.SetParent(map.transform.parent, false);
        var floor = floorObject.GetComponent<Tilemap>();
        var renderer = floorObject.GetComponent<TilemapRenderer>();
        renderer.sharedMaterial = flat; renderer.sortingOrder = -1;
        var tiles = new Dictionary<string, Tile>();
        foreach (var name in new[] { "floor_a", "floor_b", "void", "wall_top", "wall_face" })
        {
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(Art + name + ".asset");
            if (tile == null) { tile = ScriptableObject.CreateInstance<Tile>(); tile.sprite = Sprite(name); tile.colliderType = Tile.ColliderType.None; AssetDatabase.CreateAsset(tile, Art + name + ".asset"); }
            tiles[name] = tile;
        }
        foreach (var p in map.cellBounds.allPositionsWithin)
        {
            var old = map.GetTile(p); if (old == null) continue;
            bool outside = old.name == "AseLocationBunker3_39";
            bool wall = map.GetColliderType(p) != Tile.ColliderType.None;
            string tile = outside ? "void" : wall ? "wall_top" : ((p.x * 13 + p.y * 7) % 11 == 0 ? "floor_b" : "floor_a");
            if (!outside && wall && map.HasTile(p + Vector3Int.down) && map.GetColliderType(p + Vector3Int.down) == Tile.ColliderType.None) tile = "wall_face";
            floor.SetTile(p, tiles[tile]);
        }
        // Keep the original Tilemap and its exact sprite-based collision geometry.
        map.GetComponent<TilemapRenderer>().enabled = false;
        int hidden = 0;
        foreach (var sr in original.Select(t => t.GetComponent<SpriteRenderer>()).Where(r => r != null && Environment(r.transform)))
        {
            if (sr.enabled) hidden++;
            sr.enabled = false;
        }
        foreach (var light in Components<Light2D>(s).Where(l => Environment(l.transform)))
            if (light.lightType != Light2D.LightType.Global) light.enabled = false;
        foreach (var ps in Components<ParticleSystem>(s).Where(p => Environment(p.transform)))
        { var r = ps.GetComponent<ParticleSystemRenderer>(); if (r != null) r.enabled = false; }
        foreach (var volume in Components<UnityEngine.Rendering.Volume>(s)) volume.enabled = false;
        // Preserve the stateful floor navigation; disabled post-processing removes its bloom.

        foreach (var tm in Components<Tilemap>(s).Where(m => m.name.StartsWith("TilemapZone")))
        { tm.color = new Color(.06f, .09f, .13f, .45f); tm.GetComponent<TilemapRenderer>().sharedMaterial = flat; }
        var decor = new GameObject("SIMPLE - room silhouettes").transform;
        foreach (var tm in Components<Tilemap>(s).Where(m => m.name == "TilemapMirrorOutline" || m.name == "TilemapMirror")) tm.GetComponent<TilemapRenderer>().enabled = false;
        var pool = Prop(decor, "SIMPLE observation tank", "pool", new Vector3(12, .5f, 0), 1);
        pool.transform.localScale = new Vector3(5.5f, 3.5f, 1);
        // Bind each primary silhouette to its existing interactive station, not a duplicate interaction.
        foreach (var station in Components<BunkerStation>(s))
        {
            if (!station.gameObject.activeInHierarchy) continue;
            var so = new SerializedObject(station);
            var type = (BunkerStationType)so.FindProperty("stationType").intValue;
            string sprite = type == BunkerStationType.CharacterSelection ? "capsule" :
                type == BunkerStationType.WeaponSelection ? "weapon_bench" :
                type == BunkerStationType.Upgrade ? "terminal" :
                type == BunkerStationType.AnomalyStabilizer ? "anomaly_ring" :
                type == BunkerStationType.EscapeProtocol ? "terminal" : null;
            if (sprite == null) continue;
            // The extra weapon-room upgrade point remains a small secondary terminal.
            float scale = station.name == "p_bunkerWeaponTable3" || type == BunkerStationType.EscapeProtocol ? 1 : 2;
            Vector3 pos = station.transform.position + Vector3.up * (scale == 2 ? 1.8f : 1);
            var sr = Prop(station.transform, "SIMPLE " + sprite, sprite, pos, scale);
            var hover = station.GetComponent<BunkerHoverOutline>();
            if (hover != null)
            {
                var h = new SerializedObject(hover); h.FindProperty("autoFindRenderers").boolValue = false;
                var targets = h.FindProperty("targetRenderers"); targets.arraySize = 1; targets.GetArrayElementAtIndex(0).objectReferenceValue = sr;
                h.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        foreach (var room in Components<BunkerRoomAccess>(s).Where(r => r.gameObject.activeInHierarchy))
        {
            var so = new SerializedObject(room);
            var opened = (GameObject)so.FindProperty("openDoorVisual").objectReferenceValue;
            var closed = (GameObject)so.FindProperty("closedDoorVisual").objectReferenceValue;
            if (opened == null || closed == null) throw new InvalidOperationException("Missing door binding: " + room.name);
            Prop(opened.transform, "SIMPLE open threshold", "door_open", room.transform.position + Vector3.up * .5f, 1);
            Prop(closed.transform, "SIMPLE closed door", "door_closed", room.transform.position + Vector3.up, 1);
        }
        foreach (var gate in Components<BunkerGateVisual>(s).Where(g => g.gameObject.activeInHierarchy))
        {
            var so = new SerializedObject(gate);
            foreach (string field in new[] { "openedDoor", "closedDoor" })
            {
                var go = (GameObject)so.FindProperty(field).objectReferenceValue;
                if (go != null) Prop(go.transform, "SIMPLE run gate", field == "openedDoor" ? "door_open" : "door_closed", gate.transform.position + Vector3.up * 1.5f, 2);
            }
        }
        Prop(decor, "Secret - strange object", "strange_object", new Vector3(53.5f,-22,0), 2);

        Prop(decor, "Weapon - stool", "stool", new Vector3(46,-3,0), 1);
        Prop(decor, "Upgrade - storage", "crate", new Vector3(55,3,0), 1);
        // An existing pool/fence obstacle must remain visible after its noisy artwork is hidden.
        var obstacles = new List<string>();
        foreach (var c in Components<Collider2D>(s).Where(c => c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger && Environment(c.transform)))
        {
            if (c is TilemapCollider2D || c is CompositeCollider2D || c.GetComponentInParent<BunkerStation>() != null ||
                c.GetComponentInParent<BunkerRoomAccess>() != null || c.GetComponentInParent<BunkerGateVisual>() != null || c.GetComponentInParent<Rigidbody2D>() != null) continue;
            Bounds b = c.bounds;
            if (b.size.x < .1f || b.size.y < .1f) continue;
            var sr = Prop(c.transform, "SIMPLE obstacle footprint", "wall_face", b.center, 1);
            sr.transform.localScale = new Vector3(b.size.x / c.transform.lossyScale.x, b.size.y / c.transform.lossyScale.y, 1);
            sr.sortingOrder = 0;
            obstacles.Add(PathOf(c.transform));
        }
        foreach (var sr in Components<SpriteRenderer>(s).Where(r => r.name == "SIMPLE obstacle footprint" && r.transform.parent.name.StartsWith("p_Capsule1 (5) Variant"))) sr.enabled = false;
        var rear = Prop(decor, "Character - plain rear bench", "wall_face", new Vector3(32.5f, 3.4f, 0), 1);
        rear.transform.localScale = new Vector3(9, 1.5f, 1);
        // Physics, active states, station events and door access references are invariants of this pass.
        for (int i = 0; i < gameplay.Length; i++) if (EditorJsonUtility.ToJson(gameplay[i]) != before[i]) throw new InvalidOperationException("Gameplay changed: " + PathOf(gameplay[i].transform));
        foreach (var p in positions) if (p.t.position != p.position || p.t.rotation != p.rotation || p.t.localScale != p.localScale || p.t.gameObject.activeSelf != p.activeSelf) throw new InvalidOperationException("Original transform/state changed: " + PathOf(p.t));
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(s);
        File.WriteAllText(QA + "build.txt", "PASS: " + gameplay.Length + " gameplay/collider components unchanged; " + positions.Length + " original transforms/states unchanged.\nHidden sprite renderers: " + hidden + "\nObstacle footprints:\n" + string.Join("\n", obstacles));
        Debug.Log("SIMPLE authored. Original MainMenu untouched. " + QA + "build.txt");
    }

    static SpriteRenderer Prop(Transform parent, string name, string sprite, Vector3 position, float scale)
    {
        var go = new GameObject(name, typeof(SpriteRenderer)); go.transform.SetParent(parent, false);
        go.transform.position = position; go.transform.localScale = Vector3.one * scale;
        var sr = go.GetComponent<SpriteRenderer>(); sr.sprite = Sprite(sprite); sr.sharedMaterial = flat; sr.sortingOrder = 2;
        return sr;
    }

    [MenuItem("Tools/Subject42/Bunker Prototype/Open OLD")]
    public static void OpenOld() => Open("OLD");
    [MenuItem("Tools/Subject42/Bunker Prototype/Open SIMPLE")]
    public static void OpenSimple() => Open("SIMPLE");
    static void Open(string variant)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play Mode before switching snapshots.");
        if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(Root + variant + "/MainMenu.unity");
    }
    [MenuItem("Tools/Subject42/Bunker Prototype/Capture overview")]
    public static void CaptureOverview()
    {
        Directory.CreateDirectory(QA);
        var camera = Components<Camera>(SceneManager.GetActiveScene()).First(c => c.CompareTag("MainCamera"));
        var position = camera.transform.position; var rotation = camera.transform.rotation;
        float size = camera.orthographicSize; var target = camera.targetTexture;
        var previous = RenderTexture.active;
        var rt = new RenderTexture(1920, 1080, 24);
        var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
        try
        {
            camera.transform.position = new Vector3(51, -2, -100); camera.transform.rotation = Quaternion.identity;
            camera.orthographicSize = 35; camera.targetTexture = rt;
            camera.Render(); RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); texture.Apply();
            File.WriteAllBytes(QA + "overview.png", texture.EncodeToPNG());
        }
        finally
        {
            camera.transform.position = position; camera.transform.rotation = rotation; camera.orthographicSize = size;
            camera.targetTexture = target; RenderTexture.active = previous;
            UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    [MenuItem("Tools/Subject42/Bunker Prototype/Activate OLD (gameplay)")]
    public static void ActivateOld() => Activate("OLD");
    [MenuItem("Tools/Subject42/Bunker Prototype/Activate SIMPLE (gameplay)")]
    public static void ActivateSimple() => Activate("SIMPLE");
    static void Activate(string variant)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var current = File.ReadAllBytes(Main);
        var old = File.ReadAllBytes(Root + "OLD/MainMenu.unity");
        var simple = File.ReadAllBytes(Root + "SIMPLE/MainMenu.unity");
        if (!current.SequenceEqual(old) && !current.SequenceEqual(simple))
            throw new InvalidOperationException("MainMenu has edits beyond the prototype snapshots. Preserve those edits before switching.");
        EditorSceneManager.OpenScene(Root + variant + "/MainMenu.unity");
        File.Copy(Root + variant + "/MainMenu.unity", Main, true);
        AssetDatabase.ImportAsset(Main, ImportAssetOptions.ForceUpdate);
        EditorSceneManager.OpenScene(Main);
    }

}
