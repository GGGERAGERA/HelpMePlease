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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        PhysicalCombatFeedbackRuntime.TryDetachDeathVisual(health);
#endif
    }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public void DebugSimulateFeedback(
        float damage,
        Vector2 hitPoint,
        bool isCritical,
        bool lethal)
    {
        if (health.IsDead)
            return;
        whiteFlash?.Flash();
        SpawnBlood(hitPoint, isCritical);
        PlayHitSound(isCritical);
        ShowDamagePopup(Mathf.RoundToInt(damage), isCritical);
        health.OnDamageTaken?.Invoke();
        if (lethal)
            PresentDeath();
    }
#endif
    private void SpawnBlood(Vector2 hitPoint, bool isCritical)
    {
        if (bloodHitPrefab == null)
            return;
        float bloodHitDestroyTime = bloodHitPrefab.main.duration;
        PooledGameObject pooledBlood = bloodHitPool?.Get(
            hitPoint,
            Quaternion.identity);
        ParticleSystem blood = pooledBlood != null
            ? pooledBlood.PrimaryParticleSystem
            : Instantiate(bloodHitPrefab, hitPoint, Quaternion.identity);
        if (isCritical)
        {
            var main = blood.main;
            main.startSizeMultiplier *= 1.4f;
            main.startSpeedMultiplier *= 1.3f;

            var emission = blood.emission;
            emission.rateOverTimeMultiplier *= 1.5f;
        }

        blood.Play();

        if (pooledBlood != null)
            pooledBlood.ReleaseAfter(bloodHitDestroyTime);
        else
            Destroy(blood.gameObject, bloodHitDestroyTime);
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
            transform.position, Quaternion.identity);
        ParticleSystem effect = pooled != null
            ? pooled.PrimaryParticleSystem
            : Instantiate(deathFXPrefab, transform.position, Quaternion.identity);
        float lifetime = effect.main.duration;
        effect.Play();
        if (pooled != null)
            pooled.ReleaseAfter(lifetime);
        else
            Destroy(effect.gameObject, lifetime);
    }
}
