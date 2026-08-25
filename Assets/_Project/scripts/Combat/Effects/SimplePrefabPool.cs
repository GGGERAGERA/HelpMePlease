using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

/// <summary>
/// Small scene-owned prefab pool for high-frequency gameplay objects.
/// The owner controls lifetime; active objects fall back to Destroy after the
/// owner is gone.
/// </summary>
public sealed class SimplePrefabPool
{
    private readonly MonoBehaviour owner;
    private readonly GameObject prefab;
    private readonly Transform inactiveRoot;
    private readonly ObjectPool<PooledGameObject> pool;
    private bool disposed;

    public SimplePrefabPool(
        MonoBehaviour owner,
        GameObject prefab,
        int prewarmCount,
        int maximumSize)
    {
        this.owner = owner;
        this.prefab = prefab;

        GameObject root = new($"{prefab.name} Pool");
        inactiveRoot = root.transform;
        inactiveRoot.SetParent(owner.transform, false);

        int warmCount = Mathf.Max(0, prewarmCount);
        int capacity = Mathf.Max(1, warmCount);
        pool = new ObjectPool<PooledGameObject>(
            Create,
            null,
            OnRelease,
            OnDestroyItem,
            true,
            capacity,
            Mathf.Max(capacity, maximumSize));

        List<PooledGameObject> prewarmed = new(warmCount);
        for (int i = 0; i < warmCount; i++)
            prewarmed.Add(pool.Get());

        for (int i = 0; i < prewarmed.Count; i++)
            pool.Release(prewarmed[i]);
    }

    public PooledGameObject Get(
        Vector3 position,
        Quaternion rotation,
        float scale = 1f,
        bool useHierarchyParticleScaling = false)
    {
        if (disposed || owner == null || prefab == null)
            return null;

        PooledGameObject item = pool.Get();
        item.Prepare(
            position,
            rotation,
            scale,
            useHierarchyParticleScaling);
        return item;
    }

    internal bool Release(PooledGameObject item)
    {
        if (item == null)
            return false;

        if (disposed || owner == null)
        {
            DestroyObject(item.gameObject);
            return true;
        }

        pool.Release(item);
        return true;
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        pool.Clear();

        if (inactiveRoot != null)
            DestroyObject(inactiveRoot.gameObject);
    }

    private PooledGameObject Create()
    {
        GameObject instance = Object.Instantiate(prefab, inactiveRoot);
        PooledGameObject item =
            instance.GetComponent<PooledGameObject>() ??
            instance.AddComponent<PooledGameObject>();
        item.Configure(this);
        instance.SetActive(false);
        return item;
    }

    private void OnRelease(PooledGameObject item)
    {
        if (item == null)
            return;

        item.transform.SetParent(inactiveRoot, false);
        item.gameObject.SetActive(false);
    }

    private static void OnDestroyItem(PooledGameObject item)
    {
        if (item != null)
            DestroyObject(item.gameObject);
    }

    internal static void DestroyObject(Object target)
    {
        if (target == null)
            return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Object.DestroyImmediate(target);
            return;
        }
#endif
        Object.Destroy(target);
    }
}

[DisallowMultipleComponent]
public sealed class PooledGameObject : MonoBehaviour
{
    private SimplePrefabPool ownerPool;
    private Vector3 authoredScale;
    private ParticleSystem[] particles;
    private float[] authoredParticleSizes;
    private float[] authoredParticleSpeeds;
    private float[] authoredEmissionRates;
    private ParticleSystemScalingMode[] authoredParticleScalingModes;
    private TrailRenderer[] trails;
    private float[] authoredTrailWidths;
    private Collider2D[] colliders;
    private bool[] authoredColliderStates;
    private Rigidbody2D[] bodies;
    private bool released = true;
    private float releaseAt = -1f;

    public IWeaponProjectile WeaponProjectile { get; private set; }
    public EnemyProjectile EnemyProjectile { get; private set; }
    public ProjectileCombatContext CombatContext { get; private set; }
    public ParticleSystem PrimaryParticleSystem { get; private set; }
    public DamagePopup DamagePopup { get; private set; }
    public Vector3 AuthoredScale => authoredScale;

