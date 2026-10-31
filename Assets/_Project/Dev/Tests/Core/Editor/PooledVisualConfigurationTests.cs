#if UNITY_EDITOR
using NUnit.Framework;
using System;
using System.Linq;
using System.Reflection;
using Subject42.Visuals;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class PooledVisualConfigurationTests
{
    [Test]
    public void RepeatedPooledRegistrationDoesNotRebuildUnchangedScene()
    {
        WithRegisteredParticle((runtime, root, target) =>
        {
            LiveVisualRuntime.ApplyHierarchy(root);
            int revision = runtime.Revision;
            LiveVisualRuntime.RegisterHierarchy(root);
            LiveVisualRuntime.ApplyHierarchy(root);
            Assert.That(runtime.Revision, Is.EqualTo(revision), "Reusing a registered effect must not rebuild every scene target.");
            var child = new GameObject("New particle child"); child.SetActive(false); child.transform.SetParent(root.transform);
            child.AddComponent<ParticleSystem>();
            int count = runtime.Targets.Count;
            LiveVisualRuntime.RegisterHierarchy(root); LiveVisualRuntime.ApplyHierarchy(root);
            Assert.That(runtime.Targets.Count, Is.GreaterThan(count), "A genuinely new child must still be discovered.");
            Assert.That(runtime.Revision, Is.GreaterThan(revision));
        });
    }

    [Test]
    public void CapturePooledConfigurationOnlyVisitsRequestedHierarchy()
    {
        WithRegisteredParticle((runtime, root, target) =>
        {
            var outside = new GameObject("Unrelated visual"); outside.SetActive(false);
            int visited = 0;
            var unrelated = new VisualTarget("unrelated", "unrelated", "unrelated", "unrelated", outside.AddComponent<SpriteRenderer>()) { Alive = () => { visited++; return true; } };
            runtime.Targets.Add(unrelated);
            try
            {
                var main = root.GetComponent<ParticleSystem>().main; main.startSizeMultiplier = 7f;
                LiveVisualRuntime.CaptureConfiguredHierarchy(root);
                Assert.That(target.Properties.Single(p=>p.Id=="particles.size").Current, Is.EqualTo(7f));
                Assert.That(visited, Is.Zero, "Preparing a pooled effect must not inspect unrelated scene targets.");
            }
            finally { runtime.Targets.Remove(unrelated); UnityEngine.Object.DestroyImmediate(outside); }
        });
    }

    static void WithRegisteredParticle(Action<LiveVisualRuntime, GameObject, VisualTarget> check)
    {
        var instance = typeof(LiveVisualRuntime).GetField("<Instance>k__BackingField", BindingFlags.Static|BindingFlags.NonPublic);
        var previous = instance.GetValue(null);
        var owner = new GameObject("Isolated pooled visual test"); owner.SetActive(false);
        var root = new GameObject("Pooled particle test"); root.SetActive(false); root.AddComponent<ParticleSystem>();
        var runtime = owner.AddComponent<LiveVisualRuntime>();
        try
        {
            instance.SetValue(null,runtime);
            typeof(LiveVisualRuntime).GetField("<Preset>k__BackingField",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(runtime,AssetDatabase.LoadAssetAtPath<VisualTuningPreset>(VisualTuningPresetStorage.AssetPath));
            LiveVisualRuntime.RegisterHierarchy(root);
            check(runtime,root,runtime.Targets.Single(t=>t.Component is ParticleSystem));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner); UnityEngine.Object.DestroyImmediate(root); instance.SetValue(null,previous);
        }
    }

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
