#if UNITY_EDITOR
using System;
using NUnit.Framework;
using Subject42.Visuals;
using UnityEngine;
using UnityEditor;
using System.Linq;
using Object = UnityEngine.Object;

public sealed class LiveVisualProfileTests
{
    [Test]
    public void ProductionCatalogReferencesActualPrefabRootsAndHistoricalMaterials()
    {
        var preset = AssetDatabase.LoadAssetAtPath<VisualTuningPreset>(VisualTuningPresetStorage.AssetPath);
        var baseline = JsonUtility.FromJson<VisualProfile>(preset.LiveOriginalBaseline.text);
        baseline.Validate();
        Assert.That(baseline.Values.Count, Is.GreaterThan(7000));
        foreach (var source in preset.LiveSources)
        {
            Assert.That(source.Prefab, Is.SameAs(AssetDatabase.LoadAssetAtPath<GameObject>(source.AssetPath)), source.AssetPath);
            Assert.That(source.Prefab.transform.parent, Is.Null, "Catalog must bind prefab roots, not a renderer child.");
        }
        foreach (var source in baseline.MaterialSources)
            Assert.That(preset.ResolveLiveMaterial(source.AssetPath), Is.Not.Null, source.AssetPath);
    }
    [Test]
    public void PixelCorrectionPreservesTemporaryCombatFlashMaterial()
    {
        var actor = new GameObject("Visual material ownership");
        var preset = ScriptableObject.CreateInstance<VisualTuningPreset>();
        var sprite = actor.AddComponent<SpriteRenderer>();
        var body = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/World/EnemyBodyReadabilityLit.mat");
        var flash = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Shaders/M_WhiteFlash.mat");
        sprite.sharedMaterial = body;
        preset.LiveSpriteShader = Shader.Find("Subject42/Environment Tile Lit");
        var target = new VisualTarget("body", "body-type", "Враги", "Body", sprite);
        try
        {
            VisualComponentAdapters.Populate(target, preset, new VisualProfile());
            var saturation = target.Properties.Single(p => p.Id == "shader._StudioSaturation");
            saturation.Write(0);
            Assert.That(sprite.sharedMaterial, Is.Not.SameAs(body));
            sprite.sharedMaterial = flash;
            target.Maintain?.Invoke(); saturation.Write(3);
            Assert.That(sprite.sharedMaterial, Is.SameAs(flash), "Live reapplication cannot erase the visible damage flash.");
            sprite.sharedMaterial = body;
            saturation.Write(3);
            Assert.That(sprite.sharedMaterial, Is.Not.SameAs(body), "The live body override resumes after the flash.");
        }
        finally
        {
            target.Release?.Invoke(); Object.DestroyImmediate(actor); Object.DestroyImmediate(preset);
        }
    }
    [Test]
    public void InstanceWinsOverTypeAndGroupAndDoesNotLeakToSibling()
    {
        var profile = new VisualProfile();
        profile.Set("Group", "Враги", "sprite.alpha", .7f);
        profile.Set("Type", "bomber", "sprite.alpha", .4f);
        profile.Set("Instance", "one", "sprite.alpha", .1f);
        Assert.That(profile.Resolve("Враги", "bomber", "one", "sprite.alpha", 1), Is.EqualTo(.1f));
        Assert.That(profile.Resolve("Враги", "bomber", "two", "sprite.alpha", 1), Is.EqualTo(.4f));
        Assert.That(profile.Resolve("Враги", "zombie", "three", "sprite.alpha", 1), Is.EqualTo(.7f));
    }

    [Test]
    public void ImportRejectsNonFiniteDuplicateOrUnknownScopeWithoutChangingSession()
    {
        var profile = new VisualProfile();
        Assert.Throws<ArgumentException>(() => profile.Set("Type", "zombie", "light.intensity", float.NaN));
        Assert.Throws<ArgumentException>(() => profile.Set("Type", "zombie", "light.intensity", float.PositiveInfinity));
        Assert.Throws<ArgumentException>(() => profile.Set("Prefab", "zombie", "light.intensity", 2));
        Assert.That(profile.Values, Is.Empty);
        profile.Values.Add(new VisualOverride { Scope = "Type", Target = "one", Property = "x", Value = 1 });
        profile.Values.Add(new VisualOverride { Scope = "Type", Target = "one", Property = "x", Value = 2 });
        Assert.Throws<ArgumentException>(() => profile.Validate());
    }

    [Test]
    public void ProductionSerializationExcludesInstanceOverridesAndRoundTripsTypeDiffs()
    {
        var profile = new VisualProfile { Name = "Мой профиль", BaselineRevision = "19b6c01" };
        profile.Set("Type", "zombie", "shader._StudioSaturation", 0);
        profile.Set("Instance", "temporary", "sprite.alpha", .25f);
        var exported = profile.ForProduction();
        var restored = JsonUtility.FromJson<VisualProfile>(JsonUtility.ToJson(exported));
        restored.Validate();
        Assert.That(restored.Values.Count, Is.EqualTo(1));
        Assert.That(restored.Resolve("Враги", "zombie", "new spawn", "shader._StudioSaturation", 1), Is.Zero);
        Assert.That(profile.Values.Count, Is.EqualTo(2), "Export must not destroy the live instance override.");
    }
}
#endif
