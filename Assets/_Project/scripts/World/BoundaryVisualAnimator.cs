using UnityEngine;

public sealed class BoundaryVisualAnimator : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private PlayerBoundaryHazard playerHazard;
    [SerializeField] private SpriteRenderer[] hazardStrips;
    [SerializeField] private SpriteRenderer[] outsideTints;

    [Header("Calm State")]
    [SerializeField] private Color calmHazardTint =
        new(1f, 0.78f, 0.72f, 0.82f);
    [SerializeField] private Color calmOutsideTint =
        new(0.26f, 0.025f, 0.035f, 0.14f);
    [SerializeField, Min(0.01f)] private float calmPulseSpeed = 0.35f;
    [SerializeField, Range(0f, 0.15f)] private float calmPulseAlpha = 0.025f;

    [Header("Alert State")]
    [SerializeField] private Color alertHazardTint =
        new(1f, 0.48f, 0.42f, 1f);
    [SerializeField] private Color alertOutsideTint =
        new(0.52f, 0.025f, 0.035f, 0.28f);
    [SerializeField, Min(0.01f)] private float alertPulseSpeed = 1.6f;
    [SerializeField, Range(0f, 0.35f)] private float alertPulseAlpha = 0.14f;

    [Header("Transition")]
    [SerializeField, Min(0.05f)] private float transitionSpeed = 1.8f;

    private float alertBlend;
    private float pulseTime;
    private bool isOutside;

    private void OnEnable()
    {
        if (playerHazard != null)
        {
            playerHazard.OutsideStateChanged += HandleOutsideStateChanged;
            isOutside = playerHazard.IsOutside;
        }

        alertBlend = isOutside ? 1f : 0f;
        ApplyColors(0f);
    }

    private void OnDisable()
    {
        if (playerHazard != null)
            playerHazard.OutsideStateChanged -= HandleOutsideStateChanged;
    }

    private void Update()
    {
        alertBlend = Mathf.MoveTowards(
            alertBlend,
            isOutside ? 1f : 0f,
            transitionSpeed * Time.deltaTime
        );

        float pulseSpeed = Mathf.Lerp(
            calmPulseSpeed,
            alertPulseSpeed,
            alertBlend
        );
        pulseTime += Time.deltaTime * pulseSpeed * Mathf.PI * 2f;

        float pulseAmount = Mathf.Lerp(
            calmPulseAlpha,
            alertPulseAlpha,
            alertBlend
        );
        float pulse = Mathf.Sin(pulseTime) * pulseAmount;

        ApplyColors(pulse);
    }

    private void HandleOutsideStateChanged(bool outside)
    {
        isOutside = outside;
    }

    private void ApplyColors(float alphaOffset)
    {
        Color hazardColor = Color.Lerp(
            calmHazardTint,
            alertHazardTint,
            alertBlend
        );
        hazardColor.a = Mathf.Clamp01(hazardColor.a + alphaOffset);

        Color outsideColor = Color.Lerp(
            calmOutsideTint,
            alertOutsideTint,
            alertBlend
        );
        outsideColor.a = Mathf.Clamp01(
            outsideColor.a + alphaOffset * 0.2f
        );

        ApplyColor(hazardStrips, hazardColor);
        ApplyColor(outsideTints, outsideColor);
    }

    private static void ApplyColor(
        SpriteRenderer[] renderers,
        Color color)
    {
        if (renderers == null)
            return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
                renderers[i].color = color;
        }
    }
}
