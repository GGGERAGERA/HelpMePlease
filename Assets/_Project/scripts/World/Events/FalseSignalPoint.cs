using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class FalseSignalPoint : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] private Material lineMaterial;
    [SerializeField] private Color color = new(1f, 0.35f, 0.1f, 0.9f);
    [SerializeField, Min(0.1f)] private float visualRadius = 0.75f;
    [SerializeField, Min(8)] private int segments = 32;

    private FalseSignalEvent owner;
    private bool isReal;
    private bool consumed;
    private LineRenderer coreRing;
    private Collider2D signalCollider;
    private bool fading;
    private float fadeDuration;
    private float fadeRemaining;
    private float visibility = 1f;
    private bool trapWarningActive;
    private float trapWarningElapsed;
    private Color trapWarningColor;

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
        if (trapWarningActive)
            trapWarningElapsed += Time.deltaTime;

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

        ApplyVisual();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed || owner == null || !other.CompareTag("Player"))
            return;

        consumed = true;

        if (signalCollider != null)
            signalCollider.enabled = false;

        FalseSignalEvent eventOwner = owner;
        owner = null;
        eventOwner.ResolveSignal(this, isReal);
    }

    public void BeginTrapWarning(Color warningColor)
    {
        trapWarningActive = true;
        trapWarningElapsed = 0f;
        trapWarningColor = warningColor;
        visibility = 1f;
        SetVisualEnabled(true);
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
        ApplyVisual();
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

    private void ApplyVisual()
    {
        Color coreColor = trapWarningActive
            ? trapWarningColor
            : color;

        if (trapWarningActive)
        {
            float pulse = Mathf.PingPong(
                trapWarningElapsed * 8f,
                1f
            );
            coreColor.a *= Mathf.Lerp(0.3f, 1f, pulse);
        }

        coreColor.a *= visibility;

        if (coreRing != null)
        {
            coreRing.startColor = coreColor;
            coreRing.endColor = coreColor;
        }
    }

    private void SetVisualEnabled(bool enabled)
    {
        if (coreRing != null)
            coreRing.enabled = enabled;
    }
}
