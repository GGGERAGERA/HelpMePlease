using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Subject42UITheme;

/// <summary>Shared Canvas treatments for dark panels, action buttons and readable text.</summary>
internal static class Subject42UIStyle
{
    private static readonly Dictionary<int, Sprite> Corners = new();

    internal static Image Panel(RectTransform rect) => Frame(rect, Background, Border, 9, 2).border;

    internal static Button ActionButton(RectTransform rect)
    {
        var (border, fill) = Frame(rect, ActionSurface, ActionBorder, 6, 1);
        border.raycastTarget = true;
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = fill;
        return button;
    }

    internal static TMP_Text Text(RectTransform rect, TMP_FontAsset font, Material material, float size, Color color,
        bool bold = false, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
    {
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        if (material != null) text.fontSharedMaterial = material;
        text.fontSize = size;
        text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static (Image border, Image fill) Frame(RectTransform rect, Color fill, Color stroke, int radius, int width)
    {
        var frame = Paint(rect, stroke, radius);
        var inset = new GameObject("Fill", typeof(RectTransform)) { layer = rect.gameObject.layer };
        var inner = (RectTransform)inset.transform;
        inner.SetParent(rect, false);
        inner.anchorMin = Vector2.zero;
        inner.anchorMax = Vector2.one;
        inner.offsetMin = Vector2.one * width;
        inner.offsetMax = -Vector2.one * width;
        return (frame, Paint(inner, fill, radius - width));
    }

    private static Image Paint(RectTransform rect, Color tint, int radius)
    {
        var image = rect.gameObject.AddComponent<Image>();
        image.sprite = RoundedSlice(radius);
        image.type = Image.Type.Sliced;
        image.color = tint;
        image.raycastTarget = false;
        return image;
    }

    private static Sprite RoundedSlice(int radius)
    {
        if (Corners.TryGetValue(radius, out var sprite) && sprite != null) return sprite;

        // The one-pixel center stretches, while each corner retains its authored radius.
        int size = radius * 2 + 1;
        var mask = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = $"UI corner mask {radius}",
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color32[size * size];
        const int samples = 8;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int covered = 0;
            for (int sy = 0; sy < samples; sy++)
            for (int sx = 0; sx < samples; sx++)
            {
                float dx = Mathf.Max(0f, Mathf.Abs(x + (sx + .5f) / samples - size * .5f) - .5f);
                float dy = Mathf.Max(0f, Mathf.Abs(y + (sy + .5f) / samples - size * .5f) - .5f);
                if (dx * dx + dy * dy <= radius * radius) covered++;
            }
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(255f * covered / (samples * samples)));
        }
        mask.SetPixels32(pixels);
        mask.Apply(false, true);

        sprite = Sprite.Create(mask, new Rect(0, 0, size, size), new Vector2(.5f, .5f), 100f, 0,
            SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        sprite.hideFlags = HideFlags.HideAndDontSave;
        Corners[radius] = sprite;
        return sprite;
    }
}
