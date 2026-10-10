#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEngine;

public static class FalseSignalTestSupport
{
    public static IEnumerator ScanAndActivate(FalseSignalPoint point, Transform player)
    {
        var station=player.GetComponentInChildren<OrbitalStationRuntime>();
        Assert.That(station,Is.Not.Null);
        Move(player, point.transform.position - new Vector3(station.Rings[0].Radius,0));
        yield return CoreTestSupport.Await(()=>point.State==FalseSignalPointState.Verified);
        Move(player,point.transform.position);
        var owner=point.GetComponentInParent<FalseSignalEvent>();
        point.Interact();
        yield return CoreTestSupport.Await(()=>owner.IsCompleted);
    }
    public static void Move(Transform player,Vector2 position)
    {
        var body=player.GetComponent<Rigidbody2D>();
        body.position=position; player.position=position; Physics2D.SyncTransforms();
    }
}
#endif
