using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(LineRenderer))]
public sealed class PlayerWeaponOrbitVisual : MonoBehaviour
{
    private const int SegmentCount = 96;
    private const float DefaultIntensity = 1.25f;
    private const float DefaultWidth = 0.035f;
    private const float DefaultAlpha = 0.72f;
    private const float DefaultRotationSpeed = 4f;
    private const float DefaultPulseSpeed = 0.22f;
    private const float DefaultPulseAmount = 0.06f;

    private static readonly Color OrbitColor =
        new(0.04f, 0.88f, 1f, 1f);

    private LineRenderer ring;
    private Material ringMaterial;
    private BaseWeapon weapon;
    private float renderedRadius = -1f;
    private float phase;
    private float rotationAngle;

    public bool RingEnabled { get; private set; } = true;
    public float RingIntensity { get; private set; } = DefaultIntensity;
    public float RingWidth { get; private set; } = DefaultWidth;
    public float RingAlpha { get; private set; } = DefaultAlpha;
    public float RingRadiusMultiplier { get; private set; } = 1f;
    public float RingPulseAmount { get; private set; } = DefaultPulseAmount;
    public float RingPulseSpeed { get; private set; } = DefaultPulseSpeed;
    public float RingRotationSpeed { get; private set; } = DefaultRotationSpeed;
    public Vector2 RingOffset { get; private set; }
    public Color RingTint { get; private set; } = OrbitColor;
    public bool HasOrbitSource => weapon != null;
    public float CurrentOrbitRadius => weapon != null
        ? weapon.CurrentOrbitRadius
        : 0f;
    public float CurrentVisualRadius => CurrentOrbitRadius * RingRadiusMultiplier;

    public static PlayerWeaponOrbitVisual Ensure(
        GameObject player,
        BaseWeapon orbitWeapon)
    {
        if (player == null)
            return null;

        PlayerWeaponOrbitVisual visual =
            player.GetComponent<PlayerWeaponOrbitVisual>();
        visual ??= player.AddComponent<PlayerWeaponOrbitVisual>();
        visual.Bind(orbitWeapon);
        return visual;
    }

    public void Bind(BaseWeapon orbitWeapon)
    {
        weapon = orbitWeapon;
        renderedRadius = -1f;
        RefreshGeometry();
        RefreshPresentation();
    }

    public void SetRingEnabled(bool value)
    {
        RingEnabled = value;
        RefreshPresentation();
    }

    public void SetRingIntensity(float value)
    {
        RingIntensity = Mathf.Clamp(value, 0f, 4f);
        RefreshPresentation();
    }

    public void SetRingWidth(float value)
    {
        RingWidth = Mathf.Clamp(value, 0.005f, 0.15f);
        RefreshPresentation();
    }

    public void SetRingAlpha(float value)
    {
        RingAlpha = Mathf.Clamp01(value);
        RefreshPresentation();
    }

    public void SetRingRadiusMultiplier(float value)
    { RingRadiusMultiplier = Mathf.Clamp(value, .25f, 3f); RefreshGeometry(true); }
    public void SetRingPulseAmount(float value)
    { RingPulseAmount = Mathf.Clamp(value, 0f, .75f); RefreshColor(); }
    public void SetRingPulseSpeed(float value)
    { RingPulseSpeed = Mathf.Clamp(value, 0f, 5f); }
    public void SetRingRotationSpeed(float value)
    { RingRotationSpeed = Mathf.Clamp(value, -180f, 180f); }
    public void SetRingOffsetX(float value)
    { RingOffset = new Vector2(Mathf.Clamp(value, -3f, 3f), RingOffset.y); RefreshGeometry(true); }
    public void SetRingOffsetY(float value)
    { RingOffset = new Vector2(RingOffset.x, Mathf.Clamp(value, -3f, 3f)); RefreshGeometry(true); }
    public void SetRingTint(Color value)
    { RingTint = value; RefreshColor(); }

    public void ResetPresentationSettings()
    {
        RingEnabled = true;
        RingIntensity = DefaultIntensity;
        RingWidth = DefaultWidth;
        RingAlpha = DefaultAlpha;
        RingRadiusMultiplier = 1f;
        RingPulseAmount = DefaultPulseAmount;
        RingPulseSpeed = DefaultPulseSpeed;
        RingRotationSpeed = DefaultRotationSpeed;
        RingOffset = Vector2.zero;
        RingTint = OrbitColor;
        RefreshPresentation();
    }

    private void Awake()
    {
        ring = GetComponent<LineRenderer>();
        ConfigureRenderer();
        phase = Random.value * Mathf.PI * 2f;
    }

    private void LateUpdate()
    {
        if (weapon == null)
        {
            if (ring != null)
                ring.enabled = false;
            return;
        }

        rotationAngle = Mathf.Repeat(
            rotationAngle + RingRotationSpeed * Time.unscaledDeltaTime,
            360f
        );
        RefreshGeometry(true);
        RefreshColor();
    }

    private void ConfigureRenderer()
    {
        if (ring == null)
            return;

        ring.useWorldSpace = false;
        ring.loop = true;
        ring.positionCount = SegmentCount;
        ring.numCornerVertices = 2;
        ring.numCapVertices = 2;
        ring.alignment = LineAlignment.TransformZ;
        ring.textureMode = LineTextureMode.Stretch;
        ring.sortingLayerName = "Player";
        ring.sortingOrder = -10;
        ring.colorGradient = CreateSegmentedGradient();

        ringMaterial = AnomalyPowerVisuals.CreateMaterial(
            "Player Weapon Orbit Ring Material"
        );
        if (ringMaterial != null)
            ring.sharedMaterial = ringMaterial;

        RefreshPresentation();
    }

    private void RefreshGeometry(bool force = false)
    {
        if (ring == null || weapon == null)
            return;

        float radius = weapon.CurrentOrbitRadius * RingRadiusMultiplier;
        if (!force && Mathf.Approximately(renderedRadius, radius))
            return;

        renderedRadius = radius;
        float rotationRadians = rotationAngle * Mathf.Deg2Rad;
        for (int i = 0; i < SegmentCount; i++)
        {
            float radians = Mathf.PI * 2f * i / SegmentCount +
                rotationRadians;
            ring.SetPosition(i, new Vector3(
                Mathf.Cos(radians) * radius + RingOffset.x,
                Mathf.Sin(radians) * radius + RingOffset.y,
                0f
            ));
        }
    }

    private void RefreshPresentation()
    {
        if (ring == null)
            return;

        ring.enabled = RingEnabled && weapon != null;
        ring.startWidth = RingWidth;
        ring.endWidth = RingWidth;
        RefreshColor();
    }

    private void RefreshColor()
    {
        if (ring == null)
            return;

        float pulse = 1f - RingPulseAmount + RingPulseAmount * Mathf.Sin(
            phase + Time.unscaledTime * Mathf.PI * 2f * RingPulseSpeed
        );
        pulse = Mathf.Max(0f, pulse);
        Color color = RingTint * (RingIntensity * pulse);
        color.a = RingAlpha * pulse;
        if (ringMaterial != null)
            ringMaterial.color = color;
    }

    private static Gradient CreateSegmentedGradient()
    {
        Gradient gradient = new();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.03f),
                new GradientAlphaKey(1f, 0.47f),
                new GradientAlphaKey(0f, 0.5f),
                new GradientAlphaKey(1f, 0.53f),
                new GradientAlphaKey(1f, 0.97f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        return gradient;
    }

    private void OnDestroy()
    {
        if (ringMaterial != null)
            Destroy(ringMaterial);
    }
}
