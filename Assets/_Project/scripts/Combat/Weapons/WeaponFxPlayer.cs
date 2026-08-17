using UnityEngine;

public sealed class WeaponFxPlayer : MonoBehaviour
{
    [Header("Muzzle FX")]
    [SerializeField] private GameObject muzzleFxPrefab;
    [SerializeField] private float muzzleFxLifetime = 0.35f;

    [Header("Impact FX")]
    [SerializeField] private GameObject impactFxPrefab;
    [SerializeField] private float impactFxLifetime = 0.35f;

    [Header("Camera Shake")]
    [SerializeField] private float fireShakeDuration = 0.08f;
    [SerializeField] private float fireShakeMagnitude = 0.08f;
    [SerializeField] private float hitShakeDuration = 0.08f;
    [SerializeField] private float hitShakeMagnitude = 0.08f;
    [SerializeField] private float critShakeDuration = 0.12f;
    [SerializeField] private float critShakeMagnitude = 0.16f;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public float FireShakeDuration => fireShakeDuration;
    public float FireShakeMagnitude => fireShakeMagnitude;
    public float HitShakeDuration => hitShakeDuration;
    public float HitShakeMagnitude => hitShakeMagnitude;
    public float CritShakeDuration => critShakeDuration;
    public float CritShakeMagnitude => critShakeMagnitude;

    public void SetFireShakeDuration(float value) =>
        fireShakeDuration = Mathf.Max(0f, value);
    public void SetFireShakeMagnitude(float value) =>
        fireShakeMagnitude = Mathf.Max(0f, value);
    public void SetHitShakeDuration(float value) =>
        hitShakeDuration = Mathf.Max(0f, value);
    public void SetHitShakeMagnitude(float value) =>
        hitShakeMagnitude = Mathf.Max(0f, value);
    public void SetCritShakeDuration(float value) =>
        critShakeDuration = Mathf.Max(0f, value);
    public void SetCritShakeMagnitude(float value) =>
        critShakeMagnitude = Mathf.Max(0f, value);
#endif

    public void PlayFire(Vector2 position, Vector2 direction)
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        PhysicalCombatFeedbackRuntime.NotifyWeaponFired(this, direction);
#endif
        SpawnParticle(muzzleFxPrefab, position, direction, muzzleFxLifetime);

        CameraShake.Instance?.Shake(
            fireShakeDuration,
            fireShakeMagnitude
        );
    }

    public void PlayHit(Vector2 position, Vector2 normalDirection, bool isCritical)
    {
        SpawnParticle(impactFxPrefab, position, normalDirection, impactFxLifetime);

        CameraShake.Instance?.Shake(
            isCritical ? critShakeDuration : hitShakeDuration,
            isCritical ? critShakeMagnitude : hitShakeMagnitude
        );
    }

    private void SpawnParticle(
        GameObject prefab,
        Vector2 position,
        Vector2 direction,
        float lifetime
    )
    {
        if (prefab == null)
            return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        GameObject fx = Instantiate(
            prefab,
            position,
            Quaternion.Euler(0f, 0f, angle)
        );

        Destroy(fx, lifetime);
    }
}
