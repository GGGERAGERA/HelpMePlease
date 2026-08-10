using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
public sealed class GiantObserverBackgroundController : MonoBehaviour
{
    public enum ObserverIntensity { Low, Normal, High }
    public enum PerspectivePreset { Narrow, Normal, Wide }
    public enum FarDistancePreset { Far, VeryFar }
    public enum NearDistancePreset { Close, VeryClose }

    private enum EventPhase
    {
        Hidden,
        DistantReveal,
        Approach,
        CloseObserve,
        FadeOut
    }

    private sealed class RendererVisual
    {
        public Renderer Renderer;
        public Material[] Materials;
        public bool[] SensorMaterials;
    }

    private const int ObserverLayer = 31;
    private const int GameplayRendererIndex = 0;
    private const int ObserverRendererIndex = 1;
    private const float DistantRevealDuration = 1f;
    private const float ApproachDuration = 4f;
    private const float CloseObserveDuration = 2.5f;
    private const float FadeOutDuration = 0.75f;
    private const float ForceVisibleDistance = 25f;
    private const float BackgroundCameraTestDuration = 2f;
    private static readonly Color ObserverBackgroundColor =
        new(0.004f, 0.008f, 0.018f, 1f);
    private static readonly Color CameraTestColor = Color.magenta;
    private static readonly Quaternion RuntimeRobotRotation =
        Quaternion.Euler(8f, -6f, 0f);

    private readonly List<RendererVisual> robotVisuals = new();
    private readonly List<string> disabledRuntimeComponents = new();
    private readonly HashSet<Material> observerMaterials = new();

    private Camera gameplayCamera;
    private Camera backgroundCamera;
    private UniversalAdditionalCameraData backgroundCameraData;
    private UniversalAdditionalCameraData gameplayCameraData;
    private WorldRuleVisual worldRuleVisual;
    private GameObject observerPrefab;
    private Transform observerWorld;
    private Transform robotPivot;
    private GameObject robotInstance;
    private Light keyLight;
    private Light rimLight;
    private EventPhase phase;
    private ObserverIntensity intensity = ObserverIntensity.Normal;
    private PerspectivePreset perspective = PerspectivePreset.Normal;
    private FarDistancePreset farPreset = FarDistancePreset.VeryFar;
    private NearDistancePreset nearPreset = NearDistancePreset.VeryClose;
    private bool observerEnabled;
    private bool autoTrigger;
    private bool showConstantly;
    private bool prefabLoaded;
    private bool usesPrefabSensor;
    private bool environmentCaptured;
    private bool compositorConfigured;
    private bool forceVisible;
    private bool backgroundCameraTest;
    private float intervalMin = 10f;
    private float intervalMax = 15f;
    private float phaseStartedAt;
    private float nextTriggerAt;
    private float currentDistance;
    private float modelWidth = 1f;
    private float modelHeight = 1f;
    private float robotVerticalOffset;
    private float currentBodyLevel;
    private float currentSensorLevel;
    private float currentLightLevel;
    private float backgroundCameraTestEndsAt;
    private int originalGameplayCullingMask;
    private float originalGameplayDepth;
    private CameraClearFlags originalGameplayClearFlags;
    private Color originalGameplayBackground;
    private CameraRenderType originalGameplayRenderType;
    private Color gameplayBackgroundBeforeEvent;

