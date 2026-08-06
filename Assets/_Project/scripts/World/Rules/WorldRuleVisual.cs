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
    private static readonly int BlizzardIntensityId =
        Shader.PropertyToID("_BlizzardIntensity");
    private static readonly int BlizzardLineDensityId =
        Shader.PropertyToID("_BlizzardLineDensity");
    private static readonly int BlizzardLineSpeedId =
        Shader.PropertyToID("_BlizzardLineSpeed");
    private static readonly int BlizzardVeilId =
        Shader.PropertyToID("_BlizzardVeil");
    private static readonly int BlizzardDirectionId =
        Shader.PropertyToID("_BlizzardDirection");
    private static readonly int WetGroundIntensityId =
        Shader.PropertyToID("_WetGroundIntensity");
    private static readonly int WetPatternScaleId =
        Shader.PropertyToID("_WetPatternScale");
    private static readonly int RainDropsIntensityId =
        Shader.PropertyToID("_RainDropsIntensity");
    private static readonly int RainDropsFrequencyId =
        Shader.PropertyToID("_RainDropsFrequency");
    private static readonly int RainLargeDropsIntensityId =
        Shader.PropertyToID("_RainLargeDropsIntensity");
    private static readonly int RainLargeDropsCountId =
        Shader.PropertyToID("_RainLargeDropsCount");
    private static readonly int RainLargeDropsSpeedId =
        Shader.PropertyToID("_RainLargeDropsSpeed");
    private static readonly int RainLargeDropsScaleId =
        Shader.PropertyToID("_RainLargeDropsScale");
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
    [SerializeField] private CondensationFogOverlay condensationFogOverlay;

    [Header("Rain / Existing Scene Effect")]
    [SerializeField] private GameObject rainEffect;

    [Header("Rain / Wet Ground")]
    [SerializeField] private Material rainWorldMaterial;
    [SerializeField, Range(0f, 1f)] private float wetGroundIntensity = 0.32f;
    [SerializeField, Range(0.25f, 8f)] private float wetPatternScale = 2.8f;

    [Header("Rain / Screen Drops")]
    [SerializeField, Range(0f, 0.5f)] private float screenDropsIntensity = 0.025f;
    [SerializeField, Range(0.05f, 2f)] private float screenDropsFrequency = 0.35f;
    [SerializeField, Range(0f, 0.6f)]
    private float rainLargeDropsIntensity = 0.32f;
    [SerializeField, Range(4, 8)] private int rainLargeDropsCount = 6;
    [SerializeField, Range(0.05f, 0.5f)]
    private float rainLargeDropsSpeed = 0.18f;
    [SerializeField, Range(0.5f, 2f)]
    private float rainLargeDropsScale = 1f;

    [Header("Golden / World Visual")]
    [SerializeField, Range(0f, 0.2f)] private float goldenOverlayIntensity = 0.025f;
    [SerializeField, ColorUsage(false, true)] private Color goldenColorFilter =
        new Color(1f, 0.94f, 0.78f, 1f);

    [Header("Wind / Screen Flow")]
    [SerializeField, Range(0f, 0.4f)] private float windVisualIntensity = 0.16f;
    [SerializeField, Range(2f, 12f)] private float windLineDensity = 3f;
    [SerializeField, Range(0.05f, 2f)] private float windLineSpeed = 0.45f;

    [Header("Wind / Dust Particles")]
    [SerializeField] private ParticleSystem windParticleSystem;
    [SerializeField, Range(0f, 60f)] private float windParticleEmission = 22f;
    [SerializeField, Range(0.1f, 5f)] private float windParticleSpeed = 2.2f;
    [SerializeField] private Vector2 windParticleArea = new(22f, 13f);

    [Header("Darkness / Existing 2D Lights")]
    [SerializeField] private Light2D globalLight;
    [FormerlySerializedAs("darknessLightIntensity")]
    [SerializeField, Range(0f, 1f)]
    private float darknessGlobalIntensity = 0.05f;
    [SerializeField, Min(0.1f)] private float playerLightRadius = 6.5f;
    [SerializeField, Min(0f)] private float playerLightIntensity = 1f;
    [SerializeField, Range(0f, 1f)] private float playerLightFalloff = 0.75f;
    [SerializeField] private Sprite darknessMarkerSprite;
    [SerializeField] private Material darknessMarkerMaterial;
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

    [Header("Snow / Viewport Coverage")]
    [SerializeField, Range(0.25f, 0.75f)]
    private float snowHorizontalPaddingRatio = 0.35f;
    [SerializeField, Range(0.2f, 0.5f)]
    private float snowVerticalPaddingRatio = 0.25f;
    [SerializeField, Range(0.5f, 3f)]
    private float snowUpstreamPaddingSeconds = 2f;
    [SerializeField, Range(0.1f, 1f)]
    private float snowViewportRefreshInterval = 0.25f;
    [SerializeField, Range(1.05f, 1.5f)]
    private float snowMaxParticlesHeadroom = 1.15f;

    [FormerlySerializedAs("snowEmissionMultiplier")]
    [SerializeField, Range(0f, 3f)]
    private float snowParticleEmissionMultiplier = 1.8f;

    [Header("Snow / Screen Overlay")]
    [SerializeField, Range(0f, 0.25f)] private float snowScreenOpacity = 0.055f;
    [SerializeField, Range(0f, 0.6f)] private float blizzardIntensity = 0.32f;
    [SerializeField, Range(2f, 16f)] private float blizzardLineDensity = 8f;
    [SerializeField, Range(0.1f, 3f)] private float blizzardLineSpeed = 1.4f;
    [SerializeField, Range(0f, 0.4f)] private float blizzardVeil = 0.14f;

    private float currentSnowIntensity;
    private float targetSnowIntensity;
    private float currentSnowBlizzardIntensity;
    private float targetSnowBlizzardIntensity;
    private float snowBlizzardDirection;
    private float snowPhaseTransitionDuration = 1f;
    private float activeSnowTransitionDuration = 1f;
    private float activeSnowCalmEmissionMultiplier = 1f;
    private float activeSnowBlizzardEmissionMultiplier = 3.5f;
    private float activeSnowCalmSpeedMultiplier = 1f;
    private float activeSnowBlizzardSpeedMultiplier = 2.1f;
    private float activeSnowBlizzardVisibilityEffect = 1f;
    private float activeSnowBlizzardHorizontalSpeed = 2.4f;
    private float currentRainIntensity;
    private float targetRainIntensity;
    private float currentDarknessIntensity;
    private float targetDarknessIntensity;
    private float currentGoldenIntensity;
    private float targetGoldenIntensity;
    private float currentWindIntensity;
    private float targetWindIntensity;
    private Vector2 windVisualDirection;
    private float baselineGlobalLightIntensity;
    private bool globalLightStateCaptured;
    private Light2D playerLight;
    private float normalPlayerLightRadius;
    private float normalPlayerLightIntensity;
    private float normalPlayerLightFalloff;
    private bool playerLightStateCaptured;
    private Light2D darknessRevealLight;
    private GameObject darknessRevealObject;
    private float darknessRevealRemaining;
    private float darknessShotRevealRadius = 3.5f;
    private float darknessShotRevealDuration = 0.12f;
    private float darknessShotRevealIntensity = 1.25f;
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
    private float[] snowParticleStartSizes;
    private float[] snowParticleStartSpeeds;
    private float[] snowParticleStartLifetimes;
    private int[] snowParticleMaxCounts;
    private Vector3[] snowParticleShapeScales;
    private Vector3[] snowParticleShapePositions;
    private bool[] snowParticleVelocityEnabled;
    private ParticleSystem.MinMaxCurve[] snowParticleVelocityX;
    private ParticleSystemSimulationSpace[] snowParticleVelocitySpaces;
    private bool snowParticlesPlaying;
    private float snowViewportRefreshTimeRemaining;
    private float cachedSnowViewportWidth = -1f;
    private float cachedSnowViewportHeight = -1f;
    private float cachedSnowOrthographicSize = -1f;
    private float cachedSnowAspect = -1f;
    private GameObject windParticleObject;

    private const float SnowParticleSizeMultiplier = 1.25f;
    private const float SnowParticleSpeedMultiplier = 1.35f;

    public Sprite DarknessMarkerSprite => darknessMarkerSprite;
    public Material DarknessMarkerMaterial => darknessMarkerMaterial;

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
        CaptureGlobalLightState();
        activeSnowTransitionDuration = snowTransitionDuration;

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
            Mathf.Max(0.01f, activeSnowTransitionDuration);
        currentSnowIntensity = Mathf.MoveTowards(
            currentSnowIntensity,
            targetSnowIntensity,
            snowStep
        );
        currentSnowBlizzardIntensity = Mathf.MoveTowards(
            currentSnowBlizzardIntensity,
            targetSnowBlizzardIntensity,
            Time.deltaTime /
            Mathf.Max(0.01f, snowPhaseTransitionDuration)
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
        UpdateDarknessReveal();
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
            float normalizedSnow = snowVisualIntensity > 0f
                ? Mathf.Clamp01(
                    currentSnowIntensity / snowVisualIntensity
                )
                : 0f;
            visualMaterial.SetFloat(VisualTimeId, Time.unscaledTime);
            visualMaterial.SetFloat(
                SnowIntensityId,
                currentSnowIntensity * snowScreenOpacity *
                Mathf.Lerp(
                    1f,
                    1.35f,
                    currentSnowBlizzardIntensity
                )
            );
            visualMaterial.SetFloat(
                BlizzardIntensityId,
                normalizedSnow * currentSnowBlizzardIntensity *
                blizzardIntensity * activeSnowBlizzardVisibilityEffect
            );
            visualMaterial.SetFloat(
                BlizzardLineDensityId,
                blizzardLineDensity
            );
            visualMaterial.SetFloat(
                BlizzardLineSpeedId,
                blizzardLineSpeed
            );
            visualMaterial.SetFloat(
                BlizzardVeilId,
                normalizedSnow * currentSnowBlizzardIntensity *
                blizzardVeil * activeSnowBlizzardVisibilityEffect
            );
            visualMaterial.SetFloat(
                BlizzardDirectionId,
                snowBlizzardDirection
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
                RainLargeDropsIntensityId,
                currentRainIntensity * rainLargeDropsIntensity
            );
            visualMaterial.SetFloat(
                RainLargeDropsCountId,
                rainLargeDropsCount
            );
            visualMaterial.SetFloat(
                RainLargeDropsSpeedId,
                rainLargeDropsSpeed
            );
            visualMaterial.SetFloat(
                RainLargeDropsScaleId,
                rainLargeDropsScale
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
                ConfigureSnow(rule);
                ResetSnowVisual();
                SetSnowActive(true);
                break;

            case WorldRuleType.Rain:
                ApplyRain();
                break;

            case WorldRuleType.Darkness:
                ApplyDarkness(rule);
                break;

            case WorldRuleType.Wind:
                break;

            case WorldRuleType.Golden:
                SetGoldenActive(true);
                break;

            case WorldRuleType.Condensation:
                SetCondensationActive(true);
                break;
        }
    }

    public void Clear()
    {
        SetSnowActive(false);
        ResetSnowVisual();
        ClearRainAndDarkness();
        SetGoldenActive(false);
        SetWindActive(false);
        SetCondensationActive(false);
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
        UpdateWindParticlesDirection();
        windIndicator?.ShowApplied(windVisualDirection);
    }

    public void WarnWind(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        windIndicator?.ShowWarning(direction.normalized);
    }

    public void ClearImmediate()
    {
        SetNeutral();
    }

    private void CaptureGlobalLightState()
    {
        if (globalLight == null || globalLightStateCaptured)
            return;

        baselineGlobalLightIntensity = globalLight.intensity;
        globalLightStateCaptured = true;
    }

    public void SetSnowActive(bool active)
    {
        EnsureSnowResources();
        targetSnowIntensity = active ? snowVisualIntensity : 0f;

        if (active)
        {
            InvalidateSnowViewportCache();
            UpdateSnowEmitterCoverage(true);
        }

#if UNITY_EDITOR
        snowDiagnosticElapsed = 0f;
        snowDiagnosticIndex = 0;
        snowDiagnosticsActive = active;
#endif

        if (active && fullscreenImage != null)
            fullscreenImage.enabled = true;
    }

    public void SetSnowBlizzardState(
        float intensity,
        float horizontalDirection,
        float transitionTime)
    {
        targetSnowBlizzardIntensity = Mathf.Clamp01(intensity);
        snowPhaseTransitionDuration = Mathf.Max(0.01f, transitionTime);

        if (Mathf.Abs(horizontalDirection) > 0.001f)
            snowBlizzardDirection = Mathf.Sign(horizontalDirection);

        if (targetSnowBlizzardIntensity > 0f && fullscreenImage != null)
            fullscreenImage.enabled = true;
    }

    public void ResetSnowVisual()
    {
        currentSnowBlizzardIntensity = 0f;
        targetSnowBlizzardIntensity = 0f;
        snowBlizzardDirection = 0f;
        snowPhaseTransitionDuration = activeSnowTransitionDuration;
        RestoreSnowParticleVelocityModules();
        RestoreSnowEmitterConfiguration();

        if (visualMaterial == null)
            return;

        visualMaterial.SetFloat(BlizzardIntensityId, 0f);
        visualMaterial.SetFloat(BlizzardVeilId, 0f);
        visualMaterial.SetFloat(BlizzardDirectionId, 0f);
    }

    private void ConfigureSnow(WorldRuleData rule)
    {
        activeSnowTransitionDuration = rule.SnowTransitionDuration;
        activeSnowCalmEmissionMultiplier =
            rule.SnowCalmEmissionMultiplier;
        activeSnowBlizzardEmissionMultiplier =
            rule.SnowBlizzardEmissionMultiplier;
        activeSnowCalmSpeedMultiplier =
            rule.SnowCalmParticleSpeedMultiplier;
        activeSnowBlizzardSpeedMultiplier =
            rule.SnowBlizzardParticleSpeedMultiplier;
        activeSnowBlizzardVisibilityEffect = Mathf.Clamp01(
            (1f - rule.SnowBlizzardVisibilityMultiplier) / 0.35f
        );
        activeSnowBlizzardHorizontalSpeed =
            rule.SnowBlizzardHorizontalSpeed;
    }

    private void SetNeutral()
    {
        condensationFogOverlay?.HideImmediate();
        ClearRainAndDarkness();
        windIndicator?.Hide();
        currentSnowIntensity = 0f;
        targetSnowIntensity = 0f;
        ResetSnowVisual();
        currentRainIntensity = 0f;
        targetRainIntensity = 0f;
        currentDarknessIntensity = 0f;
        targetDarknessIntensity = 0f;
        currentGoldenIntensity = 0f;
        targetGoldenIntensity = 0f;
        currentWindIntensity = 0f;
        targetWindIntensity = 0f;
        windVisualDirection = Vector2.zero;
        StopWindParticles();

#if UNITY_EDITOR
        snowDiagnosticElapsed = 0f;
        snowDiagnosticIndex = 0;
        snowDiagnosticsActive = false;
#endif

        if (visualMaterial != null)
        {
            visualMaterial.SetFloat(SnowIntensityId, 0f);
            visualMaterial.SetFloat(BlizzardIntensityId, 0f);
            visualMaterial.SetFloat(BlizzardVeilId, 0f);
            visualMaterial.SetFloat(BlizzardDirectionId, 0f);
            visualMaterial.SetFloat(RainDropsIntensityId, 0f);
            visualMaterial.SetFloat(RainLargeDropsIntensityId, 0f);
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

    private void SetCondensationActive(bool active)
    {
        if (condensationFogOverlay == null)
        {
            condensationFogOverlay =
                CondensationFogOverlay.Instance;
        }

        if (condensationFogOverlay == null)
        {
            condensationFogOverlay =
                FindFirstObjectByType<CondensationFogOverlay>();
        }

        if (condensationFogOverlay == null)
            return;

        if (active)
            condensationFogOverlay.Show();
        else
            condensationFogOverlay.Hide();
    }

    private void ApplyRain()
    {
        SetRainActive(true);
    }

    private void ApplyDarkness(WorldRuleData rule)
    {
        playerLightRadius = rule.DarknessPlayerLightRadius;
        playerLightIntensity = rule.DarknessPlayerLightIntensity;
        darknessShotRevealRadius = rule.DarknessShotRevealRadius;
        darknessShotRevealDuration = rule.DarknessShotRevealDuration;
        darknessShotRevealIntensity = rule.DarknessShotRevealIntensity;
        targetDarknessIntensity = 1f;
        ResolvePlayerLight();
        EnsureDarknessRevealLight();
    }

    private void ClearRainAndDarkness()
    {
        SetRainActive(false);
        currentDarknessIntensity = 0f;
        targetDarknessIntensity = 0f;
        StopDarknessReveal();
        UpdateDarknessResources();
    }

    private void UpdateDarknessResources()
    {
        if (globalLight != null && globalLightStateCaptured)
        {
            globalLight.intensity = Mathf.Lerp(
                baselineGlobalLightIntensity,
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
        playerLight.intensity = Mathf.Lerp(
            normalPlayerLightIntensity,
            playerLightIntensity,
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
        normalPlayerLightIntensity = playerLight.intensity;
        normalPlayerLightFalloff = playerLight.falloffIntensity;
        playerLightStateCaptured = true;
    }

    public void RevealDarkness(Vector2 origin, float multiplier)
    {
        if (targetDarknessIntensity <= 0f)
            return;

        EnsureDarknessRevealLight();

        if (darknessRevealLight == null)
            return;

        float safeMultiplier = Mathf.Max(1f, multiplier);
        darknessRevealObject.transform.position = new Vector3(
            origin.x,
            origin.y,
            0f
        );
        darknessRevealLight.pointLightOuterRadius =
            darknessShotRevealRadius * safeMultiplier;
        darknessRevealLight.intensity =
            darknessShotRevealIntensity * safeMultiplier;
        darknessRevealRemaining = darknessShotRevealDuration;
        darknessRevealLight.enabled = true;
    }

    public void StopDarknessReveal()
    {
        darknessRevealRemaining = 0f;

        if (darknessRevealLight != null)
            darknessRevealLight.enabled = false;
    }

    private void UpdateDarknessReveal()
    {
        if (darknessRevealLight == null ||
            !darknessRevealLight.enabled)
        {
            return;
        }

        darknessRevealRemaining -= Time.unscaledDeltaTime;

        if (darknessRevealRemaining <= 0f)
            StopDarknessReveal();
    }

    private void EnsureDarknessRevealLight()
    {
        if (darknessRevealLight != null)
            return;

        ResolvePlayerLight();

        darknessRevealObject = new GameObject("DarknessShotRevealLight");
        darknessRevealObject.transform.SetParent(transform, false);
        darknessRevealLight =
            darknessRevealObject.AddComponent<Light2D>();
        darknessRevealLight.lightType = Light2D.LightType.Point;
        darknessRevealLight.color = new Color(1f, 0.72f, 0.42f, 1f);
        darknessRevealLight.falloffIntensity = 0.78f;
        darknessRevealLight.intensity = darknessShotRevealIntensity;
        darknessRevealLight.pointLightOuterRadius =
            darknessShotRevealRadius;

        if (playerLight != null)
            darknessRevealLight.lightCookieSprite =
                playerLight.lightCookieSprite;

        darknessRevealLight.enabled = false;
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

        if (active)
        {
            EnsureWindResources();

            if (fullscreenImage != null)
                fullscreenImage.enabled = true;
        }

        if (windParticleSystem == null)
            return;

        if (active)
        {
            UpdateWindParticlesDirection();

            if (!windParticleSystem.isPlaying)
                windParticleSystem.Play(true);
        }
        else
        {
            StopWindParticles();
        }
    }

    private void EnsureWindResources()
    {
        if (windParticleSystem != null)
            return;

        windParticleObject = new GameObject("WindDustParticles");
        windParticleObject.layer = 0;
        windParticleSystem =
            windParticleObject.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = windParticleSystem.main;
        main.loop = true;
        main.duration = 5f;
        main.startLifetime = 2.5f;
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.07f);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.72f, 0.78f, 0.82f, 0.12f),
            new Color(0.9f, 0.94f, 1f, 0.28f)
        );
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 96;

        ParticleSystem.EmissionModule emission = windParticleSystem.emission;
        emission.rateOverTime = windParticleEmission;

        ParticleSystem.ShapeModule shape = windParticleSystem.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(
            Mathf.Max(0.1f, windParticleArea.x),
            Mathf.Max(0.1f, windParticleArea.y),
            0.1f
        );

        ParticleSystemRenderer particleRenderer =
            windParticleObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        particleRenderer.velocityScale = 0.08f;
        particleRenderer.lengthScale = 0.18f;
        particleRenderer.sortingLayerName = "Foreground";
        particleRenderer.sortingOrder = 2;

        windParticleSystem.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );
    }

    private void UpdateWindParticlesDirection()
    {
        if (windParticleSystem == null)
            return;

        Vector2 velocity = windVisualDirection * windParticleSpeed;
        ParticleSystem.VelocityOverLifetimeModule velocityModule =
            windParticleSystem.velocityOverLifetime;
        velocityModule.enabled = true;
        velocityModule.space = ParticleSystemSimulationSpace.World;
        velocityModule.x = velocity.x;
        velocityModule.y = velocity.y;
        velocityModule.z = 0f;
    }

    private void StopWindParticles()
    {
        if (windParticleSystem == null)
            return;

        windParticleSystem.Stop(
            true,
            ParticleSystemStopBehavior.StopEmittingAndClear
        );
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
        snowParticleStartSizes =
            new float[snowParticleSystems.Length];
        snowParticleStartSpeeds =
            new float[snowParticleSystems.Length];
        snowParticleStartLifetimes =
            new float[snowParticleSystems.Length];
        snowParticleMaxCounts =
            new int[snowParticleSystems.Length];
        snowParticleShapeScales =
            new Vector3[snowParticleSystems.Length];
        snowParticleShapePositions =
            new Vector3[snowParticleSystems.Length];
        snowParticleVelocityEnabled =
            new bool[snowParticleSystems.Length];
        snowParticleVelocityX =
            new ParticleSystem.MinMaxCurve[snowParticleSystems.Length];
        snowParticleVelocitySpaces =
            new ParticleSystemSimulationSpace[snowParticleSystems.Length];

        for (int i = 0; i < snowParticleSystems.Length; i++)
        {
            ParticleSystem particleSystem = snowParticleSystems[i];
            ParticleSystem.MainModule main = particleSystem.main;
            snowParticleStartSizes[i] = main.startSizeMultiplier;
            snowParticleStartSpeeds[i] = main.startSpeedMultiplier;
            snowParticleStartLifetimes[i] =
                main.startLifetimeMultiplier;
            snowParticleMaxCounts[i] = main.maxParticles;
            main.useUnscaledTime = true;
            main.cullingMode =
                ParticleSystemCullingMode.AlwaysSimulate;

            ParticleSystem.EmissionModule emission = particleSystem.emission;
            snowParticleEmissionRates[i] =
                emission.rateOverTimeMultiplier;
            ParticleSystem.VelocityOverLifetimeModule velocity =
                particleSystem.velocityOverLifetime;
            snowParticleVelocityEnabled[i] = velocity.enabled;
            snowParticleVelocityX[i] = velocity.x;
            snowParticleVelocitySpaces[i] = velocity.space;
            ParticleSystem.ShapeModule shape = particleSystem.shape;
            snowParticleShapeScales[i] = shape.scale;
            snowParticleShapePositions[i] = shape.position;
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

            snowViewportRefreshTimeRemaining -= Time.unscaledDeltaTime;
            UpdateSnowEmitterCoverage(
                snowViewportRefreshTimeRemaining <= 0f
            );
            UpdateSnowEmitterUpstreamOffset();

            Vector3 cameraPosition = targetCamera != null
                ? targetCamera.transform.position
                : transform.position;
            snowParticleInstance.transform.position =
                GetSnowEmitterPosition(cameraPosition);

            for (int i = 0; i < snowParticleSystems.Length; i++)
            {
                ParticleSystem particleSystem = snowParticleSystems[i];
                ParticleSystem.EmissionModule emission =
                    particleSystem.emission;
                emission.rateOverTimeMultiplier =
                    snowParticleEmissionRates[i] *
                    snowParticleEmissionMultiplier *
                    normalized *
                    Mathf.Lerp(
                        activeSnowCalmEmissionMultiplier,
                        activeSnowBlizzardEmissionMultiplier,
                        currentSnowBlizzardIntensity
                    );
                ParticleSystem.MainModule main = particleSystem.main;
                main.startSizeMultiplier = snowParticleStartSizes[i] *
                    Mathf.Lerp(
                        1f,
                        SnowParticleSizeMultiplier,
                        normalized
                    );
                main.startSpeedMultiplier = snowParticleStartSpeeds[i] *
                    Mathf.Lerp(
                        1f,
                        SnowParticleSpeedMultiplier,
                        normalized
                    ) *
                    Mathf.Lerp(
                        activeSnowCalmSpeedMultiplier,
                        activeSnowBlizzardSpeedMultiplier,
                        currentSnowBlizzardIntensity
                    );
                UpdateSnowParticleVelocity(i, particleSystem);

                if (!snowParticlesPlaying)
                    particleSystem.Play(true);
            }

            snowParticlesPlaying = true;
            return;
        }

        if (!snowParticlesPlaying && !snowParticleInstance.activeSelf)
            return;

        for (int i = 0; i < snowParticleSystems.Length; i++)
        {
            ParticleSystem.MainModule main =
                snowParticleSystems[i].main;
            main.startSizeMultiplier = snowParticleStartSizes[i];
            main.startSpeedMultiplier = snowParticleStartSpeeds[i];
            ParticleSystem.EmissionModule emission =
                snowParticleSystems[i].emission;
            emission.rateOverTimeMultiplier =
                snowParticleEmissionRates[i];

            if (snowParticlesPlaying)
            {
                snowParticleSystems[i].Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear
                );
            }
        }

        snowParticlesPlaying = false;
        RestoreSnowEmitterConfiguration();

        if (snowParticleInstance.activeSelf)
            snowParticleInstance.SetActive(false);
    }

    private void InvalidateSnowViewportCache()
    {
        cachedSnowViewportWidth = -1f;
        cachedSnowViewportHeight = -1f;
        cachedSnowOrthographicSize = -1f;
        cachedSnowAspect = -1f;
        snowViewportRefreshTimeRemaining = 0f;
    }

    private void UpdateSnowEmitterCoverage(bool force)
    {
        if (!force || targetCamera == null)
            return;

        snowViewportRefreshTimeRemaining = Mathf.Max(
            0.1f,
            snowViewportRefreshInterval
        );

        if (!targetCamera.orthographic || snowParticleSystems == null)
            return;

        float orthographicSize = targetCamera.orthographicSize;
        float aspect = targetCamera.aspect;
        float visibleHeight = orthographicSize * 2f;
        float visibleWidth = visibleHeight * aspect;

        if (Mathf.Approximately(
                cachedSnowOrthographicSize,
                orthographicSize) &&
            Mathf.Approximately(cachedSnowAspect, aspect) &&
            Mathf.Approximately(cachedSnowViewportWidth, visibleWidth) &&
            Mathf.Approximately(cachedSnowViewportHeight, visibleHeight))
        {
            return;
        }

        cachedSnowOrthographicSize = orthographicSize;
        cachedSnowAspect = aspect;
        cachedSnowViewportWidth = visibleWidth;
        cachedSnowViewportHeight = visibleHeight;

        float upstreamPadding = activeSnowBlizzardHorizontalSpeed *
            snowUpstreamPaddingSeconds;
        float emitterWidth = visibleWidth *
            (1f + snowHorizontalPaddingRatio) + upstreamPadding;
        float coveredHeight = visibleHeight *
            (1f + snowVerticalPaddingRatio);

        for (int i = 0; i < snowParticleSystems.Length; i++)
        {
            ParticleSystem particleSystem = snowParticleSystems[i];

            if (particleSystem == null)
                continue;

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            Vector3 shapeScale = snowParticleShapeScales[i];
            shapeScale.x = emitterWidth;
            shape.scale = shapeScale;

            ParticleSystem.MainModule main = particleSystem.main;
            float calmFallSpeed = snowParticleStartSpeeds[i] *
                SnowParticleSpeedMultiplier *
                activeSnowCalmSpeedMultiplier;
            float requiredLifetime = coveredHeight /
                Mathf.Max(0.1f, calmFallSpeed) * 1.1f;
            float lifetime = Mathf.Max(
                snowParticleStartLifetimes[i],
                requiredLifetime
            );
            main.startLifetimeMultiplier = lifetime;

            float peakEmission = snowParticleEmissionRates[i] *
                snowParticleEmissionMultiplier *
                activeSnowBlizzardEmissionMultiplier;
            int requiredMaxParticles = Mathf.CeilToInt(
                peakEmission * lifetime * snowMaxParticlesHeadroom
            );
            main.maxParticles = Mathf.Max(
                snowParticleMaxCounts[i],
                requiredMaxParticles
            );
        }
    }

    private void UpdateSnowEmitterUpstreamOffset()
    {
        if (snowParticleSystems == null)
            return;

        float upstreamOffset = -snowBlizzardDirection *
            activeSnowBlizzardHorizontalSpeed *
            snowUpstreamPaddingSeconds *
            currentSnowBlizzardIntensity * 0.5f;

        for (int i = 0; i < snowParticleSystems.Length; i++)
        {
            ParticleSystem particleSystem = snowParticleSystems[i];

            if (particleSystem == null)
                continue;

            ParticleSystem.ShapeModule shape = particleSystem.shape;
            Vector3 shapePosition = snowParticleShapePositions[i];
            shapePosition.x += upstreamOffset;
            shape.position = shapePosition;
        }
    }

    private Vector3 GetSnowEmitterPosition(Vector3 cameraPosition)
    {
        float visibleHeight = cachedSnowViewportHeight > 0f
            ? cachedSnowViewportHeight
            : targetCamera != null && targetCamera.orthographic
                ? targetCamera.orthographicSize * 2f
                : snowParticleCameraOffset.y * 2f;
        float topPadding = visibleHeight *
            snowVerticalPaddingRatio * 0.5f;

        return new Vector3(
            cameraPosition.x,
            cameraPosition.y + visibleHeight * 0.5f + topPadding,
            cameraPosition.z + snowParticleCameraOffset.z
        );
    }

    private void RestoreSnowEmitterConfiguration()
    {
        if (snowParticleSystems == null ||
            snowParticleStartLifetimes == null ||
            snowParticleMaxCounts == null ||
            snowParticleShapeScales == null ||
            snowParticleShapePositions == null)
        {
            return;
        }

        for (int i = 0; i < snowParticleSystems.Length; i++)
        {
            ParticleSystem particleSystem = snowParticleSystems[i];

            if (particleSystem == null)
                continue;

            ParticleSystem.MainModule main = particleSystem.main;
            main.startLifetimeMultiplier = snowParticleStartLifetimes[i];
            main.maxParticles = snowParticleMaxCounts[i];
            ParticleSystem.ShapeModule shape = particleSystem.shape;
            shape.scale = snowParticleShapeScales[i];
            shape.position = snowParticleShapePositions[i];
        }

        InvalidateSnowViewportCache();
    }

    private void UpdateSnowParticleVelocity(
        int index,
        ParticleSystem particleSystem)
    {
        ParticleSystem.VelocityOverLifetimeModule velocity =
            particleSystem.velocityOverLifetime;
        float horizontalVelocity = snowBlizzardDirection *
            activeSnowBlizzardHorizontalSpeed *
            currentSnowBlizzardIntensity;

        if (Mathf.Abs(horizontalVelocity) <= 0.001f)
        {
            velocity.enabled = snowParticleVelocityEnabled[index];
            velocity.space = snowParticleVelocitySpaces[index];
            velocity.x = snowParticleVelocityX[index];
            return;
        }

        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.World;
        velocity.x = horizontalVelocity;
    }

    private void RestoreSnowParticleVelocityModules()
    {
        if (snowParticleSystems == null ||
            snowParticleVelocityEnabled == null ||
            snowParticleVelocityX == null ||
            snowParticleVelocitySpaces == null)
        {
            return;
        }

        for (int i = 0; i < snowParticleSystems.Length; i++)
        {
            ParticleSystem particleSystem = snowParticleSystems[i];

            if (particleSystem == null)
                continue;

            ParticleSystem.VelocityOverLifetimeModule velocity =
                particleSystem.velocityOverLifetime;
            velocity.enabled = snowParticleVelocityEnabled[i];
            velocity.space = snowParticleVelocitySpaces[i];
            velocity.x = snowParticleVelocityX[i];
        }
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
                GetSnowEmitterPosition(cameraPosition);
        }

        if (windParticleSystem != null && windParticleSystem.isPlaying)
        {
            windParticleSystem.transform.position = new Vector3(
                cameraPosition.x,
                cameraPosition.y,
                0f
            );
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

        if (windParticleObject != null)
            Destroy(windParticleObject);

        if (darknessRevealObject != null)
            Destroy(darknessRevealObject);
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
