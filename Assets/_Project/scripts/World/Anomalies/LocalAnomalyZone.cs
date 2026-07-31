using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public abstract class LocalAnomalyZone : MonoBehaviour
{
    protected LocalAnomalyData Data { get; private set; }
    protected LevelAnomalyController Controller { get; private set; }

    public LocalAnomalyType AnomalyType => Data != null
        ? Data.AnomalyType
        : default;

    public void Initialize(
        LocalAnomalyData data,
        LevelAnomalyController controller)
    {
        Data = data;
        Controller = controller;
        InitializeFromData(data);
    }

    protected abstract void InitializeFromData(LocalAnomalyData data);

    public abstract void Despawn();
}
