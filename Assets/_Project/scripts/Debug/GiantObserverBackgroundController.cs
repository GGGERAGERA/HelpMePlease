using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
public sealed class GiantObserverBackgroundController : MonoBehaviour
{
    public enum ObserverIntensity { Low, Normal, High }

    private enum EventPhase { Hidden, Prepare, LightOn, Observe, LightOff }

    private const float PrepareDuration = 0.3f;
    private const float LightOnDuration = 0.45f;
    private const float ObserveDuration = 2.35f;
    private const float LightOffDuration = 0.55f;
    private const float SensorDelay = 0.2f;
    private const float SensorOffLead = 0.15f;
    private const float PrepareRevealLevel = 0.12f;
    private const float ParallaxFactor = 0.045f;
    private const float ObserverScale = 1.75f;
    private const int BacklightSortingOrder = -195;
    private const int ObserverSortingOrder = -185;

    private readonly List<SpriteRenderer> silhouetteRenderers = new();
    private readonly List<SpriteRenderer> sensorRenderers = new();
    private readonly List<SpriteRenderer> lightRenderers = new();
    private readonly List<Color> silhouetteColors = new();
    private readonly List<Color> sensorColors = new();
    private readonly List<Color> lightColors = new();

    private Camera sandboxCamera;
    private WorldRuleVisual worldRuleVisual;
    private Transform observerRoot;
    private Transform lightRoot;
    private Sprite runtimeSprite;
    private Sprite gradientSprite;
    private Texture2D runtimeTexture;
    private Texture2D gradientTexture;
    private EventPhase phase;
    private ObserverIntensity intensity = ObserverIntensity.Normal;
    private bool observerEnabled;
    private bool autoTrigger;
    private float intervalMin = 10f;
    private float intervalMax = 15f;
    private float phaseStartedAt;
    private float nextTriggerAt;
    private Vector3 eventCameraPosition;
    private Vector3 eventRootPosition;
    private Vector3 eventLightPosition;
    private Color cameraColorBeforeEvent;

    public bool ObserverEnabled => observerEnabled;
    public bool AutoTrigger => autoTrigger;
    public bool IsVisible => phase != EventPhase.Hidden;
    public float IntervalMin => intervalMin;
    public float IntervalMax => intervalMax;
    public ObserverIntensity Intensity => intensity;

    public void Configure(Camera targetCamera, WorldRuleVisual ruleVisual)
    {
        sandboxCamera = targetCamera;
        worldRuleVisual = ruleVisual;
        EnsureVisual();
        SetVisualLevels(0f, 0f, 0f);
        ScheduleNextTrigger();
    }

    public void SetObserverEnabled(bool enabled)
    {
        observerEnabled = enabled;
        if (!enabled)
        {
            HideImmediately();
            return;
        }
        ScheduleNextTrigger();
    }

    public void SetAutoTrigger(bool enabled)
    {
        autoTrigger = enabled;
        if (enabled)
            ScheduleNextTrigger();
    }

    public bool TriggerNow()
    {
        if (!observerEnabled || sandboxCamera == null || phase != EventPhase.Hidden)
            return false;

        EnsureVisual();
        ConfigureRevealComposition();
        cameraColorBeforeEvent = sandboxCamera.backgroundColor;
        eventCameraPosition = sandboxCamera.transform.position;
        eventRootPosition = observerRoot.position;
        eventLightPosition = lightRoot.position;
        BeginPhase(EventPhase.Prepare);
        ScheduleNextTrigger();
        return true;
    }

    public void SetInterval(float minimum, float maximum)
    {
        intervalMin = Mathf.Max(1f, Mathf.Min(minimum, maximum));
        intervalMax = Mathf.Max(intervalMin, Mathf.Max(minimum, maximum));
        ScheduleNextTrigger();
    }

    public void SetIntensity(ObserverIntensity value) => intensity = value;

    public static string GetIntensityName(ObserverIntensity value) => value switch
    {
        ObserverIntensity.Low => "LOW",
        ObserverIntensity.High => "HIGH",
        _ => "NORMAL"
    };

