#if UNITY_EDITOR
using UnityEditor;

// Discard only after all play-scene teardown callbacks: their saves must remain isolated.
[InitializeOnLoad]
public static class BunkerDebugLifecycle
{
    static BunkerDebugLifecycle()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) PlayerPrefs.EndSandbox();
        };
    }
}
#endif
