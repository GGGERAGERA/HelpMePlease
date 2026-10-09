using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName="Subject42/Missions/Mission")]
public sealed class MissionDefinition : ScriptableObject
{
    [SerializeField] private string missionId;
    [SerializeField] private string targetSectorId;
    [SerializeField] private string title;
    [SerializeField,TextArea] private string offerText;
    [SerializeField,TextArea] private string description;
    [SerializeField] private MissionObjectiveDefinition[] objectives=System.Array.Empty<MissionObjectiveDefinition>();
    [SerializeField] private MissionRewardDefinition reward;
    [SerializeField] private SurfaceSectorContent presentation;
    [SerializeField] private int markerPriority=100;
    public string MissionId => missionId;
    public string TargetSectorId => targetSectorId;
    public string Title => title;
    public string OfferText => offerText;
    public string Description => description;
    public IReadOnlyList<MissionObjectiveDefinition> Objectives => objectives;
    public MissionRewardDefinition Reward => reward;
    public SurfaceSectorContent Presentation => presentation;
    public int MarkerPriority => markerPriority;
    public bool AvailableInProduction
    {
        get
        {
            foreach (var objective in objectives)
                if (objective.RunContent != null && !objective.RunContent.AvailableInProduction) return false;
            return true;
        }
    }
}