    private void Update()
    {
        if (!observerEnabled || sandboxCamera == null)
            return;

        if (phase == EventPhase.Hidden)
        {
            if (autoTrigger && Time.time >= nextTriggerAt)
                TriggerNow();
            return;
        }

        ApplyParallax();
        float elapsed = Time.time - phaseStartedAt;
        switch (phase)
        {
            case EventPhase.Prepare:
                UpdatePrepare(elapsed);
                break;
            case EventPhase.LightOn:
                UpdateLightOn(elapsed);
                break;
            case EventPhase.Observe:
                SetVisualLevels(1f, 1f, 1f);
                ApplyRevealEnvironment(1f);
                if (elapsed >= ObserveDuration)
                    BeginPhase(EventPhase.LightOff);
                break;
            case EventPhase.LightOff:
                UpdateLightOff(elapsed);
                break;
        }
    }

    private void UpdatePrepare(float elapsed)
    {
        float progress = Mathf.Clamp01(elapsed / PrepareDuration);
        float flicker = 0.12f + 0.055f * Mathf.Sin(progress * Mathf.PI * 5f);
        float light = Smooth01(progress) * Mathf.Max(0.05f, flicker);
        SetVisualLevels(light * 0.22f, 0f, light);
        ApplyRevealEnvironment(light);
        if (elapsed >= PrepareDuration)
            BeginPhase(EventPhase.LightOn);
    }

    private void UpdateLightOn(float elapsed)
    {
        float light = Mathf.Lerp(
            PrepareRevealLevel,
            1f,
            Smooth01(elapsed / LightOnDuration)
        );
        float sensor = Smooth01(
            (elapsed - SensorDelay) / (LightOnDuration - SensorDelay)
        );
        SetVisualLevels(light, sensor, light);
        ApplyRevealEnvironment(light);
        if (elapsed >= LightOnDuration)
            BeginPhase(EventPhase.Observe);
    }

    private void UpdateLightOff(float elapsed)
    {
        float sensor = 1f - Smooth01(elapsed / SensorOffLead);
        float bodyFade = 1f - Smooth01(
            (elapsed - SensorOffLead) / (LightOffDuration - SensorOffLead)
        );
        SetVisualLevels(bodyFade, sensor, bodyFade);
        ApplyRevealEnvironment(bodyFade);
        if (elapsed >= LightOffDuration)
            FinishEvent();
    }

    private void BeginPhase(EventPhase value)
    {
        phase = value;
        phaseStartedAt = Time.time;
    }

    private void FinishEvent()
    {
        SetVisualLevels(0f, 0f, 0f);
        RestoreVisualEnvironment();
        phase = EventPhase.Hidden;
    }

    private void HideImmediately()
    {
        SetVisualLevels(0f, 0f, 0f);
        RestoreVisualEnvironment();
        phase = EventPhase.Hidden;
    }

    private void ScheduleNextTrigger()
    {
        nextTriggerAt = Time.time + Random.Range(intervalMin, intervalMax);
    }

    private void ApplyParallax()
    {
        Vector3 cameraDelta = sandboxCamera.transform.position - eventCameraPosition;
        cameraDelta.z = 0f;
        Vector3 parallaxDelta = cameraDelta * ParallaxFactor;
        observerRoot.position = eventRootPosition + parallaxDelta;
        lightRoot.position = eventLightPosition + parallaxDelta;
    }

    private void ApplyRevealEnvironment(float amount)
    {
        amount = Mathf.Clamp01(amount);
        float backgroundStrength = intensity switch
        {
            ObserverIntensity.Low => 0.32f,
            ObserverIntensity.High => 0.78f,
            _ => 0.56f
        };
        Color coldLight = new(0.18f, 0.29f, 0.39f, cameraColorBeforeEvent.a);
        sandboxCamera.backgroundColor = Color.Lerp(
            cameraColorBeforeEvent,
            coldLight,
            amount * backgroundStrength
        );

        float darknessAtFullReveal = intensity switch
        {
            ObserverIntensity.Low => 0.78f,
            ObserverIntensity.High => 0.4f,
            _ => 0.56f
        };
        worldRuleVisual?.SetDebugDarknessOverlayMultiplier(
            Mathf.Lerp(1f, darknessAtFullReveal, amount)
        );
    }

    private void RestoreVisualEnvironment()
    {
        if (sandboxCamera != null && phase != EventPhase.Hidden)
            sandboxCamera.backgroundColor = cameraColorBeforeEvent;
        worldRuleVisual?.SetDebugDarknessOverlayMultiplier(1f);
    }

