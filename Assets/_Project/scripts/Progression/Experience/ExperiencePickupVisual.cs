using UnityEngine;

/// <summary>Presentation only. The pickup root remains the movement and collision origin.</summary>
[DisallowMultipleComponent]
public sealed class ExperiencePickupVisual : MonoBehaviour
{
    [SerializeField] private SpriteRenderer core;
    [SerializeField] private SpriteRenderer spark;
    [SerializeField] private ExperienceVisualPreset[] presets;
    [SerializeField] private ExperiencePickupEffect pickupEffect;
    [SerializeField, Range(0.03f, 0.06f)] private float bobAmplitude = 0.04f;
    [SerializeField, Min(1)] private float bobPeriod = 2.8f;
    private Vector3 restPosition;
    private float phase;
    private float pixelsPerUnit;
    private ExperienceVisualPreset current;
    private MaterialPropertyBlock properties;
    private static readonly int PhaseId = Shader.PropertyToID("_Phase");

    private void Awake()
    {
        restPosition = core.transform.localPosition;
        properties = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        int total = 0;
        foreach (var preset in presets) total += preset.productionWeight;
        int pick = Random.Range(0, total);
        current = presets[0];
        foreach (var preset in presets)
        {
            pick -= preset.productionWeight;
            if (pick < 0) { current = preset; break; }
        }
        core.sprite = current.sprite;
        pixelsPerUnit = core.sprite.pixelsPerUnit;
        core.color = Color.white;
        core.transform.localScale = Vector3.one;
        core.transform.localRotation = Quaternion.identity;
        phase = Random.value * 10f;
        properties.SetFloat(PhaseId, phase);
        core.SetPropertyBlock(properties);
        spark.color = current.sparkColor;
        spark.enabled = false;
        core.transform.localPosition = restPosition;
    }

    private void LateUpdate()
    {
        float t = Time.time + phase;
        Vector3 local = restPosition + Vector3.up *
            (Mathf.Sin(t * (2f * Mathf.PI / bobPeriod)) * bobAmplitude);
        // Snap the visual only. Telekinesis and the magnet keep continuous root motion.
        Vector3 world = transform.TransformPoint(local);
        world.x = Mathf.Round(world.x * pixelsPerUnit) / pixelsPerUnit;
        world.y = Mathf.Round(world.y * pixelsPerUnit) / pixelsPerUnit;
        core.transform.position = world;
        float cycle = Mathf.Repeat(t, 3.7f);
        spark.enabled = cycle < 0.18f;
        if (spark.enabled)
        {
            int step = Mathf.FloorToInt(cycle / 0.06f);
            int edge = Mathf.CeilToInt(core.sprite.rect.width * 0.5f) + 1;
            spark.transform.position = world + new Vector3(edge, 1 + step, 0) / pixelsPerUnit;
        }
    }

    public void PlayPickupFeedback(ExperienceManager owner)
    {
        if (owner != null)
            owner.PlayPickupEffect(pickupEffect, core.transform.position, current.sparkColor);
    }
}
