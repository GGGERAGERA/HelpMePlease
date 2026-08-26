using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

[DisallowMultipleComponent]
public sealed class ProductionVisualTuningController : MonoBehaviour
{
    private sealed class SpriteVisualState
    {
        public SpriteRenderer Renderer;
        public Vector3 LocalPosition;
        public Vector3 LocalScale;
        public Color Color;
    }

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
    public float TrailBrightness { get; private set; } = 1f;
    public float PlayerVisualScale { get; private set; } = 1f;
    public Vector2 PlayerVisualOffset { get; private set; }
    public float PlayerBrightness { get; private set; } = 1f;
    public float PlayerSaturation { get; private set; } = 1f;
    public float PlayerOpacity { get; private set; } = 1f;
    public Color PlayerTint { get; private set; } = Color.white;
    public float PlayerTintStrength { get; private set; }
    public float WeaponVisualScale { get; private set; } = 1f;
    public Vector2 WeaponVisualOffset { get; private set; }
    public float WeaponBrightness { get; private set; } = 1f;
    public float WeaponSaturation { get; private set; } = 1f;
    public float WeaponOpacity { get; private set; } = 1f;
    public Color WeaponTint { get; private set; } = Color.white;
    public float WeaponTintStrength { get; private set; }
    public float LaserCoreWidth { get; private set; } = 1f;
    public float LaserGlowWidth { get; private set; } = 1f;
    public float LaserBrightness { get; private set; } = 1f;
    public int PlayerRendererCount => playerSprites.Count;
    public int WeaponRendererCount => weaponSprites.Count;
    public float ProductionProjectileVisualScale => hasProductionValues
        ? productionValues.ProjectileScale : 1f;
    public float ProductionTrailWidth => hasProductionValues
        ? productionValues.TrailWidth : 1f;
    public float ProductionTrailTime => hasProductionValues
        ? productionValues.TrailLifetime : 1f;
    public float ProductionTrailAlpha => hasProductionValues
        ? productionValues.TrailOpacity : 1f;
    public float ProductionTrailBrightness => hasProductionValues
        ? productionValues.TrailBrightness : 1f;
    public float ProductionLaserCoreWidth => hasProductionValues
        ? productionValues.LaserCoreWidth : 1f;
    public float ProductionLaserGlowWidth => hasProductionValues
        ? productionValues.LaserGlowWidth : 1f;
    public float ProductionLaserBrightness => hasProductionValues
        ? productionValues.LaserBrightness : 1f;

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
    public float ProductionVignetteIntensity
    {
        get
        {
            if (hasProductionValues)
                return productionValues.VignetteIntensity;
            ResolveVignette();
            return vignetteCaptured ? originalVignetteIntensity : 0f;
        }
    }

    private readonly List<ProjectileVisualState> projectiles = new();
    private readonly List<SpriteVisualState> playerSprites = new();
    private readonly List<SpriteVisualState> weaponSprites = new();
    private bool existingProjectilesRegistered;
    private Vignette vignette;
    private bool vignetteCaptured;
    private bool originalVignetteActive;
    private bool originalVignetteOverride;
    private float originalVignetteIntensity;
    private bool hasProductionValues;
    private VisualTuningSnapshot productionValues;

    private void Awake()
    {
        Instance = this;
    }

    public void Configure()
    {
        ResolveVignette();
        RegisterExistingProjectiles();
        RefreshCharacterVisuals();
    }

