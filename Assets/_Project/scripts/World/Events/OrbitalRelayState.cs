using System;

public enum OrbitalRelayPhase { Inactive, Stabilization, Transition, Bonus, Completed, Failed }

public readonly struct OrbitalRelaySnapshot
{
    public readonly OrbitalRelayPhase Phase;
    public readonly int ActiveNodeIndex, StabilizationActivations, BonusActivations, Combo;
    public readonly float RemainingTime, ContactProgress, ComboRemaining;
    public bool Stabilized => Phase == OrbitalRelayPhase.Transition || Phase == OrbitalRelayPhase.Bonus || Phase == OrbitalRelayPhase.Completed;
    internal OrbitalRelaySnapshot(OrbitalRelayPhase phase, int node, float remaining,
        float progress, int stabilization, int bonus, int combo, float comboRemaining)
    { Phase = phase; ActiveNodeIndex = node; RemainingTime = remaining; ContactProgress = progress;
      StabilizationActivations = stabilization; BonusActivations = bonus; Combo = combo; ComboRemaining = comboRemaining; }
}

// Pure gameplay clock. Unity adapters supply real contact and selection, never mutate counters.
public sealed class OrbitalRelayState
{
    private readonly OrbitalRelaySettings settings;
    private readonly int nodeCount;
    private readonly Func<int, int> selectNextNode;
    private OrbitalRelayPhase phase;
    private int node = -1, stabilization, bonus, combo;
    private float remaining, contact, comboRemaining;
    public OrbitalRelaySnapshot Snapshot => new(phase, node, remaining,
        contact / settings.ContactTime, stabilization, bonus, combo, comboRemaining);
    public OrbitalRelayResult? Result { get; private set; }
    public OrbitalRelayState(OrbitalRelaySettings settings, int nodeCount, Func<int, int> selectNextNode)
    {
        if (!settings.TryValidate(out string error)) throw new ArgumentException(error);
        if (nodeCount < 2 || selectNextNode == null) throw new ArgumentException("Relay requires two nodes and a selector.");
        this.settings = settings; this.nodeCount = nodeCount; this.selectNextNode = selectNextNode;
    }
    private void SelectNext()
    {
        int next = selectNextNode(node);
        if (next < 0 || next >= nodeCount || next == node) throw new InvalidOperationException("Relay selector repeated or returned an invalid node.");
        node = next;
    }
    public void Start()
    {
        if (phase != OrbitalRelayPhase.Inactive) return;
        SelectNext(); phase = OrbitalRelayPhase.Stabilization; remaining = settings.StabilizationDuration;
    }
    public void Tick(float deltaTime, bool hasContact)
    {
        if (phase == OrbitalRelayPhase.Inactive || phase == OrbitalRelayPhase.Completed || phase == OrbitalRelayPhase.Failed) return;
        if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || deltaTime < 0) throw new ArgumentOutOfRangeException(nameof(deltaTime));
        if (deltaTime == 0) return;
        float dt = Math.Min(deltaTime, remaining);
        remaining = Math.Max(0, remaining - dt);
        if (phase == OrbitalRelayPhase.Transition)
        {
            if (remaining <= 0) { phase = OrbitalRelayPhase.Bonus; remaining = settings.BonusDuration; combo = 0; comboRemaining = 0; }
            return;
        }
        comboRemaining = Math.Max(0, comboRemaining - dt);
        if (comboRemaining <= 0) combo = 0;
        contact = hasContact ? contact + dt : 0;
        // Small tolerance only compensates float sums at an exact clock boundary.
        if (contact + .000001f >= settings.ContactTime)
        {
            contact = 0;
            combo++; comboRemaining = settings.ComboWindow;
            if (phase == OrbitalRelayPhase.Stabilization) stabilization++; else bonus++;
            SelectNext();
            if (phase == OrbitalRelayPhase.Stabilization && stabilization >= settings.RequiredActivations)
            { phase = OrbitalRelayPhase.Transition; remaining = settings.TransitionDuration; return; }
        }
        if (remaining <= 0) Finish(phase == OrbitalRelayPhase.Bonus);
    }
    private void Finish(bool success)
    {
        phase = success ? OrbitalRelayPhase.Completed : OrbitalRelayPhase.Failed;
        Result = OrbitalRelayResult.Calculate(success, bonus, settings.GoldPerActivation);
        node = -1; contact = remaining = 0;
    }
    public void CancelGameplay()
    {
        if (phase == OrbitalRelayPhase.Completed || phase == OrbitalRelayPhase.Failed) return;
        Finish(phase == OrbitalRelayPhase.Transition || phase == OrbitalRelayPhase.Bonus);
    }
    public void DisposeWithoutRewards()
    {
        if (phase == OrbitalRelayPhase.Completed || phase == OrbitalRelayPhase.Failed) return;
        phase = Snapshot.Stabilized ? OrbitalRelayPhase.Completed : OrbitalRelayPhase.Failed;
        node = -1; contact = remaining = 0; Result = null;
    }
}
