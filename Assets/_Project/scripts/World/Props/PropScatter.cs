using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authoring data for an editor-only prop scatter operation.
/// Generated objects are ordinary children and require no runtime processing.
/// </summary>
[AddComponentMenu("Level Design/Prop Scatter")]
public sealed class PropScatter : MonoBehaviour
{
    [SerializeField] private List<GameObject> sources = new List<GameObject>();
    [SerializeField, Min(0)] private int count = 30;

    [Header("Area (local XY)")]
    [SerializeField, Min(0f)] private float areaWidth = 6f;
    [SerializeField, Min(0f)] private float areaHeight = 3.4f;
    [SerializeField, Min(0f)] private float minDistanceFromCenter = 0.4f;
    [SerializeField, Min(0f)] private float maxDistanceFromCenter = 3f;

    [Header("Variation")]
    [SerializeField, Min(0f)] private float minScale = 0.92f;
    [SerializeField, Min(0f)] private float maxScale = 1.08f;
    [SerializeField] private bool randomRotation = true;
    [SerializeField, Range(0f, 1f)] private float centerBias = 0.7f;
    [SerializeField] private int seed = 123;

    [SerializeField, HideInInspector] private Transform generatedRoot;

    public IReadOnlyList<GameObject> Sources => sources;
    public int Count => count;
    public float AreaWidth => areaWidth;
    public float AreaHeight => areaHeight;
    public float MinDistanceFromCenter => minDistanceFromCenter;
    public float MaxDistanceFromCenter => maxDistanceFromCenter;
    public float MinScale => minScale;
    public float MaxScale => maxScale;
    public bool RandomRotation => randomRotation;
    public float CenterBias => centerBias;
    public int Seed => seed;
    public Transform GeneratedRoot => generatedRoot;

    public void SetGeneratedRoot(Transform value)
    {
        generatedRoot = value;
    }

    private void OnValidate()
    {
        count = Mathf.Max(0, count);
        areaWidth = Mathf.Max(0f, areaWidth);
        areaHeight = Mathf.Max(0f, areaHeight);
        minDistanceFromCenter = Mathf.Max(0f, minDistanceFromCenter);
        maxDistanceFromCenter = Mathf.Max(minDistanceFromCenter, maxDistanceFromCenter);
        minScale = Mathf.Max(0f, minScale);
        maxScale = Mathf.Max(minScale, maxScale);
    }
}
