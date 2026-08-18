using UnityEngine;

/// <summary>
/// Serialized map of the reusable station prefab regions.
/// The hierarchy and layout live in StationWindow.prefab.
/// </summary>
public sealed class StationUIShell : MonoBehaviour
{
    [SerializeField] private RectTransform header;
    [SerializeField] private RectTransform mainContent;
    [SerializeField] private RectTransform infoPanel;
    [SerializeField] private RectTransform stationProgressPanel;
    [SerializeField] private RectTransform footer;

    public RectTransform Header => header;
    public RectTransform MainContent => mainContent;
    public RectTransform InfoPanel => infoPanel;
    public RectTransform StationProgressPanel => stationProgressPanel;
    public RectTransform Footer => footer;
}
