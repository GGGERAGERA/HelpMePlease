#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class RocketStormPlacementTests
{
    private RocketStormEvent storm;

    [SetUp]
    public void CreateProductionEvent()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/Environment/WorldEvents/RocketStormEvent.prefab");
        storm = Object.Instantiate(prefab).GetComponent<RocketStormEvent>();
    }

    [TearDown]
    public void DestroyProductionEvent()
    {
        if (storm != null) Object.DestroyImmediate(storm.gameObject);
    }

    [Test]
    public void BlockedPreferredArenaUsesClearFullSizeAreaInSameTerritory()
    {
        // The 2.5 m beacon is clear at the origin; the 24 x 20 arena hits this obstacle.
        Rect obstacle = new(7f, -2f, 2f, 4f);
        Rect site = new(-40f, -20f, 80f, 40f);
        var context = new WorldEventPlacementContext(Vector2.zero, site, site, 42,
            footprint => !footprint.Overlaps(obstacle));

        Assert.That(storm.TryPreparePlacement(context, out string error), Is.True, error);

        Rect arena = ReservedArena();
        Assert.That(arena.size, Is.EqualTo(new Vector2(24f, 20f)));
        Assert.That(arena.Overlaps(obstacle), Is.False);
        AssertInsideWithPlayerMargin(arena, site);
        Assert.That((Vector2)storm.transform.position, Is.EqualTo(arena.center));
    }

    [Test]
    public void NarrowTerritorySearchesAlongFreeAxisToKeepReservedExitAccessible()
    {
        // Every beacon x projects onto the same arena x, while moving y is still possible.
        Rect site = new(-13f, -30f, 26f, 60f);
        Vector2 exit = new(0f, -7f);
        const float reservedExitClearance = 4f;
        var context = new WorldEventPlacementContext(Vector2.zero, site, site, 42,
            footprint => DistanceToRect(exit, footprint) >= reservedExitClearance);

        Assert.That(storm.TryPreparePlacement(context, out string error), Is.True, error);

        Rect arena = ReservedArena();
        Assert.That(arena.size, Is.EqualTo(new Vector2(24f, 20f)));
        Assert.That(DistanceToRect(exit, arena), Is.GreaterThanOrEqualTo(reservedExitClearance));
        AssertInsideWithPlayerMargin(arena, site);
    }

    [Test]
    public void FullyBlockedTerritoryRejectsPlacement()
    {
        Rect site = new(-13f, -11f, 26f, 22f);
        Rect obstacle = new(-1f, -1f, 2f, 2f);
        var context = new WorldEventPlacementContext(Vector2.zero, site, site, 42,
            footprint => !footprint.Overlaps(obstacle));

        Assert.That(storm.TryPreparePlacement(context, out string error), Is.False);
        Assert.That(error, Is.Not.Null.And.Not.Empty);
    }

    private Rect ReservedArena()
    {
        var footprints = new List<Rect>();
        storm.CollectReservedFootprints(footprints);
        Assert.That(footprints, Has.Count.EqualTo(1));
        return footprints[0];
    }

    private static void AssertInsideWithPlayerMargin(Rect arena, Rect site)
    {
        Assert.That(arena.xMin, Is.GreaterThanOrEqualTo(site.xMin + 1f));
        Assert.That(arena.yMin, Is.GreaterThanOrEqualTo(site.yMin + 1f));
        Assert.That(arena.xMax, Is.LessThanOrEqualTo(site.xMax - 1f));
        Assert.That(arena.yMax, Is.LessThanOrEqualTo(site.yMax - 1f));
    }

    private static float DistanceToRect(Vector2 point, Rect rect) => Vector2.Distance(point,
        new Vector2(Mathf.Clamp(point.x, rect.xMin, rect.xMax),
            Mathf.Clamp(point.y, rect.yMin, rect.yMax)));
}
#endif
