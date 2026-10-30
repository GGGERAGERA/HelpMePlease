using UnityEngine;

public sealed class WeaponFxPlayer : MonoBehaviour
{
    [Header("Muzzle FX")]
    [SerializeField] private GameObject muzzleFxPrefab;
    [SerializeField] private float muzzleFxLifetime = 0.35f;

    [Header("Impact FX")]
    [SerializeField] private GameObject impactFxPrefab;
    [SerializeField] private float impactFxLifetime = 0.35f;

    [Header("Runtime Pool")]
    [SerializeField, Min(0)] private int prewarmPerEffect = 4;
    [SerializeField, Min(1)] private int maximumPerEffect = 64;

    private SimplePrefabPool muzzlePool;
    private SimplePrefabPool impactPool;

    [Header("Camera Shake")]
    [SerializeField] private float fireShakeDuration = 0.08f;
    [SerializeField] private float fireShakeMagnitude = 0.08f;
    [SerializeField] private float hitShakeDuration = 0.08f;
    [SerializeField] private float hitShakeMagnitude = 0.08f;
    [SerializeField] private float critShakeDuration = 0.12f;
    [SerializeField] private float critShakeMagnitude = 0.16f;

    private void Awake()
    {
        if (muzzleFxPrefab != null)
        {
            muzzlePool = new SimplePrefabPool(
                this,
                muzzleFxPrefab,
                prewarmPerEffect,
                maximumPerEffect);
        }

        if (impactFxPrefab == muzzleFxPrefab)
        {
            impactPool = muzzlePool;
        }
        else if (impactFxPrefab != null)
        {
            impactPool = new SimplePrefabPool(
                this,
                impactFxPrefab,
                prewarmPerEffect,
                maximumPerEffect);
        }
    }

    public void PlayFire(Vector2 position, Vector2 direction)
    {
        SpawnParticle(
            muzzlePool,
            muzzleFxPrefab,
            position,
            direction,
            muzzleFxLifetime
        );

        CameraShake.Instance?.Shake(
            fireShakeDuration,
            fireShakeMagnitude
        );
    }

    public void PlayHit(Vector2 position, Vector2 normalDirection, bool isCritical)
    {
        SpawnParticle(
            impactPool,
            impactFxPrefab,
            position,
            normalDirection,
            impactFxLifetime
        );

        CameraShake.Instance?.Shake(
            isCritical ? critShakeDuration : hitShakeDuration,
            isCritical ? critShakeMagnitude : hitShakeMagnitude
        );
    }

    private GameObject SpawnParticle(
        SimplePrefabPool effectPool,
        GameObject prefab,
        Vector2 position,
        Vector2 direction,
        float lifetime
    )
    {
        if (prefab == null)
            return null;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
        PooledGameObject pooled = effectPool?.Get(position, rotation);

        if (pooled != null)
        {
            pooled.ReleaseAfter(lifetime);
            return pooled.gameObject;
        }

        GameObject fx = Instantiate(prefab, position, rotation);
        Destroy(fx, lifetime);
        return fx;
    }

    private void OnDestroy()
    {
        muzzlePool?.Dispose();

        if (!ReferenceEquals(impactPool, muzzlePool))
            impactPool?.Dispose();

        muzzlePool = null;
        impactPool = null;
    }
}
