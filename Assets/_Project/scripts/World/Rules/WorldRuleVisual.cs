using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class WorldRuleVisual : MonoBehaviour
{
    private static readonly int RuleTypeId =
        Shader.PropertyToID("_RuleType");
    private static readonly int IntensityId =
        Shader.PropertyToID("_Intensity");
    private static readonly int VisualTimeId =
        Shader.PropertyToID("_VisualTime");
    private static readonly int PulseSpeedId =
        Shader.PropertyToID("_PulseSpeed");
    private static readonly int PulseStrengthId =
        Shader.PropertyToID("_PulseStrength");
    private static readonly int EdgeIntensityId =
        Shader.PropertyToID("_EdgeIntensity");

    [Header("References")]
    [SerializeField] private Image fullscreenImage;
    [SerializeField] private Material visualMaterial;

    [Header("Transition")]
    [SerializeField, Min(0.01f)] private float transitionDuration = 1f;
    [SerializeField, Range(0f, 1f)] private float globalIntensity = 1f;

    [Header("Animation")]
    [SerializeField, Min(0f)] private float pulseSpeed = 1f;
    [SerializeField, Range(0f, 1f)] private float pulseStrength = 0.3f;
    [SerializeField, Range(0f, 2f)] private float edgeIntensity = 1f;

    [Header("Rule Strength")]
    [SerializeField, Range(0f, 1f)] private float hasteIntensity = 0.42f;
    [SerializeField, Range(0f, 1f)] private float regenerationIntensity = 0.38f;
    [SerializeField, Range(0f, 1f)] private float explosiveIntensity = 0.48f;

    private float currentIntensity;
    private float targetIntensity;

    private void Awake()
    {
        if (fullscreenImage != null)
        {
            fullscreenImage.raycastTarget = false;
            fullscreenImage.material = visualMaterial;
        }

        ApplyTuning();
        SetNeutral();
    }

    private void Update()
    {
        if (visualMaterial == null)
            return;

        float step = Time.unscaledDeltaTime /
            Mathf.Max(0.01f, transitionDuration);
        currentIntensity = Mathf.MoveTowards(
            currentIntensity,
            targetIntensity,
            step
        );

        visualMaterial.SetFloat(IntensityId, currentIntensity);
        visualMaterial.SetFloat(VisualTimeId, Time.unscaledTime);

        if (fullscreenImage != null &&
            targetIntensity <= 0f &&
            currentIntensity <= 0f)
        {
            fullscreenImage.enabled = false;
        }
    }

    public void ShowRule(WorldRuleType ruleType)
    {
        if (visualMaterial == null)
            return;

        if (fullscreenImage != null)
            fullscreenImage.enabled = true;

        visualMaterial.SetFloat(RuleTypeId, (float)ruleType);
        targetIntensity = GetIntensity(ruleType) * globalIntensity;
    }

    public void ClearRule()
    {
        targetIntensity = 0f;
    }

    public void ClearImmediate()
    {
        SetNeutral();
    }

    private float GetIntensity(WorldRuleType ruleType)
    {
        switch (ruleType)
        {
            case WorldRuleType.Haste:
                return hasteIntensity;

            case WorldRuleType.Regeneration:
                return regenerationIntensity;

            case WorldRuleType.ExplosiveInfection:
                return explosiveIntensity;

            default:
                return 0f;
        }
    }

    private void SetNeutral()
    {
        currentIntensity = 0f;
        targetIntensity = 0f;

        if (visualMaterial != null)
            visualMaterial.SetFloat(IntensityId, 0f);

        if (fullscreenImage != null)
            fullscreenImage.enabled = false;
    }

    private void ApplyTuning()
    {
        if (visualMaterial == null)
            return;

        visualMaterial.SetFloat(PulseSpeedId, pulseSpeed);
        visualMaterial.SetFloat(PulseStrengthId, pulseStrength);
        visualMaterial.SetFloat(EdgeIntensityId, edgeIntensity);
    }

    private void OnDisable()
    {
        SetNeutral();
    }

#if UNITY_EDITOR
    [ContextMenu("Preview/Haste")]
    private void PreviewHaste()
    {
        Preview(WorldRuleType.Haste);
    }

    [ContextMenu("Preview/Regeneration")]
    private void PreviewRegeneration()
    {
        Preview(WorldRuleType.Regeneration);
    }

    [ContextMenu("Preview/Explosive Infection")]
    private void PreviewExplosiveInfection()
    {
        Preview(WorldRuleType.ExplosiveInfection);
    }

    [ContextMenu("Preview/Clear")]
    private void PreviewClear()
    {
        ClearImmediate();
    }

    private void Preview(WorldRuleType ruleType)
    {
        if (visualMaterial == null)
            return;

        if (fullscreenImage != null)
            fullscreenImage.enabled = true;

        visualMaterial.SetFloat(RuleTypeId, (float)ruleType);
        ApplyTuning();
        currentIntensity = GetIntensity(ruleType) * globalIntensity;
        targetIntensity = currentIntensity;
        visualMaterial.SetFloat(IntensityId, currentIntensity);
        visualMaterial.SetFloat(
            VisualTimeId,
            (float)UnityEditor.EditorApplication.timeSinceStartup
        );
    }
#endif
}
