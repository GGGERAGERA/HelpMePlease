#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GoldenPathLabAuthoring
{
    [MenuItem("Tools/Subject42/Golden Path Lab/Open scene")]
    public static void Open()
    {
        if (EditorApplication.isPlaying) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (!File.Exists(GoldenPathLab.ScenePath)) Create();
        EditorSceneManager.OpenScene(GoldenPathLab.ScenePath);
    }

    // Creates only the dev scene; preserves open scenes and production build settings.
    public static void Create()
    {
        if (File.Exists(GoldenPathLab.ScenePath)) return;
        Directory.CreateDirectory(Path.GetDirectoryName(GoldenPathLab.ScenePath));
        var previous = SceneManager.GetActiveScene();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var lab = new GameObject("Golden Path Lab (Editor Only)").AddComponent<GoldenPathLab>();
        lab.Character = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Scriptable Objects/Characters/01_Gera.asset");
        var camera = new GameObject("Lab Camera").AddComponent<Camera>();
        camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.045f, .063f, .095f);
        camera.transform.position = new Vector3(0, 0, -10);
        EditorSceneManager.SaveScene(scene, GoldenPathLab.ScenePath);
        EditorSceneManager.CloseScene(scene, true);
        if (previous.IsValid()) SceneManager.SetActiveScene(previous);
        AssetDatabase.Refresh();
    }
}
#endif
