#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Subject42.Visuals;
using UnityEngine;
using UnityEditor;
using System.Linq;
using Object = UnityEngine.Object;

public sealed class LiveVisualProfileTests
{
    [Test]
    public void RuntimeProfileExportLoadPreservesSessionAndRejectsInvalidProfiles()
    {
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        string name = "Runtime profile round trip";
        var originalRuntime = LiveVisualRuntime.Instance;
        var authoredPreset = AssetDatabase.LoadAssetAtPath<VisualTuningPreset>(VisualTuningPresetStorage.AssetPath);
        Assert.That(authoredPreset, Is.Not.Null);
        var productionReference = authoredPreset.LiveProductionProfile;
        bool productionWasNull = productionReference == null;
        int productionReferenceId = new SerializedObject(authoredPreset)
            .FindProperty("LiveProductionProfile").objectReferenceInstanceIDValue;
        var authoredPaths = authoredPreset.LiveSources.Select(s => s.AssetPath)
            .Concat(authoredPreset.LiveHistoricalMaterials.Select(m => m.AssetPath).Where(p => p.StartsWith("Assets/") && !p.Contains("/Fonts/")))
            .Concat(new[] { VisualTuningPresetStorage.AssetPath,
                AssetDatabase.GetAssetPath(authoredPreset.LiveOriginalBaseline), AssetDatabase.GetAssetPath(productionReference) })
            .Where(p => !string.IsNullOrEmpty(p)).Distinct().ToArray();
        var authored = authoredPaths.ToDictionary(p => p, File.ReadAllBytes);
        var root = new GameObject(name);
        root.SetActive(false);
        var runtime = root.AddComponent<LiveVisualRuntime>();
        var preset = Object.Instantiate(authoredPreset);
        var localProduction = new TextAsset(JsonUtility.ToJson(new VisualProfile { Name = "Local production snapshot" }));
        try
        {
            // Exercise profile operations without scene discovery or replacing the global runtime.
            preset.LiveProductionProfile = localProduction;
            typeof(LiveVisualRuntime).GetField("<Preset>k__BackingField", fields).SetValue(runtime, preset);
            var sprite = root.AddComponent<SpriteRenderer>();
            var target = new VisualTarget("temporary", "test-type", "test-group", name, sprite);
            VisualComponentAdapters.Populate(target, preset, runtime.Original);
            runtime.Targets.Add(target);
            var alpha = target.Properties.Single(p => p.Id == "sprite.tint.a");
            var profile = new VisualProfile();
            profile.Set("Group", target.Group, alpha.Id, .7f);
            profile.Set("Type", target.TypeKey, alpha.Id, .4f);
            profile.Set("Instance", target.Key, alpha.Id, .1f);
            runtime.Load(profile);
            Assert.That(sprite.color.a, Is.EqualTo(.1f).Within(.0001f));
            var liveCustom = runtime.Custom;
            string liveJson = JsonUtility.ToJson(liveCustom);
            var mode = runtime.Mode;
            var saved = JsonUtility.FromJson<VisualProfile>(runtime.Export(name));
            saved.Validate();
            Assert.That(saved.Name, Is.EqualTo(name));
            Assert.That(saved.Values.Count, Is.EqualTo(2));
            Assert.That(saved.Values.Any(v => v.Scope == "Instance"), Is.False);
            Assert.That(saved.Resolve(target.Group, target.TypeKey, target.Key, alpha.Id, 1), Is.EqualTo(.4f));
            Assert.That(saved.Resolve(target.Group, "sibling-type", "other", alpha.Id, 1), Is.EqualTo(.7f));
            Assert.That(runtime.Custom, Is.SameAs(liveCustom));
            Assert.That(JsonUtility.ToJson(runtime.Custom), Is.EqualTo(liveJson));
            Assert.That(runtime.Mode, Is.EqualTo(mode));
            Assert.That(sprite.color.a, Is.EqualTo(.1f).Within(.0001f));

            runtime.Edit("Type", target, alpha, .9f);
            Assert.That(sprite.color.a, Is.EqualTo(.9f).Within(.0001f));
            runtime.Load(saved);
            Assert.That(sprite.color.a, Is.EqualTo(.4f).Within(.0001f));
            Assert.That(runtime.Mode, Is.EqualTo(VisualComparisonMode.Custom));
            Assert.That(JsonUtility.ToJson(runtime.Custom), Is.EqualTo(JsonUtility.ToJson(saved)));

            runtime.SetMode(VisualComparisonMode.Current);
            liveCustom = runtime.Custom;
            liveJson = JsonUtility.ToJson(liveCustom);
            float currentAlpha = sprite.color.a;
            int revision = runtime.Revision;
            bool canUndo = runtime.CanUndo, canRedo = runtime.CanRedo;
            var invalid = saved.Copy();
            invalid.Values.Add(invalid.Values[0].Copy());
            Assert.Throws<ArgumentException>(() => runtime.Load(invalid));
            Assert.That(runtime.Custom, Is.SameAs(liveCustom));
            Assert.That(JsonUtility.ToJson(runtime.Custom), Is.EqualTo(liveJson));
            Assert.That(runtime.Mode, Is.EqualTo(VisualComparisonMode.Current));
            Assert.That(runtime.Revision, Is.EqualTo(revision));
            Assert.That(runtime.CanUndo, Is.EqualTo(canUndo));
            Assert.That(runtime.CanRedo, Is.EqualTo(canRedo));
            Assert.That(sprite.color.a, Is.EqualTo(currentAlpha));
            Assert.That(localProduction != null, Is.True);
            Assert.That(preset.LiveProductionProfile == localProduction, Is.True);
            Assert.That(authoredPreset.LiveProductionProfile == null, Is.EqualTo(productionWasNull));
            Assert.That(authoredPreset.LiveProductionProfile == productionReference, Is.True);
            Assert.That(new SerializedObject(authoredPreset).FindProperty("LiveProductionProfile")
                .objectReferenceInstanceIDValue, Is.EqualTo(productionReferenceId));
            Assert.That(LiveVisualRuntime.Instance == originalRuntime, Is.True);
            foreach (var pair in authored)
                Assert.That(File.ReadAllBytes(pair.Key), Is.EqualTo(pair.Value), pair.Key + " changed during runtime profile operations.");
        }
        finally
        {
            // An always-inactive MonoBehaviour need not receive OnDestroy in Unity.
            foreach (var target in runtime.Targets) target.Release?.Invoke();
            runtime.Targets.Clear();
            Object.DestroyImmediate(root);
            Object.DestroyImmediate(preset);
            Object.DestroyImmediate(localProduction);
        }
    }

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
        // Check managed copying before JsonUtility can normalize inline serialized nulls.
        var nullableSources = new VisualProfile();
        nullableSources.MaterialSources.Add(new VisualMaterialSource
        { Target = "zombie", AssetPath = "Assets/Materials/Zombie.mat", ShaderName = "Sprites/Default" });
        nullableSources.MaterialSources.Add(null);
        nullableSources.MaterialSources.Add(new VisualMaterialSource
        { Target = "bomber", AssetPath = "Assets/Materials/Bomber.mat", ShaderName = "Universal Render Pipeline/2D/Sprite-Lit-Default" });
        Assert.That(nullableSources.MaterialSources[1], Is.Null);
        var nullableCopy = nullableSources.Copy();
        var nullableExport = nullableSources.ForProduction();
        Assert.That(nullableSources.MaterialSources[1], Is.Null, "Direct copying must preserve the source null.");
        Assert.That(nullableCopy.MaterialSources, Is.Not.SameAs(nullableExport.MaterialSources));
        foreach (var duplicate in new[] { nullableCopy, nullableExport })
        {
            Assert.That(duplicate.MaterialSources, Is.Not.SameAs(nullableSources.MaterialSources));
            Assert.That(duplicate.MaterialSources.Count, Is.EqualTo(3));
            Assert.That(duplicate.MaterialSources[1], Is.Null, "Direct Copy/ForProduction must preserve null entries.");
            foreach (int index in new[] { 0, 2 })
            {
                var source = nullableSources.MaterialSources[index];
                var copied = duplicate.MaterialSources[index];
                Assert.That(copied, Is.Not.Null);
                Assert.That(copied, Is.Not.SameAs(source));
                Assert.That(copied.Target, Is.EqualTo(source.Target));
                Assert.That(copied.AssetPath, Is.EqualTo(source.AssetPath));
                Assert.That(copied.ShaderName, Is.EqualTo(source.ShaderName));
                Assert.That(nullableCopy.MaterialSources[index], Is.Not.SameAs(nullableExport.MaterialSources[index]));
            }
        }

