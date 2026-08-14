using System;
using UnityEngine;

[Serializable]
public struct AnomalyArtHookSet
{
    [Tooltip("Optional prefab containing boundary accent sprites.")]
    [SerializeField] private GameObject boundaryAccentPrefab;

    [Tooltip("Optional prefab containing reusable pattern marks.")]
    [SerializeField] private GameObject patternAccentPrefab;

    [Tooltip("Optional center/core accent prefab.")]
    [SerializeField] private GameObject centerAccentPrefab;

    [Tooltip("Optional decorative-only FX root. Gameplay must not depend on it.")]
    [SerializeField] private GameObject decorativeFxPrefab;

    public GameObject BoundaryAccentPrefab => boundaryAccentPrefab;
    public GameObject PatternAccentPrefab => patternAccentPrefab;
    public GameObject CenterAccentPrefab => centerAccentPrefab;
    public GameObject DecorativeFxPrefab => decorativeFxPrefab;

    public AnomalyArtHookSet(
        GameObject boundaryAccent,
        GameObject patternAccent,
        GameObject centerAccent,
        GameObject decorativeFx)
    {
        boundaryAccentPrefab = boundaryAccent;
        patternAccentPrefab = patternAccent;
        centerAccentPrefab = centerAccent;
        decorativeFxPrefab = decorativeFx;
    }
}

/// <summary>
/// Presentation-only adapter for modular artist-authored anomaly accents.
/// Empty slots intentionally produce empty roots and never affect gameplay.
/// </summary>
public sealed class AnomalyArtHooks : MonoBehaviour
{
    private Transform boundaryRoot;
    private Transform patternRoot;
    private Transform centerRoot;
    private Transform fxRoot;

    public int RootCount => 4;
    public int InstantiatedArtCount { get; private set; }
    public bool IsVisible => gameObject.activeSelf;

    public static AnomalyArtHooks Create(
        Transform parent,
        AnomalyArtHookSet hooks,
        string typeName)
    {
        if (parent == null)
            return null;

        GameObject root = new($"{typeName} Art Hooks");
        root.transform.SetParent(parent, false);
        AnomalyArtHooks result = root.AddComponent<AnomalyArtHooks>();
        result.Initialize(hooks);
        return result;
    }

    public void SetVisible(bool visible)
    {
        gameObject.SetActive(visible);
    }

    /// <summary>
    /// Fits an artist-authored unit-size boundary prefab to the anomaly area.
    /// Other hook roots keep their authored scale.
    /// </summary>
    public void SetBoundarySize(Vector2 worldSize)
    {
        if (boundaryRoot == null)
            return;

        boundaryRoot.localScale = new Vector3(
            Mathf.Max(0.01f, worldSize.x),
            Mathf.Max(0.01f, worldSize.y),
            1f
        );
    }

    /// <summary>
    /// Aligns a unit-length, +X-authored pattern prefab to a live hazard segment.
    /// This is presentation-only and never feeds collision or damage geometry.
    /// </summary>
    public void AlignPatternToWorldSegment(Vector2 start, Vector2 end)
    {
        if (patternRoot == null)
            return;

        Vector2 delta = end - start;
        patternRoot.position = (start + end) * 0.5f;
        patternRoot.rotation = Quaternion.Euler(
            0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        patternRoot.localScale = new Vector3(
            Mathf.Max(0.01f, delta.magnitude), 1f, 1f);
    }

    private void Initialize(AnomalyArtHookSet hooks)
    {
        boundaryRoot = CreateRoot("BOUNDARY Accents");
        patternRoot = CreateRoot("PATTERN Accents");
        centerRoot = CreateRoot("CENTER Accent");
        fxRoot = CreateRoot("FX Decorative");

        InstantiateOptional(hooks.BoundaryAccentPrefab, boundaryRoot);
        InstantiateOptional(hooks.PatternAccentPrefab, patternRoot);
        InstantiateOptional(hooks.CenterAccentPrefab, centerRoot);
        InstantiateOptional(hooks.DecorativeFxPrefab, fxRoot);
    }

    private Transform CreateRoot(string rootName)
    {
        GameObject root = new(rootName);
        root.transform.SetParent(transform, false);
        return root.transform;
    }

    private void InstantiateOptional(GameObject prefab, Transform parent)
    {
        if (prefab == null || parent == null)
            return;

        Instantiate(prefab, parent, false);
        InstantiatedArtCount++;
    }
}
