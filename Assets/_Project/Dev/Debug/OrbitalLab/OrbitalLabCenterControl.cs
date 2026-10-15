#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Subject42.Combat.OrbitalStation;
using UnityEngine;

// Retains lab callers/tuning while production owns input and center movement.
public sealed class OrbitalLabCenterControl : IDisposable
{
    private readonly OrbitalCenterShift center;
    public Vector2 Offset => center != null ? center.Offset : Vector2.zero;
    public OrbitalLabCenterControl(OrbitalStationRuntime station, Camera camera, float speedMultiplier)
    {
        center = station.GetComponent<OrbitalCenterShift>();
        center.ConfigurePointerControl(camera, speedMultiplier);
        station.LabShiftBulletTime = true;
    }
    public void Tick(float maximumOffset, float moveSpeed, float returnSpeed) =>
        center.Configure(maximumOffset, moveSpeed, returnSpeed);
    // Unity's destroyed-object comparison is required when scene teardown retires the station first.
    public void ReleaseInput() { if (center != null) center.ResetOffset(); }
    public void Dispose() => ReleaseInput();
}
#endif
