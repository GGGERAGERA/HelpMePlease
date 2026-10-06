using UnityEngine;
public enum CorridorPhase { Inactive, Running, FinalPush, Finishing, Completed, Failed, Cancelled }
public sealed class CorridorRuntimeState
{
    public CorridorPhase Phase { get; private set; }
    public int Completed { get; private set; }
    public float FinalPushRemaining { get; private set; }
    public bool ExitOpen => Phase == CorridorPhase.FinalPush && FinalPushRemaining <= 0;
    private float finishing;
    private readonly CorridorRoute route;
    private readonly CorridorSettings settings;
    public CorridorRuntimeState(CorridorRoute route, CorridorSettings settings)
    { this.route = route; this.settings = settings; FinalPushRemaining = settings.finalPushDuration; }
    public void Start() { if (Phase == CorridorPhase.Inactive) Phase = CorridorPhase.Running; }
    public bool TryAdvance(Vector2 previous, Vector2 current)
    {
        if (Phase != CorridorPhase.Running || Completed >= route.CheckpointCount ||
            !new CorridorGate(route, Completed).Crossed(previous, current)) return false;
        if (++Completed == route.CheckpointCount) Phase = CorridorPhase.FinalPush;
        return true;
    }
    public void Tick(float deltaTime)
    {
        if (Phase == CorridorPhase.FinalPush) FinalPushRemaining = Mathf.Max(0, FinalPushRemaining - deltaTime);
        if (Phase == CorridorPhase.Finishing && (finishing -= deltaTime) <= 0) Phase = CorridorPhase.Completed;
    }
    public bool TryBeginFinish(Vector2 previous, Vector2 current)
    {
        if (!new CorridorExit(route).Crossed(previous, current, ExitOpen)) return false;
        Phase = CorridorPhase.Finishing; finishing = .45f; return true;
    }
    public void Terminate(CorridorPhase phase)
    {
        if (Phase == CorridorPhase.Completed || Phase == CorridorPhase.Failed || Phase == CorridorPhase.Cancelled) return;
        if (phase == CorridorPhase.Failed || phase == CorridorPhase.Cancelled) Phase = phase;
    }
}
