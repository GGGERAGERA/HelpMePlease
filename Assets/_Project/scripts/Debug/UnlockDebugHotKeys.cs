using UnityEngine;

public sealed class UnlockDebugHotkeys : MonoBehaviour
{
    private void Update()
    {
#if UNITY_EDITOR
        if (UnlockProgressService.Instance == null)
            return;

        if (Input.GetKeyDown(KeyCode.F6))
        {
            UnlockProgressService.Instance.DebugAddKilledTupiks(100);
            Debug.Log("[DEBUG] Added 100 Tupik kills.");
        }

        if (Input.GetKeyDown(KeyCode.F7))
        {
            UnlockProgressService.Instance.DebugCompleteDarkLevel();
            Debug.Log("[DEBUG] Completed Darkness level modifier.");
        }

        if (Input.GetKeyDown(KeyCode.F8))
        {
            UnlockProgressService.Instance.DebugCompleteRainLevel();
            Debug.Log("[DEBUG] Completed Rain level modifier.");
        }

        if (Input.GetKeyDown(KeyCode.F9))
        {
            UnlockProgressService.Instance.DebugUnlockAll();
            Debug.Log("[DEBUG] Unlock all content.");
        }

        if (Input.GetKeyDown(KeyCode.F10))
        {
            UnlockProgressService.Instance.DebugResetAll();
            Debug.Log("[DEBUG] Reset all unlock progress.");
        }
#endif
    }
}