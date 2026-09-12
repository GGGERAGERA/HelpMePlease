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
using UnityEngine.Rendering;
public sealed class Subject42OrbitalPrefabTests
{
    private static OrbitalPresentationConfig Config => Resources.Load<OrbitalPresentationConfig>("OrbitalStation/OrbitalPresentationConfig");

    [Test]
    public void RingLine_ContinuousTrajectoryHasUniformOpacity()
    {
        var view = Object.Instantiate(Config.RingPrefab);
        try
        {
            foreach (var path in new[] { OrbitalPathGeometry.Circle, new OrbitalPathGeometry(Config) })
            {
                view.InitializeGeometry(path);
                Assert.That(view.BackLine.enabled, Is.False);
                var line = view.FrontLine;
                Assert.That(line.loop, Is.True);
                Assert.That(line.useWorldSpace, Is.False);
                for (int i = 0; i < line.positionCount; i++)
                    Assert.That(Vector2.Distance(line.GetPosition(i) * path.Scale(1f),
                        path.Position(i / (float)line.positionCount, 1f)), Is.LessThan(.00001f));
                foreach (float alpha in new[] { .52f, .95f, .16f })
                {
                    view.SetAppearance(3.7f, .08f, new Color(.4f, .8f, 1f, alpha));
                    Assert.That(line.transform.localScale.x, Is.EqualTo(path.Scale(3.7f)));
                    for (int i = 0; i <= 100; i++)
                        Assert.That(line.colorGradient.Evaluate(i / 100f).a, Is.EqualTo(alpha).Within(.0001f));
                }
            }
        }
        finally { Object.DestroyImmediate(view.gameObject); }
    }
    [Test]
    public void DepthSorting_UsesPlayerInternalLayerAndKeepsWeaponArtIntact()
    {
        foreach (var entry in Config.PlayerVariants)
        {
            var playerGroup = entry.Production.GetComponent<SortingGroup>();
            Assert.That(playerGroup, Is.Not.Null);
            Assert.That(playerGroup.sortingLayerName, Is.EqualTo("Player"));
            foreach (var body in entry.Production.transform.Find("Graphic").GetComponentsInChildren<SpriteRenderer>(true))
            {
                Assert.That(Config.RingPrefab.BackLine.sortingLayerID, Is.EqualTo(body.sortingLayerID));
                Assert.That(Config.MountPrefab.DepthGroup.sortingLayerID, Is.EqualTo(body.sortingLayerID));
                Assert.That(Config.RingPrefab.BackLine.sortingOrder, Is.LessThan(body.sortingOrder));
                Assert.That(Config.RingPrefab.FrontLine.sortingOrder, Is.GreaterThan(body.sortingOrder));
            }
        }
        var mount = Object.Instantiate(Config.MountPrefab);
        try
        {
            Assert.That(mount.DepthGroup.sortAtRoot, Is.False);
            foreach (OrbitalModuleKind kind in System.Enum.GetValues(typeof(OrbitalModuleKind)))
            {
                var module = Object.Instantiate(Config.GetPrefab(kind), mount.transform, false);
                try
                {
                    foreach (float y in new[] { 1f, -1f, .001f, -.001f, 1f })
                    {
                        mount.UpdateDepth(y);
                        Assert.That(mount.DepthGroup.sortingOrder, y > 0f ? Is.LessThan(-1) : Is.GreaterThan(1));
                        foreach (var renderer in module.GetComponentsInChildren<Renderer>(true))
                        {
                            var groups = renderer.GetComponentsInParent<SortingGroup>(true);
                            Assert.That(groups, Does.Contain(mount.DepthGroup));
                            foreach (var group in groups)
                                Assert.That(group.sortAtRoot, Is.False, "A weapon must not escape the mount's depth group");
                        }
                    }
                }
                finally { Object.DestroyImmediate(module); }
            }
        }
        finally { Object.DestroyImmediate(mount.gameObject); }
    }

    [Test]
    public void OuterRings_KeepFullOpacityAndAuthoredSortingAfterReorder()
    {
        var root = new GameObject("Outer ring depth fixture");
        try
        {
            var state = OrbitalRunState.CreateDefault(7).AddRing();
            var ring = new OrbitalRingRuntime(state, root.transform, Config.VisualMaterial, Config.PixelSprite);
            var view = root.GetComponentInChildren<OrbitalRingView>();
            foreach (int order in new[] { 1, 0, 2, 0 })
            {
                state.Order = order;
                ring.Tick(0f);
                Assert.That(view.BackLine.enabled, Is.False);
                Assert.That(view.FrontLine.sortingLayerName, Is.EqualTo("Player"));
                Assert.That(view.FrontLine.colorGradient.Evaluate(.25f).a,
                    Is.EqualTo(view.FrontLine.colorGradient.Evaluate(.75f).a).Within(.0001f));
                foreach (var mount in ring.Mounts)
                    Assert.That(mount.Transform.GetComponent<OrbitalMountView>().DepthGroup.enabled, Is.False);
            }
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void RingDepth_TickKeepsStateAndMountMotionUnchanged()
    {
        var root = new GameObject("Ring depth fixture");
        try
        {
            var state = OrbitalRunState.CreateDefault(7).Rings[0];
            var ring = new OrbitalRingRuntime(state, root.transform, Config.VisualMaterial, Config.PixelSprite);
            for (int i = 0; i < 24; i++)
            {
                float expectedPhase = Mathf.Repeat(state.CurrentPhase + ring.RotationSpeed * ring.Direction * .5f, 360f);
                ring.Tick(.5f);
                Assert.That(state.CurrentPhase, Is.EqualTo(expectedPhase));
                Assert.That(state.Radius, Is.EqualTo(1.25f));
                Assert.That(state.MountCapacity, Is.EqualTo(3));
                foreach (var mount in ring.Mounts)
                {
                    float angle = (expectedPhase + mount.LocalPhase) * Mathf.Deg2Rad;
                    Assert.That(Vector3.Distance(mount.Transform.localPosition,
                        new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * ring.Radius), Is.LessThan(.00001f));
                    var group = mount.Transform.GetComponent<OrbitalMountView>().DepthGroup;
                    Assert.That(group.enabled, Is.False, "Circle mounts must retain authored weapon sorting above the continuous ring");
                }
            }
        }
        finally { Object.DestroyImmediate(root); }
    }
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
            Assert.That(view.EffectsRoot.childCount, Is.EqualTo(1));
            Assert.That(view.EffectsRoot.GetChild(0), Is.SameAs(view.CoreParticles.transform),
                "Only the authored core particles belong here before runtime effects spawn");
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