    private void ConfigureRevealComposition()
    {
        float halfHeight = sandboxCamera.orthographicSize;
        Vector3 center = sandboxCamera.transform.position;
        center.z = 0f;
        observerRoot.rotation = Quaternion.identity;
        observerRoot.localScale = Vector3.one * ObserverScale;
        observerRoot.position = center + new Vector3(0.8f, halfHeight * 0.65f, 0f);
        lightRoot.rotation = Quaternion.identity;
        lightRoot.localScale = Vector3.one;
        lightRoot.position = center + new Vector3(0f, halfHeight * 0.5f, 0f);
    }

    private void EnsureVisual()
    {
        if (observerRoot != null)
            return;

        CreateRuntimeSprites();
        GameObject lightObject = new("Giant Observer Distant Light (Sandbox Only)");
        lightObject.transform.SetParent(transform, false);
        lightRoot = lightObject.transform;
        GameObject root = new("Giant Observer Background (Sandbox Only)");
        root.transform.SetParent(transform, false);
        observerRoot = root.transform;

        Color shell = new(0.22f, 0.29f, 0.34f, 1f);
        Color shellEdge = new(0.31f, 0.4f, 0.46f, 1f);
        Color shellShadow = new(0.13f, 0.18f, 0.22f, 1f);
        Color sensor = new(0.25f, 0.95f, 1f, 1f);

        CreateLightShape("Cold Technical Backlight", new Vector2(0f, 2f),
            new Vector2(43f, 29f), new Color(0.24f, 0.67f, 0.94f, 1f));
        CreateLightShape("Horizon Light Bank", new Vector2(0f, -5.4f),
            new Vector2(54f, 12f), new Color(0.18f, 0.48f, 0.72f, 1f));
        CreateLightShape("Head Rim Halo", new Vector2(1f, 4.5f),
            new Vector2(24f, 18f), new Color(0.42f, 0.8f, 1f, 1f));

        CreateObserverShape("Head", new Vector2(0f, 1.4f),
            new Vector2(9.8f, 6.6f), 0f, shell);
        CreateObserverShape("Crown", new Vector2(-0.5f, 5f),
            new Vector2(7.8f, 1.25f), -3f, shellEdge);
        CreateObserverShape("Face Shadow", new Vector2(0.3f, 0.1f),
            new Vector2(8.9f, 2.4f), 1f, shellShadow);
        CreateObserverShape("Jaw", new Vector2(0.9f, -2f),
            new Vector2(8.1f, 1.8f), 4f, shellEdge);
        CreateObserverShape("Near Shoulder", new Vector2(1.2f, -4.1f),
            new Vector2(15.2f, 3.1f), -3f, shell);
        CreateObserverShape("Far Shoulder", new Vector2(-4.8f, -4.7f),
            new Vector2(9.2f, 2.7f), 6f, shellShadow);
        CreateObserverShape("Upper Torso", new Vector2(2.1f, -7.2f),
            new Vector2(12.5f, 5.4f), -2f, shellShadow);
        CreateObserverShape("Antenna", new Vector2(-3f, 7f),
            new Vector2(0.72f, 4.4f), -11f, shellEdge);
        CreateObserverShape("Sensor Housing", new Vector2(0.05f, 2.05f),
            new Vector2(5.8f, 2.25f), 1f, shellShadow);
        CreateSensorShape("Primary Sensor Halo", new Vector2(-0.35f, 2.1f),
            new Vector2(3.2f, 2.15f), 1f,
            new Color(0.2f, 0.82f, 1f, 0.24f));
        CreateSensorShape("Primary Sensor", new Vector2(-0.35f, 2.1f),
            new Vector2(2.15f, 1.05f), 1f, sensor);
        CreateSensorShape("Sensor Core", new Vector2(-0.7f, 2.18f),
            new Vector2(0.55f, 0.48f), 1f, Color.white);
    }

