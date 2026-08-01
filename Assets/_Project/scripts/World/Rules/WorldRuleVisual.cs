using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WorldRuleVisual : MonoBehaviour
{
    private static readonly int VisualTimeId =
        Shader.PropertyToID("_VisualTime");
    private static readonly int SnowIntensityId =
        Shader.PropertyToID("_SnowIntensity");
    private static readonly int SnowColorId =
        Shader.PropertyToID("_SnowColor");
    private static readonly int SnowDensityId =
        Shader.PropertyToID("_SnowDensity");
    private static readonly int SnowScaleId =
        Shader.PropertyToID("_SnowScale");
    private static readonly int WetGroundIntensityId =
        Shader.PropertyToID("_WetGroundIntensity");
    private static readonly int WetPatternScaleId =
        Shader.PropertyToID("_WetPatternScale");
    private static readonly int RainDropsIntensityId =
        Shader.PropertyToID("_RainDropsIntensity");
    private static readonly int RainDropsFrequencyId =
        Shader.PropertyToID("_RainDropsFrequency");
    private static readonly int GoldenOverlayIntensityId =
        Shader.PropertyToID("_GoldenOverlayIntensity");
    private static readonly int GoldenOverlayColorId =
        Shader.PropertyToID("_GoldenOverlayColor");
    private static readonly int WindVisualIntensityId =
        Shader.PropertyToID("_WindVisualIntensity");
    private static readonly int WindLineDensityId =
        Shader.PropertyToID("_WindLineDensity");
    private static readonly int WindLineSpeedId =
        Shader.PropertyToID("_WindLineSpeed");
    private static readonly int WindDirectionId =
        Shader.PropertyToID("_WindDirection");

    [Header("References")]
    [SerializeField] private Image fullscreenImage;
    [SerializeField] private Material visualMaterial;
    [SerializeField] private WindRuleIndicator windIndicator;

    [Header("Rain / Existing Scene Effect")]
    [SerializeField] private GameObject rainEffect;

    [Header("Rain / Wet Ground")]
    [SerializeField] private Material rainWorldMaterial;
    [SerializeField, Range(0f, 1f)] private float wetGroundIntensity = 0.32f;
    [SerializeField, Range(0.25f, 8f)] private float wetPatternScale = 2.8f;

    [Header("Rain / Screen Drops")]
    [SerializeField, Range(0f, 0.5f)] private float screenDropsIntensity = 0.14f;
    [SerializeField, Range(0.05f, 2f)] private float screenDropsFrequency = 0.35f;

    [Header("Golden / World Visual")]
    [SerializeField, Range(0f, 0.2f)] private float goldenOverlayIntensity = 0.07f;
    [SerializeField, ColorUsage(false, true)] private Color goldenColorFilter =
        new Color(1f, 0.94f, 0.78f, 1f);

    [Header("Wind / Screen Flow")]
    [SerializeField, Range(0f, 0.4f)] private float windVisualIntensity = 0.16f;
    [SerializeField, Range(2f, 12f)] private float windLineDensity = 5.5f;
    [SerializeField, Range(0.05f, 2f)] private float windLineSpeed = 0.45f;

    [Header("Darkness / Existing 2D Lights")]
    [SerializeField] private Light2D globalLight;
    [SerializeField, HideInInspector] private float normalLightIntensity = 1f;
    [FormerlySerializedAs("darknessLightIntensity")]
    [SerializeField, Range(0f, 1f)]
    private float darknessGlobalIntensity = 0.05f;
    [SerializeField, Min(0.1f)] private float playerLightRadius = 6.5f;
    [SerializeField, Range(0f, 1f)] private float playerLightFalloff = 0.75f;
    [SerializeField, Min(0.01f)] private float transitionDuration = 1f;

    [Header("Snow / Transition")]
    [SerializeField, Min(0.01f)] private float snowTransitionDuration = 1f;
    [SerializeField, Range(0f, 1f)] private float snowVisualIntensity = 0.58f;

    [Header("Snow / Camera Color")]
    [SerializeField] private Color snowColorFilter =
        new Color(0.68f, 0.84f, 1f, 1f);
    [SerializeField, Range(-100f, 100f)] private float snowTemperature = -38f;
    [SerializeField, Range(-100f, 100f)] private float snowTint = -4f;
    [SerializeField, Range(-100f, 100f)] private float snowSaturation = -20f;
    [SerializeField, Range(-100f, 100f)] private float snowContrast = 12f;
    [SerializeField, Range(-5f, 5f)] private float snowPostExposure = -0.12f;
    [SerializeField, Range(0f, 1f)] private float snowVolumeWeight = 1f;

    [Header("Snow / Ground Coverage")]
    [SerializeField] private Material snowWorldMaterial;
    [SerializeField] private Camera targetCamera;
    [SerializeField] private Vector2 snowWorldSize = new Vector2(42f, 24f);
    [SerializeField] private float snowWorldDepth = 1f;
    [SerializeField] private string snowSortingLayer = "Background";
    [SerializeField] private int snowSortingOrder = 30;
    [FormerlySerializedAs("snowParticleColor")]
    [SerializeField] private Color snowCoverageColor =
        new Color(0.82f, 0.92f, 1f, 0.72f);
    [FormerlySerializedAs("snowDensity")]
    [SerializeField, Range(0.5f, 12f)] private float snowPatchDensity = 3.2f;
    [FormerlySerializedAs("snowScale")]
    [SerializeField, Range(0.25f, 8f)] private float snowCoverageScale = 2.4f;

    [Header("Snow / Falling Particles")]
    [SerializeField] private GameObject snowParticlePrefab;
    [SerializeField] private Vector3 snowParticleCameraOffset =
        new Vector3(0f, 8f, 2f);
    [SerializeField, Range(0f, 2f)] private float snowEmissionMultiplier = 1f;

    [Header("Snow / Screen Overlay")]
    [SerializeField, Range(0f, 0.25f)] private float snowScreenOpacity = 0.055f;

    private float currentSnowIntensity;
    private float targetSnowIntensity;
    private float currentRainIntensity;
    private float targetRainIntensity;
    private float currentDarknessIntensity;
    private float targetDarknessIntensity;
    private float currentGoldenIntensity;
    private float targetGoldenIntensity;
    private float currentWindIntensity;
    private float targetWindIntensity;
    private Vector2 windVisualDirection;
    private Light2D playerLight;
    private float normalPlayerLightRadius;
    private float normalPlayerLightFalloff;
    private bool playerLightStateCaptured;
    private GameObject rainWorldObject;
    private Mesh rainWorldMesh;
    private MeshRenderer rainWorldRenderer;
    private MaterialPropertyBlock rainProperties;
    private GameObject snowWorldObject;
    private Mesh snowWorldMesh;
    private MeshRenderer snowWorldRenderer;
    private MaterialPropertyBlock snowProperties;
    private GameObject snowVolumeObject;
    private Volume snowVolume;
    private VolumeProfile snowVolumeProfile;
    private GameObject goldenVolumeObject;
    private Volume goldenVolume;
    private VolumeProfile goldenVolumeProfile;
    private GameObject snowParticleInstance;
    private ParticleSystem[] snowParticleSystems;
    private float[] snowParticleEmissionRates;
    private bool snowParticlesPlaying;

#if UNITY_EDITOR
    private static readonly float[] SnowDiagnosticTimes =
    {
        0.5f,
        1f,
        2f
    };

    private float snowDiagnosticElapsed;
    private int snowDiagnosticIndex;
    private bool snowDiagnosticsActive;
#endif

    private void Awake()
    {
        if (fullscreenImage != null)
        {
            fullscreenImage.raycastTarget = false;
            fullscreenImage.material = visualMaterial;
        }

        EnsureSnowResources();
        EnsureRainResources();
        EnsureGoldenResources();
        SetNeutral();
    }

    private void Update()
    {
        float step = Time.unscaledDeltaTime /
            Mathf.Max(0.01f, transitionDuration);
        float snowStep =
            Mathf.Max(0.0001f, snowVisualIntensity) *
            Time.unscaledDeltaTime /
            Mathf.Max(0.01f, snowTransitionDuration);
        currentSnowIntensity = Mathf.MoveTowards(
            currentSnowIntensity,
            targetSnowIntensity,
            snowStep
        );
        currentRainIntensity = Mathf.MoveTowards(
            currentRainIntensity,
            targetRainIntensity,
            step
        );
        currentDarknessIntensity = Mathf.MoveTowards(
            currentDarknessIntensity,
            targetDarknessIntensity,
            step
        );
        currentGoldenIntensity = Mathf.MoveTowards(
            currentGoldenIntensity,
            targetGoldenIntensity,
            step
        );
        currentWindIntensity = Mathf.MoveTowards(
            currentWindIntensity,
            targetWindIntensity,
            step
        );

        if (visualMaterial != null)
        {
            visualMaterial.SetFloat(VisualTimeId, Time.unscaledTime);
            visualMaterial.SetFloat(
                SnowIntensityId,
                currentSnowIntensity * snowScreenOpacity
            );
            visualMaterial.SetFloat(
                RainDropsIntensityId,
                currentRainIntensity * screenDropsIntensity
            );
            visualMaterial.SetFloat(
                RainDropsFrequencyId,
                screenDropsFrequency
            );
            visualMaterial.SetFloat(
                GoldenOverlayIntensityId,
                currentGoldenIntensity * goldenOverlayIntensity
            );
            visualMaterial.SetColor(
                GoldenOverlayColorId,
                goldenColorFilter
            );
            visualMaterial.SetFloat(
                WindVisualIntensityId,
                currentWindIntensity * windVisualIntensity
            );
            visualMaterial.SetFloat(WindLineDensityId, windLineDensity);
            visualMaterial.SetFloat(WindLineSpeedId, windLineSpeed);
            visualMaterial.SetVector(
                WindDirectionId,
                new Vector4(
                    windVisualDirection.x,
                    windVisualDirection.y,
                    0f,
                    0f
                )
            );
        }

        UpdateSnowResources();
        UpdateRainResources();
        UpdateDarknessResources();
        UpdateGoldenResources();

#if UNITY_EDITOR
        UpdateSnowDiagnostics();
#endif

        if (fullscreenImage != null &&
            targetSnowIntensity <= 0f &&
            currentSnowIntensity <= 0f &&
            targetRainIntensity <= 0f &&
            currentRainIntensity <= 0f &&
            targetGoldenIntensity <= 0f &&
            currentGoldenIntensity <= 0f &&
            targetWindIntensity <= 0f &&
            currentWindIntensity <= 0f)
        {
            fullscreenImage.enabled = false;
        }
    }

    private void LateUpdate()
    {
        FollowCamera();
    }

    public void Apply(WorldRuleData rule)
    {
        if (rule == null || rule.RuleType == WorldRuleType.None)
        {
            Clear();
            return;
        }

        switch (rule.RuleType)
        {
            case WorldRuleType.Snow:
                SetSnowActive(true);
                break;

            case WorldRuleType.Rain:
                ApplyRain();
                break;

            case WorldRuleType.Darkness:
                ApplyDarkness();
                break;

            case WorldRuleType.Wind:
                break;

            case WorldRuleType.Golden:
                SetGoldenActive(true);
                break;
        }
    }

    public void Clear()
    {
        SetSnowActive(false);
        ClearRainAndDarkness();
        SetGoldenActive(false);
        SetWindActive(false);
        windIndicator?.Hide();
    }

    public void ShowWind(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            SetWindActive(false);
            windIndicator?.Hide();
            return;
        }

        windVisualDirection = direction.normalized;
        SetWindActive(true);
        windIndicator?.Show(windVisualDirection);
    }

    public void ClearImmediate()
    {
        SetNeutral();
    }

    public void SetSnowActive(bool active)
    {
        EnsureSnowResources();
        targetSnowIntensity = active ? snowVisualIntensity : 0f;

#if UNITY_EDITOR
        snowDiagnosticElapsed = 0f;
        snowDiagnosticIndex = 0;
        snowDiagnosticsActive = active;
#endif

        if (active && fullscreenImage != null)
            fullscreenImage.enabled = true;
    }

    private void SetNeutral()
    {
        ClearRainAndDarkness();
        windIndicator?.Hide();
        currentSnowIntensity = 0f;
        targetSnowIntensity = 0f;
        currentRainIntensity = 0f;
        targetRainIntensity = 0f;
        currentDarknessIntensity = 0f;
        targetDarknessIntensity = 0f;
        currentGoldenIntensity = 0f;
        targetGoldenIntensity = 0f;
        currentWindIntensity = 0f;
        targetWindIntensity = 0f;
        windVisualDirection = Vector2.zero;

#if UNITY_EDITOR
        snowDiagnosticElapsed = 0f;
        snowDiagnosticIndex = 0;
        snowDiagnosticsActive = false;
#endif

        if (visualMaterial != null)
        {
            visualMaterial.SetFloat(SnowIntensityId, 0f);
            visualMaterial.SetFloat(RainDropsIntensityId, 0f);
            visualMaterial.SetFloat(GoldenOverlayIntensityId, 0f);
            visualMaterial.SetFloat(WindVisualIntensityId, 0f);
        }

        if (fullscreenImage != null)
            fullscreenImage.enabled = false;

        UpdateSnowResources();
        UpdateRainResources();
        UpdateDarknessResources();
        UpdateGoldenResources();
    }

    private void ApplyRain()
    {
        SetRainActive(true);
    }

    private void ApplyDarkness()
    {
        targetDarknessIntensity = 1f;
        ResolvePlayerLight();
    }

    private void ClearRainAndDarkness()
    {
        SetRainActive(false);
        targetDarknessIntensity = 0f;
    }

    private void UpdateDarknessResources()
    {
        if (globalLight != null)
        {
            globalLight.intensity = Mathf.Lerp(
                normalLightIntensity,
                darknessGlobalIntensity,
                currentDarknessIntensity
            );
        }

        if (currentDarknessIntensity > 0f ||
            targetDarknessIntensity > 0f)
        {
            ResolvePlayerLight();
        }

        if (playerLight == null || !playerLightStateCaptured)
            return;

        playerLight.pointLightOuterRadius = Mathf.Lerp(
            normalPlayerLightRadius,
            playerLightRadius,
            currentDarknessIntensity
        );
        playerLight.falloffIntensity = Mathf.Lerp(
            normalPlayerLightFalloff,
            playerLightFalloff,
            currentDarknessIntensity
        );
    }

    private void ResolvePlayerLight()
    {
        if (playerLight != null)
            return;

        playerLightStateCaptured = false;

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        if (player == null)
            return;

        Light2D fallback = null;
        Light2D[] lights = player.GetComponentsInChildren<Light2D>(true);

        for (int i = 0; i < lights.Length; i++)
        {
            Light2D candidate = lights[i];

            if (candidate.lightType != Light2D.LightType.Point)
                continue;

            fallback ??= candidate;

            if (candidate.gameObject.name == "SpriteLight2D")
            {
                playerLight = candidate;
                break;
            }
        }

        playerLight ??= fallback;

        if (playerLight == null)
            return;

        normalPlayerLightRadius = playerLight.pointLightOuterRadius;
        normalPlayerLightFalloff = playerLight.falloffIntensity;
        playerLightStateCaptured = true;
    }

    private void SetRainActive(bool active)
    {
        EnsureRainResources();
        targetRainIntensity = active ? 1f : 0f;

        if (active)
        {
            if (rainEffect != null)
                rainEffect.SetActive(true);

            if (fullscreenImage != null)
                fullscreenImage.enabled = true;
        }
    }

    private void EnsureSnowResources()
    {
        if (snowWorldObject == null && snowWorldMaterial != null)
            CreateSnowWorldOverlay();

        if (snowVolume == null)
            CreateSnowColorVolume();

        if (snowParticleInstance == null && snowParticlePrefab != null)
            CreateSnowParticles();
    }

    private void EnsureRainResources()
    {
        if (rainWorldObject == null && rainWorldMaterial != null)
            CreateRainWorldOverlay();
    }

    private void EnsureGoldenResources()
    {
        if (goldenVolume == null)
            CreateGoldenColorVolume();
    }

    private void SetGoldenActive(bool active)
    {
        EnsureGoldenResources();
        targetGoldenIntensity = active ? 1f : 0f;

        if (active && fullscreenImage != null)
            fullscreenImage.enabled = true;
    }

    private void SetWindActive(bool active)
    {
        targetWindIntensity = active ? 1f : 0f;

        if (active && fullscreenImage != null)
            fullscreenImage.enabled = true;
    }

    private void CreateGoldenColorVolume()
    {
        goldenVolumeObject = new GameObject("GoldenColorVolume");
        goldenVolumeObject.transform.SetParent(transform, false);
        goldenVolume = goldenVolumeObject.AddComponent<Volume>();
        goldenVolume.isGlobal = true;
        goldenVolume.priority = 99f;
        goldenVolume.weight = 0f;

        goldenVolumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        goldenVolumeProfile.name = "GoldenColorVolumeProfile";
        ColorAdjustments colorAdjustments =
            goldenVolumeProfile.Add<ColorAdjustments>();
        colorAdjustments.active = true;
        colorAdjustments.colorFilter.Override(goldenColorFilter);
        goldenVolume.sharedProfile = goldenVolumeProfile;
    }

    private void UpdateGoldenResources()
    {
        if (goldenVolume == null)
            return;

        goldenVolume.weight = currentGoldenIntensity * 0.3f;
    }

    private void CreateRainWorldOverlay()
    {
        rainWorldObject = new GameObject("RainWetGroundOverlay");
        rainWorldObject.transform.SetParent(transform, false);

        MeshFilter filter = rainWorldObject.AddComponent<MeshFilter>();
        rainWorldRenderer = rainWorldObject.AddComponent<MeshRenderer>();
        rainWorldRenderer.sharedMaterial = rainWorldMaterial;
        rainWorldRenderer.sortingLayerName = "Background";
        rainWorldRenderer.sortingOrder = 29;

        rainWorldMesh = new Mesh
        {
            name = "RainWetGroundOverlayMesh",
            vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f)
            },
            uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            },
            triangles = new[] { 0, 2, 1, 2, 3, 1 }
        };
        filter.sharedMesh = rainWorldMesh;
        rainProperties = new MaterialPropertyBlock();
        rainWorldObject.SetActive(false);
    }

    private void UpdateRainResources()
    {
        if (rainWorldRenderer != null && rainWorldObject != null)
        {
            bool visible = currentRainIntensity > 0f ||
                targetRainIntensity > 0f;
            rainWorldObject.SetActive(visible);

            if (visible)
            {
                if (targetCamera == null)
                    targetCamera = Camera.main;

                Vector3 cameraPosition = targetCamera != null
                    ? targetCamera.transform.position
                    : transform.position;
                rainWorldObject.transform.position = new Vector3(
                    cameraPosition.x,
                    cameraPosition.y,
                    snowWorldDepth
                );
                rainWorldObject.transform.localScale = new Vector3(
                    snowWorldSize.x,
                    snowWorldSize.y,
                    1f
                );
                rainWorldRenderer.GetPropertyBlock(rainProperties);
                rainProperties.SetFloat(
                    WetGroundIntensityId,
                    currentRainIntensity * wetGroundIntensity
                );
                rainProperties.SetFloat(
                    WetPatternScaleId,
                    wetPatternScale
                );
                rainProperties.SetFloat(
                    VisualTimeId,
                    Time.unscaledTime
                );
                rainWorldRenderer.SetPropertyBlock(rainProperties);
            }
        }

        if (rainEffect != null &&
            targetRainIntensity <= 0f &&
            currentRainIntensity <= 0f)
        {
            rainEffect.SetActive(false);
        }
    }

    private void CreateSnowWorldOverlay()
    {
        snowWorldObject = new GameObject("SnowWorldOverlay");
        snowWorldObject.transform.SetParent(transform, false);

        MeshFilter filter = snowWorldObject.AddComponent<MeshFilter>();
        snowWorldRenderer = snowWorldObject.AddComponent<MeshRenderer>();
        snowWorldRenderer.sharedMaterial = snowWorldMaterial;
        snowWorldRenderer.sortingLayerName = snowSortingLayer;
        snowWorldRenderer.sortingOrder = snowSortingOrder;

        snowWorldMesh = new Mesh
        {
            name = "SnowWorldOverlayMesh",
            vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(0.5f, -0.5f, 0f),
                new Vector3(-0.5f, 0.5f, 0f),
                new Vector3(0.5f, 0.5f, 0f)
            },
            uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(1f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f)
            },
            triangles = new[] { 0, 2, 1, 2, 3, 1 }
        };
        filter.sharedMesh = snowWorldMesh;
        snowProperties = new MaterialPropertyBlock();
        snowWorldObject.SetActive(false);
    }

    private void CreateSnowColorVolume()
    {
        snowVolumeObject = new GameObject("SnowColorVolume");
        snowVolumeObject.transform.SetParent(transform, false);
        snowVolume = snowVolumeObject.AddComponent<Volume>();
        snowVolume.isGlobal = true;
        snowVolume.priority = 100f;
        snowVolume.weight = 0f;

        snowVolumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        snowVolumeProfile.name = "SnowColorVolumeProfile";
        ColorAdjustments colorAdjustments =
            snowVolumeProfile.Add<ColorAdjustments>();
        colorAdjustments.active = true;
        colorAdjustments.colorFilter.Override(snowColorFilter);
        colorAdjustments.saturation.Override(snowSaturation);
        colorAdjustments.contrast.Override(snowContrast);
        colorAdjustments.postExposure.Override(snowPostExposure);
        WhiteBalance whiteBalance =
            snowVolumeProfile.Add<WhiteBalance>();
        whiteBalance.active = true;
        whiteBalance.temperature.Override(snowTemperature);
        whiteBalance.tint.Override(snowTint);
        snowVolume.sharedProfile = snowVolumeProfile;
    }

    private void CreateSnowParticles()
    {
        snowParticleInstance = Instantiate(
            snowParticlePrefab,
            transform
        );
        snowParticleInstance.name = "Snow1";
        snowParticleSystems =
            snowParticleInstance.GetComponentsInChildren<ParticleSystem>(true);
        snowParticleEmissionRates =
            new float[snowParticleSystems.Length];

        for (int i = 0; i < snowParticleSystems.Length; i++)
        {
            ParticleSystem particleSystem = snowParticleSystems[i];
            ParticleSystem.MainModule main = particleSystem.main;
            main.useUnscaledTime = true;
            main.cullingMode =
                ParticleSystemCullingMode.AlwaysSimulate;

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            snowParticleEmissionRates[i] =
                emission.rateOverTimeMultiplier;
            emission.rateOverTimeMultiplier = 0f;
            particleSystem.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }

        snowParticleInstance.SetActive(false);
    }

    private void UpdateSnowResources()
    {
        float normalized = snowVisualIntensity > 0f
            ? Mathf.Clamp01(currentSnowIntensity / snowVisualIntensity)
            : 0f;

        if (snowVolume != null)
            snowVolume.weight = normalized * snowVolumeWeight;

        UpdateSnowParticles(normalized);

        if (snowWorldRenderer == null || snowWorldObject == null)
            return;

        bool visible = currentSnowIntensity > 0f ||
            targetSnowIntensity > 0f;
        snowWorldObject.SetActive(visible);

        if (!visible)
            return;

        if (targetCamera == null)
            targetCamera = Camera.main;

        Vector3 cameraPosition = targetCamera != null
            ? targetCamera.transform.position
            : transform.position;
        snowWorldObject.transform.position = new Vector3(
            cameraPosition.x,
            cameraPosition.y,
            snowWorldDepth
        );
        snowWorldObject.transform.localScale = new Vector3(
            snowWorldSize.x,
            snowWorldSize.y,
            1f
        );

        snowWorldRenderer.sortingLayerName = snowSortingLayer;
        snowWorldRenderer.sortingOrder = snowSortingOrder;
        snowWorldRenderer.GetPropertyBlock(snowProperties);
        snowProperties.SetFloat(SnowIntensityId, currentSnowIntensity);
        snowProperties.SetColor(SnowColorId, snowCoverageColor);
        snowProperties.SetFloat(SnowDensityId, snowPatchDensity);
        snowProperties.SetFloat(SnowScaleId, snowCoverageScale);
        snowWorldRenderer.SetPropertyBlock(snowProperties);
    }

    private void UpdateSnowParticles(float normalized)
    {
        if (snowParticleInstance == null ||
            snowParticleSystems == null)
        {
            return;
        }

        bool shouldSimulate = normalized > 0f ||
            targetSnowIntensity > 0f;

        if (shouldSimulate)
        {
            if (!snowParticleInstance.activeSelf)
                snowParticleInstance.SetActive(true);

            if (targetCamera == null)
                targetCamera = Camera.main;

            Vector3 cameraPosition = targetCamera != null
                ? targetCamera.transform.position
                : transform.position;
            snowParticleInstance.transform.position =
                cameraPosition + snowParticleCameraOffset;

            for (int i = 0; i < snowParticleSystems.Length; i++)
            {
                ParticleSystem particleSystem = snowParticleSystems[i];
                ParticleSystem.EmissionModule emission =
                    particleSystem.emission;
                emission.rateOverTimeMultiplier =
                    snowParticleEmissionRates[i] *
                    snowEmissionMultiplier *
                    normalized;

                if (!snowParticlesPlaying)
                    particleSystem.Play(true);
            }

            snowParticlesPlaying = true;
            return;
        }

        if (!snowParticlesPlaying)
            return;

        for (int i = 0; i < snowParticleSystems.Length; i++)
        {
            ParticleSystem.EmissionModule emission =
                snowParticleSystems[i].emission;
            emission.rateOverTimeMultiplier = 0f;
            snowParticleSystems[i].Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear
            );
        }

        snowParticlesPlaying = false;
        snowParticleInstance.SetActive(false);
    }

    private void RefreshFullscreenVisibility()
    {
        if (fullscreenImage == null)
            return;

        fullscreenImage.enabled =
            currentSnowIntensity > 0f ||
            targetSnowIntensity > 0f ||
            currentRainIntensity > 0f ||
            targetRainIntensity > 0f ||
            currentGoldenIntensity > 0f ||
            targetGoldenIntensity > 0f ||
            currentWindIntensity > 0f ||
            targetWindIntensity > 0f;
    }

    private void FollowCamera()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;

        if (targetCamera == null)
            return;

        Vector3 cameraPosition = targetCamera.transform.position;

        if (snowWorldObject != null && snowWorldObject.activeSelf)
        {
            snowWorldObject.transform.position = new Vector3(
                cameraPosition.x,
                cameraPosition.y,
                snowWorldDepth
            );
        }

        if (rainWorldObject != null && rainWorldObject.activeSelf)
        {
            rainWorldObject.transform.position = new Vector3(
                cameraPosition.x,
                cameraPosition.y,
                snowWorldDepth
            );
        }

        if (snowParticleInstance != null &&
            snowParticleInstance.activeSelf)
        {
            snowParticleInstance.transform.position =
                cameraPosition + snowParticleCameraOffset;
        }
    }

    private void OnDisable()
    {
        SetNeutral();
    }

    private void OnDestroy()
    {
        if (snowWorldMesh != null)
            Destroy(snowWorldMesh);

        if (rainWorldMesh != null)
            Destroy(rainWorldMesh);

        if (snowVolumeProfile != null)
            Destroy(snowVolumeProfile);

        if (goldenVolumeProfile != null)
            Destroy(goldenVolumeProfile);
    }

