#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
public static class CorridorBotSteering
{
    public static Vector2 Desired(CorridorNavigationSnapshot snapshot, Vector2 position)
    {
        var route = snapshot.Route;
        float along = route.Project(position);
        float objectiveDistance = snapshot.Completed < route.CheckpointCount ? route.CheckpointDistance(snapshot.Completed)+1 :
            route.Length + (snapshot.ExitOpen ? 1 : -2);
        // Stop at corners before following the next segment: never take a chord through walls.
        float walked = 0, lookahead = Mathf.Min(objectiveDistance, along + 3);
        for (int i = 1; i < route.Vertices.Count; i++)
        {
            walked += Vector2.Distance(route.Vertices[i-1],route.Vertices[i]);
            if (walked > along+.25f) { lookahead = Mathf.Min(lookahead,walked); break; }
        }
        Vector2 target = route.Sample(lookahead);
        if (snapshot.Completed < route.CheckpointCount && along >= route.CheckpointDistance(snapshot.Completed)-3)
            target = snapshot.Objective;
        if (snapshot.Completed == route.CheckpointCount && along >= route.Length-2.25f) target = snapshot.Objective;
        foreach (var threat in snapshot.Threats)
        {
            if (Vector2.Distance(target,threat.Position) > threat.Radius+1) continue;
            route.Point(lookahead,out var forward); Vector2 side = new(-forward.y,forward.x);
            Vector2 left = target + side*(route.HalfWidth-1), right = target-side*(route.HalfWidth-1);
            var safer = Vector2.Distance(left,threat.Position)>Vector2.Distance(right,threat.Position) ? left:right;
            if (route.StaysInside(position,safer)) target = safer;
        }
        Vector2 delta = target-position;
        if (delta.magnitude <= .25f) return Vector2.zero;
        return delta.normalized * (along <= snapshot.FrontDistance+2 ? 1.5f : 1);
    }
}
#endif
