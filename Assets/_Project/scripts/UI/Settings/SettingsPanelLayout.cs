using UnityEngine;

/// <summary>Fits the shared authored settings window within either scene's canvas.</summary>
[DisallowMultipleComponent]
public sealed class SettingsPanelLayout : MonoBehaviour
{
    [SerializeField] private RectTransform window;

    private void OnEnable() => FitWindow();
    private void OnRectTransformDimensionsChange() => FitWindow();

    private void FitWindow()
    {
        if (window == null) return;
        Vector2 bounds = ((RectTransform)transform).rect.size;
        float scale = Mathf.Min(1f, (bounds.x - 48f) / window.sizeDelta.x,
            (bounds.y - 48f) / window.sizeDelta.y);
        window.localScale = Vector3.one * Mathf.Max(.01f, scale);
    }
}