    private void CreateRuntimeSprites()
    {
        runtimeTexture = new Texture2D(4, 4, TextureFormat.RGBA32, false)
        {
            name = "Giant Observer Runtime Pixel",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        Color[] pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        runtimeTexture.SetPixels(pixels);
        runtimeTexture.Apply(false, true);
        runtimeSprite = CreateSprite(runtimeTexture, "Giant Observer Runtime Shape");

        const int gradientSize = 64;
        gradientTexture = new Texture2D(gradientSize, gradientSize,
            TextureFormat.RGBA32, false)
        {
            name = "Giant Observer Runtime Light Gradient",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        Color[] gradientPixels = new Color[gradientSize * gradientSize];
        for (int y = 0; y < gradientSize; y++)
        {
            for (int x = 0; x < gradientSize; x++)
            {
                Vector2 normalized = new(
                    (x + 0.5f) / gradientSize * 2f - 1f,
                    (y + 0.5f) / gradientSize * 2f - 1f
                );
                float alpha = Smooth01(1f - Mathf.Clamp01(normalized.magnitude));
                gradientPixels[y * gradientSize + x] =
                    new Color(1f, 1f, 1f, alpha);
            }
        }
        gradientTexture.SetPixels(gradientPixels);
        gradientTexture.Apply(false, true);
        gradientSprite = CreateSprite(gradientTexture,
            "Giant Observer Runtime Light Gradient");
    }

    private static Sprite CreateSprite(Texture2D texture, string spriteName)
    {
        Sprite sprite = Sprite.Create(texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f), texture.width);
        sprite.name = spriteName;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private void CreateLightShape(string shapeName, Vector2 localPosition,
        Vector2 size, Color color)
    {
        SpriteRenderer renderer = CreateRenderer(shapeName, lightRoot,
            gradientSprite, localPosition, size, 0f, color,
            BacklightSortingOrder);
        lightRenderers.Add(renderer);
        lightColors.Add(color);
    }

    private void CreateObserverShape(string shapeName, Vector2 localPosition,
        Vector2 size, float rotation, Color color)
    {
        SpriteRenderer renderer = CreateRenderer(shapeName, observerRoot,
            runtimeSprite, localPosition, size, rotation, color,
            ObserverSortingOrder);
        silhouetteRenderers.Add(renderer);
        silhouetteColors.Add(color);
    }

    private void CreateSensorShape(string shapeName, Vector2 localPosition,
        Vector2 size, float rotation, Color color)
    {
        SpriteRenderer renderer = CreateRenderer(shapeName, observerRoot,
            gradientSprite, localPosition, size, rotation, color,
            ObserverSortingOrder + 1);
        sensorRenderers.Add(renderer);
        sensorColors.Add(color);
    }

    private static SpriteRenderer CreateRenderer(string shapeName,
        Transform parent, Sprite sprite, Vector2 localPosition, Vector2 size,
        float rotation, Color color, int sortingOrder)
    {
        GameObject shape = new(shapeName);
        shape.transform.SetParent(parent, false);
        shape.transform.localPosition = new Vector3(localPosition.x,
            localPosition.y, 0f);
        shape.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        shape.transform.localScale = new Vector3(size.x, size.y, 1f);
        SpriteRenderer renderer = shape.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingLayerName = "Background";
        renderer.sortingOrder = sortingOrder;
        return renderer;
    }

    private void SetVisualLevels(float body, float sensor, float light)
    {
        SetAlpha(silhouetteRenderers, silhouetteColors, body * BodyAlpha());
        SetAlpha(sensorRenderers, sensorColors, sensor * SensorAlpha());
        SetAlpha(lightRenderers, lightColors, light * LightAlpha());
    }

    private static void SetAlpha(List<SpriteRenderer> renderers,
        List<Color> colors, float alpha)
    {
        for (int i = 0; i < renderers.Count; i++)
        {
            if (renderers[i] == null) continue;
            Color color = colors[i];
            color.a *= Mathf.Clamp01(alpha);
            renderers[i].color = color;
            renderers[i].enabled = color.a > 0.001f;
        }
    }

    private float BodyAlpha() => intensity switch
    {
        ObserverIntensity.Low => 0.64f,
        ObserverIntensity.High => 1f,
        _ => 0.9f
    };

    private float SensorAlpha() => intensity switch
    {
        ObserverIntensity.Low => 0.78f,
        ObserverIntensity.High => 1f,
        _ => 0.96f
    };

    private float LightAlpha() => intensity switch
    {
        ObserverIntensity.Low => 0.2f,
        ObserverIntensity.High => 0.52f,
        _ => 0.36f
    };

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private void OnDestroy()
    {
        RestoreVisualEnvironment();
        if (runtimeSprite != null) Destroy(runtimeSprite);
        if (gradientSprite != null) Destroy(gradientSprite);
        if (runtimeTexture != null) Destroy(runtimeTexture);
        if (gradientTexture != null) Destroy(gradientTexture);
    }
}
#endif