        // Exact JSON and mutation isolation use real sources only: inline Unity
        // serialization has a separate null representation from managed copying.
        var profile = new VisualProfile { Name = "Мой профиль", BaselineRevision = "19b6c01" };
        profile.Set("Type", "zombie", "shader._StudioSaturation", 0);
        profile.Set("Instance", "temporary", "sprite.alpha", .25f);
        profile.MaterialSources.Add(new VisualMaterialSource
        { Target = "zombie", AssetPath = "Assets/Materials/Zombie.mat", ShaderName = "Sprites/Default" });
        profile.MaterialSources.Add(new VisualMaterialSource
        { Target = "bomber", AssetPath = "Assets/Materials/Bomber.mat", ShaderName = "Universal Render Pipeline/2D/Sprite-Lit-Default" });
        profile.Notes.Add("Keep provenance and sparse values unchanged.");
        string originalJson = JsonUtility.ToJson(profile);
        var copy = profile.Copy();
        var exported = profile.ForProduction();
        var expectedExport = JsonUtility.FromJson<VisualProfile>(originalJson);
        expectedExport.Values.RemoveAll(value => value.Scope == "Instance");
        Assert.That(JsonUtility.ToJson(copy), Is.EqualTo(originalJson), "Copy must preserve the exact serialized contract and order.");
        Assert.That(JsonUtility.ToJson(exported), Is.EqualTo(JsonUtility.ToJson(expectedExport)));
        var restored = JsonUtility.FromJson<VisualProfile>(JsonUtility.ToJson(exported));
        restored.Validate();
        Assert.That(restored.Values.Count, Is.EqualTo(1));
        Assert.That(restored.Resolve("Враги", "zombie", "new spawn", "shader._StudioSaturation", 1), Is.Zero);
        Assert.That(profile.Values.Count, Is.EqualTo(2), "Export must not destroy the live instance override.");
        Assert.That(JsonUtility.ToJson(profile), Is.EqualTo(originalJson));
        Assert.That(restored.Notes, Is.EqualTo(profile.Notes));
        Assert.That(copy.MaterialSources, Is.Not.SameAs(profile.MaterialSources));
        Assert.That(exported.MaterialSources, Is.Not.SameAs(profile.MaterialSources));
        Assert.That(copy.MaterialSources, Is.Not.SameAs(exported.MaterialSources));
        foreach (var duplicate in new[] { copy, exported })
        {
            var sibling = ReferenceEquals(duplicate, copy) ? exported : copy;
            string siblingJson = JsonUtility.ToJson(sibling);
            Assert.That(duplicate.MaterialSources[0], Is.Not.SameAs(profile.MaterialSources[0]));
            Assert.That(duplicate.MaterialSources[1], Is.Not.SameAs(profile.MaterialSources[1]));
            duplicate.MaterialSources[0].Target = "changed target";
            duplicate.MaterialSources[0].AssetPath = "changed path";
            duplicate.MaterialSources[0].ShaderName = "changed shader";
            duplicate.Values[0].Value = .75f;
            duplicate.Notes.Add("copy-only note");
            Assert.That(JsonUtility.ToJson(profile), Is.EqualTo(originalJson), "Mutating either copy must not change its source.");
            Assert.That(JsonUtility.ToJson(sibling), Is.EqualTo(siblingJson), "Material sources, Values and Notes must be independent between copies in both directions.");
        }
        string copyJson = JsonUtility.ToJson(copy);
        string exportedJson = JsonUtility.ToJson(exported);
        profile.MaterialSources[1].Target = "source target";
        profile.MaterialSources[1].AssetPath = "source path";
        profile.MaterialSources[1].ShaderName = "source shader";
        profile.Values[0].Value = .5f;
        profile.Notes.Add("source-only note");
        Assert.That(JsonUtility.ToJson(copy), Is.EqualTo(copyJson));
        Assert.That(JsonUtility.ToJson(exported), Is.EqualTo(exportedJson));
        copy.MaterialSources[0].Target = "copy-only target";
        Assert.That(JsonUtility.ToJson(exported), Is.EqualTo(exportedJson), "Sibling copies must also be independent.");
        var absentSources = new VisualProfile { MaterialSources = null };
        Assert.That(absentSources.Copy().MaterialSources, Is.Empty);
        Assert.That(absentSources.ForProduction().MaterialSources, Is.Empty);
    }
}
#endif
