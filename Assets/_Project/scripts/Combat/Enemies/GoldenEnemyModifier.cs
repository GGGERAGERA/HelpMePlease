using UnityEngine;

public sealed class GoldenEnemyModifier : MonoBehaviour
{
    private EnemyHealth health;
    private SpriteRenderer bodyRenderer;
    private Color originalColor;
    private Color goldenTint = new(1f, 0.62f, 0.08f, 1f);
    private ParticleSystem deathFxPrefab;
    private float originalMaxHealth;
    private float assignmentPulseDuration = 0.28f;
    private float assignmentPulseRemaining;
    private float deathFlashIntensity = 1.35f;
    private bool hasOriginalColor;
    private bool rolledThisSpawn;
    private bool deathSubscribed;

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

    private void Update()
    {
        if (!IsGolden || assignmentPulseRemaining <= 0f ||
            bodyRenderer == null)
        {
            return;
        }

        assignmentPulseRemaining = Mathf.Max(
            0f,
            assignmentPulseRemaining - Time.unscaledDeltaTime
        );
        float duration = Mathf.Max(0.01f, assignmentPulseDuration);
        float progress = 1f - assignmentPulseRemaining / duration;
        float pulse = Mathf.Sin(progress * Mathf.PI);
        Color baseTint = GetTintedColor();
        Color pulseTint = Color.Lerp(baseTint, Color.white, 0.45f);
        bodyRenderer.color = Color.Lerp(baseTint, pulseTint, pulse);
    }

    public void ConfigureVisuals(
        Color tint,
        float pulseDuration,
        float flashIntensity,
        ParticleSystem existingDeathFxPrefab)
    {
        goldenTint = tint;
        assignmentPulseDuration = Mathf.Max(0.01f, pulseDuration);
        deathFlashIntensity = Mathf.Max(0f, flashIntensity);
        deathFxPrefab = existingDeathFxPrefab;
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
        assignmentPulseRemaining = assignmentPulseDuration;
        SubscribeToDeath();
    }

    private void ApplyTint()
    {
        if (bodyRenderer == null)
            return;

        originalColor = bodyRenderer.color;
        hasOriginalColor = true;
        bodyRenderer.color = GetTintedColor();
    }

    private Color GetTintedColor()
    {
        return new Color(
            originalColor.r * goldenTint.r,
            originalColor.g * goldenTint.g,
            originalColor.b * goldenTint.b,
            originalColor.a
        );
    }

    private void SubscribeToDeath()
    {
        if (deathSubscribed || health == null)
            return;

        health.OnDied += HandleDeath;
        deathSubscribed = true;
    }

    private void HandleDeath(EnemyHealth enemy)
    {
        if (!IsGolden || enemy != health)
            return;

        SpawnDeathFx();
        ShowRewardFeedback();
    }

    private void SpawnDeathFx()
    {
        if (deathFxPrefab == null)
            return;

        ParticleSystem instance = Instantiate(
            deathFxPrefab,
            transform.position,
            Quaternion.identity
        );
        ParticleSystem[] systems =
            instance.GetComponentsInChildren<ParticleSystem>(true);
        Color flashColor = goldenTint * deathFlashIntensity;
        flashColor.a = 1f;
        float lifetime = 0.35f;

        for (int i = 0; i < systems.Length; i++)
        {
            ParticleSystem.MainModule main = systems[i].main;
            main.loop = false;
            main.useUnscaledTime = true;
            main.startColor = flashColor;
            lifetime = Mathf.Max(
                lifetime,
                main.duration + main.startLifetime.constantMax
            );
        }

        instance.Play(true);
        Destroy(instance.gameObject, lifetime);
    }

    private void ShowRewardFeedback()
    {
        if (health == null || health.damagePopupPrefab == null)
            return;

        GameObject popup = Instantiate(
            health.damagePopupPrefab,
            transform.position + health.popupOffset,
            Quaternion.identity
        );
        DamagePopup damagePopup = popup.GetComponent<DamagePopup>();

        if (damagePopup != null)
            damagePopup.SetRewardMultiplier(RewardMultiplier, goldenTint);
    }

    private void ResetSpawnState(bool restoreHealth)
    {
        if (restoreHealth && IsGolden && health != null)
            health.SetRuntimeMaxHealth(originalMaxHealth);

        if (hasOriginalColor && bodyRenderer != null)
            bodyRenderer.color = originalColor;

        if (deathSubscribed && health != null)
        {
            health.OnDied -= HandleDeath;
            deathSubscribed = false;
        }

        hasOriginalColor = false;
        rolledThisSpawn = false;
        assignmentPulseRemaining = 0f;
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
