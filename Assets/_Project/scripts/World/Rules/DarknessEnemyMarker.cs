using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Authored enemy eye presentation. Never changes animation colors or combat state.</summary>
public sealed class DarknessEnemyMarker : MonoBehaviour
{
    [SerializeField] private SortingGroup bodyGroup;
    [SerializeField] private SortingGroup[] eyeGroups = System.Array.Empty<SortingGroup>();
    [SerializeField] private SpriteRenderer[] eyes = System.Array.Empty<SpriteRenderer>();
    [SerializeField] private GameObject fallbackEyes;
    [SerializeField] private Material eyeMaterial;
    private Material[] originalMaterials;
    private int originalLayer;
    private bool active;

    public void SetActive(bool value, float intensity)
    {
        value &= intensity > 0f;
        if (value == active) return;
        active = value;
        if (value)
        {
            if (bodyGroup != null)
            {
                originalLayer = bodyGroup.sortingLayerID;
                bodyGroup.sortingLayerName = "Default";
            }
            originalMaterials = new Material[eyes.Length];
            for (int i = 0; i < eyes.Length; i++)
            {
                originalMaterials[i] = eyes[i].sharedMaterial;
                eyes[i].sharedMaterial = eyeMaterial;
            }
        }
        else
        {
            if (bodyGroup != null) bodyGroup.sortingLayerID = originalLayer;
            for (int i = 0; i < eyes.Length; i++)
                eyes[i].sharedMaterial = originalMaterials[i];
        }
        foreach (var group in eyeGroups) group.enabled = value;
        if (fallbackEyes != null) fallbackEyes.SetActive(value);
    }

    private void OnDisable() => SetActive(false, 0f);
}
