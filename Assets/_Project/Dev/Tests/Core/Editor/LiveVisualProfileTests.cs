#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using TMPro;
using NUnit.Framework;
using Subject42.Visuals;
using UnityEngine;
using UnityEditor;
using System.Linq;
using Object = UnityEngine.Object;

public sealed class LiveVisualProfileTests
{
    private sealed class CountingProfileReader : StringReader
    {
        public int CharactersRead { get; private set; }
        public CountingProfileReader(string text) : base(text) { }
        public override int Read(char[] buffer, int index, int count)
        {
            // Exercise short reads as well as the final one-character boundary.
            int read = base.Read(buffer, index, Math.Min(count, 997));
            CharactersRead += read;
            return read;
        }
    }

    [Test]
    public void StudioDestroyRestoresCursorOnlyForOpenOwner()
    {
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        var instanceField = typeof(LiveVisualStudio).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        object originalInstance = instanceField.GetValue(null);
        Assert.That(ReferenceEquals(originalInstance, null), Is.True,
            "Do not replace or destroy an existing Studio, including a stale static reference.");
        Assert.That(Subject42DebugMenu.IsDebugMenuOpen, Is.False,
            "Cursor restoration belongs to F1 while its menu is open; this scenario requires it closed.");
        bool originalVisible = Cursor.visible;
        var originalLock = Cursor.lockState;
        GameObject root = null;
        try
        {
            // Probe visibility independently: batchmode may reject Locked while still
            // supporting visible/hidden. Do not mistake an unsupported state for a defect.
            Cursor.lockState = CursorLockMode.None;
            if (Cursor.lockState != CursorLockMode.None)
                Assert.Inconclusive("Environment rejects CursorLockMode.None; cursor lifecycle was not exercised.");
            Cursor.visible = false;
            bool canHide = !Cursor.visible;
            Cursor.visible = true;
            bool canShow = Cursor.visible;
            if (!canHide || !canShow)
                Assert.Inconclusive($"Environment cannot toggle Cursor.visible at None (hide={canHide}, show={canShow}); " +
                    "DestroyImmediate restoration was not exercised. MANUAL_PLAYTEST_REQUIRED.");

            Cursor.lockState = CursorLockMode.Locked;
            bool canLock = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = CursorLockMode.None;
            Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
            if (!canLock)
                TestContext.WriteLine("Locked is unsupported; only visibility restoration will be exercised. " +
                    "Locked restoration remains unverified. MANUAL_PLAYTEST_REQUIRED.");

            // None is a separate visibility case, never a substitute expected value
            // for Locked. A supported Locked case retains its exact original assertion.
            foreach (var savedLock in canLock
                         ? new[] { CursorLockMode.None, CursorLockMode.Locked }
                         : new[] { CursorLockMode.None })
            // Closed first, so its non-interference is checked even when the open case fails.
            foreach (bool open in new[] { false, true })
            {
                root = new GameObject("NightShift-cursor-" + Guid.NewGuid().ToString("N"));
                root.SetActive(false);
                var studio = root.AddComponent<LiveVisualStudio>();
                var ui = new GameObject("Owned test UI");
                ui.transform.SetParent(root.transform, false);
                ui.SetActive(open);
                typeof(LiveVisualStudio).GetField("uiRoot", fields).SetValue(studio, ui);
                typeof(LiveVisualStudio).GetField("previousCursorVisible", fields).SetValue(studio, false);
                typeof(LiveVisualStudio).GetField("previousCursorLock", fields).SetValue(studio, savedLock);
                // Unity delivers OnDestroy only to previously active objects. No frame is yielded,
                // and no Open/Show/Build/Hide, runtime or PlayerPrefs path is invoked.
                root.SetActive(true);
                root.SetActive(false);
                instanceField.SetValue(null, studio);
                Cursor.lockState = savedLock;
                Cursor.visible = false;
                Assert.That(Cursor.lockState, Is.EqualTo(savedLock),
                    "Environment must actually support the saved lock state.");
                Assert.That(Cursor.visible, Is.False, "Environment must support the saved visibility.");
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                Assert.That(Cursor.lockState, Is.EqualTo(CursorLockMode.None));
                Assert.That(Cursor.visible, Is.True);
                Assert.That(LiveVisualStudio.IsOpen, Is.EqualTo(open));

                // Destroy the component while its own UI still exists, using the real lifecycle.
                Object.DestroyImmediate(studio);
                Assert.That(ReferenceEquals(instanceField.GetValue(null), null), Is.True,
                    "OnDestroy must clear the owning static reference.");
                Assert.That(Cursor.visible, Is.EqualTo(!open),
                    "An open owner must restore visibility; a closed owner must leave it alone.");
                Assert.That(Cursor.lockState, Is.EqualTo(open ? savedLock : CursorLockMode.None),
                    "An open owner must restore locking; a closed owner must leave it alone.");
                Object.DestroyImmediate(root);
                root = null;
            }
            if (!canLock)
                Assert.Inconclusive("Visibility-only destruction checks completed, but the environment rejects Locked. " +
                    "Locked restoration is unverified; this scenario is not a full PASS. MANUAL_PLAYTEST_REQUIRED.");
        }
        finally
        {
            if (root != null) Object.DestroyImmediate(root);
            instanceField.SetValue(null, originalInstance);
            Cursor.lockState = originalLock;
            Cursor.visible = originalVisible;
        }
    }

