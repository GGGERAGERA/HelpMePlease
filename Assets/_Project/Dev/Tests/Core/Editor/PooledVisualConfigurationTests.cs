#if UNITY_EDITOR
using NUnit.Framework;
using Subject42.Visuals;
using UnityEngine;

public sealed class PooledVisualConfigurationTests
{
    [Test]
    public void PresentationCallbackSeesAuthoredStateBeforeActivationOnEveryReuse()
    {
        var owner = new GameObject("Pool callback owner");
        var prefab = new GameObject("Pool callback particle source");
        prefab.SetActive(false);
        var particle = prefab.AddComponent<ParticleSystem>();
        var main = particle.main;
        main.playOnAwake = false;
        main.startSize = 2f;
        main.startSpeed = 4f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.3f, .7f);
        SimplePrefabPool pool = null;
        PooledGameObject active = null;
        try
        {
            pool = new SimplePrefabPool(owner.AddComponent<LiveVisualRuntime>(), prefab, 1, 2);
            int configured = 0;
            PooledGameObject ConfigureAndGet()
            {
                return pool.Get(Vector3.zero, Quaternion.identity, configurePresentation: item =>
                {
                    active = item;
                    Assert.That(item.gameObject.activeSelf, Is.False, "Visual configuration must precede PlayOnAwake/activation.");
                    var prepared = item.PrimaryParticleSystem.main;
                    Assert.That(prepared.startSizeMultiplier, Is.EqualTo(2f), "A prior configured/custom value cannot compound on reuse.");
                    Assert.That(prepared.startSpeedMultiplier, Is.EqualTo(4f));
                    Assert.That(prepared.startLifetime.mode, Is.EqualTo(ParticleSystemCurveMode.TwoConstants));
                    Assert.That(prepared.startLifetime.constantMin, Is.EqualTo(.3f).Within(.00001f));
                    Assert.That(prepared.startLifetime.constantMax, Is.EqualTo(.7f).Within(.00001f));
                    prepared.startSizeMultiplier *= .75f;
                    prepared.startSpeedMultiplier *= 1.3f;
                    prepared.startLifetime = .14f;
                    configured++;
                });
            }
            var first = ConfigureAndGet();
            Assert.That(first.gameObject.activeSelf, Is.True);
            Assert.That(first.PrimaryParticleSystem.main.startSizeMultiplier, Is.EqualTo(1.5f));
            first.Release();
            active = null;
            var second = ConfigureAndGet();
            Assert.That(second, Is.SameAs(first));
            Assert.That(second.PrimaryParticleSystem.main.startSizeMultiplier, Is.EqualTo(1.5f));
            Assert.That(configured, Is.EqualTo(2));
            second.Release();
            active = null;
        }
        finally
        {
            active?.Release();
            pool?.Dispose();
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(prefab);
        }
    }

    [Test]
    public void ReuseWithoutCallbackRestoresAuthoredParticleLifetimeCurve()
    {
        var owner = new GameObject("Pool lifetime owner");
        var prefab = new GameObject("Pool lifetime particle source");
        prefab.SetActive(false);
        var particle = prefab.AddComponent<ParticleSystem>();
        var main = particle.main;
        main.playOnAwake = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(.4f, 1.2f);
        SimplePrefabPool pool = null;
        PooledGameObject active = null;
        try
        {
            pool = new SimplePrefabPool(owner.AddComponent<LiveVisualRuntime>(), prefab, 1, 2);
            var first = pool.Get(Vector3.zero, Quaternion.identity);
            active = first;
            var edited = first.PrimaryParticleSystem.main;
            edited.startLifetime = 30f;
            first.Release();
            active = null;
            var reused = pool.Get(Vector3.zero, Quaternion.identity);
            active = reused;
            var restored = reused.PrimaryParticleSystem.main.startLifetime;
            Assert.That(restored.mode, Is.EqualTo(ParticleSystemCurveMode.TwoConstants));
            Assert.That(restored.constantMin, Is.EqualTo(.4f).Within(.00001f));
            Assert.That(restored.constantMax, Is.EqualTo(1.2f).Within(.00001f));
            reused.Release();
            active = null;
        }
        finally
        {
            active?.Release();
            pool?.Dispose();
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(prefab);
        }
    }
}
#endif
