#if UNITY_EDITOR
using Subject42.DebugLabs.Telekinetic;
using UnityEditor;
using UnityEditor.SceneManagement;

internal static class DirectedStationLabMenu
{
    [MenuItem("Tools/Subject42/Dev/Directed Station Lab/Play")]
    private static void Play()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode ||
            !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        EditorSceneManager.OpenScene(DirectedStationLab.ScenePath);
        BunkerDebugTools.SetGameView1600();
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
        EditorApplication.EnterPlaymode();
    }
}
#endif
