#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

[Category("Authoring")]
public sealed class ProductionBoundaryAuthoringTests
{
    private string fixtureRoot;

    [SetUp]
    public void CreateFixtureFolder()
    {
        // Existing InitTestScene*.unity* ignore rules also cover this folder/meta.
        string name = "InitTestSceneIntegrity_" + Guid.NewGuid().ToString("N") + ".unity-fixtures";
        fixtureRoot = "Assets/" + name;
        AssetDatabase.CreateFolder("Assets", name);
    }

    [TearDown]
    public void RemoveFixtureFolder()
    {
        if (!string.IsNullOrEmpty(fixtureRoot)) AssetDatabase.DeleteAsset(fixtureRoot);
    }

    [Test]
    public void ProductionAssetsHaveNoDevDependenciesOrMissingReferences()
    {
        string[] roots = Subject42AssetReferenceValidator.GetProductionRoots();
        string[] paths = Subject42AssetReferenceValidator.GetInspectionPaths(roots);
        var report = Subject42AssetReferenceValidator.Validate(roots);
        string output = Path.GetFullPath("Artifacts/GeneratedQA/Phase7/ProductionBoundary");
        Directory.CreateDirectory(output);
        File.WriteAllLines(Path.Combine(output, "production-roots.txt"), roots);
        File.WriteAllLines(Path.Combine(output, "inspected-assets.txt"), paths);
        File.WriteAllText(Path.Combine(output, "validation.txt"), report.ErrorCount == 0
            ? "PASS: no Dev dependencies, missing scripts, or broken serialized references.\n"
            : report.FormatErrors());
        Assert.That(roots, Is.Not.Empty, "Production asset discovery must not silently scan nothing.");
        Assert.That(report.ErrorCount, Is.Zero, report.FormatErrors());
    }

    [Test]
    public void IntentionalNullsAreAcceptedInScenesPrefabsAndConfigs()
    {
        string prefab = SaveRendererPrefab(null);
        string scene = SaveRendererScene(null);
        var config = ScriptableObject.CreateInstance<CharacterData>();
        string configPath = fixtureRoot + "/Optional.asset";
        AssetDatabase.CreateAsset(config, configPath);
        Assert.That(Subject42AssetReferenceValidator.Validate(new[] { prefab, scene, configPath }).ErrorCount,
            Is.Zero, "Unset fields are not broken references.");
    }

    [Test]
    public void AssetDiskPathsResolveProjectAndInstalledPackageFiles()
    {
        string prefab = SaveRendererPrefab(null);
        Assert.That(Subject42AssetReferenceValidator.GetPhysicalAssetPath(prefab),
            Is.EqualTo(Path.GetFullPath(prefab)));
        var package = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages()
            .FirstOrDefault(entry => !string.IsNullOrEmpty(entry.resolvedPath) &&
                !string.IsNullOrEmpty(entry.assetPath) && File.Exists(Path.Combine(entry.resolvedPath, "package.json")));
        Assert.That(package, Is.Not.Null, "The EditMode test framework requires an installed package.");
        string logicalPath = package.assetPath + "/package.json";
        Assert.That(Subject42AssetReferenceValidator.GetPhysicalAssetPath(logicalPath),
            Is.EqualTo(Path.GetFullPath(Path.Combine(package.resolvedPath, "package.json"))));
        Assert.That(File.Exists(Subject42AssetReferenceValidator.GetPhysicalAssetPath(logicalPath)), Is.True);
    }

