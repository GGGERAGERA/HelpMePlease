#if UNITY_EDITOR
using NUnit.Framework;
using UnityEngine;

public sealed class Subject42PoolingTests
{
    private GameObject ownerObject;
    private GameObject prefab;
    private SimplePrefabPool pool;

    [SetUp]
    public void SetUp()
    {
        ownerObject = new GameObject("Pool Owner Test");
        ProjectileFireBehaviour owner =
            ownerObject.AddComponent<ProjectileFireBehaviour>();

        prefab = new GameObject("Pooled Projectile Test");
        prefab.SetActive(false);
        prefab.transform.localScale = new Vector3(1.5f, 2f, 1f);
        prefab.AddComponent<Rigidbody2D>();
        prefab.AddComponent<BoxCollider2D>();
        TrailRenderer trail = prefab.AddComponent<TrailRenderer>();
        trail.widthMultiplier = 0.4f;
        ParticleSystem particle = prefab.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particle.main;
        main.startSizeMultiplier = 1.25f;
        main.startSpeedMultiplier = 2.5f;
        ParticleSystem.EmissionModule emission = particle.emission;
        emission.rateOverTimeMultiplier = 3.5f;
        prefab.AddComponent<Bullet>();

        pool = new SimplePrefabPool(owner, prefab, 1, 4);
    }

    [TearDown]
    public void TearDown()
    {
        pool?.Dispose();

        if (ownerObject != null)
            Object.DestroyImmediate(ownerObject);

        if (prefab != null)
            Object.DestroyImmediate(prefab);
    }

    [Test]
    public void ReusedProjectile_RestoresPhysicsColliderTrailAndScale()
    {
        PooledGameObject first = pool.Get(
            new Vector3(3f, 4f, 0f),
            Quaternion.Euler(0f, 0f, 25f),
            2f,
            true);

        Rigidbody2D firstBody = first.GetComponent<Rigidbody2D>();
        Collider2D firstCollider = first.GetComponent<Collider2D>();
        TrailRenderer firstTrail = first.GetComponent<TrailRenderer>();
        ParticleSystem firstParticle = first.GetComponent<ParticleSystem>();
        firstBody.linearVelocity = new Vector2(8f, -3f);
        firstBody.angularVelocity = 5f;
        firstCollider.enabled = false;
        firstTrail.widthMultiplier = 9f;
        ParticleSystem.MainModule firstMain = firstParticle.main;
        firstMain.startSizeMultiplier = 8f;
        firstMain.startSpeedMultiplier = 9f;
        ParticleSystem.EmissionModule firstEmission = firstParticle.emission;
        firstEmission.rateOverTimeMultiplier = 10f;

        Assert.That(first.Release(), Is.True);

        PooledGameObject second = pool.Get(
            Vector3.zero,
            Quaternion.identity,
            1f,
            false);

        Assert.That(second, Is.SameAs(first));
        Assert.That(second.transform.localScale, Is.EqualTo(
            new Vector3(1.5f, 2f, 1f)));
        Assert.That(second.GetComponent<Rigidbody2D>().linearVelocity,
            Is.EqualTo(Vector2.zero));
        Assert.That(second.GetComponent<Rigidbody2D>().angularVelocity,
            Is.Zero);
        Assert.That(second.GetComponent<Collider2D>().enabled, Is.True);
        Assert.That(second.GetComponent<TrailRenderer>().widthMultiplier,
            Is.EqualTo(0.4f).Within(0.0001f));
        ParticleSystem secondParticle = second.GetComponent<ParticleSystem>();
        ParticleSystem.MainModule secondMain = secondParticle.main;
        ParticleSystem.EmissionModule secondEmission = secondParticle.emission;
        Assert.That(secondMain.startSizeMultiplier,
            Is.EqualTo(1.25f).Within(0.0001f));
        Assert.That(secondMain.startSpeedMultiplier,
            Is.EqualTo(2.5f).Within(0.0001f));
        Assert.That(secondEmission.rateOverTimeMultiplier,
            Is.EqualTo(3.5f).Within(0.0001f));
        Assert.That(secondMain.scalingMode,
            Is.EqualTo(ParticleSystemScalingMode.Local));
        Assert.That(second.WeaponProjectile, Is.Not.Null);
        Assert.That(second.CombatContext, Is.Not.Null);

        second.Release();
    }
}
#endif
