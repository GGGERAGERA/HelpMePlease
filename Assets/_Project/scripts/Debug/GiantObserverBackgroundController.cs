using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
public sealed class GiantObserverBackgroundController : MonoBehaviour
{
    public enum ObserverIntensity { Low, Normal, High }
    public enum RobotVisibility { Original, Boosted }

    private enum EventPhase { Hidden, Prepare, LightOn, Observe, LightOff }

    private sealed class RendererVisual
    {
        public Renderer Renderer;
        public Material[] Materials;
        public bool[] SensorMaterials;
    }

    private const float PrepareDuration = 0.3f;
    private const float LightOnDuration = 0.45f;
    private const float ObserveDuration = 2.35f;
    private const float LightOffDuration = 0.55f;
    private const float SensorDelay = 0.2f;
    private const float SensorOffLead = 0.15f;
    private const float PrepareRevealLevel = 0.12f;
    private const float ParallaxFactor = 0.045f;
    private const int BacklightSortingOrder = -195;
    private const int ObserverSortingOrder = -185;
    private const float ObserverWorldZ = 4f;
    private const float LightWorldZ = 5f;

    private readonly List<RendererVisual> robotVisuals = new();
    private readonly List<SpriteRenderer> fallbackRenderers = new();
    private readonly List<SpriteRenderer> sensorRenderers = new();
    private readonly List<SpriteRenderer> lightRenderers = new();
    private readonly List<Color> fallbackColors = new();
    private readonly List<Color> sensorColors = new();
    private readonly List<Color> lightColors = new();
    private readonly List<string> disabledRuntimeComponents = new();

    private Camera sandboxCamera;
    private WorldRuleVisual worldRuleVisual;
    private GameObject observerPrefab;
    private Transform observerRoot;
    private Transform lightRoot;
    private GameObject robotInstance;
    private Sprite runtimeSprite;
    private Sprite gradientSprite;
    private Texture2D runtimeTexture;
    private Texture2D gradientTexture;
    private EventPhase phase;
    private ObserverIntensity intensity = ObserverIntensity.Normal;
    private RobotVisibility robotVisibility = RobotVisibility.Original;
    private bool observerEnabled;
    private bool autoTrigger;
    private bool prefabLoaded;
    private bool usesPrefabSensor;
    private float intervalMin = 10f;
    private float intervalMax = 15f;
    private float screenWidthPercent = 65f;
    private float phaseStartedAt;
    private float nextTriggerAt;
    private Vector3 eventCameraPosition;
    private Vector3 eventRootPosition;
    private Vector3 eventLightPosition;
    private Vector2 sourceBoundsSize;
    private Vector3 sourceBoundsCenterOffset;
    private Color cameraColorBeforeEvent;

    public bool ObserverEnabled => observerEnabled;
    public bool AutoTrigger => autoTrigger;
    public bool IsVisible => phase != EventPhase.Hidden;
    public bool PrefabLoaded => prefabLoaded;
    public bool UsesPrefabSensor => usesPrefabSensor;
    public float IntervalMin => intervalMin;
    public float IntervalMax => intervalMax;
    public float ScreenWidthPercent => screenWidthPercent;
    public int RendererCount => robotVisuals.Count;
    public Vector2 SourceBoundsSize => sourceBoundsSize;
    public ObserverIntensity Intensity => intensity;
    public RobotVisibility Visibility => robotVisibility;
    public IReadOnlyList<string> DisabledRuntimeComponents => disabledRuntimeComponents;

    public string CurrentStateName => phase switch
    {
        EventPhase.Prepare => "PREPARE",
        EventPhase.LightOn => "LIGHT",
        EventPhase.Observe => "OBSERVE",
        EventPhase.LightOff => "FADE",
        _ => "HIDDEN"
    };

    public void Configure(Camera targetCamera, WorldRuleVisual ruleVisual,
        GameObject prefab)
    {
        sandboxCamera = targetCamera;
        worldRuleVisual = ruleVisual;
        observerPrefab = prefab;
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

    public void SetScreenWidthPercent(float value)
    {
        screenWidthPercent = Mathf.Clamp(value, 50f, 100f);
        if (phase != EventPhase.Hidden)
        {
            ConfigureRevealComposition();
            eventCameraPosition = sandboxCamera.transform.position;
            eventRootPosition = observerRoot.position;
            eventLightPosition = lightRoot.position;
        }
    }

    public void SetRobotVisibility(RobotVisibility value)
    {
        robotVisibility = value;
        if (phase != EventPhase.Hidden)
            ApplyRobotVisibility(1f, 1f);
    }

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
        float light = Mathf.Lerp(PrepareRevealLevel, 1f,
            Smooth01(elapsed / LightOnDuration));
        float sensor = Smooth01(
            (elapsed - SensorDelay) / (LightOnDuration - SensorDelay));
        SetVisualLevels(light, sensor, light);
        ApplyRevealEnvironment(light);
        if (elapsed >= LightOnDuration)
            BeginPhase(EventPhase.Observe);
    }