    public bool ObserverEnabled => observerEnabled;
    public bool AutoTrigger => autoTrigger;
    public bool ShowConstantly => showConstantly;
    public bool IsVisible => phase != EventPhase.Hidden || showConstantly || forceVisible ||
        backgroundCameraTest;
    public bool PrefabLoaded => prefabLoaded;
    public bool UsesPrefabSensor => usesPrefabSensor;
    public bool CameraStackOk => compositorConfigured && backgroundCamera != null &&
        gameplayCamera != null && backgroundCameraData != null &&
        gameplayCameraData != null && (!backgroundCamera.enabled ||
        (backgroundCameraData.renderType == CameraRenderType.Base &&
         gameplayCameraData.renderType == CameraRenderType.Overlay &&
         backgroundCameraData.cameraStack.Contains(gameplayCamera) &&
         GetBackgroundRendererName() == GetGameplayRendererName()));
    public float IntervalMin => intervalMin;
    public float IntervalMax => intervalMax;
    public float CurrentDistance => currentDistance;
    public float RobotScreenCoverage => CalculateScreenCoverage(currentDistance);
    public int RendererCount => robotVisuals.Count;
    public int EnabledRendererCount => CountEnabledRenderers();
    public int MaterialCount => observerMaterials.Count;
    public int UnsupportedShaderCount => CountUnsupportedShaders();
    public bool BackgroundCameraActive => backgroundCamera != null && backgroundCamera.enabled;
    public bool ForwardRendererOk => GetBackgroundRendererName() == "UniversalRenderer";
    public bool GameplayForwardRendererOk =>
        GetGameplayRendererName() == "UniversalRenderer";
    public bool ObserverMaskOk => backgroundCamera != null &&
        (backgroundCamera.cullingMask & (1 << ObserverLayer)) != 0;
    public int ObserverLayerIndex => ObserverLayer;
    public bool RobotActive => robotInstance != null && robotInstance.activeInHierarchy;
    public bool RobotLayersOk => robotInstance != null && LayersMatch(robotInstance.transform);
    public Vector3 RobotWorldPosition => robotPivot != null ? robotPivot.position : Vector3.zero;
    public Vector3 BackgroundCameraForward => backgroundCamera != null
        ? backgroundCamera.transform.forward : Vector3.forward;
    public float BackgroundNearClip => backgroundCamera != null
        ? backgroundCamera.nearClipPlane : 0f;
    public float BackgroundFarClip => backgroundCamera != null
        ? backgroundCamera.farClipPlane : 0f;
    public bool RobotInFrontOfCamera => IsRobotInFront();
    public string LastRenderState => GetLastRenderState();
    public ObserverIntensity Intensity => intensity;
    public PerspectivePreset Perspective => perspective;
    public FarDistancePreset FarPreset => farPreset;
    public NearDistancePreset NearPreset => nearPreset;
    public IReadOnlyList<string> DisabledRuntimeComponents => disabledRuntimeComponents;

    public string CurrentStateName => backgroundCameraTest ? "BACKGROUND_CAMERA_TEST" :
        forceVisible ? "FORCE_VISIBLE" :
        showConstantly ? "CLOSE_OBSERVE (CONSTANT)" :
        phase switch
        {
            EventPhase.DistantReveal => "DISTANT_REVEAL",
            EventPhase.Approach => "APPROACH",
            EventPhase.CloseObserve => "CLOSE_OBSERVE",
            EventPhase.FadeOut => "FADE_OUT",
            _ => "HIDDEN"
        };

    public void Configure(Camera targetCamera, WorldRuleVisual ruleVisual,
        GameObject prefab)
    {
        gameplayCamera = targetCamera;
        worldRuleVisual = ruleVisual;
        observerPrefab = prefab;
        originalGameplayCullingMask = gameplayCamera.cullingMask;
        originalGameplayDepth = gameplayCamera.depth;
        originalGameplayClearFlags = gameplayCamera.clearFlags;
        originalGameplayBackground = gameplayCamera.backgroundColor;
        gameplayCamera.cullingMask &= ~(1 << ObserverLayer);
        CreateObserverWorld();
        SetPresentationActive(false);
        ScheduleNextTrigger();
    }

    public void SetObserverEnabled(bool enabled)
    {
        observerEnabled = enabled;
        if (!enabled)
        {
            showConstantly = false;
            forceVisible = false;
            backgroundCameraTest = false;
            HideImmediately();
        }
        else
        {
            ScheduleNextTrigger();
        }
    }

    public void SetAutoTrigger(bool enabled)
    {
        autoTrigger = enabled;
        if (enabled) ScheduleNextTrigger();
    }

