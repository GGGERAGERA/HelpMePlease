using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WorldRuleVisual : MonoBehaviour
{
    private static readonly int VisualTimeId =
        Shader.PropertyToID("_VisualTime");
    private static readonly int SnowIntensityId =
        Shader.PropertyToID("_SnowIntensity");
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
    [Header("References")]
    [SerializeField] private Image fullscreenImage;
    [SerializeField] private Material visualMaterial;
    [SerializeField] private WindRuleIndicator windIndicator;
    [SerializeField] private CondensationFogOverlay condensationFogOverlay;

    [Header("Authored World Rule Visuals")]
    [SerializeField] private WorldRuleAuthoredVisual rainVisualPrefab;
    [SerializeField] private WorldRuleAuthoredVisual windVisualPrefab;
    [SerializeField] private WorldRuleAuthoredVisual goldenVisualPrefab;
    private WorldRuleAuthoredVisual rainVisual;
    private WorldRuleAuthoredVisual windVisual;
    private WorldRuleAuthoredVisual goldenVisual;

    [SerializeField] private WorldRuleAuthoredVisual snowVisualPrefab;
    [SerializeField] private WorldRuleAuthoredVisual darknessVisualPrefab;
    private WorldRuleAuthoredVisual snowVisual;
    private WorldRuleAuthoredVisual darknessVisual;
    private WorldRuleAuthoredVisual SnowPresentation => snowVisual != null ? snowVisual : snowVisualPrefab;

    [Header("Darkness / Existing 2D Lights")]
    [SerializeField] private Light2D globalLight;
    [SerializeField, Min(0.1f)] private float playerLightRadius = 6.5f;
    [SerializeField, Min(0f)] private float playerLightIntensity = 1f;
    [SerializeField, Range(0f, 1f)] private float playerLightFalloff = 0.75f;
    [SerializeField, Min(0.01f)] private float transitionDuration = 1f;

    [Header("Snow / Transition")]
    [SerializeField, Min(0.01f)] private float snowTransitionDuration = 1f;

    [SerializeField] private Camera targetCamera;
    private float snowWorldDepth;

    [Header("Snow / Falling Particles")]
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
    private float debugDarknessOverlayMultiplier = 1f;
    private float debugPlayerGlowIntensityMultiplier = 1f;
    private float debugPlayerGlowRadiusMultiplier = 1f;
    private float debugWindDustAmountMultiplier = 1f;
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
    private readonly Dictionary<Object, float>
        playerLightRadiusMultipliers = new();
    private readonly Dictionary<Object, float>
        blackoutGlobalLightMultipliers = new();
    private const float EyesMinimumRadiusMultiplier = 0.6f;
    private const float EyesMinimumGlobalLightMultiplier = 0.35f;
    private Light2D darknessRevealLight;
    private float darknessRevealRemaining;
    private float darknessShotRevealRadius = 3.5f;
    private float darknessShotRevealDuration = 0.12f;
    private float darknessShotRevealIntensity = 1.25f;
    private GameObject snowWorldObject;
    private MeshRenderer snowWorldRenderer;
    private MaterialPropertyBlock snowProperties;
    private Volume snowVolume;
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
    private Image debugDarknessImage;
    private Material ownedVisualMaterial;

    // Native snow was authored for soft textures; solid pixel flakes need a
    // bounded screen population and a one-texel size to preserve combat visibility.

    public bool PlayerGlowAvailable
    {
        get
        {
            ResolvePlayerLight();
            return playerLight != null && playerLightStateCaptured;
        }
    }

    public float PlayerGlowIntensityMultiplier =>
        debugPlayerGlowIntensityMultiplier;
    public float PlayerGlowRadiusMultiplier => debugPlayerGlowRadiusMultiplier;
    public float WindDustAmountMultiplier => debugWindDustAmountMultiplier;

    public void SetPlayerGlowIntensityMultiplier(float value)
    {
        debugPlayerGlowIntensityMultiplier = Mathf.Clamp(value, 0f, 5f);
        UpdateDarknessResources();
    }

    public void SetPlayerGlowRadiusMultiplier(float value)
    {
        debugPlayerGlowRadiusMultiplier = Mathf.Clamp(value, 0.1f, 5f);
        UpdateDarknessResources();
    }

    public void ResetPlayerGlowDebugSettings()
    {
        debugPlayerGlowIntensityMultiplier = 1f;
        debugPlayerGlowRadiusMultiplier = 1f;
        UpdateDarknessResources();
    }

    public void SetWindDustAmountMultiplier(float value)
    {
        debugWindDustAmountMultiplier = Mathf.Clamp(value, 0f, 5f);
        if (windVisual != null) windVisual.SetAmount(debugWindDustAmountMultiplier);
    }

    public void ResetWindDustDebugSettings()
    {
        SetWindDustAmountMultiplier(1f);
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void SetDebugDarknessOverlayMultiplier(float multiplier)
    {
        debugDarknessOverlayMultiplier = Mathf.Clamp01(multiplier);
    }

    public void ConfigureDebugRuntime(
        Image screenImage,
        Image darknessImage,
        CondensationFogOverlay condensation,
        Material screenMaterial,
        Material rainMaterial,
        Material snowMaterial,
        GameObject snowParticles,
        Camera camera,
        Sprite markerSprite,
        Material markerMaterial)
    {
        fullscreenImage = screenImage;
        debugDarknessImage = darknessImage;
        condensationFogOverlay = condensation;
        targetCamera = camera;

        if (screenMaterial != null)
        {
            if (ownedVisualMaterial != null) Destroy(ownedVisualMaterial);
            ownedVisualMaterial = new Material(screenMaterial)
            {
                name = "Sandbox World Rule Overlay (Runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };
            visualMaterial = ownedVisualMaterial;
        }

        if (fullscreenImage != null)
        {
            fullscreenImage.raycastTarget = false;
            fullscreenImage.material = visualMaterial;
        }
        if (debugDarknessImage != null)
        {
            debugDarknessImage.raycastTarget = false;
            debugDarknessImage.color = Color.clear;
        }

        EnsureSnowResources();
        EnsureAuthoredVisuals();
        SetNeutral();
    }
#endif

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
        if (visualMaterial != null)
        {
            // Per-view UI state must not mutate the shared authored material asset.
            ownedVisualMaterial = new Material(visualMaterial) { hideFlags = HideFlags.HideAndDontSave };
            visualMaterial = ownedVisualMaterial;
        }
        activeSnowTransitionDuration = snowTransitionDuration;

        if (fullscreenImage != null)
        {
            fullscreenImage.raycastTarget = false;
            fullscreenImage.material = visualMaterial;
        }

        EnsureSnowResources();
        EnsureAuthoredVisuals();
        SetNeutral();
    }

    private void Update()
    {
        float step = Time.unscaledDeltaTime /
            Mathf.Max(0.01f, transitionDuration);
        float snowStep = snowVisualPrefab == null ? 0f :
            Mathf.Max(0.0001f, SnowPresentation.snowVisualIntensity) *
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

        if (visualMaterial != null && snowVisualPrefab != null)
        {
            float normalizedSnow = SnowPresentation.snowVisualIntensity > 0f
                ? Mathf.Clamp01(
                    currentSnowIntensity / SnowPresentation.snowVisualIntensity
                )
                : 0f;
            visualMaterial.SetFloat(VisualTimeId, Time.unscaledTime);
            visualMaterial.SetFloat(
                SnowIntensityId,
                currentSnowIntensity * SnowPresentation.snowScreenOpacity *
                Mathf.Lerp(
                    1f,
                    1.35f,
                    currentSnowBlizzardIntensity
                )
            );
            visualMaterial.SetFloat(
                BlizzardIntensityId,
                normalizedSnow * currentSnowBlizzardIntensity *
                SnowPresentation.blizzardIntensity * activeSnowBlizzardVisibilityEffect
            );
            visualMaterial.SetFloat(
                BlizzardLineDensityId,
                SnowPresentation.blizzardLineDensity
            );
            visualMaterial.SetFloat(
                BlizzardLineSpeedId,
                SnowPresentation.blizzardLineSpeed
            );
            visualMaterial.SetFloat(
                BlizzardVeilId,
                normalizedSnow * currentSnowBlizzardIntensity *
                SnowPresentation.blizzardVeil * activeSnowBlizzardVisibilityEffect
            );
            visualMaterial.SetFloat(
                BlizzardDirectionId,
                snowBlizzardDirection
            );

        }

        UpdateSnowResources();
        UpdateRainResources();
        UpdateDarknessResources();
        if (goldenVisual != null) goldenVisual.SetIntensity(currentGoldenIntensity);

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
        targetSnowIntensity = active && snowVisualPrefab != null ? SnowPresentation.snowVisualIntensity : 0f;

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
        SetWindActive(false);
        SetGoldenActive(false);

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
        }

        if (fullscreenImage != null)
            fullscreenImage.enabled = false;

        UpdateSnowResources();
        UpdateRainResources();
        UpdateDarknessResources();
        if (goldenVisual != null) goldenVisual.SetIntensity(currentGoldenIntensity);
    }

    private void SetCondensationActive(bool active)
    {
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
        if (darknessVisual != null)
        {
            darknessVisual.SetRuleActive(currentDarknessIntensity > 0f || targetDarknessIntensity > 0f, Vector2.zero);
            darknessVisual.SetIntensity(currentDarknessIntensity * debugDarknessOverlayMultiplier);
        }
        float radiusMultiplier = GetPlayerLightRadiusMultiplier();
        float eyesStrength = Mathf.InverseLerp(
            1f,
            EyesMinimumRadiusMultiplier,
            radiusMultiplier
        );

        if (globalLight != null && globalLightStateCaptured)
        {
            float baseGlobalIntensity = Mathf.Lerp(
                baselineGlobalLightIntensity,
                darknessVisualPrefab != null ? darknessVisualPrefab.darknessGlobalIntensity : baselineGlobalLightIntensity,
                currentDarknessIntensity
            );
            float eyesGlobalMultiplier = Mathf.Lerp(
                1f,
                EyesMinimumGlobalLightMultiplier,
                eyesStrength
            );
            globalLight.intensity =
                baseGlobalIntensity * eyesGlobalMultiplier *
                GetBlackoutGlobalLightMultiplier();
        }

        if (currentDarknessIntensity > 0f ||
            targetDarknessIntensity > 0f ||
            playerLightRadiusMultipliers.Count > 0)
        {
            ResolvePlayerLight();
        }

        if (playerLight == null || !playerLightStateCaptured)
            return;

        float baseRadius = Mathf.Lerp(
            normalPlayerLightRadius,
            playerLightRadius,
            currentDarknessIntensity
        );
        playerLight.pointLightOuterRadius = baseRadius * radiusMultiplier *
            debugPlayerGlowRadiusMultiplier;
        playerLight.intensity = Mathf.Lerp(
            normalPlayerLightIntensity,
            playerLightIntensity,
            currentDarknessIntensity
        ) * debugPlayerGlowIntensityMultiplier;
        playerLight.falloffIntensity = Mathf.Lerp(
            normalPlayerLightFalloff,
            playerLightFalloff,
            currentDarknessIntensity
        );
    }

    public void SetPlayerLightRadiusMultiplier(
        Object source,
        float multiplier)
    {
        if (source == null)
            return;

        playerLightRadiusMultipliers[source] = Mathf.Clamp01(multiplier);
    }

    public void RemovePlayerLightRadiusMultiplier(Object source)
    {
        if (ReferenceEquals(source, null))
            return;

        if (!playerLightRadiusMultipliers.Remove(source))
            return;

        UpdateDarknessResources();
    }

    private float GetPlayerLightRadiusMultiplier()
    {
        float result = 1f;

        foreach (float multiplier in playerLightRadiusMultipliers.Values)
            result = Mathf.Min(result, multiplier);

        return result;
    }

    public void SetBlackoutGlobalLightMultiplier(
        Object source,
        float multiplier)
    {
        if (source == null)
            return;

        blackoutGlobalLightMultipliers[source] =
            Mathf.Clamp01(multiplier);
        UpdateDarknessResources();
    }

    public void RemoveBlackoutGlobalLightMultiplier(Object source)
    {
        if (ReferenceEquals(source, null))
            return;

        if (!blackoutGlobalLightMultipliers.Remove(source))
            return;

        UpdateDarknessResources();
    }

    private float GetBlackoutGlobalLightMultiplier()
    {
        float result = 1f;

        foreach (float multiplier in blackoutGlobalLightMultipliers.Values)
            result = Mathf.Min(result, multiplier);

        return result;
    }

    private void ResolvePlayerLight()
    {
        if (playerLight != null)
            return;

        playerLightStateCaptured = false;

        Transform player = PlayerRuntimeReference.ResolvePlayerTransform(forceLookup: true);

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
        darknessRevealLight.transform.position = new Vector3(
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
        if (darknessRevealLight != null || darknessVisual == null) return;
        darknessRevealLight = darknessVisual.darknessShotReveal;
        ResolvePlayerLight();
        if (playerLight != null && darknessRevealLight != null)
            darknessRevealLight.lightCookieSprite = playerLight.lightCookieSprite;
    }

    private void SetRainActive(bool active)
    {
        targetRainIntensity = active ? 1f : 0f;
        if (rainVisual != null) rainVisual.SetRuleActive(active, Vector2.down);
    }

    private void EnsureSnowResources()
    {
        if (snowVisual != null || snowVisualPrefab == null) return;
        snowVisual = CreateAuthoredVisual(snowVisualPrefab);
        snowVisual.gameObject.SetActive(true);
        snowWorldRenderer = snowVisual.snowGround;
        snowWorldObject = snowWorldRenderer.gameObject;
        snowWorldDepth = snowWorldRenderer.transform.localPosition.z;
        snowProperties = new MaterialPropertyBlock();
        snowVolume = snowVisual.snowVolume;
        snowParticleInstance = snowVisual.snowParticles;
        CreateSnowParticles();
    }

    private void EnsureAuthoredVisuals()
    {
        if (darknessVisual == null && darknessVisualPrefab != null)
            darknessVisual = CreateAuthoredVisual(darknessVisualPrefab);
        if (rainVisual == null && rainVisualPrefab != null)
            rainVisual = CreateAuthoredVisual(rainVisualPrefab);
        if (windVisual == null && windVisualPrefab != null)
            windVisual = CreateAuthoredVisual(windVisualPrefab);
        if (goldenVisual == null && goldenVisualPrefab != null)
            goldenVisual = CreateAuthoredVisual(goldenVisualPrefab);
    }

    private WorldRuleAuthoredVisual CreateAuthoredVisual(WorldRuleAuthoredVisual prefab)
    {
        var instance = Instantiate(prefab, prefab.IsScreenOverlay ? null : transform);
        instance.gameObject.SetActive(false);
        instance.Bind(targetCamera);
        return instance;
    }

    private void SetGoldenActive(bool active)
    {
        targetGoldenIntensity = active ? 1f : 0f;
        if (goldenVisual != null) goldenVisual.SetRuleActive(active, Vector2.zero);
    }

    private void SetWindActive(bool active)
    {
        targetWindIntensity = active ? 1f : 0f;
        if (windVisual != null) windVisual.SetRuleActive(active, windVisualDirection);
    }

    private void UpdateRainResources()
    {
        if (rainVisual != null) rainVisual.SetIntensity(currentRainIntensity);
    }

    private void CreateSnowParticles()
    {
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
        if (snowVisual == null) return;
        float normalized = SnowPresentation.snowVisualIntensity > 0f
            ? Mathf.Clamp01(currentSnowIntensity / SnowPresentation.snowVisualIntensity)
            : 0f;

        if (snowVolume != null)
            snowVolume.weight = normalized * SnowPresentation.snowVolumeWeight;

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

        snowWorldRenderer.GetPropertyBlock(snowProperties);
        snowProperties.SetFloat(SnowIntensityId, currentSnowIntensity);
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
                    SnowPresentation.snowParticleEmissionMultiplier *
                    normalized *
                    Mathf.Lerp(
                        activeSnowCalmEmissionMultiplier,
                        activeSnowBlizzardEmissionMultiplier,
                        currentSnowBlizzardIntensity
                    );
                ParticleSystem.MainModule main = particleSystem.main;

                float emissionBudget = snowParticleMaxCounts[i] /
                    Mathf.Max(0.1f, main.startLifetimeMultiplier);
                emission.rateOverTimeMultiplier = Mathf.Min(
                    emission.rateOverTimeMultiplier,
                    emissionBudget * normalized *
                    Mathf.Lerp(0.35f, 1f, currentSnowBlizzardIntensity));
                main.startSpeedMultiplier = snowParticleStartSpeeds[i] *
                    Mathf.Lerp(
                        1f,
                        SnowPresentation.snowFallSpeedMultiplier,
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
                SnowPresentation.snowFallSpeedMultiplier *
                activeSnowCalmSpeedMultiplier;
            float requiredLifetime = coveredHeight /
                Mathf.Max(0.1f, calmFallSpeed) * 1.1f;
            float lifetime = Mathf.Max(
                snowParticleStartLifetimes[i],
                requiredLifetime
            );
            main.startLifetimeMultiplier = lifetime;

            float peakEmission = snowParticleEmissionRates[i] *
                SnowPresentation.snowParticleEmissionMultiplier *
                activeSnowBlizzardEmissionMultiplier;
            int requiredMaxParticles = Mathf.CeilToInt(
                peakEmission * lifetime * snowMaxParticlesHeadroom
            );
            main.maxParticles = Mathf.Clamp(
                requiredMaxParticles, 1,
                snowParticleMaxCounts[i]);
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

        if (snowParticleInstance != null &&
            snowParticleInstance.activeSelf)
        {
            snowParticleInstance.transform.position =
                GetSnowEmitterPosition(cameraPosition);
        }

    }

    private void OnDisable()
    {
        ResetPlayerGlowDebugSettings();
        ResetWindDustDebugSettings();
        playerLightRadiusMultipliers.Clear();
        blackoutGlobalLightMultipliers.Clear();
        SetNeutral();
    }

    private void OnDestroy()
    {
        // The screen Canvas is a scene root; this controller still owns its lifetime.
        if (goldenVisual != null)
            Destroy(goldenVisual.gameObject);

        if (ownedVisualMaterial != null)
            Destroy(ownedVisualMaterial);
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
