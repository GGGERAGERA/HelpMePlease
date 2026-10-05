#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using System.Linq;
public sealed class OrbitalRelayAuthoringTests
{
    [Test] public void LabCompositionHasRequiredAuthoredServices()
    {
        var composition = AssetDatabase.LoadAssetAtPath<ProductionSceneComposition>(ProductionSceneCompositionAuthoring.PrefabPath);
        Assert.That(composition, Is.Not.Null);
        var serialized = new SerializedObject(composition);
        foreach (string field in new[] { "localization", "audio", "unlocks", "bunkerProgression", "transition", "orbital", "visualPreset" })
            Assert.That(serialized.FindProperty(field)?.objectReferenceValue, Is.Not.Null, "Lab composition dependency: " + field);
    }
    [Test] public void ProductionStartZoneIsDiscoverableByExistingInteractor()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OrbitalRelayAuthoring.PrefabPath);
        var arena = prefab.GetComponentInChildren<CircleCollider2D>(true);
        var relay = prefab.GetComponent<OrbitalRelayEvent>();
        var serialized = new SerializedObject(relay);
        Assert.That(serialized.FindProperty("startArea"), Is.Not.Null, "Relay requires a separate start zone.");
        arena = (CircleCollider2D)serialized.FindProperty("startArea").objectReferenceValue;
        Assert.That(arena.radius, Is.EqualTo(2.5f));
        Assert.That(arena.GetComponent<Interactable>(), Is.EqualTo(relay),
            "PlayerInteractor discovers Interactable on the collider object, not its parent.");
    }
    [Test] public void InactivePrefabShowsStartZoneAndHidesNodes()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OrbitalRelayAuthoring.PrefabPath);
        Assert.That(prefab.transform.Find("StartZoneVisual"), Is.Not.Null);
        Assert.That(prefab.transform.Find("StartZoneVisual").gameObject.activeSelf, Is.True);
        Assert.That(prefab.transform.Find("StartZoneVisual").GetComponentsInChildren<Renderer>().Length, Is.GreaterThan(0));
        Assert.That(prefab.transform.Find("Nodes").gameObject.activeSelf, Is.False, "Don't reveal Node locations before E.");
    }
    [Test] public void LabResultNotificationHasVisibleAuthoredAncestors()
    {
        var support = AssetDatabase.LoadAssetAtPath<GameObject>(WorldSystemsLabRelaySupportAuthoring.SupportPath);
        var view = support.GetComponentInChildren<RunMessageService>(true).View;
        foreach (var group in view.GetComponentsInParent<CanvasGroup>(true))
            if (group.gameObject != view.gameObject)
                Assert.That(group.alpha, Is.EqualTo(1), "Result is hidden by " + group.name);
    }
    [Test] public void LabCatalogUsesSameProductionPrefab()
    {
        string scene = System.IO.File.ReadAllText(WorldSystemsLabController.ScenePath);
        Assert.That(scene, Does.Contain(AssetDatabase.AssetPathToGUID(OrbitalRelayAuthoring.PrefabPath)));
        Assert.That(scene, Does.Not.Contain("607475cde93c4153994eb855547209bb"));
    }
    [Test] public void ProductionPrefabExists()
    { Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Environment/WorldEvents/OrbitalRelayEvent.prefab"), Is.Not.Null); }
    [Test] public void AuthoredPrefabHasValidReferencesAndNoDevDependencies()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<OrbitalRelayEvent>(OrbitalRelayAuthoring.PrefabPath);
        Assert.That(prefab.TryValidateConfiguration(out string error), Is.True, error);
        Assert.That(prefab.GetComponentsInChildren<OrbitalRelayNode>(true).Length, Is.EqualTo(3));
        Assert.That(prefab.GetComponentsInChildren<LineRenderer>(true).All(line => line.positionCount == 64), Is.True);
        Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true).All(component => component != null), Is.True);
        Assert.That(AssetDatabase.GetDependencies(OrbitalRelayAuthoring.PrefabPath, true).Any(path => path.StartsWith("Assets/_Project/Dev/")), Is.False);
    }
    [Test] public void InvalidNodeCollectionIsRejected()
    {
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(OrbitalRelayAuthoring.PrefabPath));
        try
        {
            var relay = root.GetComponent<OrbitalRelayEvent>(); var node = root.GetComponentInChildren<OrbitalRelayNode>(true);
            OrbitalRelayAuthoring.Set(relay, "nodes", new[] { node, node });
            Assert.That(relay.TryValidateConfiguration(out _), Is.False);
            OrbitalRelayAuthoring.Set(relay, "nodes", new[] { node });
            Assert.That(relay.TryValidateConfiguration(out _), Is.False);
        }
        finally { Object.DestroyImmediate(root); }
    }
    [Test] public void NonuniformContactCircleRejected()
    {
        var root = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(OrbitalRelayAuthoring.PrefabPath));
        try
        {
            root.GetComponentInChildren<OrbitalRelayNode>(true).transform.localScale = new Vector3(1, 2, 1);
            Assert.That(root.GetComponent<OrbitalRelayEvent>().TryValidateConfiguration(out _), Is.False);
        }
        finally { Object.DestroyImmediate(root); }
    }
    [Test] public void MigratedAssetsHaveNoMissingScriptOrExternalGuid()
    {
        foreach (string path in new[] { OrbitalRelayAuthoring.PrefabPath, WorldSystemsLabRelaySupportAuthoring.SupportPath,
            WorldSystemsLabController.ScenePath, "Assets/_Project/Scenes/MainBuild/MVP.unity",
            "Assets/_Project/Data/SurfaceMap/Sector_D1.asset", "Assets/_Project/Data/SurfaceMap/Sector_D2.asset" })
        {
            foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(
                System.IO.File.ReadAllText(path), @"guid: ([a-f0-9]{32})"))
            {
                string guid = match.Groups[1].Value;
                if (guid.StartsWith("0000000000000000")) continue; // Unity builtin resources.
                Assert.That(AssetDatabase.GUIDToAssetPath(guid), Is.Not.Empty, path + " missing GUID " + guid);
            }
        }
        var support = AssetDatabase.LoadAssetAtPath<GameObject>(WorldSystemsLabRelaySupportAuthoring.SupportPath);
        Assert.That(support.GetComponentsInChildren<MonoBehaviour>(true).All(component => component != null), Is.True);
        Assert.That(support.GetComponentInChildren<RunMessageService>(true), Is.Not.Null);
        Assert.That(support.GetComponentInChildren<UpgradeManager>(true), Is.Not.Null);
    }
}
#endif
