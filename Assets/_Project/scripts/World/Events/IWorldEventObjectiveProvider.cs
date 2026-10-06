using UnityEngine;
public readonly struct WorldEventObjectiveGuidance
{
    public readonly string TitleKey, DescriptionKey;
    public readonly Vector2 Target;
    public readonly int Completed, Total;
    public WorldEventObjectiveGuidance(string title, string description, Vector2 target, int completed, int total)
    { TitleKey = title; DescriptionKey = description; Target = target; Completed = completed; Total = total; }
}
public interface IWorldEventObjectiveProvider { WorldEventObjectiveGuidance GetObjectiveGuidance(); }
