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
    [SerializeField] private Vector3 worldOffset = new(0f, 1.4f, 0f);
    [SerializeField, Min(12f)] private float headGap = 30f;

    private Transform trackedVisual;
    private SpriteRenderer[] speakerSprites;
    private int characterCount;

    public void SetText(string text)
    {
        label.text = text;
        label.maxVisibleCharacters = int.MaxValue;
        // Lay out the complete line once: typing must not move or rewrap the words.
        label.ForceMeshUpdate();
        characterCount = label.textInfo.characterCount;
        label.maxVisibleCharacters = 0;
    }

    public void Reveal(float progress) => label.maxVisibleCharacters =
        Mathf.FloorToInt(Mathf.Clamp01(progress) * characterCount);

    public void Present(Transform speaker, float alpha)
    {
        group.interactable = group.blocksRaycasts = false;
        if (speaker == null || worldCamera == null) { Hide(); return; }
        var movement = speaker.GetComponent<CharacterMovement2D>();
        Transform visual = movement != null && movement.VisualRoot != null ? movement.VisualRoot : speaker;
        if (trackedVisual != visual)
        {
            trackedVisual = visual;
            speakerSprites = visual.GetComponentsInChildren<SpriteRenderer>();
        }
        Vector3 anchor = speaker.position + worldOffset;
        if (speakerSprites != null)
            foreach (var sprite in speakerSprites)
                if (sprite != null && sprite.enabled && sprite.gameObject.activeInHierarchy && !sprite.forceRenderingOff)
                    anchor.y = Mathf.Max(anchor.y, sprite.bounds.max.y);
        Vector3 screen = worldCamera.WorldToScreenPoint(anchor);
        if (screen.z <= 0f) { Hide(); return; }
        var rect = (RectTransform)canvas.transform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, screen, null, out Vector2 local);
        local.y += headGap;
        Vector2 half = bubble.rect.size * .5f;
        local.x = Mathf.Clamp(local.x, rect.rect.xMin + half.x + 8f, rect.rect.xMax - half.x - 8f);
        local.y = Mathf.Clamp(local.y, rect.rect.yMin + 12f, rect.rect.yMax - bubble.rect.height - 8f);
        bubble.anchoredPosition = new Vector2(Mathf.Round(local.x), Mathf.Round(local.y));
        group.alpha = alpha;
    }

    public void Hide() { if (group != null) group.alpha = 0f; }
}