    public void SetShowConstantly(bool enabled)
    {
        if (!enabled)
        {
            showConstantly = false;
            HideImmediately();
            return;
        }

        observerEnabled = true;
        forceVisible = false;
        backgroundCameraTest = false;
        showConstantly = true;
        phase = EventPhase.Hidden;
        CaptureEnvironment();
        SetPresentationActive(true);
        SetRobotDistance(NearDistance());
        SetVisualLevels(1f, 1f, 1f);
        ApplyRevealEnvironment(1f);
    }

    public void ShowForceVisible()
    {
        if (!prefabLoaded) return;
        observerEnabled = true;
        showConstantly = false;
        backgroundCameraTest = false;
        forceVisible = true;
        phase = EventPhase.Hidden;
        intensity = ObserverIntensity.High;
        CaptureEnvironment();
        SetPresentationActive(true);
        SetRobotDistance(ForceVisibleDistance, true);
        SetVisualLevels(1.6f, 1.6f, 1f);
        ApplyForceVisibleEnvironment();
    }

    public void StartBackgroundCameraTest()
    {
        observerEnabled = true;
        showConstantly = false;
        forceVisible = false;
        backgroundCameraTest = true;
        phase = EventPhase.Hidden;
        backgroundCameraTestEndsAt = Time.unscaledTime + BackgroundCameraTestDuration;
        SetVisualLevels(0f, 0f, 0f);
        SetPresentationActive(true);
        backgroundCamera.backgroundColor = CameraTestColor;
    }

    public bool TriggerNow()
    {
        if (!observerEnabled || !prefabLoaded || IsVisible)
            return false;

        CaptureEnvironment();
        SetRobotDistance(FarDistance());
        SetVisualLevels(0.08f, 0f, 0.12f);
        SetPresentationActive(true);
        BeginPhase(EventPhase.DistantReveal);
        ScheduleNextTrigger();
        return true;
    }

    public void SetInterval(float minimum, float maximum)
    {
        intervalMin = Mathf.Max(1f, Mathf.Min(minimum, maximum));
        intervalMax = Mathf.Max(intervalMin, Mathf.Max(minimum, maximum));
        ScheduleNextTrigger();
    }

    public void SetIntensity(ObserverIntensity value)
    {
        intensity = value;
        if (IsVisible)
            SetVisualLevels(currentBodyLevel, currentSensorLevel, currentLightLevel);
    }

    public void SetPerspective(PerspectivePreset value)
    {
        perspective = value;
        if (backgroundCamera != null) backgroundCamera.fieldOfView = PerspectiveFov();
        RefreshTuningDistance();
    }

    public void SetFarDistance(FarDistancePreset value)
    {
        farPreset = value;
        if (phase == EventPhase.DistantReveal) SetRobotDistance(FarDistance());
    }

    public void SetNearDistance(NearDistancePreset value)
    {
        nearPreset = value;
        if (showConstantly || phase == EventPhase.CloseObserve)
            SetRobotDistance(NearDistance());
    }

    private void Update()
    {
        if (!observerEnabled || gameplayCamera == null || !prefabLoaded)
            return;

        SyncCameraViewport();
        if (backgroundCameraTest)
        {
            if (Time.unscaledTime >= backgroundCameraTestEndsAt)
            {
                backgroundCameraTest = false;
                HideImmediately();
            }
            return;
        }
        if (forceVisible)
        {
            SetRobotDistance(ForceVisibleDistance, true);
            SetVisualLevels(1.6f, 1.6f, 1f);
            ApplyForceVisibleEnvironment();
            return;
        }
        if (showConstantly)
        {
            SetRobotDistance(NearDistance());
            ApplyCloseObservationMotion();
            SetVisualLevels(1f, 1f, 1f);
            ApplyRevealEnvironment(1f);
            return;
        }

        if (phase == EventPhase.Hidden)
        {
            if (autoTrigger && Time.time >= nextTriggerAt) TriggerNow();
            return;
        }

        float elapsed = Time.time - phaseStartedAt;
        switch (phase)
        {
            case EventPhase.DistantReveal:
                UpdateDistantReveal(elapsed);
                break;
            case EventPhase.Approach:
                UpdateApproach(elapsed);
                break;
            case EventPhase.CloseObserve:
                UpdateCloseObserve(elapsed);
                break;
            case EventPhase.FadeOut:
                UpdateFadeOut(elapsed);
                break;
        }
    }

