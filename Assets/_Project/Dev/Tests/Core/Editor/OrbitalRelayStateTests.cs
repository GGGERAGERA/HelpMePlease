#if UNITY_EDITOR
using NUnit.Framework;

public sealed class OrbitalRelayStateTests
{
    private static OrbitalRelaySettings Settings => new(3, 20, .6f, .75f, 15, 10, 2.5f, 1.6f);
    private static OrbitalRelayState Create()
    {
        var state = new OrbitalRelayState(Settings, 2, previous => previous == 0 ? 1 : 0);
        state.Start(); return state;
    }
    private static void Stabilize(OrbitalRelayState state)
    { for (int i = 0; i < 3; i++) state.Tick(.6f, true); }
    [Test] public void ThresholdStartsTransitionNotCompletion()
    {
        var s = Create(); Stabilize(s);
        Assert.That(s.Snapshot.Phase, Is.EqualTo(OrbitalRelayPhase.Transition));
        Assert.That(s.Snapshot.BonusActivations, Is.Zero); Assert.That(s.Result, Is.Null);
        s.Tick(.75f, true);
        Assert.That(s.Snapshot.Phase, Is.EqualTo(OrbitalRelayPhase.Bonus));
        Assert.That(s.Snapshot.RemainingTime, Is.EqualTo(15)); Assert.That(s.Snapshot.Combo, Is.Zero);
    }
    [Test] public void BonusCountsOnlyBonusActivations()
    {
        var s = Create(); Stabilize(s); s.Tick(.75f, false);
        for (int i = 0; i < 4; i++) s.Tick(.6f, true);
        s.Tick(15, false);
        Assert.That(s.Result.Value.Gold, Is.EqualTo(40));
        Assert.That(s.Result.Value.UpgradeSelections, Is.EqualTo(1));
        Assert.That(s.Result.Value.BonusActivations, Is.EqualTo(4));
    }
    [Test] public void TimeoutFailsBeforeThreshold()
    {
        var s = Create(); s.Tick(.6f, true); s.Tick(.6f, true); s.Tick(20, false);
        Assert.That(s.Snapshot.Phase, Is.EqualTo(OrbitalRelayPhase.Failed));
        Assert.That(s.Result.Value.Gold, Is.Zero); Assert.That(s.Result.Value.UpgradeSelections, Is.Zero);
    }
    [Test] public void ContactBreakAndPauseDoNotGrantProgress()
    {
        var s = Create(); s.Tick(.3f, true); s.Tick(.1f, false); s.Tick(.3f, true);
        Assert.That(s.Snapshot.StabilizationActivations, Is.Zero);
        var before = s.Snapshot; s.Tick(0, false);
        Assert.That(s.Snapshot.ContactProgress, Is.EqualTo(before.ContactProgress));
        Assert.That(s.Snapshot.RemainingTime, Is.EqualTo(before.RemainingTime));
    }
    [Test] public void ComboExpiresWithoutScoreLossAndTwoNodesAlternate()
    {
        var s = Create(); int first = s.Snapshot.ActiveNodeIndex;
        s.Tick(.6f, true); Assert.That(s.Snapshot.ActiveNodeIndex, Is.Not.EqualTo(first));
        s.Tick(.6f, true); Assert.That(s.Snapshot.ActiveNodeIndex, Is.EqualTo(first));
        Assert.That(s.Snapshot.Combo, Is.EqualTo(2)); s.Tick(2.5f, false);
        Assert.That(s.Snapshot.Combo, Is.Zero); Assert.That(s.Snapshot.StabilizationActivations, Is.EqualTo(2));
    }
    [Test] public void BoundaryContactWinsBeforeTimeout()
    {
        var settings = new OrbitalRelaySettings(1, .6f, .6f, .75f, 15, 10, 2.5f, 1.6f);
        var s = new OrbitalRelayState(settings, 2, p => p == 0 ? 1 : 0); s.Start(); s.Tick(.6f, true);
        Assert.That(s.Snapshot.Phase, Is.EqualTo(OrbitalRelayPhase.Transition));
    }
    [Test] public void CancelAfterQualificationAndAdministrativeDisposalDiffer()
    {
        var s = Create(); Stabilize(s); s.CancelGameplay();
        Assert.That(s.Result.Value.UpgradeSelections, Is.EqualTo(1));
        var disposed = Create(); Stabilize(disposed); disposed.DisposeWithoutRewards();
        Assert.That(disposed.Result, Is.Null);
        s.Tick(100, true); Assert.That(s.Result.Value.Gold, Is.Zero);
    }
    [Test] public void InvalidSettingsAndOverflowAreSafe()
    {
        var invalid = new OrbitalRelaySettings(0, 20, float.NaN, .75f, 15, 10, 2.5f, 1.6f);
        Assert.That(invalid.TryValidate(out _), Is.False);
        Assert.That(OrbitalRelayResult.Calculate(true, int.MaxValue, int.MaxValue).Gold, Is.EqualTo(int.MaxValue));
    }
}
#endif
