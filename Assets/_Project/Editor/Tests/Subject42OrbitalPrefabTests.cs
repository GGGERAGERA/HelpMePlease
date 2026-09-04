#if UNITY_EDITOR
using System.IO;
using System.Collections;
using System.Text.RegularExpressions;
using UnityEngine.TestTools;
using System.Linq;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEngine;
public sealed class Subject42OrbitalPrefabTests
{
    private static OrbitalPresentationConfig Config => Resources.Load<OrbitalPresentationConfig>("OrbitalStation/OrbitalPresentationConfig");
    [TestCase(OrbitalModuleKind.Pistol)]
    [TestCase(OrbitalModuleKind.LaserSword)]
    [TestCase(OrbitalModuleKind.ImpulseGun)]
    [TestCase(OrbitalModuleKind.ArcEmitter)]
    [TestCase(OrbitalModuleKind.LinkNode)]
    public void VisualPrefab_IsAuthoredAndPure(OrbitalModuleKind kind)
    {
        string path = AssetDatabase.GetAssetPath(Config.GetPrefab(kind));
        Assert.That(path, Is.Not.Empty);
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            Assert.That(root.GetComponent<OrbitalModuleView>().IsValid, Is.True);
            Assert.That(root.GetComponentsInChildren<BaseWeapon>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Collider2D>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Rigidbody2D>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<AudioSource>(true), Is.Empty);
            Assert.That(root.GetComponentsInChildren<Component>(true).Any(c => c == null), Is.False);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    [TestCase(OrbitalModuleKind.Pistol, "p_miniWeaponPistol1")]
    [TestCase(OrbitalModuleKind.LaserSword, "p_miniWeaponLaserSward1")]
    [TestCase(OrbitalModuleKind.ImpulseGun, "p_miniWeaponImpulseGun1")]
    public void SourceArt_ScaleSortingAndBaseColorsPreserved(OrbitalModuleKind kind, string sourceName)
    {
        var source = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/miniWeapons/" + sourceName + ".prefab");
        var view = Config.GetPrefab(kind).GetComponent<OrbitalModuleView>();
        Assert.That(view.Body.localScale.x, Is.EqualTo(Config.GetScale(kind)).Within(.0001f));
        foreach (var renderer in source.GetComponentsInChildren<SpriteRenderer>(true))
        {
            string path = AnimationUtility.CalculateTransformPath(renderer.transform, source.transform);
            var copy = (path.Length == 0 ? view.Body : view.Body.Find(path)).GetComponent<SpriteRenderer>();
            Assert.That(copy.sortingLayerName, Is.EqualTo("Player"));
            Assert.That(copy.sortingOrder, Is.EqualTo(renderer.sortingOrder + Config.MountedWeaponSortingOffset));
            Assert.That(copy.color, Is.EqualTo(renderer.color), sourceName + "/" + path);
        }
    }

    [Test]
    public void Composition_RequiredAuthoredRootsAndDynamicCounts()
    {
        Assert.That(Config.ValidateRequiredReferences(out var error), Is.True, error);
        var root = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(Config.StationPrefab));
        try
        {
            var view = root.GetComponent<OrbitalStationView>();
            Assert.That(view.IsValid, Is.True);
            Assert.That(view.RingsRoot.childCount, Is.Zero);
            Assert.That(view.EffectsRoot.childCount, Is.Zero);
            Assert.That(view.Core.transform.IsChildOf(root.transform), Is.True);
            Assert.That(root.GetComponents<OrbitalStationRuntime>().Length, Is.EqualTo(1));
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        Assert.That(Config.RingPrefab.MountsRoot.childCount, Is.Zero);
        var broken = Object.Instantiate(Config);
        try { broken.RingPrefab = null; Assert.That(broken.ValidateRequiredReferences(out _), Is.False); }
        finally { Object.DestroyImmediate(broken); }
    }
    [Test]
    public void ProductionCharacters_AreSeparateFromLegacySources()
    {
        Assert.That(Config.PlayerVariants.Length, Is.GreaterThan(0));
        foreach (var entry in Config.PlayerVariants)
        {
            Assert.That(entry.Production, Is.Not.SameAs(entry.Source));
            var root = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(entry.Production));
            try
            {
                Assert.That(root.GetComponentsInChildren<BaseWeapon>(true), Is.Empty);
                Assert.That(root.GetComponentsInChildren<PlayerWeaponOrbitVisual>(true), Is.Empty);
                Assert.That(root.GetComponentsInChildren<OrbitalStationView>(true).Length, Is.EqualTo(1));
                Assert.That(root.GetComponent<PlayerHealth>(), Is.Not.Null);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
    }
    [UnityTest]
    public IEnumerator MissingInstanceReference_FailsOnceWithoutRepairOrStateMutation()
    {
        yield return new EnterPlayMode();
        var stage = ScriptableObject.CreateInstance<StageProfileData>();
        var rule = ScriptableObject.CreateInstance<WorldRuleData>();
        var anomaly = ScriptableObject.CreateInstance<LocalAnomalyData>();
        var manager = RunStateManager.EnsureExists();
        manager.BeginNewRun(null, null, stage, rule, anomaly);
        string before = Subject42OrbitalRestoreTests.Snapshot(manager.OrbitalStationState);
        var player = new GameObject("Missing authored reference fixture");
        var root = Object.Instantiate(Config.StationPrefab, player.transform);
        var view = root.GetComponent<OrbitalStationView>();
        var core = view.Core;
        view.Core = null;
        LogAssert.Expect(LogType.Error, new Regex("operation=restore.*required authored station references are missing"));
        var station = OrbitalStationRuntime.Ensure(player);
        Assert.That(station.IsInitialized, Is.False);
        Assert.That(root.activeSelf, Is.False, "partial authored structure must not remain visible");
        Assert.That(OrbitalStationRuntime.Ensure(player), Is.SameAs(station));
        Assert.That(Subject42OrbitalRestoreTests.Snapshot(manager.OrbitalStationState), Is.EqualTo(before));
        Assert.That(view.Core, Is.Null, "no runtime child discovery or repair");
        view.Core = core; // Explicitly restore the missing serialized reference.
        Assert.That(station.SimulateSectorRestore(), Is.True);
        Assert.That(root.activeSelf, Is.True);
        Assert.That(Subject42OrbitalRestoreTests.Snapshot(manager.OrbitalStationState), Is.EqualTo(before));
        Object.Destroy(player); Object.Destroy(manager.gameObject);
        Object.Destroy(stage); Object.Destroy(rule); Object.Destroy(anomaly);
        yield return null;
        yield return new ExitPlayMode();
    }

    [Test]
    public void ProductionBuild_NoStrippingOrFixedRootConstruction()
    {
        string prefix = "Assets/_Project/scripts/Combat/OrbitalStation/";
        string visual = File.ReadAllText(prefix + "OrbitalModuleVisual.cs");
        Assert.That(visual, Does.Not.Contain("DisableCombatComponents"));
        Assert.That(visual, Does.Not.Contain("GetComponentsInChildren"));
        Assert.That(visual, Does.Not.Contain("new GameObject"));
        string model = File.ReadAllText(prefix + "OrbitalStationModel.cs");
        Assert.That(model, Does.Not.Contain("AddComponent"));
        Assert.That(model, Does.Not.Contain("new GameObject"));
        string station = File.ReadAllText(prefix + "OrbitalStationRuntime.cs");
        string initialize = station.Substring(station.IndexOf("public void Initialize()"));
        initialize = initialize.Substring(0, initialize.IndexOf("private void FailRestore"));
        Assert.That(initialize, Does.Not.Contain("new GameObject"));
        Assert.That(initialize, Does.Not.Contain("AddComponent"));
        Assert.That(initialize, Does.Not.Contain("new Material"));
    }
}
#endif
