#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using Subject42.Combat.OrbitalStation;
using UnityEngine;

// Retains lab callers/tuning while production owns input and center movement.
public sealed class OrbitalLabCenterControl : IDisposable
{
    private readonly OrbitalCenterShift center;
    public Vector2 Offset => center != null ? center.Offset : Vector2.zero;
    public OrbitalLabCenterControl(OrbitalStationRuntime station, Transform player)
    {
        center = station.GetComponent<OrbitalCenterShift>();
    }
    public void Tick(float maximumOffset, float moveSpeed, float returnSpeed) =>
        center.Configure(maximumOffset, moveSpeed, returnSpeed);
    public void ReleaseInput() => center?.ResetOffset();
    public void Dispose() => ReleaseInput();
}
#endif
