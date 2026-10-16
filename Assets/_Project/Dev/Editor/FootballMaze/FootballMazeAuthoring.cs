using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>One-time editor authoring. The saved scene owns every tile, collider and reference.</summary>
public static class FootballMazeAuthoring
{
    const string Project = "Assets/_Project/";
    const string Root = Project + "Dev/Labs/FootballMazeLab/";
    static readonly Color[] Colors = {
        new(.3f, 1f, .68f), new(1f, .72f, .18f), new(.2f, .85f, 1f),
        new(1f, .43f, .25f), new(.65f, .5f, 1f), new(1f, .35f, .7f) };
    static Tilemap walls;
    static Tile floorTile, wallTile;
    static Material flat;
    static Font font;
    static FootballMazeLab lab;
    static Transform puzzleRoot;
    static readonly List<FootballMazeDoor> doors = new();
    static readonly List<FootballMazeButton> buttons = new();

    [MenuItem("Tools/Subject42/Dev/Football Maze/Open scene")]
    public static void Open() => EditorSceneManager.OpenScene(FootballMazeLab.ScenePath);

    [MenuItem("Tools/Subject42/Dev/Football Maze/Create scene (only if absent)")]
    public static void Create()
    {
        if (File.Exists(FootballMazeLab.ScenePath))
            throw new InvalidOperationException("FootballMazeLab already exists. Edit the scene directly.");
        Build();
    }

    public static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        Directory.CreateDirectory(Root);
        doors.Clear(); buttons.Clear();
        // Keep any unsaved bunker scene open; author only the newly active additive scene.
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
            Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
        floorTile = Load<Tile>("art/Sprites/newLowBunker/BunkerLowTilemap/newLowBunker1_0.asset");
        // These palette tiles both have one-metre sprites, matching the scene Grid.
        wallTile = Load<Tile>("art/Sprites/Environment/BunkerTilePalette3/AseLocationBunker3_9.asset");
        flat = Load<Material>("art/Football/FootballFlat.mat");
        font = Load<Font>("Fonts/LiberationSans.ttf");
        lab = new GameObject("FootballMazeLab · authored puzzle session").AddComponent<FootballMazeLab>();
        puzzleRoot = new GameObject("Circuits · explicit button and door references").transform;
        var grid = new GameObject("Bunker Grid · BunkerLowTilePalette + BunkerTilePalette3", typeof(Grid));
        var floor = Map(grid.transform, "Floor · bunker palette", -20);
        floor.color = new Color(.36f, .22f, .2f);
        walls = Map(grid.transform, "Walls · solid grid collision", 5);
        walls.color = new Color(.7f, .8f, .86f);
        for (int x = 0; x <= 90; x++) for (int y = 0; y <= 48; y++)
        {
            floor.SetTile(new Vector3Int(x, y, 0), floorTile);
            if (x == 0 || x == 30 || x == 60 || x == 90 || y == 0 || y == 24 || y == 48)
                Wall(x, y, x, y);
        }
        // 01: a dogleg arrival, branching loop and firing court.
        Wall(9, 1, 10, 11); Wall(16, 8, 17, 19); Wall(4, 17, 10, 18);
        // 02: two routes on a shared divider. Switching seals the lower spur.
        Wall(44, 1, 44, 23); Wall(45, 12, 59, 13); Wall(31, 12, 38, 13);
        Wall(49, 3, 53, 4);
        Cut(44, 1, 44, 3); // Return loop remains available when the lower shortcut shuts.
        // 03: a true ball-sized aperture and a separate full-height retrieval shutter.
        Wall(78, 1, 78, 23); Wall(65, 11, 72, 12); Wall(69, 2, 70, 5);
        Cut(78, 8, 78, 8);
        // 04: the central baffle hides the target from the marked striking pad.
        Wall(70, 29, 82, 30); Wall(77, 32, 79, 39); Wall(65, 44, 85, 44);
        Wall(68, 32, 68, 38); Wall(68, 31, 76, 31);
        // 05: approach loop and isolated target court above the radial repulsor.
        Wall(35, 32, 40, 33); Wall(53, 25, 54, 36); Wall(34, 40, 38, 41);
        // 06: route switch, ball tunnel, retrieval and final goal court.
        Wall(17, 25, 17, 47); Wall(9, 25, 9, 39); Wall(1, 40, 9, 40);
        Wall(22, 33, 23, 35); Cut(9, 35, 9, 35);
        // Connections describe a serpentine route; all remain ordinary editable cells.
        Cut(60, 16, 60, 20); Cut(83, 24, 87, 24);

