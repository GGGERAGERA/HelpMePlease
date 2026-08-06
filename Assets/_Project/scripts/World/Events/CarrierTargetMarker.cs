using UnityEngine;

public sealed class CarrierTargetMarker : MonoBehaviour
{
    private const int FeedbackRingSegments = 32;
    private const float FeedbackDuration = 0.6f;

    private static readonly Color Cyan =
        new(0.1f, 1f, 1f, 0.95f);
    private static readonly Color Yellow =
        new(1f, 0.9f, 0.1f, 0.95f);

    private LineRenderer diamond;
    private LineRenderer feedbackBeam;
    private LineRenderer feedbackRing;
    private Vector3 baseScale;
    private float markerTime;
    private float feedbackTime;
    private bool suppressed;

    public bool IsSuppressed => suppressed;

    public void Initialize(Material material)
    {
        baseScale = transform.localScale;
        diamond = CreateLine(material, 4, true, 0.1f, 52);
        diamond.SetPosition(0, new Vector3(0f, 0.34f, 0f));
        diamond.SetPosition(1, new Vector3(0.27f, 0f, 0f));
        diamond.SetPosition(2, new Vector3(0f, -0.34f, 0f));
        diamond.SetPosition(3, new Vector3(-0.27f, 0f, 0f));
        diamond.startColor = Cyan;
        diamond.endColor = Yellow;

        feedbackBeam = CreateLine(material, 2, false, 0.16f, 50);
        feedbackBeam.SetPosition(0, new Vector3(0f, -1.25f, 0f));
        feedbackBeam.SetPosition(1, new Vector3(0f, 2.15f, 0f));

        feedbackRing = CreateLine(
            material,
            FeedbackRingSegments,
            true,
            0.12f,
            51
        );

        markerTime = 0f;
        feedbackTime = FeedbackDuration;
        UpdateSpawnFeedback();
    }

    public void SetSuppressed(bool value)
    {
        suppressed = value;
        gameObject.SetActive(!value);
    }

    private void Update()
    {
        if (suppressed || Time.timeScale == 0f)
            return;

        markerTime += Time.deltaTime;
        float pulse = 1f + Mathf.Sin(markerTime * 5f) * 0.08f;
        transform.localScale = baseScale * pulse;

        if (feedbackTime <= 0f)
            return;

        feedbackTime = Mathf.Max(0f, feedbackTime - Time.deltaTime);
        UpdateSpawnFeedback();
    }

    private void UpdateSpawnFeedback()
    {
        float progress = 1f - feedbackTime / FeedbackDuration;
        float alpha = Mathf.SmoothStep(0f, 1f, feedbackTime /
            FeedbackDuration);
        Color cyan = Cyan;
        Color yellow = Yellow;
        cyan.a = alpha * 0.9f;
        yellow.a = alpha * 0.9f;

        feedbackBeam.startColor = cyan;
        feedbackBeam.endColor = yellow;
        feedbackRing.startColor = cyan;
        feedbackRing.endColor = yellow;

        float radius = Mathf.Lerp(0.24f, 0.95f, progress);

        for (int i = 0; i < FeedbackRingSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / FeedbackRingSegments;
            feedbackRing.SetPosition(
                i,
                new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle) * radius,
                    0f
                )
            );
        }

        bool feedbackVisible = feedbackTime > 0f;
        feedbackBeam.enabled = feedbackVisible;
        feedbackRing.enabled = feedbackVisible;
    }

    private LineRenderer CreateLine(
        Material material,
        int positionCount,
        bool loop,
        float width,
        int sortingOrder)
    {
        LineRenderer line = gameObject.AddComponent<LineRenderer>();
        line.sharedMaterial = material;
        line.useWorldSpace = false;
        line.loop = loop;
        line.positionCount = positionCount;
        line.startWidth = width;
        line.endWidth = width;
        line.sortingLayerName = "Foreground";
        line.sortingOrder = sortingOrder;
        return line;
    }
}
