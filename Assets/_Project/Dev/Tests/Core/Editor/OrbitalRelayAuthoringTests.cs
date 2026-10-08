#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using System.Linq;
public sealed class OrbitalRelayAuthoringTests
{
    [Test] public void ProductionStartZoneIsDiscoverableByExistingInteractor()
    {
        var relay = AssetDatabase.LoadAssetAtPath<OrbitalRelayEvent>(OrbitalRelayAuthoring.PrefabPath);
        Assert.That(relay, Is.Not.Null);
        var startZones = relay.GetComponentsInChildren<CircleCollider2D>(true)
            .Where(collider => collider.GetComponent<Interactable>() == relay).ToArray();
        Assert.That(startZones, Has.Length.EqualTo(1),
            "PlayerInteractor must discover the relay on its start collider.");
        Assert.That(startZones[0].enabled && startZones[0].isTrigger, Is.True);
        Assert.That(startZones[0].radius, Is.GreaterThan(0));
        Assert.That(startZones[0], Is.Not.SameAs(relay.ArenaBounds));
    }
    [Test] public void LabResultNotificationHasVisibleAuthoredAncestors()
    {
        var support = AssetDatabase.LoadAssetAtPath<GameObject>(WorldSystemsLabRelaySupportAuthoring.SupportPath);
        Assert.That(support, Is.Not.Null);
        Assert.That(support.GetComponentsInChildren<MonoBehaviour>(true).All(component => component != null), Is.True);
        var view = support.GetComponentInChildren<RunMessageService>(true).View;
        Assert.That(view, Is.Not.Null);
        Assert.That(support.GetComponentInChildren<UpgradeManager>(true), Is.Not.Null);
        foreach (var group in view.GetComponentsInParent<CanvasGroup>(true))
            if (group.gameObject != view.gameObject)
                Assert.That(group.alpha, Is.EqualTo(1), "Result is hidden by " + group.name);
    }
    [Test] public void AuthoredPrefabHasValidReferences()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<OrbitalRelayEvent>(OrbitalRelayAuthoring.PrefabPath);
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.TryValidateConfiguration(out string error), Is.True, error);
        Assert.That(prefab.GetComponentsInChildren<MonoBehaviour>(true).All(component => component != null), Is.True);
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
            OrbitalRelayAuthoring.Set(relay, "nodes", new[] { node, null });
            Assert.That(relay.TryValidateConfiguration(out _), Is.False, "Missing authored Nodes must be rejected.");
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
}
#endif
