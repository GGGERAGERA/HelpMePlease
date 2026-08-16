using UnityEngine;

/// <summary>
/// Owns only the native cursor presentation for the bunker scene.
/// </summary>
[DisallowMultipleComponent]
public sealed class BunkerCursorView : MonoBehaviour
{
    private void OnEnable()
    {
        ShowSystemCursor();
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus)
            ShowSystemCursor();
    }

    private static void ShowSystemCursor()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
}
