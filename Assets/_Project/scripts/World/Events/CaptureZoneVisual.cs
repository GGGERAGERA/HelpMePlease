using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(CaptureZoneEvent))]
public sealed class CaptureZoneVisual : MonoBehaviour
{
    [Header("Materials")]
    [SerializeField] private Material lineMaterial;
    [SerializeField] private Material particleMaterial;

    [Header("Field")]
    [SerializeField] private Color fillColor = new Color(0.04f, 0.34f, 0.45f, 0.08f);
    [SerializeField] private Color glowColor = new Color(0.08f, 0.55f, 0.8f, 0.18f);
    [SerializeField] private Color contourColor = new Color(0.2f, 0.85f, 1f, 0.65f);
    [SerializeField, Min(0.01f)] private float contourWidth = 0.045f;
    [SerializeField, Min(0.01f)] private float glowWidth = 0.15f;
    [SerializeField, Range(24, 160)] private int contourSegments = 96;

    [Header("Pulse")]
    [SerializeField, Min(0f)] private float pulseSpeed = 0.85f;
    [SerializeField, Range(0f, 0.5f)] private float pulseAmount = 0.18f;

    [Header("Particles")]
    [SerializeField, Min(0f)] private float particlesPerSecond = 5f;
    [SerializeField, Min(0.01f)] private float particleSize = 0.07f;
    [SerializeField, Min(0.1f)] private float particleLifetime = 2.2f;

    [Header("Rendering")]
    [SerializeField] private string sortingLayerName = "Midground";

    private CaptureZoneEvent captureZoneEvent;
    private Transform visualRoot;
    private LineRenderer fill;
    private LineRenderer glow;
    private LineRenderer contour;
    private ParticleSystem perimeterParticles;
    private Coroutine pulseRoutine;
    private float pulseTime;

    private void Awake()
    {
        captureZoneEvent = GetComponent<CaptureZoneEvent>();
        BuildVisual();
    }

    private void OnEnable()
    {
        if (fill == null)
            return;

        pulseRoutine = StartCoroutine(Pulse());
    }

    private void OnDisable()
    {
        if (pulseRoutine != null)
        {
            StopCoroutine(pulseRoutine);
            pulseRoutine = null;
        }

        ApplyPulse(1f);
    }

    private void BuildVisual()
    {
        if (captureZoneEvent == null || lineMaterial == null)
            return;

        float radius = captureZoneEvent.CaptureRadius;
        visualRoot = CreateWorldScaleRoot();

        fill = CreateLine("FieldFill", -2);
        fill.positionCount = 2;
        fill.numCapVertices = 32;
        fill.startWidth = radius * 2f;
        fill.endWidth = radius * 2f;
        fill.SetPosition(0, new Vector3(-0.001f, 0f, 0f));
        fill.SetPosition(1, new Vector3(0.001f, 0f, 0f));

        glow = CreateRing("OuterGlow", radius, glowWidth, -1);
        contour = CreateRing("OuterContour", radius, contourWidth, 0);

        if (particleMaterial != null && particlesPerSecond > 0f)
            perimeterParticles = CreatePerimeterParticles(radius);

        ApplyPulse(1f);
    }

    private Transform CreateWorldScaleRoot()
    {
        GameObject root = new GameObject("CaptureZoneVisual");
        Transform rootTransform = root.transform;
        rootTransform.SetParent(transform, false);

        Vector3 parentScale = transform.lossyScale;
        rootTransform.localScale = new Vector3(
            SafeInverse(parentScale.x),
            SafeInverse(parentScale.y),
            SafeInverse(parentScale.z)
        );

        return rootTransform;
    }

    private LineRenderer CreateRing(string objectName, float radius, float width, int sortingOrder)
    {
        LineRenderer line = CreateLine(objectName, sortingOrder);
        line.loop = true;
        line.positionCount = contourSegments;
        line.startWidth = width;
        line.endWidth = width;

        for (int i = 0; i < contourSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / contourSegments;
            line.SetPosition(i, new Vector3(
                Mathf.Cos(angle) * radius,
                Mathf.Sin(angle) * radius,
                0f
            ));
        }

        return line;
    }

    private LineRenderer CreateLine(string objectName, int sortingOrder)
    {
        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(visualRoot, false);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.sharedMaterial = lineMaterial;
        line.useWorldSpace = false;
        line.alignment = LineAlignment.View;
        line.textureMode = LineTextureMode.Stretch;
        line.numCornerVertices = 4;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.sortingLayerName = sortingLayerName;
        line.sortingOrder = sortingOrder;
        return line;
    }

    private ParticleSystem CreatePerimeterParticles(float radius)
    {
        GameObject particlesObject = new GameObject("PerimeterParticles");
        particlesObject.transform.SetParent(visualRoot, false);

        ParticleSystem particles = particlesObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.useUnscaledTime = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = particleLifetime;
        main.startSpeed = 0f;
        main.startSize = particleSize;
        main.startColor = new Color(contourColor.r, contourColor.g, contourColor.b, 0.28f);
        main.maxParticles = 18;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.rateOverTime = particlesPerSecond;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.radiusThickness = 0f;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient alphaGradient = new Gradient();
        alphaGradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.25f),
                new GradientAlphaKey(0f, 1f)
            }
        );
        colorOverLifetime.color = alphaGradient;

        ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.sharedMaterial = particleMaterial;
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.sortingLayerName = sortingLayerName;
        particleRenderer.sortingOrder = 1;
        particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;

        particles.Play();
        return particles;
    }

    private IEnumerator Pulse()
    {
        while (true)
        {
            pulseTime += Time.unscaledDeltaTime * pulseSpeed;
            float pulse = 1f + Mathf.Sin(pulseTime * Mathf.PI * 2f) * pulseAmount;
            ApplyPulse(pulse);
            yield return null;
        }
    }

    private void ApplyPulse(float multiplier)
    {
        SetColor(fill, WithMultipliedAlpha(fillColor, multiplier));
        SetColor(glow, WithMultipliedAlpha(glowColor, multiplier));
        SetColor(contour, WithMultipliedAlpha(contourColor, multiplier));
    }

    private static void SetColor(LineRenderer line, Color color)
    {
        if (line == null)
            return;

        line.startColor = color;
        line.endColor = color;
    }

    private static Color WithMultipliedAlpha(Color color, float multiplier)
    {
        color.a = Mathf.Clamp01(color.a * multiplier);
        return color;
    }

    private static float SafeInverse(float value)
    {
        return Mathf.Abs(value) > 0.0001f ? 1f / value : 1f;
    }
}
