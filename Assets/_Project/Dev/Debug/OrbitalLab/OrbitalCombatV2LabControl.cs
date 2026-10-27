#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Subject42.Combat.OrbitalStation;

// Bound only by EnemyOrbitalLab; no time owner, persistent state or combat loop.
public sealed class OrbitalCombatV2LabControl : IDisposable
{
    public const float SlowMultiplier = .4f;
    private readonly OrbitalStationRuntime station;
    private bool needsRelease = true, disposed;
    public bool UsesBulletTime { get; private set; }
    public bool IsSlowingOrbits { get; private set; }

    public OrbitalCombatV2LabControl(OrbitalStationRuntime station)
    {
        this.station = station;
        if (station.LabShiftInput != null) throw new InvalidOperationException("Lab Shift already has an owner.");
        station.LabShiftInput = RouteShift;
        station.LabInputSuspended = Suspend;
    }

    // The station supplies its existing input eligibility; false never blocks RMB.
    public bool RouteShift(float deltaTime, bool held, bool allowed)
    {
        if (disposed) return false;
        if (!allowed || deltaTime <= 0f) { Suspend(); return false; }
        if (!held)
        {
            needsRelease = false;
            SetRotation(false);
            return false;
        }
        if (needsRelease) return false;
        SetRotation(!UsesBulletTime);
        return UsesBulletTime;
    }

    public void SetBulletTime(bool value)
    {
        if (UsesBulletTime == value) return;
        Suspend();
        UsesBulletTime = value;
    }

    public void Suspend()
    {
        needsRelease = true;
        SetRotation(false);
        if (station != null && UsesBulletTime)
        {
            station.BulletTime.Release();
            station.InputOwner?.ReleaseBulletTime();
        }
    }

    private void SetRotation(bool slow)
    {
        IsSlowingOrbits = slow;
        if (station == null) return;
        foreach (var ring in station.Rings) ring.LabRotationMultiplier = slow ? SlowMultiplier : 1f;
    }

    public void Dispose()
    {
        if (disposed) return;
        Suspend();
        if (station != null)
        {
            if (station.LabShiftInput == RouteShift) station.LabShiftInput = null;
            if (station.LabInputSuspended == Suspend) station.LabInputSuspended = null;
        }
        disposed = true;
    }
}
#endif
