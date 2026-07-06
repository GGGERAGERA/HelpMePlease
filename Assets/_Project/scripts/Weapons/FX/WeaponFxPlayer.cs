using UnityEngine;

public sealed class WeaponFxPlayer : MonoBehaviour
{
    [Header("Muzzle FX")]
    [SerializeField] private ParticleSystem muzzleFxPrefab;
    [SerializeField] private float muzzleFxLifetime = 0.35f;

    [Header("Impact FX")]
    [SerializeField] private ParticleSystem impactFxPrefab;
    [SerializeField] private float impactFxLifetime = 0.35f;

    [Header("Camera Shake")]
    [SerializeField] private float fireShakeDuration = 0.08f;
    [SerializeField] private float fireShakeMagnitude = 0.08f;
    [SerializeField] private float hitShakeDuration = 0.08f;
    [SerializeField] private float hitShakeMagnitude = 0.08f;
    [SerializeField] private float critShakeDuration = 0.12f;
    [SerializeField] private float critShakeMagnitude = 0.16f;

    public void PlayFire(Vector2 position, Vector2 direction)
    {
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
        ParticleSystem prefab,
        Vector2 position,
        Vector2 direction,
        float lifetime
    )
    {
        if (prefab == null)
            return;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        ParticleSystem fx = Instantiate(
            prefab,
            position,
            Quaternion.Euler(0f, 0f, angle)
        );

        fx.Play();
        Destroy(fx.gameObject, lifetime);
    }
}