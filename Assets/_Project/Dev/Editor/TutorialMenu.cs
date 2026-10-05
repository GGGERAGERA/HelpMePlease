#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class TutorialMenu
{
    [MenuItem("Tools/Subject42/Dev/RESET TUTORIAL")]
    private static void Reset() => TutorialController.ResetCompletion();
    [MenuItem("Tools/Subject42/Dev/RESET CONTROL ONBOARDING")]
    private static void ResetControls()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:OnboardingStepDefinition"))
        {
            var step = AssetDatabase.LoadAssetAtPath<OnboardingStepDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            PlayerPrefs.DeleteKey(MetaProgressionManager.TutorialCompletionKeyPrefix + step.Id);
        }
        PlayerPrefs.Save();
        Debug.Log("[Onboarding] Controls reset; re-enter Bunker or start a new run.");
    }
}
#endif