    internal void Configure(SimplePrefabPool pool)
    {
        ownerPool = pool;
        authoredScale = transform.localScale;
        particles = GetComponentsInChildren<ParticleSystem>(true);
        trails = GetComponentsInChildren<TrailRenderer>(true);
        colliders = GetComponentsInChildren<Collider2D>(true);
        bodies = GetComponentsInChildren<Rigidbody2D>(true);

        authoredParticleSizes = new float[particles.Length];
        authoredParticleSpeeds = new float[particles.Length];
        authoredEmissionRates = new float[particles.Length];
        authoredParticleScalingModes =
            new ParticleSystemScalingMode[particles.Length];
        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem.MainModule main = particles[i].main;
            ParticleSystem.EmissionModule emission = particles[i].emission;
            authoredParticleSizes[i] = main.startSizeMultiplier;
            authoredParticleSpeeds[i] = main.startSpeedMultiplier;
            authoredEmissionRates[i] = emission.rateOverTimeMultiplier;
            authoredParticleScalingModes[i] = main.scalingMode;
        }

        authoredTrailWidths = new float[trails.Length];
        for (int i = 0; i < trails.Length; i++)
            authoredTrailWidths[i] = trails[i].widthMultiplier;

        authoredColliderStates = new bool[colliders.Length];
        for (int i = 0; i < colliders.Length; i++)
            authoredColliderStates[i] = colliders[i].enabled;

        WeaponProjectile = GetComponent<IWeaponProjectile>();
        EnemyProjectile = GetComponent<EnemyProjectile>();
        PrimaryParticleSystem = GetComponent<ParticleSystem>();
        DamagePopup = GetComponent<DamagePopup>();
        DamagePopup?.ConfigurePoolHandle(this);
        if (WeaponProjectile != null)
        {
            CombatContext = GetComponent<ProjectileCombatContext>() ??
                gameObject.AddComponent<ProjectileCombatContext>();
        }
    }

    internal void Prepare(
        Vector3 position,
        Quaternion rotation,
        float scale,
        bool useHierarchyParticleScaling)
    {
        released = false;
        releaseAt = -1f;
        transform.SetParent(null, false);
        transform.SetPositionAndRotation(position, rotation);

        float safeScale = Mathf.Max(0.1f, scale);
        transform.localScale = authoredScale * safeScale;

        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody2D body = bodies[i];
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            if (body.transform == transform)
            {
                body.position = position;
                body.SetRotation(rotation.eulerAngles.z);
            }
        }

        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = authoredColliderStates[i];

        for (int i = 0; i < trails.Length; i++)
        {
            trails[i].widthMultiplier = authoredTrailWidths[i] * safeScale;
            trails[i].Clear();
        }

        for (int i = 0; i < particles.Length; i++)
        {
            ParticleSystem particle = particles[i];
            particle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particle.main;
            ParticleSystem.EmissionModule emission = particle.emission;
            main.startSizeMultiplier = authoredParticleSizes[i];
            main.startSpeedMultiplier = authoredParticleSpeeds[i];
            main.scalingMode = authoredParticleScalingModes[i];
            emission.rateOverTimeMultiplier = authoredEmissionRates[i];

            if (useHierarchyParticleScaling)
            {
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
        }

        gameObject.SetActive(true);
    }

    public void ReleaseAfter(float duration)
    {
        if (duration <= 0f)
        {
            Release();
            return;
        }

        releaseAt = Time.time + duration;
    }

    public bool Release()
    {
        if (released)
            return false;

        released = true;
        releaseAt = -1f;

        if (ownerPool != null && ownerPool.Release(this))
            return true;

        SimplePrefabPool.DestroyObject(gameObject);
        return true;
    }

    private void Update()
    {
        if (!released && releaseAt >= 0f && Time.time >= releaseAt)
            Release();
    }
}