    [Test]
    public void DeletedSpriteIsReportedInScenePrefabAndConfig()
    {
        string spritePath = fixtureRoot + "/Sprite.png";
        var texture = new Texture2D(2, 2);
        try { File.WriteAllBytes(spritePath, texture.EncodeToPNG()); }
        finally { Object.DestroyImmediate(texture); }
        AssetDatabase.ImportAsset(spritePath, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(spritePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.SaveAndReimport();
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
        Assert.That(sprite, Is.Not.Null);
        string prefab = SaveRendererPrefab(sprite);
        string scene = SaveRendererScene(sprite);
        string configPath = fixtureRoot + "/Character.asset";
        var config = ScriptableObject.CreateInstance<CharacterData>();
        config.portrait = sprite;
        AssetDatabase.CreateAsset(config, configPath);
        AssetDatabase.SaveAssetIfDirty(config);
        AssetDatabase.DeleteAsset(spritePath);
        var report = Subject42AssetReferenceValidator.Validate(new[] { prefab, scene, configPath });
        foreach (string path in new[] { prefab, scene, configPath })
            Assert.That(report.Issues.Any(issue => issue.Code == "BROKEN_ASSET_GUID" &&
                issue.Message.Contains(path)), Is.True, report.FormatErrors());
    }

    [Test]
    public void ExistingGuidWithInvalidSubassetIdIsBroken()
    {
        string materialPath = fixtureRoot + "/Material.mat";
        var material = new Material(Shader.Find("Sprites/Default"));
        AssetDatabase.CreateAsset(material, materialPath);
        string prefab = SaveRendererPrefab(null, material);
        string text = File.ReadAllText(prefab);
        string guid = AssetDatabase.AssetPathToGUID(materialPath);
        text = Regex.Replace(text, @"\{fileID: \d+, guid: " + guid + @", type: 2\}",
            "{fileID: 987654321, guid: " + guid + ", type: 2}");
        File.WriteAllText(prefab, text);
        AssetDatabase.ImportAsset(prefab, ImportAssetOptions.ForceSynchronousImport);
        var report = Subject42AssetReferenceValidator.Validate(new[] { prefab });
        Assert.That(report.Issues.Any(issue => issue.Code == "BROKEN_FILE_ID"), Is.True, report.FormatErrors());
    }

    [Test]
    public void MissingScriptAndDanglingLocalReferenceAreReported()
    {
        string prefab = SaveRendererPrefab(null);
        string text = Regex.Replace(File.ReadAllText(prefab), @"m_Script: \{[^}]*\}", "m_Script: {fileID: 0}");
        text = text.Replace("m_Sprite: {fileID: 0}", "m_Sprite: {fileID: 987654321}");
        File.WriteAllText(prefab, text);
        LogAssert.Expect(LogType.Error, new Regex(@"Broken text PPtr in file\(.*\). Local file identifier \(987654321\) doesn't exist!"));
        AssetDatabase.ImportAsset(prefab, ImportAssetOptions.ForceSynchronousImport);
        var report = Subject42AssetReferenceValidator.Validate(new[] { prefab });
        Assert.That(report.Issues.Any(issue => issue.Code == "MISSING_SCRIPT"), Is.True, report.FormatErrors());
        Assert.That(report.Issues.Any(issue => issue.Code == "BROKEN_LOCAL_FILE_ID"), Is.True, report.FormatErrors());
    }

    [Test]
    public void InspectionPreservesOpenDirtyUnsavedScenes()
    {
        string scenePath = SaveRendererScene(null);
        // A preview avoids NewScene(Additive)'s restriction when the user already has an untitled scene.
        Scene unsaved = EditorSceneManager.NewPreviewScene();
        try
        {
            var marker = new GameObject("Unsaved integrity marker");
            SceneManager.MoveGameObjectToScene(marker, unsaved);
            EditorSceneManager.MarkSceneDirty(unsaved);
            var handles = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(index => SceneManager.GetSceneAt(index).handle).ToArray();
            var dirty = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(index => SceneManager.GetSceneAt(index).isDirty).ToArray();
            var paths = Enumerable.Range(0, SceneManager.sceneCount)
                .Select(index => SceneManager.GetSceneAt(index).path).ToArray();
            var activeHandle = SceneManager.GetActiveScene().handle;
            var report = Subject42AssetReferenceValidator.Validate(new[] { scenePath });
            Assert.That(report.ErrorCount, Is.Zero, report.FormatErrors());
            Assert.That(Enumerable.Range(0, SceneManager.sceneCount)
                .Select(index => SceneManager.GetSceneAt(index).handle), Is.EqualTo(handles));
            Assert.That(Enumerable.Range(0, SceneManager.sceneCount)
                .Select(index => SceneManager.GetSceneAt(index).isDirty), Is.EqualTo(dirty));
            Assert.That(Enumerable.Range(0, SceneManager.sceneCount)
                .Select(index => SceneManager.GetSceneAt(index).path), Is.EqualTo(paths));
            Assert.That(unsaved.isDirty, Is.True);
            Assert.That(unsaved.path, Is.Empty);
            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.scene.handle, Is.EqualTo(unsaved.handle));
            Assert.That(SceneManager.GetActiveScene().handle, Is.EqualTo(activeHandle));
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(unsaved);
        }
    }

    [TestCase("Dark")]
    [TestCase("Blue")]
    [TestCase("BigGait")]
    public void ProductionSpiderAnimationsHaveProductionOwnership(string variant)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/Enemies/p_Enemy_Spider" + variant + ".prefab");
        var controller = prefab.GetComponentInChildren<Animator>(true).runtimeAnimatorController;
        Assert.That(controller, Is.Not.Null);
        Assert.That(Subject42AssetReferenceValidator.IsDevPath(AssetDatabase.GetAssetPath(controller)), Is.False,
            "Production animation must survive removal of the development labs.");
        Assert.That(controller.animationClips, Is.Not.Empty);
        Assert.That(controller.animationClips.All(clip => clip != null && clip.length > 0f), Is.True);
    }

