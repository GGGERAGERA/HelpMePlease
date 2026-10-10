using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Presentation only: menu controls stay on the stationary canvas above this image.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(Image))]
public sealed class StartScreenAtmosphere : MonoBehaviour
{
    [Header("Mouse Parallax (canvas pixels at 1080p)")]
    [SerializeField] private Vector2 mouseTravel = new Vector2(27f, 15f);
    [SerializeField, Min(.01f)] private float followTime = .35f;
    [SerializeField] private Vector2 clearInterval = new Vector2(10f, 18f);
    [SerializeField, Min(1f)] private float condensationDuration = 6f;
    [SerializeField, Range(0f, .4f)] private float condensationOpacity = .18f;
    [SerializeField] private Image hand;

    [Header("Palm Contact (canvas pixels at 1080p)")]
    [SerializeField, Min(.01f)] private float contactDuration = .08f;
    [SerializeField, Min(0f)] private float handApproachDistance = 36f;
    [SerializeField, Range(.5f, 1f)] private float handApproachScale = .86f;
    [SerializeField] private Vector2 impactTravel = new Vector2(12f, 6f);
    [SerializeField, Min(.01f)] private float strongShakeDuration = .1f;
    [SerializeField, Min(.01f)] private float shakeDuration = .35f;
    [SerializeField, Min(1f)] private float shakeFrequency = 18f;

    [Header("After Contact")]
    [SerializeField, Min(0f)] private float handHoldDuration = .45f;
    [SerializeField, Min(0f)] private float fadeDuration = 1f;
    [SerializeField] private AudioCueId glassBreakCue = AudioCueId.CapsuleGlassBreak;
    [SerializeField, Min(0f)] private float blackHoldDuration = 2f;
    public float FadeDuration => fadeDuration;

    private RectTransform background;
    private RectTransform viewport;
    private Image artwork;
    [SerializeField] private RawImage condensation;
    private Texture2D condensationTexture;
    private Vector2 velocity;
    private Vector2 viewportSize;
    private float nextCondensation;
    private bool focused = true;
    private bool awakening;
    private float awakeningStartedAt;
    private Vector2 awakeningFramePosition;
    private Vector2 handRestPosition;
    private Vector3 handRestScale;

    private void Awake()
    {
        background = (RectTransform)transform;
        viewport = transform.parent as RectTransform;
        artwork = GetComponent<Image>();
        artwork.raycastTarget = false;
        handRestPosition = hand.rectTransform.anchoredPosition;
        handRestScale = hand.rectTransform.localScale;
        ResetAwakening();
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
        if (awakening)
        {
            float age = Time.unscaledTime - awakeningStartedAt;
            float approach = Mathf.Clamp01(age / contactDuration);
            // Accelerate into the glass, then hold the authored pose throughout the fade.
            approach *= approach;
            hand.color = new Color(1f, 1f, 1f, Mathf.Clamp01(age / (contactDuration * .4f)));
            hand.rectTransform.localScale = handRestScale * Mathf.Lerp(handApproachScale, 1f, approach);
            hand.rectTransform.anchoredPosition = handRestPosition + Vector2.down * (handApproachDistance * (1f - approach));
            background.anchoredPosition = awakeningFramePosition + ContactShake(age - contactDuration);
        }
        else
            background.anchoredPosition = Vector2.SmoothDamp(background.anchoredPosition, target,
                ref velocity, followTime, Mathf.Infinity, Time.unscaledDeltaTime);

        float elapsed = Time.unscaledTime - nextCondensation;
        if (elapsed >= condensationDuration)
        {
            nextCondensation = Time.unscaledTime + Random.Range(clearInterval.x, clearInterval.y);
            elapsed = -1f;
        }
        float breath = elapsed < 0f ? 0f : Mathf.Sin(Mathf.PI * Mathf.Clamp01(elapsed / condensationDuration));
        if (awakening)
            breath = .75f * Mathf.Sin(Mathf.PI * Mathf.Clamp01((Time.unscaledTime - awakeningStartedAt) / 2.4f));
        condensation.color = new Color(.66f, .85f, .85f, breath * breath * condensationOpacity);
    }

    public IEnumerator PlayAwakening()
    {
        awakening = true;
        awakeningStartedAt = Time.unscaledTime;
        awakeningFramePosition = background.anchoredPosition;
        bool contacted = false;
        // Even a zero hold must allow the contact frame and its full shake to finish.
        float holdUntil = contactDuration + Mathf.Max(handHoldDuration, shakeDuration);
        while (Time.unscaledTime - awakeningStartedAt <= holdUntil)
        {
            float age = Time.unscaledTime - awakeningStartedAt;
            if (!contacted && age >= contactDuration)
            {
                contacted = true;
                AudioService.Instance.Play(AudioCueId.CapsuleGlassTap);
                AudioService.Instance.Play(AudioCueId.CapsuleBodyResonance);
            }
            yield return null;
        }
    }

    public void FadeMenuMusic(float progress) => AudioSettingsService.Instance.SetMusicGain(1f - progress);

    public IEnumerator PlayGlassBreak()
    {
        // The shared overlay has already submitted a fully opaque frame.
        // The persistent SFX pool retains the tail when StartScreen unloads.
        AudioService.Instance.Play(glassBreakCue);
        yield return new WaitForSecondsRealtime(blackHoldDuration);
    }

    private Vector2 ContactShake(float age)
    {
        if (age < 0f || age >= shakeDuration) return Vector2.zero;
        float strong = Mathf.Min(strongShakeDuration, shakeDuration);
        float envelope = age < strong
            ? Mathf.Lerp(1f, .55f, age / strong)
            : .55f * (1f - Mathf.SmoothStep(0f, 1f, (age - strong) / (shakeDuration - strong)));
        float phase = age * shakeFrequency * Mathf.PI * 2f;
        return new Vector2(impactTravel.x * Mathf.Cos(phase), -impactTravel.y * Mathf.Cos(phase * .77f)) * envelope;
    }

    public void ResetAwakening()
    {
        if (awakening && !SceneTransitionOverlay.IsTransitioning)
            AudioSettingsService.Instance?.SetMusicGain(1f);
        awakening = false;
        hand.color = Color.clear;
        hand.rectTransform.anchoredPosition = handRestPosition;
        hand.rectTransform.localScale = handRestScale;
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
        // Contact can occur at either parallax extreme. Cover their combined displacement.
        Vector2 travel = new Vector2(Mathf.Abs(mouseTravel.x) + Mathf.Abs(impactTravel.x),
            Mathf.Abs(mouseTravel.y) + Mathf.Abs(impactTravel.y));
        background.sizeDelta = CalculateCoverSize(viewportSize, rect.width / rect.height, travel);
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
        condensation.texture = condensationTexture;
    }

    private static float Patch(float u, float v, float cx, float cy, float rx, float ry)
    {
        float dx = (u - cx) / rx, dy = (v - cy) / ry;
        return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(1f - dx * dx - dy * dy));
    }

    private void OnApplicationFocus(bool hasFocus) => focused = hasFocus;

    private void OnDisable()
    {
        ResetAwakening();
        if (background != null) background.anchoredPosition = Vector2.zero;
        velocity = Vector2.zero;
        if (condensation != null) condensation.color = Color.clear;
    }

    private void OnDestroy()
    {
        if (condensationTexture != null) Destroy(condensationTexture);
    }
}
