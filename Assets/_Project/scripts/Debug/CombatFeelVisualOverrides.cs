using UnityEngine;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
[DisallowMultipleComponent]
public sealed class CombatFeelProjectileVisual : MonoBehaviour
{
    private CombatFeelLabSettings settings;
    private Transform visual;
    private Vector3 basePosition;
    private Vector3 baseScale;
    private Quaternion baseRotation;
    private SpriteRenderer[] renderers;
    private Color[] colors;
    private TrailRenderer[] trails;
    private float[] trailWidths;
    private float[] trailTimes;
    private Gradient[] trailColors;
    private float configuredAt;
    private float spin;
    private float pulse;
    private float pulseSpeed;
    private int appliedVersion;
    private Vector2 configuredDirection;

    public void Configure(CombatFeelLabSettings lab, Vector2 direction)
    {
        Capture();
        Restore();
        settings = lab;
        configuredDirection = direction;
        appliedVersion = settings != null ? settings.Version : 0;
        configuredAt = Time.unscaledTime;
        if (visual == null || settings == null) return;

        float illusion = settings.Get(CombatFeelParameter.SpeedIllusion);
        float scale = settings.Get(CombatFeelParameter.ProjectileScale);
        visual.localScale = Vector3.Scale(baseScale, new Vector3(
            scale * (settings.Get(CombatFeelParameter.ProjectileStretch) +
                     illusion * .5f),
            scale * settings.Get(CombatFeelParameter.ProjectileSquash), 1f));
        visual.localPosition = basePosition + Vector3.right *
            (settings.Get(CombatFeelParameter.ForwardVisualOffset) + illusion * .06f);
        spin = settings.Get(CombatFeelParameter.ProjectileSpin);
        pulse = settings.Get(CombatFeelParameter.ProjectilePulse);
        pulseSpeed = settings.Get(CombatFeelParameter.ProjectilePulseSpeed);

        float brightness = settings.Get(CombatFeelParameter.ProjectileBrightness);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].color = MultiplyRgb(colors[i], brightness);

        float width = settings.Get(CombatFeelParameter.TrailWidth);
        float lifetime = settings.Get(CombatFeelParameter.TrailLifetime) *
            settings.Get(CombatFeelParameter.TrailLength);
        float opacity = settings.Get(CombatFeelParameter.TrailOpacity);
        for (int i = 0; i < trails.Length; i++)
        {
            trails[i].widthMultiplier = trailWidths[i] * width;
            trails[i].time = trailTimes[i] * lifetime;
            trails[i].colorGradient = CloneGradient(trailColors[i], opacity);
            trails[i].Clear();
        }
    }

    private void Update()
    {
        if (visual == null || settings == null) return;
        if (appliedVersion != settings.Version)
        {
            Configure(settings, configuredDirection);
            return;
        }
        float age = Time.unscaledTime - configuredAt;
        float scalePulse = 1f + Mathf.Sin(age * pulseSpeed) * pulse;
        float streakLifetime = settings.Get(CombatFeelParameter.InitialStreakLifetime);
        float streak = streakLifetime > 0f
            ? settings.Get(CombatFeelParameter.InitialStreakLength) *
              (1f - Mathf.Clamp01(age / streakLifetime)) : 0f;
        float scale = settings.Get(CombatFeelParameter.ProjectileScale);
        visual.localScale = Vector3.Scale(baseScale, new Vector3(
            scale * (settings.Get(CombatFeelParameter.ProjectileStretch) +
                settings.Get(CombatFeelParameter.SpeedIllusion) * .5f + streak) * scalePulse,
            scale * settings.Get(CombatFeelParameter.ProjectileSquash) /
                Mathf.Max(.1f, scalePulse), 1f));
        if (spin != 0f)
            visual.localRotation *= Quaternion.Euler(
                0f, 0f, spin * Time.unscaledDeltaTime);
    }

    private void Capture()
    {
        if (visual != null) return;
        SpriteRenderer primary = GetComponentInChildren<SpriteRenderer>(true);
        visual = primary != null ? primary.transform : transform;
        basePosition = visual.localPosition;
        baseScale = visual.localScale;
        baseRotation = visual.localRotation;
        renderers = GetComponentsInChildren<SpriteRenderer>(true);
        colors = new Color[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) colors[i] = renderers[i].color;
        trails = GetComponentsInChildren<TrailRenderer>(true);
        trailWidths = new float[trails.Length];
        trailTimes = new float[trails.Length];
        trailColors = new Gradient[trails.Length];
        for (int i = 0; i < trails.Length; i++)
        {
            trailWidths[i] = trails[i].widthMultiplier;
            trailTimes[i] = trails[i].time;
            trailColors[i] = trails[i].colorGradient;
        }
    }

    private void Restore()
    {
        if (visual == null) return;
        visual.localPosition = basePosition;
        visual.localScale = baseScale;
        visual.localRotation = baseRotation;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].color = colors[i];
        for (int i = 0; i < trails.Length; i++) if (trails[i] != null)
        {
            trails[i].widthMultiplier = trailWidths[i];
            trails[i].time = trailTimes[i];
            trails[i].colorGradient = trailColors[i];
            trails[i].Clear();
        }
    }

    private void OnDisable() => Restore();

    private static Color MultiplyRgb(Color color, float value) => new(
        Mathf.Clamp01(color.r * value), Mathf.Clamp01(color.g * value),
        Mathf.Clamp01(color.b * value), color.a);

    private static Gradient CloneGradient(Gradient source, float alpha)
    {
        Gradient result = new();
        GradientAlphaKey[] keys = source.alphaKeys;
        for (int i = 0; i < keys.Length; i++) keys[i].alpha *= alpha;
        result.SetKeys(source.colorKeys, keys);
        return result;
    }
}