    private void UpdateDistantReveal(float elapsed)
    {
        float progress = Smooth01(elapsed / DistantRevealDuration);
        SetRobotDistance(FarDistance());
        SetVisualLevels(Mathf.Lerp(0.04f, 0.14f, progress), progress,
            Mathf.Lerp(0.08f, 0.28f, progress));
        ApplyRevealEnvironment(progress * 0.16f);
        if (elapsed >= DistantRevealDuration) BeginPhase(EventPhase.Approach);
    }

    private void UpdateApproach(float elapsed)
    {
        float progress = Smooth01(elapsed / ApproachDuration);
        SetRobotDistance(Mathf.Lerp(FarDistance(), NearDistance(), progress));
        SetVisualLevels(Mathf.Lerp(0.14f, 1f, progress), 1f,
            Mathf.Lerp(0.28f, 1f, progress));
        ApplyRevealEnvironment(Mathf.Lerp(0.16f, 1f, progress));
        if (elapsed >= ApproachDuration) BeginPhase(EventPhase.CloseObserve);
    }

    private void UpdateCloseObserve(float elapsed)
    {
        SetRobotDistance(NearDistance());
        ApplyCloseObservationMotion();
        SetVisualLevels(1f, 1f, 1f);
        ApplyRevealEnvironment(1f);
        if (elapsed >= CloseObserveDuration) BeginPhase(EventPhase.FadeOut);
    }

    private void UpdateFadeOut(float elapsed)
    {
        float remaining = 1f - Smooth01(elapsed / FadeOutDuration);
        SetVisualLevels(remaining, remaining, remaining);
        ApplyRevealEnvironment(remaining);
        if (elapsed >= FadeOutDuration) FinishEvent();
    }

    private void ApplyCloseObservationMotion()
    {
        float correction = Mathf.Sin(Time.time * 0.45f) * 1.2f;
        robotPivot.rotation = BackgroundRelativeRobotRotation() *
            Quaternion.Euler(0f, correction, 0f);
    }

    private void BeginPhase(EventPhase value)
    {
        phase = value;
        phaseStartedAt = Time.time;
    }

    private void FinishEvent()
    {
        forceVisible = false;
        backgroundCameraTest = false;
        SetVisualLevels(0f, 0f, 0f);
        SetPresentationActive(false);
        RestoreEnvironment();
        phase = EventPhase.Hidden;
    }

    private void HideImmediately()
    {
        forceVisible = false;
        backgroundCameraTest = false;
        SetVisualLevels(0f, 0f, 0f);
        SetPresentationActive(false);
        RestoreEnvironment();
        phase = EventPhase.Hidden;
    }

    private void ScheduleNextTrigger()
    {
        nextTriggerAt = Time.time + Random.Range(intervalMin, intervalMax);
    }

