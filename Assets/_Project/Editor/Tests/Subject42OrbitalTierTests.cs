#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class Subject42OrbitalTierTests
{
    private static OrbitalPresentationConfig Config => OrbitalPresentationConfig.Active;

    [Test]
    public void Progression_UsesOnlyThisRingsCommittedUpgrades()
    {
        var state = OrbitalRunState.CreateDefault(7);
        var first = state.Rings[0];
        var other = state.AddRing();
        Assert.That(first.VisualTier, Is.EqualTo(1));
        Assert.That(state.UpgradeCore(), Is.True);
        Assert.That(state.UpgradeModuleDamage(state.Modules[0].StableModuleId), Is.True);
        Assert.That(first.VisualTier, Is.EqualTo(1));
        Assert.That(state.UpgradeRingSpeed(first.StableRingId), Is.True);
        Assert.That(first.VisualTier, Is.EqualTo(2));
        Assert.That(state.UpgradeRingPower(first.StableRingId), Is.True);
        Assert.That(first.VisualTier, Is.EqualTo(3));
        Assert.That(state.AddMount(first.StableRingId, out _), Is.True);
        Assert.That(first.VisualTier, Is.EqualTo(4));
        Assert.That(state.UpgradeRingPower(first.StableRingId), Is.True);
        Assert.That(first.VisualTier, Is.EqualTo(4));
        Assert.That(other.VisualTier, Is.EqualTo(1));
        var restored = JsonUtility.FromJson<OrbitalRunState>(JsonUtility.ToJson(state));
        Assert.That(restored.Rings[0].VisualTier, Is.EqualTo(4));
        Assert.That(restored.Rings[1].VisualTier, Is.EqualTo(1));
    }

    [Test]
    public void TierTransition_PreservesHueDepthMaterialAndHasNoRestoreBurst()
    {
        var view = Object.Instantiate(Config.RingPrefab);
        try
        {
            var material = view.FrontLine.sharedMaterial;
            Assert.That(material.shader.name, Is.EqualTo("Subject42/Orbital Ring"));
            Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
            view.InitializeTier(1);
            Assert.That(view.IsTierTransitionActive, Is.False);
            for (int tier = 2; tier <= 4; tier++)
            {
                view.UpdateTierAppearance(tier, 2f, 0f, 0f, false, true, 0f);
                Assert.That(view.IsTierTransitionActive, Is.True);
                view.UpdateTierAppearance(tier, 2f, 0f, 0f, false, true, .2f);
                view.UpdateTierAppearance(tier, 2f, 0f, 0f, false, true, 0f);
                var block = new MaterialPropertyBlock();
                view.FrontLine.GetPropertyBlock(block);
                float burstBrightness = block.GetVector("_RingSurface").x;
                view.UpdateTierAppearance(tier, 2f, 0f, 0f, false, true, .2f);
                view.UpdateTierAppearance(tier, 2f, 0f, 0f, false, true, 0f);
                Assert.That(view.AccentColor, Is.EqualTo(Config.GetRingTier(tier).BaseColor));
                view.FrontLine.GetPropertyBlock(block);
                Assert.That(burstBrightness, Is.GreaterThan(block.GetVector("_RingSurface").x));
                Assert.That(view.IsTierTransitionActive, Is.False);
                view.UpdateTierAppearance(tier, 2f, 1f, 3f, false, true, 0f);
                var front = view.FrontLine.colorGradient.Evaluate(.5f);
                var back = view.BackLine.colorGradient.Evaluate(.5f);
                Assert.That(front.r, Is.EqualTo(view.AccentColor.r).Within(.001f));
                Assert.That(back.a, Is.EqualTo(front.a * .5f).Within(.001f));
                Assert.That(view.FrontLine.sharedMaterial, Is.SameAs(material));
            }
            view.InitializeTier(4);
            Assert.That(view.IsTierTransitionActive, Is.False);
        }
        finally { Object.DestroyImmediate(view.gameObject); }
    }

    [Test]
    public void RenderReadability_MixedStationAtTwoZooms()
    {
        var root = new GameObject("Tier render fixture");
        var cameraRoot = new GameObject("Tier render camera");
        var target = new RenderTexture(1024, 1024, 24);
        var pixels = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            var camera = cameraRoot.AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.015f, .02f, .035f);
            camera.cullingMask = 1 << 31;
            camera.targetTexture = target;
            int[] tiers = { 4, 3, 1, 2, 3, 4, 4, 4 };
            var state = OrbitalRunState.CreateDefault(7);
            while (state.Rings.Count < tiers.Length) state.AddRing();
            for (int i = 0; i < tiers.Length; i++)
            {
                var ringState = state.Rings[i];
                for (int upgrade = 1; upgrade < tiers[i]; upgrade++) state.UpgradeRingPower(ringState.StableRingId);
                var ring = new OrbitalRingRuntime(ringState, root.transform, Config.VisualMaterial, Config.PixelSprite);
                ring.Tick(.016f);
                for (int mount = 0; mount < ring.Mounts.Count; mount++)
                {
                    var module = Object.Instantiate(Config.GetPrefab((OrbitalModuleKind)(mount % 3)), ring.Mounts[mount].Transform, false);
                    ring.Mounts[mount].SetVisualState(OrbitalMountRuntime.VisualState.Occupied);
                }
            }
            foreach (var child in root.GetComponentsInChildren<Transform>()) child.gameObject.layer = 31;
            foreach (float zoom in new[] { 5.5f, 10f })
            {
                camera.orthographicSize = zoom;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
                pixels.Apply();
                string folder = System.IO.Path.GetFullPath("Artifacts/OrbitalTiers");
                System.IO.Directory.CreateDirectory(folder);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, $"mixed-{zoom:0.0}.png"), pixels.EncodeToPNG());
            }
        }
        finally
        {
            RenderTexture.active = previous;
            Object.DestroyImmediate(cameraRoot);
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(pixels);
        }
    }

    [Test]
    public void RingTick_AfterWarmupDoesNotAllocateManagedMemory()
    {
        var root = new GameObject("Tier allocation fixture");
        try
        {
            var state = OrbitalRunState.CreateDefault(7);
            state.UpgradeRingPower(state.Rings[0].StableRingId);
            state.UpgradeRingPower(state.Rings[0].StableRingId);
            var ring = new OrbitalRingRuntime(state.Rings[0], root.transform, Config.VisualMaterial, Config.PixelSprite);
            for (int i = 0; i < 32; i++) ring.Tick(.016f);
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 256; i++) ring.Tick(.016f);
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
        }
        finally { Object.DestroyImmediate(root); }
    }

    [UnityTest]
    public IEnumerator MixedAndMatchingRings_RestoreWithIndependentColorsAndAuthoredModules()
    {
        yield return new EnterPlayMode();
        var root = new GameObject("Tier station fixture");
        try
        {
            var state = OrbitalRunState.CreateDefault(7);
            while (state.Rings.Count < 8) state.AddRing();
            int[] upgrades = { 3, 2, 0, 1, 2, 3, 3, 3 };
            for (int i = 0; i < state.Rings.Count; i++)
                for (int j = 0; j < upgrades[i]; j++)
                    Assert.That(state.UpgradeRingPower(state.Rings[i].StableRingId), Is.True);
            for (int restore = 0; restore < 3; restore++)
            {
                foreach (var ringState in state.Rings)
                {
                    var ring = new OrbitalRingRuntime(ringState, root.transform, Config.VisualMaterial, Config.PixelSprite);
                    ring.Tick(.1f);
                    Assert.That(ring.AccentColor, Is.EqualTo(Config.GetRingTier(ringState.VisualTier).BaseColor));
                    var mount = ring.Mounts[0];
                    var mountView = mount.Transform.GetComponent<OrbitalMountView>();
                    Assert.That(mountView.Marker.color.r, Is.EqualTo(ring.AccentColor.r).Within(.001f));
                    var module = Object.Instantiate(Config.LaserSwordPrefab, mount.Transform, false);
                    var renderers = module.GetComponentsInChildren<SpriteRenderer>();
                    var colors = new Color[renderers.Length];
                    for (int i = 0; i < colors.Length; i++) colors[i] = renderers[i].color;
                    ring.SetSelected(true);
                    ring.SetInteractionState(true, true);
                    ring.Tick(.1f);
                    for (int i = 0; i < colors.Length; i++) Assert.That(renderers[i].color, Is.EqualTo(colors[i]));
                    mount.SetVisualState(OrbitalMountRuntime.VisualState.ValidHover);
                    Assert.That(mountView.Marker.color.g, Is.EqualTo(1f));
                    ring.Teardown();
                }
                yield return null;
                state = JsonUtility.FromJson<OrbitalRunState>(JsonUtility.ToJson(state));
            }
        }
        finally { Object.Destroy(root); }
        yield return null;
        yield return new ExitPlayMode();
    }
}
#endif
