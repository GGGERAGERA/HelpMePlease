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
        Assert.That(target.Properties.Any(p => p.Id.StartsWith("config.Ring") || p.Id.StartsWith("config.Tier")), Is.False,
            "Saved ring appearance remains compatible without becoming Studio controls.");
        Object.DestroyImmediate(source);
    }

    [Test]
    public void SavedRingAppearanceSurvivesImportExportComparisonResetAndDestruction()
    {
        var source = ScriptableObject.CreateInstance<OrbitalPresentationConfig>();
        OrbitalPresentationConfig.Configure(source);
        var config = OrbitalPresentationConfig.Active;
        var host = new GameObject("Saved ring profile compatibility");
        try
        {
            var runtime = host.AddComponent<LiveVisualRuntime>();
            runtime.enabled = false;
            runtime.Targets.Add(OrbitalVisualAdapter.CreateTarget());
            runtime.Original.Set("Type", "config:orbital", "config.RingSurface.Glow", .25f);
            var saved = new VisualProfile();
            saved.Set("Group", "ORBITAL", "config.RingSurface.Glow", .5f);
            saved.Set("Type", "config:orbital", "config.RingSurface.Glow", 2f);
            saved.Set("Instance", "config:orbital", "config.RingSurface.Glow", 3f);
            saved.Set("Type", "config:orbital", "config.RingLineWidth", .12f);
            saved.Set("Type", "config:orbital", "config.RingSurface.Pattern", 1.6f);
            saved.Set("Type", "config:orbital", "config.RingSurface.DashCount", 900f);
            saved.Set("Type", "config:orbital", "config.RingSurface.PatternSpeed", -99f);
            runtime.Load(saved);
            Assert.That(runtime.OriginalMatchedCount, Is.EqualTo(1), "Imported ring IDs still count as an applied baseline contract.");
            Assert.That(config.RingSurface.Glow, Is.EqualTo(3f));
            Assert.That(config.RingLineWidth, Is.EqualTo(.12f));
            Assert.That(config.RingSurface.Pattern, Is.EqualTo(OrbitalPresentationConfig.RingPattern.Energy));
            Assert.That(config.RingSurface.DashCount, Is.EqualTo(512));
            Assert.That(config.RingSurface.PatternSpeed, Is.EqualTo(-10f));

            var exported = JsonUtility.FromJson<VisualProfile>(runtime.Export("Saved appearance"));
            Assert.That(exported.Values.Count, Is.EqualTo(6));
            Assert.That(exported.Values.Any(v => v.Scope == "Instance"), Is.False);
            Assert.That(exported.TryGet("Type", "config:orbital", "config.RingSurface.DashCount", out float rawDashCount), Is.True);
            Assert.That(rawDashCount, Is.EqualTo(900f), "Import/export must retain the saved data, including values sanitized only on application.");
            runtime.Load(exported);
            Assert.That(config.RingSurface.Glow, Is.EqualTo(2f), "Production round-trip keeps Type over Group.");
            config.RingSurface.Saturation = 1.75f; // A laboratory edits an unrelated value.
            runtime.SetMode(VisualComparisonMode.Current);
            Assert.That(config.RingSurface.Glow, Is.EqualTo(1f));
            Assert.That(config.RingSurface.Saturation, Is.EqualTo(1.75f), "Restoration affects only overridden fields.");
            Assert.That(config.RingLineWidth, Is.EqualTo(.045f));
            runtime.SetMode(VisualComparisonMode.Original);
            Assert.That(config.RingSurface.Glow, Is.EqualTo(.25f));
            runtime.SetMode(VisualComparisonMode.Custom);
            Assert.That(config.RingSurface.Glow, Is.EqualTo(2f));
            runtime.Reset("Type");
            Assert.That(config.RingSurface.Glow, Is.EqualTo(1f));
            runtime.Undo();
            Assert.That(config.RingSurface.Glow, Is.EqualTo(2f));
            Object.DestroyImmediate(host);
            Assert.That(config.RingSurface.Glow, Is.EqualTo(1f));
            Assert.That(config.RingLineWidth, Is.EqualTo(.045f));
            Assert.That(source.RingSurface.Glow, Is.EqualTo(1f), "Authored presentation is never modified.");
        }
        finally
        {
            if (host != null) Object.DestroyImmediate(host);
            Object.DestroyImmediate(source);
        }
    }

    [TestCase("I", 1)]
    [TestCase("II", 2)]
    [TestCase("III", 3)]
    [TestCase("IV", 4)]
    public void SavedTierAppearanceAppliesWithoutExposingTierControls(string tierId, int tier)
    {
        var source = ScriptableObject.CreateInstance<OrbitalPresentationConfig>();
        OrbitalPresentationConfig.Configure(source);
        var config = OrbitalPresentationConfig.Active;
        var original = config.GetRingTier(tier);
        var host = new GameObject("Saved tier profile compatibility");
        try
        {
            var runtime = host.AddComponent<LiveVisualRuntime>();
            runtime.enabled = false;
            var profile = new VisualProfile();
            string id = "config.Tier" + tierId;
            profile.Set("Type", "config:orbital", id + ".BaseColor.r", 4f);
            profile.Set("Type", "config:orbital", id + ".BaseColor.a", 2f);
            profile.Set("Type", "config:orbital", id + ".Alpha", .3f);
            profile.Set("Type", "config:orbital", id + ".Emission", 40f);
            profile.Set("Type", "config:orbital", id + ".Glow", 5f);
            profile.Set("Type", "config:orbital", id + ".CoreWhitening", .4f);
            profile.Set("Type", "config:orbital", id + ".PulseStrength", .5f);
            profile.Set("Type", "config:orbital", id + ".PulseSpeed", 25f);
            runtime.Load(profile);
            var appearance = config.GetRingTier(tier);
            Assert.That(appearance.BaseColor.r, Is.EqualTo(4f));
            Assert.That(appearance.BaseColor.a, Is.EqualTo(1f));
            Assert.That(appearance.Alpha, Is.EqualTo(.3f));
            Assert.That(appearance.Emission, Is.EqualTo(32f));
            Assert.That(appearance.Glow, Is.EqualTo(5f));
            Assert.That(appearance.CoreWhitening, Is.EqualTo(.4f));
            Assert.That(appearance.PulseStrength, Is.EqualTo(.5f));
            Assert.That(appearance.PulseSpeed, Is.EqualTo(20f));
            runtime.SetMode(VisualComparisonMode.Current);
            Assert.That(config.GetRingTier(tier), Is.EqualTo(original));
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(source);
        }
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
