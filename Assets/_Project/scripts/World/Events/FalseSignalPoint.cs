using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class FalseSignalPoint : MonoBehaviour
{
    [Header("Test Visual")]
    [SerializeField] private Material lineMaterial;
    [SerializeField] private Color color = new(1f, 0.35f, 0.1f, 0.9f);
    [SerializeField, Min(0.1f)] private float visualRadius = 0.75f;
    [SerializeField, Min(8)] private int segments = 32;

    private FalseSignalEvent owner;
    private bool isReal;
    private bool consumed;

    public void Initialize(FalseSignalEvent eventOwner, bool realSignal)
    {
        owner = eventOwner;
        isReal = realSignal;
        consumed = false;
    }

    private void Awake()
    {
        BuildVisual();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (consumed || owner == null || !other.CompareTag("Player"))
            return;

        consumed = true;
        FalseSignalEvent eventOwner = owner;
        owner = null;
        eventOwner.ResolveSignal(this, isReal);
        Destroy(gameObject);
    }

    private void BuildVisual()
    {
        if (lineMaterial == null)
            return;

        LineRenderer line = gameObject.AddComponent<LineRenderer>();
        line.sharedMaterial = lineMaterial;
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = Mathf.Max(8, segments);
        line.startWidth = 0.12f;
        line.endWidth = 0.12f;
        line.startColor = color;
        line.endColor = color;
        line.sortingLayerName = "Midground";
        line.sortingOrder = 2;

        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / line.positionCount;
            line.SetPosition(
                i,
                new Vector3(
                    Mathf.Cos(angle) * visualRadius,
                    Mathf.Sin(angle) * visualRadius,
                    0f
                )
            );
        }
    }
}