    public void ApplyProductionSnapshot(VisualTuningSnapshot values)
    {
        productionValues = values;
        hasProductionValues = true;
        SetProjectileVisualScale(values.ProjectileScale);
        SetTrailWidth(values.TrailWidth);
        SetTrailTime(values.TrailLifetime);
        SetTrailAlpha(values.TrailOpacity);
        SetTrailBrightness(values.TrailBrightness);
        SetPlayerVisualScale(values.PlayerScale);
        SetPlayerVisualOffsetX(values.PlayerOffsetX);
        SetPlayerVisualOffsetY(values.PlayerOffsetY);
        SetPlayerBrightness(values.PlayerBrightness);
        SetPlayerSaturation(values.PlayerSaturation);
        SetPlayerOpacity(values.PlayerOpacity);
        SetPlayerTint(values.PlayerTint);
        SetPlayerTintStrength(values.PlayerTintStrength);
        SetWeaponVisualScale(values.WeaponScale);
        SetWeaponVisualOffsetX(values.WeaponOffsetX);
        SetWeaponVisualOffsetY(values.WeaponOffsetY);
        SetWeaponBrightness(values.WeaponBrightness);
        SetWeaponSaturation(values.WeaponSaturation);
        SetWeaponOpacity(values.WeaponOpacity);
        SetWeaponTint(values.WeaponTint);
        SetWeaponTintStrength(values.WeaponTintStrength);
        SetLaserCoreWidth(values.LaserCoreWidth);
        SetLaserGlowWidth(values.LaserGlowWidth);
        SetLaserBrightness(values.LaserBrightness);
        SetVignetteIntensity(values.VignetteIntensity);
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

    public void SetTrailBrightness(float value)
    {
        TrailBrightness = Mathf.Clamp(value, 0f, 6f);
        ApplyProjectiles();
    }

    public void SetPlayerVisualScale(float value)
    { PlayerVisualScale = Mathf.Clamp(value, .5f, 2f); ApplyPlayerVisuals(); }
    public void SetPlayerVisualOffsetX(float value)
    { PlayerVisualOffset = new Vector2(Mathf.Clamp(value, -2f, 2f), PlayerVisualOffset.y); ApplyPlayerVisuals(); }
    public void SetPlayerVisualOffsetY(float value)
    { PlayerVisualOffset = new Vector2(PlayerVisualOffset.x, Mathf.Clamp(value, -2f, 2f)); ApplyPlayerVisuals(); }
    public void SetPlayerBrightness(float value)
    { PlayerBrightness = Mathf.Clamp(value, 0f, 4f); ApplyPlayerVisuals(); }
    public void SetPlayerSaturation(float value)
    { PlayerSaturation = Mathf.Clamp(value, 0f, 3f); ApplyPlayerVisuals(); }
    public void SetPlayerOpacity(float value)
    { PlayerOpacity = Mathf.Clamp01(value); ApplyPlayerVisuals(); }
    public void SetPlayerTint(Color value)
    { PlayerTint = value; ApplyPlayerVisuals(); }
    public void SetPlayerTintStrength(float value)
    { PlayerTintStrength = Mathf.Clamp01(value); ApplyPlayerVisuals(); }

    public void SetWeaponVisualScale(float value)
    { WeaponVisualScale = Mathf.Clamp(value, .5f, 2.5f); ApplyWeaponVisuals(); }
    public void SetWeaponVisualOffsetX(float value)
    { WeaponVisualOffset = new Vector2(Mathf.Clamp(value, -2f, 2f), WeaponVisualOffset.y); ApplyWeaponVisuals(); }
    public void SetWeaponVisualOffsetY(float value)
    { WeaponVisualOffset = new Vector2(WeaponVisualOffset.x, Mathf.Clamp(value, -2f, 2f)); ApplyWeaponVisuals(); }
    public void SetWeaponBrightness(float value)
    { WeaponBrightness = Mathf.Clamp(value, 0f, 4f); ApplyWeaponVisuals(); }
    public void SetWeaponSaturation(float value)
    { WeaponSaturation = Mathf.Clamp(value, 0f, 3f); ApplyWeaponVisuals(); }
    public void SetWeaponOpacity(float value)
    { WeaponOpacity = Mathf.Clamp01(value); ApplyWeaponVisuals(); }
    public void SetWeaponTint(Color value)
    { WeaponTint = value; ApplyWeaponVisuals(); }
    public void SetWeaponTintStrength(float value)
    { WeaponTintStrength = Mathf.Clamp01(value); ApplyWeaponVisuals(); }

    public void SetLaserCoreWidth(float value)
    { LaserCoreWidth = Mathf.Clamp(value, .1f, 8f); ApplyLaserSettings(); }
    public void SetLaserGlowWidth(float value)
    { LaserGlowWidth = Mathf.Clamp(value, .1f, 8f); ApplyLaserSettings(); }
    public void SetLaserBrightness(float value)
    { LaserBrightness = Mathf.Clamp(value, 0f, 6f); ApplyLaserSettings(); }

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
        ProjectileVisualScale = hasProductionValues
            ? productionValues.ProjectileScale : 1f;
        TrailWidth = hasProductionValues ? productionValues.TrailWidth : 1f;
        TrailTime = hasProductionValues ? productionValues.TrailLifetime : 1f;
        TrailAlpha = hasProductionValues ? productionValues.TrailOpacity : 1f;
        TrailBrightness = hasProductionValues
            ? productionValues.TrailBrightness : 1f;
        ApplyProjectiles();
    }