    private void UpdateLightOff(float elapsed)
    {
        float sensor = 1f - Smooth01(elapsed / SensorOffLead);
        float bodyFade = 1f - Smooth01(
            (elapsed - SensorOffLead) / (LightOffDuration - SensorOffLead));
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
        sandboxCamera.backgroundColor = Color.Lerp(cameraColorBeforeEvent,
            coldLight, amount * backgroundStrength);

        float darknessAtFullReveal = intensity switch
        {
            ObserverIntensity.Low => 0.78f,
            ObserverIntensity.High => 0.4f,
            _ => 0.56f
        };
        worldRuleVisual?.SetDebugDarknessOverlayMultiplier(
            Mathf.Lerp(1f, darknessAtFullReveal, amount));
    }

    private void RestoreVisualEnvironment()
    {
        if (sandboxCamera != null && phase != EventPhase.Hidden)
            sandboxCamera.backgroundColor = cameraColorBeforeEvent;
        worldRuleVisual?.SetDebugDarknessOverlayMultiplier(1f);
    }

    private void ConfigureRevealComposition()
    {
        float cameraHeight = sandboxCamera.orthographicSize * 2f;
        float cameraWidth = cameraHeight * sandboxCamera.aspect;
        Vector3 cameraCenter = sandboxCamera.transform.position;
        float sourceWidth = Mathf.Max(0.01f, sourceBoundsSize.x);
        float scale = cameraWidth * (screenWidthPercent / 100f) / sourceWidth;

        observerRoot.rotation = Quaternion.identity;
        observerRoot.localScale = Vector3.one * scale;
        Vector3 scaledCenterOffset = sourceBoundsCenterOffset * scale;
        float scaledHeight = sourceBoundsSize.y * scale;
        float desiredBoundsCenterY = cameraCenter.y + sandboxCamera.orthographicSize
            - scaledHeight * 0.42f;
        observerRoot.position = new Vector3(
            cameraCenter.x - scaledCenterOffset.x,
            desiredBoundsCenterY - scaledCenterOffset.y,
            ObserverWorldZ);

        lightRoot.rotation = Quaternion.identity;
        lightRoot.localScale = new Vector3(cameraWidth / 35f,
            cameraHeight / 25f, 1f);
        lightRoot.position = new Vector3(cameraCenter.x,
            cameraCenter.y + sandboxCamera.orthographicSize * 0.55f,
            LightWorldZ);
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

        CreateLightShape("Cold Technical Backlight", new Vector2(0f, 2f),
            new Vector2(43f, 29f), new Color(0.24f, 0.67f, 0.94f, 1f));
        CreateLightShape("Horizon Light Bank", new Vector2(0f, -5.4f),
            new Vector2(54f, 12f), new Color(0.18f, 0.48f, 0.72f, 1f));
        CreateLightShape("Head Rim Halo", new Vector2(1f, 4.5f),
            new Vector2(24f, 18f), new Color(0.42f, 0.8f, 1f, 1f));

        if (!CreatePrefabRobot())
            CreateProceduralFallback();
    }

    private bool CreatePrefabRobot()
    {
        if (observerPrefab == null)
            return false;

        robotInstance = Instantiate(observerPrefab, observerRoot, false);
        robotInstance.name = "p_robot1 (Runtime Observer Visual)";
        robotInstance.transform.localPosition = Vector3.zero;
        DisableRuntimeGameplay(robotInstance);
        CollectPrefabRenderers(robotInstance);
        if (robotVisuals.Count == 0)
        {
            Destroy(robotInstance);
            robotInstance = null;
            return false;
        }

        ConfigurePrefabSorting(robotInstance);
        Bounds bounds = CalculateRendererBounds();
        sourceBoundsSize = new Vector2(bounds.size.x, bounds.size.y);
        sourceBoundsCenterOffset = observerRoot.InverseTransformPoint(bounds.center);
        prefabLoaded = true;
        return true;
    }