    [TestCase("Gera")]
    [TestCase("DiMag")]
    [TestCase("Vika")]
    public void CharacterDeathFeedbackBindsExistingRenderers(string character)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/Characters/" + character + ".prefab");
        var feedback = prefab.GetComponent<PlayerFeedbackPresentation>();
        var renderers = new SerializedObject(feedback).FindProperty("renderers");
        Assert.That(renderers.arraySize, Is.GreaterThan(0));
        for (int index = 0; index < renderers.arraySize; index++)
            Assert.That(renderers.GetArrayElementAtIndex(index).objectReferenceValue, Is.Not.Null,
                character + " death feedback renderer " + index);
    }

    [TestCase(false, "ORPHAN_PREFAB_OVERRIDE")]
    [TestCase(true, "MISSING_PREFAB_SOURCE")]
    public void MissingPrefabReferencesIdentifyTheirRole(bool strippedSource, string expectedCode)
    {
        string source = SaveRendererPrefab(null);
        string variant = SaveVariant(source);
        string guid = AssetDatabase.AssetPathToGUID(source);
        string text = File.ReadAllText(variant);
        if (strippedSource)
        {
            string instanceId = Regex.Match(text, @"(?m)^--- !u!1001 &(-?\d+)").Groups[1].Value;
            Assert.That(instanceId, Is.Not.Empty);
            text += "\n--- !u!212 &987654321 stripped\nSpriteRenderer:\n" +
                "  m_CorrespondingSourceObject: {fileID: 987654320, guid: " + guid + ", type: 3}\n" +
                "  m_PrefabInstance: {fileID: " + instanceId + "}\n  m_PrefabAsset: {fileID: 0}\n";
        }
        else
        {
            var target = new Regex(@"(- target: \{fileID: )-?\d+(, guid: " + guid + @", type: 3\})");
            Assert.That(target.IsMatch(text), Is.True);
            text = target.Replace(text, "${1}987654321${2}", 1);
        }
        File.WriteAllText(variant, text);
        AssetDatabase.ImportAsset(variant, ImportAssetOptions.ForceSynchronousImport);
        var report = Subject42AssetReferenceValidator.Validate(new[] { variant });
        Assert.That(report.Issues.Any(issue => issue.Code == expectedCode), Is.True, report.FormatErrors());
        Assert.That(report.Issues.Any(issue => issue.Code == "BROKEN_FILE_ID"), Is.False,
            "Prefab bookkeeping needs a different diagnosis from a missing live object reference.");
    }

    [Test]
    public void NestedPrefabAndVariantReferencesRemainValid()
    {
        string variant = SaveVariant(SaveRendererPrefab(null));
        var owner = new GameObject("Nested fixture");
        try
        {
            var nested = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(variant));
            nested.transform.SetParent(owner.transform, false);
            string path = fixtureRoot + "/Nested.prefab";
            PrefabUtility.SaveAsPrefabAsset(owner, path);
            var report = Subject42AssetReferenceValidator.Validate(new[] { path });
            Assert.That(report.ErrorCount, Is.Zero, report.FormatErrors());
        }
        finally { Object.DestroyImmediate(owner); }
    }

    [Test]
    public void SharedDevDependencyListsAllConsumersOnce()
    {
        string name = "InitTestSceneIntegrityDev_" + Guid.NewGuid().ToString("N") + ".unity-fixtures";
        string devFolder = "Assets/_Project/Dev/" + name;
        AssetDatabase.CreateFolder("Assets/_Project/Dev", name);
        try
        {
            string materialPath = devFolder + "/Shared.mat";
            var material = new Material(Shader.Find("Sprites/Default"));
            AssetDatabase.CreateAsset(material, materialPath);
            string first = SaveRendererPrefab(null, material);
            string second = fixtureRoot + "/Other.prefab";
            Assert.That(AssetDatabase.CopyAsset(first, second), Is.True);
            var report = Subject42AssetReferenceValidator.Validate(new[] { first, second });
            var issues = report.Issues.Where(issue => issue.Code == "DEV_DEPENDENCY").ToArray();
            Assert.That(issues, Has.Length.EqualTo(1), report.FormatErrors());
            Assert.That(issues[0].Message, Does.Contain(materialPath).And.Contain(first).And.Contain(second));
            Assert.That(issues[0].Severity, Is.EqualTo(Subject42ValidationSeverity.Error));
        }
        finally { AssetDatabase.DeleteAsset(devFolder); }
    }

    private string SaveVariant(string source)
    {
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source));
        try
        {
            instance.name = "Variant fixture";
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance);
            string path = fixtureRoot + "/Variant.prefab";
            PrefabUtility.SaveAsPrefabAsset(instance, path);
            return path;
        }
        finally { Object.DestroyImmediate(instance); }
    }

    private string SaveRendererPrefab(Sprite sprite, Material material = null)
    {
        var owner = new GameObject("Reference fixture", typeof(SpriteRenderer), typeof(CameraFollow));
        try
        {
            owner.GetComponent<SpriteRenderer>().sprite = sprite;
            if (material != null) owner.GetComponent<SpriteRenderer>().sharedMaterial = material;
            string path = fixtureRoot + "/Renderer.prefab";
            PrefabUtility.SaveAsPrefabAsset(owner, path);
            return path;
        }
        finally { Object.DestroyImmediate(owner); }
    }

    private string SaveRendererScene(Sprite sprite)
    {
        // Scene files accept the same serialized GameObject/component documents as a prefab.
        // This builds a real imported fixture scene without touching the editor's current scene setup.
        string prefab = SaveRendererPrefab(sprite);
        string path = fixtureRoot + "/Renderer.unity";
        File.Copy(prefab, path);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        return path;
    }
}
#endif
