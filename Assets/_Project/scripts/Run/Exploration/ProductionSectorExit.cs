using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public sealed class ProductionSectorExit : MonoBehaviour
{
    private static readonly List<ProductionSectorExit> activeExits = new();

    private RunFlowController runFlow;
    private Material material;
    private LineRenderer ring;
    private float pulse;
    private bool initialized;

    public static IReadOnlyList<ProductionSectorExit> ActiveExits =>
        activeExits;
    public bool IsMapVisible => initialized && isActiveAndEnabled;

    private void OnEnable()
    {
        if (!activeExits.Contains(this))
            activeExits.Add(this);
    }

    private void OnDisable()
    {
        activeExits.Remove(this);
    }

    public void Initialize(
        Vector2 position,
        float radius,
        RunFlowController flow)
    {
        transform.position = position;
        runFlow = flow;
        initialized = true;

        CircleCollider2D trigger = GetComponent<CircleCollider2D>();
        trigger.isTrigger = true;
        trigger.radius = Mathf.Max(0.5f, radius);

        material = AnomalyPowerVisuals.CreateMaterial(
            "Sector Exit Runtime Material"
        );
        ring = AnomalyPowerVisuals.CreateLine(
            transform,
            "Exit Ring",
            new Color(0.2f, 1f, 0.45f, 1f),
            0.16f,
            33,
            material
        );
        ring.useWorldSpace = false;

        for (int i = 0; i < ring.positionCount; i++)
        {
            float angle = i / (float)(ring.positionCount - 1) *
                Mathf.PI * 2f;
            ring.SetPosition(i, new Vector3(
                Mathf.Cos(angle) * trigger.radius,
                Mathf.Sin(angle) * trigger.radius,
                0f
            ));
        }
    }

    private void Update()
    {
        pulse += Time.deltaTime * 3f;
        float scale = 1f + Mathf.Sin(pulse) * 0.08f;

        if (ring != null)
            ring.transform.localScale = Vector3.one * scale;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponentInParent<PlayerHealth>() == null)
            return;

        if (runFlow != null && runFlow.HandleExitReached())
            enabled = false;
    }

    private void OnDestroy()
    {
        activeExits.Remove(this);

        if (material != null)
            Destroy(material);
    }
}
