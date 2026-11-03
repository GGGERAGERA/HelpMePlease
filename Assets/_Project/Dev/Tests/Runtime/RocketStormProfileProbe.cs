#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Profiling;
using UnityEngine;

// Frame tags only, for the opt-in Editor profiling scenario (no gameplay changes).
[DefaultExecutionOrder(32700)]
public sealed class RocketStormProfileProbe : MonoBehaviour
{
    public RocketStormEvent Storm;
    public bool Recording;
    public int WarningCount, FallingCount, ImpactCount, Mismatches;
    public readonly List<Vector3> Targets = new();
    public readonly List<Vector3> ImpactTargets = new();
    public readonly List<string> Manifest = new();
    public readonly List<Observation> Frames = new(5000);
    public struct Observation { public int frame, stage; public float dt, elapsed; }
    static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly FieldInfo Runner = typeof(RocketStormEvent).GetField("runner",Flags), Elapsed = typeof(RocketStormEvent).GetField("elapsed",Flags);
    static readonly FieldInfo Shots = typeof(RocketAttackRunner).GetField("shots",Flags), Impacts = typeof(RocketAttackRunner).GetField("impacts",Flags);
    static readonly ProfilerMarker Observe = new("RocketProfile.Observe");
    static readonly ProfilerMarker[] Stages = { new("RocketProfile.Before"),new("RocketProfile.Warnings"),new("RocketProfile.PreImpact"),new("RocketProfile.Explosions") };
    readonly HashSet<object> seenShots = new(), seenImpacts = new();
    FieldInfo targetField, markerField, fallField, ageField, impactItem;
    void LateUpdate()
    {
        if (!Recording || Storm == null) return;
        using (Observe.Auto())
        {
            var runner = Runner.GetValue(Storm);
            var shots = (IList)Shots.GetValue(runner);
            var impacts = (IList)Impacts.GetValue(runner);
            foreach (var shot in shots)
            {
                if (targetField == null)
                {
                    var type = shot.GetType(); targetField = type.GetField("target",Flags); markerField = type.GetField("marker",Flags);
                    fallField = type.GetField("falling",Flags); ageField = type.GetField("age",Flags);
                }
                if (fallField.GetValue(shot) != null) FallingCount = Math.Max(FallingCount,shots.Count);
                if (!seenShots.Add(shot)) continue;
                var target = (Vector3)targetField.GetValue(shot); var marker = (PooledGameObject)markerField.GetValue(shot);
                Targets.Add(target); WarningCount++;
                if (marker == null || Vector3.Distance(marker.transform.position,target) > .001f || Mathf.Abs(marker.transform.localScale.x-4f) > .001f || Mathf.Abs(marker.transform.localScale.y-4f) > .001f) Mismatches++;
                Manifest.Add($"warning target={target.x:R},{target.y:R} diameter={marker.transform.localScale.x:R} delay={shot.GetType().GetField("delay",Flags).GetValue(shot)} fall={shot.GetType().GetField("duration",Flags).GetValue(shot)}");
            }
            foreach (var impact in impacts)
            {
                if (!seenImpacts.Add(impact)) continue;
                impactItem ??= impact.GetType().GetField("item",Flags);
                var item = (PooledGameObject)impactItem.GetValue(impact);
                var position = item.transform.position; ImpactTargets.Add(position); ImpactCount++;
                if (!Targets.Exists(t=>Vector3.Distance(t,position)<.001f)) Mismatches++;
            }
            float elapsed = (float)Elapsed.GetValue(Storm);
            int stage = ImpactCount > 0 ? 3 : FallingCount > 0 ? 2 : WarningCount > 0 ? 1 : 0;
            Frames.Add(new Observation { frame=Time.frameCount, stage=stage, dt=Time.unscaledDeltaTime, elapsed=elapsed });
            using (Stages[stage].Auto()) { }
        }
    }
}
#endif
