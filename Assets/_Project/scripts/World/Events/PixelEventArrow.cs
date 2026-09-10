using UnityEngine;
using UnityEngine.UI;

public static class PixelEventArrow
{
    private static Sprite sprite;

    public static void Apply(RectTransform arrow)
    {
        if (arrow == null || !arrow.TryGetComponent<Image>(out var image)) return;
        if (sprite == null)
        {
            var texture = new Texture2D(13, 13, TextureFormat.RGBA32, false)
            {
                name = "Pixel Event Arrow", filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[13 * 13];
            for (int y = 2; y <= 11; y++)
            for (int x = 1; x <= 11; x++)
            {
                bool head = y >= 6 && Mathf.Abs(x - 6) <= 11 - y;
                bool stem = y < 6 && Mathf.Abs(x - 6) <= 1;
                if (head || stem) pixels[y * 13 + x] = new Color32(255, 255, 255, 255);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            sprite = Sprite.Create(texture, new Rect(0, 0, 13, 13), new Vector2(0.5f, 0.5f), 13);
        }
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = true;
        image.material = null;
        if (image.TryGetComponent<Outline>(out var outline)) outline.enabled = false;
    }
}