#if UNITY_EDITOR
    private void UpdateSnowDiagnostics()
    {
        if (!snowDiagnosticsActive)
            return;

        snowDiagnosticElapsed += Time.unscaledDeltaTime;

        while (snowDiagnosticIndex < SnowDiagnosticTimes.Length &&
               snowDiagnosticElapsed >=
               SnowDiagnosticTimes[snowDiagnosticIndex])
        {
            float sampleTime =
                SnowDiagnosticTimes[snowDiagnosticIndex];

            if (snowParticleSystems == null ||
                snowParticleSystems.Length == 0)
            {
                Debug.LogWarning(
                    $"[WorldRuleVisual][Snow1] t={sampleTime:F1}s " +
                    "ParticleSystem not found.",
                    this
                );
            }
            else
            {
                for (int i = 0; i < snowParticleSystems.Length; i++)
                {
                    ParticleSystem particleSystem =
                        snowParticleSystems[i];
                    ParticleSystem.MainModule main =
                        particleSystem.main;
                    ParticleSystem.EmissionModule emission =
                        particleSystem.emission;

                    Debug.Log(
                        $"[WorldRuleVisual][Snow1] t={sampleTime:F1}s " +
                        $"index={i} " +
                        $"isPlaying={particleSystem.isPlaying} " +
                        $"isEmitting={particleSystem.isEmitting} " +
                        $"particleCount={particleSystem.particleCount} " +
                        $"emissionRate=" +
                        $"{emission.rateOverTimeMultiplier:F2} " +
                        $"cullingMode={main.cullingMode} " +
                        $"worldPosition=" +
                        $"{particleSystem.transform.position}",
                        particleSystem
                    );
                }
            }

            snowDiagnosticIndex++;
        }

        if (snowDiagnosticIndex >= SnowDiagnosticTimes.Length)
            snowDiagnosticsActive = false;
    }

    [ContextMenu("Preview/Clear")]
    private void PreviewClear()
    {
        ClearImmediate();
    }

#endif
}