[DisallowMultipleComponent]
public sealed class CombatFeelParticleOverride : MonoBehaviour
{
    private ParticleSystem[] systems;
    private Vector3 baseScale;
    private Quaternion baseRotation;
    private float[] sizes;
    private float[] speeds;
    private float[] lifetimes;
    private float[] emissionRates;
    private CombatFeelLabSettings settings;
    private bool muzzle;
    private Vector2 direction;
    private int appliedVersion;

    public void Configure(CombatFeelLabSettings settings, bool muzzle, Vector2 direction)
    {
        if (systems == null)
            Capture();
        else
            baseRotation = transform.localRotation;
        Restore();
        if (settings == null) return;
        this.settings = settings;
        this.muzzle = muzzle;
        this.direction = direction;
        appliedVersion = settings.Version;
        float scale = settings.Get(muzzle ? CombatFeelParameter.MuzzleScale :
            CombatFeelParameter.ImpactScale);
        float stretch = muzzle
            ? settings.Get(CombatFeelParameter.MuzzleStretch) : 1f;
        transform.localScale = Vector3.Scale(baseScale,
            new Vector3(scale * stretch, scale, 1f));
        float randomRotation = settings.Get(muzzle
            ? CombatFeelParameter.MuzzleRandomRotation
            : CombatFeelParameter.ImpactRotationRandomness);
        transform.localRotation = baseRotation * Quaternion.Euler(
            0f, 0f, Random.Range(-randomRotation, randomRotation));
        float brightness = settings.Get(muzzle
            ? CombatFeelParameter.MuzzleBrightness
            : CombatFeelParameter.ImpactBrightness);
        float amount = settings.Get(muzzle
            ? CombatFeelParameter.MuzzleSparks
            : CombatFeelParameter.ImpactSparks);
        float lifetime = settings.Get(muzzle
            ? CombatFeelParameter.MuzzleDuration
            : CombatFeelParameter.ImpactLifetime);
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem.MainModule main = systems[i].main;
            main.startSizeMultiplier = sizes[i] * scale;
            main.startSpeedMultiplier = speeds[i] * Mathf.Max(.1f, brightness);
            main.startLifetimeMultiplier = lifetimes[i] * lifetime;
            ParticleSystem.EmissionModule emission = systems[i].emission;
            emission.rateOverTimeMultiplier = emissionRates[i] * amount;
        }
    }

    private void Update()
    {
        if (settings != null && appliedVersion != settings.Version)
            Configure(settings, muzzle, direction);
    }

    private void Capture()
    {
        if (systems != null) return;
        baseScale = transform.localScale;
        baseRotation = transform.localRotation;
        systems = GetComponentsInChildren<ParticleSystem>(true);
        sizes = new float[systems.Length];
        speeds = new float[systems.Length];
        lifetimes = new float[systems.Length];
        emissionRates = new float[systems.Length];
        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem.MainModule main = systems[i].main;
            sizes[i] = main.startSizeMultiplier;
            speeds[i] = main.startSpeedMultiplier;
            lifetimes[i] = main.startLifetimeMultiplier;
            emissionRates[i] = systems[i].emission.rateOverTimeMultiplier;
        }
    }

    private void Restore()
    {
        if (systems == null) return;
        transform.localScale = baseScale;
        transform.localRotation = baseRotation;
        for (int i = 0; i < systems.Length; i++) if (systems[i] != null)
        {
            ParticleSystem.MainModule main = systems[i].main;
            main.startSizeMultiplier = sizes[i];
            main.startSpeedMultiplier = speeds[i];
            main.startLifetimeMultiplier = lifetimes[i];
            ParticleSystem.EmissionModule emission = systems[i].emission;
            emission.rateOverTimeMultiplier = emissionRates[i];
        }
    }

    private void OnDisable() => Restore();
}
#endif