    private void CreateObserverWorld()
    {
        GameObject world = new("GiantObserverWorld");
        world.transform.SetParent(transform, false);
        observerWorld = world.transform;

        GameObject cameraObject = new("Giant Observer Background Camera");
        cameraObject.transform.SetParent(observerWorld, false);
        backgroundCamera = cameraObject.AddComponent<Camera>();
        backgroundCamera.orthographic = false;
        backgroundCamera.fieldOfView = PerspectiveFov();
        backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
        backgroundCamera.backgroundColor = ObserverBackgroundColor;
        backgroundCamera.cullingMask = 1 << ObserverLayer;
        backgroundCamera.nearClipPlane = 0.1f;
        backgroundCamera.farClipPlane = 2000f;
        backgroundCamera.allowHDR = true;
        backgroundCamera.depth = gameplayCamera.depth - 10f;
        backgroundCamera.transform.localPosition = Vector3.zero;
        backgroundCamera.transform.localRotation = Quaternion.Euler(-4f, 0f, 0f);
        backgroundCameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
        backgroundCameraData.SetRenderer(ObserverRendererIndex);
        backgroundCameraData.renderType = CameraRenderType.Base;
        backgroundCameraData.renderPostProcessing = false;

        gameplayCameraData = gameplayCamera.GetComponent<UniversalAdditionalCameraData>();
        if (gameplayCameraData == null)
            gameplayCameraData = gameplayCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        originalGameplayRenderType = gameplayCameraData.renderType;
        gameplayCameraData.SetRenderer(GameplayRendererIndex);
        gameplayCameraData.renderType = CameraRenderType.Base;

        GameObject pivotObject = new("Observer Robot Pivot");
        pivotObject.transform.SetParent(observerWorld, false);
        robotPivot = pivotObject.transform;
        robotPivot.localRotation = RuntimeRobotRotation;

        CreateRobotInstance();
        CreateObserverLights();
        SyncCameraViewport();
        compositorConfigured = true;
    }

    private void CreateRobotInstance()
    {
        if (observerPrefab == null) return;
        robotInstance = Instantiate(observerPrefab, robotPivot, false);
        robotInstance.name = "p_robot1 (Perspective Background Observer)";
        robotInstance.transform.localPosition = Vector3.zero;
        Vector3 assetScale = robotInstance.transform.localScale;
        robotInstance.transform.localScale = new Vector3(assetScale.x, assetScale.y,
            Mathf.Max(assetScale.x, assetScale.y));
        SetLayerRecursively(robotInstance, ObserverLayer);
        DisableRuntimeGameplay(robotInstance);
        CollectRenderers(robotInstance);
        if (robotVisuals.Count == 0) return;

        Bounds bounds = CalculateBounds();
        Vector3 localCenter = robotPivot.InverseTransformPoint(bounds.center);
        robotInstance.transform.localPosition -= localCenter;
        bounds = CalculateBounds();
        modelWidth = Mathf.Max(0.01f, bounds.size.x);
        modelHeight = Mathf.Max(0.01f, bounds.size.y);
        robotVerticalOffset = modelHeight * 0.12f;
        prefabLoaded = true;
    }

    private void CreateObserverLights()
    {
        GameObject lights = new("Observer Lights");
        lights.transform.SetParent(observerWorld, false);
        keyLight = CreateDirectionalLight(lights.transform, "Cold Key",
            new Color(0.18f, 0.48f, 0.7f), new Vector3(12f, -24f, 0f));
        rimLight = CreateDirectionalLight(lights.transform, "Cold Rim",
            new Color(0.35f, 0.78f, 1f), new Vector3(-28f, 150f, -12f));
    }

    private static Light CreateDirectionalLight(Transform parent, string lightName,
        Color color, Vector3 rotation)
    {
        GameObject lightObject = new(lightName);
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.localRotation = Quaternion.Euler(rotation);
        SetLayerRecursively(lightObject, ObserverLayer);
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.color = color;
        light.intensity = 0f;
        light.shadows = LightShadows.None;
        light.cullingMask = 1 << ObserverLayer;
        return light;
    }

