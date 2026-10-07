using UnityEngine;

/// <summary>Authored segment presentation. Damage and cadence belong to the site hazard.</summary>
public sealed class AnomalyBeamView : MonoBehaviour
{
    public enum BeamState { Inactive, Telegraph, Active, Ending }

    [SerializeField] private Transform segment;
    [SerializeField] private SpriteRenderer startEmitter;
    [SerializeField] private SpriteRenderer endEmitter;
    [SerializeField] private SpriteRenderer footprint;
    [SerializeField] private SpriteRenderer[] footprintCaps;
    [SerializeField] private SpriteRenderer[] edges;
    [SerializeField] private SpriteRenderer outerGlow;
    [SerializeField] private SpriteRenderer core;
    [SerializeField] private SpriteRenderer[] pulses;
    [SerializeField] private SpriteRenderer[] nodes;
    [SerializeField] private float endingDuration = 0.12f;
    [SerializeField] private float pulseSpeed = 9f;
    [SerializeField, Range(0f, 1f)] private float footprintAlpha = 0.065f;
    [SerializeField, Range(0f, 1f)] private float activeGlowAlpha = 0.3f;
    [SerializeField, Range(0f, 1f)] private float telegraphCoreAlpha = 0.18f;

    private BeamState state;
    private float stateStarted;
    private float length;
    private float halfWidth;
    private Vector2 start;
    private Vector2 end;
    private AnomalyVisualTuningValues original;
    private AnomalyVisualTuningValues values;

    public BeamState State => state;
    public float DamageHalfWidth => halfWidth;
    internal AnomalyVisualTuningValues VisualValues => values;
    internal const AnomalyVisualTuningCapabilities Capabilities =
        AnomalyVisualTuningCapabilities.PrimaryColor |
        AnomalyVisualTuningCapabilities.SecondaryColor |
        AnomalyVisualTuningCapabilities.BoundaryAlpha |
        AnomalyVisualTuningCapabilities.InnerLineWidth |
        AnomalyVisualTuningCapabilities.EdgeGlow |
        AnomalyVisualTuningCapabilities.PatternSpeed;

    private void Awake()
    {
        original = new AnomalyVisualTuningValues
        {
            PrimaryColor = core.color, SecondaryColor = outerGlow.color,
            InnerLineWidth = core.size.y, EdgeGlow = outerGlow.size.y,
            BoundaryAlpha = 1f, VisualScale = 1f, PatternSpeed = pulseSpeed
        };
        values = original;
        SetState(BeamState.Inactive);
    }

    public void SetNodes(Vector2[] positions)
    {
        for (int i = 0; i < nodes.Length; i++)
            nodes[i].transform.position = positions[i];
    }

    public void SetSegment(Vector2 from, Vector2 to, float damageHalfWidth)
    {
        start = from;
        end = to;
        halfWidth = damageHalfWidth;
        FitSegment();
    }

    private void FitSegment()
    {
        Vector2 delta = end - start;
        length = delta.magnitude;
        segment.position = (start + end) * 0.5f;
        segment.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        startEmitter.transform.localPosition = new Vector3(-length * 0.5f, 0f);
        endEmitter.transform.localPosition = new Vector3(length * 0.5f, 0f);
        footprint.size = new Vector2(length, halfWidth * 2f);
        for (int i = 0; i < footprintCaps.Length; i++)
        {
            footprintCaps[i].transform.localPosition = new Vector3((i == 0 ? -1f : 1f) * length * 0.5f, 0f);
            footprintCaps[i].size = Vector2.one * halfWidth * 2f;
        }
        for (int i = 0; i < edges.Length; i++)
        {
            edges[i].transform.localPosition = new Vector3(0f, (i == 0 ? -1f : 1f) * halfWidth);
            edges[i].size = new Vector2(length, edges[i].size.y);
        }
        core.size = new Vector2(length, values.InnerLineWidth);
        outerGlow.size = new Vector2(length, values.EdgeGlow);
    }

    public void SetState(BeamState next)
    {
        state = next;
        stateStarted = Time.time;
        Refresh();
    }

    private void Update()
    {
        if (state == BeamState.Ending && Time.time - stateStarted >= endingDuration)
            SetState(BeamState.Inactive);
        if (state != BeamState.Inactive) Refresh();
    }

    private void Refresh()
    {
        float age = Time.time - stateStarted;
        float fade = state == BeamState.Inactive ? 0f : state == BeamState.Ending
            ? 1f - Mathf.Clamp01(age / endingDuration) : 1f;
        bool charging = state == BeamState.Telegraph;
        float charge = charging ? Mathf.Lerp(0.35f, 0.8f, Mathf.Clamp01(age / 0.55f)) : 1f;
        Tint(startEmitter, values.SecondaryColor, fade * charge);
        Tint(endEmitter, values.SecondaryColor, fade * charge);
        Tint(core, values.PrimaryColor, fade * (charging ? telegraphCoreAlpha : 1f));
        Tint(outerGlow, values.SecondaryColor, fade * (charging ? activeGlowAlpha * 0.4f : activeGlowAlpha));
        Tint(footprint, values.SecondaryColor, fade * footprintAlpha * values.BoundaryAlpha);
        foreach (var cap in footprintCaps) Tint(cap, values.SecondaryColor, fade * footprintAlpha * values.BoundaryAlpha);
        foreach (var edge in edges) Tint(edge, values.SecondaryColor, fade * (charging ? 0.3f : 0.2f) * values.BoundaryAlpha);
        for (int i = 0; i < pulses.Length; i++)
        {
            float position = Mathf.Repeat(age * values.PatternSpeed + i * length / pulses.Length, Mathf.Max(0.01f, length));
            pulses[i].transform.localPosition = new Vector3(position - length * 0.5f, 0f);
            Tint(pulses[i], values.PrimaryColor, fade * (charging ? 0.4f : 0.9f));
        }
    }

    private static void Tint(SpriteRenderer renderer, Color color, float alpha)
    {
        color.a *= alpha;
        renderer.color = color;
        renderer.enabled = alpha > 0f;
    }

    internal void ApplyVisualValues(AnomalyVisualTuningValues next)
    {
        values = next;
        values.VisualScale = 1f; // Presentation must continue to mark the real damage footprint.
        FitSegment();
        Refresh();
    }

    internal void ResetVisualValues() => ApplyVisualValues(original);
}