    private void DisableRuntimeGameplay(GameObject instance)
    {
        foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
        {
            collider.enabled = false;
            RecordDisabled(collider);
        }
        foreach (Collider2D collider in instance.GetComponentsInChildren<Collider2D>(true))
        {
            collider.enabled = false;
            RecordDisabled(collider);
        }
        foreach (Rigidbody body in instance.GetComponentsInChildren<Rigidbody>(true))
        {
            body.isKinematic = true;
            body.detectCollisions = false;
            body.constraints = RigidbodyConstraints.FreezeAll;
            RecordDisabled(body);
        }
        foreach (Rigidbody2D body in instance.GetComponentsInChildren<Rigidbody2D>(true))
        {
            body.simulated = false;
            body.bodyType = RigidbodyType2D.Kinematic;
            body.constraints = RigidbodyConstraints2D.FreezeAll;
            RecordDisabled(body);
        }
        foreach (Joint joint in instance.GetComponentsInChildren<Joint>(true))
        {
            joint.enableCollision = false;
            RecordDisabled(joint);
        }
        foreach (Joint2D joint in instance.GetComponentsInChildren<Joint2D>(true))
        {
            joint.enabled = false;
            RecordDisabled(joint);
        }
        foreach (MonoBehaviour behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
        {
            behaviour.enabled = false;
            RecordDisabled(behaviour);
        }
    }

    private void RecordDisabled(Component component)
    {
        string name = component.GetType().Name;
        if (!disabledRuntimeComponents.Contains(name))
            disabledRuntimeComponents.Add(name);
    }

    private void CollectPrefabRenderers(GameObject instance)
    {
        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer || renderer is TrailRenderer ||
                renderer is LineRenderer)
                continue;

            Material[] materials = renderer.sharedMaterials;
            bool[] sensorMaterials = FindSensorMaterials(renderer, materials);
            robotVisuals.Add(new RendererVisual
            {
                Renderer = renderer,
                Materials = materials,
                SensorMaterials = sensorMaterials
            });
            foreach (bool sensorMaterial in sensorMaterials)
                usesPrefabSensor |= sensorMaterial;
        }
    }

    private static bool[] FindSensorMaterials(Renderer renderer, Material[] materials)
    {
        bool[] result = new bool[materials.Length];
        string rendererName = renderer.name.ToLowerInvariant();
        bool sensorRenderer = rendererName.Contains("eye") ||
            rendererName.Contains("sensor");
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            result[i] = sensorRenderer;
            if (material == null) continue;
            string materialName = material.name.ToLowerInvariant();
            if (materialName.Contains("eye") || materialName.Contains("sensor"))
                result[i] = true;
        }
        return result;
    }

    private void ConfigurePrefabSorting(GameObject instance)
    {
        int minimumOrder = int.MaxValue;
        foreach (RendererVisual visual in robotVisuals)
            minimumOrder = Mathf.Min(minimumOrder, visual.Renderer.sortingOrder);
        if (minimumOrder == int.MaxValue) minimumOrder = 0;

        foreach (RendererVisual visual in robotVisuals)
        {
            int relativeOrder = visual.Renderer.sortingOrder - minimumOrder;
            visual.Renderer.sortingLayerName = "Background";
            visual.Renderer.sortingOrder = ObserverSortingOrder + relativeOrder;
        }

        SortingGroup[] groups = instance.GetComponentsInChildren<SortingGroup>(true);
        int minimumGroupOrder = int.MaxValue;
        foreach (SortingGroup group in groups)
            minimumGroupOrder = Mathf.Min(minimumGroupOrder, group.sortingOrder);
        if (minimumGroupOrder == int.MaxValue) minimumGroupOrder = 0;
        foreach (SortingGroup group in groups)
        {
            int relativeOrder = group.sortingOrder - minimumGroupOrder;
            group.sortingLayerName = "Background";
            group.sortingOrder = ObserverSortingOrder + relativeOrder;
        }
    }

    private Bounds CalculateRendererBounds()
    {
        Bounds bounds = robotVisuals[0].Renderer.bounds;
        for (int i = 1; i < robotVisuals.Count; i++)
            bounds.Encapsulate(robotVisuals[i].Renderer.bounds);
        return bounds;
    }

    private void CreateProceduralFallback()
    {
        prefabLoaded = false;
        sourceBoundsSize = new Vector2(16f, 15.5f);
        sourceBoundsCenterOffset = new Vector3(0f, -0.25f, 0f);
        Color shell = new(0.22f, 0.29f, 0.34f, 1f);
        Color shadow = new(0.13f, 0.18f, 0.22f, 1f);
        CreateFallbackShape("Fallback Head", new Vector2(0f, 1.4f),
            new Vector2(9.8f, 6.6f), shell);
        CreateFallbackShape("Fallback Shoulders", new Vector2(0f, -4.1f),
            new Vector2(16f, 3.1f), shell);
        CreateFallbackShape("Fallback Upper Torso", new Vector2(1f, -7.2f),
            new Vector2(12.5f, 5.4f), shadow);
        CreateSensorShape("Fallback Sensor Halo", new Vector2(-0.35f, 2.1f),
            new Vector2(3.2f, 2.15f), new Color(0.2f, 0.82f, 1f, 0.24f));
        CreateSensorShape("Fallback Sensor", new Vector2(-0.35f, 2.1f),
            new Vector2(2.15f, 1.05f), new Color(0.25f, 0.95f, 1f, 1f));
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
                Vector2 normalized = new((x + 0.5f) / gradientSize * 2f - 1f,
                    (y + 0.5f) / gradientSize * 2f - 1f);
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

    private void CreateLightShape(string name, Vector2 position, Vector2 size,
        Color color)
    {
        SpriteRenderer renderer = CreateRenderer(name, lightRoot, gradientSprite,
            position, size, color, BacklightSortingOrder);
        lightRenderers.Add(renderer);
        lightColors.Add(color);
    }

    private void CreateFallbackShape(string name, Vector2 position, Vector2 size,
        Color color)
    {
        SpriteRenderer renderer = CreateRenderer(name, observerRoot, runtimeSprite,
            position, size, color, ObserverSortingOrder);
        fallbackRenderers.Add(renderer);
        fallbackColors.Add(color);
    }

    private void CreateSensorShape(string name, Vector2 position, Vector2 size,
        Color color)
    {
        SpriteRenderer renderer = CreateRenderer(name, observerRoot, gradientSprite,
            position, size, color, ObserverSortingOrder + 1);
        sensorRenderers.Add(renderer);
        sensorColors.Add(color);
    }

    private static SpriteRenderer CreateRenderer(string name, Transform parent,
        Sprite sprite, Vector2 position, Vector2 size, Color color, int order)
    {
        GameObject shape = new(name);
        shape.transform.SetParent(parent, false);
        shape.transform.localPosition = new Vector3(position.x, position.y, 0f);
        shape.transform.localScale = new Vector3(size.x, size.y, 1f);
        SpriteRenderer renderer = shape.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingLayerName = "Background";
        renderer.sortingOrder = order;
        return renderer;
    }

    private void SetVisualLevels(float body, float sensor, float light)
    {
        ApplyRobotVisibility(body, sensor);
        SetAlpha(fallbackRenderers, fallbackColors, body * BodyAlpha());
        SetAlpha(sensorRenderers, sensorColors, sensor * SensorAlpha());
        SetAlpha(lightRenderers, lightColors, light * LightAlpha());
    }

    private void ApplyRobotVisibility(float body, float sensor)
    {
        bool bodyVisible = body > 0.015f;
        bool sensorVisible = sensor > 0.015f;
        foreach (RendererVisual visual in robotVisuals)
        {
            visual.Renderer.enabled = bodyVisible;
            ApplyMaterialAppearance(visual,
                bodyVisible && robotVisibility == RobotVisibility.Boosted,
                sensorVisible);
        }
    }

    private static void ApplyMaterialAppearance(RendererVisual visual,
        bool boosted, bool sensorVisible)
    {
        for (int i = 0; i < visual.Materials.Length; i++)
        {
            Material material = visual.Materials[i];
            bool hiddenSensor = visual.SensorMaterials[i] && !sensorVisible;
            if ((!boosted && !hiddenSensor) || material == null)
            {
                visual.Renderer.SetPropertyBlock(null, i);
                continue;
            }

            MaterialPropertyBlock block = new();
            if (hiddenSensor)
            {
                if (material.HasProperty("_BaseColor"))
                    block.SetColor("_BaseColor", Color.black);
                if (material.HasProperty("_Color"))
                    block.SetColor("_Color", Color.black);
                if (material.HasProperty("_EmissionColor"))
                    block.SetColor("_EmissionColor", Color.black);
                visual.Renderer.SetPropertyBlock(block, i);
                continue;
            }
            if (material.HasProperty("_BaseColor"))
                block.SetColor("_BaseColor", BoostColor(material.GetColor("_BaseColor")));
            if (material.HasProperty("_Color"))
                block.SetColor("_Color", BoostColor(material.GetColor("_Color")));
            if (material.HasProperty("_EmissionColor"))
                block.SetColor("_EmissionColor",
                    material.GetColor("_EmissionColor") * 1.18f);
            visual.Renderer.SetPropertyBlock(block, i);
        }
    }

    private static Color BoostColor(Color color)
    {
        return new Color(Mathf.Min(color.r * 1.22f + 0.035f, 1f),
            Mathf.Min(color.g * 1.22f + 0.035f, 1f),
            Mathf.Min(color.b * 1.22f + 0.035f, 1f), color.a);
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
