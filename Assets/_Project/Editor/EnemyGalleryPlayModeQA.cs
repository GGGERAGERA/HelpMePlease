using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Opt-in, real-time scene soak; never runs as part of normal Gallery playback.</summary>
[InitializeOnLoad]
public static class EnemyGalleryPlayModeQA
{
    private const string Output = "Artifacts/EnemyGallery/";
    private const string Session = "EnemyGalleryQA";
    private static EnemyGalleryController gallery;
    private static Vector3[] anchors;
    private static HashSet<string>[] frames;
    private static readonly List<string> messages = new();
    private static readonly List<string> errors = new();
    private static readonly List<string> warnings = new();
    private static double started, nextSample;
    private static int zone = -1;
    private static Quaternion[] pivots;
    private static bool[] pivotMoved;

    static EnemyGalleryPlayModeQA()
    {
        EditorApplication.update += Tick;
        EditorApplication.playModeStateChanged += state =>
        {
            if (!SessionState.GetBool(Session, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                started = EditorApplication.timeSinceStartup; nextSample = started + 2;
                gallery = null; zone = -1;
                messages.Clear(); errors.Clear(); warnings.Clear();
                Application.logMessageReceived += Log;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                Application.logMessageReceived -= Log;
                SessionState.SetBool(Session, false);
                File.WriteAllText(Output + "complete.txt", "Returned to Edit Mode.");
            }
        };
    }
    [MenuItem("Tools/Subject42/Validate Enemy Gallery (3 minute Play Mode)")]
    public static void Start()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Already playing.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits before QA.");
        Directory.CreateDirectory(Output);
        File.WriteAllText(Output + "play.txt", "Validation starting.\n");
        EditorSceneManager.OpenScene(EnemyGalleryAuthoring.ScenePath, OpenSceneMode.Single);
        var authored = SceneManager.GetActiveScene().GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<EnemyGalleryController>()).Single();
        var sources = authored.Exhibits.Select(e => PrefabUtility.GetCorrespondingObjectFromSource(e.instance)).ToArray();
        if (sources.Any(p => p == null) || sources.Distinct().Count() != sources.Length ||
            !sources.OrderBy(p => p.name).SequenceEqual(EnemyGalleryAuthoring.FindEnemies().OrderBy(p => p.name)))
            throw new InvalidOperationException("Authored lineup must contain exactly one linked instance of every production prefab.");
        File.WriteAllText(Output + "edit.txt", "PASS exactly one linked scene instance per production enemy prefab.");
        File.Delete(Output + "complete.txt");
        SessionState.SetBool(Session, true);
        EditorApplication.isPlaying = true;
    }
    private static void Log(string condition, string trace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors.Add(condition + "\n" + trace);
        if (type == LogType.Warning) warnings.Add(condition);
    }
    private static void Check(bool condition, string message)
    {
        messages.Add((condition ? "PASS " : "FAIL ") + message);
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Tick()
    {
        if (!EditorApplication.isCompiling && !EditorApplication.isUpdating && !EditorApplication.isPlayingOrWillChangePlaymode && File.Exists(Output + "play.request"))
        {
            File.Delete(Output + "play.request");
            try { Start(); } catch (Exception e) { File.WriteAllText(Output + "play.txt", e.ToString()); }
        }
        if (!SessionState.GetBool(Session, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.timeSinceStartup < nextSample) return;
        nextSample = EditorApplication.timeSinceStartup + .07;
        try { Sample(); }
        catch (Exception e) { errors.Add(e.ToString()); Finish(); }
    }
    private static void Sample()
    {
        if (gallery == null)
        {
            gallery = UnityEngine.Object.FindFirstObjectByType<EnemyGalleryController>();
            Check(gallery != null && gallery.Player.isActiveAndEnabled, "Production player and movement active");
            Check(gallery.Exhibits.Length == EnemyGalleryAuthoring.FindEnemies().Length, "All production enemies present exactly once: " + gallery.Exhibits.Length);
            anchors = gallery.Exhibits.Select(e => e.instance.transform.position).ToArray();
            frames = gallery.Exhibits.Select(_ => new HashSet<string>()).ToArray();
            pivots = gallery.Exhibits.Select(e => e.motionPivot != null ? e.motionPivot.localRotation : Quaternion.identity).ToArray();
            pivotMoved = new bool[frames.Length];
            foreach (var e in gallery.Exhibits)
            {
                // Unity strips prefab connection metadata on entering Play Mode; verify links in Edit Mode above.
                var prefab = EnemyGalleryAuthoring.FindEnemies().Single(p => p.name == e.instance.name);
                Check(e.instance.transform.localScale == prefab.transform.localScale, e.instance.name + " root scale matches prefab");
                foreach (var t in e.instance.GetComponentsInChildren<Transform>(true).Where(t => t != e.instance.transform))
                {
                    var source = prefab.transform.Find(AnimationUtility.CalculateTransformPath(t, e.instance.transform));
                    Check(source != null && t.localScale == source.localScale, e.instance.name + "/" + t.name + " visual scale matches prefab");
                }
            }
            Check(UnityEngine.Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None).Length == 0 &&
                UnityEngine.Object.FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None).Length == 0, "No damage receivers");
            gallery.SetNamesVisible(false); Check(gallery.Exhibits.All(e => !e.label.activeSelf), "Names OFF");
            gallery.SetNamesVisible(true); Check(gallery.Exhibits.All(e => e.label.activeSelf), "Names ON");
            messages.Add("Keyboard movement and F1 key delivery require a separate interactive Game View check.");
        }
        for (int i = 0; i < gallery.Exhibits.Length; i++)
        {
            var e = gallery.Exhibits[i];
            if (e.instance == null || !e.instance.activeInHierarchy || Vector3.Distance(anchors[i], e.instance.transform.position) > .0001f)
                throw new InvalidOperationException("Exhibit moved, died or despawned: " + i);
            if ((e.animator != null && !e.animator.isActiveAndEnabled) || !e.instance.GetComponentsInChildren<SpriteRenderer>().Any(r => r.enabled && r.sprite != null))
                throw new InvalidOperationException("Invisible/stopped exhibit: " + e.instance.name);
            frames[i].Add(string.Join(",", e.instance.GetComponentsInChildren<SpriteRenderer>().Select(r => r.sprite != null ? r.sprite.GetInstanceID() : 0)));
            if (e.motionPivot != null && Quaternion.Angle(pivots[i], e.motionPivot.localRotation) > 2f) pivotMoved[i] = true;
        }
        if (UnityEngine.Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None).Length != 0 ||
            UnityEngine.Object.FindObjectsByType<EnemySpawner>(FindObjectsSortMode.None).Length != 0 ||
            UnityEngine.Object.FindObjectsByType<BaseWeapon>(FindObjectsSortMode.None).Length != 0 ||
            UnityEngine.Object.FindObjectsByType<RunFlowController>(FindObjectsSortMode.None).Length != 0)
            throw new InvalidOperationException("Combat or run system appeared");
        double elapsed = EditorApplication.timeSinceStartup - started;
        int nextZone = Math.Min(gallery.Exhibits.Length - 1, (int)(elapsed / 23));
        if (nextZone != zone)
        {
            if (zone >= 0) Capture(gallery.Exhibits[zone].instance.name);
            zone = nextZone;
            gallery.Player.GetComponent<Rigidbody2D>().position = (Vector2)anchors[zone] + new Vector2(1.7f, -.4f);
        }
        File.WriteAllText(Output + "progress.txt", $"{elapsed:F1}s / 180s; sprites: " + string.Join(", ", frames.Select(f => f.Count)));
        if (elapsed < 180) return;
        Capture(gallery.Exhibits[zone].instance.name);
        for (int i = 0; i < frames.Length; i++)
            if (gallery.Exhibits[i].animator != null) Check(frames[i].Count > 1, gallery.Exhibits[i].instance.name + " changing sprite frames: " + frames[i].Count);
            else Check(pivotMoved[i], gallery.Exhibits[i].instance.name + " production articulated pivot moves; no active production Animator");
        Check(errors.Count == 0, "No Console errors/exceptions during 180 second proximity soak");
        Check(true, "All anchors stable; no enemies disappeared, no projectiles/weapons/spawners/run progression");
        Finish();
    }
    private static void Capture(string name)
    {
        var camera = Camera.main;
        var previous = camera.targetTexture; var active = RenderTexture.active;
        var texture = RenderTexture.GetTemporary(1280, 720, 24);
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            File.WriteAllBytes(Output + name + ".png", image.EncodeToPNG());
        }
        finally { camera.targetTexture = previous; RenderTexture.active = active; RenderTexture.ReleaseTemporary(texture); UnityEngine.Object.DestroyImmediate(image); }
    }
    private static void Finish()
    {
        messages.Add("Console errors: " + errors.Count); messages.AddRange(errors);
        messages.Add("Console warnings: " + warnings.Count); messages.AddRange(warnings.Distinct());
        File.WriteAllLines(Output + "play.txt", messages);
        EditorApplication.isPlaying = false;
    }
}
