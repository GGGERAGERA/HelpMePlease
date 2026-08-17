using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public sealed class ProductionVisualTuningController : MonoBehaviour
{
    private sealed class ProjectileVisualState
    {
        public GameObject Projectile;
        public Transform VisualRoot;
        public Vector3 VisualScale;
        public TrailRenderer[] Trails;
        public float[] TrailWidths;
        public float[] TrailTimes;
        public Gradient[] TrailGradients;
    }

    public static ProductionVisualTuningController Instance { get; private set; }

    public float ProjectileVisualScale { get; private set; } = 1f;
    public float TrailWidth { get; private set; } = 1f;
    public float TrailTime { get; private set; } = 1f;
    public float TrailAlpha { get; private set; } = 1f;

    public bool VignetteAvailable
    {
        get
        {
            ResolveVignette();
            return vignette != null;
        }
    }

    public float VignetteIntensity
    {
        get
        {
            ResolveVignette();
            return vignette != null ? vignette.intensity.value : 0f;
        }
    }

    private readonly List<ProjectileVisualState> projectiles = new();
    private bool existingProjectilesRegistered;
    private Vignette vignette;
    private bool vignetteCaptured;
    private bool originalVignetteActive;
    private bool originalVignetteOverride;
    private float originalVignetteIntensity;

    private void Awake()
    {
        Instance = this;
    }

    public void Configure()
    {
        ResolveVignette();
        RegisterExistingProjectiles();
    }

    public static void RegisterProjectile(GameObject projectile)
    {
        if (Instance == null || projectile == null ||
            projectile.GetComponent<Bullet>() == null)
        {
            return;
        }

        Instance.RegisterProjectileInstance(projectile);
    }

    public void SetProjectileVisualScale(float value)
    {
        ProjectileVisualScale = Mathf.Clamp(value, 0.1f, 4f);
        ApplyProjectiles();
    }

    public void SetTrailWidth(float value)
    {
        TrailWidth = Mathf.Clamp(value, 0.1f, 5f);
        ApplyProjectiles();
    }

    public void SetTrailTime(float value)
    {
        TrailTime = Mathf.Clamp(value, 0.1f, 6f);
        ApplyProjectiles();
    }

    public void SetTrailAlpha(float value)
    {
        TrailAlpha = Mathf.Clamp(value, 0f, 3f);
        ApplyProjectiles();
    }

    public void SetVignetteIntensity(float value)
    {
        ResolveVignette();
        if (vignette == null)
            return;

        vignette.active = true;
        vignette.intensity.overrideState = true;
        vignette.intensity.value = Mathf.Clamp01(value);
    }

    public void ResetProjectileSettings()
    {
        ProjectileVisualScale = 1f;
        TrailWidth = 1f;
        TrailTime = 1f;
        TrailAlpha = 1f;
        ApplyProjectiles();
    }

    public void ResetVignette()
    {
        if (!vignetteCaptured || vignette == null)
            return;

        vignette.active = originalVignetteActive;
        vignette.intensity.overrideState = originalVignetteOverride;
        vignette.intensity.value = originalVignetteIntensity;
    }

    private void RegisterExistingProjectiles()
    {
        if (existingProjectilesRegistered)
            return;

        existingProjectilesRegistered = true;
        Bullet[] activeBullets = FindObjectsByType<Bullet>(FindObjectsSortMode.None);
        for (int i = 0; i < activeBullets.Length; i++)
            RegisterProjectileInstance(activeBullets[i].gameObject);
    }

    private void RegisterProjectileInstance(GameObject projectile)
    {
        for (int i = 0; i < projectiles.Count; i++)
        {
            if (projectiles[i].Projectile == projectile)
                return;
        }

        Transform visualRoot = ResolveProjectileVisualRoot(projectile.transform);
        TrailRenderer[] trails =
            projectile.GetComponentsInChildren<TrailRenderer>(true);
        ProjectileVisualState state = new()
        {
            Projectile = projectile,
            VisualRoot = visualRoot,
            VisualScale = visualRoot != null ? visualRoot.localScale : Vector3.one,
            Trails = trails,
            TrailWidths = new float[trails.Length],
            TrailTimes = new float[trails.Length],
            TrailGradients = new Gradient[trails.Length]
        };

        for (int i = 0; i < trails.Length; i++)
        {
            state.TrailWidths[i] = trails[i].widthMultiplier;
            state.TrailTimes[i] = trails[i].time;
            state.TrailGradients[i] = CloneGradient(trails[i].colorGradient);
        }

        projectiles.Add(state);
        ApplyProjectile(state);
    }

    private static Transform ResolveProjectileVisualRoot(Transform projectile)
    {
        Transform authored = projectile.Find("fx_projectile1");
        if (authored != null)
            return authored;

        for (int i = 0; i < projectile.childCount; i++)
        {
            Transform child = projectile.GetChild(i);
            if (child.GetComponentInChildren<Renderer>(true) != null ||
                child.GetComponentInChildren<ParticleSystem>(true) != null ||
                child.GetComponentInChildren<Light2D>(true) != null)
            {
                return child;
            }
        }

        return null;
    }

    private void ApplyProjectiles()
    {
        for (int i = projectiles.Count - 1; i >= 0; i--)
        {
            ProjectileVisualState state = projectiles[i];
            if (state.Projectile == null)
            {
                projectiles.RemoveAt(i);
                continue;
            }

            ApplyProjectile(state);
        }
    }

    private void ApplyProjectile(ProjectileVisualState state)
    {
        if (state.VisualRoot != null)
            state.VisualRoot.localScale = state.VisualScale * ProjectileVisualScale;

        for (int i = 0; i < state.Trails.Length; i++)
        {
            TrailRenderer trail = state.Trails[i];
            if (trail == null)
                continue;

            trail.widthMultiplier = state.TrailWidths[i] * TrailWidth;
            trail.time = state.TrailTimes[i] * TrailTime;
            trail.colorGradient = WithAlphaMultiplier(
                state.TrailGradients[i],
                TrailAlpha
            );
        }
    }

    private void ResolveVignette()
    {
        if (vignette != null)
            return;

        VolumeProfile profile = VolumeManager.instance != null
            ? VolumeManager.instance.globalDefaultProfile
            : null;
        if (profile == null || !profile.TryGet(out vignette))
            return;

        originalVignetteActive = vignette.active;
        originalVignetteOverride = vignette.intensity.overrideState;
        originalVignetteIntensity = vignette.intensity.value;
        vignetteCaptured = true;
    }

    private static Gradient CloneGradient(Gradient source)
    {
        Gradient clone = new();
        clone.mode = source.mode;
        clone.SetKeys(source.colorKeys, source.alphaKeys);
        return clone;
    }

    private static Gradient WithAlphaMultiplier(Gradient source, float multiplier)
    {
        GradientAlphaKey[] alphaKeys = source.alphaKeys;
        for (int i = 0; i < alphaKeys.Length; i++)
        {
            alphaKeys[i].alpha = Mathf.Clamp01(
                alphaKeys[i].alpha * multiplier
            );
        }

        Gradient result = new();
        result.mode = source.mode;
        result.SetKeys(source.colorKeys, alphaKeys);
        return result;
    }

    private void OnDestroy()
    {
        ResetProjectileSettings();
        ResetVignette();
        if (Instance == this)
            Instance = null;
    }
}
