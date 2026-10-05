using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WorldEventPressureModifier : MonoBehaviour
{
    private IDisposable lease;
    public void SetBonusActive(WorldEventSpawner spawner, WorldEvent source, bool active, float multiplier)
    {
        if (!active) { Release(); return; }
        if (lease == null && spawner != null) lease = spawner.AcquireSpawnPressure(source, multiplier);
    }
    public void Release() { lease?.Dispose(); lease = null; }
    private void OnDisable() => Release();
    private void OnDestroy() => Release();
}