    private void SetPresentationActive(bool active)
    {
        if (backgroundCamera == null || gameplayCamera == null ||
            backgroundCameraData == null || gameplayCameraData == null)
            return;

        if (active)
        {
            backgroundCameraData.SetRenderer(ObserverRendererIndex);
            backgroundCameraData.renderType = CameraRenderType.Base;
            gameplayCameraData.SetRenderer(ObserverRendererIndex);
            gameplayCameraData.renderType = CameraRenderType.Overlay;
            if (!backgroundCameraData.cameraStack.Contains(gameplayCamera))
                backgroundCameraData.cameraStack.Add(gameplayCamera);
            gameplayCamera.clearFlags = originalGameplayClearFlags;
            gameplayCamera.backgroundColor = originalGameplayBackground;
            backgroundCamera.enabled = true;
            if (!backgroundCameraTest)
                backgroundCamera.backgroundColor = ObserverBackgroundColor;
            return;
        }

        backgroundCamera.enabled = false;
        backgroundCameraData.cameraStack.Remove(gameplayCamera);
        gameplayCameraData.renderType = originalGameplayRenderType;
        gameplayCameraData.SetRenderer(GameplayRendererIndex);
        gameplayCamera.clearFlags = originalGameplayClearFlags;
        gameplayCamera.backgroundColor = originalGameplayBackground;
        backgroundCamera.backgroundColor = ObserverBackgroundColor;
    }

    private void SyncCameraViewport()
    {
        if (backgroundCamera == null || gameplayCamera == null) return;
        backgroundCamera.rect = gameplayCamera.rect;
        backgroundCamera.targetDisplay = gameplayCamera.targetDisplay;
        backgroundCamera.depth = gameplayCamera.depth - 10f;
    }

    private void SetRobotDistance(float distance, bool centerInView = false)
    {
        currentDistance = Mathf.Max(0.5f, distance);
        float verticalOffset = centerInView ? 0f : robotVerticalOffset;
        Transform cameraTransform = backgroundCamera.transform;
        robotPivot.position = cameraTransform.position +
            cameraTransform.forward * currentDistance +
            cameraTransform.up * verticalOffset;
        if (phase != EventPhase.CloseObserve && !showConstantly)
            robotPivot.rotation = BackgroundRelativeRobotRotation();
    }

    private Quaternion BackgroundRelativeRobotRotation() => backgroundCamera != null
        ? backgroundCamera.transform.rotation * RuntimeRobotRotation
        : RuntimeRobotRotation;

    private float FarDistance()
    {
        float targetCoverage = farPreset == FarDistancePreset.VeryFar ? 0.035f : 0.08f;
        return DistanceForCoverage(targetCoverage);
    }

    private float NearDistance()
    {
        float targetCoverage = nearPreset == NearDistancePreset.VeryClose ? 1.4f : 0.82f;
        return DistanceForCoverage(targetCoverage);
    }

    private float DistanceForCoverage(float coverage)
    {
        float aspect = gameplayCamera != null ? Mathf.Max(0.1f, gameplayCamera.aspect) :
            16f / 9f;
        float halfFov = PerspectiveFov() * 0.5f * Mathf.Deg2Rad;
        return modelWidth /
            Mathf.Max(0.001f, 2f * Mathf.Tan(halfFov) * aspect * coverage);
    }

    private float CalculateScreenCoverage(float distance)
    {
        if (distance <= 0f || gameplayCamera == null) return 0f;
        float halfFov = PerspectiveFov() * 0.5f * Mathf.Deg2Rad;
        float visibleWidth = 2f * distance * Mathf.Tan(halfFov) *
            Mathf.Max(0.1f, gameplayCamera.aspect);
        return modelWidth / Mathf.Max(0.01f, visibleWidth);
    }

    private float PerspectiveFov() => perspective switch
    {
        PerspectivePreset.Narrow => 30f,
        PerspectivePreset.Wide => 50f,
        _ => 40f
    };

    private void RefreshTuningDistance()
    {
        if (showConstantly || phase == EventPhase.CloseObserve)
            SetRobotDistance(NearDistance());
        else if (phase == EventPhase.DistantReveal)
            SetRobotDistance(FarDistance());
    }

