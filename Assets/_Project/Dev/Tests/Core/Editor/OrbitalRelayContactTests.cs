#if UNITY_EDITOR
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Subject42.Combat.OrbitalStation;

public sealed class OrbitalRelayContactTests
{
    [Test] public void ReleaseContactApiExists()
    { Assert.That(typeof(OrbitalModuleRuntime).GetMethod("HasBodyContact"), Is.Not.Null); }
    private static bool Contact(SpriteRenderer sprite, Vector2 center, float radius)
    {
        Type detector = typeof(OrbitalModuleRuntime).Assembly.GetType("Subject42.Combat.OrbitalStation.OrbitalBodyContactDetector");
        Assert.That(detector, Is.Not.Null);
        return (bool)detector.GetMethod("IntersectsCircle", BindingFlags.Static | BindingFlags.Public)
            .Invoke(null, new object[] { sprite, center, radius });
    }
    [Test] public void RotatedBodyUsesOrientedRectangleAndRejectsInvalidRadius()
    {
        var root = new GameObject("contact fixture");
        Texture2D texture = new(10, 2);
        Sprite asset = Sprite.Create(texture, new Rect(0, 0, 10, 2), Vector2.one * .5f, 1f);
        try
        {
            var body = root.AddComponent<SpriteRenderer>(); body.sprite = asset;
            root.transform.rotation = Quaternion.Euler(0, 0, 45);
            Assert.That(Contact(body, new Vector2(3, -3), .1f), Is.False, "World AABB corner must not count.");
            Assert.That(Contact(body, Vector2.zero, .1f), Is.True);
            root.transform.localScale = new Vector3(2, .5f, 1);
            Assert.That(Contact(body, new Vector2(5, 5), .1f), Is.True);
            Assert.That(Contact(body, Vector2.zero, float.NaN), Is.False);
            Assert.That(Contact(body, Vector2.zero, -1), Is.False);
            body.enabled = false; Assert.That(Contact(body, Vector2.zero, .1f), Is.False);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); UnityEngine.Object.DestroyImmediate(asset); UnityEngine.Object.DestroyImmediate(texture); }
    }
}
#endif