    public void ResetPlayerSettings()
    {
        PlayerVisualScale = hasProductionValues ? productionValues.PlayerScale : 1f;
        PlayerVisualOffset = hasProductionValues
            ? new Vector2(productionValues.PlayerOffsetX,
                productionValues.PlayerOffsetY) : Vector2.zero;
        PlayerBrightness = hasProductionValues ? productionValues.PlayerBrightness : 1f;
        PlayerSaturation = hasProductionValues ? productionValues.PlayerSaturation : 1f;
        PlayerOpacity = hasProductionValues ? productionValues.PlayerOpacity : 1f;
        PlayerTint = hasProductionValues ? productionValues.PlayerTint : Color.white;
        PlayerTintStrength = hasProductionValues
            ? productionValues.PlayerTintStrength : 0f;
        ApplyPlayerVisuals();
    }

    public void ResetWeaponSettings()
    {
        WeaponVisualScale = hasProductionValues ? productionValues.WeaponScale : 1f;
        WeaponVisualOffset = hasProductionValues
            ? new Vector2(productionValues.WeaponOffsetX,
                productionValues.WeaponOffsetY) : Vector2.zero;
        WeaponBrightness = hasProductionValues ? productionValues.WeaponBrightness : 1f;
        WeaponSaturation = hasProductionValues ? productionValues.WeaponSaturation : 1f;
        WeaponOpacity = hasProductionValues ? productionValues.WeaponOpacity : 1f;
        WeaponTint = hasProductionValues ? productionValues.WeaponTint : Color.white;
        WeaponTintStrength = hasProductionValues
            ? productionValues.WeaponTintStrength : 0f;
        ApplyWeaponVisuals();
    }

    public void ResetLaserSettings()
    {
        LaserCoreWidth = hasProductionValues ? productionValues.LaserCoreWidth : 1f;
        LaserGlowWidth = hasProductionValues ? productionValues.LaserGlowWidth : 1f;
        LaserBrightness = hasProductionValues ? productionValues.LaserBrightness : 1f;
        ApplyLaserSettings();
    }

    public void ResetVignette()
    {
        if (hasProductionValues)
        {
            SetVignetteIntensity(productionValues.VignetteIntensity);
            return;
        }
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
                TrailAlpha,
                TrailBrightness
            );
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        CombatFeelProjectileVisual feel = state.Projectile != null
            ? state.Projectile.GetComponent<CombatFeelProjectileVisual>() : null;
        feel?.RebaseAfterProductionVisualChange();
#endif
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

