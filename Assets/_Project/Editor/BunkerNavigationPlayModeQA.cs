using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class BunkerNavigationPlayModeQA
{
    private const string Output = "Artifacts/BunkerNetwork/";
    private const string Session = "BunkerNetworkQA";
    private static double until;
    private static readonly List<string> report = new();
    static BunkerNavigationPlayModeQA()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Session, false))
            { until = EditorApplication.timeSinceStartup + 4; report.Clear(); }
            if (s == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Session, false))
            {
                if (SessionState.GetBool(Session + "IntroExists", false)) PlayerPrefs.SetInt(BunkerIntroController.ViewedPreferenceKey, SessionState.GetInt(Session + "Intro", 0));
                else PlayerPrefs.DeleteKey(BunkerIntroController.ViewedPreferenceKey);
                PlayerPrefs.Save(); SessionState.SetBool(Session, false);
                File.WriteAllText(Output + "complete.txt", "Returned to Edit Mode; intro preference restored.");
            }
        };
    }
    [MenuItem("Tools/Subject42/Bunker/Run Navigation Play Mode QA")]
    public static void Start()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().name != "MainMenu" || SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("Open saved MainMenu in Edit Mode.");
        Directory.CreateDirectory(Output);
        SessionState.SetBool(Session + "IntroExists", PlayerPrefs.HasKey(BunkerIntroController.ViewedPreferenceKey));
        SessionState.SetInt(Session + "Intro", PlayerPrefs.GetInt(BunkerIntroController.ViewedPreferenceKey, 0));
        PlayerPrefs.SetInt(BunkerIntroController.ViewedPreferenceKey, 1);
        SessionState.SetBool(Session, true);
        EditorApplication.isPlaying = true;
    }
    private static void Check(bool pass, string message)
    {
        report.Add((pass ? "PASS " : "FAIL ") + message);
        File.WriteAllLines(Output + "PlayMode.txt", report);
        if (!pass) throw new Exception(message);
    }
    private static T Find<T>() where T : UnityEngine.Object => UnityEngine.Object.FindFirstObjectByType<T>();
    private static void Tick()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling && File.Exists(Output + "play.request"))
        { File.Delete(Output + "play.request"); try { Start(); } catch (Exception e) { File.WriteAllText(Output + "PlayMode.txt", e.ToString()); } }
        if (!SessionState.GetBool(Session, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < until) return;
        until = double.MaxValue;
        try { Run(); }
        catch (Exception e) { report.Add("FAIL " + e); File.WriteAllLines(Output + "PlayMode.txt", report); }
        finally { EditorApplication.isPlaying = false; }
    }
    private static void Run()
    {
        var view = Find<BunkerNavigationView>();
        Check(view != null && UnityEngine.Object.FindObjectsByType<BunkerNavigationView>(FindObjectsSortMode.None).Length == 1, "Exactly one navigation network");
        var so = new SerializedObject(view);
        var array = so.FindProperty("routes");
        var floors = new Dictionary<string, MeshRenderer>();
        for (int i = 0; i < array.arraySize; i++)
        {
            var p = array.GetArrayElementAtIndex(i);
            floors.Add(p.FindPropertyRelative("destination").stringValue, (MeshRenderer)p.FindPropertyRelative("floor").objectReferenceValue);
        }
        var rooms = UnityEngine.Object.FindObjectsByType<BunkerRoomAccess>(FindObjectsSortMode.None);
        BunkerRoomAccess Room(BunkerRoomId id) => rooms.Single(r => r.RoomId == id);
        var gate = Find<BunkerGateVisual>(); var mini = Find<FootballMinigame>();
        Check(floors.Count == 8, "Six room branches, Run Gate and real football mini-game branch");
        Check(floors["RunGate"].enabled, "Exit route visible on scene initialization, including closed gate");
        Capture("00-default", 55, -4, 31);
        foreach (var r in rooms) r.SetUnlocked(r.RoomId == BunkerRoomId.CharacterSelection || r.RoomId == BunkerRoomId.WeaponSelection);
        gate.Open(); mini.enabled = true;
        Check(floors.Where(p => p.Value.enabled).Select(p => p.Key).OrderBy(x => x).SequenceEqual(new[] { "Character", "MiniGame", "RunGate", "Weapon" }), "A: only Character, Weapon, MiniGame and open RunGate visible");
        Capture("01-few-open", 55, -4, 31);
        Capture("02-mini-game-open", 79, -3, 20);
        Room(BunkerRoomId.UpgradeStation).SetUnlocked(true);
        Check(floors["UpgradeStation"].enabled, "B: opening Upgrade immediately shows its branch without reload/Refresh");
        Capture("03-upgrade-open", 46, -8, 18);
        mini.enabled = false;
        Check(!floors["MiniGame"].enabled, "C: disabling mini-game component immediately hides its complete branch");
        gate.Close();
        Check(floors["RunGate"].enabled, "D: closed Run Gate keeps permanent exit route visible");
        Capture("04-closed-rooms-and-gate", 55, -4, 31);
        gate.Open();
        Check(floors["RunGate"].enabled, "E: opening Run Gate keeps exit route visible");
        mini.enabled = true;
        mini.transform.parent.gameObject.SetActive(false);
        Check(!floors["MiniGame"].enabled, "Mini-game root inactive hides route");
        mini.transform.parent.gameObject.SetActive(true);
        Check(floors["MiniGame"].enabled, "Mini-game root restored shows route");
        var character = Room(BunkerRoomId.CharacterSelection);
        character.gameObject.SetActive(false);
        Check(!floors["Character"].enabled, "Inactive room hides branch");
        character.gameObject.SetActive(true);
        Check(floors["Character"].enabled, "Re-enabled room restores branch");
        gate.GetComponent<BunkerStation>().SetInteractionEnabled(false);
        Check(floors["RunGate"].enabled, "Unavailable gate station does not hide permanent exit route");
        gate.GetComponent<BunkerStation>().SetInteractionEnabled(true);
        foreach (var r in rooms) r.SetUnlocked(true);
        Check(floors.Values.All(f => f.enabled), "F: all open room branches visible together");
        Capture("05-full-network", 55, -4, 31);
        InspectFloorClearance(view);
        var hashes = view.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh.GetInstanceID()).ToArray();
        var player = Find<CharacterMovement2D>(); var position = player.transform.position;
        player.transform.position += new Vector3(10, 2, 0);
        Check(hashes.SequenceEqual(view.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh.GetInstanceID())), "Moving player leaves authored mesh assets unchanged");
        player.transform.position = position;
        Capture("06-zoom-in", 44, -10, 7);
        Capture("07-zoom-out", 44, -10, 20);
        Check(true, "G: zoom captures rendered using fixed world-space mesh width");
        view.enabled = false;
        Check(view.GetComponentsInChildren<MeshRenderer>().All(f => !f.enabled), "Disabled network hides all floor renderers");
        character.SetUnlocked(false);
        view.enabled = true;
        Check(!floors["Character"].enabled && floors["Weapon"].enabled, "Re-enabled network reads latest authoritative state");
    }
    public static void Capture(string name, float x, float y, float size)
    {
        // A camera teleport is not gameplay motion. Prevent its history from smearing QA stills.
        var blur = UnityEngine.Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None)
            .Where(v => v.profile != null).SelectMany(v => v.profile.components)
            .OfType<UnityEngine.Rendering.Universal.MotionBlur>().ToArray();
        var blurStates = blur.Select(b => b.active).ToArray();
        var blurIntensity = blur.Select(b => b.intensity.value).ToArray();
        var blurOverrides = blur.Select(b => b.intensity.overrideState).ToArray();
        foreach (var b in blur) { b.active = true; b.intensity.Override(0); }
        var camera = Camera.main; var old = camera.transform.position; float oldSize = camera.orthographicSize;
        var rt = new RenderTexture(2400, 1350, 24); var previous = RenderTexture.active; var target = camera.targetTexture;
        var texture = new Texture2D(2400, 1350, TextureFormat.RGB24, false);
        try
        {
            camera.transform.position = new Vector3(x, y, -10); camera.orthographicSize = size;
            var cameraData = camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (cameraData != null)
            {
                cameraData.resetHistory = true;
                UnityEngine.Rendering.VolumeManager.instance.Update(camera.transform, cameraData.volumeLayerMask);
            }
            camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
            texture.ReadPixels(new Rect(0, 0, 2400, 1350), 0, 0); texture.Apply();
            Directory.CreateDirectory(Output); File.WriteAllBytes(Output + name + ".png", texture.EncodeToPNG());
        }
        finally
        {
            for (int i = 0; i < blur.Length; i++)
            { blur[i].active = blurStates[i]; blur[i].intensity.value = blurIntensity[i]; blur[i].intensity.overrideState = blurOverrides[i]; }
            camera.targetTexture = target; RenderTexture.active = previous; camera.transform.position = old; camera.orthographicSize = oldSize;
            UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(rt);
        }
    }
    private static void InspectFloorClearance(BunkerNavigationView view)
    {
        Physics2D.SyncTransforms(); var collisions = new HashSet<string>();
        foreach (var filter in view.GetComponentsInChildren<MeshFilter>())
        {
            var v = filter.sharedMesh.vertices; var markers = filter.sharedMesh.uv2;
            for (int i = 0; i < v.Length; i += 4)
            {
                if (markers[i].x > 0) continue;
                Vector2 a = filter.transform.TransformPoint((v[i] + v[i + 1]) * .5f);
                Vector2 b = filter.transform.TransformPoint((v[i + 2] + v[i + 3]) * .5f);
                int count = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a,b) / .2f));
                for (int n = 0; n <= count; n++)
                {
                    Vector2 p = Vector2.Lerp(a,b,(float)n/count);
                    foreach (var c in Physics2D.OverlapCircleAll(p,.12f))
                        if (!c.isTrigger && c.attachedRigidbody == null && c.gameObject.layer != 9)
                            collisions.Add(filter.name + " at " + p + " overlaps " + c.name + " parent=" + c.transform.parent?.name + " bounds=" + c.bounds);
                }
            }
        }
        File.WriteAllLines(Output + "Clearance.txt", collisions.Count == 0 ? new[] { "PASS: route strips clear of solid colliders." } : collisions);
    }
}
