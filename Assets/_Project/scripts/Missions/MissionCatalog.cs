using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName="Subject42/Missions/Catalog")]
public sealed class MissionCatalog : ScriptableObject
{
    [SerializeField] private MissionDefinition[] missions=System.Array.Empty<MissionDefinition>();
    public IReadOnlyList<MissionDefinition> Missions => missions;
}