    [Test]
    public void StudioNamedFileSaveLoadPreservesDraftAndRejectsInvalidImport()
    {
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        string name = "NightShift-" + Guid.NewGuid().ToString("N");
        string assetPath = "Assets/_Project/Data/World/Presentation/Profiles/" + name + ".json";
        string path = Path.GetFullPath(assetPath);
        string directory = Path.GetDirectoryName(path);
        bool directoryExisted = Directory.Exists(directory);
        bool directoryMetaExisted = File.Exists(directory + ".meta");
        Assert.That(File.Exists(path) || File.Exists(path + ".meta"), Is.False);
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
        GameObject root = null;
        VisualTuningPreset preset = null;
        LiveVisualRuntime runtime = null;
        TextAsset localProduction = null;
        bool ownsFile = false;
        bool ownsDirectory = false;
        try
        {
            // Do not Initialize/Attach: those discover a scene and install the global runtime.
            // File actions need only a private preset and a real runtime's profile operations.
            root = new GameObject(name);
            root.SetActive(false);
            runtime = root.AddComponent<LiveVisualRuntime>();
            preset = Object.Instantiate(authoredPreset);
            // Exercise an assigned reference without touching the authored preset or snapshot.
            var productionProfile = new VisualProfile { Name = name + "-production" };
            productionProfile.Validate();
            localProduction = new TextAsset(JsonUtility.ToJson(productionProfile));
            preset.LiveProductionProfile = localProduction;
            Action assertProductionReferences = () =>
            {
                Assert.That(localProduction != null, Is.True, "Local production snapshot must remain alive.");
                Assert.That(preset.LiveProductionProfile == localProduction, Is.True,
                    "Draft operations must preserve the assigned local production snapshot.");
                Assert.That(preset.LiveProductionProfile.GetInstanceID(), Is.EqualTo(localProduction.GetInstanceID()));
                // Unity's unassigned references can have different managed wrappers after Instantiate.
                // Compare Unity null semantics and the serialized reference, never format a fake-null Object.
                Assert.That(authoredPreset.LiveProductionProfile == null, Is.EqualTo(productionWasNull));
                Assert.That(authoredPreset.LiveProductionProfile == productionReference, Is.True);
                Assert.That(new SerializedObject(authoredPreset).FindProperty("LiveProductionProfile")
                    .objectReferenceInstanceIDValue, Is.EqualTo(productionReferenceId));
            };
            typeof(LiveVisualRuntime).GetField("<Preset>k__BackingField", fields).SetValue(runtime, preset);
            var studio = root.AddComponent<LiveVisualStudio>();
            typeof(LiveVisualStudio).GetField("runtime", fields).SetValue(studio, runtime);
            var inputObject = new GameObject("Profile name", typeof(RectTransform));
            inputObject.transform.SetParent(root.transform, false);
            var input = inputObject.AddComponent<TMP_InputField>();
            input.SetTextWithoutNotify(name);
            typeof(LiveVisualStudio).GetField("presetName", fields).SetValue(studio, input);
            foreach (string field in new[] { "modeInfo", "targetInfo" })
            {
                var textObject = new GameObject(field, typeof(RectTransform));
                textObject.transform.SetParent(root.transform, false);
                typeof(LiveVisualStudio).GetField(field, fields).SetValue(studio, textObject.AddComponent<TextMeshProUGUI>());
            }
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
            const int maxJsonCharacters = 4 * 1024 * 1024;
            var oversized = profile.Copy();
            // Notes are valid profile data and survive ForProduction/Export unchanged.
            oversized.Notes.Add(new string('x', maxJsonCharacters));
            oversized.Validate();
            runtime.Load(oversized);
            string oversizedJson = runtime.Export(name);
            Assert.That(oversizedJson.Length, Is.GreaterThan(maxJsonCharacters));
            Assert.That(oversizedJson.Length, Is.LessThan(maxJsonCharacters + 4096));
            JsonUtility.FromJson<VisualProfile>(oversizedJson).Validate();
            Action assertSizeRejectedWithoutSessionChange = () =>
            {
                var customBefore = runtime.Custom;
                string jsonBefore = JsonUtility.ToJson(customBefore);
                var modeBefore = runtime.Mode;
                int revisionBefore = runtime.Revision;
                float alphaBefore = sprite.color.a;
                var saveError = Assert.Throws<TargetInvocationException>(() =>
                    typeof(LiveVisualStudio).GetMethod("SaveProfile", fields).Invoke(studio, new object[] { false }));
                Assert.That(saveError.InnerException, Is.InstanceOf<ArgumentException>());
                var loadError = Assert.Throws<TargetInvocationException>(() =>
                    typeof(LiveVisualStudio).GetMethod("LoadJson", fields).Invoke(studio, new object[] { oversizedJson }));
                Assert.That(loadError.InnerException, Is.InstanceOf<ArgumentException>());
                Assert.That(runtime.Custom, Is.SameAs(customBefore));
                Assert.That(JsonUtility.ToJson(runtime.Custom), Is.EqualTo(jsonBefore));
                Assert.That(runtime.Mode, Is.EqualTo(modeBefore));
                Assert.That(runtime.Revision, Is.EqualTo(revisionBefore));
                Assert.That(sprite.color.a, Is.EqualTo(alphaBefore));
                assertProductionReferences();
            };
            // The unique path was checked absent above; own any failed-action residue for finally.
            ownsFile = true;
            ownsDirectory = !directoryExisted && !directoryMetaExisted;
            assertSizeRejectedWithoutSessionChange();
            Assert.That(File.Exists(path) || File.Exists(path + ".meta"), Is.False,
                "Oversized Save must not create a draft or import it.");
            Assert.That(Directory.Exists(directory), Is.EqualTo(directoryExisted),
                "Size validation must precede directory creation.");
            Assert.That(File.Exists(directory + ".meta"), Is.EqualTo(directoryMetaExisted));
            runtime.Load(profile);
            liveCustom = runtime.Custom;
            liveJson = JsonUtility.ToJson(liveCustom);
            mode = runtime.Mode;
            // Reserve exclusively before the real Save action; never overwrite an existing draft.
            Directory.CreateDirectory(directory);
            ownsDirectory = !directoryExisted && !directoryMetaExisted;
            using (new FileStream(path, FileMode.CreateNew, FileAccess.Write)) { }
            ownsFile = true;
            typeof(LiveVisualStudio).GetMethod("SaveProfile", fields).Invoke(studio, new object[] { false });
            assertProductionReferences();
            Assert.That(runtime.Custom, Is.SameAs(liveCustom));
            Assert.That(JsonUtility.ToJson(runtime.Custom), Is.EqualTo(liveJson));
            Assert.That(runtime.Mode, Is.EqualTo(mode));
            var saved = JsonUtility.FromJson<VisualProfile>(File.ReadAllText(path));
            saved.Validate();
            Assert.That(saved.Name, Is.EqualTo(name));
            Assert.That(saved.Values.Count, Is.EqualTo(2));
            Assert.That(saved.Values.Any(v => v.Scope == "Instance"), Is.False);
            Assert.That(saved.Resolve(target.Group, target.TypeKey, target.Key, alpha.Id, 1), Is.EqualTo(.4f));
            Assert.That(saved.Resolve(target.Group, "sibling-type", "other", alpha.Id, 1), Is.EqualTo(.7f));
            Assert.That(AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath), Is.Not.Null);
            runtime.Edit("Type", target, alpha, .9f);
            Assert.That(sprite.color.a, Is.EqualTo(.9f).Within(.0001f));
            typeof(LiveVisualStudio).GetMethod("LoadProfile", fields).Invoke(studio, null);
            assertProductionReferences();
            Assert.That(sprite.color.a, Is.EqualTo(.4f).Within(.0001f));
            Assert.That(runtime.Mode, Is.EqualTo(VisualComparisonMode.Custom));
            Assert.That(JsonUtility.ToJson(runtime.Custom), Is.EqualTo(JsonUtility.ToJson(saved)));

            runtime.SetMode(VisualComparisonMode.Current);
            liveCustom = runtime.Custom;
            liveJson = JsonUtility.ToJson(liveCustom);
            float currentAlpha = sprite.color.a;
            int revision = runtime.Revision;
            var error = Assert.Throws<TargetInvocationException>(() =>
                typeof(LiveVisualStudio).GetMethod("LoadJson", fields).Invoke(studio, new object[] { "{ definitely invalid JSON" }));
            Assert.That(error.InnerException, Is.InstanceOf<ArgumentException>());
            Assert.That(runtime.Custom, Is.SameAs(liveCustom));
            Assert.That(JsonUtility.ToJson(runtime.Custom), Is.EqualTo(liveJson));
            Assert.That(runtime.Mode, Is.EqualTo(VisualComparisonMode.Current));
            Assert.That(runtime.Revision, Is.EqualTo(revision));
            Assert.That(sprite.color.a, Is.EqualTo(currentAlpha));
            assertProductionReferences();
            byte[] savedBytes = File.ReadAllBytes(path);
            byte[] savedMeta = File.ReadAllBytes(path + ".meta");
            var readText = typeof(LiveVisualStudio).GetMethod("ReadProfileText", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(readText, Is.Not.Null);
            using (var reader = new CountingProfileReader(oversizedJson))
            {
                string bounded = (string)readText.Invoke(null, new object[] { reader });
                Assert.That(bounded, Is.EqualTo(oversizedJson.Substring(0, maxJsonCharacters + 1)));
                Assert.That(reader.CharactersRead, Is.EqualTo(maxJsonCharacters + 1),
                    "Import must stop after the decoded character limit plus one, even with short reads.");
            }
            try
            {
                // UTF-16 BOM and a file larger than 4 MiB must retain the character contract.
                string boundaryJson = System.Text.Encoding.UTF8.GetString(savedBytes).TrimStart('\uFEFF');
                boundaryJson = boundaryJson.PadRight(maxJsonCharacters);
                File.WriteAllText(path, boundaryJson, System.Text.Encoding.Unicode);
                typeof(LiveVisualStudio).GetMethod("LoadProfile", fields).Invoke(studio, null);
                Assert.That(JsonUtility.ToJson(runtime.Custom), Is.EqualTo(JsonUtility.ToJson(saved)));
                assertProductionReferences();
                runtime.SetMode(VisualComparisonMode.Current);
                var customBefore = runtime.Custom;
                string jsonBefore = JsonUtility.ToJson(customBefore);
                int revisionBefore = runtime.Revision;
                float alphaBefore = sprite.color.a;
                File.WriteAllText(path, oversizedJson, System.Text.Encoding.Unicode);
                var fileError = Assert.Throws<TargetInvocationException>(() =>
                    typeof(LiveVisualStudio).GetMethod("LoadProfile", fields).Invoke(studio, null));
                Assert.That(fileError.InnerException, Is.InstanceOf<ArgumentException>());
                Assert.That(runtime.Custom, Is.SameAs(customBefore));
                Assert.That(JsonUtility.ToJson(runtime.Custom), Is.EqualTo(jsonBefore));
                Assert.That(runtime.Mode, Is.EqualTo(VisualComparisonMode.Current));
                Assert.That(runtime.Revision, Is.EqualTo(revisionBefore));
                Assert.That(sprite.color.a, Is.EqualTo(alphaBefore));
                assertProductionReferences();
            }
            finally
            {
                File.WriteAllBytes(path, savedBytes);
            }
            runtime.Load(oversized);
            runtime.SetMode(VisualComparisonMode.Current);
            assertSizeRejectedWithoutSessionChange();
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(savedBytes),
                "Rejected Save must preserve the existing owned draft byte-for-byte.");
            Assert.That(File.ReadAllBytes(path + ".meta"), Is.EqualTo(savedMeta));
            foreach (var pair in authored)
                Assert.That(File.ReadAllBytes(pair.Key), Is.EqualTo(pair.Value), pair.Key + " changed during draft Save/Load.");
        }
        finally
        {
            // An always-inactive MonoBehaviour need not receive OnDestroy in Unity.
            if (runtime != null)
            {
                foreach (var target in runtime.Targets) target.Release?.Invoke();
                runtime.Targets.Clear();
            }
            if (root != null) Object.DestroyImmediate(root);
            if (preset != null) Object.DestroyImmediate(preset);
            if (localProduction != null) Object.DestroyImmediate(localProduction);
            if (ownsFile)
            {
                AssetDatabase.DeleteAsset(assetPath);
                if (File.Exists(path)) File.Delete(path);
                if (File.Exists(path + ".meta")) File.Delete(path + ".meta");
            }
            // Only remove an empty folder created by this invocation; never recurse.
            if (ownsDirectory && Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any())
            {
                // Delete the registered, empty folder through Unity as well as the filesystem.
                // The emptiness/ownership guards above exclude pre-existing or foreign contents.
                string directoryAssetPath = assetPath.Substring(0, assetPath.LastIndexOf('/'));
                AssetDatabase.DeleteAsset(directoryAssetPath);
                if (Directory.Exists(directory)) Directory.Delete(directory);
                if (File.Exists(directory + ".meta")) File.Delete(directory + ".meta");
            }
            // Synchronize fallback filesystem deletions before Unity's test cleanup check.
            if (ownsFile || ownsDirectory) AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }
        Assert.That(File.Exists(path) || File.Exists(path + ".meta"), Is.False, "Owned draft must be removed.");
        Assert.That(AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath) == null, Is.True);
        if (ownsDirectory)
        {
            Assert.That(Directory.Exists(directory) || File.Exists(directory + ".meta"), Is.False,
                "Owned empty profile directory must be removed.");
            Assert.That(AssetDatabase.IsValidFolder(assetPath.Substring(0, assetPath.LastIndexOf('/'))), Is.False);
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
