using UnityEngine;
using UnityEngine.UI;

/// <summary>Presentation only: menu controls stay on the stationary canvas above this image.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Image))]
public sealed class StartScreenAtmosphere : MonoBehaviour
{
    [SerializeField] private Vector2 mouseTravel = new Vector2(18f, 10f);
    [SerializeField, Min(.01f)] private float followTime = .35f;
    [SerializeField] private Vector2 clearInterval = new Vector2(10f, 18f);
    [SerializeField, Min(1f)] private float condensationDuration = 6f;
    [SerializeField, Range(0f, .4f)] private float condensationOpacity = .18f;

    private RectTransform background;
    private RectTransform viewport;
    private Image artwork;
    private RawImage condensation;
    private Texture2D condensationTexture;
    private Vector2 velocity;
    private Vector2 viewportSize;
    private float nextCondensation;
    private bool focused = true;

    private void Awake()
    {
        background = (RectTransform)transform;
        viewport = transform.parent as RectTransform;
        artwork = GetComponent<Image>();
        artwork.raycastTarget = false;
        background.anchorMin = background.anchorMax = background.pivot = new Vector2(.5f, .5f);
        CreateCondensation();
        FitBackground();
    }

    private void OnEnable()
    {
        focused = Application.isFocused;
        nextCondensation = Time.unscaledTime + Random.Range(clearInterval.x, clearInterval.y);
    }

    private void LateUpdate()
    {
        if (viewport == null || artwork.sprite == null) return;
        if (viewportSize != viewport.rect.size) FitBackground();

        Vector2 target = Vector2.zero;
        Vector3 pointer = Input.mousePosition;
        if (focused && Screen.width > 0 && Screen.height > 0 &&
            pointer.x >= 0 && pointer.x <= Screen.width && pointer.y >= 0 && pointer.y <= Screen.height)
        {
            target = new Vector2((pointer.x / Screen.width * 2f - 1f) * mouseTravel.x,
                (pointer.y / Screen.height * 2f - 1f) * mouseTravel.y);
        }
        background.anchoredPosition = Vector2.SmoothDamp(background.anchoredPosition, target,
            ref velocity, followTime, Mathf.Infinity, Time.unscaledDeltaTime);

        float elapsed = Time.unscaledTime - nextCondensation;
        if (elapsed >= condensationDuration)
        {
            nextCondensation = Time.unscaledTime + Random.Range(clearInterval.x, clearInterval.y);
            elapsed = -1f;
        }
        float breath = elapsed < 0f ? 0f : Mathf.Sin(Mathf.PI * Mathf.Clamp01(elapsed / condensationDuration));
        condensation.color = new Color(.66f, .85f, .85f, breath * breath * condensationOpacity);
    }

    public static Vector2 CalculateCoverSize(Vector2 viewport, float aspect, Vector2 travel)
    {
        // Overscan covers the screen even at maximum mouse travel; preserve the original aspect.
        float height = Mathf.Max(viewport.y + 2f * Mathf.Abs(travel.y) + 4f,
            (viewport.x + 2f * Mathf.Abs(travel.x) + 4f) / Mathf.Max(.01f, aspect));
        return new Vector2(height * aspect, height);
    }

    private void FitBackground()
    {
        if (viewport == null || artwork.sprite == null) return;
        viewportSize = viewport.rect.size;
        Rect rect = artwork.sprite.rect;
        background.sizeDelta = CalculateCoverSize(viewportSize, rect.width / rect.height, mouseTravel);
    }

    private void CreateCondensation()
    {
        // Build once, animate alpha only. No blur of the artwork or per-frame texture allocations.
        const int width = 256, height = 144;
        condensationTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Capsule condensation mask", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
        };
        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            float u = x / (float)(width - 1), v = y / (float)(height - 1);
            float left = Patch(u, v, .19f, .35f, .17f, .26f);
            float right = Patch(u, v, .82f, .43f, .15f, .3f);
            float lower = Patch(u, v, .48f, .15f, .31f, .1f) * .45f;
            float grain = .55f + .45f * Mathf.PerlinNoise(u * 15f + 3.7f, v * 12f + 9.1f);
            byte alpha = (byte)(255f * Mathf.Clamp01(Mathf.Max(left, right, lower) * grain));
            pixels[y * width + x] = new Color32(255, 255, 255, alpha);
        }
        condensationTexture.SetPixels32(pixels);
        condensationTexture.Apply(false, true);
        var glass = new GameObject("Capsule condensation", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        glass.layer = gameObject.layer;
        glass.transform.SetParent(transform, false);
        condensation = glass.GetComponent<RawImage>();
        condensation.texture = condensationTexture;
        condensation.raycastTarget = false;
        condensation.color = Color.clear;
        RectTransform rectTransform = condensation.rectTransform;
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = rectTransform.offsetMax = Vector2.zero;
    }

    private static float Patch(float u, float v, float cx, float cy, float rx, float ry)
    {
        float dx = (u - cx) / rx, dy = (v - cy) / ry;
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - dx * dx - dy * dy));
    }

    private void OnApplicationFocus(bool hasFocus) => focused = hasFocus;

    private void OnDisable()
    {
        if (background != null) background.anchoredPosition = Vector2.zero;
        velocity = Vector2.zero;
        if (condensation != null) condensation.color = Color.clear;
    }

    private void OnDestroy()
    {
        if (condensation != null) Destroy(condensation.gameObject);
        if (condensationTexture != null) Destroy(condensationTexture);
    }
}
