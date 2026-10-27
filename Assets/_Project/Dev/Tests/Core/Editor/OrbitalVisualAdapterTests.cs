#if UNITY_EDITOR
using NUnit.Framework;
using System.Linq;
using Subject42.Combat.OrbitalStation;
using Subject42.Visuals;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public sealed class OrbitalVisualAdapterTests
{
    [TearDown]
    public void ReleaseRuntimeConfiguration()
    {
        if (OrbitalPresentationConfig.IsConfigured) Object.DestroyImmediate(OrbitalPresentationConfig.Active);
    }

    [Test]
    public void RuntimeConfigurationNeverMutatesAuthoredPresentation()
    {
        var source = ScriptableObject.CreateInstance<OrbitalPresentationConfig>();
        OrbitalPresentationConfig.Configure(source);
        var runtime = OrbitalPresentationConfig.Active;
        Assert.That(runtime, Is.Not.SameAs(source));
        runtime.RingSurface.Glow = 3f;
        runtime.PistolProjectile.TrailOpacity = .8f;
        runtime.GetFlashStyle(OrbitalFlashFamily.MuzzlePistol).Brightness = 4f;
        Assert.That(source.RingSurface.Glow, Is.EqualTo(1f));
        Assert.That(source.PistolProjectile.TrailOpacity, Is.EqualTo(.3f));
        Assert.That(source.GetFlashStyle(OrbitalFlashFamily.MuzzlePistol).Brightness, Is.EqualTo(1f));
        Object.DestroyImmediate(source);
    }

    [Test]
    public void FlashFamiliesHaveIndependentIdentityDefaultsAndOverrides()
    {
        var config = ScriptableObject.CreateInstance<OrbitalPresentationConfig>();
        var source = new Color(.2f, .7f, 1f, .6f);
        foreach (OrbitalFlashFamily family in System.Enum.GetValues(typeof(OrbitalFlashFamily)))
        {
            var style = config.GetFlashStyle(family);
            Assert.That(style.ApplyColor(source), Is.EqualTo(source), family.ToString());
            Assert.That(style.Duration, Is.EqualTo(1f));
            Assert.That(style.Width, Is.EqualTo(1f));
        }
        config.GetFlashStyle(OrbitalFlashFamily.MuzzlePistol).Brightness = 3f;
        config.GetFlashStyle(OrbitalFlashFamily.MuzzlePistol).Opacity = .5f;
        Assert.That(config.GetFlashStyle(OrbitalFlashFamily.MuzzlePistol).ApplyColor(source).a, Is.EqualTo(.3f));
        Assert.That(config.GetFlashStyle(OrbitalFlashFamily.MuzzleShotgun).ApplyColor(source), Is.EqualTo(source));
        Assert.That(config.GetFlashStyle(OrbitalFlashFamily.Impact).ApplyColor(source), Is.EqualTo(source));
        Assert.That(config.GetFlashStyle(OrbitalFlashFamily.CoreRay).ApplyColor(source), Is.EqualTo(source));
        var tint = config.GetFlashStyle(OrbitalFlashFamily.MuzzlePistol);
        tint.Tint = new Color(.4f, .1f, .8f, .2f);
        tint.TintAmount = 1f;
        Assert.That(tint.ApplyColor(source).a, Is.EqualTo(.1f).Within(.00001f), "Tint alpha is a real editable channel.");
        Object.DestroyImmediate(config);
    }

    [Test]
    public void FlashTailCanReproducePreHierarchyConstantOpacity()
    {
        Assert.That(OrbitalFlashStyle.TailOpacity(.8f, 0f), Is.EqualTo(1f));
        Assert.That(OrbitalFlashStyle.TailOpacity(0f, 1f), Is.EqualTo(1f));
        Assert.That(OrbitalFlashStyle.TailOpacity(1f, 1f), Is.EqualTo(0f));
        Assert.That(OrbitalFlashStyle.TailOpacity(.8f, 1f), Is.LessThan(1f));
    }

    [Test]
    public void AdapterChangesExistingProjectileStyleWithoutChangingCollisionOrCadence()
    {
        var source = ScriptableObject.CreateInstance<OrbitalPresentationConfig>();
        OrbitalPresentationConfig.Configure(source);
        var settings = OrbitalPresentationConfig.Active.GetWeaponSettings(OrbitalModuleKind.Pistol);
        Vector2 hitShape = settings.Presentation.BodySize;
        float interval = settings.FireInterval, damage = settings.Damage, radius = settings.ProjectileRadius;
        var target = OrbitalVisualAdapter.CreateTarget();
        target.Properties.Single(p => p.Id == "config.PistolProjectile.Color.r").Write(3f);
        target.Properties.Single(p => p.Id == "config.PistolProjectile.TrailSeconds").Write(2f);
        Assert.That(settings.Presentation.Color.r, Is.EqualTo(3f), "Existing modules retain the same live visual style.");
        Assert.That(settings.Presentation.TrailSeconds, Is.EqualTo(2f));
        Assert.That(source.PistolProjectile.TrailSeconds, Is.EqualTo(.045f), "The source asset is untouched.");
        Assert.That(settings.Presentation.BodySize, Is.EqualTo(hitShape));
        Assert.That(settings.FireInterval, Is.EqualTo(interval));
        Assert.That(settings.Damage, Is.EqualTo(damage));
        Assert.That(settings.ProjectileRadius, Is.EqualTo(radius));
        Assert.That(target.Properties.Any(p => p.Id.Contains("BodySize") || p.Id.Contains("Radius") || p.Id.Contains("FireInterval")), Is.False);
        Object.DestroyImmediate(source);
    }

    [Test]
    public void AdapterRejectsNonFiniteAndBoundsFlashDurationWithoutTouchingSiblingFamily()
    {
        var source = ScriptableObject.CreateInstance<OrbitalPresentationConfig>();
        OrbitalPresentationConfig.Configure(source);
        var target = OrbitalVisualAdapter.CreateTarget();
        var duration = target.Properties.Single(p => p.Id == "flash.ImpactPistol.Duration");
        duration.Write(float.NaN);
        duration.Write(float.PositiveInfinity);
        Assert.That(duration.Read(), Is.EqualTo(1f));
        duration.Write(100000f);
        Assert.That(duration.Read(), Is.EqualTo(20f));
        Assert.That(OrbitalPresentationConfig.Active.GetFlashStyle(OrbitalFlashFamily.ImpactShotgun).Duration, Is.EqualTo(1f));
        Object.DestroyImmediate(source);
    }

    [Test]
    public void ResetPointLightRestoresBothCapturedRadiiInCustomMode()
    {
        var host = new GameObject("Visual restore regression");
        var lightObject = new GameObject("Point light");
        var preset = ScriptableObject.CreateInstance<VisualTuningPreset>();
        try
        {
            var runtime = host.AddComponent<LiveVisualRuntime>();
            var light = lightObject.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.pointLightOuterRadius = 10f;
            light.pointLightInnerRadius = 5f;
            var target = new VisualTarget("light-instance", "point-light", "Свет", "Свет", light);
            VisualComponentAdapters.Populate(target, preset, new VisualProfile());
            runtime.Targets.Add(target);
            runtime.BeginEdit();
            runtime.Edit("Type", target, target.Properties.Single(p => p.Id == "light.innerRadius"), 2f);
            runtime.Edit("Type", target, target.Properties.Single(p => p.Id == "light.outerRadius"), 3f);
            Assert.That(light.pointLightInnerRadius, Is.EqualTo(2f));
            Assert.That(light.pointLightOuterRadius, Is.EqualTo(3f));
            runtime.Reset("Type", target);
            Assert.That(light.pointLightInnerRadius, Is.EqualTo(5f), "Reset must not clamp the captured inner radius to the edited outer radius.");
            Assert.That(light.pointLightOuterRadius, Is.EqualTo(10f));
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(lightObject);
            Object.DestroyImmediate(preset);
        }
    }

    [Test]
    public void ResetPostProcessingRestoresInheritedOverrideState()
    {
        var volumeObject = new GameObject("Post restore regression");
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();
        var preset = ScriptableObject.CreateInstance<VisualTuningPreset>();
        VolumeProfile runtimeProfile = null;
        try
        {
            var bloom = profile.Add<Bloom>();
            bloom.intensity.value = 1.5f;
            bloom.intensity.overrideState = false;
            var volume = volumeObject.AddComponent<Volume>();
            volume.sharedProfile = profile;
            var target = new VisualTarget("volume", "volume-type", "Камера", "Volume", volume);
            VisualComponentAdapters.Populate(target, preset, new VisualProfile());
            runtimeProfile = volume.profile;
            Assert.That(runtimeProfile.TryGet<Bloom>(out var runtimeBloom), Is.True);
            var property = target.Properties.Single(p => p.Id == "post.bloom");
            property.Write(6f);
            Assert.That(runtimeBloom.intensity.overrideState, Is.True);
            property.RestoreCurrent();
            Assert.That(runtimeBloom.intensity.value, Is.EqualTo(1.5f));
            Assert.That(runtimeBloom.intensity.overrideState, Is.False, "Current must restore inherited blending, not just the same numeric value.");
            Assert.That(bloom.intensity.overrideState, Is.False, "Shared profile is never edited.");
        }
        finally
        {
            Object.DestroyImmediate(volumeObject);
            if (runtimeProfile != null) Object.DestroyImmediate(runtimeProfile);
            Object.DestroyImmediate(profile);
            Object.DestroyImmediate(preset);
        }
    }
}
#endif
