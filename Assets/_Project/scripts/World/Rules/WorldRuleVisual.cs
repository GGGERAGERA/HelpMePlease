using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Serialization;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WorldRuleVisual : MonoBehaviour
{
    private static readonly int RuleTypeId =
        Shader.PropertyToID("_RuleType");
    private static readonly int IntensityId =
        Shader.PropertyToID("_Intensity");
    private static readonly int VisualTimeId =
        Shader.PropertyToID("_VisualTime");
    private static readonly int PulseSpeedId =
        Shader.PropertyToID("_PulseSpeed");
    private static readonly int PulseStrengthId =
        Shader.PropertyToID("_PulseStrength");
    private static readonly int EdgeIntensityId =
        Shader.PropertyToID("_EdgeIntensity");
    private static readonly int SnowIntensityId =
        Shader.PropertyToID("_SnowIntensity");
    private static readonly int SnowColorId =
        Shader.PropertyToID("_SnowColor");
    private static readonly int SnowDensityId =
        Shader.PropertyToID("_SnowDensity");
    private static readonly int SnowScaleId =
        Shader.PropertyToID("_SnowScale");

    [Header("References")]
    [SerializeField] private Image fullscreenImage;
    [SerializeField] private Material visualMaterial;
    [SerializeField] private WindRuleIndicator windIndicator;

    [Header("Rain / Existing Scene Effect")]
    [SerializeField] private GameObject rainEffect;

    [Header("Darkness / Existing Scene Light")]
    [SerializeField] private Light2D globalLight;
    [SerializeField, Min(0f)] private float normalLightIntensity = 1f;
    [SerializeField, Min(0f)] private float darknessLightIntensity = 0.01f;

    [Header("Transition")]
    [SerializeField, Min(0.01f)] private float transitionDuration = 1f;
    [SerializeField, Range(0f, 1f)] private float globalIntensity = 1f;

    [Header("Animation")]
    [SerializeField, Min(0f)] private float pulseSpeed = 1f;
    [SerializeField, Range(0f, 1f)] private float pulseStrength = 0.3f;
    [SerializeField, Range(0f, 2f)] private float edgeIntensity = 1f;

    [Header("Rule Strength")]
    [SerializeField, Range(0f, 1f)] private float hasteIntensity = 0.42f;
    [SerializeField, Range(0f, 1f)] private float regenerationIntensity = 0.38f;
    [SerializeField, Range(0f, 1f)] private float explosiveIntensity = 0.48f;

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

    private float currentIntensity;
    private float targetIntensity;
    private float currentSnowIntensity;
    private float targetSnowIntensity;
    private GameObject snowWorldObject;
    private Mesh snowWorldMesh;
    private MeshRenderer snowWorldRenderer;
    private MaterialPropertyBlock snowProperties;
    private GameObject snowVolumeObject;
    private Volume snowVolume;
    private VolumeProfile snowVolumeProfile;
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
        ApplyTuning();
        SetNeutral();
    }

    private void Update()
    {
        float step = Time.unscaledDeltaTime /
            Mathf.Max(0.01f, transitionDuration);
        currentIntensity = Mathf.MoveTowards(
            currentIntensity,
            targetIntensity,
            step
        );

        float snowStep =
            Mathf.Max(0.0001f, snowVisualIntensity) *
            Time.unscaledDeltaTime /
            Mathf.Max(0.01f, snowTransitionDuration);
        currentSnowIntensity = Mathf.MoveTowards(
            currentSnowIntensity,
            targetSnowIntensity,
            snowStep
        );

        if (visualMaterial != null)
        {
            visualMaterial.SetFloat(IntensityId, currentIntensity);
            visualMaterial.SetFloat(VisualTimeId, Time.unscaledTime);
            visualMaterial.SetFloat(
                SnowIntensityId,
                currentSnowIntensity * snowScreenOpacity
            );
        }

        UpdateSnowResources();

#if UNITY_EDITOR
        UpdateSnowDiagnostics();
#endif

        if (fullscreenImage != null &&
            targetIntensity <= 0f &&
            currentIntensity <= 0f &&
            targetSnowIntensity <= 0f &&
            currentSnowIntensity <= 0f)
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
            case WorldRuleType.ExplosiveInfection:
            case WorldRuleType.Haste:
            case WorldRuleType.Regeneration:
                ShowRule(rule.RuleType);
                break;

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
            case WorldRuleType.Golden:
                break;
        }
    }

    public void Clear()
    {
        ClearRule();
        SetSnowActive(false);
        ClearRainAndDarkness();
        windIndicator?.Hide();
    }

    public void ShowWind(Vector2 direction)
    {
        windIndicator?.Show(direction);
    }

    public void ShowRule(WorldRuleType ruleType)
    {
        if (visualMaterial == null)
            return;

        if (fullscreenImage != null)
            fullscreenImage.enabled = true;

        visualMaterial.SetFloat(RuleTypeId, (float)ruleType);
        targetIntensity = GetIntensity(ruleType) * globalIntensity;
    }

    public void ClearRule()
    {
        targetIntensity = 0f;
    }

    public void ClearImmediate()
    {
        SetNeutral();
    }

    public void ClearRuleImmediate()
    {
        currentIntensity = 0f;
        targetIntensity = 0f;

        if (visualMaterial != null)
            visualMaterial.SetFloat(IntensityId, 0f);

        RefreshFullscreenVisibility();
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

    private float GetIntensity(WorldRuleType ruleType)
    {
        switch (ruleType)
        {
            case WorldRuleType.Haste:
                return hasteIntensity;

            case WorldRuleType.Regeneration:
                return regenerationIntensity;

            case WorldRuleType.ExplosiveInfection:
                return explosiveIntensity;

            default:
                return 0f;
        }
    }

    private void SetNeutral()
    {
        ClearRainAndDarkness();
        windIndicator?.Hide();
        currentIntensity = 0f;
        targetIntensity = 0f;
        currentSnowIntensity = 0f;
        targetSnowIntensity = 0f;

#if UNITY_EDITOR
        snowDiagnosticElapsed = 0f;
        snowDiagnosticIndex = 0;
        snowDiagnosticsActive = false;
#endif

        if (visualMaterial != null)
        {
            visualMaterial.SetFloat(IntensityId, 0f);
            visualMaterial.SetFloat(SnowIntensityId, 0f);
        }

        if (fullscreenImage != null)
            fullscreenImage.enabled = false;

        UpdateSnowResources();
    }

    private void ApplyRain()
    {
        if (rainEffect == null)
            return;

        rainEffect.SetActive(true);
    }

    private void ApplyDarkness()
    {
        if (globalLight == null)
            return;

        globalLight.intensity = darknessLightIntensity;
    }

    private void ClearRainAndDarkness()
    {
        if (rainEffect != null)
            rainEffect.SetActive(false);

        if (globalLight != null)
            globalLight.intensity = normalLightIntensity;
    }

    private void ApplyTuning()
    {
        if (visualMaterial == null)
            return;

        visualMaterial.SetFloat(PulseSpeedId, pulseSpeed);
        visualMaterial.SetFloat(PulseStrengthId, pulseStrength);
        visualMaterial.SetFloat(EdgeIntensityId, edgeIntensity);
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
            currentIntensity > 0f ||
            targetIntensity > 0f ||
            currentSnowIntensity > 0f ||
            targetSnowIntensity > 0f;
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

        if (snowVolumeProfile != null)
            Destroy(snowVolumeProfile);
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

    [ContextMenu("Preview/Haste")]
    private void PreviewHaste()
    {
        Preview(WorldRuleType.Haste);
    }

    [ContextMenu("Preview/Regeneration")]
    private void PreviewRegeneration()
    {
        Preview(WorldRuleType.Regeneration);
    }

    [ContextMenu("Preview/Explosive Infection")]
    private void PreviewExplosiveInfection()
    {
        Preview(WorldRuleType.ExplosiveInfection);
    }

    [ContextMenu("Preview/Clear")]
    private void PreviewClear()
    {
        ClearImmediate();
    }

    private void Preview(WorldRuleType ruleType)
    {
        if (visualMaterial == null)
            return;

        if (fullscreenImage != null)
            fullscreenImage.enabled = true;

        visualMaterial.SetFloat(RuleTypeId, (float)ruleType);
        ApplyTuning();
        currentIntensity = GetIntensity(ruleType) * globalIntensity;
        targetIntensity = currentIntensity;
        visualMaterial.SetFloat(IntensityId, currentIntensity);
        visualMaterial.SetFloat(
            VisualTimeId,
            (float)UnityEditor.EditorApplication.timeSinceStartup
        );
    }
#endif
}
