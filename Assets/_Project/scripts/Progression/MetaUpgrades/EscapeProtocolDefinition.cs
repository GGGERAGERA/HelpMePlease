using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Subject42/Progression/Escape Protocol")]
public sealed class EscapeProtocolDefinition : ScriptableObject
{
    [Serializable] public sealed class Stage
    {
        public string id;
        public string title;
        public string objective;
        [Min(1)] public int cost = 5;
        [Tooltip("Future real unlock IDs. Empty stages promise no intermediate reward.")]
        public string[] unlockIds = Array.Empty<string>();
    }
    [SerializeField] private Stage[] stages = Array.Empty<Stage>();
    [SerializeField] private SurfaceSectorDefinition finalSector;
    public System.Collections.Generic.IReadOnlyList<Stage> Stages => stages;
    public SurfaceSectorDefinition FinalSector => finalSector;
    public bool IsValid
    {
        get
        {
            if (stages == null || stages.Length == 0 || finalSector == null || !finalSector.IsEscapeFinal) return false;
            var ids = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var stage in stages)
                if (stage == null || string.IsNullOrWhiteSpace(stage.id) || !ids.Add(stage.id) || stage.cost <= 0 ||
                    string.IsNullOrWhiteSpace(stage.title) || string.IsNullOrWhiteSpace(stage.objective)) return false;
            return true;
        }
    }
}