    private void SetVisualLevels(float body, float sensor, float light)
    {
        currentBodyLevel = Mathf.Clamp01(body);
        currentSensorLevel = Mathf.Clamp01(sensor);
        currentLightLevel = Mathf.Clamp01(light);
        bool rendererActive = body > 0.003f || sensor > 0.003f;
        foreach (RendererVisual visual in robotVisuals)
        {
            visual.Renderer.enabled = rendererActive;
            ApplyMaterialLevels(visual, body, sensor);
        }

        float keyAtFull = intensity switch
        {
            ObserverIntensity.Low => 0.1f,
            ObserverIntensity.High => 0.34f,
            _ => 0.21f
        };
        float rimAtFull = intensity switch
        {
            ObserverIntensity.Low => 0.34f,
            ObserverIntensity.High => 0.95f,
            _ => 0.68f
        };
        if (keyLight != null) keyLight.intensity = keyAtFull * currentLightLevel;
        if (rimLight != null) rimLight.intensity = rimAtFull * currentLightLevel;
    }

    private void ApplyMaterialLevels(RendererVisual visual, float body, float sensor)
    {
        float exposureAtFull = intensity switch
        {
            ObserverIntensity.Low => 0.52f,
            ObserverIntensity.High => 0.82f,
            _ => 0.68f
        };
        for (int i = 0; i < visual.Materials.Length; i++)
        {
            Material material = visual.Materials[i];
            if (material == null) continue;
            bool isSensor = visual.SensorMaterials[i];
            MaterialPropertyBlock block = new();
            float baseLevel = isSensor
                ? Mathf.Max(body * exposureAtFull, sensor * 0.72f)
                : body * exposureAtFull;
            if (material.HasProperty("_BaseColor"))
                block.SetColor("_BaseColor", ScaleColor(
                    material.GetColor("_BaseColor"), baseLevel));
            if (material.HasProperty("_Color"))
                block.SetColor("_Color", ScaleColor(
                    material.GetColor("_Color"), baseLevel));
            if (material.HasProperty("_EmissionColor"))
                block.SetColor("_EmissionColor", material.GetColor("_EmissionColor") *
                    (isSensor ? Mathf.Clamp01(sensor) : body * 0.35f));
            visual.Renderer.SetPropertyBlock(block, i);
        }
    }

    private void CaptureEnvironment()
    {
        if (environmentCaptured) return;
        gameplayBackgroundBeforeEvent = gameplayCamera.backgroundColor;
        environmentCaptured = true;
    }

    private void ApplyRevealEnvironment(float amount)
    {
        if (!environmentCaptured) return;
        worldRuleVisual?.SetDebugDarknessOverlayMultiplier(
            Mathf.Lerp(1f, 0.7f, Mathf.Clamp01(amount)));
    }

    private void ApplyForceVisibleEnvironment()
    {
        if (!environmentCaptured) return;
        worldRuleVisual?.SetDebugDarknessOverlayMultiplier(0f);
    }

