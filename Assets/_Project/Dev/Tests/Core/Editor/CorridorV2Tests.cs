#if UNITY_EDITOR
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
public sealed class CorridorGameplayTests
{
    [Test] public void OrderedCrossingNoShortcutFinalPushAndExit()
    {
        var settings=AssetDatabase.LoadAssetAtPath<CorridorConfig>(CorridorMigrationAuthoring.ConfigPath).settings.Snapshot();
        settings.routePreset=CorridorRouteMode.L;
        var route=new CorridorRoute(settings,0,Vector2.zero); var state=new CorridorRuntimeState(route,settings); state.Start();
        Vector2 second=route.Point(route.CheckpointDistance(1),out var forward);
        Assert.That(state.TryAdvance(second-forward,second+forward),Is.False);
        Vector2 corner=route.Vertices[2]; Assert.That(route.StaysInside(corner-Vector2.right*8.4f,corner+Vector2.up*8.4f),Is.False);
        Vector2 exit=route.Point(route.Length,out var end);
        Assert.That(state.TryBeginFinish(exit-end,exit+end),Is.False);
        for(int i=0;i<3;i++)
        {
            Vector2 gate=route.Point(route.CheckpointDistance(i),out var direction);
            Assert.That(state.TryAdvance(gate+direction,gate-direction),Is.False);
            Assert.That(state.TryAdvance(route.Vertices[0],gate+direction),Is.False,"Teleport cannot progress.");
            Assert.That(state.TryAdvance(gate-direction,gate+direction),Is.True);
            Assert.That(state.Completed,Is.EqualTo(i+1));
        }
        Assert.That(state.Phase,Is.EqualTo(CorridorPhase.FinalPush)); Assert.That(state.ExitOpen,Is.False);
        state.Tick(3.49f); Assert.That(state.ExitOpen,Is.False); state.Tick(.02f); Assert.That(state.ExitOpen,Is.True);
        Assert.That(state.TryBeginFinish(exit+end,exit-end),Is.False);
        Assert.That(state.TryBeginFinish(exit-end,exit+end),Is.True); state.Tick(.45f);
        Assert.That(state.Phase,Is.EqualTo(CorridorPhase.Completed));
        // Same fixture checks honest steering around the L bend: movement alone crosses each gate.
        var steeringState=new CorridorRuntimeState(route,settings); steeringState.Start();
        Vector2 actor=route.Vertices[0];
        for(int tick=0;tick<2000&&steeringState.Completed<3;tick++)
        {
            var snapshot=new CorridorNavigationSnapshot(route,steeringState,0,System.Array.Empty<CorridorStrikeThreat>());
            Vector2 next=actor+Vector2.ClampMagnitude(CorridorBotSteering.Desired(snapshot,actor),1)*.1f;
            Assert.That(route.StaysInside(actor,next),Is.True,"Steering never takes a shortcut through walls.");
            steeringState.TryAdvance(actor,next); actor=next;
        }
        Assert.That(steeringState.Completed,Is.EqualTo(3),"Steering must cross gates, including the corner gate.");
    }
}
#endif
