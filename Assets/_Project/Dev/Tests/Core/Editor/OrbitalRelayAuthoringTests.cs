#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using System.Linq;
public sealed class OrbitalRelayAuthoringTests
{
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
            var relay = root.GetComponent<OrbitalRelayEvent>(); var node = root.GetComponentInChildren<OrbitalRelayNode>();
            OrbitalRelayAuthoring.Set(relay, "nodes", new[] { node, node });
            Assert.That(relay.TryValidateConfiguration(out _), Is.False);
            OrbitalRelayAuthoring.Set(relay, "nodes", new[] { node });
            Assert.That(relay.TryValidateConfiguration(out _), Is.False);
        }
        finally { Object.DestroyImmediate(root); }
    }
}
#endif
