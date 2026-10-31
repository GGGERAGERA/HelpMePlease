using TMPro;
using UnityEngine;

/// <summary>Screen-facing pixel panel follows a world speaker, without raycasts.</summary>
[DisallowMultipleComponent]
public sealed class BunkerBarkView : MonoBehaviour
{
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private RectTransform bubble;
    [SerializeField] private TMP_Text label;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private Vector3 worldOffset = new(0f, 1.15f, 0f);

    public void SetText(string text) => label.text = text;

    public void Present(Transform speaker, float alpha)
    {
        group.interactable = group.blocksRaycasts = false;
        if (speaker == null || worldCamera == null) { Hide(); return; }
        Vector3 screen = worldCamera.WorldToScreenPoint(speaker.position + worldOffset);
        if (screen.z <= 0f) { Hide(); return; }
        var rect = (RectTransform)canvas.transform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, null, out Vector2 local);
        Vector2 half = bubble.rect.size * .5f;
        local.x = Mathf.Clamp(local.x, rect.rect.xMin + half.x + 8f, rect.rect.xMax - half.x - 8f);
        local.y = Mathf.Clamp(local.y, rect.rect.yMin + 12f, rect.rect.yMax - bubble.rect.height - 8f);
        bubble.anchoredPosition = new Vector2(Mathf.Round(local.x), Mathf.Round(local.y));
        group.alpha = alpha;
    }

    public void Hide() { if (group != null) group.alpha = 0f; }
}
