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
    public void RingDepth_AuthoredSemicirclesShareEndpointsAndBlendOpacity()
    {
        var view = Object.Instantiate(Config.RingPrefab);
        try
        {
            Assert.That(view.GetComponentsInChildren<LineRenderer>().Length, Is.EqualTo(2));
            var back = view.BackLine;
            var front = view.FrontLine;
            Assert.That(back.loop || front.loop || back.useWorldSpace || front.useWorldSpace, Is.False);
            Assert.That(back.positionCount, Is.EqualTo(front.positionCount));
            for (int i = 0; i < back.positionCount; i++)
            {
                Vector3 b = back.GetPosition(i), f = front.GetPosition(i);
                Assert.That(b.y, Is.GreaterThanOrEqualTo(0f));
                Assert.That(f.y, Is.LessThanOrEqualTo(0f));
                Assert.That(b.magnitude, Is.EqualTo(1f).Within(.00001f));
                Assert.That(f, Is.EqualTo(new Vector3(b.x, -b.y, 0f)));
            }
            Assert.That(back.GetPosition(0), Is.EqualTo(front.GetPosition(0)));
            Assert.That(back.GetPosition(back.positionCount - 1), Is.EqualTo(front.GetPosition(front.positionCount - 1)));
            foreach (float alpha in new[] { .52f, .95f, .16f })
            {
                var color = new Color(.4f, .8f, 1f, alpha);
                view.SetAppearance(3.7f, .08f, color);
                Assert.That(back.transform.localScale, Is.EqualTo(front.transform.localScale));
                Assert.That(front.transform.localScale.x, Is.EqualTo(3.7f));
                Assert.That(back.widthMultiplier, Is.EqualTo(front.widthMultiplier));
                Assert.That(back.colorGradient.Evaluate(.5f).a, Is.EqualTo(alpha * .5f).Within(.0001f));
                foreach (float endpoint in new[] { 0f, 1f })
                    Assert.That(back.colorGradient.Evaluate(endpoint).a,
                        Is.EqualTo(front.colorGradient.Evaluate(endpoint).a).Within(.0001f));
                float previous = alpha;
                for (int i = 0; i <= 12; i++)
                {
                    float current = back.colorGradient.Evaluate(i * .01f).a;
                    Assert.That(current, Is.LessThanOrEqualTo(previous + .0001f));
                    previous = current;
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
                bool first = order == 0;
                Assert.That(view.BackLine.sortingLayerName, Is.EqualTo(first ? "Default" : "Player"));
                Assert.That(view.BackLine.colorGradient.Evaluate(.5f).a,
                    Is.EqualTo(view.FrontLine.colorGradient.Evaluate(.5f).a * (first ? .5f : 1f)).Within(.0001f));
                if (!first) Assert.That(view.BackLine.sortingOrder, Is.EqualTo(view.FrontLine.sortingOrder));
                foreach (var mount in ring.Mounts)
                    Assert.That(mount.Transform.GetComponent<OrbitalMountView>().DepthGroup.enabled, Is.EqualTo(first));
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
                        new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * state.Radius), Is.LessThan(.00001f));
                    var group = mount.Transform.GetComponent<OrbitalMountView>().DepthGroup;
                    Assert.That(group.sortingOrder, mount.Transform.localPosition.y > 0f ? Is.LessThan(0) : Is.GreaterThan(0));
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
