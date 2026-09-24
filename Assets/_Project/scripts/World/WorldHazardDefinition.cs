using System;
using UnityEngine;

/// <summary>Add other boss hazards by providing a definition and runtime; the director is type-agnostic.</summary>
public abstract class WorldHazardDefinition : ScriptableObject
{
    public abstract float DangerRadius { get; }
    public abstract bool IsConfigured { get; }
    public abstract IWorldHazardAttack CreateAttack(MonoBehaviour owner);
}

public interface IWorldHazardAttack : IDisposable
{
    bool IsBusy { get; }
    bool TryStart(Vector3 target);
    void Tick(float deltaTime, Func<bool> combatAllowed);
    void Cancel();
}
