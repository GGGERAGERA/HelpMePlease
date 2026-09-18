using System;
using UnityEngine;

// Energy belongs to the station; OrbitalInteractionController owns time changes.
[Serializable]
public sealed class BulletTimeAbility
{
    [SerializeField, Min(.1f)] private float capacitySeconds = 3f;
    [SerializeField, Min(.1f)] private float rechargeSeconds = 6f;
    [SerializeField, Range(.1f, 1f)] private float worldTimeScale = .4f;
    [SerializeField, Min(.01f)] private float transitionSeconds = .18f;
    private float energy = 1f;
    public float Energy => energy;
    public float WorldTimeScale => worldTimeScale;
    public float TransitionSeconds => transitionSeconds;
    public bool IsActive { get; private set; }
    public bool HasBeenUsed { get; private set; }

    public void Tick(bool held, float unscaledDeltaTime)
    {
        IsActive = held && energy > 0f && unscaledDeltaTime > 0f;
        if (IsActive)
        {
            HasBeenUsed = true;
            energy = Mathf.Max(0f, energy - unscaledDeltaTime / capacitySeconds);
            IsActive = energy > 0f;
        }
        else if (!held)
            energy = Mathf.Min(1f, energy + Mathf.Max(0f, unscaledDeltaTime) / rechargeSeconds);
    }

    public void Release() => IsActive = false;
    public void Reset()
    {
        Release();
        energy = 1f;
        HasBeenUsed = false;
    }
}
