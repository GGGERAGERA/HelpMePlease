#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Subject42.Dev.VisualEffects;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class VisualEffectsLabTests
{
    private const string Folder = "Assets/_Project/Dev/Labs/VisualEffectsLab/";
    private const string SceneBackup = "Subject42.VisualEffectsLab.TestScene";
    [Serializable] private sealed class SceneRecord { public string Path; public bool Loaded, Active; }
    [Serializable] private sealed class EditorSetup { public SceneRecord[] Scenes; public float CaptureDelta; }

    [SetUp] public void PreserveScene()
    {
        if (EditorApplication.isPlaying) return; // UnityTest resumes SetUp after EnterPlayMode's domain reload.
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            Assert.That(scene.isDirty, Is.False, "Do not discard user scene edits: " + scene.path);
            if (scene.path.Length == 0) Assert.That(SceneManager.sceneCount, Is.EqualTo(1),
                "Save untitled scenes in a multi-scene setup before verification.");
        }
        SessionState.SetString(SceneBackup, JsonUtility.ToJson(new EditorSetup {
            CaptureDelta = Time.captureDeltaTime,
            Scenes = EditorSceneManager.GetSceneManagerSetup().Select(s => new SceneRecord {
                Path = s.path, Loaded = s.isLoaded, Active = s.isActive }).ToArray() }));
    }

    [UnityTearDown] public IEnumerator Cleanup()
    {
        if (EditorApplication.isPlaying) yield return new ExitPlayMode();
        string saved = SessionState.GetString(SceneBackup, "");
        SessionState.EraseString(SceneBackup);
        if (saved.Length == 0) yield break;
        var setup = JsonUtility.FromJson<EditorSetup>(saved);
        Time.captureDeltaTime = setup.CaptureDelta;
        if (setup.Scenes.Length == 0 || setup.Scenes.Length == 1 && setup.Scenes[0].Path.Length == 0)
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        else EditorSceneManager.RestoreSceneManagerSetup(setup.Scenes.Select(s => new SceneSetup {
            path = s.Path, isLoaded = s.Loaded, isActive = s.Active }).ToArray());
    }

    [Test]
    public void CatalogAndScenesAreAuthoredOutsideProductionBuild()
    {
        Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(Folder + "EffectsLab.unity"), Is.Not.Null,
            "The requested effect catalog scene must be authored and openable.");
        Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(Folder + "ShaderLab.unity"), Is.Not.Null,
            "Shader experiments need their separate authored scene.");
        Assert.That(AssetDatabase.LoadMainAssetAtPath(Folder + "EffectsCatalog.asset"), Is.Not.Null);
        Assert.That(EditorBuildSettings.scenes.Any(s => s.path.StartsWith(Folder)), Is.False,
            "Developer galleries must not enter the production scene list.");
    }

    [Test]
    public void CatalogCoversNativeAndDedicatedEffectsAndRecordsRealCallers()
    {
        var catalog = AssetDatabase.LoadAssetAtPath<EffectLabCatalog>(EffectLabCatalog.CatalogPath);
        Assert.That(catalog, Is.Not.Null);
        Assert.That(catalog.CoveredPrefabs, Is.GreaterThan(1100));
        Assert.That(catalog.Effects.Select(e => e.Id).Distinct().Count(), Is.EqualTo(catalog.Effects.Length));
        var covered = catalog.Effects.Select(e => e.SourceAssetPath).ToHashSet();
        string[] folders = { "Assets/_Project/prefabs/fx/", "Assets/Epic Toon FX/Prefabs/", "Assets/JMO Assets/Cartoon FX Remaster/CFXR Prefabs/" };
        foreach (string path in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets" }).Select(AssetDatabase.GUIDToAssetPath).Where(p => p.EndsWith(".prefab")))
        {
            // Independent on-disk coverage check avoids loading unrelated legacy prefab references.
            string yaml = File.ReadAllText(path);
            bool native = yaml.Contains("--- !u!198 &") || yaml.Contains("--- !u!96 &") || yaml.Contains("--- !u!120 &");
            if (native || folders.Any(path.StartsWith)) Assert.That(covered.Contains(path), Is.True, "Missing FX source: " + path);
        }
        Assert.That(catalog.Effects.Any(e => e.TransformIndices.Length > 0), Is.True, "Embedded effects must retain subtree identity.");
        Assert.That(catalog.Effects.Any(e => e.Callers.Any(c => c.ComponentType == "EnemyHealthPresentation" && c.PropertyPath == "bloodHitPrefab")), Is.True);
        Assert.That(catalog.Effects.Count(e => e.Demo != EffectLabDemo.Asset), Is.EqualTo(8));
        foreach (var caller in catalog.Effects.SelectMany(e => e.Callers).Where(c => c.ScriptPath.Length > 0))
            Assert.That(AssetDatabase.LoadAssetAtPath<MonoScript>(caller.ScriptPath), Is.Not.Null, caller.ScriptPath);
    }

    [UnityTest, Timeout(180000)]
    public IEnumerator SelectedEffectsReplayWithVisualSourcesAndNoGameplay()
    {
        BunkerDebugTools.SetGameView1600();
        EditorSceneManager.OpenScene(EffectLabCatalog.EffectsScenePath);
        yield return new EnterPlayMode();
        Time.captureDeltaTime = 1f / 60f;
        yield return null;
        var lab = Object.FindFirstObjectByType<EffectLabController>();
        Assert.That(lab, Is.Not.Null); lab.Loop = false;
        lab.SearchQuery = "PF_KineticImpact_Prototype";
        Assert.That(lab.FilteredCount, Is.EqualTo(1));
        Assert.That(lab.Current.SourceAssetPath.EndsWith("PF_KineticImpact_Prototype.prefab"), Is.True);
        lab.SearchQuery = "__no_such_effect__";
        Assert.That(lab.FilteredCount, Is.Zero); Assert.That(lab.EffectInstance, Is.Null);
        lab.SearchQuery = "";
        int Index(string suffix) => System.Array.FindIndex(lab.Catalog.Effects, e => e.SourceAssetPath.EndsWith(suffix) && e.Demo == EffectLabDemo.Asset);
        int impact = Index("PF_KineticImpact_Prototype.prefab");
        lab.SelectEffect(impact); yield return null;
        Assert.That(lab.LiveParticleCount, Is.GreaterThan(0));
        string output = EffectLabCatalog.Output; Directory.CreateDirectory(output);
        ScreenCapture.CaptureScreenshot(output + "/effects-impact-a.png");
        for (int i = 0; i < 5; i++) yield return null;
        ScreenCapture.CaptureScreenshot(output + "/effects-impact-b.png");
        yield return null;
        int paired = System.Array.FindIndex(lab.Catalog.Effects, e => e.Demo == EffectLabDemo.Asset &&
            e.Callers.Any(c => c.ComponentType == "EnemyHealthPresentation" && c.PropertyPath == "bloodHitPrefab"));
        lab.SelectEffect(paired); yield return null;
        Assert.That(lab.CallerInstance, Is.Not.Null);
        Assert.That(lab.CallerInstance.GetComponentInChildren<EnemyHealth>(true), Is.Null);
        Assert.That(lab.CallerInstance.GetComponentsInChildren<Collider2D>(true).Any(c => c.enabled), Is.False);
        ScreenCapture.CaptureScreenshot(output + "/effects-source.png"); yield return null;
        foreach (string library in new[] { "Cartoon FX Remaster", "Epic Toon FX" })
        {
            int index = System.Array.FindIndex(lab.Catalog.Effects, e => e.Library == library && e.Kind == EffectLabKind.Particles);
            lab.SelectEffect(index); yield return null;
            Assert.That(lab.EffectInstance.activeInHierarchy, Is.True, library);
            Assert.That(lab.LiveParticleCount, Is.GreaterThan(0), library);
            lab.Replay(); yield return null;
            Assert.That(lab.EffectInstance, Is.Not.Null, "Replay must restart a fresh visual instance.");
            ScreenCapture.CaptureScreenshot(output + "/effects-" + (library.StartsWith("Cartoon") ? "cfxr" : "epic") + ".png");
            yield return null;
        }
        int embedded = System.Array.FindIndex(lab.Catalog.Effects, e => e.Demo == EffectLabDemo.Asset && e.TransformIndices.Length > 0);
        lab.SelectEffect(embedded); yield return null;
        Assert.That(lab.EffectInstance, Is.Not.Null);
        int ownedEmbedded = System.Array.FindIndex(lab.Catalog.Effects, e => e.Demo == EffectLabDemo.Asset && e.TransformIndices.Length > 0 && e.Callers.Length == 0);
        lab.SelectEffect(ownedEmbedded); yield return null;
        Assert.That(lab.CallerInstance, Is.Not.Null, "Embedded FX show their known owner even without a serialized caller.");
        foreach (string suffix in new[] { "p_FxBloodAttacked1.prefab", "CFXR4 Firework HDR Shoot Single (Random Color).prefab" })
        {
            lab.SelectEffect(Index(suffix));
            var deathSubEmitters = lab.EffectInstance.GetComponentsInChildren<ParticleSystem>(true)
                .Where(p => p.subEmitters.enabled)
                .SelectMany(p => Enumerable.Range(0, p.subEmitters.subEmittersCount)
                    .Where(i => p.subEmitters.GetSubEmitterType(i) == ParticleSystemSubEmitterType.Death)
                    .Select(i => p.subEmitters.GetSubEmitterSystem(i))).Where(p => p != null).Distinct().ToArray();
            Assert.That(deathSubEmitters, Is.Not.Empty, suffix);
            var authored = AssetDatabase.LoadAssetAtPath<GameObject>(lab.Current.SourceAssetPath);
            foreach (var subEmitter in deathSubEmitters)
            {
                string path = AnimationUtility.CalculateTransformPath(subEmitter.transform, lab.EffectInstance.transform);
                var original = authored.transform.Find(path).GetComponent<ParticleSystem>();
                Assert.That(subEmitter.main.playOnAwake, Is.EqualTo(original.main.playOnAwake), "Keep authored flags: " + suffix);
                if (!original.main.playOnAwake) Assert.That(subEmitter.isPlaying, Is.False, "Death sub-emitter must wait for its event: " + suffix);
            }
            yield return null;
        }
        foreach (var demo in lab.Catalog.Effects.Where(e => e.Demo != EffectLabDemo.Asset).ToArray())
        {
            lab.SelectEffect(System.Array.IndexOf(lab.Catalog.Effects, demo));
            for (int i = 0; i < 4; i++) yield return null;
            Assert.That(lab.EffectInstance, Is.Not.Null, demo.DisplayName);
            lab.Replay(); yield return null;
            ScreenCapture.CaptureScreenshot(output + "/effects-" + demo.Demo + ".png"); yield return null;
        }
        Assert.That(Object.FindFirstObjectByType<EnemySpawner>(), Is.Null);
        Assert.That(Object.FindFirstObjectByType<RunStateManager>(), Is.Null);
        Assert.That(Object.FindFirstObjectByType<PlayerHealth>(), Is.Null);
        lab.StopPreview(); Assert.That(lab.EffectInstance, Is.Null);
    }

    [UnityTest, Timeout(120000)]
    public IEnumerator ShaderGalleryAnimatesWithoutMutatingMaterials()
    {
        BunkerDebugTools.SetGameView1600();
        EditorSceneManager.OpenScene(EffectLabCatalog.ShaderScenePath);
        yield return new EnterPlayMode();
        Time.captureDeltaTime = 1f / 60f;
        yield return null;
        var lab = Object.FindFirstObjectByType<ShaderLabController>();
        Assert.That(lab != null, Is.True, "A live scene-owned ShaderLabController is required.");
        Assert.That(lab.Samples, Is.Not.Null, "Sample enumeration must be readable before the first Update.");
        int sampleCount = lab.Samples.Count();
        Assert.That(sampleCount, Is.EqualTo(26));
        // Captured scene locals in this iterator do not survive EnterPlayMode's domain reload.
        float readyDeadline = Time.realtimeSinceStartup + 5;
        while (lab.AnimationTime <= 0 && Time.realtimeSinceStartup < readyDeadline) yield return null;
        Assert.That(lab.AnimationTime, Is.GreaterThan(0), "The scene must have completed its first Update.");
        Assert.That(lab.PageCount, Is.EqualTo(4));
        Assert.That(lab.SelectedPage, Is.EqualTo(2), "Open on the new creature studies.");
        var pageSizes = new int[lab.PageCount];
        for (int page = 0; page < pageSizes.Length; page++) pageSizes[page] = lab.GetPageSamples(page).Length;
        Assert.That(pageSizes, Is.EqualTo(new[] { 12, 2, 6, 6 }));
        foreach (var sample in lab.Samples)
        {
            Assert.That(ShaderUtil.ShaderHasError(sample.sharedMaterial.shader), Is.False, sample.name);
            Assert.That(sample.sharedMaterial.GetFloat("_LabTime"), Is.Zero);
        }
        lab.SelectPage(0); yield return null; yield return null;
        foreach (var sample in lab.Samples.Take(12))
        { Assert.That(sample.gameObject.activeInHierarchy, Is.True); Assert.That(sample.isVisible, Is.True, sample.name); }
        foreach (var sample in lab.Samples.Skip(12)) Assert.That(sample.gameObject.activeInHierarchy, Is.False);
        float initial = lab.AnimationTime;
        Directory.CreateDirectory(EffectLabCatalog.Output);
        ScreenCapture.CaptureScreenshot(EffectLabCatalog.Output + "/shaders-a.png");
        yield return new WaitForSecondsRealtime(.65f);
        Assert.That(lab.AnimationTime - initial, Is.GreaterThan(.5f));
        ScreenCapture.CaptureScreenshot(EffectLabCatalog.Output + "/shaders-b.png");
        yield return null;

        lab.SelectPage(1); lab.ResetAnimation();
        yield return new WaitForSecondsRealtime(.65f);
        foreach (var sample in lab.Samples.Take(12)) Assert.That(sample.gameObject.activeInHierarchy, Is.False);
        var block = new MaterialPropertyBlock();
        foreach (var sample in lab.GetPageSamples(1))
        {
            Assert.That(sample.isVisible, Is.True, sample.name);
            Assert.That(sample.sharedMaterial.shader.name, Is.EqualTo("Subject42/Dev/EnvironmentExperiments"));
            Assert.That(sample.sharedMaterial.GetTexture("_FloorTex"), Is.Not.Null);
            Assert.That(sample.sharedMaterial.GetTexture("_ActorTex"), Is.Not.Null);
            sample.GetPropertyBlock(block); Assert.That(block.GetFloat("_LabTime"), Is.GreaterThan(.5f));
        }
        ScreenCapture.CaptureScreenshot(EffectLabCatalog.Output + "/environment-a.png");
        yield return new WaitForSecondsRealtime(.65f);
        ScreenCapture.CaptureScreenshot(EffectLabCatalog.Output + "/environment-b.png");
        yield return null;
        lab.Paused = true; float pausedTime = lab.AnimationTime;
        yield return new WaitForSecondsRealtime(.15f);
        Assert.That(lab.AnimationTime, Is.EqualTo(pausedTime));
        lab.Intensity = 0;
        yield return null;
        foreach (var sample in lab.GetPageSamples(1))
        { sample.GetPropertyBlock(block); Assert.That(block.GetFloat("_Intensity"), Is.Zero); }
        ScreenCapture.CaptureScreenshot(EffectLabCatalog.Output + "/environment-baseline.png"); yield return null;
        for (int page = 2; page < lab.PageCount; page++)
        {
            lab.SelectPage(page); lab.Intensity = 1; lab.ResetAnimation(); lab.Paused = false;
            yield return new WaitForSecondsRealtime(.65f);
            for (int other = 0; other < lab.PageCount; other++)
                foreach (var sample in lab.GetPageSamples(other))
                    Assert.That(sample.gameObject.activeInHierarchy, Is.EqualTo(other == page), sample.name);
            foreach (var sample in lab.GetPageSamples(page))
            {
                Assert.That(sample.isVisible, Is.True, sample.name);
                sample.GetPropertyBlock(block); Assert.That(block.GetFloat("_LabTime"), Is.GreaterThan(.5f));
            }
            string prefix = EffectLabCatalog.Output + (page == 2 ? "/creatures" : "/zones");
            ScreenCapture.CaptureScreenshot(prefix + "-a.png");
            yield return new WaitForSecondsRealtime(.65f);
            ScreenCapture.CaptureScreenshot(prefix + "-b.png"); yield return null;
            lab.Paused = true; lab.Intensity = 0; yield return null;
            ScreenCapture.CaptureScreenshot(prefix + "-baseline.png"); yield return null;
        }
        lab.Intensity = 1; lab.ResetAnimation(); lab.Paused = false;
        lab.SelectPage(0); yield return null;
        lab.SelectPage(1); yield return null;
        foreach (var sample in lab.Samples)
        {
            Assert.That(sample.sharedMaterial.GetFloat("_LabTime"), Is.Zero);
            Assert.That(sample.sharedMaterial.GetFloat("_Intensity"), Is.EqualTo(1));
        }
    }
}
#endif
