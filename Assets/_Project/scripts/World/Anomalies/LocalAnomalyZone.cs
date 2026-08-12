using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public abstract class LocalAnomalyZone : MonoBehaviour
{
    protected LocalAnomalyData Data { get; private set; }
    protected LevelAnomalyController Controller { get; private set; }
    protected BoxCollider2D AreaCollider { get; private set; }
    protected Vector2 AreaSize { get; private set; }

    public LocalAnomalyType AnomalyType => Data != null
        ? Data.AnomalyType
        : default;
    public Collider2D FocusArea => AreaCollider;

    public void Initialize(
        LocalAnomalyData data,
        LevelAnomalyController controller,
        Vector2 areaSize)
    {
        Data = data;
        Controller = controller;
        AreaSize = new Vector2(
            Mathf.Max(0.1f, areaSize.x),
            Mathf.Max(0.1f, areaSize.y)
        );
        AreaCollider = GetComponent<BoxCollider2D>();

        if (AreaCollider != null)
        {
            AreaCollider.isTrigger = true;
            AreaCollider.size = AreaSize;
        }

        InitializeFromData(data, AreaSize);
    }

    protected abstract void InitializeFromData(
        LocalAnomalyData data,
        Vector2 areaSize);

    public abstract void Despawn();
}
