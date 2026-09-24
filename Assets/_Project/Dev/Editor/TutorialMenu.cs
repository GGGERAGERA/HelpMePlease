#if UNITY_EDITOR
using UnityEditor;

public static class TutorialMenu
{
    [MenuItem("Tools/Subject42/Dev/RESET TUTORIAL")]
    private static void Reset() => TutorialController.ResetCompletion();
}
#endif
