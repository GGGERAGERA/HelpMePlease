#if UNITY_EDITOR
using System;
using System.IO;
using Subject42.Dev.VisualEffects;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static partial class VisualEffectsLabAuthoring
{
    static VisualEffectsLabAuthoring() => EditorApplication.update += Poll;

    [MenuItem("Tools/Subject42/Dev/Visual Effects/Rebuild catalog and scenes")]
    public static void BuildAll()
    {
        RequireEditMode();
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            RebuildCatalog();
            BuildEffectsScene();
            BuildShaderScene();
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        Debug.Log("Visual Effects Lab: authored catalog and both scenes. Production Build Settings unchanged.");
    }

    [MenuItem("Tools/Subject42/Dev/Visual Effects/Play Effects Lab")]
    public static void PlayEffects() => PlayScene(EffectLabCatalog.EffectsScenePath);
    [MenuItem("Tools/Subject42/Dev/Visual Effects/Play Shader Lab")]
    public static void PlayShaders() => PlayScene(EffectLabCatalog.ShaderScenePath);

    [MenuItem("Tools/Subject42/Dev/Visual Effects/Rebuild Shader Lab only")]
    public static void RebuildShaders()
    {
        RequireEditMode();
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try { BuildShaderScene(); }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
    }

    private static void PlayScene(string path)
    {
        RequireEditMode();
        BunkerDebugTools.SetGameView1600();
        EditorSceneManager.OpenScene(path);
        EditorApplication.EnterPlaymode();
    }

    private static void RequireEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Stop Play Mode and wait for compilation/import.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save your modified scenes before rebuilding or switching labs.");
    }

    private static void Poll()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
            SessionState.GetBool("Subject42.Verification.AnyRunActive", false)) return;
        string pending = SessionState.GetString(EffectLabCatalog.PendingSceneKey, "");
        if (pending.Length > 0 && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            SessionState.EraseString(EffectLabCatalog.PendingSceneKey);
            try { PlayScene(pending); } catch (Exception exception) { Debug.LogException(exception); }
        }
        string request = EffectLabCatalog.Output + "/editor.request";
        if (!File.Exists(request)) return;
        string command = File.ReadAllText(request).Trim(); File.Delete(request);
        Directory.CreateDirectory(EffectLabCatalog.Output);
        try
        {
            switch (command)
            {
                case "author": BuildAll(); break;
                case "author-shaders": RebuildShaders(); break;
                case "play-effects": PlayEffects(); break;
                case "play-shaders": PlayShaders(); break;
                case "stop": EditorApplication.ExitPlaymode(); break;
                case "capture":
                    if (!EditorApplication.isPlaying) throw new InvalidOperationException("Capture requires Play Mode.");
                    ScreenCapture.CaptureScreenshot(EffectLabCatalog.Output + "/manual.png"); break;
                default: throw new ArgumentException("Unknown lab command: " + command);
            }
            File.WriteAllText(EffectLabCatalog.Output + "/last-command.txt", command + " OK\n" + DateTime.UtcNow.ToString("O"));
        }
        catch (Exception exception)
        {
            File.WriteAllText(EffectLabCatalog.Output + "/last-command.txt", command + " FAILED\n" + exception);
            Debug.LogException(exception);
        }
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
        EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
    }

    private static Camera CreateStage(string title, float cameraSize)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var camera = new GameObject("Lab Camera", typeof(Camera)).GetComponent<Camera>();
        camera.tag = "MainCamera"; camera.orthographic = true; camera.orthographicSize = cameraSize;
        camera.transform.position = new Vector3(0, 0, -18);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f, .055f, .085f);
        camera.nearClipPlane = .1f; camera.farClipPlane = 150;
        camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        var globalLight = new GameObject("Neutral 2D light", typeof(Light2D)).GetComponent<Light2D>();
        globalLight.lightType = Light2D.LightType.Global; globalLight.intensity = 1f;
        var light = new GameObject("Neutral mesh light", typeof(Light)).GetComponent<Light>();
        light.type = LightType.Directional; light.intensity = .8f; light.transform.rotation = Quaternion.Euler(30, -30, 0);
        Label(title, new Vector3(2, 7.3f, 0), 18, 3.3f, new Color(.65f, 1, .93f));
        return camera;
    }

    private static TMP_Text Label(string text, Vector3 position, float width, float fontSize, Color color)
    {
        var label = new GameObject(text, typeof(TextMeshPro)).GetComponent<TextMeshPro>();
        label.transform.position = position; label.rectTransform.sizeDelta = new Vector2(width, 1);
        label.text = text; label.fontSize = fontSize; label.alignment = TextAlignmentOptions.Center;
        label.color = color; label.enableWordWrapping = false;
        label.font = TMP_Settings.defaultFontAsset;
        return label;
    }

    private static void SetReference(SerializedObject data, string field, Object value) => data.FindProperty(field).objectReferenceValue = value;

    private static void BuildEffectsScene()
    {
        var camera = CreateStage("SUBJECT #42 / EFFECTS LAB", 8.5f);
        camera.transform.position = new Vector3(2, 0, -18);
        var root = new GameObject("Effect preview", typeof(EffectLabController));
        var staging = new GameObject("Inactive cloning stage"); staging.SetActive(false);
        var left = new GameObject("Isolated FX").transform; left.position = new Vector3(-1.5f, 1, 0);
        var right = new GameObject("Source with FX").transform; right.position = new Vector3(7, 1, 0);
        var caption = Label("FX PREVIEW", new Vector3(-1.5f, 5.5f, 0), 8, 2.7f, Color.white);
        var sourceCaption = Label("SOURCE VISUAL", new Vector3(7, 5.5f, 0), 8, 2.5f, Color.white);
        Label("ISOLATED ASSET", new Vector3(-1.5f, -3.9f, 0), 7, 2.2f, new Color(.4f, .65f, .75f));
        Label("SOURCE DATA / PRESENTATION ONLY", new Vector3(7, -3.9f, 0), 8, 2.2f, new Color(.4f, .65f, .75f));
        var data = new SerializedObject(root.GetComponent<EffectLabController>());
        // NewScene unloads unreferenced assets; resolve the saved asset after creating the stage.
        SetReference(data, "catalog", AssetDatabase.LoadAssetAtPath<EffectLabCatalog>(EffectLabCatalog.CatalogPath));
        SetReference(data, "previewCamera", camera);
        SetReference(data, "inactiveStaging", staging.transform); SetReference(data, "effectAnchor", left);
        SetReference(data, "callerAnchor", right); SetReference(data, "effectCaption", caption); SetReference(data, "callerCaption", sourceCaption);
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene(), EffectLabCatalog.EffectsScenePath);
    }

}
#endif
