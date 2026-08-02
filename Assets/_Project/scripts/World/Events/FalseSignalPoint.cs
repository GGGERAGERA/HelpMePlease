using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class FalseSignalPoint : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Material lineMaterial;
    [SerializeField] private Color color = new(1f, 0.35f, 0.1f, 0.9f);
    [SerializeField, Min(0.1f)] private float visualRadius = 0.75f;
    [SerializeField, Min(8)] private int segments = 32;
    [SerializeField, Min(1f)] private float glowRadiusMultiplier = 1.18f;
    [SerializeField, Range(0f, 1f)] private float glowAlpha = 0.28f;
    [SerializeField, Min(0f)] private float pulseSpeed = 2.2f;
    [SerializeField, Range(0f, 1f)] private float pulseStrength = 0.18f;

    private FalseSignalEvent owner;
    private bool isReal;
    private bool consumed;
    private LineRenderer coreRing;
    private LineRenderer glowRing;
    private Collider2D signalCollider;
    private bool fading;
    private float fadeDuration;
    private float fadeRemaining;
    private float visibility = 1f;

    public void Initialize(FalseSignalEvent eventOwner, bool realSignal)
    {
        owner = eventOwner;
        isReal = realSignal;
        consumed = false;
    }

    private void Awake()
    {
        signalCollider = GetComponent<Collider2D>();
        BuildVisual();
    }

    private void Update()
    {
        if (fading)
        {
            fadeRemaining = Mathf.Max(
                0f,
                fadeRemaining - Time.unscaledDeltaTime
            );
            visibility = fadeRemaining / fadeDuration;

            if (fadeRemaining <= 0f)
            {
                Destroy(gameObject);
                return;
            }
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(
            Time.unscaledTime * pulseSpeed * Mathf.PI * 2f
        );
        ApplyVisual(pulse);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed || owner == null || !other.CompareTag("Player"))
            return;

        consumed = true;
        SetVisualEnabled(false);

        if (signalCollider != null)
            signalCollider.enabled = false;

        FalseSignalEvent eventOwner = owner;
        owner = null;
        eventOwner.ResolveSignal(this, isReal);
        Destroy(gameObject);
    }

    public void FadeOutAndDestroy(float duration)
    {
        if (consumed || fading)
            return;

        consumed = true;
        fading = true;
        fadeDuration = Mathf.Max(0.05f, duration);
        fadeRemaining = fadeDuration;
        owner = null;
        transform.SetParent(null, true);

        if (signalCollider != null)
            signalCollider.enabled = false;
    }

    private void OnDestroy()
    {
        owner?.HandleSignalPointDestroyed(this);
        owner = null;
    }

    private void BuildVisual()
    {
        if (lineMaterial == null)
            return;

        coreRing = CreateRing(visualRadius, 0.15f, 3);
        glowRing = CreateRing(
            visualRadius * glowRadiusMultiplier,
            0.24f,
            2
        );
        ApplyVisual(0f);
    }

    private LineRenderer CreateRing(
        float radius,
        float width,
        int sortingOrder)
    {
        LineRenderer line = gameObject.AddComponent<LineRenderer>();
        line.sharedMaterial = lineMaterial;
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = Mathf.Max(8, segments);
        line.startWidth = width;
        line.endWidth = width;
        line.sortingLayerName = "Midground";
        line.sortingOrder = sortingOrder;

        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / line.positionCount;
            line.SetPosition(
                i,
                new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0f
                )
            );
        }

        return line;
    }

    private void ApplyVisual(float pulse)
    {
        float pulseAmount = 1f + pulse * pulseStrength;
        Color coreColor = color;
        coreColor.a *= visibility * Mathf.Lerp(0.82f, 1f, pulse);
        Color outerColor = color;
        outerColor.a = glowAlpha * visibility * Mathf.Lerp(0.65f, 1f, pulse);

        if (coreRing != null)
        {
            coreRing.startColor = coreColor;
            coreRing.endColor = coreColor;
            coreRing.widthMultiplier = pulseAmount;
        }

        if (glowRing != null)
        {
            glowRing.startColor = outerColor;
            glowRing.endColor = outerColor;
            glowRing.widthMultiplier = pulseAmount;
        }
    }

    private void SetVisualEnabled(bool enabled)
    {
        if (coreRing != null)
            coreRing.enabled = enabled;
        if (glowRing != null)
            glowRing.enabled = enabled;
    }
}
