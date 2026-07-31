using UnityEngine;

public sealed class GoldenEnemyModifier : MonoBehaviour
{
    private static readonly Color GoldenTint =
        new(1f, 0.72f, 0.2f, 1f);

    private EnemyHealth health;
    private SpriteRenderer bodyRenderer;
    private Color originalColor;
    private float originalMaxHealth;
    private bool hasOriginalColor;
    private bool rolledThisSpawn;

    public bool IsGolden { get; private set; }
    public float HealthMultiplier { get; private set; } = 1f;
    public float RewardMultiplier { get; private set; } = 1f;

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        EnemyWhiteFlash whiteFlash = GetComponent<EnemyWhiteFlash>();
        bodyRenderer = whiteFlash != null
            ? whiteFlash.TargetRenderer
            : GetComponentInChildren<SpriteRenderer>();
    }

    private void OnEnable()
    {
        ResetSpawnState(false);
    }

    public bool TryBeginSpawnRoll()
    {
        if (rolledThisSpawn)
            return false;

        rolledThisSpawn = true;
        return true;
    }

    public void Apply(float healthMultiplier, float rewardMultiplier)
    {
        if (IsGolden || health == null)
            return;

        IsGolden = true;
        HealthMultiplier = Mathf.Max(1f, healthMultiplier);
        RewardMultiplier = Mathf.Max(1f, rewardMultiplier);

        originalMaxHealth = health.maxHealth;
        health.SetRuntimeMaxHealth(originalMaxHealth * HealthMultiplier);
        ApplyTint();
    }

    private void ApplyTint()
    {
        if (bodyRenderer == null)
            return;

        originalColor = bodyRenderer.color;
        hasOriginalColor = true;
        bodyRenderer.color = new Color(
            originalColor.r * GoldenTint.r,
            originalColor.g * GoldenTint.g,
            originalColor.b * GoldenTint.b,
            originalColor.a
        );
    }

    private void ResetSpawnState(bool restoreHealth)
    {
        if (restoreHealth && IsGolden && health != null)
            health.SetRuntimeMaxHealth(originalMaxHealth);

        if (hasOriginalColor && bodyRenderer != null)
            bodyRenderer.color = originalColor;

        hasOriginalColor = false;
        rolledThisSpawn = false;
        IsGolden = false;
        HealthMultiplier = 1f;
        RewardMultiplier = 1f;
        originalMaxHealth = 0f;
    }

    private void OnDisable()
    {
        ResetSpawnState(true);
    }
}