        var tileCollider = walls.gameObject.AddComponent<TilemapCollider2D>();
        tileCollider.compositeOperation = Collider2D.CompositeOperation.Merge;
        tileCollider.includeLayers = 1 << LayerMask.NameToLayer("Ball");
        var wallBody = walls.gameObject.AddComponent<Rigidbody2D>(); wallBody.bodyType = RigidbodyType2D.Static;
        var composite = walls.gameObject.AddComponent<CompositeCollider2D>();
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        composite.sharedMaterial = Load<PhysicsMaterial2D>("art/mat/footBallMats/FootballVS_Bounce.physicsMaterial2D");
        composite.includeLayers = tileCollider.includeLayers;
        MakePlayerAndBall();
        MakeCamera();

        var d1 = Door("A1", 0, 30, 6, 30, 10);
        var d2a = Door("B1", 1, 44, 5, 44, 9, true);
        var d2b = Door("B2", 1, 44, 17, 44, 21);
        var d3 = Door("C1", 2, 78, 17, 78, 21);
        var d4 = Door("D1", 3, 60, 40, 60, 44);
        var d4pickup = Door("D2", 3, 68, 33, 68, 37);
        var d5 = Door("E1", 4, 30, 28, 30, 32);
        var d6a = Door("F1", 5, 17, 28, 17, 32, true);
        var d6b = Door("F2", 5, 17, 42, 17, 46);
        var d6c = Door("F3", 5, 9, 27, 9, 30);
        var d6goal = Door("F4", 5, 4, 40, 7, 40);
        Button("A1", 0, new(23, 14), new[] { d1 });
        Button("B1 / B2", 1, new(52, 7), new[] { d2b }, new[] { d2a }, true);
        Button("C1", 2, new(84, 8.5f), new[] { d3 });
        Button("D1 / D2", 3, new(73, 34), new[] { d4, d4pickup });
        Button("F1 / F2", 5, new(24, 39), new[] { d6b }, new[] { d6a }, true);
        Button("F3 / F4", 5, new(5, 35.5f), new[] { d6c, d6goal });
        lab.Doors = doors.ToArray(); lab.Buttons = buttons.ToArray();

        Slit(new(78.5f, 8.5f), true, "C · только мяч · 0.52 м");
        Slit(new(9.5f, 35.5f), true, "F · только мяч");
        // These low mesh fences have an explicitly authored Ball layer exclusion.
        LowFence(new(72.5f, 39.5f), new(9, .3f), "D · прострел сверху");
        LowFence(new(47.5f, 40), new(13, .3f), "E · прострел через поле");
        Wall(41, 40, 41, 47); Wall(54, 40, 54, 47);
        var d5pickup = Door("E2", 4, 54, 42, 54, 45);
        Button("E1 / E2", 4, new(49, 43), new[] { d5, d5pickup });
        lab.Doors = doors.ToArray(); lab.Buttons = buttons.OrderBy(b => b.Section).ToArray();

