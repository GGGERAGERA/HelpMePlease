using UnityEngine;

[CreateAssetMenu(
    fileName = "LocalAnomalyData",
    menuName = "Game/Levels/Local Anomaly Data"
)]
public sealed class LocalAnomalyData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private LocalAnomalyType anomalyType;

    [Header("View")]
    [SerializeField] private string displayName;
    [TextArea(2, 4)]
    [SerializeField] private string description;
    [TextArea(1, 2)]
    [SerializeField] private string pinnedDescription;

    [Header("Selection")]
    [SerializeField, Min(0f)] private float selectionWeight = 1f;

    public LocalAnomalyType AnomalyType => anomalyType;
    public float SelectionWeight => Mathf.Max(0f, selectionWeight);
    public LevelMechanicPresentationData Presentation =>
        new(displayName, description, pinnedDescription);
}
