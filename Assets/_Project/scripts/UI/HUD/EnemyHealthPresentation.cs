using UnityEngine;

// Owns pooled/transient combat presentation; health publishes damage/death only.
public sealed class EnemyHealthPresentation : MonoBehaviour
{
    [SerializeField] private EnemyHealth health;
    [SerializeField] private EnemyWhiteFlash whiteFlash;
    [SerializeField] private GameObject damagePopupPrefab;
    [SerializeField] private Vector3 popupOffset = new(0,1,0);
    [SerializeField] private ParticleSystem bloodHitPrefab;
    [SerializeField] private ParticleSystem deathFXPrefab;
    public Vector3 PopupOffset => popupOffset;
    private SimplePrefabPool damagePopupPool;
    private SimplePrefabPool bloodHitPool;
    private SimplePrefabPool deathFxPool;
    private const float HitDuration = .2f, HitLifetime = .14f, HitSizeScale = .75f;
    private const float CommonDeathDuration = .35f, CommonDeathLifetime = .28f, CommonDeathSizeScale = 7f / 6f;

    public GameObject DamagePopupPrefab => damagePopupPrefab;
    public GameObject BloodHitPrefab =>
        bloodHitPrefab != null ? bloodHitPrefab.gameObject : null;
    public GameObject DeathFxPrefab =>
        deathFXPrefab != null ? deathFXPrefab.gameObject : null;

    public void SetFeedbackPools(
        SimplePrefabPool popupPool,
        SimplePrefabPool hitPool,
        SimplePrefabPool deathPool)
    {
        damagePopupPool = popupPool;
        bloodHitPool = hitPool;
        deathFxPool = deathPool;
    }
    private void OnEnable() { health.HitFeedback += OnHit; health.DeathFeedback += OnDeath; }
    private void OnDisable() { health.HitFeedback -= OnHit; health.DeathFeedback -= OnDeath; }
    private void OnHit(float damage, Vector2 point, bool critical)
    {
        whiteFlash?.Flash(); SpawnBlood(point,critical); PlayHitSound(critical); ShowDamagePopup(Mathf.RoundToInt(damage),critical);
    }
    private void OnDeath()
    {
        PresentDeath();
    }
    private void SpawnBlood(Vector2 hitPoint, bool isCritical)
    {
        if (bloodHitPrefab == null)
            return;
        PooledGameObject pooledBlood = bloodHitPool?.Get(
            hitPoint,
            Quaternion.identity,
            configurePresentation: item => ConfigureBlood(item.PrimaryParticleSystem, isCritical));
        ParticleSystem blood = pooledBlood != null
            ? pooledBlood.PrimaryParticleSystem
            : Instantiate(bloodHitPrefab, hitPoint, Quaternion.identity);
        if (pooledBlood == null)
        {
            ConfigureBlood(blood, isCritical);
            Subject42.Visuals.LiveVisualRuntime.RegisterHierarchy(blood.gameObject, bloodHitPrefab.gameObject);
            Subject42.Visuals.LiveVisualRuntime.CaptureConfiguredHierarchy(blood.gameObject);
            Subject42.Visuals.LiveVisualRuntime.ApplyHierarchy(blood.gameObject);
        }

        blood.Play();
        float bloodHitDestroyTime = blood.main.duration;
        if (pooledBlood != null)
            pooledBlood.ReleaseAfter(bloodHitDestroyTime);
        else
            Destroy(blood.gameObject, bloodHitDestroyTime);
    }

    private static void ConfigureBlood(ParticleSystem blood, bool isCritical)
    {
        // This prefab is also used by props/minigames: tune only this owned instance.
        ConfigureCompactEffect(blood, HitDuration, HitLifetime, HitSizeScale);
        if (!isCritical) return;
        var main = blood.main;
        main.startSizeMultiplier *= 1.4f;
        main.startSpeedMultiplier *= 1.3f;
        var emission = blood.emission;
        emission.rateOverTimeMultiplier *= 1.5f;
    }
    void ShowDamagePopup(int damage, bool isCritical)
    {
        if (damagePopupPrefab == null)
            return;

        Vector3 spawnPos = transform.position + popupOffset;
        PooledGameObject pooledPopup = damagePopupPool?.Get(
            spawnPos,
            Quaternion.identity);
        DamagePopup dp = pooledPopup != null
            ? pooledPopup.DamagePopup
            : Instantiate(damagePopupPrefab, spawnPos, Quaternion.identity)
                .GetComponent<DamagePopup>();
        if (dp != null)
            dp.SetDamage(damage, isCritical);
        else
            pooledPopup?.Release();
    }
    private void PlayHitSound(bool isCritical)
    {
        // One bounded cue per damage event; critical replaces the ordinary hit.
        AudioService.Instance?.PlayAt(
            isCritical ? AudioCueId.EnemyCritical : AudioCueId.EnemyHit,
            transform.position);
    }
    private void PresentDeath()
    {
        AudioService.Instance?.PlayAt(
            health.IsBoss ? AudioCueId.BossDeath : AudioCueId.CommonEnemyDeath,
            transform.position);
        if (deathFXPrefab == null)
            return;
        PooledGameObject pooled = deathFxPool?.Get(
            transform.position, Quaternion.identity,
            configurePresentation: item => ConfigureDeath(item.PrimaryParticleSystem));
        ParticleSystem effect = pooled != null
            ? pooled.PrimaryParticleSystem
            : Instantiate(deathFXPrefab, transform.position, Quaternion.identity);
        if (pooled == null)
        {
            ConfigureDeath(effect);
            Subject42.Visuals.LiveVisualRuntime.RegisterHierarchy(effect.gameObject, deathFXPrefab.gameObject);
            Subject42.Visuals.LiveVisualRuntime.CaptureConfiguredHierarchy(effect.gameObject);
            Subject42.Visuals.LiveVisualRuntime.ApplyHierarchy(effect.gameObject);
        }
        float lifetime = effect.main.duration;
        effect.Play();
        if (pooled != null)
            pooled.ReleaseAfter(lifetime);
        else
            Destroy(effect.gameObject, lifetime);
    }

    private void ConfigureDeath(ParticleSystem effect)
    {
        if (!health.IsBoss)
            ConfigureCompactEffect(effect, CommonDeathDuration, CommonDeathLifetime, CommonDeathSizeScale);
    }

    private static void ConfigureCompactEffect(ParticleSystem effect, float duration, float lifetime, float sizeScale)
    {
        // Get/Instantiate may have started playOnAwake; duration is editable only stopped.
        effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = effect.main;
        main.duration = duration;
        main.startLifetime = lifetime;
        // The existing pool restores the authored size before every Get.
        main.startSizeMultiplier *= sizeScale;
    }
}