    private void RefreshCharacterVisuals()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null) return;
        SpriteRenderer[] renderers =
            player.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || ContainsRenderer(playerSprites, renderer) ||
                ContainsRenderer(weaponSprites, renderer)) continue;
            bool weapon = renderer.GetComponentInParent<BaseWeapon>() != null;
            string objectName = renderer.gameObject.name.ToLowerInvariant();
            if (!weapon && (objectName.Contains("shadow") ||
                objectName.Contains("ring") || objectName.Contains("halo")))
                continue;
            (weapon ? weaponSprites : playerSprites).Add(new SpriteVisualState
            {
                Renderer = renderer,
                LocalPosition = renderer.transform.localPosition,
                LocalScale = renderer.transform.localScale,
                Color = renderer.color
            });
        }
        ApplyPlayerVisuals();
        ApplyWeaponVisuals();
        ApplyLaserSettings();
    }

    private static bool ContainsRenderer(
        List<SpriteVisualState> states, SpriteRenderer renderer)
    {
        for (int i = 0; i < states.Count; i++)
            if (states[i].Renderer == renderer) return true;
        return false;
    }

    private void ApplyPlayerVisuals() => ApplySprites(
        playerSprites, PlayerVisualScale, PlayerVisualOffset,
        PlayerBrightness, PlayerSaturation, PlayerOpacity,
        PlayerTint, PlayerTintStrength);

    private void ApplyWeaponVisuals() => ApplySprites(
        weaponSprites, WeaponVisualScale, WeaponVisualOffset,
        WeaponBrightness, WeaponSaturation, WeaponOpacity,
        WeaponTint, WeaponTintStrength);

    private static void ApplySprites(
        List<SpriteVisualState> states, float scale, Vector2 offset,
        float brightness, float saturation, float opacity,
        Color tint, float tintStrength)
    {
        for (int i = states.Count - 1; i >= 0; i--)
        {
            SpriteVisualState state = states[i];
            if (state.Renderer == null) { states.RemoveAt(i); continue; }
            state.Renderer.transform.localScale = state.LocalScale * scale;
            state.Renderer.transform.localPosition = state.LocalPosition +
                new Vector3(offset.x, offset.y, 0f);
            Color color = AdjustColor(
                state.Color, brightness, saturation, opacity, tint, tintStrength);
            state.Renderer.color = color;
        }
    }

    private static Color AdjustColor(
        Color source, float brightness, float saturation, float opacity,
        Color tint, float tintStrength)
    {
        Color.RGBToHSV(source, out float hue, out float sat, out float value);
        Color color = Color.HSVToRGB(hue, Mathf.Clamp01(sat * saturation),
            Mathf.Clamp01(value));
        color.r *= brightness;
        color.g *= brightness;
        color.b *= brightness;
        color = Color.Lerp(color, tint * brightness, tintStrength);
        color.a = source.a * opacity;
        return color;
    }

    private void ApplyLaserSettings() => LaserBeamRenderer.SetDebugVisualMultipliers(
        LaserCoreWidth, LaserGlowWidth, LaserBrightness);

    private static Gradient WithAlphaMultiplier(
        Gradient source, float alphaMultiplier, float brightnessMultiplier)
    {
        GradientColorKey[] colorKeys = source.colorKeys;
        for (int i = 0; i < colorKeys.Length; i++)
            colorKeys[i].color *= brightnessMultiplier;
        GradientAlphaKey[] alphaKeys = source.alphaKeys;
        for (int i = 0; i < alphaKeys.Length; i++)
        {
            alphaKeys[i].alpha = Mathf.Clamp01(
                alphaKeys[i].alpha * alphaMultiplier
            );
        }

        Gradient result = new();
        result.mode = source.mode;
        result.SetKeys(colorKeys, alphaKeys);
        return result;
    }

    private void OnDestroy()
    {
        ResetProjectileSettings();
        ResetPlayerSettings();
        ResetWeaponSettings();
        ResetLaserSettings();
        ResetVignette();
        if (Instance == this)
            Instance = null;
    }
}
