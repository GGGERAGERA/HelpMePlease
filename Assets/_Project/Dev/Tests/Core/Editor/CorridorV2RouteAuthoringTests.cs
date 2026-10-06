#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
public sealed class CorridorRoutePlacementTests
{
    [Test] public void PresetsSeedRotationAndWholeRouteAdmission()
    {
        var settings = AssetDatabase.LoadAssetAtPath<CorridorConfig>(CorridorMigrationAuthoring.ConfigPath).settings;
        int turns = 0;
        foreach (CorridorRouteMode mode in System.Enum.GetValues(typeof(CorridorRouteMode)))
        {
            Assert.That(CorridorRouteBuilder.TryBuild(settings,mode,42,turns,Vector2.zero,out var route,out var error),Is.True,error);
            Assert.That(CorridorRouteBuilder.TryBuild(settings,mode,42,turns,Vector2.zero,out var replay,out _),Is.True);
            CollectionAssert.AreEqual(route.Vertices,replay.Vertices);
            Assert.That(route.Length,Is.EqualTo(129).Within(.001)); Assert.That(route.CheckpointCount,Is.EqualTo(3));
            Assert.That(CorridorRouteBuilder.TryBuild(settings,mode,42,turns+1,Vector2.zero,out var rotated,out _),Is.True);
            for (int i=0;i<route.Vertices.Count;i++) Assert.That(Vector2.Distance(rotated.Vertices[i],new Vector2(-route.Vertices[i].y,route.Vertices[i].x)),Is.LessThan(.001));
            var context = new WorldEventPlacementContext(Vector2.zero,new Rect(-200,-200,400,400),new Rect(-1,-1,2,2),42,_=>true);
            Assert.That(CorridorPlacement.Admitted(route,context,out _),Is.True,"Site constrains only start.");
            var blocked = new WorldEventPlacementContext(Vector2.zero,context.PlayableArea,null,42,_=>false);
            Assert.That(CorridorPlacement.Admitted(route,blocked,out _),Is.False);
            turns++;
        }
        var straight = new CorridorRoute(settings,0,Vector2.zero); // Explicit Straight, not config Random.
        var copy = settings.Snapshot(); copy.routePreset = CorridorRouteMode.Straight;
        straight = new CorridorRoute(copy,0,Vector2.zero);
        Assert.That(CorridorPlacement.Admitted(straight,new WorldEventPlacementContext(Vector2.zero,new Rect(-50,-50,100,100),null,42,null),out _),Is.False);
        copy.routeDefinitions = null;
        Assert.That(CorridorRouteBuilder.TryBuild(copy,CorridorRouteMode.Straight,42,0,Vector2.zero,out _,out _),Is.False);
    }
}
#endif