        lab.Checkpoints = new[] {
            CP(new(4, 5), new(5, 5), "01 / ПЕРВЫЙ КОНТАКТ\nПроведи мяч к A1. Удар откроет зелёную дверь."),
            CP(new(33, 8), new(34, 8), "02 / СМЕНА МАРШРУТА\nB закроет проход позади. Вернись через нижний обход и проведи мяч через верхнюю дверь."),
            CP(new(64, 18), new(65, 18), "03 / ЧЕРЕЗ РЕШЁТКУ\nУдарь через узкое окно C. Откроется верхняя дверь — забери мяч с другой стороны."),
            CP(new(85, 26), new(86, 26), "04 / РИКОШЕТ\nС оранжевой метки бей в отмеченный участок верхней стены. Отскок попадёт в D1."),
            CP(new(57, 42), new(56, 42), "05 / ОТТАЛКИВАНИЕ\nСильный удар вверх с фиолетовой метки: поле изогнёт траекторию мяча к E1."),
            CP(new(27, 30), new(26, 30), "06 / ДОСТАВКА\nПереключи F1/F2, обойди сверху. Прострели окно F3, забери мяч через нижнюю дверь и доставь в ворота.") };
        Checkpoint(1, new(34, 8), new(6, 7));
        Checkpoint(2, new(65, 18), new(7, 7));
        Checkpoint(3, new(85, 27), new(7, 4));
        Checkpoint(4, new(56.5f, 42), new(5, 6));
        Checkpoint(5, new(26.5f, 30), new(5, 5));
        Goal();
        var anomaly = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>("prefabs/Bunker/Minigames/PF_FootballGravityZone.prefab"));
        anomaly.name = "E · existing repulsion anomaly"; anomaly.transform.position = new(45, 35, 0);
        lab.Anomaly = anomaly.GetComponent<GravityZone>();
        lab.AnomalyData = Load<LocalAnomalyData>("Data/Anomalies/LocalAnomaly_Gravity.asset");
        MakeHUD();

        Label("FOOTBALL / MAZE LAB", new(14, 22), Colors[0], 1.1f);
        Label("01  CONTACT", new(23, 21), Colors[0]);
        Label("02  SWITCHBACK", new(47, 22.5f), Colors[1]);
        Label("03  BALL TUNNEL", new(71, 22.5f), Colors[2]);
        Label("04  BANK SHOT", new(76, 46.5f), Colors[3]);
        Label("05  REPULSOR", new(46, 46.5f), Colors[4]);
        Label("06  DELIVERY", new(23, 46.5f), Colors[5]);
        MarkPad(new(23, 8), Colors[0], "A"); MarkPad(new(48, 7), Colors[1], "B");
        MarkPad(new(74, 8.5f), Colors[2], "C"); MarkPad(new(84, 34), Colors[3], "D");
        MarkPad(new(46.5f, 31), Colors[4], "E"); MarkPad(new(13, 35.5f), Colors[5], "F");
        Guide(new[] { new Vector2(84, 34), new(78.5f, 43.8f), new(73, 34) }, Colors[3]);
        Guide(new[] { new Vector2(46.5f, 31), new(46.8f, 35), new(49, 39.5f), new(49, 43) }, Colors[4]);
        RectSprite("D · reflector stripe", new(78, 43.94f), new(3.5f, .12f), Colors[3], 10);
        Label("ОТСКОК", new(78, 45.3f), Colors[3], .7f);
        // Small author-time accents communicate paths without covering the playfield.
        for (int section = 0; section < 6; section++)
        {
            Vector2 origin = section < 3 ? new(section * 30, 0) : new((5 - section) * 30, 24);
            for (int i = 2; i < 29; i += 3)
                RectSprite("Section skirting", origin + new Vector2(i, 1.15f), new(.55f, .08f), Colors[section] * .55f, 0);
        }
        tileCollider.ProcessTilemapChanges(); composite.GenerateGeometry();
        walls.CompressBounds(); floor.CompressBounds();
        EditorSceneManager.SaveScene(scene, FootballMazeLab.ScenePath);
        AssetDatabase.SaveAssets();
        Selection.activeGameObject = grid;
        if (SceneView.lastActiveSceneView != null)
            SceneView.lastActiveSceneView.LookAt(new Vector3(45, 24, 0), Quaternion.identity, 52, true, true);
        Debug.Log("[FootballMaze] Authored six sections, seven buttons, twelve safe doors and two palette Tilemaps.");
    }

    static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(Project + path)
        ?? throw new InvalidOperationException("Missing asset: " + path);
    static Tilemap Map(Transform grid, string name, int order)
    {
        var go = new GameObject(name, typeof(Tilemap), typeof(TilemapRenderer)); go.transform.SetParent(grid);
        var renderer = go.GetComponent<TilemapRenderer>(); renderer.sharedMaterial = flat;
        renderer.sortingOrder = order;
        return go.GetComponent<Tilemap>();
    }
    static void Wall(int x0, int y0, int x1, int y1)
    {
        for (int x = x0; x <= x1; x++) for (int y = y0; y <= y1; y++)
        {
            walls.SetTile(new Vector3Int(x, y, 0), wallTile);
            walls.SetColliderType(new Vector3Int(x, y, 0), Tile.ColliderType.Grid);
        }
    }
    static void Cut(int x0, int y0, int x1, int y1)
    { for (int x = x0; x <= x1; x++) for (int y = y0; y <= y1; y++) walls.SetTile(new(x, y, 0), null); }

    static void MakePlayerAndBall()
    {
        var player = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>("prefabs/Characters/DiMag.prefab"));
        PrefabUtility.UnpackPrefabInstance(player, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        player.name = "Player · existing CharacterMovement2D";
        player.transform.position = new(4, 5, 0);
        // This standalone lab has no combat, progression, weapon or bunker station lifecycle.
        foreach (var component in player.GetComponentsInChildren<MonoBehaviour>(true))
            if (!(component is CharacterMovement2D)) Object.DestroyImmediate(component);
        var movement = player.GetComponent<CharacterMovement2D>();
        var serialized = new SerializedObject(movement); serialized.FindProperty("baseMoveSpeed").floatValue = 5f;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        lab.Player = player.GetComponent<Rigidbody2D>();
        var ball = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>("prefabs/Bunker/Minigames/PF_FootballBall.prefab"));
        ball.name = "Ball · existing dribble, aim, kick and anomaly physics";
        ball.transform.position = new(5, 5, 0);
        ball.layer = LayerMask.NameToLayer("Ball"); ball.tag = "Ball";
        lab.Ball = ball.GetComponent<BallRollVisual>(); lab.BallRuntime = ball.GetComponent<FootballBallRuntime>();
        lab.BallBody = ball.GetComponent<Rigidbody2D>();
        lab.BallBody.useAutoMass = false; lab.BallBody.mass = 1f; lab.BallBody.linearDamping = .14f;
        lab.BallBody.sharedMaterial = Load<PhysicsMaterial2D>("art/mat/footBallMats/FootballVS_Bounce.physicsMaterial2D");
        foreach (var c in ball.GetComponents<CircleCollider2D>()) if (!c.isTrigger)
        { c.radius = .2f; c.sharedMaterial = lab.BallBody.sharedMaterial; }
        lab.Ball.radius = .2f; lab.Ball.visual.localScale = Vector3.one * .4f;
        lab.Ball.dribbleDistance = .85f; lab.Ball.dribbleJitterRadius = .015f;
        lab.Ball.kickZoneRadius = 3f; lab.Ball.kickRange = 1.65f;
        lab.Ball.minPower = 4f; lab.Ball.maxPower = 20f;
        lab.Ball.rayWidth = .045f; lab.Ball.rayColor = new Color(.9f, .95f, .6f);
        serialized = new SerializedObject(lab.Ball); serialized.FindProperty("orbitWhileAiming").boolValue = true;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        foreach (var r in player.GetComponentsInChildren<SpriteRenderer>(true)) r.sharedMaterial = flat;
        ball.SetActive(true);
    }
    static void MakeCamera()
    {
        var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(CameraFollow));
        go.tag = "MainCamera"; go.transform.position = new(14, 10, -10);
        var camera = go.GetComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 8.8f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new(.025f, .035f, .045f);
        lab.CameraFollow = go.GetComponent<CameraFollow>(); lab.CameraFollow.target = lab.Player.transform;
        lab.CameraFollow.smoothSpeed = .12f;
    }
    static FootballMazeDoor Door(string id, int section, int x0, int y0, int x1, int y1, bool open = false)
    {
        Cut(x0, y0, x1, y1);
        var go = new GameObject(id + " · safe shutter"); go.transform.SetParent(puzzleRoot);
        go.transform.position = new((x0 + x1 + 1) * .5f, (y0 + y1 + 1) * .5f, 0);
        Vector2 size = new(x1 - x0 + 1, y1 - y0 + 1);
        var closed = RectSprite("Closed · bunker steel", go.transform.position, size, new(.48f, .58f, .63f), 9);
        closed.transform.SetParent(go.transform, true);
        var solid = closed.gameObject.AddComponent<BoxCollider2D>();
        solid.includeLayers = 1 << LayerMask.NameToLayer("Ball");
        var opened = new GameObject("Open · empty aperture"); opened.transform.SetParent(go.transform, false);
        var gate = go.AddComponent<BunkerGateVisual>(); var so = new SerializedObject(gate);
        so.FindProperty("openedDoor").objectReferenceValue = opened;
        so.FindProperty("closedDoor").objectReferenceValue = closed.gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();
        closed.gameObject.SetActive(!open); opened.SetActive(open);
        var aperture = go.AddComponent<BoxCollider2D>(); aperture.isTrigger = true; aperture.size = size;
        var door = go.AddComponent<FootballMazeDoor>(); door.Gate = gate; door.Aperture = aperture;
        door.Player = lab.Player; door.Ball = lab.BallBody; door.Section = section; door.InitiallyOpen = open;
        door.CircuitColor = Colors[section];
        var status = RectSprite("Circuit stripe", (Vector2)go.transform.position + (size.x < size.y ? new Vector2(-.55f, 0) : new Vector2(0, -.55f)),
            size.x < size.y ? new(.12f, size.y) : new(size.x, .12f), Colors[section], 10);
        status.transform.SetParent(go.transform, true); door.Status = status;
        Label(id, (Vector2)go.transform.position + new Vector2(1.2f, 1.2f), Colors[section], .8f).transform.SetParent(go.transform, true);
        doors.Add(door); return door;
    }
    static FootballMazeButton Button(string id, int section, Vector2 p, FootballMazeDoor[] opens,
        FootballMazeDoor[] closes = null, bool toggle = false)
    {
        var go = new GameObject(id + " · ball strike button"); go.transform.SetParent(puzzleRoot); go.transform.position = p;
        var face = RectSprite("Bunker target", p, new(1.25f, 1.25f), Colors[section], 12,
            Load<Sprite>("art/Football/FootballTarget.png")); face.transform.SetParent(go.transform, true);
        var collider = go.AddComponent<CircleCollider2D>(); collider.isTrigger = true; collider.radius = .7f;
        var button = go.AddComponent<FootballMazeButton>(); button.Lab = lab; button.Section = section;
        button.Face = face; button.CircuitColor = Colors[section]; button.OpenDoors = opens;
        button.CloseDoors = closes ?? Array.Empty<FootballMazeDoor>(); button.Toggle = toggle;
        button.Feedback = Sound(go, "AudioEffects/SFX/UI/Menu/Button_confirm.wav");
        button.ResultFocus = opens[0].transform;
        Label(id, p + new Vector2(0, 1.2f), Colors[section], .8f).transform.SetParent(go.transform, true);
        buttons.Add(button); return button;
    }
    static FootballMazeLab.CheckpointData CP(Vector2 p, Vector2 b, string hint) => new()
    { PlayerSpawn = Point("Safe player spawn", p), BallSpawn = Point("Safe ball spawn", b), Hint = hint };
    static Transform Point(string name, Vector2 p)
    { var go = new GameObject(name); go.transform.SetParent(lab.transform); go.transform.position = p; return go.transform; }
    static void Checkpoint(int index, Vector2 p, Vector2 size)
    {
        var go = new GameObject($"Checkpoint {index + 1} · player AND ball"); go.transform.position = p;
        var zone = go.AddComponent<FootballMazeZone>(); zone.Lab = lab; zone.Checkpoint = index;
        zone.Area = go.AddComponent<BoxCollider2D>(); zone.Area.isTrigger = true; zone.Area.size = size;
        RectSprite("Checkpoint paint", p, new(2f, .15f), new(.25f, .8f, .6f, .65f), -1);
        Label("CP " + (index + 1), p + new Vector2(0, -1.2f), Colors[index], .65f);
    }
    static void Goal()
    {
        var go = new GameObject("Exit goal · deliver the only ball"); go.transform.position = new(5.5f, 44.5f);
        var zone = go.AddComponent<FootballMazeZone>(); zone.Lab = lab; zone.Finish = true;
        zone.Area = go.AddComponent<BoxCollider2D>(); zone.Area.isTrigger = true; zone.Area.size = new(4, 3);
        var net = RectSprite("Existing tiled football net", go.transform.position, new(5, 4),
            new(.6f, .75f, .8f, .65f), -1, Load<Sprite>("art/Football/FootballNet.png"));
        net.transform.localScale = Vector3.one; net.drawMode = SpriteDrawMode.Tiled; net.size = new(5, 4);
        Label("ВЫХОД / МЯЧ СЮДА", new(6, 47), Colors[5], .8f);
        RectSprite("Goal left post", new(3, 44), new(.18f, 4f), Color.white, 12);
        RectSprite("Goal right post", new(8, 44), new(.18f, 4f), Color.white, 12);
        RectSprite("Goal back bar", new(5.5f, 46), new(5.18f, .18f), Color.white, 12);
    }
    static void Slit(Vector2 p, bool vertical, string name)
    {
        // .40 m ball fits a .52 m opening; the .60 m player foot collider does not.
        foreach (float sign in new[] { -1f, 1f })
        {
            Vector2 offset = vertical ? new(0, sign * .38f) : new(sign * .38f, 0);
            var r = RectSprite(name, p + offset, vertical ? new(1, .24f) : new(.24f, 1), new(.5f, .65f, .7f), 10);
            var col = r.gameObject.AddComponent<BoxCollider2D>(); col.includeLayers = 1 << LayerMask.NameToLayer("Ball");
        }
        Label("ТОЛЬКО МЯЧ", p + new Vector2(-2.5f, 1.4f), Colors[2], .6f);
    }
    static void LowFence(Vector2 p, Vector2 size, string name)
    {
        var r = RectSprite(name, p, size, new(.45f, .7f, .75f), 11);
        var collider = r.gameObject.AddComponent<BoxCollider2D>(); collider.excludeLayers = 1 << LayerMask.NameToLayer("Ball");
        Label("НИЗКАЯ РЕШЁТКА · МЯЧ ПРОХОДИТ", p + new Vector2(0, .8f), new(.55f, .7f, .75f), .5f);
    }
    static void MarkPad(Vector2 p, Color color, string letter)
    {
        var sprite = Load<Sprite>("art/Football/Chalk.png");
        RectSprite("Strike pad " + letter, p, new(1.7f, 1.7f), color * .7f, -1, sprite);
        Label(letter + " · УДАР", p + new Vector2(0, -1.4f), color, .6f);
    }
    static void Guide(Vector2[] points, Color color)
    {
        color.a = .3f;
        for (int i = 1; i < points.Length; i++)
        {
            Vector2 delta = points[i] - points[i - 1]; int count = Mathf.CeilToInt(delta.magnitude * 1.4f);
            for (int j = 1; j < count; j += 2)
            {
                var r = RectSprite("Faded shot guide", Vector2.Lerp(points[i - 1], points[i], j / (float)count), new(.25f, .055f), color, -1);
                r.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            }
        }
    }
    static SpriteRenderer RectSprite(string name, Vector2 p, Vector2 size, Color color, int order, Sprite sprite = null)
    {
        var go = new GameObject(name); go.transform.position = p;
        var renderer = go.AddComponent<SpriteRenderer>(); renderer.sprite = sprite != null ? sprite : floorTile.sprite;
        renderer.sharedMaterial = flat; renderer.color = color; renderer.sortingOrder = order;
        go.transform.localScale = new(size.x / renderer.sprite.bounds.size.x, size.y / renderer.sprite.bounds.size.y, 1);
        return renderer;
    }
    static TextMesh Label(string text, Vector2 p, Color color, float scale = 1)
    {
        var go = new GameObject(text); go.transform.position = p;
        var tm = go.AddComponent<TextMesh>(); tm.text = text; tm.font = font; tm.fontSize = 48;
        tm.characterSize = .08f * scale; tm.anchor = TextAnchor.MiddleCenter; tm.color = color;
        var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = font.material; renderer.sortingOrder = 20;
        return tm;
    }
    static AudioSource Sound(GameObject go, string path)
    {
        var audio = go.AddComponent<AudioSource>(); audio.clip = Load<AudioClip>(path);
        audio.playOnAwake = false; audio.spatialBlend = 0; audio.volume = .45f; return audio;
    }
    static void MakeHUD()
    {
        var canvas = new GameObject("Maze HUD · no score or timer", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvas.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new(1600, 900); scaler.matchWidthOrHeight = .5f;
        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        lab.Hint = UIText(canvas.transform, "Task", new(24, -20), new(960, 88), 23, TextAnchor.UpperLeft);
        lab.Status = UIText(canvas.transform, "Checkpoint status", new(24, -114), new(900, 32), 16, TextAnchor.UpperLeft);
        var controls = UIText(canvas.transform, "Controls", new(24, -154), new(900, 28), 16, TextAnchor.UpperLeft);
        controls.text = "WASD — движение    F — ведение    ЛКМ удержать / отпустить — удар";
        MakeUIButton(canvas.transform, "ВЕРНУТЬ МЯЧ [R]", new(-230, -26), lab.ReturnBall);
        MakeUIButton(canvas.transform, "ЗАНОВО [BACKSPACE]", new(-230, -76), lab.RestartMaze);
        lab.CompletionPanel = new GameObject("Completed", typeof(RectTransform), typeof(Image));
        var rect = lab.CompletionPanel.GetComponent<RectTransform>(); rect.SetParent(canvas.transform, false);
        rect.sizeDelta = new(680, 180); lab.CompletionPanel.GetComponent<Image>().color = new(.04f, .09f, .1f, .96f);
        var done = UIText(rect, "Delivered", new(25, -30), new(630, 130), 28, TextAnchor.MiddleCenter);
        done.text = "МЯЧ ДОСТАВЛЕН\nЛабиринт пройден!\nBACKSPACE — пройти ещё раз";
        lab.CompletionPanel.SetActive(false);
        lab.CheckpointSound = Sound(lab.gameObject, "AudioEffects/pickupCoin.wav");
    }
    static Text UIText(Transform parent, string name, Vector2 pos, Vector2 size, int fontSize, TextAnchor align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text)); var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false); rt.anchorMin = rt.anchorMax = new(0, 1); rt.pivot = new(0, 1);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        var text = go.GetComponent<Text>(); text.font = font; text.fontSize = fontSize; text.alignment = align;
        text.color = new(.8f, .93f, .92f); text.raycastTarget = false; return text;
    }
    static void MakeUIButton(Transform parent, string text, Vector2 pos, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(text, typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = go.GetComponent<RectTransform>(); rt.SetParent(parent, false); rt.anchorMin = rt.anchorMax = new(1, 1);
        rt.pivot = new(0, 1); rt.anchoredPosition = pos; rt.sizeDelta = new(210, 42);
        go.GetComponent<Image>().color = new(.08f, .17f, .2f, .96f);
        var label = UIText(go.transform, "Label", new(5, -5), new(200, 32), 16, TextAnchor.MiddleCenter); label.text = text;
        // Persistent listeners survive scene save/reload.
        UnityEditor.Events.UnityEventTools.AddPersistentListener(go.GetComponent<Button>().onClick, action);
    }
}