    private void RestoreEnvironment()
    {
        if (gameplayCamera != null && environmentCaptured)
            gameplayCamera.backgroundColor = gameplayBackgroundBeforeEvent;
        environmentCaptured = false;
        worldRuleVisual?.SetDebugDarknessOverlayMultiplier(1f);
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
        foreach (MonoBehaviour behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
        {
            behaviour.enabled = false;
            RecordDisabled(behaviour);
        }
    }

    private void RecordDisabled(Component component)
    {
        string typeName = component.GetType().Name;
        if (!disabledRuntimeComponents.Contains(typeName))
            disabledRuntimeComponents.Add(typeName);
    }

    private void CollectRenderers(GameObject instance)
    {
        foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer || renderer is TrailRenderer ||
                renderer is LineRenderer) continue;
            Material[] materials = renderer.sharedMaterials;
            bool[] sensorMaterials = new bool[materials.Length];
            string rendererName = renderer.name.ToLowerInvariant();
            for (int i = 0; i < materials.Length; i++)
            {
                if (materials[i] != null) observerMaterials.Add(materials[i]);
                string materialName = materials[i] != null
                    ? materials[i].name.ToLowerInvariant() : string.Empty;
                sensorMaterials[i] = rendererName.Contains("eye") ||
                    rendererName.Contains("sensor") || materialName.Contains("eye") ||
                    materialName.Contains("sensor");
                usesPrefabSensor |= sensorMaterials[i];
            }
            robotVisuals.Add(new RendererVisual
            {
                Renderer = renderer,
                Materials = materials,
                SensorMaterials = sensorMaterials
            });
        }
    }

    private Bounds CalculateBounds()
    {
        Bounds bounds = robotVisuals[0].Renderer.bounds;
        for (int i = 1; i < robotVisuals.Count; i++)
            bounds.Encapsulate(robotVisuals[i].Renderer.bounds);
        return bounds;
    }

    private int CountEnabledRenderers()
    {
        int count = 0;
        foreach (RendererVisual visual in robotVisuals)
        {
            if (visual.Renderer != null && visual.Renderer.enabled &&
                visual.Renderer.gameObject.activeInHierarchy)
                count++;
        }
        return count;
    }

    private int CountUnsupportedShaders()
    {
        int count = 0;
        foreach (Material material in observerMaterials)
        {
            if (material.shader == null || !material.shader.isSupported) count++;
        }
        return count;
    }

    private string GetBackgroundRendererName()
    {
        if (backgroundCameraData == null || backgroundCameraData.scriptableRenderer == null)
            return string.Empty;
        return backgroundCameraData.scriptableRenderer.GetType().Name;
    }

    private string GetGameplayRendererName()
    {
        if (gameplayCameraData == null || gameplayCameraData.scriptableRenderer == null)
            return string.Empty;
        return gameplayCameraData.scriptableRenderer.GetType().Name;
    }

    private bool IsRobotInFront()
    {
        if (backgroundCamera == null || robotPivot == null) return false;
        Vector3 toRobot = robotPivot.position - backgroundCamera.transform.position;
        return Vector3.Dot(backgroundCamera.transform.forward, toRobot) >
            backgroundCamera.nearClipPlane;
    }

    private string GetLastRenderState()
    {
        if (!BackgroundCameraActive) return "CAMERA OFF";
        if (!ObserverMaskOk || !RobotLayersOk) return "LAYER MISMATCH";
        if (backgroundCameraTest) return "VISIBLE (MAGENTA TEST; ROBOT HIDDEN)";
        if (!RobotActive || EnabledRendererCount == 0) return "ROBOT HIDDEN";
        if (!RobotInFrontOfCamera) return "BEHIND CAMERA";
        if (robotVisuals.Count == 0) return "OUT OF FRUSTUM";
        Plane[] planes = GeometryUtility.CalculateFrustumPlanes(backgroundCamera);
        return GeometryUtility.TestPlanesAABB(planes, CalculateBounds())
            ? "VISIBLE" : "OUT OF FRUSTUM";
    }

    private static bool LayersMatch(Transform root)
    {
        if (root.gameObject.layer != ObserverLayer) return false;
        foreach (Transform child in root)
        {
            if (!LayersMatch(child)) return false;
        }
        return true;
    }

    private static Color ScaleColor(Color color, float multiplier)
    {
        return new Color(Mathf.Clamp01(color.r * multiplier),
            Mathf.Clamp01(color.g * multiplier),
            Mathf.Clamp01(color.b * multiplier), color.a);
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        foreach (Transform child in root.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private static float Smooth01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private void OnDestroy()
    {
        RestoreEnvironment();
        if (gameplayCamera != null)
        {
            backgroundCameraData?.cameraStack.Remove(gameplayCamera);
            if (gameplayCameraData != null)
            {
                gameplayCameraData.renderType = originalGameplayRenderType;
                gameplayCameraData.SetRenderer(GameplayRendererIndex);
            }
            gameplayCamera.cullingMask = originalGameplayCullingMask;
            gameplayCamera.depth = originalGameplayDepth;
            gameplayCamera.clearFlags = originalGameplayClearFlags;
            gameplayCamera.backgroundColor = originalGameplayBackground;
        }
    }
}
#endif
